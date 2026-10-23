"""Independent canonical AcroForm, appearance and Poppler audit of native event input.

Positive PDFs are authored only by the C# probe. Python creates deliberately
invalid negative controls to demonstrate that forged local hashes cannot pass.
"""
from __future__ import annotations
import argparse
import datetime as dt
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
import pypdf
from PIL import Image, ImageChops, ImageDraw
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ArrayObject, DecodedStreamObject, NameObject, NumberObject
from forms_verifier_common import (appearance_text, appearances_font_usage, canonical_fields, choice_options,
    list_selection_pixels, normal_appearance, normalized_value, pdf_roi, reference, render, same_pixels, sha, store, text_lines)

PDFS = ("input.pdf", "focused-saved.pdf", "second-saved.pdf")
OPTIONS = [("R", "Red display"), ("G", "Green display"), ("B", "Blue display"), ("K", "Black display")]
EXPECTED = {
    "focused-saved.pdf": {"Radio": "/Blue", "Multiline": "first line\r\nsecond line", "SingleList": ("G", [1]), "MultiList": (["R", "B"], [0, 2])},
    "second-saved.pdf": {"Radio": "/Red", "Multiline": "changed line\r\nfinal line", "SingleList": ("K", [3]), "MultiList": (["G", "K"], [1, 3])},
}
FIELD_NAMES = {"Radio", "Multiline", "SingleList", "MultiList", "ReadOnly", "Required"}
NATIVE_CHECKS = {phase + "-" + suffix for phase in ("first-commit", "first-reopen", "second-commit", "second-reopen")
                 for suffix in ("radio-exclusive", "multiline-value", "single-list-export-index", "multi-list-exports-indices", "required-value")}
NATIVE_CHECKS.update({"widget-type-matrix", "native-readonly-required-flags", "readonly-input-refused",
                      "normal-appearances-after-first-save", "required-does-not-validate-automatically", "flags-and-widget-count-after-second-save"})
RECTANGLES = [(35, 585, 125, 615), (35, 425, 405, 545), (35, 265, 265, 405), (295, 265, 565, 405), (35, 195, 305, 235), (35, 135, 305, 175)]


def audit(directory, renderer, report, fail_fast=False):
    def check(identifier, passed, detail):
        report["checks"].append({"id": identifier, "passed": bool(passed), "detail": detail})
    path = directory / "result.json"
    native = json.loads(path.read_text(encoding="utf-8-sig"))
    result = native["Result"]
    report["nativeReportSHA256"] = sha(path)
    native_checks = result["Checks"]
    check("native-event-host-evidence", native["Probe"] == "forms-advanced" and native["ExpectationsPassed"] is True
          and len(native_checks) == len({item["Id"] for item in native_checks}) == len(NATIVE_CHECKS)
          and {item["Id"] for item in native_checks} == NATIVE_CHECKS
          and all(item["Passed"] for item in native_checks),
          {"managedAssemblySHA256": native["ManagedAssemblySHA256"], "nativeModule": native["NativeModule"], "status": result["Status"]})
    outputs = {item["File"]: item["SHA256"] for item in result["Outputs"]}
    check("fixed-output-matrix", len(result["Outputs"]) == len(outputs) == len(PDFS) and set(outputs) == set(PDFS), list(outputs))
    readers, graphs = {}, {}
    report["pdfs"] = []
    for name in PDFS:
        target = directory / name
        digest = sha(target)
        check(name + ":native-output-binding", digest == outputs.get(name), {"sha256": digest, "bytes": target.stat().st_size})
        report["pdfs"].append({"file": name, "sha256": digest, "bytes": target.stat().st_size})
        reader = readers[name] = PdfReader(target, strict=True)
        acro, fields = graphs[name] = canonical_fields(reader)
        check(name + ":canonical-field-widget-graph", set(fields) == FIELD_NAMES and len(reader.pages) == 1
              and len(fields["Radio"]["widgets"]) == 2 and all(len(value["widgets"]) == 1 for key, value in fields.items() if key != "Radio"),
              {"names": sorted(fields), "widgetCount": sum(len(value["widgets"]) for value in fields.values())})
        page = reader.pages[0]
        check(name + ":retained-page-content-and-geometry", tuple(map(float, page.mediabox)) == (0, 0, 600, 700)
              and "P0 advanced AcroForm" in page.extract_text() and "ORIGINAL CONTENT 314159" in page.extract_text(), page.extract_text())
        readonly = fields["ReadOnly"]["field"]
        required = fields["Required"]["field"]
        check(name + ":readonly-required-flags-and-values", int(readonly.get("/Ff", 0)) & 1 and int(required.get("/Ff", 0)) & 2
              and normalized_value(readonly) == "FIXED RO" and normalized_value(required) == ("provided" if name == "focused-saved.pdf" else ""), {"readonlyFlags": int(readonly.get("/Ff", 0)),
              "requiredFlags": int(required.get("/Ff", 0)), "readonlyValue": normalized_value(readonly), "requiredValue": normalized_value(required)})
        radio = fields["Radio"]
        on_states = []
        for widget in radio["widgets"]:
            normal = widget["/AP"]["/N"].get_object()
            on_states.extend(str(key) for key in normal if key != "/Off")
        check(name + ":radio-parent-and-distinct-export-states", radio["type"] == "/Btn" and int(radio["field"].get("/Ff", 0)) & (1 << 15)
              and sorted(on_states) == ["/Blue", "/Red"] and "/Subtype" not in radio["field"],
              {"flags": int(radio["field"].get("/Ff", 0)), "exportStates": on_states})
        multiline = fields["Multiline"]
        check(name + ":multiline-flag", multiline["type"] == "/Tx" and int(multiline["field"].get("/Ff", 0)) & (1 << 12), int(multiline["field"].get("/Ff", 0)))
        for key in ("SingleList", "MultiList"):
            field = fields[key]["field"]
            flags = int(field.get("/Ff", 0))
            check(name + ":" + key + ":export-display-options-and-flags", fields[key]["type"] == "/Ch" and choice_options(field) == OPTIONS
                  and not flags & (1 << 17) and bool(flags & (1 << 21)) == (key == "MultiList"),
                  {"options": choice_options(field), "flags": flags})
        if name == "input.pdf":
            continue
        expected = EXPECTED[name]
        states = [str(widget.get("/AS")) for widget in radio["widgets"]]
        active_states = [state for state in states if state != "/Off"]
        check(name + ":radio-canonical-V-AS-exclusive", str(radio["field"].get("/V")) == expected["Radio"] and active_states == [expected["Radio"]],
              {"V": str(radio["field"].get("/V")), "AS": states})
        for widget in radio["widgets"]:
            normal_appearance(widget)
        for key in ("SingleList", "MultiList"):
            field = fields[key]["field"]
            value, indices = expected[key]
            actual_indices = list(map(int, field.get("/I", [])))
            check(name + ":" + key + ":canonical-selection-V-I", normalized_value(field) == value and actual_indices == indices,
                  {"V": normalized_value(field), "I": actual_indices, "expected": [value, indices]})
            ap = normal_appearance(fields[key]["widgets"][0])
            decoded, fragments = appearance_text(ap, reader)
            appearances_font_usage(ap, reader)
            lines = text_lines(decoded)
            check(name + ":" + key + ":actual-AP-displays-labels", lines == [item[1] for item in OPTIONS], {"decoded": decoded, "fragments": fragments})
        field = multiline["field"]
        ap = normal_appearance(multiline["widgets"][0])
        decoded, fragments = appearance_text(ap, reader)
        appearances_font_usage(ap, reader)
        actual_lines = text_lines(decoded)
        expected_lines = text_lines(expected["Multiline"])
        check(name + ":multiline-canonical-and-actual-AP-text", normalized_value(field) == expected["Multiline"] and actual_lines == expected_lines,
              {"V": normalized_value(field), "decoded": decoded, "expected": expected["Multiline"]})
        ys = [fragment["y"] for fragment in fragments if fragment["text"].strip()]
        # The native appearance serializer emits one text fragment per line.
        check(name + ":multiline-AP-lines-have-distinct-descending-baselines", len(ys) == 2 and ys[0] > ys[1], fragments)
        readonly_ap = normal_appearance(fields["ReadOnly"]["widgets"][0])
        readonly_text, _ = appearance_text(readonly_ap, reader)
        check(name + ":readonly-appearance-stays-fixed", text_lines(readonly_text) == ["FIXED RO"], readonly_text)
        required_ap = normal_appearance(fields["Required"]["widgets"][0])
        required_text, _ = appearance_text(required_ap, reader)
        check(name + ":required-appearance-agrees-with-value", text_lines(required_text) == (["provided"] if name == "focused-saved.pdf" else []), required_text)
    if fail_fast and any(not item["passed"] for item in report["checks"]):
        raise ValueError("Negative control rejected by AcroForm semantic checks")
    images = render(directory, PDFS, renderer, report, check)
    baseline = images["input.pdf"]
    readonly_roi = pdf_roi(baseline, (44, 204, 296, 226))
    readonly_ink = sum(value < 160 for value in baseline.crop(readonly_roi).convert("L").tobytes())
    check("input:readonly-AP-is-visible-text", readonly_ink > 80, {"roi": readonly_roi, "darkPixels": readonly_ink})
    for name in EXPECTED:
        image = images[name]
        fields = graphs[name][1]
        for key in ("SingleList", "MultiList"):
            selected, rows = list_selection_pixels(readers[name], fields[key]["widgets"][0], image, EXPECTED[name][key][1])
            check(name + ":" + key + ":visible-highlight-matches-canonical-selection", selected and len(rows) == 4, rows)
        for index, (x, y, state) in enumerate(((50, 600, "/Red"), (110, 600, "/Blue"))):
            roi = pdf_roi(image, (x - 3, y - 3, x + 3, y + 3))
            dark = sum(value < 100 for value in image.crop(roi).convert("L").tobytes())
            expected_on = EXPECTED[name]["Radio"] == state
            check(name + ":radio:" + state[1:] + ":visible-dot-matches-AS", (dark > 80) if expected_on else dark == 0,
                  {"roi": roi, "darkPixels": dark, "selected": expected_on})
        difference = ImageChops.difference(baseline, image)
        for rectangle in RECTANGLES:
            difference.paste((0, 0, 0), pdf_roi(image, rectangle))
        check(name + ":retained-pixels-outside-fields", baseline.size == image.size and difference.getbbox() is None, {"excludedPdfRects": RECTANGLES})
        for label, rectangle in (("header", (35, 640, 560, 690)), ("footer", (35, 60, 560, 100)), ("readonly", (40, 200, 300, 230))):
            check(name + ":" + label + "-pixels-exact", same_pixels(baseline, image, pdf_roi(image, rectangle)), list(rectangle))
    for label, rectangle in (("radio", (35, 585, 125, 615)), ("multiline", (40, 430, 400, 540)), ("single-list", (40, 270, 260, 400)), ("multi-list", (300, 270, 560, 400))):
        roi = pdf_roi(baseline, rectangle)
        check("second-edit:" + label + "-appearance-visibly-updated", not same_pixels(images["focused-saved.pdf"], images["second-saved.pdf"], roi), list(rectangle))
    sheet = Image.new("RGB", (1800, 740), "#dddddd")
    draw = ImageDraw.Draw(sheet)
    for index, name in enumerate(PDFS):
        draw.text((index * 600 + 8, 8), name, fill="black")
        sheet.paste(images[name].resize((600, 700)), (index * 600, 30))
    contact = directory / "forms-advanced-contact-sheet.png"
    sheet.save(contact)
    report["contactSheet"] = {"file": contact.name, "sha256": sha(contact)}


def controls(directory, renderer, report):
    root = directory / "self-tests" / uuid.uuid4().hex
    root.mkdir(parents=True)
    results = []
    for identifier in ("missing-output", "orphan-widget", "stale-multiline-AP", "stale-list-AP", "wrong-radio-AS", "wrong-choice-I", "retained-content-changed"):
        case = root / identifier
        case.mkdir()
        for name in (*PDFS, "result.json"):
            shutil.copyfile(directory / name, case / name)
        store(case / "independent-audit.json", {"passed": True, "status": "stale-success-sentinel"})
        target = case / "focused-saved.pdf"
        if identifier == "missing-output":
            target.unlink()
        else:
            writer = PdfWriter(clone_from=target)
            acro, fields = canonical_fields(writer)
            if identifier == "orphan-widget":
                fields["Radio"]["widgets"][0][NameObject("/Parent")] = fields["ReadOnly"]["field"].indirect_reference
            elif identifier == "stale-multiline-AP":
                ap = normal_appearance(fields["Multiline"]["widgets"][0])
                fonts, _ = appearances_font_usage(ap, writer)
                ap.set_data(("BT " + str(next(iter(fonts))) + " 12 Tf 2 30 Td (WRONG STALE AP) Tj ET").encode("ascii"))
            elif identifier == "stale-list-AP":
                original = PdfReader(directory / "input.pdf")
                _, original_fields = canonical_fields(original)
                ap = normal_appearance(fields["SingleList"]["widgets"][0])
                ap.set_data(normal_appearance(original_fields["SingleList"]["widgets"][0]).get_data())
            elif identifier == "wrong-radio-AS":
                for widget in fields["Radio"]["widgets"]:
                    widget[NameObject("/AS")] = NameObject("/Off")
            elif identifier == "wrong-choice-I":
                fields["MultiList"]["field"][NameObject("/I")] = ArrayObject([NumberObject(1)])
            else:
                content = writer.pages[0].get_contents().get_data().replace(b"ORIGINAL CONTENT 314159", b"WRONG ORIGINAL CONTENT")
                stream = DecodedStreamObject()
                stream.set_data(content)
                writer.pages[0][NameObject("/Contents")] = writer._add_object(stream)
            temporary = case / "mutated.pdf"
            with temporary.open("wb") as stream:
                writer.write(stream)
            writer.close()
            temporary.replace(target)
            native = json.loads((case / "result.json").read_text(encoding="utf-8-sig"))
            for output in native["Result"]["Outputs"]:
                if output["File"] == target.name:
                    output["SHA256"] = sha(target)
            store(case / "result.json", native)
        process = subprocess.run([sys.executable, str(Path(__file__).resolve()), "--run", str(case), "--renderer", str(renderer), "--fail-fast"],
                                 capture_output=True, text=True, timeout=90)
        failed = json.loads((case / "independent-audit.json").read_text(encoding="utf-8"))
        rejected = [item["id"] for item in failed.get("checks", []) if not item["passed"]]
        expected = ("FileNotFoundError" in failed.get("error", "") if identifier == "missing-output" else
                    "Parent" in failed.get("error", "") if identifier == "orphan-widget" else
                    "focused-saved.pdf:multiline-canonical-and-actual-AP-text" in rejected if identifier == "stale-multiline-AP" else
                    "focused-saved.pdf:SingleList:visible-highlight-matches-canonical-selection" in rejected if identifier == "stale-list-AP" else
                    "focused-saved.pdf:radio-canonical-V-AS-exclusive" in rejected if identifier == "wrong-radio-AS" else
                    "focused-saved.pdf:MultiList:canonical-selection-V-I" in rejected if identifier == "wrong-choice-I" else
                    "focused-saved.pdf:retained-page-content-and-geometry" in rejected)
        results.append({"id": identifier, "passed": process.returncode != 0 and failed.get("passed") is False and failed.get("status") != "stale-success-sentinel" and expected,
                        "expectedRejectionDetected": expected, "exit": process.returncode, "rejectedChecks": rejected, "error": failed.get("error"),
                        "report": str((case / "independent-audit.json").relative_to(directory)), "reportSHA256": sha(case / "independent-audit.json")})
    report["negativeControls"] = results
    report["checks"].append({"id": "semantic-negative-controls-and-stale-pass-protection", "passed": all(item["passed"] for item in results), "detail": results})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True)
    parser.add_argument("--renderer", required=True)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--fail-fast", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args()
    supplied = Path(args.run).resolve()
    directory = supplied / "forms-advanced" if (supplied / "forms-advanced").is_dir() else supplied
    directory.mkdir(parents=True, exist_ok=True)
    target = directory / "independent-audit.json"
    report = {"status": "verification-incomplete", "passed": False, "verifiedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
              "scriptSHA256": sha(__file__), "helperSHA256": sha(Path(__file__).parent / "forms_verifier_common.py"),
              "pypdfVersion": pypdf.__version__, "checks": [],
              "limitations": ["Windows native API event injection, not physical keyboard, Avalonia UI or IME integration.",
                              "One fixed AcroForm corpus, no XFA, validation/calculation JavaScript or general form preservation guarantee.",
                              "Linux/Kylin and official reference performance remain unverified."]}
    store(target, report)
    try:
        renderer = Path(args.renderer).resolve(strict=True)
        audit(directory, renderer, report, args.fail_fast)
        if args.self_test:
            controls(directory, renderer, report)
        report["passed"] = bool(report["checks"]) and all(item["passed"] for item in report["checks"])
        report["status"] = "advanced-acroforms-independently-confirmed" if report["passed"] else "verification-failed"
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
