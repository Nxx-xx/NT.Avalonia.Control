"""Fail-closed independent audit of the fixed advanced redaction corpus.

This verifies fixture evidence, not arbitrary documents. It deliberately checks
page scope for shared Forms, and both bytes and geometry for partial text.
Run after the native probe: python verify_redaction_advanced.py --output RUN_ROOT
Use --self-test to prove a missing case and tampered candidate are rejected.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import platform
import shutil
import subprocess
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import pypdf
from pypdf import PdfReader
from pypdf.generic import ContentStream, StreamObject
from PIL import Image, ImageChops

from verify_redaction import audit


CASES = {
    "partial-text-naive-settext",
    "partial-text-preserve-positions",
    "shared-form-single-instance",
    "whole-vector-path",
    "hidden-annotation-form-native-leak",
    "ligature-character-refusal",
    "partial-path-refusal",
}
RANGE_SECRET = "P0_RANGE_SECRET_931A"
FORM_SECRET = "P0_SHARED_SECRET_74CF"
HIDDEN_SECRET = "P0_HIDDEN_ANNOT_FORM_48D2"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def require(condition: Any, message: str) -> None:
    if not condition:
        raise ValueError(message)


def exact_path(directory: Path, supplied: str, filename: str) -> Path:
    expected = (directory / filename).resolve()
    actual = Path(supplied).resolve()
    require(actual == expected, f"Evidence path must be the fixed case path: {expected}")
    require(actual.is_file(), f"Required evidence file is missing: {actual}")
    return actual


def case_checks(case: dict[str, Any]) -> bool:
    checks = case.get("Checks")
    return isinstance(checks, list) and len(checks) >= 4 and all(
        set(check) == {"Name", "Passed"} and check["Name"] and check["Passed"] is True
        for check in checks
    )


def content_paints(path: Path, all_streams: bool = False) -> list[dict[str, Any]]:
    """Inspect painted paths with their active fill colors, including q/Q state.

    The positive fixture uses only simple untransformed rectangles. Unsupported
    operators must not silently widen this into a general geometric algorithm.
    """
    reader = PdfReader(path, strict=True)
    streams = []
    if all_streams:
        refs = {(number, generation) for generation, entries in reader.xref.items()
                for number in entries if number and generation != 65535}
        refs.update((number, 0) for number in reader.xref_objStm)
        from pypdf.generic import IndirectObject
        for number, generation in sorted(refs):
            obj = reader.get_object(IndirectObject(number, generation, reader))
            if isinstance(obj, StreamObject) and (obj.get("/Subtype") == "/Form" or
                    (obj.get("/Type") is None and obj.get("/Subtype") is None)):
                streams.append((f"xref:{number}:{generation}", obj))
    else:
        streams = [(f"page:{index}", page.get_contents()) for index, page in enumerate(reader.pages)]
    result = []
    for location, stream in streams:
        if stream is None:
            continue
        operations = ContentStream(stream, reader).operations
        color = (0.0, 0.0, 0.0)
        stack = []
        points = []
        for operands, operator in operations:
            values = [float(value) for value in operands] if operator in (b"rg", b"g", b"re", b"m", b"l") else []
            if operator == b"q":
                stack.append(color)
            elif operator == b"Q":
                require(bool(stack), "Unbalanced graphics-state restore in path fixture")
                color = stack.pop()
            elif operator == b"rg":
                color = tuple(values)
            elif operator == b"g":
                color = (values[0],) * 3
            elif operator == b"re":
                x, y, width, height = values
                points.extend([(x, y), (x + width, y + height)])
            elif operator in (b"m", b"l"):
                points.append(tuple(values))
            elif operator in (b"f", b"f*", b"B", b"B*", b"b", b"b*"):
                result.append({"Location": location, "Color": list(color), "Points": points})
                points = []
            elif operator in (b"n", b"S", b"s"):
                points = []
    return result


def color_count(paints: list[dict[str, Any]], color: list[float]) -> int:
    return sum(all(abs(a - b) < 0.001 for a, b in zip(item["Color"], color)) for item in paints)


def verify_rendered_geometry(directory: Path, native: dict[str, Any], renderer: str) -> dict[str, Any]:
    """Compare retained-region pixels with a renderer independent of PDFium."""
    visual = directory / "visual"
    visual.mkdir(exist_ok=True)
    evidence = []
    for case in native["Result"]["Cases"]:
        if case["Name"] not in ("partial-text-naive-settext", "partial-text-preserve-positions", "shared-form-single-instance", "whole-vector-path"):
            continue
        images = []
        for suffix, path in (("input", case["Input"]), ("output", case["Output"])):
            prefix = visual / f"{case['Name']}.{suffix}"
            command = [renderer, "-r", "144", "-png", path, str(prefix)]
            result = subprocess.run(command, capture_output=True, text=True, check=False)
            require(result.returncode == 0, f"Independent rendering failed for {path}: {result.stderr}")
            pngs = sorted(visual.glob(prefix.name + "-*.png"))
            require(bool(pngs), f"Independent rendering produced no images for {path}")
            images.append(pngs)
        record: dict[str, Any] = {"Name": case["Name"], "Renderer": str(Path(renderer).resolve()),
                                 "Dpi": 144, "Images": [{"Path": str(path), "SHA256": sha256(path)}
                                                        for group in images for path in group]}
        original = Image.open(images[0][0]).convert("RGB")
        saved = Image.open(images[1][0]).convert("RGB")
        require(original.size == saved.size, "Candidate page geometry changed")
        if case["Name"].startswith("partial-text-"):
            # The source uses an identity text transform and 14pt Helvetica.
            # The right retained string is followed only by white page space.
            x, y = case["OriginalRightOrigin"]
            region = (int(x * 2) - 1, int((300 - y - 16) * 2),
                      min(original.width, int((x + 110) * 2)), int((300 - y + 5) * 2))
            same = ImageChops.difference(original.crop(region), saved.crop(region)).getbbox() is None
            expected = case["Name"] == "partial-text-preserve-positions"
            require(same == expected, f"{case['Name']}: independent retained-right-region comparison contradicted expected geometry")
            record.update({"RetainedRightCropPixels": list(region), "RetainedRightPixelsUnchanged": same})
        elif case["Name"] == "shared-form-single-instance":
            require(len(images[0]) == len(images[1]) == 2, "Shared Form rendering must have two pages")
            page1_before = Image.open(images[0][1]).convert("RGB")
            page1_after = Image.open(images[1][1]).convert("RGB")
            require(ImageChops.difference(page1_before, page1_after).getbbox() is None,
                    "Retained shared Form page 1 changed visually")
            record["RetainedPageOnePixelsUnchanged"] = True
        else:
            # An opaque overlay could remove the visible rectangle, so this
            # pixel check supplements the decoded-operator audit above.
            red_center = (140, 400)  # PDF (70,100), 2 px per point, page height 300.
            blue_center = (440, 400)  # PDF (220,100).
            require(original.getpixel(red_center) == (255, 0, 0), "Input target rectangle is not red")
            require(saved.getpixel(red_center) == (255, 255, 255), "Removed target rectangle is not white")
            require(original.getpixel(blue_center) == saved.getpixel(blue_center) == (0, 0, 255),
                    "Retained blue rectangle changed visually")
            record.update({"TargetRedRemoved": True, "RetainedBlueUnchanged": True})
        evidence.append(record)
    return {"Renderer": renderer, "Cases": evidence, "IndependentRenderedChecksPassed": True}


def inspect(directory: Path, native: dict[str, Any]) -> dict[str, Any]:
    require(native.get("Probe") == "redaction-advanced", "Incorrect probe identity")
    require(native.get("ExpectationsPassed") is True, "Native expectations did not all pass")
    cases = native["Result"]["Cases"]
    require(len(cases) == len(CASES) and {case["Name"] for case in cases} == CASES,
            "The report must contain each of the seven fixed advanced cases exactly once")
    reports = []
    for case in cases:
        name = case["Name"]
        source = exact_path(directory, case["Input"], name + ".input.pdf")
        require(sha256(source) == case["InputSha256"], f"{name}: input hash mismatch")
        secrets = ([RANGE_SECRET] if name.startswith("partial-text-") else [FORM_SECRET] if name == "shared-form-single-instance"
                   else [RANGE_SECRET, HIDDEN_SECRET] if name == "hidden-annotation-form-native-leak" else [])
        before = audit(source, secrets)
        require(not before["Failures"], f"{name}: input decoding failed")
        item: dict[str, Any] = {"Name": name, "InputAudit": before}
        if case["Output"] is None:
            require(case["Decision"] == "rejected-before-mutation", f"{name}: refusal decision missing")
            require(sha256(source) == case["InputUnchangedSha256"], f"{name}: refusal mutated input")
            require(not (directory / (name + ".native-candidate.pdf")).exists(), f"{name}: refusal left output")
            if name == "ligature-character-refusal":
                require("ffi" in before["PageText"][0], "The ligature fixture must decode one code to ffi")
            else:
                paints = content_paints(source)
                require(color_count(paints, [1, 0, 0]) == 1 and color_count(paints, [0, 0, 1]) == 1,
                        "Partial-path refusal input must retain both original painted paths")
            item.update({"Classification": "explicitly-rejected", "ExpectedResultObserved": True})
            reports.append(item)
            continue
        destination = exact_path(directory, case["Output"], name + ".native-candidate.pdf")
        require(sha256(destination) == case["OutputSha256"], f"{name}: output hash mismatch")
        require(case_checks(case), f"{name}: nonempty successful native checks required")
        after = audit(destination, secrets)
        require(not after["Failures"], f"{name}: output decoding failed")
        require(after["PreviousXrefPointer"] is None and after["EndOfFileMarkerCountHeuristic"] == 1,
                f"{name}: candidate must use full rewrite with no previous revision")
        item["OutputAudit"] = after
        if name.startswith("partial-text-"):
            require(before["SecretOccurrences"], f"{name}: source secret is absent")
            require(not after["SecretOccurrences"] and not after["RawSecretMarkers"], f"{name}: decoded secret survived")
            require("KEEP_LEFT" in after["PageText"][0] and "KEEP_RIGHT" in after["PageText"][0],
                    f"{name}: adjacent retained text missing")
            if name == "partial-text-naive-settext":
                require(case["AdjacentGeometryPreserved"] is False and abs(case["RightShiftPoints"]) > 1,
                        "The naive SetText negative test must show adjacent movement")
                classification = "unsafe-adjacent-geometry-changed"
            else:
                require(case["AdjacentGeometryPreserved"] is True and abs(case["RightShiftPoints"]) < 0.01,
                        "The bounded ASCII candidate must preserve adjacent origins")
                classification = "bounded-ascii-and-position-candidate"
            item.update({"Classification": classification,
                         "RightShiftPoints": case["RightShiftPoints"],
                         "GeometryIndependentVerification": "Poppler image crop comparison required and recorded separately"})
        elif name == "shared-form-single-instance":
            require(len(before["PageText"]) == len(after["PageText"]) == 2, "Shared Form case requires two pages")
            require(all(FORM_SECRET in text for text in before["PageText"]), "Both source pages must invoke shared Form")
            require(FORM_SECRET not in after["PageText"][0] and "KEEP_PAGE_ZERO" in after["PageText"][0],
                    "Shared Form deletion leaked or removed control on page 0")
            require(after["PageText"][1] == before["PageText"][1] and FORM_SECRET in after["PageText"][1],
                    "Page 1 must retain the exact shared content")
            require(after["SecretOccurrences"], "Shared secret must intentionally remain available on retained page")
            require(not after["PageResources"][0]["XObjects"] and after["PageResources"][1]["XObjects"],
                    "Page 0 must drop its Form resource; page 1 must retain a Form resource")
            item["Classification"] = "page-scoped-shared-instance-preservation"
        elif name == "hidden-annotation-form-native-leak":
            require(RANGE_SECRET not in after["PageText"][0] and "KEEP_VISIBLE_TEXT" in after["PageText"][0],
                    "Native active page edit must remove target and preserve control")
            hidden_hits = [hit for hit in after["SecretOccurrences"] if hit["Secret"] == HIDDEN_SECRET]
            require(bool(hidden_hits), "The deliberately unsafe hidden marker was unexpectedly absent")
            root = PdfReader(destination, strict=True).trailer["/Root"]
            require("/AcroForm" in root and "/OpenAction" in root,
                    "Native-only candidate must expose retained form and action counterexamples")
            item.update({"Classification": "unsafe-hidden-annotation-form-action-data-retained", "RetainedHiddenHits": hidden_hits})
        else:
            before_paints = content_paints(source, all_streams=True)
            after_paints = content_paints(destination, all_streams=True)
            require(color_count(before_paints, [1, 0, 0]) == 1, "Source must contain exactly one target red painted path")
            require(color_count(after_paints, [1, 0, 0]) == 0, "Red target path survives in a decoded current stream")
            require(color_count(before_paints, [0, 0, 1]) == color_count(after_paints, [0, 0, 1]) == 1,
                    "Retained blue path must survive")
            require("KEEP_VISIBLE_TEXT" in after["PageText"][0], "Retained text missing")
            item.update({"Classification": "whole-path-object-removal", "InputPaints": before_paints, "OutputPaints": after_paints})
        item["ExpectedResultObserved"] = True
        reports.append(item)
    return {"Cases": reports, "ExpectedResultsObserved": True}


def self_test(directory: Path, native: dict[str, Any]) -> list[dict[str, Any]]:
    results = []
    missing = copy.deepcopy(native)
    missing["Result"]["Cases"].pop()
    try:
        inspect(directory, missing)
    except (ValueError, KeyError, FileNotFoundError):
        results.append({"Name": "missing-case-fails-closed", "Passed": True})
    else:
        raise ValueError("Missing-case self test was incorrectly accepted")
    duplicate = copy.deepcopy(native)
    duplicate["Result"]["Cases"][-1] = copy.deepcopy(duplicate["Result"]["Cases"][0])
    try:
        inspect(directory, duplicate)
    except (ValueError, KeyError, FileNotFoundError):
        results.append({"Name": "duplicate-case-fails-closed", "Passed": True})
    else:
        raise ValueError("Duplicate-case self test was incorrectly accepted")
    tampered = copy.deepcopy(native)
    tampered["Result"]["Cases"][0]["OutputSha256"] = "0" * 64
    try:
        inspect(directory, tampered)
    except (ValueError, KeyError, FileNotFoundError):
        results.append({"Name": "tampered-output-hash-fails-closed", "Passed": True})
    else:
        raise ValueError("Tampered-output self test was incorrectly accepted")
    wrong_page = copy.deepcopy(native)
    target = next(case for case in wrong_page["Result"]["Cases"] if case["Name"] == "shared-form-single-instance")
    target["Output"] = target["Input"]
    try:
        inspect(directory, wrong_page)
    except (ValueError, KeyError, FileNotFoundError):
        results.append({"Name": "wrong-artifact-path-fails-closed", "Passed": True})
    else:
        raise ValueError("Wrong artifact path self test was incorrectly accepted")
    # Mutate real copied artifact bytes, preserving original evidence. The
    # appended comment leaves a syntactically readable PDF, so rejection must
    # come from evidence integrity rather than an incidental parsing failure.
    with tempfile.TemporaryDirectory(prefix="audit-self-test-", dir=directory) as temporary:
        copied_directory = Path(temporary)
        require(copied_directory.resolve().is_relative_to(directory.resolve()), "Temporary evidence copy escaped its run directory")
        copied_native = copy.deepcopy(native)
        for case in copied_native["Result"]["Cases"]:
            for key in ("Input", "Output"):
                if case[key] is not None:
                    original = Path(case[key])
                    copied = copied_directory / original.name
                    copied.write_bytes(original.read_bytes())
                    case[key] = str(copied)
        target = Path(copied_native["Result"]["Cases"][0]["Output"])
        target.write_bytes(target.read_bytes() + b"\n% intentional audit self-test tamper\n")
        try:
            inspect(copied_directory, copied_native)
        except (ValueError, KeyError, FileNotFoundError):
            results.append({"Name": "actual-artifact-byte-tamper-fails-closed", "Passed": True})
        else:
            raise ValueError("Actual artifact tampering was incorrectly accepted")
    return results


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True, help="P0 run root")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--renderer", default=shutil.which("pdftoppm"), help="Independent Poppler pdftoppm executable")
    args = parser.parse_args()
    directory = args.output.resolve() / "redaction-advanced"
    destination = directory / "independent-audit.json"
    destination.unlink(missing_ok=True)
    (directory / "independent-audit.failure.json").unlink(missing_ok=True)
    try:
        report_path = directory / "result.json"
        native = json.loads(report_path.read_text(encoding="utf-8"))
        inspected = inspect(directory, native)
        require(args.renderer, "An independent pdftoppm renderer is required")
        rendered = verify_rendered_geometry(directory, native, args.renderer)
        tests = self_test(directory, native) if args.self_test else []
        report = {"Probe": "redaction-advanced-independent-audit", "CompletedUtc": datetime.now(timezone.utc).isoformat(),
                  "Status": "restricted-expected-results-confirmed", "Python": platform.python_version(), "PyPdf": pypdf.__version__,
                  "NativeReportSha256": sha256(report_path), **inspected, "RenderedEvidence": rendered, "FailClosedSelfTests": tests,
                  "Limitations": ["Bounded fixture verification does not certify arbitrary PDF redaction.",
                                  "Partial text geometry is also compared using independent Poppler rendered images.",
                                  "The shared Form secret remains intentionally on page 1; document-wide deletion is a different scope.",
                                  "Hidden information cleanup must pass the separate structural-writer audit."]}
        destination.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(json.dumps({"Report": str(destination), "Status": report["Status"],
                          "Cases": len(report["Cases"]), "FailClosedSelfTests": len(tests)}, indent=2))
        return 0
    except Exception as exc:
        # No stale successful report is retained on failure.
        failure = {"Probe": "redaction-advanced-independent-audit", "Status": "failed-closed", "Error": repr(exc)}
        (directory / "independent-audit.failure.json").write_text(json.dumps(failure, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(failure, indent=2))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
