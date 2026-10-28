"""Synthetic memory sampler/oracle contracts only; no .NET or live process probe."""
from __future__ import annotations

import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import charts_memory_sampler as sampler
import charts_memory_oracle as oracle


def identity(mode="full"):
    return dict(runToken="0123456789abcdef0123456789abcdef", processId=1234, processStartedUtc="2026-10-27T00:00:00.1234567Z", mode=mode,
                windowsCreationFileTimeUtc=None, linuxProcStartTicks=987654321, linuxBootId="12345678-1234-1234-1234-123456789abc")


def rows():
    expected = identity()
    output = []
    # Deliberately above binary64's exact integer range.
    origin = 1_000_000_000_000_000_001
    for index in range(2):
        start = origin + index * 20_000_001
        output.append({**expected, "kind": "process", "index": index, "reason": "before-source-generation" if index == 0 else "terminal-hold",
                       "scheduledNs": start - 5, "readStartNs": start, "readEndNs": start + 100,
                       "gapNs": None if index == 0 else 20_000_001, "deadlineLatenessNs": 5, "readStartQpc": None, "readEndQpc": None,
                       "observedIdentity": {key: expected[key] for key in ("windowsCreationFileTimeUtc", "linuxProcStartTicks", "linuxBootId")},
                       "metrics": dict(rssBytes=100 + index, osLifetimePeakRssBytes=110 + index, statusSwapBytes=0), "errors": []})
    return expected, output, dict(qpcFrequency=None, perfCounterImplementation="clock_gettime(CLOCK_MONOTONIC)", monotonicNs=True)


def host_fixture(mode="empty"):
    contract = oracle.load(Path(oracle.__file__).with_name("ChartsMemoryProbe.Contract.json"))
    expected = identity(mode)
    phases = [dict(name=name, startQpc=200 + index * 10, endQpc=205 + index * 10, uiThreadAllocatedBytes=0) for index, name in enumerate(oracle.PHASES)]
    managed = [dict(phase=name, startQpc=200, endQpc=201, estimatedManagedBytes=10, totalAllocatedBytes=20,
                    lastGcIndex=0, lastGcGeneration=0, lastGcHeapSizeBytes=0, lastGcFragmentedBytes=0,
                    lastGcTotalCommittedBytes=0, collections=[0, 0, 0]) for name in oracle.MANAGED_PHASES]
    environment = {key: 0 for key in contract["matchedEnvironment"]}
    environment.update(fixedXAxis=[-1, 1000000], fixedYAxis=[-2, 194], renderer="software", renderScaling=1, dpi=[96, 96],
                       chartDips=[1200, 650], rasterPixels=[1200, 650], surface=dict(hasGrContext=False, error=None), tieredCompilation=False,
                       samplingMode="MinMax", strokeThickness=2, markerSize=0, showMarkers=False, showLegend=False, showGridLines=True,
                       showDataLabels=False, seriesCount=1, fontProbes=[dict(scalar=scalar, matched=True, glyph=12, font="Test", sha256="A" * 64) for scalar in [48, 70]],
                       sources=[dict(name=name, bytes=1, sha256="B" * 64) for name in sorted(oracle.EXPECTED_DEPENDENCIES)], display=None, waylandDisplay=None)
    envelope = dict(schema="charts-memory-probe/v1", **expected, stopwatchFrequency=1_000_000_000)
    ready = dict(envelope, payload=dict(readyQpc=100, environment=environment))
    payload = dict(status="completed", samplerReadyObservedQpc=180, workStartQpc=190, workEndQpc=300, phases=phases, managed=managed,
                   nativeBytes=None, gpuBytes=None, hitPassed=True, allFinitePointsInsideAxes=True, sourceGeneratedAfterSamplerReady=True,
                   primitiveCount=10, inputCount=0, rawInputRetained=False, originalPointValueBytes=0, emptySourceNoGeneration=True,
                   datasetManifest=None, sampling=dict(requestedMode="MinMax", renderScale=1, pixelWidth=1, sourceCount=0, visibleCount=0,
                                                      renderedCount=0, figureCount=0, pixelBucketCount=1000, isApplied=False))
    if mode == "full":
        payload.update(inputCount=1_000_000, rawInputRetained=True, originalPointValueBytes=16_000_000, emptySourceNoGeneration=False,
                       datasetManifest=dict(id="S1M-L", generatorVersion="charts-c0-fixed-integer-xorshift64star-v1", seedHex="1020304050607085", revision=0,
                                            inputPointCount=1_000_000, validPointCount=1_000_000, seriesPointCounts=[1_000_000], seriesSha256=["C" * 64], sha256="D" * 64,
                                            monotonicX=True, containsGaps=False, markerSize=0, realtimePoc=False))
        payload["sampling"].update(sourceCount=1_000_000, visibleCount=1_000_000, renderedCount=3000, figureCount=1, isApplied=True)
    work = dict(envelope, payload=payload)
    raw = dict(envelope, payload=dict(status="completed", error=None, phases=copy.deepcopy(phases), managed=copy.deepcopy(managed), finishedQpc=400))
    return ready, work, raw, contract


def host_receipt(expected, parent_utc=None):
    observed = {key: expected[key] for key in ("processId", "windowsCreationFileTimeUtc", "linuxProcStartTicks", "linuxBootId")}
    observed.update(readStartedUtc="2026-10-27T01:00:00.0000001Z", readFinishedUtc="2026-10-27T01:00:00.0000002Z", readStartQpc=10, readEndQpc=11,
                    qpcFrequency=10_000_000 if expected["windowsCreationFileTimeUtc"] is not None else 1_000_000_000)
    parent = parent_utc or expected["processStartedUtc"]
    receipt = dict(schemaVersion="charts-memory-process-v2", hostReadyIdentityValidated=True, processId=expected["processId"], processStartedUtc=parent, parentObservedProcessStartedUtc=parent,
                   canonicalProcessStartedUtc=expected["processStartedUtc"], nativeIdentityAtLaunch=copy.deepcopy(observed), nativeIdentityAtReady=copy.deepcopy(observed))
    receipt["nativeIdentityAtReady"].update(readStartQpc=20, readEndQpc=21)
    return receipt


class MemoryTests(unittest.TestCase):
    def test_complete_filetime_keeps_seventh_digit(self):
        first = sampler.utc_filetime("2026-10-27T00:00:00.1234567Z")
        self.assertGreater(first, 2 ** 53)
        self.assertEqual(1, sampler.utc_filetime("2026-10-27T00:00:00.1234568Z") - first)
        self.assertEqual(first, sampler.utc_filetime("2026-10-27T00:00:00.1234567+00:00"))
        with self.assertRaises(ValueError):
            sampler.utc_filetime("2026-10-27T00:00:00.123456Z")

    def test_proc_stat_command_parentheses(self):
        fields = ["S"] + ["0"] * 18 + ["987654321"]
        self.assertEqual(987654321, sampler.parse_proc_stat("1234 (odd ) name (xx)) " + " ".join(fields)))
        with self.assertRaises(ValueError):
            sampler.parse_proc_stat("bad")

    def test_proc_kib_exact_units(self):
        self.assertEqual({"VmRSS": 12288, "VmHWM": 13312}, sampler.parse_kib("VmRSS: 12 kB\nVmHWM: 13 kB\n", ["VmRSS", "VmHWM"]))
        for text in ["VmRSS: 12 MB", "VmRSS: -2 kB", "VmRSS: 2 kB\nVmRSS: 3 kB"]:
            with self.assertRaises(ValueError):
                sampler.parse_kib(text, ["VmRSS"])

    def test_observed_maxima_do_not_subtract_input(self):
        _, values, _ = rows()
        self.assertEqual(101, sampler.observed_maxima(values)["rssBytes"])
        values[1]["errors"] = [dict(message="failed")]
        self.assertEqual(100, sampler.observed_maxima(values)["rssBytes"])

    def test_ns_integer_roundtrip_and_gaps(self):
        expected, values, clock = rows()
        roundtrip = json.loads(json.dumps(values))
        self.assertEqual(values, roundtrip)
        result = oracle.validate_stream(roundtrip, expected, "process", "linux", clock)
        self.assertEqual(20_000_001, result["largestGapNs"])
        self.assertEqual(101, result["maxObserved"]["rssBytes"])

    def stream_reject(self, change):
        expected, values, clock = rows(); change(values)
        with self.assertRaises(ValueError):
            oracle.validate_stream(values, expected, "process", "linux", clock)

    def test_reused_pid_start_rejected(self):
        self.stream_reject(lambda values: values[1].update(linuxProcStartTicks=987654322))

    def test_nonce_rejected(self):
        self.stream_reject(lambda values: values[1].update(runToken="f" * 32))

    def test_external_identity_mismatch(self):
        self.stream_reject(lambda values: values[1]["observedIdentity"].update(linuxBootId="wrong"))

    def test_sample_error_not_zero(self):
        self.stream_reject(lambda values: values[1].update(errors=[dict(message="permission denied")], metrics=None))

    def test_gap_tampering(self):
        self.stream_reject(lambda values: values[1].update(gapNs=20_000_000))

    def test_float_timestamp_rejected(self):
        self.stream_reject(lambda values: values[0].update(readStartNs=float(values[0]["readStartNs"])))

    def test_missing_terminal_sample(self):
        self.stream_reject(lambda values: values[1].update(reason="periodic"))

    def test_metric_unit_not_an_alias(self):
        self.stream_reject(lambda values: values[1]["metrics"].update(rssKb=1))

    def test_os_counter_separate_from_sampled_current(self):
        self.stream_reject(lambda values: values[1]["metrics"].update(osLifetimePeakRssBytes=1))
        expected, values, _ = rows()
        expected.update(windowsCreationFileTimeUtc=str(sampler.utc_filetime(expected["processStartedUtc"])), linuxProcStartTicks=None, linuxBootId=None)
        for row in values:
            row.update(expected)
            row["observedIdentity"] = {key: expected[key] for key in ("windowsCreationFileTimeUtc", "linuxProcStartTicks", "linuxBootId")}
            row.update(readStartQpc=row["readStartNs"] // 100, readEndQpc=(row["readEndNs"] + 99) // 100,
                       metrics=dict(privateCommitBytes=100, workingSetBytes=80, osLifetimePeakCommitChargeBytes=150, osLifetimePeakWorkingSetBytes=120))
        result = oracle.validate_stream(values, expected, "process", "windows", dict(qpcFrequency=10_000_000))
        self.assertEqual(100, result["maxObserved"]["privateCommitBytes"])
        self.assertEqual(150, result["maxObserved"]["osLifetimePeakCommitChargeBytes"])

    def test_smaps_private_resident_components_and_missing_swap_pss(self):
        expected, values, clock = rows()
        for row in values:
            row.update(kind="smaps", metrics=dict(rssBytes=30, pssBytes=20, privateResidentBytes=12, privateCleanBytes=5,
                                                  privateDirtyBytes=7, privateHugetlbBytes=0, swapBytes=2, swapPssBytes=None))
        self.assertEqual(12, oracle.validate_stream(values, expected, "smaps", "linux", clock)["maxObserved"]["privateResidentBytes"])
        values[1]["metrics"]["privateResidentBytes"] = 11
        with self.assertRaises(ValueError):
            oracle.validate_stream(values, expected, "smaps", "linux", clock)

    def test_windows_identity_full_filetime_independent_oracle(self):
        value = dict(schema="charts-memory-probe/v1", **identity())
        value.update(windowsCreationFileTimeUtc=str(sampler.utc_filetime(value["processStartedUtc"])), linuxProcStartTicks=None, linuxBootId=None)
        self.assertEqual(value["processId"], oracle.identity(value)["processId"])
        value["processStartedUtc"] = "2026-10-27T00:00:00.1234568Z"
        with self.assertRaises(ValueError):
            oracle.identity(value)

    def test_empty_host_contract(self):
        expected, payload, environment = oracle.validate_host(*host_fixture())
        self.assertEqual("empty", expected["mode"])
        self.assertEqual(0, payload["inputCount"])

    def test_full_host_count_hash_minmax_contract(self):
        with patch.object(oracle, "expected_dataset", return_value=("C" * 64, "D" * 64)):
            self.assertEqual(1_000_000, oracle.validate_host(*host_fixture("full"))[1]["sampling"]["sourceCount"])

    def host_reject(self, change, mode="full"):
        fixture = host_fixture(mode); change(*fixture)
        with patch.object(oracle, "expected_dataset", return_value=("C" * 64, "D" * 64)):
            with self.assertRaises(ValueError):
                oracle.validate_host(*fixture)

    def test_source_removed_before_total(self):
        self.host_reject(lambda ready, work, raw, contract: work["payload"].update(rawInputRetained=False))

    def test_source_preclipped(self):
        self.host_reject(lambda ready, work, raw, contract: work["payload"]["sampling"].update(visibleCount=999999))

    def test_source_hash_mismatch(self):
        self.host_reject(lambda ready, work, raw, contract: work["payload"]["datasetManifest"].update(seriesSha256=["E" * 64]))

    def test_empty_source_generation_forbidden(self):
        self.host_reject(lambda ready, work, raw, contract: work["payload"].update(emptySourceNoGeneration=False), "empty")

    def test_handshake_must_precede_generation(self):
        self.host_reject(lambda ready, work, raw, contract: work["payload"].update(samplerReadyObservedQpc=301))

    def test_native_subtraction_claim_forbidden(self):
        self.host_reject(lambda ready, work, raw, contract: work["payload"].update(nativeBytes=123))

    def test_fixed_axes_cannot_auto_fit_empty(self):
        self.host_reject(lambda ready, work, raw, contract: ready["payload"]["environment"].update(fixedXAxis=[0, 1]), "empty")

    def test_fonts_and_dependency_content_required(self):
        self.host_reject(lambda ready, work, raw, contract: ready["payload"]["environment"]["fontProbes"][0].update(matched=False))
        self.host_reject(lambda ready, work, raw, contract: ready["payload"]["environment"].update(sources=[]))

    def test_pair_signed_increment_and_independent_metric_kinds(self):
        baseline = dict(pairIndex=1, position=1, platform="linux", environment=dict(fontProbes=[], display=None, waylandDisplay=None), streams=dict(process=dict(maxObserved=dict(rssBytes=100))))
        full = copy.deepcopy(baseline); full.update(position=2); full["streams"]["process"]["maxObserved"]["rssBytes"] = 90
        result = oracle.compare_pair(baseline, full, ["fontProbes"], 50)
        self.assertEqual(-10, result["increments"]["process.rssBytes"]["fullMinusEmptyObservedBytes"])
        full["streams"]["process"]["maxObserved"]["rssBytes"] = 151
        self.assertFalse(oracle.compare_pair(baseline, full, ["fontProbes"], 50)["diagnosticIncrementTargetPassed"])
        full["environment"]["fontProbes"] = ["different"]
        with self.assertRaises(ValueError):
            oracle.compare_pair(baseline, full, ["fontProbes"], 50)

    def test_atomic_fresh_output_and_duplicate_json(self):
        parent = Path(__file__).resolve().parents[2] / "artifacts/charts-memory-sampling-ci/fixtures"
        parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="synthetic-", dir=parent) as temporary:
            root = Path(temporary).resolve(); self.assertEqual(parent.resolve(), root.parent)
            output = root / "ready.json"; sampler.write_new(output, dict(status="ready"))
            self.assertEqual("ready", sampler.load(output)["status"])
            with self.assertRaises(ValueError):
                sampler.write_new(output, {})
            output.write_text('{"pid":1,"pid":2}', encoding="utf-8")
            with self.assertRaises(ValueError):
                oracle.load(output)

    def test_partial_complete_cannot_release_other_identity(self):
        expected = identity()
        value = {**expected, "status": "release"}
        oracle.control_identity(expected, value, "release")
        value["processStartedUtc"] = "2026-10-27T00:00:00.1234568Z"
        with self.assertRaises(ValueError):
            oracle.control_identity(expected, value, "release")

    def test_exit_codes_preserve_valid_budget_failure(self):
        self.assertEqual(0, oracle.diagnostic_exit_code(True, True))
        self.assertEqual(1, oracle.diagnostic_exit_code(True, False))
        self.assertEqual(2, oracle.diagnostic_exit_code(False, None))
        self.assertEqual(0, oracle.diagnostic_exit_code(True, False, evidence_only=True))
        self.assertEqual(2, oracle.diagnostic_exit_code(False, None, evidence_only=True))

    def test_linux_parent_child_utc_difference_needs_exact_native_proof(self):
        expected = identity()
        expected["processStartedUtc"] = "2026-10-26T18:08:32.7439054Z"
        receipt = host_receipt(expected, "2026-10-26T18:08:32.7445606Z")
        proof = oracle.validate_host_receipt(expected, receipt, "linux")
        self.assertFalse(proof["utcConversionsEqual"])
        self.assertEqual("2026-10-26T18:08:32.7445606Z", proof["parentObservedProcessStartedUtc"])
        self.assertEqual(expected["processStartedUtc"], proof["canonicalProcessStartedUtc"])
        # The observed delta changes sign in another actual v1 counterexample.
        expected["processStartedUtc"] = "2026-10-26T18:08:41.6269993Z"
        self.assertFalse(oracle.validate_host_receipt(expected, host_receipt(expected, "2026-10-26T18:08:41.6245606Z"), "linux")["utcConversionsEqual"])

    def test_linux_utc_equality_cannot_mask_reused_pid(self):
        expected = identity(); receipt = host_receipt(expected)
        receipt["nativeIdentityAtReady"]["linuxProcStartTicks"] += 1
        with self.assertRaises(ValueError):
            oracle.validate_host_receipt(expected, receipt, "linux")

    def test_linux_boot_mismatch_is_not_a_tolerable_utc_conversion(self):
        expected = identity(); receipt = host_receipt(expected)
        receipt["nativeIdentityAtLaunch"]["linuxBootId"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
        with self.assertRaises(ValueError):
            oracle.validate_host_receipt(expected, receipt, "linux")

    def test_receipt_parent_utc_cannot_be_rewritten_to_canonical(self):
        expected = identity(); receipt = host_receipt(expected, "2026-10-27T00:00:00.1234568Z")
        receipt["processStartedUtc"] = expected["processStartedUtc"]
        with self.assertRaises(ValueError):
            oracle.validate_host_receipt(expected, receipt, "linux")

    def test_canonical_utc_must_remain_exact_child_text(self):
        expected = identity(); receipt = host_receipt(expected)
        receipt["canonicalProcessStartedUtc"] = "2026-10-27T00:00:00.1234568Z"
        with self.assertRaises(ValueError):
            oracle.validate_host_receipt(expected, receipt, "linux")

    def test_windows_parent_creation_filetime_stays_strict(self):
        expected = identity(); expected.update(windowsCreationFileTimeUtc=str(sampler.utc_filetime(expected["processStartedUtc"])), linuxProcStartTicks=None, linuxBootId=None)
        self.assertTrue(oracle.validate_host_receipt(expected, host_receipt(expected), "windows")["utcConversionsEqual"])
        with self.assertRaises(ValueError):
            oracle.validate_host_receipt(expected, host_receipt(expected, "2026-10-27T00:00:00.1234568Z"), "windows")

    def test_native_process_instance_dedup_does_not_use_utc_conversion(self):
        expected = identity(); changed = copy.deepcopy(expected)
        changed["processStartedUtc"] = "2026-10-27T00:00:00.1234999Z"
        self.assertEqual(oracle.native_process_key(expected), oracle.native_process_key(changed))
        changed["linuxProcStartTicks"] += 1
        self.assertNotEqual(oracle.native_process_key(expected), oracle.native_process_key(changed))


if __name__ == "__main__":
    unittest.main()
