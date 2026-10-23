"""Bind the published-corpus operation matrix to its frozen executable and independent evidence."""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    root, output = Path(args.run).resolve(), Path(args.output).resolve()
    repo = Path(__file__).resolve().parents[2]
    sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest().upper()
    read = lambda path: json.loads(path.read_text(encoding="utf-8-sig"))
    completed = read(root / "p0-run.json")
    require(completed["Status"] == "experiments-verified-with-open-release-gates"
            and completed["RealCorpusFeasibilityExecuted"], "Incomplete real-corpus run")
    evidence = set()
    for step in completed["Steps"]:
        path = Path(step["Log"])
        require(step["ExitCode"] == 0 and sha(path) == step["LogSHA256"], "Changed/failing step: " + step["Name"])
        evidence.add(path)
    baseline = read(root / "baseline/baseline.json")
    require(sha(root / "baseline/source-manifest.json") == baseline["SourceManifestSHA256"], "Changed frozen manifest")
    require(sha(root / "baseline/source-snapshot.zip") == baseline["SnapshotSHA256"], "Changed source archive")
    execution = read(root / "execution-manifest.json")
    for item in execution["Files"]:
        require(sha(repo / item["Path"]) == item["SHA256"], "Changed executable source: " + item["Path"])
    frozen = read(root / "baseline/source-manifest.json")
    prefixes = ("src/NT.AvaloniaControl.PdfViewer/", "src/NT.AvaloniaControl.PdfViewer.Themes/")
    product = {item["Path"]: item["SHA256"] for item in frozen if item["Path"].startswith(prefixes)}
    require(all(sha(repo / path) == digest for path, digest in product.items()), "Product changed after freeze")
    parent_path = repo / "docs/validation/pdfviewer-p0-partial-redaction-20261015.json"
    parent = read(parent_path)
    parent_root = Path(parent.get("artifactRoot", repo / "artifacts/pdfviewer" / parent["runId"]))
    parent_product = {item["Path"]: item["SHA256"] for item in read(parent_root / "baseline/source-manifest.json")
                      if item["Path"].startswith(prefixes)}
    require(product == parent_product, "Historical evidence inheritance requires identical product source")
    reports, native_hashes = {}, set()
    for probe in ("save", "fonts", "forms", "redaction", "baseline", "real-corpus"):
        report = reports[probe] = read(root / probe / "result.json")
        require(report["ExpectationsPassed"] and report["ManagedAssemblySHA256"] == execution["AssemblySHA256"], "Mixed/failing probe: " + probe)
        require(sha(Path(report["NativeModule"]["Path"])) == report["NativeModule"]["SHA256"], "Changed native module")
        native_hashes.add(report["NativeModule"]["SHA256"])
    require(len(native_hashes) == 1, "Mixed native libraries")
    real = root / "real-corpus"
    result = reports["real-corpus"]["Result"]
    require(result["FunctionalAssemblySHA256"] == parent["functionalAssemblySHA256"], "Changed product binary")
    original = read(root / "independent-verification.json")
    require(original["passed"] and all(item["passed"] for item in original["checks"]), "Failing original P0 independent checks")
    audit_path, visual_path = real / "independent-audit.json", real / "manual-visual-review.json"
    audit, visual = read(audit_path), read(visual_path)
    require(audit["passed"] and audit["checks"] and all(item["passed"] for item in audit["checks"]), "Incomplete independent audit")
    require(audit["nativeReportSHA256"] == sha(real / "result.json")
            and audit["nativeAssemblySHA256"] == execution["AssemblySHA256"], "Stale executable/report binding")
    require(audit["scriptSHA256"] == sha(Path(__file__).with_name("verify_real_corpus.py"))
            and audit["helperSHA256"] == sha(Path(__file__).with_name("forms_verifier_common.py")), "Changed verifier source")
    config = Path(__file__).parent / "Fixtures/real-corpus-sources.json"
    require(audit["sourceManifestSHA256"] == audit["pinnedConfigurationSHA256"] == sha(config) == sha(real / "sources/manifest.json"), "Changed source/expectation matrix")
    preparation = read(real / "corpus.json")
    require(preparation["PreparationScriptSHA256"] == sha(Path(__file__).with_name("prepare_real_corpus.py")), "Changed source preparer")
    controls = audit["negativeControls"]
    require(len(controls) >= 12 and all(item["passed"] and item["expectedRejectionDetected"] and item["exit"] != 0 for item in controls), "Missing semantic negative controls")
    for item in controls:
        require(sha(real / item["report"]) == item["reportSHA256"], "Changed negative report")
    for item in audit["rasters"]:
        require(sha(real / item["pdf"]) == item["pdfSHA256"] and sha(real / item["png"]) == item["pngSHA256"]
                and sha(real / item["stderr"]) == item["stderrSHA256"], "Stale raster binding")
    require(visual["passed"] and visual["images"] and visual["independentAuditSHA256"] == sha(audit_path)
            and visual["nativeReportSHA256"] == sha(real / "result.json"), "Incomplete/stale manual visual review")
    for item in visual["images"]:
        require(sha(real / item["file"]) == item["sha256"], "Changed reviewed image")
    regressions = []
    for suite, count in (("Core", 67), ("Headless", 19)):
        path = root / f"logs/regression-{suite}.xml"
        assembly = ET.parse(path).getroot().find("assembly")
        require(assembly is not None and assembly.get("total") == assembly.get("passed") == str(count)
                and all(assembly.get(key) == "0" for key in ("failed", "errors", "skipped", "not-run")), "Incomplete regression: " + suite)
        regressions.append({"suite": suite, "total": count, "passed": count, "xmlSHA256": sha(path)})
        evidence.add(path)
    for folder in ("real-corpus", "baseline", "fonts", "fixtures", "save", "forms", "redaction"):
        evidence.update(path for path in (root / folder).rglob("*") if path.is_file())
    evidence.update(root / name for name in ("p0-run.json", "execution-manifest.json", "independent-verification.json"))
    operations = [{"source": case["Id"], "operation": op["Name"], "outcome": op["Outcome"],
                   "exceptionType": op["ExceptionType"], "reason": op["Reason"], "expectationMatched": op["ExpectationMatched"]}
                  for case in result["Cases"] for op in case["Operations"]]
    require(len(operations) == 37 and all(op["expectationMatched"] for op in operations), "Changed operation matrix")
    record = {"schemaVersion": 7, "date": completed["CompletedUtc"][:10], "runId": root.name,
        "artifactRoot": str(root).replace("\\", "/"), "exportedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "exporterSHA256": sha(Path(__file__)), "phase": "P0 published corpus, operation boundaries and finite feasibility closeout",
        "status": "initial real-corpus feasibility independently verified; official reference and device baseline dependencies remain open",
        "G0Complete": False, "P0TechnicalFeasibilityEvidenceComplete": True,
        "productionPartialRedactionImplemented": False, "productionFormsImplemented": False, "productEditingReady": False,
        "baseline": {**baseline, "SourceFileCount": len(frozen)}, "productSourceUnchangedSinceParent": True,
        "executionAssemblySHA256": execution["AssemblySHA256"], "functionalAssemblySHA256": result["FunctionalAssemblySHA256"],
        "nativeLibrarySHA256": next(iter(native_hashes)),
        "parentRecord": {"path": str(parent_path.relative_to(repo)).replace("\\", "/"), "sha256": sha(parent_path), "runId": parent["runId"],
            "scope": "Identical product source and DLL. Font/form/partial-redaction matrices, package consumers, Demo/Usage builds and dependency audit are historical evidence, not re-executed or counted as current coverage."},
        "corpus": {"sources": len(result["Cases"]), "pages": sum(case["Read"]["PageCount"] for case in result["Cases"]),
            "bytes": sum(case["Input"]["Bytes"] for case in result["Cases"]), "manifestSHA256": sha(config),
            "nativeCheckCount": len(result["Checks"]), "operations": operations, "accepted": sum(op["outcome"] == "accepted" for op in operations),
            "refused": sum(op["outcome"] == "refused" for op in operations), "readSamples": [case["Read"] for case in result["Cases"]]},
        "independent": {"checkCount": len(audit["checks"]), "auditSHA256": sha(audit_path), "negativeControls": controls,
            "rasterCount": len(audit["rasters"]), "limitations": audit["limitations"], "visualReview": visual},
        "regression": regressions,
        "phaseAssignments": {"P1": "Public contracts, lifetime/scheduling, Ubuntu native/CI", "P3": "Large corpus, UI/present/peak and official performance comparison",
            "P4": "Unified save transactions and bounded undo/history", "P5": "Complete annotations, new font formats/fallback/shaping and external editors",
            "P6": "Production form host, focus/virtualization/timers, input font/caret, layout/validation/IME", "P7": "Production partial redaction, selection/preview and broader painting/encoding compatibility",
            "P8": "Printing/sharing and devices", "P9": "Full platform, compatibility, stability and release acceptance"},
        "openG0Items": ["Licensed runnable official reference package/version and reference host", "Exact Ubuntu/Kylin images, patch/display/hardware availability baseline"],
        "platform": {"executed": "Current Windows x64 backend and Headless regression", "UbuntuLtsX64": "not executed", "KylinX64Arm64": "not executed", "officialPerformance": "not compared"},
        "evidence": [{"path": str(path.relative_to(root)).replace("\\", "/"), "sha256": sha(path), "bytes": path.stat().st_size} for path in sorted(evidence)]}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "evidence": len(evidence), "checks": len(audit["checks"]), "negativeControls": len(controls)}))


if __name__ == "__main__":
    main()
