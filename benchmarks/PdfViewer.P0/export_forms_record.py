"""Export the P0 native AcroForm matrix and CJK resource-scheme evidence."""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    root, output = Path(args.run).resolve(), Path(args.output).resolve()
    output.unlink(missing_ok=True)
    repo = Path(__file__).resolve().parents[2]
    sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest().upper()
    read = lambda path: json.loads(path.read_text(encoding="utf-8-sig"))
    require = lambda condition, message: None if condition else (_ for _ in ()).throw(RuntimeError(message))
    completed = read(root / "p0-run.json")
    require(completed["Status"] == "experiments-verified-with-open-release-gates" and completed["AdvancedAcroFormFeasibilityExecuted"], "Incomplete run")
    baseline = read(root / "baseline/baseline.json")
    require(sha(root / "baseline/source-manifest.json") == baseline["SourceManifestSHA256"], "Stale baseline manifest")
    require(sha(root / "baseline/source-snapshot.zip") == baseline["SnapshotSHA256"], "Stale baseline archive")
    execution = read(root / "execution-manifest.json")
    for item in execution["Files"]:
        require(sha(repo / item["Path"]) == item["SHA256"], "Changed execution source: " + item["Path"])
    frozen = read(root / "baseline/source-manifest.json")
    product_paths = ("src/NT.AvaloniaControl.PdfViewer/", "src/NT.AvaloniaControl.PdfViewer.Themes/")
    for item in frozen:
        if item["Path"].startswith(product_paths):
            require(sha(repo / item["Path"]) == item["SHA256"], "Changed product source: " + item["Path"])
    parent_path = repo / "docs/validation/pdfviewer-p0-font-subset-20261015.json"
    parent = read(parent_path)
    parent_root = repo / "artifacts/pdfviewer" / parent["runId"]
    parent_sources = {item["Path"]: item["SHA256"] for item in read(parent_root / "baseline/source-manifest.json") if item["Path"].startswith(product_paths)}
    current_sources = {item["Path"]: item["SHA256"] for item in frozen if item["Path"].startswith(product_paths)}
    require(parent_sources == current_sources, "This benchmark-only record cannot inherit package validation after product changes")
    product_hash = read(root / "font-subset/result.json")["Result"]["FunctionalAssemblySHA256"]
    require(product_hash == parent["fontSubsetting"]["result"]["FunctionalAssemblySHA256"], "Product binary differs from prior package validation")
    summaries = {}
    evidence = set()
    native_modules = set()
    for probe in ("save", "fonts", "forms", "redaction", "baseline", "production", "redaction-advanced", "production-redaction", "font-subset", "forms-advanced", "cjk-forms"):
        report = read(root / probe / "result.json")
        require(report["ExpectationsPassed"] and report["ManagedAssemblySHA256"] == execution["AssemblySHA256"], "Mixed/failing executable: " + probe)
        require(sha(Path(report["NativeModule"]["Path"])) == report["NativeModule"]["SHA256"], "Changed native library")
        native_modules.add(report["NativeModule"]["SHA256"])
        evidence.add(root / probe / "result.json")
    require(len(native_modules) == 1, "Mixed native libraries")
    for probe, filename in (("forms-advanced", "verify_forms_advanced.py"), ("cjk-forms", "verify_cjk_forms.py")):
        folder = root / probe
        native, audit, visual = (read(folder / name) for name in ("result.json", "independent-audit.json", "manual-visual-review.json"))
        native_hash = sha(folder / "result.json")
        require(audit["passed"] and visual["passed"] and all(item["passed"] for item in audit["checks"]), "Failing independent/visual evidence: " + probe)
        require(audit["nativeReportSHA256"] == native_hash and visual["nativeReportSHA256"] == native_hash, "Stale report binding: " + probe)
        require(visual["independentAuditSHA256"] == sha(folder / "independent-audit.json"), "Stale visual audit binding")
        require(audit["scriptSHA256"] == sha(Path(__file__).with_name(filename)) and audit["helperSHA256"] == sha(Path(__file__).with_name("forms_verifier_common.py")), "Stale verifier source")
        require(len(audit.get("negativeControls", [])) >= 6 and all(item["passed"] and item["expectedRejectionDetected"] for item in audit["negativeControls"]), "Missing semantic negative controls")
        for item in native["Result"]["Outputs"]:
            require(sha(folder / item["File"]) == item["SHA256"], "Stale PDF: " + item["File"])
        for item in audit["rasters"]:
            require(sha(folder / item["pdf"]) == item["pdfSHA256"] and sha(folder / item["png"]) == item["pngSHA256"], "Stale render binding")
        for item in visual["images"]:
            require(sha(folder / item["file"]) == item["sha256"], "Stale visual image")
        for item in audit["negativeControls"]:
            require(sha(folder / item["report"]) == item["reportSHA256"], "Stale negative control")
        summaries[probe] = {"result": native["Result"], "nativeReportSHA256": native_hash,
            "independentCheckCount": len(audit["checks"]), "independentCheckIds": [item["id"] for item in audit["checks"]],
            "negativeControls": audit["negativeControls"], "visualReview": visual, "limitations": audit["limitations"]}
        evidence.update(path for path in folder.rglob("*") if path.is_file() and path.suffix.lower() in (".json", ".pdf", ".png", ".txt"))
    regressions = []
    for suite in ("Core", "Headless"):
        path = root / f"logs/regression-{suite}.xml"
        assembly = ET.parse(path).getroot().find("assembly")
        require(assembly is not None and assembly.get("failed") == "0" and assembly.get("errors") == "0", "Failing regression")
        regressions.append({"suite": suite, **{key: assembly.get(key) for key in ("total", "passed", "failed", "errors", "skipped")}, "xmlSHA256": sha(path)})
        evidence.add(path)
    # Re-executed suites are explicit; package consumers/builds remain prior evidence
    # for an identical product binary, never relabelled as current-run execution.
    for name in ("independent-verification.json", "production/independent-audit.json", "redaction-advanced/independent-audit.json", "production-redaction/independent-audit.json", "font-subset/independent-audit.json"):
        audit = read(root / name)
        passed = audit.get("passed") if name != "redaction-advanced/independent-audit.json" else audit.get("Status") == "restricted-expected-results-confirmed" and all(item["Passed"] for item in audit["FailClosedSelfTests"])
        require(passed, "Failing prior-feature regression: " + name)
        evidence.add(root / name)
    evidence.update(root / name for name in ("p0-run.json", "execution-manifest.json", "baseline/baseline.json", "baseline/source-manifest.json", "baseline/source-snapshot.zip", "baseline/native-assets.json", "baseline/targets.json", "baseline/official-documents/manifest.json", "font-subset/sources/source-manifest.json"))
    evidence.update(path for path in (root / "font-subset/sources").iterdir() if path.is_file())
    if (root / "forms-native-research").is_dir():
        evidence.update(path for path in (root / "forms-native-research").rglob("*") if path.is_file())
    record = {"schemaVersion": 5, "date": completed["CompletedUtc"][:10], "runId": root.name,
        "exportedUtc": dt.datetime.now(dt.timezone.utc).isoformat(), "exporterSHA256": sha(Path(__file__)),
        "phase": "P0-C03 advanced AcroForm native host and CJK editable font-resource scheme",
        "status": "bounded field and font experiments independently verified; full G0 remains open",
        "G0Complete": False, "productionFormsImplemented": False, "productEditingReady": False,
        "baseline": baseline, "frozenProductSourceUnchanged": True, "productSourceUnchangedSinceParent": True,
        "executionAssemblySHA256": execution["AssemblySHA256"], "functionalAssemblySHA256": product_hash,
        "nativeLibrarySHA256": next(iter(native_modules)),
        "parentRecord": {"path": str(parent_path.relative_to(repo)).replace("\\", "/"), "sha256": sha(parent_path), "runId": parent["runId"],
            "scope": "Prior production package consumers, builds and dependency audit for identical product source/binary; not rerun or counted as new forms coverage"},
        "advancedAcroForms": summaries["forms-advanced"], "cjkFormFonts": summaries["cjk-forms"], "regression": regressions,
        "platform": {"executed": "Windows x64 headless native input, pypdf/fontTools structure and Poppler rendering", "UbuntuLtsX64": "not executed", "KylinX64Arm64": "not executed", "physicalIME": "not executed", "officialRuntimePerformance": "not compared"},
        "openG0Items": ["Production form-session/UI lifecycle, physical IME and external interactive editor validation", "Representative real-world corpus and remaining font/layout compatibility", "Character/glyph-cluster and partial-image redaction; broader complex painting", "Ubuntu/Kylin native devices and runnable official reference/UI performance"],
        "evidence": [{"path": str(path.relative_to(root)).replace("\\", "/"), "sha256": sha(path), "bytes": path.stat().st_size} for path in sorted(evidence)]}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "evidence": len(evidence), "advancedChecks": summaries["forms-advanced"]["independentCheckCount"], "cjkChecks": summaries["cjk-forms"]["independentCheckCount"]}))


if __name__ == "__main__":
    main()
