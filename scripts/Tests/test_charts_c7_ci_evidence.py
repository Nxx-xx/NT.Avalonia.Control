"""Synthetic CI adapter/evidence contracts only; never starts .NET or native UI."""
from pathlib import Path
import copy
import importlib.util
import json
import os
import re
import tempfile
import unittest
from unittest import mock

SCRIPT = Path(__file__).resolve().parents[1] / "charts_c7_ci_evidence.py"
spec = importlib.util.spec_from_file_location("charts_c7_ci_evidence", SCRIPT)
evidence = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evidence)
REPO = SCRIPT.parent.parent


def temporary_workspace():
    base = Path(os.environ.get("CHARTS_TEST_TEMP_ROOT", str(REPO / "artifacts/charts-c7-ci-contract-tests"))).resolve()
    base.mkdir(parents=True, exist_ok=True)
    temporary = tempfile.TemporaryDirectory(prefix="ci-fixture-", dir=base)
    if not Path(temporary.name).resolve().is_relative_to(base):
        raise RuntimeError("Synthetic cleanup target escaped the selected test root")
    return temporary


def original_receipts(platform="linux"):
    runs = []
    for i in range(5):
        runs.append(dict(kind="interaction", role="formal", raw=f"runs/interaction-{i}/raw.json", receipt=f"processes/interaction-{i}/process.json"))
    for role, count in (("calibration", 2), ("formal", 100)):
        for i in range(count):
            runs.append(dict(kind="first-use", role=role, raw=f"runs/first-{role}-{i}/raw.json", receipt=f"processes/first-{role}-{i}/process.json"))
    campaign = dict(schemaVersion="charts-c7-performance-campaign/v1", platform=platform, phase="formal", runs=runs,
                    protocolSha256=evidence.PROTOCOL_PINS["scripts/charts-c7-performance-protocol.json"])
    oracle = dict(schemaVersion="charts-c7-performance-oracle/v1", platform=platform, phase="formal", runsExpected=107, runsValidated=107,
                  campaignEvidenceComplete=True, evidenceIntegrityPassed=True, workloadSchedulesPassed=True, absoluteHitGatesPassed=True,
                  passed=True, formalMeasurementProtocolCompleted=True, failures=[],
                  protocol={"sha256": evidence.PROTOCOL_PINS["scripts/charts-c7-performance-protocol.json"]},
                  oracleTool={"sha256": evidence.PROTOCOL_PINS["scripts/charts_c7_performance_oracle.py"]},
                  tracks={name: dict(independentProcesses=5, processes=[{} for _ in range(5)]) for name in evidence.TRACKS},
                  firstUse=dict(independentFormalProcesses=100, calibrationsExcluded=2))
    execution = dict(schema="charts-c703-execution/v1", platform=platform, phase="formal", passed=True, failedNativeProcesses=[], error=None)
    return campaign, oracle, execution


def put(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2), encoding="utf-8")


def synthetic_tree(home, platform="linux", fail_workload=False, full=False):
    repo, root = home / "repo", home / "output"
    root.mkdir(); pins = {}
    for rel in evidence.PROTOCOL_PINS:
        path = repo / rel
        path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(rel.encode())
        pins[rel] = evidence.fingerprint(path)["sha256"]
    for rel in evidence.PRODUCT_INPUTS:
        path = repo / rel; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(b"synthetic product snapshot")
    with mock.patch.dict(evidence.PROTOCOL_PINS, pins, clear=True):
        campaign, oracle, execution = original_receipts(platform)
    if not full:
        campaign["runs"] = campaign["runs"][:1]
    if fail_workload:
        oracle.update(passed=False, workloadSchedulesPassed=False, formalMeasurementProtocolCompleted=False)
        execution.update(passed=False, failedNativeProcesses=[dict(id="interaction-0", exitCode=1, timedOut=False, terminalRawPresent=True)], error="fixed workload pending")
    base = root / "campaign"
    put(base / "campaign.json", campaign); put(base / "oracle.json", oracle); put(base / "results.json", execution)
    put(base / "source-before.json", []); put(base / "source-after.json", [])
    archives = []
    for name in sorted(evidence.ARCHIVE_NAMES):
        path = base / "measured-assemblies" / name; path.parent.mkdir(exist_ok=True); path.write_bytes(name.encode())
        fp = evidence.fingerprint(path); archives.append(dict(name=name, path="measured-assemblies/" + name, bytes=fp["bytes"], sha256=fp["sha256"]))
    put(base / "measured-assemblies.json", archives)
    for row in campaign["runs"]:
        raw = evidence.member(base, row["raw"]); put(raw, dict(schema="charts-c7-performance/v1", nativeTextBeforeUi={}, nativeTextAfterRealText={}, scenes=[]))
        put(evidence.member(base, row["receipt"]), {}); (raw.parent / "requests.jsonl").write_text("{}\n")
        tracks = evidence.TRACKS if row["kind"] == "interaction" else {"S1M-L-minmax"}
        for track in tracks:
            for i in range(2 if track == "S10-B-exact" else 1):
                path = raw.parent / "datasets" / f"{track}-series-{i}.xy64le"; path.parent.mkdir(exist_ok=True); path.write_bytes(b"synthetic dataset")
            for suffix in ("first", "last") if row["kind"] == "interaction" else ("first",):
                path = raw.parent / "png" / f"{track}-{suffix}.png"; path.parent.mkdir(exist_ok=True); path.write_bytes(b"synthetic PNG presence only")
    binary = repo / "benchmarks/Charts.NativeBenchmarks/bin/Release/net10.0"
    rid, ext = ("win-x64", ".dll") if platform == "windows" else ("linux-x64", ".so")
    for rel in ["SkiaSharp.dll", "Avalonia.Skia.dll", "HarfBuzzSharp.dll"] + [f"runtimes/{rid}/native/{n}{ext}" for n in ("libSkiaSharp", "libHarfBuzzSharp")]:
        path = binary / rel; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(rel.encode())
    return repo, root, pins


class OutcomeContracts(unittest.TestCase):
    def test_original_107_complete_gate(self):
        self.assertTrue(evidence.outcomes(*original_receipts(), 0)["originalGatePassed"])

    def test_workload_failure_is_complete_evidence_not_pass(self):
        campaign, oracle, execution = original_receipts()
        oracle.update(workloadSchedulesPassed=False, passed=False, formalMeasurementProtocolCompleted=False)
        execution.update(passed=False, failedNativeProcesses=[dict(id="interaction-1", exitCode=1)])
        result = evidence.outcomes(campaign, oracle, execution, 1)
        self.assertTrue(result["formalEvidenceComplete"]); self.assertFalse(result["originalGatePassed"])
        self.assertFalse(result["formalMeasurementProtocolCompleted"]); self.assertEqual(len(result["failedNativeProcesses"]), 1)

    def test_missing_process_cannot_qualify(self):
        campaign, oracle, execution = original_receipts(); campaign["runs"].pop()
        self.assertFalse(evidence.outcomes(campaign, oracle, execution, 0)["formalEvidenceComplete"])

    def test_calibrations_never_relabelled_formal(self):
        campaign, oracle, execution = original_receipts(); campaign["runs"][5]["role"] = "formal"
        self.assertFalse(evidence.outcomes(campaign, oracle, execution, 0)["formalEvidenceComplete"])

    def test_repeated_raw_rejected(self):
        campaign, oracle, execution = original_receipts(); campaign["runs"][-1]["raw"] = campaign["runs"][-2]["raw"]
        self.assertFalse(evidence.outcomes(campaign, oracle, execution, 0)["formalEvidenceComplete"])

    def test_hit_failure_stays_failure(self):
        campaign, oracle, execution = original_receipts(); oracle["absoluteHitGatesPassed"] = False
        self.assertFalse(evidence.outcomes(campaign, oracle, execution, 0)["originalGatePassed"])

    def test_oracle_integrity_failure_not_complete(self):
        campaign, oracle, execution = original_receipts(); oracle["evidenceIntegrityPassed"] = False
        self.assertFalse(evidence.outcomes(campaign, oracle, execution, 0)["formalEvidenceComplete"])

    def test_shortened_track_population_rejected(self):
        campaign, oracle, execution = original_receipts(); oracle["tracks"]["S10-S-exact"]["processes"].pop()
        self.assertFalse(evidence.outcomes(campaign, oracle, execution, 0)["formalEvidenceComplete"])

    def test_changed_oracle_pin_not_complete(self):
        campaign, oracle, execution = original_receipts(); oracle["oracleTool"]["sha256"] = "0" * 64
        self.assertFalse(evidence.outcomes(campaign, oracle, execution, 0)["formalEvidenceComplete"])

    def test_wrong_receipt_schema_rejected(self):
        campaign, oracle, execution = original_receipts(); execution["schema"] = "development"
        self.assertFalse(evidence.outcomes(campaign, oracle, execution, 0)["formalEvidenceComplete"])

    def test_nonzero_child_never_becomes_pass(self):
        self.assertFalse(evidence.outcomes(*original_receipts(), 1)["originalGatePassed"])


class ArtifactContracts(unittest.TestCase):
    def test_escape_and_json_duplicates_rejected(self):
        with temporary_workspace() as temp:
            root = Path(temp)
            with self.assertRaises(ValueError): evidence.member(root, "../outside")
            put(root / "dup.json", {})
            (root / "dup.json").write_text('{"x":1,"x":2}')
            with self.assertRaises(ValueError): evidence.load(root / "dup.json")

    def test_full_synthetic_collection_preserves_files_and_boundaries(self):
        with temporary_workspace() as temp:
            repo, root, pins = synthetic_tree(Path(temp), full=True)
            raw = root / "campaign/runs/interaction-0/raw.json"; before = raw.read_bytes()
            with mock.patch.dict(evidence.PROTOCOL_PINS, pins, clear=True): result = evidence.collect(repo, root, "XvfbSoftware", 0)
            self.assertTrue(result["passed"]); self.assertEqual(raw.read_bytes(), before)
            self.assertEqual(len(result["nativeAssets"]["assets"]), 5)
            self.assertFalse(result["nativeAssets"]["nativeLoadedModuleMappingMeasured"])
            self.assertIsNone(result["officialReference"]["ratios"]); self.assertIsNone(result["present"]); self.assertIsNone(result["peakMemory"])
            self.assertFalse(result["physicalHardwareQualified"])
            with self.assertRaises(ValueError): evidence.collect(repo, root, "XvfbSoftware", 0)

    def test_failed_campaign_still_gets_complete_inventory(self):
        with temporary_workspace() as temp:
            repo, root, pins = synthetic_tree(Path(temp), fail_workload=True, full=True)
            with mock.patch.dict(evidence.PROTOCOL_PINS, pins, clear=True): result = evidence.collect(repo, root, "XvfbSoftware", 1)
            self.assertFalse(result["passed"]); self.assertTrue(result["outcome"]["formalEvidenceComplete"]); self.assertTrue(result["artifactIntegrityPassed"])
            self.assertTrue(result["artifactInventory"]["files"]); self.assertEqual(len(result["outcome"]["failedNativeProcesses"]), 1)

    def test_missing_requests_or_png_fail_retention_check(self):
        with temporary_workspace() as temp:
            repo, root, pins = synthetic_tree(Path(temp))
            (root / "campaign/runs/interaction-0/requests.jsonl").unlink()
            result = evidence.required_artifacts(root / "campaign", evidence.load(root / "campaign/campaign.json"))
            self.assertFalse(result["allPresent"]); self.assertIn("runs/interaction-0/requests.jsonl", result["missing"])

    def test_changed_archive_rejected(self):
        with temporary_workspace() as temp:
            repo, root, pins = synthetic_tree(Path(temp))
            (root / "campaign/measured-assemblies/Charts.NativeBenchmarks.dll").write_bytes(b"changed")
            with self.assertRaises(ValueError): evidence.required_artifacts(root / "campaign", evidence.load(root / "campaign/campaign.json"))

    def test_native_archive_does_not_replace_different_old_evidence(self):
        with temporary_workspace() as temp:
            repo, root, pins = synthetic_tree(Path(temp)); target = root / "native-assets/SkiaSharp.dll"; target.parent.mkdir(); target.write_bytes(b"old failure proof")
            with self.assertRaises(ValueError): evidence.native_assets(repo, root, "linux", {})
            self.assertEqual(target.read_bytes(), b"old failure proof")

    def test_native_text_path_is_not_opened_outside_producer(self):
        with temporary_workspace() as temp:
            repo, root, pins = synthetic_tree(Path(temp))
            result = evidence.native_assets(repo, root, "linux", {"nativeTextAfterRealText": {"assetPath": "/secret/not-read", "assetSha256": "0" * 64}})
            self.assertFalse(result["reportedNativeTextAssets"][0]["matchedArchivedAssetByReportedSha"])

    def test_windows_asset_layout_is_archived_without_load_claim(self):
        with temporary_workspace() as temp:
            repo, root, pins = synthetic_tree(Path(temp), platform="windows")
            result = evidence.native_assets(repo, root, "windows", {})
            self.assertTrue(result["allRequiredProducerAssetFilesArchived"])
            self.assertEqual(len(result["assets"]), 5)
            self.assertFalse(result["nativeLoadedModuleMappingMeasured"])

    def test_missing_terminal_json_is_retained_as_failure(self):
        with temporary_workspace() as temp:
            repo, root, pins = synthetic_tree(Path(temp))
            (root / "campaign/results.json").unlink()
            with mock.patch.dict(evidence.PROTOCOL_PINS, pins, clear=True): result = evidence.collect(repo, root, "XvfbSoftware", 1)
            self.assertFalse(result["passed"])
            self.assertTrue(result["errors"])
            self.assertTrue((root / "ci-evidence.json").is_file())

    def test_protocol_drift_never_becomes_pass(self):
        with temporary_workspace() as temp:
            repo, root, pins = synthetic_tree(Path(temp))
            (repo / "scripts/charts-c7-performance-protocol.json").write_bytes(b"changed threshold")
            with mock.patch.dict(evidence.PROTOCOL_PINS, pins, clear=True): result = evidence.collect(repo, root, "XvfbSoftware", 0)
            self.assertFalse(result["artifactIntegrityPassed"])
            self.assertTrue(any("pin changed" in error for error in result["errors"]))

    def test_raw_header_bounded_parser(self):
        with temporary_workspace() as temp:
            path = Path(temp) / "raw.json"; put(path, {"schema": "original", "nativeTextAfterRealText": {"assetPath": "a"}, "scenes": []})
            self.assertEqual(evidence.raw_header(path)["nativeTextAfterRealText"]["assetPath"], "a")
            with self.assertRaises(ValueError): evidence.raw_header(path, 8)


class SourceContracts(unittest.TestCase):
    def test_manual_only_workflow_and_failure_upload(self):
        workflow = (REPO / ".github/workflows/charts-c7.yml").read_text()
        self.assertIn("c7-contracts:", workflow); self.assertIn("needs: c7-contracts", workflow)
        self.assertIn("test_charts_memory_sampling.py", workflow); self.assertIn("Required C7 tool/contract is missing", workflow)
        self.assertIn("contracts/source-check.json", workflow)
        self.assertLess(workflow.index("contracts/source-check.json"), workflow.index("Required C7 tool/contract is missing"))
        self.assertIn("workflow_dispatch:", workflow); self.assertIn("default: false", workflow)
        self.assertNotRegex(workflow, r"(?m)^\s*(?:push|pull_request|schedule):")
        self.assertIn("inputs.run_formal", workflow); self.assertIn("fail-fast: false", workflow)
        self.assertIn("cancel-in-progress: false", workflow); self.assertIn("if: always()", workflow)
        self.assertIn("path: artifacts/charts-c7-ci/**", workflow); self.assertNotIn("continue-on-error", workflow)
        self.assertNotIn("secrets.", workflow); self.assertIn("windows-2025", workflow); self.assertIn("ubuntu-24.04", workflow)
        self.assertIn("xvfb-run", workflow); self.assertIn("fonts-dejavu-core", workflow)

    def test_wrapper_only_delegates_to_original_formal_campaign(self):
        wrapper = (REPO / "scripts/Invoke-ChartsC7Ci.ps1").read_text()
        self.assertIn("Invoke-ChartsC7Performance.ps1", wrapper); self.assertIn("'-Phase','formal'", wrapper)
        self.assertNotIn("-FirstUseOnly", wrapper); self.assertNotIn("--c7-measure-seconds", wrapper)
        self.assertIn("finally", wrapper); self.assertIn("ci-evidence.json", wrapper)
        self.assertIn("if (-not $ciPassed)", wrapper)


if __name__ == "__main__":
    unittest.main()
