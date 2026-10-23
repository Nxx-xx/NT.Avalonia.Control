"""Independent audit of the production C# FreeText writer, never an authoring bridge.

Usage: python verify_production_writer.py --run <run-root-or-production> \
    --renderer <pdftoppm.exe> [--self-test]

The fixed output is production/independent-audit.json. Each invocation invalidates
an older success before reading evidence. pypdf inspects canonical data, font and
appearance resource references; Poppler renders the complete pages independently.
The bounded preservation fixture is not an arbitrary-PDF compatibility proof.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import re
import shutil
import subprocess
import sys
import uuid
from pathlib import Path

import pypdf
from PIL import Image, ImageChops, ImageDraw
from pypdf import PageObject, PdfReader, PdfWriter
from pypdf.generic import ArrayObject, ContentStream, DecodedStreamObject, NameObject


PDFS = ("input.pdf", "freetext-created.pdf", "freetext-edited.pdf", "atomic-saved.pdf")
EXPECTED = {
    "freetext-created.pdf": ("中文批注 ABC 123", (30, 240, 380, 290)),
    "freetext-edited.pdf": ("中文批注 编辑后 ABC 123", (40, 230, 390, 290)),
    "atomic-saved.pdf": ("中文批注 编辑后 ABC 123", (40, 230, 390, 290)),
}
ORIGINAL_TEXT = "KEEP ORIGINAL PAGE CONTENT"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def store(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def reference(value) -> tuple[int, int] | None:
    candidate = value if hasattr(value, "idnum") else getattr(value, "indirect_reference", None)
    return (candidate.idnum, candidate.generation) if candidate is not None else None


def ap_text(appearance, reader) -> str:
    # Turn the annotation stream into a temporary in-memory page for pypdf's
    # independent CMap decoder. No PDF is written and no appearance is flattened.
    page = PageObject(reader)
    page[NameObject("/Resources")] = appearance["/Resources"]
    page[NameObject("/Contents")] = appearance
    page[NameObject("/MediaBox")] = appearance["/BBox"]
    return page.extract_text() or ""


def preservation(reader: PdfReader) -> dict:
    root = reader.trailer["/Root"]
    page = reader.pages[0]
    fields = reader.get_fields() or {}
    field = fields.get("PreservedField", {})
    widgets = [value.get_object() for value in page.get("/Annots", [])
               if value.get_object().get("/Subtype") == "/Widget"]
    widget = next((value for value in widgets if value.get("/T") == "PreservedField"), None)
    appearance = widget["/AP"]["/N"].get_object() if widget is not None else None
    attachments = reader.attachments
    attachment = list(attachments.get("keep.txt", []))
    outline = reader.outline
    return {
        "pageCount": len(reader.pages),
        "pageText": page.extract_text() or "",
        "fieldCount": len(root.get("/AcroForm", {}).get("/Fields", [])),
        "fieldValue": str(field.get("/V", "")),
        "fieldDefault": str(field.get("/DV", "")),
        "widgetCount": len(widgets),
        "widgetSubtype": str(widget.get("/Subtype", "")) if widget is not None else "",
        "widgetAppearance": appearance.get_data().hex() if appearance is not None else "",
        "widgetAppearanceText": ap_text(appearance, reader).strip() if appearance is not None else "",
        "outlineTitles": [str(item.get("/Title", "")) for item in outline if isinstance(item, dict)],
        "outlinePage": reader.get_destination_page_number(outline[0]) if outline else -1,
        "attachmentBytes": [bytes(value).hex() for value in attachment],
        "customMeaning": str(root.get("/CustomExtension", {}).get("/Meaning", "")),
        "customVersion": int(root.get("/CustomExtension", {}).get("/Version", -1)),
    }


def audit(directory: Path, renderer: Path, report: dict) -> None:
    def check(identifier: str, passed: bool, detail) -> None:
        report["checks"].append({"id": identifier, "passed": bool(passed), "detail": detail})

    native_path = directory / "result.json"
    native = json.loads(native_path.read_text(encoding="utf-8-sig"))
    report["nativeReportSHA256"] = sha(native_path)
    check("native-production-path", native["Probe"] == "production" and native["ExpectationsPassed"] is True
          and native["Result"]["Status"] == "bounded-production-writer"
          and bool(native["Result"]["Checks"]) and all(value["Passed"] for value in native["Result"]["Checks"]),
          {"managedAssemblySHA256": native["ManagedAssemblySHA256"], "nativeModule": native["NativeModule"],
           "dependency": native["Result"]["Dependency"]})
    expected_hashes = {value["File"]: value["SHA256"].upper() for value in native["Result"]["Outputs"]}
    report["pdfs"] = []
    readers = {}
    for name in PDFS:
        path = directory / name
        digest = sha(path)  # Missing files are an error, rather than an implicit skipped check.
        report["pdfs"].append({"file": name, "sha256": digest, "bytes": path.stat().st_size})
        check(name + ":native-output-binding", digest == expected_hashes.get(name), digest)
        readers[name] = PdfReader(path, strict=True)

    baseline = preservation(readers["input.pdf"])
    check("input:preservation-fixture", baseline["pageCount"] == 1 and ORIGINAL_TEXT in baseline["pageText"]
          and baseline["fieldCount"] == 1 and baseline["fieldValue"] == "KEEP_FIELD_VALUE"
          and baseline["fieldDefault"] == "KEEP_DEFAULT_VALUE" and baseline["widgetCount"] == 1
          and baseline["widgetSubtype"] == "/Widget" and bool(baseline["widgetAppearance"])
          and baseline["widgetAppearanceText"] == "KEEP_FIELD_VALUE"
          and baseline["outlineTitles"] == ["KEEP_OUTLINE"] and baseline["outlinePage"] == 0
          and baseline["attachmentBytes"] == [b"KEEP_ATTACHMENT_DATA".hex()]
          and baseline["customMeaning"] == "KEEP_UNKNOWN_DICTIONARY" and baseline["customVersion"] == 7,
          baseline)

    annotation_id = None
    for name, (text, rectangle) in EXPECTED.items():
        reader = readers[name]
        preserved = preservation(reader)
        check(name + ":original-content-and-structures", preserved == baseline, preserved)
        page = reader.pages[0]
        annots = [value.get_object() for value in page.get("/Annots", [])]
        free_texts = [value for value in annots if value.get("/Subtype") == "/FreeText"]
        check(name + ":exactly-one-interactive-freetext", len(annots) == 2 and len(free_texts) == 1,
              {"annotationCount": len(annots), "freeTextCount": len(free_texts)})
        if len(free_texts) != 1:
            raise ValueError(f"{name}: cannot inspect a unique FreeText")
        annotation = free_texts[0]
        identifier = str(annotation.get("/NM", ""))
        parsed_id = uuid.UUID(identifier.removeprefix("NT.FreeText."))
        if annotation_id is None:
            annotation_id = identifier
        check(name + ":persisted-id-and-canonical-value", identifier.startswith("NT.FreeText.")
              and identifier == annotation_id and parsed_id.int != 0 and annotation.get("/Contents") == text
              and tuple(map(float, annotation["/Rect"])) == rectangle,
              {"nm": identifier, "contents": str(annotation.get("/Contents")), "rect": list(annotation["/Rect"])})
        appearance = annotation["/AP"]["/N"].get_object()
        bbox = tuple(map(float, appearance["/BBox"]))
        check(name + ":normal-appearance-shape", bool(appearance.get_data()) and appearance.get("/Subtype") == "/Form"
              and bbox == (0, 0, rectangle[2] - rectangle[0], rectangle[3] - rectangle[1])
              and tuple(map(float, appearance.get("/Matrix", [1, 0, 0, 1, 0, 0]))) == (1, 0, 0, 1, 0, 0),
              {"bbox": bbox, "contentBytes": len(appearance.get_data())})
        decoded_text = ap_text(appearance, reader).strip()
        check(name + ":independent-ToUnicode-text", decoded_text == text, decoded_text)
        fonts = appearance["/Resources"]["/Font"]
        operations = ContentStream(appearance, reader).operations
        selections = [(str(operands[0]), float(operands[1])) for operands, operator in operations if operator == b"Tf"]
        da = re.fullmatch(r"(/\S+)\s+([\d.]+)\s+Tf\s+0\s+g", str(annotation["/DA"]))
        check(name + ":DA-references-actual-font", da is not None and bool(selections)
              and (da.group(1), float(da.group(2))) in selections, {"DA": str(annotation["/DA"]), "Tf": selections})
        if da is None or not selections:
            raise ValueError(f"{name}: FreeText font selection is unavailable")
        for font_name, size in selections:
            font = fonts[font_name].get_object()
            descendant = font["/DescendantFonts"][0].get_object()
            descriptor = descendant["/FontDescriptor"].get_object()
            font_file = descriptor["/FontFile2"].get_object().get_data()
            to_unicode = font["/ToUnicode"].get_object().get_data()
            check(name + ":embedded-TrueType-and-ToUnicode:" + font_name,
                  font.get("/Subtype") == "/Type0" and descendant.get("/Subtype") == "/CIDFontType2"
                  and font_file.startswith(b"\x00\x01\x00\x00") and bool(to_unicode) and size == 18,
                  {"font": font_name, "embeddedBytes": len(font_file), "fontSHA256": hashlib.sha256(font_file).hexdigest().upper(),
                   "toUnicodeBytes": len(to_unicode)})
            ap_reference = reference(fonts.raw_get(font_name))
            page_reference = reference(page["/Resources"]["/Font"].raw_get(font_name))
            default_reference = reference(reader.trailer["/Root"]["/AcroForm"]["/DR"]["/Font"].raw_get(font_name))
            check(name + ":same-font-object-in-AP-page-AcroForm:" + font_name,
                  ap_reference is not None and ap_reference == page_reference == default_reference,
                  {"appearance": ap_reference, "page": page_reference, "AcroFormDR": default_reference})
    check("atomic-save:byte-identical-to-export", sha(directory / "atomic-saved.pdf") == sha(directory / "freetext-edited.pdf"),
          "The verified file save preserves exactly the exported production snapshot.")

    version = subprocess.run([str(renderer), "-v"], capture_output=True, text=True, timeout=15)
    if version.returncode != 0:
        raise RuntimeError(f"Cannot identify renderer: {version.stderr}")
    report["renderer"] = {"path": str(renderer), "sha256": sha(renderer), "version": (version.stderr + version.stdout).strip()}
    images = {}
    report["rasters"] = []
    for name in PDFS:
        prefix = directory / (Path(name).stem + "-poppler")
        png = prefix.with_suffix(".png")
        png.unlink(missing_ok=True)
        command = [str(renderer), "-png", "-r", "144", "-f", "1", "-singlefile", str(directory / name), str(prefix)]
        completed = subprocess.run(command, capture_output=True, text=True, timeout=30)
        prefix.with_suffix(".stderr.txt").write_text(completed.stderr, encoding="utf-8")
        # This bundled Poppler emits these two unused fallback-font diagnostics
        # even for the unchanged input AcroForm fixture. Keep them visible; an
        # unresolved AP font tag or any other diagnostic still fails the audit.
        known_environment_warnings = {"Syntax Error: No display font for 'Symbol'", "Syntax Error: No display font for 'ArialUnicode'"}
        diagnostics = [value.strip() for value in completed.stderr.splitlines() if value.strip()]
        unexpected = [value for value in diagnostics if value not in known_environment_warnings]
        check(name + ":independent-renderer-success", completed.returncode == 0 and not unexpected and png.is_file(),
              {"exit": completed.returncode, "diagnostics": diagnostics, "unexpectedDiagnostics": unexpected,
               "environmentLimitation": "Bundled fallback fonts Symbol/ArialUnicode unavailable; unchanged input emits the same warnings. FreeText explicitly embeds FontFile2 and passes Unicode/resource checks." if diagnostics else None})
        if completed.returncode != 0 or not png.is_file():
            raise RuntimeError(f"Poppler did not render {name}")
        with Image.open(png) as image:
            images[name] = image.convert("RGB")
        report["rasters"].append({"pdf": name, "pdfSHA256": sha(directory / name), "png": png.name,
                                  "pngSHA256": sha(png), "size": list(images[name].size)})
    original = images["input.pdf"]
    if original.size != (800, 800):
        raise ValueError(f"Unexpected independent raster dimensions: {original.size}")
    # Only the union of the old and new annotation rectangles may differ. An
    # extra 2 pixels cover edge antialiasing; page text and widget pixels remain
    # inside the compared region and receive their own exact ROI checks too.
    exclusion = (58, 218, 782, 342)
    for name, (_, rectangle) in EXPECTED.items():
        image = images[name]
        if image.size != original.size:
            raise ValueError(f"Page size changed in {name}")
        difference = ImageChops.difference(original, image)
        difference.paste((0, 0, 0), exclusion)
        check(name + ":unchanged-pixels-outside-annotation-union", difference.getbbox() is None, {"excludedPixels": exclusion})
        for label, roi in [("page-original", (40, 60, 760, 120)), ("form-field", (50, 630, 630, 710))]:
            check(name + ":" + label + "-pixels-identical", ImageChops.difference(original.crop(roi), image.crop(roi)).getbbox() is None,
                  {"roi": roi})
        left, _, _, top = rectangle
        chinese_roi = (int((left + 1) * 2), int((400 - top + 1) * 2), int((left + 78) * 2), int((400 - top + 31) * 2))
        ink = sum(value < 160 for value in image.crop(chinese_roi).convert("L").tobytes())
        baseline_ink = sum(value < 160 for value in original.crop(chinese_roi).convert("L").tobytes())
        check(name + ":visible-CJK-appearance", ink > 200 and baseline_ink == 0, {"roi": chinese_roi, "darkPixels": ink, "inputDarkPixels": baseline_ink})
    check("updated-appearance-changes-visible-pixels", ImageChops.difference(images["freetext-created.pdf"], images["freetext-edited.pdf"]).getbbox() is not None,
          "The updated AP is visible after the persisted annotation ID and Contents change.")
    sheet = Image.new("RGB", (1600, 1660), "#dddddd")
    draw = ImageDraw.Draw(sheet)
    for index, name in enumerate(PDFS):
        x, y = (index % 2) * 800, (index // 2) * 830
        draw.text((x + 10, y + 8), name, fill="black")
        sheet.paste(images[name], (x, y + 30))
    contact = directory / "production-contact-sheet.png"
    sheet.save(contact)
    report["contactSheet"] = {"file": contact.name, "sha256": sha(contact), "visualReview": "separate human/agent inspection; not inferred from automated pixel checks"}


def negative_controls(directory: Path, renderer: Path, report: dict) -> None:
    root = directory / "self-tests" / uuid.uuid4().hex
    root.mkdir(parents=True)
    controls = []
    for name in ("missing-output", "tampered-bytes", "changed-original-value", "removed-field"):
        case = root / name
        case.mkdir()
        for filename in (*PDFS, "result.json"):
            shutil.copyfile(directory / filename, case / filename)
        # Prove that a previous pass cannot survive a failed invocation.
        store(case / "independent-audit.json", {"passed": True, "status": "stale-success-sentinel"})
        if name == "missing-output":
            (case / "freetext-created.pdf").unlink()
        elif name == "tampered-bytes":
            with (case / "freetext-created.pdf").open("ab") as stream:
                stream.write(b"\n% tampered bytes after native evidence\n")
        else:
            target = case / "freetext-created.pdf"
            writer = PdfWriter(clone_from=target)
            if name == "changed-original-value":
                content = writer.pages[0].get_contents().get_data().replace(ORIGINAL_TEXT.encode(), b"WRONG ORIGINAL PAGE VALUE")
                stream = DecodedStreamObject()
                stream.set_data(content)
                writer.pages[0][NameObject("/Contents")] = writer._add_object(stream)
            else:
                writer._root_object["/AcroForm"][NameObject("/Fields")] = ArrayObject()
            temporary = case / "mutated.pdf"
            with temporary.open("wb") as stream:
                writer.write(stream)
            writer.close()
            temporary.replace(target)
            # Forging the local expected hash must not defeat semantic checks.
            native = json.loads((case / "result.json").read_text(encoding="utf-8-sig"))
            for value in native["Result"]["Outputs"]:
                if value["File"] == target.name:
                    value["SHA256"] = sha(target)
            store(case / "result.json", native)
        completed = subprocess.run([sys.executable, str(Path(__file__).resolve()), "--run", str(case), "--renderer", str(renderer)],
                                   capture_output=True, text=True, timeout=60)
        failure_report = json.loads((case / "independent-audit.json").read_text(encoding="utf-8"))
        rejected = [value["id"] for value in failure_report.get("checks", []) if not value["passed"]]
        expected_rejection = (failure_report.get("error", "").startswith("FileNotFoundError:") if name == "missing-output"
                              else "freetext-created.pdf:native-output-binding" in rejected if name == "tampered-bytes"
                              else "freetext-created.pdf:original-content-and-structures" in rejected)
        passed = (completed.returncode != 0 and failure_report.get("passed") is False
                  and failure_report.get("status") != "stale-success-sentinel" and expected_rejection)
        controls.append({"id": name, "passed": passed, "exit": completed.returncode,
                         "report": str((case / "independent-audit.json").relative_to(directory)),
                         "reportSHA256": sha(case / "independent-audit.json"),
                         "rejectedChecks": rejected, "expectedRejectionDetected": expected_rejection,
                         "error": failure_report.get("error")})
    report["negativeControls"] = controls
    report["checks"].append({"id": "oracle-negative-controls-and-stale-pass-protection", "passed": all(value["passed"] for value in controls),
                             "detail": {"cases": len(controls), "directory": str(root.relative_to(directory))}})


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True, help="Run root, or its production directory")
    parser.add_argument("--renderer", required=True, help="Absolute path to independent Poppler pdftoppm executable")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    supplied = Path(args.run).resolve()
    directory = supplied / "production" if (supplied / "production").is_dir() else supplied
    directory.mkdir(parents=True, exist_ok=True)
    target = directory / "independent-audit.json"
    report = {"status": "verification-incomplete", "passed": False, "verifiedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
              "scope": "bounded production C# PDFsharp/PDFium writer; Python is only an independent verification tool",
              "scriptSHA256": sha(Path(__file__).resolve()), "pypdfVersion": pypdf.__version__, "checks": [],
              "limitations": ["One unsigned, unencrypted preservation fixture; no arbitrary-PDF preservation guarantee.",
                              "Full explicit static TrueType embedding; no automatic font subsetting or complex-script shaping.",
                              "No Linux/Kylin physical, official-runtime, UI/IME, external-editor interaction or performance parity claim."]}
    store(target, report)
    try:
        renderer = Path(args.renderer).resolve(strict=True)
        audit(directory, renderer, report)
        if args.self_test:
            negative_controls(directory, renderer, report)
        report["passed"] = bool(report["checks"]) and all(value["passed"] for value in report["checks"])
        report["status"] = "bounded-production-writer-independently-confirmed" if report["passed"] else "verification-failed"
    except Exception as error:
        report["status"] = "verification-error"
        report["error"] = f"{type(error).__name__}: {error}"
        report["passed"] = False
    store(target, report)
    print(json.dumps({"report": str(target), "passed": report["passed"], "checks": len(report["checks"]),
                      "failed": [value["id"] for value in report["checks"] if not value["passed"]], "error": report.get("error")}, ensure_ascii=True))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
