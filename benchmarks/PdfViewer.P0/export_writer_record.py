"""Extend the original P0 record with production writer and advanced deletion evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--openssl", required=True)
    parser.add_argument("--poppler", required=True)
    args = parser.parse_args()
    root = Path(args.run).resolve()
    output = Path(args.output).resolve()
    read = lambda name: json.loads((root / name).read_text(encoding="utf-8-sig"))
    sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest().upper()
    subprocess.run([sys.executable, str(Path(__file__).with_name("export_p0_record.py")), "--run", str(root), "--output", str(output), "--openssl", args.openssl, "--poppler", args.poppler], check=True)
    record = json.loads(output.read_text(encoding="utf-8"))
    production = read("production/result.json")
    independent = read("production/independent-audit.json")
    advanced = read("redaction-advanced/independent-audit.json")
    consumers = read("package-consumer-results.json")
    if not independent["passed"] or advanced["Status"] != "restricted-expected-results-confirmed" or not consumers["Passed"]:
        raise RuntimeError("Incomplete or failing writer/advanced/package evidence.")
    if independent["nativeReportSHA256"] != sha(root / "production/result.json") or advanced["NativeReportSha256"] != sha(root / "redaction-advanced/result.json"):
        raise RuntimeError("Stale native report binding.")
    audit_log = root / "dependency-audit.log"
    if not audit_log.exists() or "NU190" in audit_log.read_text(encoding="utf-8-sig"):
        raise RuntimeError("Missing or failing dependency audit log.")
    repo = Path(__file__).resolve().parents[2]
    license_path = repo / "src/NT.AvaloniaControl.PdfViewer/Licenses/PDFsharp-6.2.4-SOURCES.json"
    licenses = json.loads(license_path.read_text(encoding="utf-8-sig"))
    writer_hash = production["Result"]["WriterAssembly"]["SHA256"]
    writer_package = next(x for x in licenses["packages"] if x["id"] == "PDFsharp")
    if not any(x["sha256"] == writer_hash for x in writer_package["runtimeAssets"]):
        raise RuntimeError("Actual PDFsharp runtime does not match verified package assets.")
    pkcs_package = next(x for x in licenses["packages"] if x["id"] == "System.Security.Cryptography.Pkcs")
    if pkcs_package["version"] != "10.0.13" or not any(x["sha256"] == production["Result"]["PkcsAssembly"]["SHA256"] for x in pkcs_package["runtimeAssets"]):
        raise RuntimeError("Security override runtime mismatch.")
    record["schemaVersion"] = 2
    record["phase"] = "P0-C01 bounded production writer and P0-C02 advanced deletion validation"
    record["status"] = "bounded production FreeText implemented and verified; full G0 remains open"
    record["boundedFreeTextBackendImplemented"] = True
    record["exporterSHA256"] = sha(Path(__file__))
    record["baseline"]["SourceFileCount"] = len(read("baseline/source-manifest.json"))
    record["parentRecord"] = {"path": "docs/validation/pdfviewer-p0-20261015.json", "sha256": sha(repo / "docs/validation/pdfviewer-p0-20261015.json"), "originalSourceSnapshotSHA256": "BDE9B89246C3D5111BEEF06B4778217242DB343626BCAA81218C3CAD1D322684"}
    record["productionWriter"] = {"result": production["Result"], "independentCheckCount": len(independent["checks"]), "independentCheckIds": [x["id"] for x in independent["checks"]], "negativeControls": independent["negativeControls"], "nativeReportSHA256": independent["nativeReportSHA256"]}
    record["advancedRedaction"] = {"status": advanced["Status"], "cases": [{k: v for k, v in item.items() if k not in {"InputAudit", "OutputAudit"}} for item in advanced["Cases"]], "negativeControls": advanced["FailClosedSelfTests"], "scope": "Research primitives only; no general production redaction API. Hidden annotation/form/action leaks remain explicit blockers."}
    record["dependencyAudit"] = {"passed": True, "logSHA256": sha(audit_log), "PDFsharp": "[6.2.4]", "PkcsOverride": "10.0.13", "licenseSourceSHA256": sha(license_path), "packages": [{k: x[k] for k in ["id", "version", "licenseExpression", "officialDownloadedSha256", "officialMatchesInstalled"]} for x in licenses["packages"]]}
    record["packageConsumers"] = [{"mode": c["Mode"], "passed": c["Passed"], "checks": c["CheckCount"], "packages": c["Products"]} for c in consumers["Consumers"]]
    if (root / "integration-build-results.json").exists():
        record["integrationBuilds"] = read("integration-build-results.json")
    record["openG0Items"] = ["Automatic font subsetting and broader font/shaping/layout formats", "External editor interaction and arbitrary PDF preservation", "Production hidden-surface sanitizer and general character-cluster/resource redaction", "Remaining AcroForm/CJK/IME matrix", "Ubuntu and Kylin physical validation; official runnable reference and representative UI performance"]
    names = ["production/result.json", "production/independent-audit.json", "redaction-advanced/result.json", "redaction-advanced/independent-audit.json", "redaction-advanced/manual-visual-review.json", "package-consumer-results.json", "package-consumer.log", "dependency-audit.log"]
    record["evidence"].extend({"path": name, "sha256": sha(root / name), "bytes": (root / name).stat().st_size} for name in names)
    for name in ["integration-build-results.json", "usage-build.log", "demo-build.log"]:
        if (root / name).exists():
            record["evidence"].append({"path": name, "sha256": sha(root / name), "bytes": (root / name).stat().st_size})
    for name in sorted((root / "redaction-advanced").glob("*.input.pdf")):
        record["corpus"].append({"path": str(name.relative_to(root)).replace("\\", "/"), "bytes": name.stat().st_size, "sha256": sha(name), "source": "Synthetic advanced deletion fixture", "scope": "Bounded research verification"})
    output.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "productionChecks": len(independent["checks"]), "advancedCases": len(advanced["Cases"]), "packageChecks": sum(c["CheckCount"] for c in consumers["Consumers"])}))


if __name__ == "__main__":
    main()
