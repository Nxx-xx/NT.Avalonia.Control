"""Independently inspect P0 native candidates; this is not a redaction engine.

Run with the same output root passed to PdfViewer.P0:
  python verify_redaction.py --output artifacts/pdfviewer/<run> --bridge-hidden

Every current xref object (including unreachable objects) is decoded. Content
operands are parsed as well, so converting a literal string to a PDF hex string
or compressing a stream cannot make a fixture secret pass this audit. This is a
bounded adversarial corpus check, not a proof for arbitrary real-world PDFs.
The optional bridge makes a fresh, deliberately restricted document from the
hidden-data fixture to validate a structural-writer route; Python is not added
as a production dependency.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
from collections import deque
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import pypdf
from pypdf import PdfReader, PdfWriter
from pypdf.generic import (
    ArrayObject,
    ByteStringObject,
    ContentStream,
    DictionaryObject,
    IndirectObject,
    StreamObject,
    TextStringObject,
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def secret_list(case: dict[str, Any]) -> list[str]:
    if "Secrets" in case:
        return list(case["Secrets"].values())
    return [case["Secret"]]


def reference_key(value: IndirectObject) -> tuple[int, int]:
    return value.idnum, value.generation


def audit(path: Path, secrets: list[str]) -> dict[str, Any]:
    raw = path.read_bytes()
    reader = PdfReader(path, strict=True)
    objects: dict[tuple[int, int], Any] = {}
    failures: list[dict[str, str]] = []
    occurrences: list[dict[str, str]] = []
    decoded_streams = 0
    parsed_content_streams = 0
    images: list[dict[str, Any]] = []
    form_streams: list[dict[str, Any]] = []
    refs = {
        (number, generation)
        for generation, entries in reader.xref.items()
        for number in entries
        if number != 0 and generation != 65535
    }
    refs.update((number, 0) for number in reader.xref_objStm)

    def scan_bytes(value: bytes, location: str) -> None:
        for secret in secrets:
            encodings = ("ascii", "utf-16-le", "utf-16-be")
            for encoding in encodings:
                if secret.encode(encoding) in value:
                    occurrences.append({"Secret": secret, "Location": location, "Encoding": encoding})

    def scan_values(value: Any, location: str) -> None:
        # References are audited through the complete xref pass rather than
        # resolving recursively, which also avoids cycles in page/outline trees.
        if isinstance(value, IndirectObject):
            return
        if isinstance(value, (ByteStringObject, bytes, bytearray)):
            scan_bytes(bytes(value), location)
        elif isinstance(value, (TextStringObject, str)):
            for secret in secrets:
                if secret in str(value):
                    occurrences.append({"Secret": secret, "Location": location, "Encoding": "parsed-string"})
        elif isinstance(value, DictionaryObject):
            for key, item in value.items():
                scan_values(str(key), location + "/key")
                scan_values(item, location + str(key))
        elif isinstance(value, (ArrayObject, list, tuple)):
            for index, item in enumerate(value):
                scan_values(item, f"{location}[{index}]")

    for number, generation in sorted(refs):
        label = f"{number} {generation} R"
        try:
            obj = reader.get_object(IndirectObject(number, generation, reader))
            objects[(number, generation)] = obj
            scan_values(obj, label)
            if not isinstance(obj, StreamObject):
                continue
            data = obj.get_data()
            decoded_streams += 1
            scan_bytes(data, label + "/decoded-stream")
            subtype = obj.get("/Subtype")
            if subtype == "/Image":
                images.append({"Object": label, "DecodedBytes": len(data), "Filter": str(obj.get("/Filter"))})
            elif subtype == "/Form":
                form_streams.append({"Object": label, "DecodedBytes": len(data)})
            # Page content streams generally have no /Type or /Subtype. Parsing
            # PDF operands detects secrets represented as <hex> text strings.
            if subtype == "/Form" or (obj.get("/Type") is None and subtype is None):
                content = ContentStream(obj, reader)
                parsed_content_streams += 1
                for index, (operands, operator) in enumerate(content.operations):
                    scan_values(operands, f"{label}/operation[{index}]/{operator.decode('ascii', 'replace')}")
        except Exception as exc:  # A decoding failure must prevent a safe verdict.
            failures.append({"Object": label, "Error": repr(exc)})

    reachable: set[tuple[int, int]] = set()
    pending: deque[Any] = deque([reader.trailer])
    while pending:
        value = pending.popleft()
        if isinstance(value, IndirectObject):
            key = reference_key(value)
            if key in reachable:
                continue
            reachable.add(key)
            if key in objects:
                pending.append(objects[key])
        elif isinstance(value, DictionaryObject):
            pending.extend(value.values())
        elif isinstance(value, (ArrayObject, list, tuple)):
            pending.extend(value)

    page_text: list[str] = []
    page_resources: list[dict[str, Any]] = []
    for index, page in enumerate(reader.pages):
        try:
            text = page.extract_text()
            page_text.append(text)
            scan_values(text, f"page[{index}]/independent-extracted-text")
            xobjects = page.get("/Resources", {}).get("/XObject", {})
            if isinstance(xobjects, IndirectObject):
                xobjects = xobjects.get_object()
            page_resources.append({
                "Page": index,
                "XObjects": [
                    {"Name": str(key), "Reference": str(value), "Subtype": str(value.get_object().get("/Subtype"))}
                    for key, value in xobjects.items()
                ],
                "HasThumbnail": "/Thumb" in page,
            })
        except Exception as exc:
            failures.append({"Object": f"page[{index}]", "Error": repr(exc)})
    return {
        "Path": str(path.resolve()), "Sha256": sha256(path),
        "XrefObjects": len(refs), "DecodedStreams": decoded_streams,
        "ParsedContentStreams": parsed_content_streams,
        "ReachableObjects": len(reachable),
        "UnreachableXrefObjects": [f"{n} {g} R" for n, g in sorted(refs - reachable)],
        "SecretOccurrences": occurrences,
        "RawSecretMarkers": [secret for secret in secrets if secret.encode("ascii") in raw],
        "PageText": page_text, "PageResources": page_resources,
        "ImageObjects": images, "FormObjects": form_streams,
        "PreviousXrefPointer": reader.trailer.get("/Prev"),
        "EndOfFileMarkerCountHeuristic": raw.count(b"%%EOF"),
        "HasInfo": "/Info" in reader.trailer,
        "CatalogKeys": sorted(str(key) for key in reader.trailer["/Root"].keys()),
        "Failures": failures,
    }


def hidden_bridge(directory: Path, secrets: list[str]) -> dict[str, Any]:
    source = directory / "hidden-revision.native-candidate.pdf"
    destination = directory / "hidden-revision.structural-bridge.pdf"
    reader = PdfReader(source)
    writer = PdfWriter()
    # This fixture contains ordinary text pages. A fresh catalog deliberately
    # omits original metadata, names/attachments, outlines, and structure trees.
    # This narrow experiment does not define preservation policy for real PDFs.
    for source_page in reader.pages:
        page = writer.add_page(source_page)
        for key in ("/Thumb", "/Metadata", "/StructParents", "/PieceInfo"):
            page.pop(key, None)
    writer.metadata = None
    writer.compress_identical_objects(remove_duplicates=False, remove_unreferenced=True)
    writer.write(destination)
    result = audit(destination, secrets)
    passed = (
        not result["SecretOccurrences"]
        and not result["Failures"]
        and result["PageText"] == ["KEEP_VISIBLE_TEXT"]
        and result["PreviousXrefPointer"] is None
        and result["EndOfFileMarkerCountHeuristic"] == 1
        and not result["ImageObjects"]
        and not result["HasInfo"]
    )
    return {
        "Name": "restricted-hidden-data-structural-bridge", "Passed": passed,
        "SourceSha256": sha256(source), "Audit": result,
        "Conclusion": "A fresh structural rewrite removed the six hidden fixture surfaces and old revision while retaining visible control text."
                      if passed else "The restricted structural bridge failed its checks; no safe rewrite conclusion is available.",
        "Limitations": [
            "Research bridge uses pypdf; no production Python dependency is introduced.",
            "Rebuilding this simple catalog deliberately discards outlines, attachments, structure, and metadata.",
            "This does not handle partial text/images, shared Form edits, signatures, encryption, or arbitrary unknown PDF content.",
            "A production writer must define preservation policy, validate every reachable and unreachable object, and fail closed on unsupported structures.",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True, help="P0 run root")
    parser.add_argument("--bridge-hidden", action="store_true")
    args = parser.parse_args()
    directory = args.output.resolve() / "redaction"
    target = directory / "independent-audit.json"
    # An exception before the final write must not leave a previous run's
    # success report in place. The caller must also honor our nonzero exit.
    target.unlink(missing_ok=True)
    native_path = directory / "result.json"
    native = json.loads(native_path.read_text(encoding="utf-8"))
    expected_names = {"whole-text", "whole-image", "whole-form", "hidden-and-previous-revision",
                      "shared-nested-form", "partial-character", "partial-image"}
    native_cases = native["Result"]["Cases"]
    if (len(native_cases) != len(expected_names) or
            {case["Name"] for case in native_cases} != expected_names):
        raise ValueError("Native report must contain each of the seven expected redaction cases exactly once.")
    cases: list[dict[str, Any]] = []
    expected_results = []
    for case in native_cases:
        secrets = secret_list(case)
        original = audit(Path(case["Input"]), secrets)
        input_matches = original["Sha256"] == case["InputSha256"]
        input_markers = all(
            secret in original["RawSecretMarkers"] or
            any(hit["Secret"] == secret for hit in original["SecretOccurrences"])
            for secret in secrets
        )
        item: dict[str, Any] = {"Name": case["Name"], "InputAudit": original,
                                "InputMatchesNativeReport": input_matches,
                                "InputContainsAllFixtureMarkers": input_markers}
        if case["Output"] is None:
            unchanged = sha256(Path(case["Input"])) == case["InputSha256"] == case["InputUnchangedSha256"]
            no_output = not list(directory.glob(case["Name"] + ".native-candidate.pdf"))
            item.update({"Classification": "explicitly-rejected", "InputUnchanged": unchanged,
                         "NoOutputProduced": no_output,
                         "ExpectedResultObserved": unchanged and no_output and input_markers})
        else:
            output = audit(Path(case["Output"]), secrets)
            item["OutputAudit"] = output
            output_matches = output["Sha256"] == case["OutputSha256"]
            item["OutputMatchesNativeReport"] = output_matches
            clean = not output["SecretOccurrences"] and not output["Failures"]
            if case["Name"] in ("whole-text", "whole-image", "whole-form"):
                checks = bool(case["Checks"]) and all(check["Passed"] for check in case["Checks"])
                preserved = "KEEP_VISIBLE_TEXT" in "\n".join(output["PageText"])
                success = clean and checks and preserved and input_matches and output_matches and input_markers
                if case["Name"] == "whole-form":
                    # Removing the top-level instance must also discard its
                    # exclusive nested Form streams and XObject resources.
                    resource_free = not output["FormObjects"] and all(
                        not page["XObjects"] for page in output["PageResources"]
                    )
                    item["ExclusiveFormResourcesRemoved"] = resource_free
                    success = success and resource_free
                item.update({"Classification": "allowed-primitive-fixture" if success else "unsafe-primitive-fixture",
                             "ExpectedResultObserved": success})
            else:
                # These are deliberate negative tests: native APIs may return
                # success while hidden or nested content survives the save.
                attempted = (bool(case["Checks"]) and all(check["Passed"] for check in case["Checks"])) if "Checks" in case else (
                    case["NativeChildRemovalReturnedSuccess"] and case["NativeGenerateReturnedSuccess"]
                )
                item.update({"Classification": "unsafe-native-candidate",
                             "ExpectedResultObserved": not clean and not output["Failures"] and attempted
                             and input_matches and output_matches and input_markers})
        expected_results.append(item["ExpectedResultObserved"])
        cases.append(item)
    hidden_case = next(case for case in native_cases if case["Name"] == "hidden-and-previous-revision")
    bridge = hidden_bridge(directory, secret_list(hidden_case)) if args.bridge_hidden else None
    if bridge:
        expected_results.append(bridge["Passed"])
    report = {
        "Probe": "redaction-independent-audit", "CompletedUtc": datetime.now(timezone.utc).isoformat(),
        "Python": platform.python_version(), "PyPdf": pypdf.__version__,
        "NativeReportSha256": sha256(native_path),
        "Status": "expected-restricted-results-confirmed" if all(expected_results) else "unexpected-result-requires-investigation",
        "Cases": cases, "StructuralBridge": bridge,
        "VisualEvidence": {
            "Directory": str(directory / "visual"),
            "AvailableImages": [{"Path": str(path), "Sha256": sha256(path)}
                                for path in sorted((directory / "visual").glob("*.png"))],
            "RequiresManualVisualReview": True,
            "Renderer": "Poppler pdftoppm 96 dpi",
            "RendererWarnings": "Bundled Windows Poppler reports missing Symbol/ArialUnicode display fonts; Helvetica fixture text rendered legibly.",
        },
        "Boundaries": [
            "Fixture markers cover all xref objects, decompressed streams, parsed content operands, dictionary strings, and independently extracted text.",
            "Raw-byte and EOF counts are additional diagnostics; compressed/hex text cannot be judged from raw absence alone.",
            "Current-object traversal plus previous-xref/EOF inspection does not replace forensic analysis of arbitrary damaged PDFs.",
            "Passing the three allowed primitive fixtures is not certification for arbitrary document redaction.",
        ],
    }
    target.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"Report": str(target), "Status": report["Status"],
                      "Cases": [{"Name": c["Name"], "Classification": c["Classification"], "Expected": c["ExpectedResultObserved"]} for c in cases],
                      "BridgePassed": bridge["Passed"] if bridge else None}, indent=2))
    return 0 if all(expected_results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
