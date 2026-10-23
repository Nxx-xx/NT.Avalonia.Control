"""Independent CJK AcroForm font, AP text, canonical value and raster oracle.

The C# experiment embeds full static TrueType fonts for future form editing.
This script never authors positive output and never relies on system CJK fonts.
"""
from __future__ import annotations
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid
import pypdf
from PIL import Image, ImageChops, ImageDraw
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DecodedStreamObject, NameObject
from forms_verifier_common import (appearance_text, appearances_font_usage, canonical_fields, choice_options,
    list_selection_pixels, normal_appearance, normalized_value, pdf_roi, reference, render, same_pixels, sha, store, text_lines)
from verify_font_subset import cid_widths, cmap_pairs

EXPECTED = {
    "lxgw": {"font": "LXGWWenKai-Regular.ttf", "options": ["中文", "日本", "ABC"],
             "saved": {"CjkText": "中文日本ABC", "CjkMultiline": "中文第一行\r日本第二行", "CjkList": "日本"},
             "reedited": {"CjkText": "兼容汉字ABC", "CjkMultiline": "第二次中文\r日本兼容", "CjkList": "ABC"}},
    "zen": {"font": "ZenKakuGothicNew-Regular.ttf", "options": ["中文", "日本", "ABC"],
            "saved": {"CjkText": "中文日本ABC", "CjkMultiline": "中文第一行\r日本第二行", "CjkList": "日本"},
            "reedited": {"CjkText": "互換漢字ABC", "CjkMultiline": "第二次中文\r日本兼容", "CjkList": "ABC"}},
    "gowun": {"font": "GowunDodum-Regular.ttf", "options": ["한글", "글꼴", "ABC"],
              "saved": {"CjkText": "한글입력ABC", "CjkMultiline": "첫째 줄\r둘째 줄", "CjkList": "글꼴"},
              "reedited": {"CjkText": "새문자검증ABC", "CjkMultiline": "다시 한글\r글꼴 확인", "CjkList": "ABC"}},
}
FIELDS = {"CjkText", "CjkMultiline", "CjkList"}
RECTANGLES = {"CjkText": (40, 300, 360, 330), "CjkMultiline": (40, 180, 360, 270), "CjkList": (40, 60, 250, 155)}


def font_info(raw):
    font = raw.get_object()
    descendant = font["/DescendantFonts"][0].get_object()
    descriptor = descendant["/FontDescriptor"].get_object()
    data = descriptor["/FontFile2"].get_object().get_data()
    return font, descendant, data


def audit(directory, renderer, font_python, report, fail_fast=False):
    def check(identifier, passed, detail):
        report["checks"].append({"id": identifier, "passed": bool(passed), "detail": detail})
    native_path = directory / "result.json"
    native = json.loads(native_path.read_text(encoding="utf-8-sig"))
    result = native["Result"]
    report["nativeReportSHA256"] = sha(native_path)
    cases = result["Cases"]
    native_checks = result["Checks"]
    native_ids = {family + ":" + phase + ":" + suffix for family in EXPECTED for phase in ("saved", "reedited")
                  for suffix in ("characters-accepted", "canonical-values", "native-fallback-counterexample", "reopen-values", "appearances")}
    check("fixed-native-font-and-event-matrix", native["Probe"] == "cjk-forms" and native["ExpectationsPassed"] is True
          and len(cases) == len(EXPECTED) and {case["Id"] for case in cases} == set(EXPECTED)
          and len(native_checks) == len({item["Id"] for item in native_checks}) == len(native_ids)
          and {item["Id"] for item in native_checks} == native_ids and all(item["Passed"] for item in native_checks),
          {"managedAssemblySHA256": native["ManagedAssemblySHA256"], "nativeModule": native["NativeModule"], "status": result["Status"]})
    final_files = {family + "-" + stage + ".pdf" for family in EXPECTED for stage in ("input", "saved", "reedited")}
    native_files = {family + "-native-" + stage + ".pdf" for family in EXPECTED for stage in ("saved", "reedited")}
    expected_files = final_files | native_files
    outputs = {item["File"]: item["SHA256"] for item in result["Outputs"]}
    check("complete-output-matrix", len(result["Outputs"]) == len(outputs) == len(expected_files) and set(outputs) == expected_files, sorted(outputs))
    readers, graphs = {}, {}
    report["pdfs"], report["fonts"] = [], []
    source_manifest = Path(__file__).parent / "Fixtures" / "font-subset-sources.json"
    sources = {item["File"]: item for item in json.loads(source_manifest.read_text(encoding="utf-8-sig"))["Files"] if item["File"].endswith(".ttf")}
    report["sourceManifestSHA256"] = sha(source_manifest)
    check("font-source-manifest-binding-and-explicit-bridge-status", result["FontSourceManifestSHA256"] == sha(source_manifest)
          and result["NativeAutomaticAppearanceStatus"] == "rejected-unembedded-fallback"
          and result["Status"] == "native-canonical-managed-appearance-acroform-experiment",
          {"manifestSHA256": result["FontSourceManifestSHA256"], "nativeAutomaticAppearance": result["NativeAutomaticAppearanceStatus"]})
    jobs, font_mappings = [], []
    for case in cases:
        family = case["Id"]
        expected = EXPECTED[family]
        source_path = Path(case["FontPath"])
        if not source_path.is_absolute():
            source_path = directory / source_path
        manifest = sources[expected["font"]]
        check(family + ":frozen-real-full-font-source", Path(case["FontFile"]).name == expected["font"]
              and sha(source_path) == case["SourceSHA256"] == manifest["SHA256"] and source_path.stat().st_size == case["SourceBytes"] == manifest["Bytes"],
              {"path": str(source_path), "sha256": sha(source_path), "bytes": source_path.stat().st_size})
        all_text = "".join(expected[stage][field] for stage in ("saved", "reedited") for field in FIELDS) + "".join(expected["options"])
        points = sorted(set(ord(char) for char in all_text if char not in "\r\n"))
        jobs.append({"id": family, "path": str(source_path), "codepoints": points})
        first_points = set(expected["saved"]["CjkText"] + expected["saved"]["CjkMultiline"] + "".join(expected["options"]))
        new_points = sorted(set(expected["reedited"]["CjkText"] + expected["reedited"]["CjkMultiline"]) - first_points - {"\r", "\n"})
        check(family + ":reopened-edit-introduces-new-glyphs", bool(new_points), {"newCharacters": new_points})
        check(family + ":fixed-stage-matrix", case["InputFile"] == family + "-input.pdf" and len(case["Stages"]) == 2
              and {stage["Id"] for stage in case["Stages"]} == {"saved", "reedited"}, {"input": case["InputFile"], "stages": [stage["Id"] for stage in case["Stages"]]})
        for phase in ("input", "saved", "reedited"):
            name = family + "-" + phase + ".pdf"
            path = directory / name
            digest = sha(path)
            check(name + ":native-output-binding", digest == outputs.get(name), {"sha256": digest, "bytes": path.stat().st_size})
            report["pdfs"].append({"file": name, "sha256": digest, "bytes": path.stat().st_size})
            reader = readers[name] = PdfReader(path, strict=True)
            acro, fields = graphs[name] = canonical_fields(reader)
            check(name + ":canonical-interactive-field-widget-graph", set(fields) == FIELDS and len(reader.pages) == 1
                  and all(len(item["widgets"]) == 1 for item in fields.values()), {"fieldNames": sorted(fields)})
            page = reader.pages[0]
            check(name + ":retained-content-and-page-geometry", tuple(map(float, page.mediabox)) == (0, 0, 400, 400)
                  and page.extract_text().strip() == "KEEP CJK FORM BASELINE", page.extract_text())
            dr = acro["/DR"]["/Font"]
            default = dr.raw_get("/Cjk")
            default_font, descendant, data = font_info(default)
            embedded_hash = hashlib.sha256(data).hexdigest().upper()
            check(name + ":full-embedded-default-CID-TrueType", default_font.get("/Subtype") == "/Type0"
                  and descendant.get("/Subtype") == "/CIDFontType2" and data.startswith(b"\x00\x01\x00\x00")
                  and embedded_hash == case["SourceSHA256"] and bool(default_font["/ToUnicode"].get_object().get_data()),
                  {"embeddedSHA256": embedded_hash, "embeddedBytes": len(data), "fontReference": reference(default)})
            cid_mapping = descendant.get("/CIDToGIDMap", "/Identity")
            cid_bytes = None if cid_mapping == "/Identity" else cid_mapping.get_object().get_data()
            unicode_mapping = cmap_pairs(default_font["/ToUnicode"].get_object().get_data())
            advances, default_advance = cid_widths(descendant)
            font_mappings.append({"file": name, "family": family, "encoding": str(default_font.get("/Encoding")),
                                  "cidMap": cid_bytes, "toUnicode": unicode_mapping, "widths": advances, "defaultWidth": default_advance})
            report["fonts"].append({"file": name, "embeddedSHA256": embedded_hash, "embeddedBytes": len(data), "fullFont": True})
            list_field = fields["CjkList"]["field"]
            check(name + ":options-display-and-export-values", choice_options(list_field) == [(item, item) for item in expected["options"]]
                  and fields["CjkList"]["type"] == "/Ch", choice_options(list_field))
            check(name + ":multiline-field-flag", fields["CjkMultiline"]["type"] == "/Tx"
                  and int(fields["CjkMultiline"]["field"].get("/Ff", 0)) & (1 << 12), int(fields["CjkMultiline"]["field"].get("/Ff", 0)))
            for field_name, entry in fields.items():
                field, widget = entry["field"], entry["widgets"][0]
                rectangle = tuple(map(float, widget["/Rect"]))
                da = str(field.get("/DA", acro.get("/DA", "")))
                check(name + ":" + field_name + ":editable-font-DA-and-geometry", re.fullmatch(r"/Cjk\s+16(?:\.0*)?\s+Tf(?:\s+0\s+g)?", da) is not None
                      and rectangle == RECTANGLES[field_name] and not int(field.get("/Ff", 0)) & 1, {"DA": da, "rect": rectangle})
                if phase == "input":
                    continue
                value = expected[phase][field_name]
                ap = normal_appearance(widget)
                decoded, fragments = appearance_text(ap, reader)
                ap_fonts, selections = appearances_font_usage(ap, reader)
                ap_text = text_lines(decoded)
                expected_lines = expected["options"] if field_name == "CjkList" else value.split("\r")
                check(name + ":" + field_name + ":canonical-value-and-actual-ToUnicode-AP", normalized_value(field) == value.replace("\r", "\r\n") and ap_text == expected_lines,
                      {"V": normalized_value(field), "decoded": decoded, "expectedLines": expected_lines})
                resolved_fonts = []
                valid_fonts = True
                for tag, size in selections:
                    raw = ap_fonts.raw_get(tag)
                    ap_font, ap_descendant, ap_data = font_info(raw)
                    valid_fonts = valid_fonts and reference(raw) == reference(default) and hashlib.sha256(ap_data).hexdigest().upper() == embedded_hash
                    valid_fonts = valid_fonts and ap_font.get("/Subtype") == "/Type0" and bool(ap_font["/ToUnicode"].get_object().get_data())
                    resolved_fonts.append({"tag": tag, "size": size, "reference": reference(raw)})
                check(name + ":" + field_name + ":AP-font-closure-is-embedded-AcroForm-font", valid_fonts,
                      {"defaultReference": reference(default), "actualTf": resolved_fonts})
                if field_name == "CjkMultiline":
                    ys = [fragment["y"] for fragment in fragments]
                    check(name + ":multiline-AP-descending-line-baselines", len(ys) == 2 and ys[0] > ys[1], fragments)
                elif field_name == "CjkList":
                    index = 1 if phase == "saved" else 2
                    check(name + ":list-canonical-index", list(map(int, field.get("/I", []))) == [index], {"I": list(field.get("/I", [])), "expected": index})
        for phase in ("saved", "reedited"):
            name = family + "-native-" + phase + ".pdf"
            path = directory / name
            digest = sha(path)
            check(name + ":native-counterexample-output-binding", digest == outputs.get(name), {"sha256": digest, "bytes": path.stat().st_size})
            report["pdfs"].append({"file": name, "sha256": digest, "bytes": path.stat().st_size, "counterexample": True})
            reader = readers[name] = PdfReader(path, strict=True)
            _, fields = canonical_fields(reader)
            observed = []
            for field_name in FIELDS:
                widget = fields[field_name]["widgets"][0]
                ap = normal_appearance(widget)
                fonts, selections = appearances_font_usage(ap, reader)
                for tag, size in selections:
                    try:
                        _, _, data = font_info(fonts.raw_get(tag))
                        embedded = bool(data)
                    except KeyError:
                        embedded = False
                    observed.append({"field": field_name, "tag": tag, "embedded": embedded})
            check(name + ":native-automatic-AP-unembedded-fallback-observed", any(not item["embedded"] for item in observed)
                  and all(normalized_value(fields[field]["field"]) == EXPECTED[family][phase][field].replace("\r", "\r\n") for field in FIELDS),
                  {"actualTf": observed, "meaning": "Canonical values are correct while native automatic AP uses an unembedded fallback; this is a rejected counterexample."})
    if fail_fast and any(not item["passed"] for item in report["checks"]):
        raise ValueError("Negative control rejected by canonical/font/AP checks")
    job_path = directory / "fonttools-jobs.json"
    store(job_path, jobs)
    inspector = Path(__file__).parent / "font_subset_inspector.py"
    process = subprocess.run([str(font_python), str(inspector), str(job_path)], capture_output=True, text=True, encoding="utf-8", timeout=180)
    if process.returncode != 0:
        raise RuntimeError("Read-only fontTools oracle failed: " + process.stderr[-3000:])
    inspected = json.loads(process.stdout)
    store(directory / "fonttools-analysis.json", inspected)
    report["fontTools"] = {"version": inspected["fontToolsVersion"], "python": str(font_python), "inspectorSHA256": sha(inspector), "analysisSHA256": sha(directory / "fonttools-analysis.json")}
    for font in inspected["fonts"]:
        check(font["id"] + ":independent-full-font-cmap-coverage-and-checksums", font["allTableChecksumsCorrect"] and font["sfntChecksumCorrect"]
              and all(point is not None and point["gid"] != 0 for point in font["codepoints"].values()),
              {"glyphCount": font["glyphCount"], "cmapFormats": font["cmapFormats"], "checkedCodepoints": len(font["codepoints"]),
               "missingCodepoints": [point for point, value in font["codepoints"].items() if value is None or value["gid"] == 0]})
    source_fonts = {font["id"]: font for font in inspected["fonts"]}
    for mapping in font_mappings:
        source = source_fonts[mapping["family"]]
        wrong = []
        for point, metrics in source["codepoints"].items():
            cid = int(point)
            data = mapping["cidMap"]
            actual_gid = cid if data is None else int.from_bytes(data[cid * 2:cid * 2 + 2], "big") if len(data) >= cid * 2 + 2 else None
            unicode_scalar = mapping["toUnicode"].get(cid)
            if metrics is None or actual_gid != metrics["gid"] or unicode_scalar != chr(cid):
                wrong.append({"scalar": cid, "actualGid": actual_gid, "expectedGid": None if metrics is None else metrics["gid"], "toUnicode": unicode_scalar})
        check(mapping["file"] + ":CIDToGIDMap-and-ToUnicode-match-source-cmap", mapping["encoding"] == "/Identity-H" and not wrong,
              {"encoding": mapping["encoding"], "checkedCodepoints": len(source["codepoints"]), "wrongMappings": wrong})
        width_errors = []
        for point, metrics in source["codepoints"].items():
            if metrics is None:
                continue
            actual = mapping["widths"].get(int(point), mapping["defaultWidth"])
            expected = metrics["metrics"][0] * 1000.0 / source["unitsPerEm"]
            if abs(actual - expected) > 1.0:
                width_errors.append({"scalar": int(point), "pdfWidth": actual, "sourceWidth": expected})
        check(mapping["file"] + ":CID-widths-match-source-hmtx", not width_errors,
              {"unitsPerEm": source["unitsPerEm"], "checkedCodepoints": len(source["codepoints"]), "toleranceIn1000EmUnits": 1.0, "wrongWidths": width_errors})
    if fail_fast and any(not item["passed"] for item in report["checks"]):
        raise ValueError("Negative control rejected by independent cmap/CID glyph checks")
    images = render(directory, sorted(expected_files), renderer, report, check)
    for family in EXPECTED:
        baseline = images[family + "-input.pdf"]
        for phase in ("saved", "reedited"):
            name = family + "-" + phase + ".pdf"
            image = images[name]
            fields = graphs[name][1]
            selected, rows = list_selection_pixels(readers[name], fields["CjkList"]["widgets"][0], image, [1 if phase == "saved" else 2], 400, 400)
            check(name + ":list-visible-highlight-matches-canonical-selection", selected and len(rows) == 3, rows)
            difference = ImageChops.difference(baseline, image)
            for rectangle in RECTANGLES.values():
                left, bottom, right, top = rectangle
                difference.paste((0, 0, 0), pdf_roi(image, (left - 2, bottom - 2, right + 2, top + 2), 400, 400))
            check(name + ":retained-pixels-outside-fields", baseline.size == image.size and difference.getbbox() is None, {"excludedPdfRects": RECTANGLES})
            roi = pdf_roi(image, (30, 345, 380, 390), 400, 400)
            check(name + ":baseline-header-pixels-exact", same_pixels(baseline, image, roi), roi)
            for field_name in ("CjkText", "CjkMultiline"):
                roi = pdf_roi(image, RECTANGLES[field_name], 400, 400)
                check(name + ":" + field_name + ":visible-generated-AP", not same_pixels(baseline, image, roi), roi)
        for field_name in FIELDS:
            roi = pdf_roi(baseline, RECTANGLES[field_name], 400, 400)
            check(family + ":reopened-edit:" + field_name + ":visibly-changed", not same_pixels(images[family + "-saved.pdf"], images[family + "-reedited.pdf"], roi), roi)
    sheet = Image.new("RGB", (1200, 1290), "#dddddd")
    draw = ImageDraw.Draw(sheet)
    for row, family in enumerate(EXPECTED):
        for col, phase in enumerate(("input", "saved", "reedited")):
            name = family + "-" + phase + ".pdf"
            x, y = col * 400, row * 430
            draw.text((x + 8, y + 8), name, fill="black")
            sheet.paste(images[name].resize((400, 400)), (x, y + 25))
    path = directory / "cjk-forms-contact-sheet.png"
    sheet.save(path)
    report["contactSheet"] = {"file": path.name, "sha256": sha(path)}


def controls(directory, renderer, font_python, report):
    root = directory / "self-tests" / uuid.uuid4().hex
    root.mkdir(parents=True)
    results = []
    for identifier in ("missing-output", "removed-font-program", "wrong-ToUnicode", "wrong-CID-to-GID", "wrong-CID-widths", "stale-CJK-AP", "stale-list-AP", "orphan-field", "wrong-list-index"):
        case = root / identifier
        case.mkdir()
        shutil.copyfile(directory / "result.json", case / "result.json")
        for path in directory.glob("*.pdf"):
            shutil.copyfile(path, case / path.name)
        store(case / "independent-audit.json", {"passed": True, "status": "stale-success-sentinel"})
        target = case / "lxgw-saved.pdf"
        if identifier == "missing-output":
            target.unlink()
        else:
            writer = PdfWriter(clone_from=target)
            acro, fields = canonical_fields(writer)
            if identifier == "removed-font-program":
                default = acro["/DR"]["/Font"]["/Cjk"]
                descriptor = default["/DescendantFonts"][0]["/FontDescriptor"]
                del descriptor["/FontFile2"]
            elif identifier == "wrong-ToUnicode":
                font = acro["/DR"]["/Font"]["/Cjk"]
                stream = font["/ToUnicode"].get_object()
                data = stream.get_data()
                # Corrupt all decoded Han U+4E2D to a different scalar while
                # keeping a syntactically valid stream and forged PDF hash.
                changed = re.sub(rb"(?i)<4e2d>\s*<4e2d>", b"<4E2D> <4E00>", data)
                if changed == data:
                    raise ValueError("CJK negative control requires U+4E2D ToUnicode entry")
                stream.set_data(changed)
            elif identifier == "wrong-CID-to-GID":
                descendant = acro["/DR"]["/Font"]["/Cjk"]["/DescendantFonts"][0].get_object()
                stream = descendant["/CIDToGIDMap"].get_object()
                data = bytearray(stream.get_data())
                data[0x4E2D * 2:0x4E2D * 2 + 2] = b"\0\0"
                stream.set_data(bytes(data))
            elif identifier == "wrong-CID-widths":
                from pypdf.generic import ArrayObject, NumberObject
                descendant = acro["/DR"]["/Font"]["/Cjk"]["/DescendantFonts"][0].get_object()
                descendant[NameObject("/W")] = ArrayObject([NumberObject(0x4E2D), ArrayObject([NumberObject(9999)])])
            elif identifier == "stale-CJK-AP":
                saved_reader = PdfReader(directory / "lxgw-reedited.pdf")
                _, saved_fields = canonical_fields(saved_reader)
                ap = normal_appearance(fields["CjkText"]["widgets"][0])
                alternate = normal_appearance(saved_fields["CjkText"]["widgets"][0])
                ap.set_data(alternate.get_data())
            elif identifier == "stale-list-AP":
                original = PdfReader(directory / "lxgw-reedited.pdf")
                _, original_fields = canonical_fields(original)
                ap = normal_appearance(fields["CjkList"]["widgets"][0])
                ap.set_data(normal_appearance(original_fields["CjkList"]["widgets"][0]).get_data())
            elif identifier == "orphan-field":
                fields["CjkText"]["widgets"][0][NameObject("/Parent")] = fields["CjkList"]["field"].indirect_reference
            else:
                from pypdf.generic import ArrayObject, NumberObject
                fields["CjkList"]["field"][NameObject("/I")] = ArrayObject([NumberObject(0)])
            temporary = case / "mutated.pdf"
            with temporary.open("wb") as stream:
                writer.write(stream)
            writer.close()
            temporary.replace(target)
            native = json.loads((case / "result.json").read_text(encoding="utf-8-sig"))
            for item in native["Result"]["Outputs"]:
                if item["File"] == target.name:
                    item["SHA256"] = sha(target)
            store(case / "result.json", native)
        process = subprocess.run([sys.executable, str(Path(__file__).resolve()), "--run", str(case), "--renderer", str(renderer),
                                  "--font-python", str(font_python), "--fail-fast"], capture_output=True, text=True, timeout=180)
        failed = json.loads((case / "independent-audit.json").read_text(encoding="utf-8"))
        rejected = [item["id"] for item in failed.get("checks", []) if not item["passed"]]
        expected = ("FileNotFoundError" in failed.get("error", "") if identifier == "missing-output" else
                    "FontFile2" in failed.get("error", "") if identifier == "removed-font-program" else
                    "lxgw-saved.pdf:CjkText:canonical-value-and-actual-ToUnicode-AP" in rejected if identifier in ("wrong-ToUnicode", "stale-CJK-AP") else
                    "lxgw-saved.pdf:CIDToGIDMap-and-ToUnicode-match-source-cmap" in rejected if identifier == "wrong-CID-to-GID" else
                    "lxgw-saved.pdf:CID-widths-match-source-hmtx" in rejected if identifier == "wrong-CID-widths" else
                    "lxgw-saved.pdf:list-visible-highlight-matches-canonical-selection" in rejected if identifier == "stale-list-AP" else
                    "Parent" in failed.get("error", "") if identifier == "orphan-field" else
                    "lxgw-saved.pdf:list-canonical-index" in rejected)
        results.append({"id": identifier, "passed": process.returncode != 0 and failed.get("passed") is False and failed.get("status") != "stale-success-sentinel" and expected,
                        "expectedRejectionDetected": expected, "exit": process.returncode, "rejectedChecks": rejected, "error": failed.get("error"),
                        "report": str((case / "independent-audit.json").relative_to(directory)), "reportSHA256": sha(case / "independent-audit.json")})
    report["negativeControls"] = results
    report["checks"].append({"id": "semantic-negative-controls-and-stale-pass-protection", "passed": all(item["passed"] for item in results), "detail": results})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True)
    parser.add_argument("--renderer", required=True)
    parser.add_argument("--font-python", required=True)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--fail-fast", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args()
    supplied = Path(args.run).resolve()
    directory = supplied / "cjk-forms" if (supplied / "cjk-forms").is_dir() else supplied
    directory.mkdir(parents=True, exist_ok=True)
    target = directory / "independent-audit.json"
    report = {"status": "verification-incomplete", "passed": False, "verifiedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
              "scope": "Native canonical form-value experiment plus managed fixed-layout appearance bridge; native automatic CJK AP remains rejected.",
              "scriptSHA256": sha(__file__), "helperSHA256": sha(Path(__file__).parent / "forms_verifier_common.py"), "pypdfVersion": pypdf.__version__, "checks": [],
              "toUnicodeDecoderSHA256": sha(Path(__file__).parent / "verify_font_subset.py"),
              "limitations": ["The C# bridge regenerates fixed-size single-line, explicit multiline and fully visible list appearances after native canonical commit; this is not a product form editing API.",
                              "Full static TrueType BMP repertoire for future editing, not a current-value subset; CJK-font coverage differs by source family.",
                              "Windows x64 native API input and repeat edit, not Avalonia IME/clipboard/accessibility or physical keyboard acceptance.",
                              "No Linux/Kylin, arbitrary third-party forms, CFF/TTC/variable fonts, shaping or official performance parity claim."]}
    store(target, report)
    try:
        renderer = Path(args.renderer).resolve(strict=True)
        font_python = Path(args.font_python).resolve(strict=True)
        audit(directory, renderer, font_python, report, args.fail_fast)
        if args.self_test:
            controls(directory, renderer, font_python, report)
        report["passed"] = bool(report["checks"]) and all(item["passed"] for item in report["checks"])
        report["status"] = "cjk-forms-independently-confirmed" if report["passed"] else "verification-failed"
    except Exception as error:
        report["status"] = "verification-error"
        report["error"] = f"{type(error).__name__}: {error}"
        report["passed"] = False
    store(target, report)
    print(json.dumps({"report": str(target), "passed": report["passed"], "checks": len(report["checks"]),
                      "failed": [item["id"] for item in report["checks"] if not item["passed"]], "error": report.get("error")}, ensure_ascii=True))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
