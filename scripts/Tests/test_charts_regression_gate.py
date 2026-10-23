"""Portable negative tests for the main native guard, using explicitly synthetic data.

These fixtures exercise the evidence reader; they never claim to create a native
window. Real window evidence is produced separately by Charts.NativeBenchmarks.
"""
from __future__ import annotations

import copy
import hashlib
import json
import os
from pathlib import Path
import struct
import sys
import tempfile
import unittest
import zlib

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from charts_regression_gate import Guard, digest, load, reject_constant, unique_object
from charts_png_oracle import sha_bgra

POLICY = load(Path(__file__).resolve().parents[1] / "charts-ci-performance-policy.json")["native"]
TICK_KEYS = ("request", "applyComplete", "layoutComplete", "animationCallback", "captureStart", "captureComplete", "hitStart", "hitComplete", "terminal")


def png(width, height, rgba):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    rows = b"".join(b"\0" + rgba[y * width * 4:(y + 1) * width * 4] for y in range(height))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))


def trace_for(raw):
    result = [dict(kind="driver-started", ticks=0, correlationId=None)]
    for scenario in raw["scenarios"]:
        result.append(dict(kind="scenario-started", ticks=scenario["samples"][0]["ticks"]["request"] - 1,
                           correlationId=None, scenario=scenario["id"]))
        for sample in scenario["samples"]:
            for kind, tick in (("request-started", "request"), ("request-completed", "terminal")):
                result.append(dict(kind=kind, ticks=sample["ticks"][tick], correlationId=sample["correlationId"]))
    result.append(dict(kind="driver-terminal", ticks=result[-1]["ticks"] + 1, correlationId=None))
    for index, event in enumerate(result):
        event["index"] = index
    return result


def make_fixture(root, run_id):
    root.mkdir()
    width, height = 800, 400
    rgba = bytearray(b"\xff" * (width * height * 4))
    for chart in range(4):
        for y in range(chart * 100 + 10, chart * 100 + 30):
            for x in range(10, 30):
                offset = (y * width + x) * 4
                rgba[offset:offset + 4] = b"\x15\x5c\xc1\xff"
    image = root / "fixture.png"
    image.write_bytes(png(width, height, bytes(rgba)))
    image_sha, pixel_sha = digest(image), sha_bgra(bytes(rgba))
    manifest = [dict(path="unit-fixture-only/Program.cs", bytes=12, sha256=hashlib.sha256(b"unit fixture").hexdigest().upper())]
    driver_sha = hashlib.sha256("\n".join(f"{row['path']}|{row['bytes']}|{row['sha256']}" for row in manifest).encode()).hexdigest().upper()
    environment = dict.fromkeys(POLICY["comparableEnvironmentFields"], "unit-fixture-only")
    environment.update(stopwatchFrequency=1000, nativeHandleDescriptor="unit-fixture-only", backend="unit-fixture-only",
                       renderScaling=1.0, rasterDpi=96.0, fontFamilies=["unit-fixture-only"], cpuCount=4,
                       tieredCompilation=False, tieredCompilationSwitchPresent=True,
                       tieredCompilationSwitchValue=False, dotnetTieredCompilationOverride=None,
                       comPlusTieredCompilationOverride=None)
    runtime_config = root / "Charts.NativeBenchmarks.runtimeconfig.json"
    runtime_config.write_text(json.dumps(dict(runtimeOptions=dict(configProperties={"System.Runtime.TieredCompilation": False}))), encoding="utf-8")
    runtime_evidence = dict(path=str(runtime_config), bytes=runtime_config.stat().st_size, sha256=digest(runtime_config),
                            declaredTieredCompilation=False, appContextSwitchPresent=True, appContextSwitchValue=False,
                            dotnetOverride=None, comPlusOverride=None)
    raw = dict(schema="charts-native-window-v1", actualNativeWindowRequired=True, passed=True,
               status="completed", error=None, finishedUtc="2026-10-20T00:00:00Z", runId=run_id,
               presentMeasured=False, gpuMeasured=False, physicalInputMeasured=False, nativePresentTicks=None, gpuTicks=None,
               options=dict(samples=12, warmups=2, scenarios=POLICY["scenarios"], sourceRoot=str(root / "unit-source")),
               environment=environment, assemblyManifest=[dict(path="unit-fixture-only/Charts.NativeBenchmarks.dll")],
               driverSourceManifest=manifest, driverSourceSha256=driver_sha, runtimeConfiguration=runtime_evidence, scenarios=[])
    for scenario_index, sid in enumerate(POLICY["scenarios"]):
        chart_count = 4 if sid == "multi-4x512" else 1
        row_count = 512 if chart_count == 4 else 10000
        dataset = root / (sid + "-dataset.json")
        dataset.write_text(json.dumps(dict(rows=[dict(id=f"row-{i}", x=i, y=20 + i * 37 % 71) for i in range(row_count)])), encoding="utf-8")
        scenario = dict(id=sid, passed=True, chartCount=chart_count, pointCountPerChart=row_count,
                        datasetPath=dataset.name, datasetSha256=digest(dataset), samples=[])
        revision = 0
        for index in range(14):
            action = ("viewport", "data", "routed-key-reset")[index % 3]
            previous = revision
            revision += int(action == "data")
            roi = [[0, chart * 100, 800, 100] for chart in range(4)] if chart_count == 4 else [[0, 0, 800, 400]]
            sample = dict(index=index, phase="warmup" if index < 2 else "measured", passed=True, outcome="completed", error=None,
                          correlationId=f"{run_id}:{sid}:{index}", action=action, dataRevisionBefore=previous, dataRevisionAfter=revision,
                          nativeWindowVisible=True, checks=dict.fromkeys(POLICY["sampleChecks"], True),
                          inputHandled=True if action == "routed-key-reset" else None,
                          sourceVersionsBefore=[previous + 1] * chart_count, sourceVersionsAfter=[revision + 1] * chart_count,
                          requestedSceneRevision=index + 1,
                          ticks={key: 10000 + scenario_index * 1000000 + index * 10000 + tick_index for tick_index, key in enumerate(TICK_KEYS)},
                          uiApplyLayoutAllocatedBytes=1000, captureReadbackAllocatedBytes=1000, hitAllocatedBytes=1000,
                          capturePath=image.name, canonicalPath=image.name, captureSha256=image_sha, canonicalSha256=image_sha,
                          pixelSha256=pixel_sha, canonicalPixelSha256=pixel_sha, pixelWidth=width, pixelHeight=height,
                          plotPixelRects=roi, businessInkPixels=1600, plotInkPixels=[400] * 4 if chart_count == 4 else [1600], hits=[])
            planned = row_count // 2 + index
            if sid == "bar-10k":
                planned = row_count // 2
                while planned * 37 % 71 != 70:
                    planned += 1
                planned += index * 71
            x, y = planned, 20 + planned * 37 % 71 + revision * .25
            for chart_index in range(chart_count):
                plot = roi[chart_index]
                sample["hits"].append(dict(passed=True, chartIndex=chart_index, expectedIndex=planned, actualIndex=planned,
                                           expectedX=x, actualX=x, expectedY=y, actualY=y,
                                           expectedStableId=f"row-{planned}", actualStableId=f"row-{planned}",
                                           plot=plot, range=[0, row_count, 0, 120],
                                           queryX=plot[0] + x / row_count * plot[2], queryY=plot[1] + plot[3] - y / 120 * plot[3]))
            scenario["samples"].append(sample)
        raw["scenarios"].append(scenario)
    write_fixture(root, raw)
    return raw


def write_fixture(root, raw, events=None):
    (root / "raw.json").write_text(json.dumps(raw), encoding="utf-8")
    (root / "events.jsonl").write_text("".join(json.dumps(event) + "\n" for event in (trace_for(raw) if events is None else events)), encoding="utf-8")


class MainGuardTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory()
        cls.root = Path(cls.temporary.name)
        cls.candidate_root, cls.baseline_root = cls.root / "candidate", cls.root / "baseline"
        cls.original = make_fixture(cls.candidate_root, "unit-candidate")
        cls.baseline = make_fixture(cls.baseline_root, "unit-baseline")
        cls.pixel_cache = {}
        cls.sequence = 0
        cls.evidence_root = None
        if os.environ.get("CHARTS_GUARD_TEST_EVIDENCE"):
            cls.evidence_root = Path(os.environ["CHARTS_GUARD_TEST_EVIDENCE"]) / "synthetic-case-evidence"
            cls.evidence_root.mkdir()
            (cls.evidence_root / "blobs").mkdir()
            (cls.evidence_root / "cases").mkdir()

    @classmethod
    def tearDownClass(cls):
        cls.temporary.cleanup()

    def guard(self):
        type(self).sequence += 1
        output = self.root / f"guard-{self.sequence}" / "result.json"
        output.parent.mkdir()
        guard = Guard(output)
        guard._pixels = self.pixel_cache
        guard._unit_case_sequence = self.sequence
        return guard

    def archive_case(self, guard, phase):
        """Preserve exact bytes immediately; declared paths in raw JSON stay intact."""
        if self.evidence_root is None:
            return
        files = {}
        for prefix, root in (("candidate", self.candidate_root), ("baseline", self.baseline_root), ("guard", guard.output.parent)):
            for path in sorted(root.rglob("*")):
                if path.is_file():
                    files[path.resolve()] = prefix + "/" + path.relative_to(root).as_posix()
        for path in guard.inputs:
            path = Path(path).resolve()
            if path.is_file() and path not in files:
                files[path] = "guard-input/" + path.name
        scripts = Path(__file__).resolve().parents[1]
        for name in ("charts-ci-performance-policy.json", "charts-ci-mechanism-contracts.json",
                     "charts_regression_gate.py", "charts_mechanism_oracle.py", "charts_png_oracle.py"):
            files[(scripts / name).resolve()] = "source/" + name
        files[Path(__file__).resolve()] = "source/Tests/" + Path(__file__).name
        path_map = []
        for path, logical_path in sorted(files.items(), key=lambda item: str(item[0])):
            payload = path.read_bytes()
            fingerprint = hashlib.sha256(payload).hexdigest().upper()
            blob = self.evidence_root / "blobs" / fingerprint
            if blob.exists():
                if blob.read_bytes() != payload:
                    raise AssertionError("Content-addressed evidence collision")
            else:
                with blob.open("xb") as stream:
                    stream.write(payload)
            path_map.append(dict(originalPath=str(path), logicalPath=logical_path,
                                 blobPath="blobs/" + fingerprint, bytes=len(payload), sha256=fingerprint))
        case_id = f"{self._testMethodName}-{guard._unit_case_sequence:04d}-{phase}"
        receipt = dict(schemaVersion="charts-synthetic-guard-case-v1", caseId=case_id, testId=self.id(),
                       subTest=str(self._subtest) if self._subtest is not None else None, phase=phase,
                       scope="Synthetic unit/adversarial evidence only; no native window or product performance measurement",
                       candidateOriginalRoot=str(self.candidate_root), baselineOriginalRoot=str(self.baseline_root),
                       guardOutputOriginalPath=str(guard.output), checks=guard.checks, errors=guard.errors,
                       metrics=guard.native, comparisons=guard.comparisons, failureRates=guard.failure_rates,
                       guardInputs=guard.inputs, policy=POLICY, files=path_map,
                       activeException=repr(sys.exc_info()[1]) if sys.exc_info()[1] is not None else None)
        with (self.evidence_root / "cases" / (case_id + ".json")).open("x", encoding="utf-8") as stream:
            json.dump(receipt, stream, indent=2, allow_nan=False)
            stream.write("\n")

    def evaluate(self, mutate=None, events=None):
        raw = copy.deepcopy(self.original)
        if mutate:
            mutate(raw)
        write_fixture(self.candidate_root, raw, events)
        guard = self.guard()
        try:
            result = guard.section("unit-native", lambda: guard.native_report(self.candidate_root, POLICY, "unit-native"))
        finally:
            self.archive_case(guard, "native-evaluation")
        return guard, result

    def assert_rejected(self, mutate, expected_check=None):
        guard, _ = self.evaluate(mutate)
        self.assertTrue(guard.checks)
        self.assertTrue(guard.errors)
        if expected_check:
            self.assertTrue(any(expected_check in entry["check"] for entry in guard.errors), guard.errors)

    def test_valid_unit_only_fixture_has_all_56_samples_and_118_events(self):
        guard, result = self.evaluate()
        self.assertIsNotNone(result)
        self.assertFalse(guard.errors, guard.errors)
        self.assertEqual(dict(numerator=0, denominator=56, rate=0), guard.failure_rates["unit-native"])
        self.assertEqual(118, len(trace_for(self.original)))

    def test_false_empty_removed_and_string_checks_fail(self):
        for mode in ("false", "empty", "removed", "string"):
            def mutate(raw, mode=mode):
                checks = raw["scenarios"][0]["samples"][2]["checks"]
                key = next(iter(checks))
                if mode == "empty":
                    checks.clear()
                elif mode == "removed":
                    del checks[key]
                else:
                    checks[key] = False if mode == "false" else "true"
            with self.subTest(mode=mode):
                self.assert_rejected(mutate, "checks-present")

    def test_missing_duplicate_and_reordered_samples_fail(self):
        for mode in ("missing", "duplicate", "reordered"):
            def mutate(raw, mode=mode):
                samples = raw["scenarios"][0]["samples"]
                if mode == "missing":
                    samples.pop()
                elif mode == "duplicate":
                    samples[3] = copy.deepcopy(samples[2])
                else:
                    samples[2], samples[3] = samples[3], samples[2]
            with self.subTest(mode=mode):
                self.assert_rejected(mutate)

    def test_missing_or_reverse_ticks_fail(self):
        self.assert_rejected(lambda raw: raw["scenarios"][0]["samples"][2]["ticks"].pop("hitStart"), "complete-monotonic-ticks")
        self.assert_rejected(lambda raw: raw["scenarios"][0]["samples"][2]["ticks"].__setitem__("captureComplete", 0), "complete-monotonic-ticks")

    def test_slow_raw_interval_with_subsequent_offsets_fails_absolute_guard(self):
        def mutate(raw):
            samples = [sample for scenario in raw["scenarios"] for sample in scenario["samples"]]
            for ordinal, sample in enumerate(samples):
                for key_index, key in enumerate(TICK_KEYS):
                    sample["ticks"][key] += ordinal * 2000 + (2000 if key_index >= 1 else 0)
        self.assert_rejected(mutate, "absolute-p95-ms:apply")

    def test_inflated_allocations_fail_absolute_guard(self):
        def mutate(raw):
            for sample in raw["scenarios"][0]["samples"]:
                sample["hitAllocatedBytes"] = POLICY["maximumP95AllocatedBytes"]["hit"] + 1
        self.assert_rejected(mutate, "absolute-p95-bytes:hit")

    def test_wrong_original_hit_and_forged_expected_index_fail(self):
        self.assert_rejected(lambda raw: raw["scenarios"][0]["samples"][2]["hits"][0].__setitem__("actualIndex", 0), "hit-0")
        def mutate(raw):
            hit = raw["scenarios"][0]["samples"][2]["hits"][0]
            hit.update(expectedIndex=0, actualIndex=0, expectedX=0, actualX=0, expectedY=20.25, actualY=20.25,
                       expectedStableId="row-0", actualStableId="row-0")
        self.assert_rejected(mutate, "hit-0")

    def test_bad_pixel_digest_and_white_business_canvas_fail(self):
        self.assert_rejected(lambda raw: raw["scenarios"][0]["samples"][2].__setitem__("pixelSha256", "0" * 64), "pixel-sha")
        white = self.candidate_root / "white.png"
        rgba = b"\xff" * (800 * 400 * 4)
        white.write_bytes(png(800, 400, rgba))
        def mutate(raw):
            sample = raw["scenarios"][0]["samples"][2]
            sample.update(capturePath=white.name, canonicalPath=white.name, captureSha256=digest(white), canonicalSha256=digest(white),
                          pixelSha256=sha_bgra(rgba), canonicalPixelSha256=sha_bgra(rgba), businessInkPixels=0, plotInkPixels=[0])
        self.assert_rejected(mutate, "business-pixels")

    def test_modified_dataset_fails_even_with_updated_dataset_hash(self):
        path = self.candidate_root / "line-10k-dataset.json"
        original = path.read_bytes()
        try:
            dataset = json.loads(original)
            dataset["rows"][17]["y"] += 1
            path.write_text(json.dumps(dataset), encoding="utf-8")
            self.assert_rejected(lambda raw: raw["scenarios"][0].__setitem__("datasetSha256", digest(path)), "source-formula")
        finally:
            path.write_bytes(original)

    def test_missing_trace_correlation_fails(self):
        events = trace_for(self.original)
        next(event for event in events if event["kind"] == "request-completed")["correlationId"] = "invented-correlation"
        guard, _ = self.evaluate(events=events)
        self.assertTrue(any("trace-correlations" in entry["check"] for entry in guard.errors))

    def test_present_claim_and_numeric_boolean_fail(self):
        self.assert_rejected(lambda raw: raw.__setitem__("presentMeasured", True), "no-present-claims")
        self.assert_rejected(lambda raw: raw["scenarios"][0]["samples"][2]["ticks"].__setitem__("hitStart", True))

    def test_falsely_claimed_controlled_jit_configuration_fails(self):
        incompatible_directory = self.candidate_root / "unit-invalid-runtime-configuration"
        incompatible_directory.mkdir()
        incompatible = incompatible_directory / "Charts.NativeBenchmarks.runtimeconfig.json"
        incompatible.write_text(json.dumps(dict(runtimeOptions=dict(configProperties={"System.Runtime.TieredCompilation": True}))), encoding="utf-8")
        def false_declaration(raw):
            # Preserve every producer pass flag and a truthful file SHA, while
            # claiming false for a configuration file that really declares true.
            raw["runtimeConfiguration"].update(path=str(incompatible), bytes=incompatible.stat().st_size,
                                               sha256=digest(incompatible), declaredTieredCompilation=False)
        self.assert_rejected(false_declaration, "tiered-compilation-protocol")
        self.assert_rejected(lambda raw: raw["runtimeConfiguration"].__setitem__("appContextSwitchPresent", False))
        self.assert_rejected(lambda raw: raw["environment"].__setitem__("tieredCompilation", True))
        self.assert_rejected(lambda raw: raw["runtimeConfiguration"].__setitem__("dotnetOverride", "1"))

    def test_section_exception_preserves_prior_completed_predicate(self):
        guard = self.guard()
        guard.check("unit-section:completed", True)
        def fail():
            raise ValueError("intentional malformed raw receipt")
        try:
            result = guard.section("unit-section", fail)
        finally:
            self.archive_case(guard, "section-exception")
        self.assertIsNone(result)
        self.assertTrue(guard.checks["unit-section:completed"])
        self.assertTrue(guard.errors)
        self.assertTrue(any(value is False for value in guard.checks.values()))

    def test_relative_timing_rejects_candidate_inside_absolute_limits(self):
        def mutate(raw):
            for scenario in raw["scenarios"]:
                for sample in scenario["samples"]:
                    for key in TICK_KEYS[1:]:
                        sample["ticks"][key] += 100
        candidate_guard, candidate = self.evaluate(mutate)
        self.assertFalse(candidate_guard.errors, candidate_guard.errors)
        write_fixture(self.baseline_root, self.baseline)
        comparison_guard = self.guard()
        try:
            baseline = comparison_guard.native_report(self.baseline_root, POLICY, "unit-base")
            comparison_guard.compare_native(candidate, baseline, POLICY, self.candidate_root, self.baseline_root)
        finally:
            self.archive_case(comparison_guard, "native-comparison")
        self.assertTrue(any(":milliseconds:apply:" in entry["check"] for entry in comparison_guard.errors))
        self.assertTrue((comparison_guard.output.parent / "diffs/line-10k-000.png").is_file())

    def test_json_duplicate_and_nonfinite_are_rejected(self):
        with self.assertRaises(ValueError):
            json.loads('{"passed":true,"passed":false}', object_pairs_hook=unique_object)
        with self.assertRaises(ValueError):
            json.loads('{"ticks":NaN}', parse_constant=reject_constant)


if __name__ == "__main__":
    unittest.main()
