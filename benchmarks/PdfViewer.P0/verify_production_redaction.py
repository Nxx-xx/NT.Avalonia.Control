"""Independent public production redaction audit; this script never authors positive outputs.

Usage: python verify_production_redaction.py --run <run-root> --renderer <pdftoppm> --self-test
The fixed report is production-redaction/independent-audit.json. All active xref
objects and decoded streams are inspected, including unreachable current objects.
Shared content is checked per page: the unselected page must retain its Form.
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
import xml.etree.ElementTree as ET
from pathlib import Path

import pypdf
from PIL import Image, ImageChops, ImageDraw
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, DecodedStreamObject, IndirectObject, NameObject, TextStringObject


PDFS = ("input.pdf", "sanitized.pdf", "redacted.pdf", "atomic-saved.pdf")
HIDDEN = b"PIPE_HIDDEN_"
TEXT = "PIPE_TARGET_TEXT_82FA"
SHARED = "PIPE_SHARED_FORM_3C9D"
IMAGE = b"PIPE_TARGET_IMAGE_64E1"
REGIONS = [(145, 330, 250, 30), (35, 205, 70, 50), (125, 125, 245, 40), (35, 75, 90, 40)]
NATIVE_CHECKS = {"sanitize-public-session-commit", "sanitize-result-binds-snapshot", "already-dirty-session-rollback",
                 "pipeline-public-session-commit", "pipeline-result-binds-snapshot", "target-text-and-form-instance-removed",
                 "retained-text-and-shared-page-preserved", "pipeline-atomic-file-save", "partial-text-refused-before-commit",
                 "partial-image-refused-before-commit", "partial-form-refused-before-commit", "partial-path-refused-before-commit",
                 "later-region-failure-atomic", "pre-cancelled-pipeline-atomic", "unknown-policy-refused"}
FORBIDDEN = {"/AcroForm", "/Outlines", "/Names", "/StructTreeRoot", "/OpenAction", "/AA", "/Annots",
             "/Thumb", "/PieceInfo", "/ActualText", "/Alt", "/EmbeddedFiles", "/JavaScript", "/XFA",
             "/CustomExtension", "/CustomPageSecret", "/CustomFormSecret", "/CustomSecret"}


def technical_metadata(reader):
    root = reader.trailer["/Root"]
    info_reference = reader.trailer.get("/Info", {})
    info = info_reference.get_object() if hasattr(info_reference, "get_object") else info_reference
    allowed_info = {"/Producer", "/Creator", "/CreationDate", "/ModDate"}
    valid = set(map(str, root)).issubset({"/Type", "/Pages", "/Version", "/Metadata"})
    valid = valid and set(map(str, info)).issubset(allowed_info)
    for key, value in info.items():
        text = str(value)
        valid = valid and (text == "PDFsharp 6.2.4" if key == "/Producer"
                           else text in ("", "PDFsharp 6.2.4 (www.pdfsharp.com)") if key == "/Creator"
                           else re.fullmatch(r"D:\d{14}(?:Z|[+-]\d{2}'\d{2}')?", text) is not None)
    metadata = root.get("/Metadata")
    details = {"catalogKeys": sorted(map(str, root)), "info": {str(key): str(value) for key, value in info.items()}}
    if metadata is None:
        return valid, details
    tree = ET.fromstring(metadata.get_object().get_data())
    allowed_tags = {"xmpmeta", "RDF", "Description", "Producer", "Keywords", "title", "Alt", "li", "creator", "Seq", "description",
                    "CreatorTool", "CreateDate", "ModifyDate", "DocumentID", "InstanceID"}
    leaves = []
    for element in tree.iter():
        local = element.tag.rsplit("}", 1)[-1]
        valid = valid and local in allowed_tags
        for key, value in element.attrib.items():
            name = key.rsplit("}", 1)[-1]
            valid = valid and ((name == "xmptk" and value == "3.1-701") or (name == "about" and value == "") or (name == "lang" and value == "x-default"))
        if list(element):
            valid = valid and not (element.text or "").strip()
            continue
        text = (element.text or "").strip()
        leaves.append({"tag": element.tag, "text": text})
        if local == "Producer":
            valid = valid and text == "PDFsharp 6.2.4"
        elif local == "CreatorTool":
            valid = valid and text in ("", "PDFsharp 6.2.4 (www.pdfsharp.com)")
        elif local in ("CreateDate", "ModifyDate"):
            try:
                dt.datetime.fromisoformat(text)
            except ValueError:
                valid = False
        elif local in ("DocumentID", "InstanceID"):
            try:
                valid = valid and text.startswith("uuid:") and uuid.UUID(text.removeprefix("uuid:")).int != 0
            except ValueError:
                valid = False
        else:
            valid = valid and text == ""
    details["xmpLeaves"] = leaves
    return valid, details


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def store(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def all_objects(reader: PdfReader):
    seen = set()
    for generation, numbers in reader.xref.items():
        if generation == 65535:
            continue
        for number in numbers:
            if number == 0 or (number, generation) in seen:
                continue
            seen.add((number, generation))
            yield f"{number} {generation}", reader.get_object(IndirectObject(number, generation, reader))
    for number in reader.xref_objStm:
        if (number, 0) not in seen:
            seen.add((number, 0))
            yield f"{number} 0", reader.get_object(IndirectObject(number, 0, reader))


def scan(reader: PdfReader) -> dict:
    """Decode every active xref stream and inspect every string; no root-only traversal."""
    hits, forbidden, stream_count, object_count = [], [], 0, 0
    payloads = []
    visited = set()
    page_contents = set()
    for page in reader.pages:
        contents = page.get("/Contents")
        contents = contents.get_object() if hasattr(contents, "get_object") else contents
        parts = contents if isinstance(contents, (list, tuple)) else [contents]
        for part in parts:
            part = part.get_object() if hasattr(part, "get_object") else part
            if part is not None:
                page_contents.add(id(part))

    def walk(value, path: str):
        nonlocal stream_count
        if isinstance(value, IndirectObject):
            return  # Enumerated separately through the complete current xref.
        if isinstance(value, dict):
            if id(value) in visited:
                return
            visited.add(id(value))
            for key, child in value.items():
                if str(key) in FORBIDDEN:
                    forbidden.append(path + str(key))
                walk(child, path + str(key))
            if hasattr(value, "get_data"):
                decoded = value.get_data()  # An undecodable stream fails the invocation.
                stream_count += 1
                payloads.append(decoded)
                if HIDDEN in decoded:
                    hits.append(path + ":decoded-stream")
                if id(value) in page_contents or value.get("/Subtype") == "/Form":
                    # Parse string operands as PDF syntax too. Hex-encoded Tj/TJ
                    # text is semantically sensitive even when its marker does
                    # not occur literally in a decoded stream's byte sequence.
                    for index, (operands, _) in enumerate(ContentStream(value, reader).operations):
                        walk(operands, f"{path}:content-operands[{index}]")
        elif isinstance(value, (list, tuple)):
            for index, child in enumerate(value):
                walk(child, f"{path}[{index}]")
        elif isinstance(value, (str, bytes)):
            encoded = value.encode("utf-8") if isinstance(value, str) else bytes(value)
            payloads.append(encoded)
            if HIDDEN in encoded or HIDDEN.decode() in str(value):
                hits.append(path + ":string")

    for label, value in all_objects(reader):
        object_count += 1
        walk(value, label)
    return {"objects": object_count, "decodedStreams": stream_count, "hiddenHits": hits,
            "forbiddenSurfaces": forbidden, "targetTextInActiveObjects": any(TEXT.encode() in data for data in payloads),
            "targetImageInActiveObjects": any(IMAGE in data for data in payloads)}


def content_geometry(page, reader):
    operations = ContentStream(page.get_contents(), reader).operations
    texts = []
    for operands, operator in operations:
        if operator == b"Tj":
            texts.append(str(operands[0]))
        elif operator == b"TJ":
            texts.extend(str(item) for item in operands[0] if isinstance(item, TextStringObject))
    return {"operators": [operator.decode("ascii", "replace") for _, operator in operations], "textOperands": texts}


def resource_audit(page, reader):
    records = []
    visited = set()

    def walk(container, resources, label):
        marker = getattr(container, "indirect_reference", None)
        key = (marker.idnum, marker.generation) if marker is not None else id(container)
        if key in visited:
            return
        visited.add(key)
        operations = ContentStream(container, reader).operations
        used = {"/Font": set(), "/XObject": set(), "/ExtGState": set(), "/Pattern": set(), "/ColorSpace": set(), "/Shading": set(), "/Properties": set()}
        for operands, operator in operations:
            category = {b"Tf": "/Font", b"Do": "/XObject", b"gs": "/ExtGState", b"sh": "/Shading"}.get(operator)
            if category is not None:
                used[category].add(str(operands[0]))
            elif operator in (b"CS", b"cs") and operands and str(operands[0]) not in ("/DeviceGray", "/DeviceRGB", "/DeviceCMYK", "/Pattern"):
                used["/ColorSpace"].add(str(operands[0]))
            elif operator in (b"SCN", b"scn") and operands and isinstance(operands[-1], NameObject):
                used["/Pattern"].add(str(operands[-1]))
            elif operator in (b"BDC", b"DP") and len(operands) > 1 and isinstance(operands[1], NameObject):
                used["/Properties"].add(str(operands[1]))
        for category, names in used.items():
            defined = set(map(str, resources.get(category, {})))
            records.append({"path": label + category, "defined": sorted(defined), "used": sorted(names), "exact": names == defined})
        for name in used["/XObject"]:
            obj = resources["/XObject"][name].get_object()
            if obj.get("/Subtype") == "/Form":
                walk(obj, obj.get("/Resources", resources), label + "/XObject" + name)

    walk(page.get_contents(), page.get("/Resources", {}), "page")
    return records


def audit(directory: Path, renderer: Path, report: dict) -> None:
    def check(identifier, passed, detail):
        report["checks"].append({"id": identifier, "passed": bool(passed), "detail": detail})

    native_path = directory / "result.json"
    native = json.loads(native_path.read_text(encoding="utf-8-sig"))
    report["nativeReportSHA256"] = sha(native_path)
    check("public-production-probe", native["Probe"] == "production-redaction" and native["ExpectationsPassed"] is True
          and native["Result"]["Status"] == "bounded-production-redaction-pipeline"
          and len(native["Result"]["Checks"]) == len(NATIVE_CHECKS)
          and {item["Id"] for item in native["Result"]["Checks"]} == NATIVE_CHECKS
          and all(item["Passed"] for item in native["Result"]["Checks"]),
          {"functionalAssemblySHA256": native["Result"]["FunctionalAssemblySHA256"], "nativeModule": native["NativeModule"]})
    declared = {value["File"]: value["SHA256"].upper() for value in native["Result"]["Outputs"]}
    check("complete-unique-output-manifest", len(native["Result"]["Outputs"]) == len(PDFS) and set(declared) == set(PDFS), sorted(declared))
    report["pdfs"], readers = [], {}
    for name in PDFS:
        path = directory / name
        digest = sha(path)
        check(name + ":native-output-binding", digest == declared.get(name), digest)
        report["pdfs"].append({"file": name, "sha256": digest, "bytes": path.stat().st_size})
        readers[name] = PdfReader(path, strict=True)
    original = readers["input.pdf"]
    original_scan = scan(original)
    original_text = [page.extract_text() or "" for page in original.pages]
    check("input:authored-sensitive-surfaces", len(original.pages) == 2 and len(original_scan["hiddenHits"]) >= 15
          and original_scan["targetTextInActiveObjects"] and original_scan["targetImageInActiveObjects"]
          and TEXT in original_text[0] and SHARED in original_text[0] and SHARED in original_text[1]
          and b"PIPE_HIDDEN_OLD_REVISION" in (directory / "input.pdf").read_bytes()
          and b"/Prev" in (directory / "input.pdf").read_bytes(), {**original_scan, "pageText": original_text})

    for name in PDFS[1:]:
        reader = readers[name]
        data = (directory / name).read_bytes()
        inspection = scan(reader)
        metadata_valid, metadata_details = technical_metadata(reader)
        check(name + ":only-regenerated-technical-metadata", metadata_valid, metadata_details)
        check(name + ":all-active-xref-hidden-data-removed", not inspection["hiddenHits"] and HIDDEN not in data
              and not inspection["forbiddenSurfaces"], inspection)
        check(name + ":single-complete-rewrite", b"/Prev" not in data and len(re.findall(rb"startxref\s+\d+\s+%%EOF", data)) == 1,
              {"startxrefCount": len(re.findall(rb"startxref\s+\d+\s+%%EOF", data)), "hasPrev": b"/Prev" in data})
        check(name + ":page-tree-preserved", len(reader.pages) == 2 and all(tuple(map(float, page.mediabox)) == (0, 0, 400, 400) for page in reader.pages),
              {"pages": len(reader.pages), "mediaBoxes": [list(page.mediabox) for page in reader.pages]})
        for index, page in enumerate(reader.pages):
            resources = resource_audit(page, reader)
            check(name + f":page{index}:no-unused-resources", bool(resources) and all(value["exact"] for value in resources), resources)
        text = [page.extract_text() or "" for page in reader.pages]
        check(name + ":retained-page1-text-and-shared-resource", text[1] == original_text[1] and SHARED in text[1], text[1])
        check(name + ":retained-neighbor-text", "KEEP_LEFT" in text[0] and "KEEP_RIGHT" in text[0], text[0])
        if name == "sanitized.pdf":
            check(name + ":page-content-retained", text == original_text and inspection["targetTextInActiveObjects"]
                  and inspection["targetImageInActiveObjects"], text)
        else:
            check(name + ":selected-content-gone", TEXT not in text[0] and SHARED not in text[0]
                  and not inspection["targetTextInActiveObjects"] and not inspection["targetImageInActiveObjects"],
                  {"page0Text": text[0], "page0Content": content_geometry(reader.pages[0], reader),
                   "targetTextInActiveObjects": inspection["targetTextInActiveObjects"], "targetImageInActiveObjects": inspection["targetImageInActiveObjects"]})
    check("atomic-save:exact-exported-bytes", sha(directory / "redacted.pdf") == sha(directory / "atomic-saved.pdf"), "Save does not reauthor the already audited snapshot.")

    version = subprocess.run([str(renderer), "-v"], capture_output=True, text=True, timeout=15)
    if version.returncode:
        raise RuntimeError("Cannot identify independent Poppler renderer")
    report["renderer"] = {"path": str(renderer), "sha256": sha(renderer), "version": (version.stderr + version.stdout).strip()}
    report["rasters"], images = [], {}
    for name in PDFS:
        for page_index in (0, 1):
            prefix = directory / f"{Path(name).stem}-page{page_index}-poppler"
            png = prefix.with_suffix(".png")
            png.unlink(missing_ok=True)
            completed = subprocess.run([str(renderer), "-png", "-r", "144", "-f", str(page_index + 1), "-singlefile", str(directory / name), str(prefix)],
                                       capture_output=True, text=True, timeout=30)
            prefix.with_suffix(".stderr.txt").write_text(completed.stderr, encoding="utf-8")
            diagnostics = [line.strip() for line in completed.stderr.splitlines() if line.strip()]
            known = {"Syntax Error: No display font for 'Symbol'", "Syntax Error: No display font for 'ArialUnicode'"}
            check(name + f":page{page_index}:independent-render", completed.returncode == 0 and png.is_file()
                  and not [line for line in diagnostics if line not in known], {"exit": completed.returncode, "diagnostics": diagnostics})
            with Image.open(png) as image:
                images[(name, page_index)] = image.convert("RGB")
            if images[(name, page_index)].size != (800, 800):
                raise ValueError("Unexpected page raster dimensions")
            report["rasters"].append({"pdf": name, "page": page_index, "pdfSHA256": sha(directory / name), "png": png.name, "pngSHA256": sha(png)})
    for name in PDFS[1:]:
        check(name + ":page1-all-pixels-preserved", ImageChops.difference(images[("input.pdf", 1)], images[(name, 1)]).getbbox() is None,
              "All unselected second-page pixels, including shared Form text, must be identical.")
        difference = ImageChops.difference(images[("input.pdf", 0)], images[(name, 0)])
        if name == "sanitized.pdf":
            check(name + ":all-page0-pixels-preserved", difference.getbbox() is None,
                  "Invisible offpage annotations and hidden structures do not change page pixels.")
            continue
        exclusions = []
        for left, bottom, width, height in REGIONS:
            box = (int(left * 2) - 2, int((400 - bottom - height) * 2) - 2,
                   int((left + width) * 2) + 2, int((400 - bottom) * 2) + 2)
            exclusions.append(box)
            difference.paste((0, 0, 0), box)
        check(name + ":all-unselected-page0-pixels-preserved", difference.getbbox() is None, {"excludedPixels": exclusions})
        for label, box in [("left-text", (40, 40, 250, 85)), ("right-text", (40, 190, 300, 230)), ("blue-path", (590, 570, 710, 650))]:
            check(name + ":" + label + "-pixels-exact", ImageChops.difference(images[("input.pdf", 0)].crop(box), images[(name, 0)].crop(box)).getbbox() is None, box)
        for label, (left, bottom, width, height) in zip(("text", "image", "form", "path"), REGIONS):
            box = (int(left * 2), int((400 - bottom - height) * 2), int((left + width) * 2), int((400 - bottom) * 2))
            crop = images[(name, 0)].crop(box)
            check(name + ":" + label + "-target-pixels-blank", crop.getextrema() == ((255, 255), (255, 255), (255, 255)), box)
    sheet = Image.new("RGB", (1600, 2460), "#dddddd")
    draw = ImageDraw.Draw(sheet)
    for row, name in enumerate(PDFS[:3]):
        for page_index in (0, 1):
            x, y = page_index * 800, row * 820
            draw.text((x + 5, y + 5), f"{name} / page {page_index}", fill="black")
            sheet.paste(images[(name, page_index)], (x, y + 20))
    contact = directory / "production-redaction-contact-sheet.png"
    sheet.save(contact)
    report["contactSheet"] = {"file": contact.name, "sha256": sha(contact), "visualReview": "separate inspection required"}


def negative_controls(directory: Path, renderer: Path, report: dict):
    root = directory / "self-tests" / uuid.uuid4().hex
    root.mkdir(parents=True)
    controls = []
    for name in ("missing-output", "corrupt-pdf", "byte-tamper", "forged-hidden-leak", "forged-geometry-drift", "forged-missing-case"):
        case = root / name
        case.mkdir()
        for filename in (*PDFS, "result.json"):
            shutil.copyfile(directory / filename, case / filename)
        store(case / "independent-audit.json", {"passed": True, "status": "stale-success-sentinel"})
        target = case / "redacted.pdf"
        if name == "missing-output":
            target.unlink()
        elif name == "corrupt-pdf":
            target.write_bytes(b"This is not a PDF. A matching local hash is insufficient.\n")
            native = json.loads((case / "result.json").read_text(encoding="utf-8-sig"))
            for item in native["Result"]["Outputs"]:
                if item["File"] == target.name:
                    item["SHA256"] = sha(target)
            store(case / "result.json", native)
        elif name == "byte-tamper":
            with target.open("ab") as stream:
                stream.write(b"\n% modified after evidence was recorded\n")
        elif name == "forged-missing-case":
            native = json.loads((case / "result.json").read_text(encoding="utf-8-sig"))
            native["Result"]["Outputs"] = [item for item in native["Result"]["Outputs"] if item["File"] != target.name]
            store(case / "result.json", native)
        else:
            writer = PdfWriter(clone_from=target)
            if name == "forged-hidden-leak":
                # An unreachable active xref stream proves traversal is not root-only.
                hidden = DecodedStreamObject()
                hidden[NameObject("/Type")] = NameObject("/XObject")
                hidden[NameObject("/Subtype")] = NameObject("/Form")
                hidden.set_data(b"BT <" + b"PIPE_HIDDEN_NEGATIVE_CONTROL_IN_UNREACHABLE_STREAM".hex().encode() + b"> Tj ET")
                writer._add_object(hidden)
            else:
                original = writer.pages[0].get_contents().get_data()
                stream = DecodedStreamObject()
                stream.set_data(b"q 1 0 0 1 8 0 cm\n" + original + b"\nQ")
                writer.pages[0][NameObject("/Contents")] = writer._add_object(stream)
            temporary = case / "mutated.pdf"
            with temporary.open("wb") as stream:
                writer.write(stream)
            writer.close()
            temporary.replace(target)
            # Local report/hash forgery must not disable semantic checks.
            native = json.loads((case / "result.json").read_text(encoding="utf-8-sig"))
            for item in native["Result"]["Outputs"]:
                if item["File"] == target.name:
                    item["SHA256"] = sha(target)
            store(case / "result.json", native)
        completed = subprocess.run([sys.executable, str(Path(__file__).resolve()), "--run", str(case), "--renderer", str(renderer)],
                                   capture_output=True, text=True, timeout=90)
        failed = json.loads((case / "independent-audit.json").read_text(encoding="utf-8"))
        rejected = [item["id"] for item in failed.get("checks", []) if not item["passed"]]
        expected = (failed.get("error", "").startswith("FileNotFoundError:") if name == "missing-output"
                    else failed.get("error", "").startswith("PdfReadError:") if name == "corrupt-pdf"
                    else "redacted.pdf:all-active-xref-hidden-data-removed" in rejected if name == "forged-hidden-leak"
                    else "redacted.pdf:all-unselected-page0-pixels-preserved" in rejected if name == "forged-geometry-drift"
                    else "redacted.pdf:native-output-binding" in rejected)
        controls.append({"id": name, "passed": completed.returncode != 0 and failed.get("passed") is False
                         and failed.get("status") != "stale-success-sentinel" and expected,
                         "exit": completed.returncode, "expectedRejectionDetected": expected, "rejectedChecks": rejected,
                         "report": str((case / "independent-audit.json").relative_to(directory)), "reportSHA256": sha(case / "independent-audit.json")})
    report["negativeControls"] = controls
    report["checks"].append({"id": "oracle-negative-controls-and-stale-pass-protection", "passed": all(item["passed"] for item in controls), "detail": {"cases": len(controls)}})


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True)
    parser.add_argument("--renderer", required=True)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    directory = Path(args.run).resolve()
    if (directory / "production-redaction").is_dir():
        directory /= "production-redaction"
    directory.mkdir(parents=True, exist_ok=True)
    target = directory / "independent-audit.json"
    report = {"status": "verification-incomplete", "passed": False, "verifiedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
              "scope": "bounded public C# production sanitizer and whole-object region redaction; Python is an independent verifier",
              "scriptSHA256": sha(Path(__file__).resolve()), "pypdfVersion": pypdf.__version__, "checks": [],
              "limitations": ["One authored two-page unsigned/unencrypted corpus does not prove arbitrary-PDF compatibility.",
                              "Strict removal of non-page data is explicitly destructive; visible annotation appearances are not preserved by that policy.",
                              "Partial text/image/path/Form editing is rejected; Linux/Kylin and performance parity remain unverified."]}
    store(target, report)  # An old pass is invalidated before any input is read.
    try:
        renderer = Path(args.renderer).resolve(strict=True)
        audit(directory, renderer, report)
        if args.self_test:
            negative_controls(directory, renderer, report)
        report["passed"] = bool(report["checks"]) and all(item["passed"] for item in report["checks"])
        report["status"] = "bounded-production-redaction-independently-confirmed" if report["passed"] else "verification-failed"
    except Exception as error:
        report["status"], report["passed"], report["error"] = "verification-error", False, f"{type(error).__name__}: {error}"
    store(target, report)
    print(json.dumps({"report": str(target), "passed": report["passed"], "checks": len(report["checks"]),
                      "failed": [item["id"] for item in report["checks"] if not item["passed"]], "error": report.get("error")}, ensure_ascii=True))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
