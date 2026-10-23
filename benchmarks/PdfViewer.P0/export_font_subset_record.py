"""Export a hash-bound P0 automatic font subsetting and CJK compatibility record."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("run", "output", "openssl", "poppler"):
        parser.add_argument("--" + name, required=True)
    args = parser.parse_args()
    root, output = Path(args.run).resolve(), Path(args.output).resolve()
    repo = Path(__file__).resolve().parents[2]
    sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest().upper()
    read = lambda p: json.loads((root / p).read_text(encoding="utf-8-sig"))
    native, audit, visual = (read("font-subset/" + name) for name in ("result.json", "independent-audit.json", "manual-visual-review.json"))
    if not native["ExpectationsPassed"] or not audit["passed"] or not visual["passed"]:
        raise RuntimeError("Missing or failing font-subset evidence.")
    native_hash = sha(root / "font-subset/result.json")
    if audit["nativeReportSHA256"] != native_hash or visual["nativeReportSHA256"] != native_hash:
        raise RuntimeError("Stale font-subset audit or visual review.")
    if len(audit.get("negativeControls", [])) != 5 or not all(x["passed"] for x in audit["negativeControls"]):
        raise RuntimeError("Missing or failing font negative controls.")
    for field, file in (("scriptSHA256", "verify_font_subset.py"), ("inspectorScriptSHA256", "font_subset_inspector.py"), ("preparationScriptSHA256", "prepare_font_subset_corpus.py")):
        if audit[field] != sha(Path(__file__).with_name(file)):
            raise RuntimeError("Verifier/preparation source changed after execution: " + file)
    for image in visual["images"]:
        if sha(root / "font-subset" / image["file"]) != image["sha256"]:
            raise RuntimeError("Stale font visual review image.")
    if native["ManagedAssemblySHA256"] != read("execution-manifest.json")["AssemblySHA256"]:
        raise RuntimeError("Font probe used a different executable.")
    if native["Result"]["FunctionalAssemblySHA256"] != read("production-redaction/result.json")["Result"]["FunctionalAssemblySHA256"]:
        raise RuntimeError("Font and deletion probes used different product binaries.")
    subprocess.run([sys.executable, str(Path(__file__).with_name("export_redaction_record.py")), "--run", str(root), "--output", str(output), "--openssl", args.openssl, "--poppler", args.poppler], check=True)
    record = json.loads(output.read_text(encoding="utf-8"))
    record.update(schemaVersion=4, phase="P0-C01 managed static TrueType automatic subsetting and CJK compatibility",
                  status="bounded automatic subsetting and CJK corpus independently verified; full G0 remains open",
                  automaticTrueTypeSubsettingImplemented=True, exporterSHA256=sha(Path(__file__)))
    parent = repo / "docs/validation/pdfviewer-p0-redaction-20261015.json"
    record["parentRecord"] = {"path": str(parent.relative_to(repo)).replace("\\", "/"), "sha256": sha(parent), "runId": "20261015T091230Z-p0-redaction"}
    record["fontSubsetting"] = {"result": native["Result"], "nativeReportSHA256": native_hash,
        "corpus": read("font-subset/corpus.json"), "officialFontSources": read("font-subset/sources/source-manifest.json"),
        "independentCheckCount": len(audit["checks"]), "independentCheckIds": [x["id"] for x in audit["checks"]],
        "fonts": audit["fonts"], "fontTools": audit["fontTools"], "negativeControls": audit["negativeControls"], "visualReview": visual,
        "mode": "AutomaticSubset default; explicit Full; NoSubsetting preserves exact complete font program",
        "textMapping": "Unicode scalar -> distinct CID -> original/prepared GID; explicit UTF16BE ToUnicode, supplementary and glyph aliases preserved",
        "limits": "32 MiB static glyf TrueType, horizontal precomposed single line; no fallback/shaping/CFF/TTC/variable/vertical/variation-sequence or external-editor/Linux acceptance",
        "performanceScope": "Single-operation elapsed/allocation observations, not peak memory, percentiles, official parity or arbitrary-font compatibility"}
    record["openG0Items"] = ["Broader font formats/layout, external editor interaction and Linux/Kylin physical verification", "Remaining AcroForm/CJK/IME matrix", "Character/glyph-cluster and partial-image redaction, broader PDF rendering compatibility", "Real-world corpus, UI/per-operation peak memory/performance and runnable official comparison"]
    paths = [root / "font-subset/result.json", root / "font-subset/independent-audit.json", root / "font-subset/manual-visual-review.json", root / "font-subset/corpus.json", root / "font-subset/extracted-fonts/fonttools-analysis.json"]
    for pattern in ("*.pdf", "*.png", "sources/*"):
        paths.extend(p for p in sorted((root / "font-subset").glob(pattern)) if p.is_file())
    if (root / "font-research").is_dir():
        paths.extend(p for p in sorted((root / "font-research").iterdir()) if p.is_file())
    record["evidence"].extend({"path": str(p.relative_to(root)).replace("\\", "/"), "sha256": sha(p), "bytes": p.stat().st_size} for p in paths)
    output.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "cases": len(native["Result"]["Cases"]), "nativeChecks": len(native["Result"]["Checks"]), "independentChecks": len(audit["checks"]), "negativeControls": len(audit["negativeControls"])}))


if __name__ == "__main__":
    main()
