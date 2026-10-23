"""Bind the bounded production sanitization/redaction evidence to the P0 baseline."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--openssl", required=True)
    parser.add_argument("--poppler", required=True)
    args = parser.parse_args()
    root, output = Path(args.run).resolve(), Path(args.output).resolve()
    repo = Path(__file__).resolve().parents[2]
    sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest().upper()
    read = lambda name: json.loads((root / name).read_text(encoding="utf-8-sig"))
    native = read("production-redaction/result.json")
    independent = read("production-redaction/independent-audit.json")
    visual = read("production-redaction/manual-visual-review.json")
    if not native["ExpectationsPassed"] or not independent["passed"] or not visual["passed"]:
        raise RuntimeError("Incomplete or failing production redaction evidence.")
    native_hash = sha(root / "production-redaction/result.json")
    if independent["nativeReportSHA256"] != native_hash or visual["nativeReportSHA256"] != native_hash:
        raise RuntimeError("Stale redaction report or visual review.")
    if independent["scriptSHA256"] != sha(Path(__file__).with_name("verify_production_redaction.py")):
        raise RuntimeError("Verifier source has changed since verification.")
    if len(independent.get("negativeControls", [])) < 6 or not all(x["passed"] for x in independent["negativeControls"]):
        raise RuntimeError("Missing or failing independent negative controls.")
    for image in visual["images"]:
        if sha(root / "production-redaction" / image["file"]) != image["sha256"]:
            raise RuntimeError("Stale visual image binding.")
    source_manifest = read("baseline/source-manifest.json")
    for item in source_manifest:
        if item["Path"].startswith(("src/NT.AvaloniaControl.PdfViewer/", "src/NT.AvaloniaControl.PdfViewer.Themes/")):
            if sha(repo / item["Path"]) != item["SHA256"]:
                raise RuntimeError("Product source has changed since freezing: " + item["Path"])
    execution = read("execution-manifest.json")
    for probe in ("save", "fonts", "forms", "redaction", "baseline", "production", "redaction-advanced", "production-redaction"):
        if read(probe + "/result.json")["ManagedAssemblySHA256"] != execution["AssemblySHA256"]:
            raise RuntimeError("Mixed probe executables: " + probe)
    product_hash = native["Result"]["FunctionalAssemblySHA256"]
    writer = read("production/result.json")["Result"]
    if writer["FunctionalAssemblySHA256"] != product_hash:
        raise RuntimeError("Writer and redaction probes loaded different product assemblies.")
    for consumer in read("package-consumer-results.json")["Consumers"]:
        product = next(x for x in consumer["Products"] if x["Package"] == "NT.AvaloniaControl.PdfViewer")
        with zipfile.ZipFile(product["RestoredArchive"]) as package:
            dll = package.read("lib/net10.0/NT.AvaloniaControl.PdfViewer.dll")
            if hashlib.sha256(dll).hexdigest().upper() != product_hash:
                raise RuntimeError("Package consumers and native probe loaded different product binaries.")
    subprocess.run([sys.executable, str(Path(__file__).with_name("export_writer_record.py")),
                    "--run", str(root), "--output", str(output), "--openssl", args.openssl, "--poppler", args.poppler], check=True)
    record = json.loads(output.read_text(encoding="utf-8"))
    record.update(schemaVersion=3,
                  phase="P0-C02 bounded production hidden-information sanitizer and whole-object redaction pipeline",
                  status="bounded production pipeline implemented and independently verified; full G0 remains open",
                  boundedSanitizationAndRedactionImplemented=True,
                  exporterSHA256=sha(Path(__file__)))
    parent = repo / "docs/validation/pdfviewer-p0-writer-20261015.json"
    record["parentRecord"] = {"path": str(parent.relative_to(repo)).replace("\\", "/"), "sha256": sha(parent), "runId": "20261015T083100Z-p0-writer"}
    record["advancedRedaction"]["scope"] = "Historical restricted native primitives and deliberate unsafe counterexamples; the new bounded public pipeline is recorded separately."
    record["fonts"]["freeTextBridge"] = "Historical Python experiment; the current bounded C# FreeText backend is recorded in productionWriter."
    record["redaction"]["scope"] += " This historical probe's unsafe candidates are not outputs of the public production pipeline."
    record["productionRedaction"] = {
        "result": native["Result"], "nativeReportSHA256": native_hash,
        "independentCheckCount": len(independent["checks"]), "independentCheckIds": [x["id"] for x in independent["checks"]],
        "negativeControls": independent["negativeControls"], "visualReview": visual,
        "policy": "Explicit RemoveAllNonPageData removes all annotations/forms/outline/attachments/actions and non-page data; not flattening.",
        "pipeline": ["strict page-painting preflight and hidden-surface cleanup", "geometry-selected whole-object removal", "content regeneration", "recursive unused-resource pruning", "fresh-catalog full rewrite", "structural audit", "native reopen/page count/empty-region audit", "single revision commit"],
        "fullOfficialParity": False, "arbitraryForensicErasure": False,
        "unsupported": ["Partial text/image/path/Form", "Affected-page rotation/transparency/custom clipping", "Optional content/marked-content properties", "Type3/patterns/shadings/non-device or non-indexed color spaces/default color overrides", "Opaque JPEG/JPX/JBIG2/unknown filters", "Non-default UserUnit", "Signed/encrypted editing", "Secure erasure of source/caller memory/retained font or pixel covert channels"]}
    record["openG0Items"] = ["Automatic font subsetting and broader font/shaping/layout formats", "External editor interaction and representative real-world document corpus", "Character/glyph-cluster and partial-image redaction, broader rendering-resource compatibility and bounded memory/performance", "Remaining AcroForm/CJK/IME matrix", "Ubuntu and Kylin physical validation; official runnable reference and UI performance comparison"]
    names = ["production-redaction/result.json", "production-redaction/independent-audit.json", "production-redaction/manual-visual-review.json"]
    names += [str(p.relative_to(root)).replace("\\", "/") for p in sorted((root / "production-redaction").glob("*.pdf"))]
    names += [str(p.relative_to(root)).replace("\\", "/") for p in sorted((root / "production-redaction").glob("*.png"))]
    record["evidence"].extend({"path": name, "sha256": sha(root / name), "bytes": (root / name).stat().st_size} for name in names)
    source = root / "production-redaction/input.pdf"
    record["corpus"].append({"path": "production-redaction/input.pdf", "bytes": source.stat().st_size, "sha256": sha(source), "source": "Self-authored two-page production API corpus", "scope": "Whole text/image/path/shared Form; hidden surfaces, unused resources and old revisions; not representative real-world scale"})
    output.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "productionRedactionNativeChecks": len(native["Result"]["Checks"]), "independentChecks": len(independent["checks"]), "negativeControls": len(independent["negativeControls"])}))


if __name__ == "__main__":
    main()
