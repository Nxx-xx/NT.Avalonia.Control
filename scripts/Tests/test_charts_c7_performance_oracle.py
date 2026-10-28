"""Synthetic evidence-reader regressions; these never launch or certify a chart."""
from __future__ import annotations

from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest import mock

sys.dont_write_bytecode = True
SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import charts_c7_performance_oracle as oracle


def temporary_workspace():
    base = Path(os.environ.get("CHARTS_TEST_TEMP_ROOT", str(SCRIPTS.parent / "artifacts/c7-oracle-unit-temp"))).resolve()
    base.mkdir(parents=True, exist_ok=True)
    temporary = tempfile.TemporaryDirectory(dir=base)
    # Cleanup is limited to this newly created, owned child of the selected test root.
    if not Path(temporary.name).resolve().is_relative_to(base):
        raise RuntimeError("Temporary test path escaped its declared root")
    return temporary


def synthetic_corpus(count=10000, seed=0x1020304050607081):
    state, mask = seed, (1 << 64) - 1
    result = bytearray()
    for index in range(count):
        state = ((state ^ (state >> 12)) & mask)
        state = ((state ^ (state << 25)) & mask)
        state = ((state ^ (state >> 27)) & mask)
        value = state * 2685821657736338717 & mask
        wave = index % 1024
        y = ((wave if wave < 512 else 1024 - wave) * 32 + (value & 511) - 255) / 256
        if index % 997 == 431:
            y += 128
        result += struct.pack("<dd", index, y)
    return bytes(result)


DATA = synthetic_corpus()


def query(index=0, revision=0, start=107, phase="first-use", maintenance=False):
    ordinal = (index * 104729 + 37) % 10000
    x, y = struct.unpack_from("<dd", DATA, ordinal * 16)
    y += .25 if revision % 2 else 0
    xb, yb = struct.unpack("<QQ", struct.pack("<dd", x, y))
    return dict(index=index, phase=phase, revision=revision, probeSeries=0, probeIndex=ordinal,
                queryX=1 + x / 9999 * 1000, queryY=2 + 500 - (y + 10) / 210 * 500,
                actualSeries=0, actualIndex=ordinal, actualStableId=ordinal,
                actualXBits=f"{xb:016X}", actualYBits=f"{yb:016X}", originalXBits=f"{xb:016X}", originalYBits=f"{yb:016X}",
                referenceMatches=True, passed=True, timing=dict(startTicks=start, endTicks=start + 1, allocatedBytes=64),
                maintenance=dict(startTicks=start - 2, endTicks=start - 1, allocatedBytes=64) if maintenance else None,
                plot=[1, 2, 1000, 500], range=[0, 9999, -10, 200])


def counts():
    return dict(inputCount=10000, validCount=10000, visibleCount=10000, renderedCount=10000, primitiveCount=8,
                samplingInfo=[dict(requestedMode="None", isApplied=False, sourceCount=10000,
                                   visibleCount=10000, renderedCount=10000)])


def operation(index=0, revision=0, phase="first-use", start=100, scheduled=None):
    first = phase == "first-use"
    ticks = {"start" if first else "request": start,
             "sourceApplyStart": start + 1, "sourceApplyEnd": start + 2,
             "prepareStart": start + 3, "prepareEnd": start + 4}
    if not first:
        ticks.update(recordStart=start + 5, recordEnd=start + 6)
    ticks.update(controlledRasterStart=start + 7, controlledRasterEnd=start + 8,
                 hitStart=start + 9, hitEnd=start + 10, end=start + 11)
    if scheduled is not None:
        ticks["scheduled"] = scheduled
    allocations = dict(sourceApply=64, prepare=64, controlledRaster=64)
    allocations["firstUsableToControlledRaster" if first else "record"] = 256 if first else 64
    return dict(index=index, phase=phase, action="new-chart-source-bind" if first else "sourceReplace" if index % 10 == 0 else "viewport",
                revision=revision, ticks=ticks, allocations=allocations,
                hit=query(index, revision, start + 9, phase), counts=counts(), passed=True, error=None)


def short_schedule(pending=0, warmup_seconds=2, measure_seconds=3):
    traces, schedules, revision = [], [], 0
    for phase, seconds, started in (("warmup", warmup_seconds, 10000), ("measured", measure_seconds, 20000 + warmup_seconds * 60000)):
        planned = seconds * 60
        attempted = planned - (pending if phase == "measured" else 0)
        for index in range(attempted):
            revision += index % 10 == 0
            scheduled = started + int(index * 60000 / 60)
            traces.append(operation(index, revision, phase, scheduled, scheduled))
        deadline = started + seconds * 60000
        schedules.append(dict(phase=phase, requestedHz=60, planned=planned, attempted=attempted, completed=attempted,
                              failed=0, pending=planned - attempted, startTicks=started, plannedEndTicks=deadline,
                              actualEndTicks=deadline if not (phase == "measured" and pending) else deadline + 15 * 60000 + 1,
                              maximumDrainSeconds=15, passed=attempted == planned))
    return dict(id="S10-L-exact", firstUse=operation(), traceSamples=traces, schedules=schedules)


class RawEvidenceTests(unittest.TestCase):
    def test_nearest_rank_not_average_of_process_percentiles(self):
        self.assertEqual(oracle.percentile(list(range(1, 101)), .95), 95)
        self.assertEqual(oracle.percentile([1, 2, 99], .99), 99)
        self.assertEqual(oracle.describe([1, 2, 99])["count"], 3)

    def test_nonfinite_bool_and_reversed_ticks_are_rejected(self):
        for value in (float("nan"), float("inf"), True, "1"):
            with self.subTest(value=value), self.assertRaises(oracle.EvidenceError):
                oracle.number(value)
        with self.assertRaises(oracle.EvidenceError):
            oracle.duration(2, 1, 1000)

    def test_first_use_contains_same_revision_raster_and_original_hit(self):
        observed = oracle.validate_operation(operation(), "S10-L-exact", [DATA], 1000, first=True)
        self.assertEqual(observed["firstUsableToControlledRasterMs"], 11)

    def test_wrong_reported_duration_is_rejected(self):
        with self.assertRaisesRegex(oracle.EvidenceError, "differs"):
            oracle.timing(dict(startTicks=1, endTicks=101, allocatedBytes=0, elapsedMilliseconds=.01), 1000)

    def test_wrong_original_bits_are_rejected(self):
        value = query()
        value["actualYBits"] = "0000000000000000"
        with self.assertRaisesRegex(oracle.EvidenceError, "original binary"):
            oracle.validate_query(value, [DATA], 1000, "S10-L-exact")

    def test_source_replacement_parity_is_independently_recomputed(self):
        oracle.validate_query(query(revision=1), [DATA], 1000, "S10-L-exact")
        value = query(revision=1)
        value["revision"] = 2
        with self.assertRaises(oracle.EvidenceError):
            oracle.validate_query(value, [DATA], 1000, "S10-L-exact")

    def test_query_projection_and_fixed_probe_trace_are_checked(self):
        value = query()
        value["queryX"] += .1
        with self.assertRaisesRegex(oracle.EvidenceError, "projected"):
            oracle.validate_query(value, [DATA], 1000, "S10-L-exact")

    def test_bar_hit_requires_actual_geometry_not_only_a_valid_original_id(self):
        oracle.validate_query(query(), [DATA, DATA], 1000, "S10-B-exact")
        value = query()
        xb, yb = struct.unpack_from("<QQ", DATA, 0)
        value.update(actualSeries=0, actualIndex=0, actualStableId=0,
                     actualXBits=f"{xb:016X}", originalXBits=f"{xb:016X}",
                     actualYBits=f"{yb:016X}", originalYBits=f"{yb:016X}")
        with self.assertRaisesRegex(oracle.EvidenceError, "geometric query tolerance|visible geometry"):
            oracle.validate_query(value, [DATA, DATA], 1000, "S10-B-exact")
        value = query()
        value["index"] = 1
        with self.assertRaisesRegex(oracle.EvidenceError, "trace changed"):
            oracle.validate_query(value, [DATA], 1000, "S10-L-exact")

    def test_hit_boundary_cannot_be_shortened_independently(self):
        value = operation()
        value["ticks"]["hitEnd"] -= 1
        with self.assertRaisesRegex(oracle.EvidenceError, "boundaries"):
            oracle.validate_operation(value, "S10-L-exact", [DATA], 1000, first=True)

    def test_trace_terminal_end_must_contain_all_stage_ends(self):
        value = operation(phase="measured")
        value["ticks"]["end"] = value["ticks"]["request"]
        with self.assertRaisesRegex(oracle.EvidenceError, "terminal end"):
            oracle.validate_operation(value, "S10-L-exact", [DATA], 1000)

    def test_missing_thread_allocation_stage_is_rejected(self):
        value = operation()
        del value["allocations"]["prepare"]
        with self.assertRaisesRegex(oracle.EvidenceError, "allocation-stage"):
            oracle.validate_operation(value, "S10-L-exact", [DATA], 1000, first=True)

    def test_empty_trace_is_not_an_empty_success(self):
        value = short_schedule()
        value["traceSamples"] = []
        with self.assertRaises(oracle.EvidenceError):
            oracle.validate_schedule(value, dict(warmupSeconds=2, measureSeconds=3), [DATA], 60000, False)

    def test_short_development_trace_cannot_be_formal(self):
        with self.assertRaisesRegex(oracle.EvidenceError, "exactly match frozen"):
            oracle.validate_schedule(short_schedule(), dict(warmupSeconds=2, measureSeconds=3), [DATA], 60000, True)

    def test_longer_trace_cannot_silently_change_frozen_formal_workload(self):
        with self.assertRaisesRegex(oracle.EvidenceError, "exactly match frozen"):
            oracle.validate_schedule(short_schedule(warmup_seconds=11, measure_seconds=31),
                                     dict(warmupSeconds=11, measureSeconds=31), [DATA], 60000, True)

    def test_deleting_a_slow_or_failed_attempt_is_rejected(self):
        value = short_schedule()
        del value["traceSamples"][42]
        with self.assertRaises(oracle.EvidenceError):
            oracle.validate_schedule(value, dict(warmupSeconds=2, measureSeconds=3), [DATA], 60000, False)

    def test_pending_workload_is_valid_failure_evidence_and_not_passing(self):
        values, phases, revision, failures = oracle.validate_schedule(short_schedule(pending=5), dict(warmupSeconds=2, measureSeconds=3), [DATA], 60000, False)
        self.assertEqual(len(values), 175)
        self.assertEqual(phases[1]["pending"], 5)
        self.assertFalse(phases[1]["workloadPassed"])
        self.assertIn("Partial/failed", phases[1]["statisticsQualification"])

    def test_marking_a_pending_schedule_passed_is_rejected(self):
        value = short_schedule(pending=5)
        value["schedules"][1]["passed"] = True
        with self.assertRaises(oracle.EvidenceError):
            oracle.validate_schedule(value, dict(warmupSeconds=2, measureSeconds=3), [DATA], 60000, False)

    def test_source_update_work_cannot_be_removed_from_timings(self):
        value = short_schedule()
        value["traceSamples"][0]["revision"] = 0
        with self.assertRaisesRegex(oracle.EvidenceError, "replacement/revision"):
            oracle.validate_schedule(value, dict(warmupSeconds=2, measureSeconds=3), [DATA], 60000, False)

    def test_fabricated_present_and_reference_ratios_are_rejected(self):
        value = dict(referenceStatus="blocked-reference", referenceMeasurements=None, presentMeasured=False, present=None,
                     framePipelineMeasured=False, framePipeline=None, hardwareGpuMeasured=False, physicalInputMeasured=False)
        oracle.unsupported_claims(value)
        for name, changed in (("present", 12), ("presentMeasured", True), ("framePipeline", 3), ("referenceRatio", 1.1), ("referenceMeasurements", [1])):
            with self.subTest(name=name), self.assertRaises(oracle.EvidenceError):
                oracle.unsupported_claims({**value, name: changed})

    def test_unequal_sampling_quality_cannot_be_compared(self):
        value = dict(samplingMode="None", strokeThicknessDip=2, scatterMarkerSizeDip=0, showLegend=False,
                     showGridLines=True, showDataLabels=False, animations="None", fontSizeDip=12,
                     fontFamily="DejaVu Sans, Segoe UI", chartDips=[1200, 650], rasterPixels=[1200, 650], rasterDpi=[96, 96],
                     sourceReplacement="alternate original Y and Y+0.25; X/Id/order/count unchanged; arrays prepared outside measured operations",
                     corpusManifestDownsamplingIsRuntimeQuality=False, barOptions=[])
        oracle.validate_quality(value, "S10-L-exact")
        with self.assertRaisesRegex(oracle.EvidenceError, "Unequal sampling"):
            oracle.validate_quality(value, "S100-L-minmax")

    def test_same_state_source_view_must_restore_exact_decoded_raster(self):
        initial, final = query(), query(revision=350)
        first_frame = (1, 1, b"\x15\x5c\xc1\xff")
        self.assertTrue(oracle.raster_restoration(initial, final, first_frame, first_frame)["equalPixels"])
        with self.assertRaisesRegex(oracle.EvidenceError, "exact decoded raster"):
            oracle.raster_restoration(initial, final, first_frame, (1, 1, b"\xff\xff\xff\xff"))

    def test_different_final_source_or_view_is_not_a_restoration_pass(self):
        first_frame = (1, 1, b"\x15\x5c\xc1\xff")
        for final in (query(revision=349), {**query(revision=350), "range": [1, 9999, -10, 200]}):
            result = oracle.raster_restoration(query(), final, first_frame, first_frame)
            self.assertEqual(result["status"], "not-comparable-different-final-state")
            self.assertIsNone(result["equalPixels"])

    def test_actual_binary_data_is_checked_even_after_rehashing_metadata(self):
        with temporary_workspace() as temporary:
            root = Path(temporary)
            path = root / "source.xy64le"
            changed = bytearray(DATA)
            changed[200:208] = struct.pack("<d", 99999.)
            path.write_bytes(changed)
            digest = oracle.sha(path)
            combined = "\n".join([oracle.GENERATOR, "S10-L", "1020304050607081", "0", digest])
            scene = dict(id="S10-L-exact", datasetManifest=dict(id="S10-L", generatorVersion=oracle.GENERATOR,
                         seedHex="1020304050607081", inputPointCount=10000, validPointCount=10000,
                         seriesPointCounts=[10000], seriesSha256=[digest], revision=0,
                         sha256=hashlib.sha256(combined.encode()).hexdigest().upper()),
                         datasetFiles=[dict(path=path.name, bytes=len(changed), sha256=digest, seriesIndex=0, pointCount=10000)])
            with self.assertRaisesRegex(oracle.EvidenceError, "frozen C0 generator"):
                oracle.validate_corpus(scene, root)

    def test_duplicate_and_nonfinite_json_keys_are_rejected(self):
        with temporary_workspace() as temporary:
            path = Path(temporary) / "raw.json"
            for contents in ('{"passed":true,"passed":false}', '{"time":NaN}'):
                path.write_text(contents)
                with self.assertRaises(oracle.EvidenceError):
                    oracle.load(path)


class CampaignAggregationTests(unittest.TestCase):
    """Only aggregation is stubbed; per-record math is checked above."""
    def setUp(self):
        self.temporary = temporary_workspace()
        self.root = Path(self.temporary.name)
        self.protocol = SCRIPTS / "charts-c7-performance-protocol.json"
        self.entries = [dict(kind="interaction", role="formal", ordinal=i) for i in range(5)]
        self.entries += [dict(kind="first-use", role="formal", ordinal=i + 5) for i in range(100)]
        self.entries += [dict(kind="first-use", role="calibration", ordinal=i + 105) for i in range(2)]
        self.campaign = dict(schemaVersion="charts-c7-performance-campaign/v1", platform="windows", phase="formal",
                             protocolSha256=oracle.sha(self.protocol), expectedSources=dict(chartsSha256="A" * 64, harnessSha256="B" * 64),
                             runs=self.entries, reference=dict(status="blocked-reference", measurements=None, ratios=None),
                             present=dict(status="blocked-present", measurements=None))
        source_rows = [dict(path=path, bytes=1, sha256="F" * 64) for path in
                       ("docs/charts-performance-budgets.json", "scripts/charts-c7-performance-protocol.json",
                        "scripts/charts_c7_performance_oracle.py", "benchmarks/Charts.Benchmarks/DatasetFactory.cs")]
        for name in ("source-before.json", "source-after.json"):
            (self.root / name).write_text(json.dumps(source_rows))
        self.campaign["sourceManifest"] = dict(path="source-before.json", sha256=oracle.sha(self.root / "source-before.json"))

    def tearDown(self):
        self.temporary.cleanup()

    def run_fixture(self, transform=None):
        path = self.root / "campaign.json"
        path.write_text(json.dumps(self.campaign))
        def stub(entry, root, *unused):
            ordinal = entry["ordinal"]
            raw_path = root / f"raw-{ordinal}.json"
            identity = (1000 + ordinal, f"start-{ordinal}")
            row = dict(kind=entry["kind"], role=entry["role"], processId=identity[0], processStartedUtc=identity[1],
                       environment=dict(measured="synthetic-aggregation-test-only"), raw=dict(path=raw_path.name), receipt=dict(path="synthetic"))
            if entry["kind"] == "first-use":
                row["scenes"] = [dict(id=oracle.FIRST_TRACK, firstUse=dict(firstUsableToControlledRasterMs=ordinal))]
            else:
                row["scenes"] = [dict(id=track, metricSamples=[dict(controlledRasterMs=1.)], metrics=dict(),
                                      workloadSchedulesPassed=True, absoluteHitGate=dict(status="passed")) for track in oracle.FORMAL_TRACKS]
            return transform(entry, row, identity, raw_path) if transform else (row, identity, raw_path)
        with mock.patch.object(oracle, "validate_run", side_effect=stub):
            return oracle.validate_campaign(path, self.protocol)

    def test_five_processes_and_hundred_fresh_first_use_are_required(self):
        result = self.run_fixture()
        self.assertTrue(result["passed"])
        self.assertEqual(result["firstUse"]["independentFormalProcesses"], 100)
        self.assertEqual(result["firstUse"]["calibrationsExcluded"], 2)
        self.assertIsNone(result["officialReference"]["ratios"])

    def test_repeated_pid_and_start_cannot_masquerade_as_100_processes(self):
        def transform(entry, row, identity, path):
            return row, (777, "same-start"), path
        result = self.run_fixture(transform)
        self.assertFalse(result["passed"])
        self.assertFalse(result["evidenceIntegrityPassed"])
        self.assertTrue(any("independent" in row["reason"] for row in result["failures"]))

    def test_os_pid_reuse_with_distinct_start_times_is_not_rejected(self):
        def transform(entry, row, identity, path):
            return row, (777, identity[1]), path
        self.assertTrue(self.run_fixture(transform)["passed"])

    def test_removing_a_failed_process_does_not_create_a_passing_campaign(self):
        del self.entries[3]
        result = self.run_fixture()
        self.assertFalse(result["passed"])
        self.assertEqual(result["tracks"]["S10-L-exact"]["independentProcesses"], 4)

    def test_valid_failure_evidence_is_distinct_from_workload_success(self):
        def transform(entry, row, identity, path):
            if entry["kind"] == "interaction":
                row["scenes"][1]["workloadSchedulesPassed"] = False
            return row, identity, path
        result = self.run_fixture(transform)
        self.assertTrue(result["evidenceIntegrityPassed"])
        self.assertFalse(result["passed"])
        self.assertFalse(result["workloadSchedulesPassed"])

    def test_calibration_never_fills_missing_formal_process(self):
        self.entries[5]["role"] = "calibration"
        result = self.run_fixture()
        self.assertFalse(result["passed"])
        self.assertEqual(result["firstUse"]["independentFormalProcesses"], 99)

    def test_short_or_unequal_reference_measurements_cannot_fill_blocked_ratio(self):
        self.campaign["reference"]["ratios"] = dict(controlledRaster=1.1)
        with self.assertRaisesRegex(oracle.EvidenceError, "ratios must remain null"):
            self.run_fixture()

    def test_source_changes_during_measurement_are_rejected(self):
        (self.root / "source-after.json").write_text("[]")
        with self.assertRaisesRegex(oracle.EvidenceError, "inputs changed"):
            self.run_fixture()

    def test_updating_protocol_hash_does_not_approve_weaker_formal_counts(self):
        changed = oracle.load(self.protocol)
        changed["formal"]["independentProcessesPerInteractionTrack"] = 4
        self.protocol = self.root / "weakened-protocol.json"
        self.protocol.write_text(json.dumps(changed))
        self.campaign["protocolSha256"] = oracle.sha(self.protocol)
        with self.assertRaisesRegex(oracle.EvidenceError, "weakened"):
            self.run_fixture()


if __name__ == "__main__":
    unittest.main(verbosity=2)
