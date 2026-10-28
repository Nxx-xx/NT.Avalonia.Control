"""Archive original C7 CI evidence without launching a benchmark or changing gates.

Called after the supervised original campaign stops, including failure/timeout.
Native files are producer assets; no unrecorded process module mapping is inferred.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import sys

PROTOCOL_PINS = {
    "scripts/charts-c7-performance-protocol.json": "7E633452EA83F4A9342C68093FE597DFB0CF4B18491D21660DE70EBE73EC5463",
    "scripts/charts_c7_performance_oracle.py": "888F4A287BD71FB5BE6035E974B9902618F321CE9666544B1B72FB0F99175E41",
    "scripts/Tests/test_charts_c7_performance_oracle.py": "9119056D3EC61A03A48F6F8804B2DDBACE93B48F691B5898362CA17B6E3C374D",
    "docs/charts-performance-budgets.json": "3E7149E6C4538993CBFEBAD2542E154DC8EBEBD06A72259EF345B3E3A07DEEA2",
    "scripts/charts-ci-performance-policy.json": "CF6832A53E21E078C554F51194D93541F0B093AFF1C67D7E031EC177B8E87377",
}
TRACKS = {"S10-L-exact", "S10-S-exact", "S10-B-exact", "S100-L-minmax"}
ARCHIVE_NAMES = {"Charts.NativeBenchmarks.dll", "Charts.NativeBenchmarks.runtimeconfig.json", "Charts.NativeBenchmarks.deps.json", "NT.AvaloniaControl.Charts.dll", "NT.AvaloniaControl.Charts.Skia.dll", "NT.AvaloniaControl.Charts.Themes.dll", "Avalonia.Base.dll"}
PRODUCT_INPUTS = ("src/NT.AvaloniaControl.Charts.Skia/ChartNativeCircleOperation.cs", "tests/Charts.Headless.Tests/NativeCircleOperationTests.cs", "benchmarks/Charts.NativeBenchmarks/Program.cs", "benchmarks/Charts.NativeBenchmarks/C7PerformanceProgram.cs")


def need(ok, message):
    if not ok:
        raise ValueError(message)


def load(path):
    def pairs(items):
        result = {}
        for key, value in items:
            need(key not in result, "Duplicate JSON key: " + key)
            result[key] = value
        return result
    path = Path(path)
    need(path.stat().st_size <= 64 * 1024 * 1024, "Oversized terminal receipt")
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=pairs,
                      parse_constant=lambda x: (_ for _ in ()).throw(ValueError("Nonfinite JSON: " + x)))


def fingerprint(path):
    path = Path(path)
    before = path.stat()
    with path.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest().upper()
    after = path.stat()
    need((before.st_size, before.st_mtime_ns) == (after.st_size, after.st_mtime_ns), "File changed during fingerprint: " + str(path))
    return {"path": str(path.resolve()), "bytes": before.st_size, "sha256": digest}


def member(root, relative):
    need(isinstance(relative, str) and relative and "\0" not in relative, "Invalid member")
    path = (Path(root) / relative.replace("\\", "/")).resolve()
    need(path.is_relative_to(Path(root).resolve()), "Evidence member escapes root")
    return path


def outcomes(campaign, oracle, execution, exit_code):
    """Completeness and workload success are separate, retaining original flags."""
    runs = campaign.get("runs", [])
    runs = runs if isinstance(runs, list) and all(isinstance(r, dict) for r in runs) else []
    population = {
        "interactionFormal": sum(r.get("kind") == "interaction" and r.get("role") == "formal" for r in runs),
        "firstUseFormal": sum(r.get("kind") == "first-use" and r.get("role") == "formal" for r in runs),
        "firstUseCalibration": sum(r.get("kind") == "first-use" and r.get("role") == "calibration" for r in runs),
    }
    tracks = oracle.get("tracks", {})
    tracks = tracks if isinstance(tracks, dict) and all(isinstance(t, dict) for t in tracks.values()) else {}
    first = oracle.get("firstUse", {})
    first = first if isinstance(first, dict) else {}
    complete = (campaign.get("schemaVersion") == "charts-c7-performance-campaign/v1"
                and oracle.get("schemaVersion") == "charts-c7-performance-oracle/v1"
                and execution.get("schema") == "charts-c703-execution/v1"
                and campaign.get("platform") == oracle.get("platform") == execution.get("platform")
                and campaign.get("platform") in ("windows", "linux")
                and campaign.get("phase") == oracle.get("phase") == execution.get("phase") == "formal"
                and campaign.get("protocolSha256") == oracle.get("protocol", {}).get("sha256") == PROTOCOL_PINS["scripts/charts-c7-performance-protocol.json"]
                and oracle.get("oracleTool", {}).get("sha256") == PROTOCOL_PINS["scripts/charts_c7_performance_oracle.py"] and len(runs) == 107 and population == {"interactionFormal": 5, "firstUseFormal": 100, "firstUseCalibration": 2}
                and oracle.get("runsExpected") == oracle.get("runsValidated") == 107
                and oracle.get("campaignEvidenceComplete") is True and oracle.get("evidenceIntegrityPassed") is True
                and len({r.get("raw") for r in runs}) == len({r.get("receipt") for r in runs}) == 107
                and set(tracks) == TRACKS and all(t.get("independentProcesses") == 5 and isinstance(t.get("processes"), list) and len(t["processes"]) == 5 for t in tracks.values())
                and first.get("independentFormalProcesses") == 100 and first.get("calibrationsExcluded") == 2)
    workloads = oracle.get("workloadSchedulesPassed") is True
    hits = oracle.get("absoluteHitGatesPassed") is True
    gate = complete and workloads and hits and oracle.get("passed") is True and execution.get("passed") is True and exit_code == 0
    return {"population": population, "plannedProcesses": len(runs), "formalEvidenceComplete": complete,
            "originalEvidenceIntegrityPassed": oracle.get("evidenceIntegrityPassed"),
            "workloadSchedulesPassed": oracle.get("workloadSchedulesPassed"), "absoluteHitGatesPassed": oracle.get("absoluteHitGatesPassed"),
            "formalMeasurementProtocolCompleted": oracle.get("formalMeasurementProtocolCompleted"),
            "originalOraclePassed": oracle.get("passed"), "originalExecutionPassed": execution.get("passed"),
            "originalGatePassed": gate, "failedNativeProcesses": execution.get("failedNativeProcesses", []),
            "oracleFailures": oracle.get("failures", []), "originalExecutionError": execution.get("error"),
            "campaignExitCode": exit_code}


def raw_header(path, limit=1024 * 1024):
    """Bounded read of the original producer's stable pre-scenes JSON header."""
    with Path(path).open("rb") as stream:
        prefix = stream.read(limit)
    text = prefix.decode("utf-8-sig", errors="replace") # Arbitrary suffix may end inside a UTF8 scalar; parsed header stays strict.
    match = re.search(r'(?m)^  "scenes"\s*:', text)
    need(match is not None, "Raw header missing scenes boundary or exceeds 1MiB")
    head = text[:match.start()].rstrip()
    need(head.endswith(","), "Raw header boundary differs")
    return json.loads(head[:-1] + "\n}")


def native_assets(repo, output, platform, header):
    """Copy actual producer files, never treat asset presence as a load map."""
    binary = Path(repo).resolve() / "benchmarks/Charts.NativeBenchmarks/bin/Release/net10.0"
    rid = "win-x64" if platform == "windows" else "linux-x64"
    extension = ".dll" if platform == "windows" else ".so"
    relatives = ["SkiaSharp.dll", "Avalonia.Skia.dll", "HarfBuzzSharp.dll"] + [f"runtimes/{rid}/native/{name}{extension}" for name in ("libSkiaSharp", "libHarfBuzzSharp")]
    assets, missing = [], []
    for relative in relatives:
        source = member(binary, relative)
        if not source.is_file():
            missing.append(relative)
            continue
        target = member(output, "native-assets/" + relative)
        source_proof = fingerprint(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            with source.open("rb") as src, target.open("xb") as dst:
                shutil.copyfileobj(src, dst, 1024 * 1024)
        archive_proof = fingerprint(target)
        need((archive_proof["bytes"], archive_proof["sha256"]) == (source_proof["bytes"], source_proof["sha256"]), "Native asset archive differs; old evidence retained")
        assets.append({"relativePath": relative, "source": source_proof, "archive": archive_proof,
                       "scope": "producer-runtime-asset-file-not-process-loaded-module-mapping"})
    reported = []
    for field in ("nativeTextBeforeUi", "nativeTextAfterRealText"):
        value = header.get(field, {}) or {}
        value = value if isinstance(value, dict) else {}
        if value.get("assetPath"):
            matching = [a for a in assets if a["source"]["sha256"] == str(value.get("assetSha256", "")).upper()]
            reported.append({"producerField": field, "declaredPath": value["assetPath"], "declaredSha256": value.get("assetSha256"),
                             "matchedArchivedAssetByReportedSha": bool(matching), "resolverObserved": value.get("resolverObserved"),
                             "qualification": "Original native-text resolver report matched to archived bytes where available; no arbitrary declared external path is opened and no process module map is inferred."})
    return {"assets": assets, "missing": missing, "allRequiredProducerAssetFilesArchived": not missing,
            "reportedNativeTextAssets": reported, "nativeLoadedModuleMappingMeasured": False,
            "scope": "Skia/HarfBuzz actual producer asset bytes and original native-text reports. Presence or SHA of an asset is not evidence of a measured per-process native module mapping, GPU execution or peak memory."}


def inventory(root):
    files, excluded = [], []
    for directory, dirs, names in os.walk(root, followlinks=False):
        for name in dirs:
            if Path(directory, name).is_symlink():
                excluded.append({"path": str(Path(directory, name)), "reason": "directory symlink is not followed"})
        dirs[:] = sorted(d for d in dirs if not Path(directory, d).is_symlink())
        for name in sorted(names):
            path = Path(directory, name)
            if path.name in {"ci-evidence.json", "wrapper-result.json"}:
                continue  # This manifest and the enclosing terminal receipt cannot hash themselves.
            if path.is_symlink():
                excluded.append({"path": str(path), "reason": "symlink is not followed"})
                continue
            proof = fingerprint(path)
            proof["relativePath"] = path.relative_to(root).as_posix()
            files.append(proof)
    return {"files": files, "excluded": excluded, "selfExcluded": ["ci-evidence.json", "wrapper-result.json"],
            "scope": "Full existing run tree including failed/partial receipts, raw, JSONL, datasets, PNGs, manifests, logs, private cache/font configuration and native asset copies. No deletion or success-only filtering."}


def required_artifacts(campaign_root, campaign):
    """Check physical retention without repeating the original numeric oracle."""
    expected = {campaign_root / n for n in ("campaign.json", "oracle.json", "results.json", "source-before.json", "source-after.json", "measured-assemblies.json")}
    for row in campaign.get("runs", []):
        raw = member(campaign_root, row["raw"])
        expected.update((raw, member(campaign_root, row["receipt"]), raw.parent / "requests.jsonl"))
        tracks = TRACKS if row.get("kind") == "interaction" else {"S1M-L-minmax"}
        for track in tracks:
            expected.add(raw.parent / "datasets" / (track + "-series-0.xy64le"))
            if track == "S10-B-exact":
                expected.add(raw.parent / "datasets" / (track + "-series-1.xy64le"))
            expected.add(raw.parent / "png" / (track + "-first.png"))
            if row.get("kind") == "interaction":
                expected.add(raw.parent / "png" / (track + "-last.png"))
    archive = campaign_root / "measured-assemblies.json"
    if archive.is_file():
        rows = load(archive)
        need(isinstance(rows, list) and len(rows) == 7 and {r["name"] for r in rows} == ARCHIVE_NAMES, "Expected original seven-file assembly archive")
        for row in rows:
            path = member(campaign_root, row["path"])
            expected.add(path)
            if path.is_file():
                proof = fingerprint(path)
                need((proof["bytes"], proof["sha256"]) == (row["bytes"], row["sha256"]), "Archived producer identity mismatch")
    before, after = campaign_root / "source-before.json", campaign_root / "source-after.json"
    if before.is_file() and after.is_file():
        need(load(before) == load(after), "Original source-before/source-after differ")
    missing = [p.relative_to(campaign_root).as_posix() for p in sorted(expected) if not p.is_file()]
    return {"expectedFiles": len(expected), "allPresent": not missing, "missing": missing,
            "scope": "Original planned raw/receipts/JSONL/corpus/PNG and archive presence. Original oracle owns their numeric/source/pixel validation; this helper fingerprints the actual retained files."}


def collect(repo, root, native_mode, exit_code):
    repo, root = Path(repo).resolve(), Path(root).resolve()
    need(root.is_dir(), "The wrapper evidence directory is missing")
    need(not (root / "ci-evidence.json").exists(), "Existing evidence manifest is never overwritten")
    errors, pins, snapshots = [], [], []
    for relative, expected in PROTOCOL_PINS.items():
        try:
            proof = fingerprint(repo / relative)
            pins.append({"relativePath": relative, "file": proof, "expectedSha256": expected, "matches": proof["sha256"] == expected})
            if proof["sha256"] != expected:
                errors.append("Original protocol/oracle/budget/policy pin changed: " + relative)
        except (OSError, ValueError) as error:
            errors.append(str(error))
    for relative in PRODUCT_INPUTS:
        try:
            snapshots.append({"relativePath": relative, "file": fingerprint(repo / relative)})
        except (OSError, ValueError) as error:
            errors.append(str(error))
    original, terminal_files = {}, []
    for name in ("campaign", "oracle", "results"):
        path = root / "campaign" / (name + ".json")
        try:
            value = load(path)
            need(isinstance(value, dict), "Terminal receipt is not an object")
            original[name] = value
            terminal_files.append(fingerprint(path))
        except (OSError, ValueError) as error:
            original[name] = {}
            errors.append(f"{name}: {error}")
    result = {"schema": "charts-c7-hosted-ci-evidence/v1", "nativeMode": native_mode,
              "officialReference": {"status": "blocked-reference", "measurements": None, "ratios": None},
              "present": None, "peakMemory": None, "physicalHardwareQualified": False, "milestoneComplete": False,
              "originalProtocolPins": pins, "productAndHostSourceSnapshots": snapshots, "originalTerminalFiles": terminal_files,
              "outcome": outcomes(original["campaign"], original["oracle"], original["results"], exit_code),
              "scope": "Manual hosted software/X11 Xvfb self-developed CI gate. Original protocol, thresholds, counts, producer and oracle are reused. No official license/reference, physical desktop/GPU/touch, compositor present or peak-memory qualification."}
    try:
        result["requiredArtifacts"] = required_artifacts(root / "campaign", original["campaign"])
        if not result["requiredArtifacts"]["allPresent"]:
            errors.append("Original planned evidence files are missing; see requiredArtifacts")
    except (OSError, ValueError, KeyError, TypeError) as error:
        errors.append("required-artifacts: " + str(error))
    header = {}
    for row in original["campaign"].get("runs", []):
        try:
            header = raw_header(member(root / "campaign", row["raw"]))
            result["representativeNativeHeaderRaw"] = row["raw"]
            break
        except (OSError, ValueError, KeyError):
            continue
    try:
        result["nativeAssets"] = native_assets(repo, root, "windows" if native_mode == "HostedWindowsSoftware" else "linux", header)
        if not result["nativeAssets"]["allRequiredProducerAssetFilesArchived"]:
            errors.append("Producer native/managed runtime assets are incomplete; inspect missing paths")
    except (OSError, ValueError) as error:
        result["nativeAssets"] = {"error": str(error), "nativeLoadedModuleMappingMeasured": False}
        errors.append("native-assets: " + str(error))
    try:
        result["artifactInventory"] = inventory(root)
    except (OSError, ValueError) as error:
        errors.append("inventory: " + str(error))
    result["errors"] = errors
    result["artifactIntegrityPassed"] = not errors
    result["passed"] = not errors and result["outcome"]["originalGatePassed"]
    with (root / "ci-evidence.json").open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2, allow_nan=False)
        stream.write("\n")
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--native-mode", choices=("HostedWindowsSoftware", "XvfbSoftware"), required=True)
    parser.add_argument("--campaign-exit-code", type=int, required=True)
    args = parser.parse_args(argv)
    result = collect(args.repo, args.root, args.native_mode, args.campaign_exit_code)
    print(json.dumps({"passed": result["passed"], "formalEvidenceComplete": result["outcome"]["formalEvidenceComplete"],
                      "workloadSchedulesPassed": result["outcome"]["workloadSchedulesPassed"], "errors": result["errors"],
                      "evidence": str(args.root / "ci-evidence.json")}))
    return 0 if result["passed"] else 1 if result["artifactIntegrityPassed"] else 2


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(2)
