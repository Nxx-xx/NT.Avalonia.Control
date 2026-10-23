"""Bind the P0 CJK-cluster and partial-image experiments to frozen source and independent evidence."""
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
    output.unlink(missing_ok=True)
    repo = Path(__file__).resolve().parents[2]
    sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest().upper()
    read = lambda path: json.loads(path.read_text(encoding="utf-8-sig"))
    completed = read(root / "p0-run.json")
    require(completed["Status"] == "experiments-verified-with-open-release-gates" and completed["PartialRedactionFeasibilityExecuted"], "Incomplete run")
    for step in completed["Steps"]:
        require(step["ExitCode"] == 0 and sha(Path(step["Log"])) == step["LogSHA256"], "Failing or changed run step: " + step["Name"])
    baseline = read(root / "baseline/baseline.json")
    require(sha(root / "baseline/source-manifest.json") == baseline["SourceManifestSHA256"], "Stale baseline manifest")
    require(sha(root / "baseline/source-snapshot.zip") == baseline["SnapshotSHA256"], "Stale baseline archive")
    execution = read(root / "execution-manifest.json")
    for item in execution["Files"]:
        require(sha(repo / item["Path"]) == item["SHA256"], "Changed execution source: " + item["Path"])
    frozen = read(root / "baseline/source-manifest.json")
    product_prefixes = ("src/NT.AvaloniaControl.PdfViewer/", "src/NT.AvaloniaControl.PdfViewer.Themes/")
    current_product = {item["Path"]: item["SHA256"] for item in frozen if item["Path"].startswith(product_prefixes)}
    require(all(sha(repo / path) == digest for path, digest in current_product.items()), "Product changed after freezing")
    parent_path = repo / "docs/validation/pdfviewer-p0-forms-20261015.json"
    parent = read(parent_path)
    parent_root = repo / "artifacts/pdfviewer" / parent["runId"]
    parent_product = {item["Path"]: item["SHA256"] for item in read(parent_root / "baseline/source-manifest.json") if item["Path"].startswith(product_prefixes)}
    require(current_product == parent_product, "Benchmark-only inheritance requires identical product source")
    product_hash = read(root / "production-redaction/result.json")["Result"]["FunctionalAssemblySHA256"]
    require(product_hash == parent["functionalAssemblySHA256"], "Product binary differs from parent package evidence")
    probes = ("save", "fonts", "forms", "redaction", "baseline", "production", "redaction-advanced", "production-redaction", "cjk-cluster-redaction", "partial-image-redaction")
    reports, evidence, native_hashes = {}, set(), set()
    for probe in probes:
        path = root / probe / "result.json"
        native = reports[probe] = read(path)
        require(native["ExpectationsPassed"] and native["ManagedAssemblySHA256"] == execution["AssemblySHA256"], "Mixed or failing executable: " + probe)
        require(sha(Path(native["NativeModule"]["Path"])) == native["NativeModule"]["SHA256"], "Native library changed")
        native_hashes.add(native["NativeModule"]["SHA256"])
        evidence.add(path)
    require(len(native_hashes) == 1, "Mixed native libraries")
    regression = []
    for suite, count in (("Core", 67), ("Headless", 19)):
        path = root / f"logs/regression-{suite}.xml"
        assembly = ET.parse(path).getroot().find("assembly")
        require(assembly is not None and assembly.get("total") == assembly.get("passed") == str(count)
                and all(assembly.get(key) == "0" for key in ("failed", "errors", "skipped", "not-run")), "Incomplete regression: " + suite)
        regression.append({"suite": suite, **{key: assembly.get(key) for key in ("total", "passed", "failed", "errors", "skipped")}, "xmlSHA256": sha(path)})
        evidence.add(path)
    old_audits = {}
    for probe, script in (("production", "verify_production_writer.py"), ("production-redaction", "verify_production_redaction.py")):
        path = root / probe / "independent-audit.json"
        audit = read(path)
        require(audit["passed"] and all(item["passed"] for item in audit["checks"]), "Failing existing-feature audit: " + probe)
        require(audit["nativeReportSHA256"] == sha(root / probe / "result.json") and audit["scriptSHA256"] == sha(Path(__file__).with_name(script)), "Stale existing-feature binding: " + probe)
        old_audits[probe] = {"checks": len(audit["checks"]), "negativeControls": len(audit.get("negativeControls", [])), "auditSHA256": sha(path)}
        evidence.add(path)
    original = read(root / "independent-verification.json")
    require(original["passed"] and all(item["passed"] for item in original["checks"]), "Failing original P0 audit")
    advanced_path = root / "redaction-advanced/independent-audit.json"
    advanced = read(advanced_path)
    require(advanced["Status"] == "restricted-expected-results-confirmed" and advanced["NativeReportSha256"].upper() == sha(root / "redaction-advanced/result.json")
            and all(item["Passed"] for item in advanced["FailClosedSelfTests"]), "Stale/failing advanced-redaction audit")
    evidence.update((root / "independent-verification.json", advanced_path))
    audit_path, visual_path = (root / "partial-redaction" / name for name in ("independent-audit.json", "manual-visual-review.json"))
    audit, visual = read(audit_path), read(visual_path)
    require(audit["passed"] and visual["passed"] and bool(audit["checks"]) and all(item["passed"] for item in audit["checks"]), "Failing partial-redaction independent/visual audit")
    new_probes = ("cjk-cluster-redaction", "partial-image-redaction")
    expected_reports = {probe: sha(root / probe / "result.json") for probe in new_probes}
    for container in (audit, visual):
        bindings = container["nativeReports"]
        require(len(bindings) == 2 and {item["probe"]: item["sha256"] for item in bindings} == expected_reports, "Stale new native report binding")
    require(visual["independentAuditSHA256"] == sha(audit_path), "Stale visual audit binding")
    require(audit["scriptSHA256"] == sha(Path(__file__).with_name("verify_partial_redaction.py")), "Stale partial verifier source")
    require(audit["helperSHA256"] == sha(Path(__file__).with_name("partial_redaction_common.py")), "Stale partial syntax helper")
    require(audit["fontTools"]["helperSHA256"] == sha(Path(__file__).with_name("partial_redaction_font_inspector.py")), "Stale independent glyph helper")
    require(audit["fontTools"]["jobsSHA256"] == sha(root / "partial-redaction/font-jobs.json")
            and audit["fontTools"]["analysisSHA256"] == sha(root / "partial-redaction/font-analysis.json"), "Stale font analysis")
    controls = audit["negativeControls"]
    require(len(controls) >= 8 and all(item["passed"] and item["expectedRejectionDetected"] and item["exit"] != 0 for item in controls), "Missing semantic negative controls")
    for item in controls:
        require(sha(root / item["report"]) == item["reportSHA256"], "Changed negative-control report")
    for item in audit["rasters"]:
        require(sha(root / item["pdf"]) == item["pdfSHA256"] and sha(root / item["png"]) == item["pngSHA256"], "Stale raster binding")
    require(bool(visual["images"]), "No manually reviewed images")
    for item in visual["images"]:
        require(sha(root / item["file"]) == item["sha256"], "Changed visual image")
    for probe in new_probes:
        for item in reports[probe]["Result"]["Outputs"]:
            require(sha(root / probe / item["File"]) == item["SHA256"], "Changed partial-redaction PDF")
    for folder in (*new_probes, "partial-redaction"):
        evidence.update(path for path in (root / folder).rglob("*") if path.is_file() and path.suffix.lower() in (".json", ".pdf", ".png", ".txt"))
    evidence.update(root / name for name in ("p0-run.json", "execution-manifest.json", "baseline/baseline.json", "baseline/source-manifest.json", "baseline/source-snapshot.zip", "baseline/native-assets.json", "baseline/targets.json", "baseline/official-documents/manifest.json"))
    evidence.update(path for path in (root / "font-subset/sources").iterdir() if path.is_file())
    if (root / "pilot-diagnostics").is_dir():
        evidence.update(path for path in (root / "pilot-diagnostics").iterdir() if path.is_file())
    record = {"schemaVersion": 6, "date": completed["CompletedUtc"][:10], "runId": root.name,
        "exportedUtc": dt.datetime.now(dt.timezone.utc).isoformat(), "exporterSHA256": sha(Path(__file__)),
        "phase": "P0-C02 bounded CJK grapheme/glyph-cluster and partial-image content redaction feasibility",
        "status": "bounded partial-redaction experiments independently verified; general production capability and G0 remain open",
        "G0Complete": False, "productionPartialRedactionImplemented": False, "productEditingReady": False,
        "baseline": {**baseline, "SourceFileCount": len(frozen)}, "frozenProductSourceUnchanged": True, "productSourceUnchangedSinceParent": True,
        "executionAssemblySHA256": execution["AssemblySHA256"], "functionalAssemblySHA256": product_hash, "nativeLibrarySHA256": next(iter(native_hashes)),
        "parentRecord": {"path": str(parent_path.relative_to(repo)).replace("\\", "/"), "sha256": sha(parent_path), "runId": parent["runId"],
            "scope": "Identical product source/binary; prior package consumers, builds, dependency audit, font-subset and advanced-forms matrix/manual reviews remain historical, not re-executed or counted as new partial-redaction coverage"},
        "cjkClusterRedaction": reports[new_probes[0]]["Result"], "partialImageRedaction": reports[new_probes[1]]["Result"],
        "independent": {"checkCount": len(audit["checks"]), "checkIds": [item["id"] for item in audit["checks"]], "nativeReports": audit["nativeReports"],
            "negativeControls": controls, "auditSHA256": sha(audit_path), "limitations": audit["limitations"], "visualReview": visual},
        "regression": regression, "currentExistingFeatureAudits": old_audits,
        "platform": {"executed": "Windows x64 C# experiments and independent pypdf/fontTools/Poppler", "UbuntuLtsX64": "not executed", "KylinX64Arm64": "not executed", "officialRuntimePerformance": "not compared"},
        "openG0Items": ["General selection, PDF font/encoding/layout and arbitrary complex painting compatibility", "Production partial-redaction transaction/API and UI; real-world corpus and resource/performance budgets", "Production form host/IME, broader fonts/layout and external interactive editors", "Ubuntu/Kylin devices and runnable official reference/performance"],
        "evidence": [{"path": str(path.relative_to(root)).replace("\\", "/"), "sha256": sha(path), "bytes": path.stat().st_size} for path in sorted(evidence)]}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "evidence": len(evidence), "independentChecks": len(audit["checks"]), "negativeControls": len(controls)}))


if __name__ == "__main__":
    main()
