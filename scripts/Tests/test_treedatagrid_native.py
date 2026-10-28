import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "verify_treedatagrid_native.py"
SPEC = importlib.util.spec_from_file_location("native_guard", SCRIPT)
GUARD = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GUARD)


class NativeGuardTests(unittest.TestCase):
    def setUp(self):
        base = (SCRIPT.parent.parent / "artifacts/treedatagrid-native-guard-tests").resolve()
        base.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(dir=base)
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.assertTrue(self.root.resolve().is_relative_to(base))
        self.report = {"protocol": "treedatagrid-native-render-v1", "evidenceKind": "NativeMeasured", "passed": True, "pilot": True, "error": None,
                       "checks": {"nativeWindowOnScreen": True, "allNativeRequestsCompleted": True, "sixScenariosAndCorrelatedRenderSamples": True},
                       "options": {"Mode": "performance", "Samples": 1, "Frames": 1, "Warmup": 1}, "requests": 12, "completed": 12,
                       "scenarios": [{"scenario": name} for name in sorted(GUARD.SCENARIOS)]}
        self.events = []
        for identity, (name, warmup) in enumerate(((name, warmup) for name in sorted(GUARD.SCENARIOS) for warmup in (True, False)), 1):
            request = {"id": identity, "scenario": name, "round": 0, "step": int(not warmup), "warmup": warmup}
            applied = {"id": identity, "expectedRevision": 5, "expectedVersion": 3, "start": 10, "layoutEnd": 20, "projectionRows": 100}
            done = {"Id": identity, "Scenario": name, "Round": 0, "Step": int(not warmup), "Warmup": warmup,
                    "StartTicks": 10, "LayoutEndTicks": 20, "UiRenderTicks": 30, "BatchRequestedTicks": 40, "ProcessedTicks": 50, "RenderedTicks": 60,
                    "UiMs": 1, "RenderCompletionMs": 10, "Snapshot": {"Revision": 5, "SourceVersion": 3, "ProjectionRows": 100, "RealizedRows": 1, "RetainedRows": 1, "RealizedCells": 1}}
            self.events += [{"kind": "request", "value": request}, {"kind": "applied", "value": applied}, {"kind": "completed", "value": done}]

    def result(self, allow_pilot=True):
        (self.root / "report.json").write_text(json.dumps(self.report), encoding="utf-8")
        (self.root / "initial.json").write_text(json.dumps({"stopwatchFrequency": getattr(self, "frequency", 1000), "protocol": getattr(self, "initial_protocol", self.report["protocol"])}), encoding="utf-8")
        (self.root / "events.jsonl").write_text("\n".join(json.dumps(event) for event in self.events), encoding="utf-8")
        return GUARD.verify(self.root, allow_pilot)

    def test_complete_pilot_is_valid_only_when_explicitly_allowed(self):
        self.assertTrue(self.result()["passed"])
        self.assertFalse(self.result(False)["passed"])

    def test_open_request_is_rejected(self):
        self.events.pop()
        self.assertFalse(self.result()["passed"])

    def test_duplicate_completion_is_rejected(self):
        self.events.append(copy.deepcopy(self.events[-1]))
        self.assertFalse(self.result()["passed"])

    def test_out_of_order_render_completion_is_rejected(self):
        self.events[-1]["value"]["RenderedTicks"] = 15
        self.assertFalse(self.result()["passed"])

    def test_wrong_source_version_is_rejected(self):
        self.events[-1]["value"]["Snapshot"]["SourceVersion"] = 4
        self.assertFalse(self.result()["passed"])

    def test_wrong_request_identity_is_rejected(self):
        self.events[-1]["value"]["Step"] = 2
        self.assertFalse(self.result()["passed"])

    def test_self_reported_pass_does_not_hide_failed_checks(self):
        self.report["checks"]["nativeWindowOnScreen"] = False
        self.assertFalse(self.result()["passed"])

    def container_metrics(self):
        self.report["environment"] = {"containerMetricsVersion": 1}
        for event in self.events:
            if event["kind"] == "completed": event["value"]["Snapshot"].update(CreatedRows=2, CreatedCells=2)

    def test_container_creation_counters_are_validated(self):
        self.container_metrics(); self.assertTrue(self.result()["passed"])

    def test_created_count_cannot_be_lower_than_realized(self):
        self.container_metrics(); self.events[-1]["value"]["Snapshot"]["CreatedRows"] = 0
        self.assertFalse(self.result()["passed"])

    def test_created_count_must_not_decrease_within_a_fixture(self):
        self.container_metrics(); self.events[-1]["value"]["Snapshot"]["CreatedCells"] = 1
        self.assertFalse(self.result()["passed"])

    def test_created_count_cannot_be_a_boolean(self):
        self.container_metrics(); self.events[-1]["value"]["Snapshot"]["CreatedRows"] = True
        self.assertFalse(self.result()["passed"])

    def test_container_metric_version_cannot_be_a_boolean(self):
        self.container_metrics(); self.report["environment"]["containerMetricsVersion"] = True
        self.assertFalse(self.result()["passed"])

    def test_missing_scenario_is_rejected(self):
        self.report["scenarios"].pop()
        self.assertFalse(self.result()["passed"])

    def test_claimed_physical_present_timing_is_rejected(self):
        self.report["nativePresentTicks"] = 42
        self.assertFalse(self.result()["passed"])

    def test_warmup_cannot_replace_measured_samples(self):
        self.events[-3]["value"]["warmup"] = True
        self.events[-1]["value"]["Warmup"] = True
        self.assertFalse(self.result()["passed"])

    def test_missing_native_check_is_rejected(self):
        del self.report["checks"]["nativeWindowOnScreen"]
        self.assertFalse(self.result()["passed"])

    def test_missing_terminal_report_is_rejected(self):
        self.assertFalse(GUARD.verify(self.root, True)["passed"])

    def test_incomplete_warmup_is_rejected(self):
        self.events = self.events[3:]
        self.report["requests"] -= 1
        self.report["completed"] -= 1
        self.assertFalse(self.result()["passed"])

    def soak(self):
        self.report.update(checks={key: True for key in ("nativeWindowOnScreen", "allNativeRequestsCompleted", "retiredNativeObjectsCollected", "nativeSoakInvestigationGuards", "requestedSoakDuration")},
                           options={"Mode": "soak", "DurationSeconds": 1}, measuredSoakSeconds=1.1, cycles=1, requests=8, completed=8,
                           retention=[{"tracked": 10, "collected": 10, "retained": 0}], drift={"investigationRequired": False})
        seed = copy.deepcopy(self.events[-3:])
        self.events = [{"kind": "soak-start", "ticks": 1000}]
        for step in range(8):
            request, applied, completed = copy.deepcopy(seed)
            request["value"].update(id=step + 1, scenario="soak", step=step, warmup=False)
            applied["value"].update(id=step + 1)
            completed["value"].update(Id=step + 1, Scenario="soak", Step=step, Warmup=False)
            self.events.extend([request, applied, completed])
        self.events.extend([{"kind": "cycle-completed", "value": {"warmup": False, "commits": 1, "nativeFrames": 8}}, {"kind": "soak-end", "ticks": 2100}])

    def test_complete_pilot_soak_is_valid(self):
        self.soak()
        self.assertTrue(self.result()["passed"])

    def test_claimed_duration_cannot_replace_raw_clock_interval(self):
        self.soak()
        self.events[-1]["ticks"] = 1500
        self.assertFalse(self.result()["passed"])

    def test_forced_gc_inside_measured_soak_is_rejected(self):
        self.soak()
        self.events.append({"kind": "forced-gc", "ticks": 1500})
        self.assertFalse(self.result()["passed"])

    def test_retained_native_graph_is_rejected(self):
        self.soak()
        self.report["retention"][0]["retained"] = 1
        self.assertFalse(self.result()["passed"])

    def telemetry(self):
        self.soak()
        self.report["protocol"] = "treedatagrid-native-render-v2"
        self.report["checks"]["memoryTelemetryCrossChecks"] = True
        self.report["checks"]["defaultAndCustomFactoryScrollCycles"] = True
        for e in self.events:
            if e["kind"] == "cycle-completed": e["value"].update(cycle=0, defaultFactoryScroll=True)
            if e["kind"] == "completed": e["value"].update(UiMs=10, RenderCompletionMs=50)
        readings = []
        for identity, (phase, ticks) in enumerate((("baseline-after-gc", 500), ("steady", 1200), ("steady-final", 2050), ("after-final-gc", 2500)), 1):
            generations = [{"sizeBeforeBytes": size, "fragmentationBeforeBytes": frag, "sizeAfterBytes": size, "fragmentationAfterBytes": frag} for size, frag in ((300, 30), (300, 30), (300, 30), (100, 10), (0, 0))]
            reading = {"id": identity, "phase": phase, "ticks": ticks, "sampleEndTicks": ticks + 5, "rawManagedBytes": 1100, "managedBytes": 1100, "managedEstimateStatus": "available",
                       "gc": {"index": identity, "generation": 2, "heapSizeBytes": 1000, "fragmentedBytes": 100, "liveBytes": 900, "totalCommittedBytes": 2000, "generations": generations},
                       "gcCollectionsBefore": [identity * 3, identity * 2, identity], "gcCollections": [identity * 3, identity * 2, identity], "gcOccurredDuringSample": False,
                       "privateBytes": 10000, "workingSetBytes": 9000, "handles": 100, "processPeakWorkingSetBytes": 11000, "cpuMilliseconds": identity * 10, "totalAllocatedBytes": identity * 10000,
                       "crossChecksValid": True, "crossCheckErrors": []}
            readings.append(reading)
        self.report["baselineMemory"] = readings[0]
        self.report["memory"] = [{"resources": r} for r in readings[1:3]]
        self.report["drift"]["finalAfterGc"] = readings[3]
        self.report["memoryTelemetry"] = {"snapshots": 4, "unavailableCurrentEstimates": 0, "allCrossChecksValid": True}
        self.events += [{"kind": "resource-snapshot", "ticks": r["sampleEndTicks"] + 5, "value": r} for r in readings]
        return readings

    def test_v2_memory_cross_checks_are_valid(self):
        self.telemetry()
        self.assertTrue(self.result()["passed"])

    def auto_height_soak(self):
        self.telemetry()
        self.report["options"]["AutoHeightDuringSoak"] = True
        for e in self.events:
            if e["kind"] == "completed": e["value"]["Snapshot"].update(AutoRowHeight=True, MaxRowHeight=456)
            if e["kind"] == "cycle-completed": e["value"].update(autoRowHeight=True, maxRowHeight=456)
        self.events.append({"kind": "host-automation-detached", "ticks": 70, "value": {"lastRequestId": 8, "automationPeerPresent": True, "retiredGridStillChild": False}})

    def test_automatic_height_soak_correlates_tall_geometry_with_every_cycle(self):
        self.auto_height_soak(); self.assertTrue(self.result()["passed"])

    def test_fixed_height_frames_cannot_qualify_an_automatic_height_soak(self):
        self.auto_height_soak()
        next(e for e in self.events if e["kind"] == "completed")["value"]["Snapshot"]["AutoRowHeight"] = False
        self.assertFalse(self.result()["passed"])

    def test_automatic_height_cycle_requires_a_real_tall_row(self):
        self.auto_height_soak()
        for e in self.events:
            if e["kind"] == "completed": e["value"]["Snapshot"]["MaxRowHeight"] = 28
            if e["kind"] == "cycle-completed": e["value"]["maxRowHeight"] = 28
        self.assertFalse(self.result()["passed"])

    def test_automatic_height_summary_cannot_override_frame_geometry(self):
        self.auto_height_soak()
        next(e for e in self.events if e["kind"] == "cycle-completed")["value"]["maxRowHeight"] = 900
        self.assertFalse(self.result()["passed"])

    def test_automatic_height_missing_geometry_is_rejected(self):
        self.auto_height_soak()
        del next(e for e in self.events if e["kind"] == "completed")["value"]["Snapshot"]["MaxRowHeight"]
        self.assertFalse(self.result()["passed"])

    def test_automatic_height_option_must_be_boolean(self):
        self.auto_height_soak(); self.report["options"]["AutoHeightDuringSoak"] = "yes"
        self.assertFalse(self.result()["passed"])

    def test_missing_host_automation_retirement_is_rejected(self):
        self.auto_height_soak(); self.events.pop(); self.assertFalse(self.result()["passed"])

    def test_duplicate_host_automation_retirement_is_rejected(self):
        self.auto_height_soak(); self.events.append(copy.deepcopy(self.events[-1])); self.assertFalse(self.result()["passed"])

    def test_retained_host_automation_child_is_rejected(self):
        self.auto_height_soak(); self.events[-1]["value"]["retiredGridStillChild"] = True; self.assertFalse(self.result()["passed"])

    def test_host_automation_retirement_must_follow_render_completion(self):
        self.auto_height_soak(); self.events[-1]["ticks"] = 59; self.assertFalse(self.result()["passed"])

    def test_host_automation_retirement_must_match_the_last_cycle_frame(self):
        self.auto_height_soak(); self.events[-1]["value"]["lastRequestId"] = 7; self.assertFalse(self.result()["passed"])

    def source_refresh_soak(self):
        self.telemetry()
        self.report["options"]["RefreshSourceDuringSoak"] = True
        self.report["checks"]["sameSourceRefreshEveryCycle"] = True
        for e in self.events:
            if e["kind"] == "cycle-completed": e["value"]["sameSourceRefresh"] = True
        event = {"kind": "source-refresh", "ticks": 15, "value": {"requestId": 5, "cycle": 0, "warmup": False, "versionBefore": 2, "versionAfter": 3, "defaultFactory": True}}
        self.events.append(event)
        return event

    def test_configured_source_refresh_is_correlated_to_its_ui_request(self):
        self.source_refresh_soak()
        self.assertTrue(self.result()["passed"])

    def test_missing_source_refresh_cannot_be_claimed_by_a_terminal_check(self):
        self.source_refresh_soak(); self.events.pop()
        self.assertFalse(self.result()["passed"])

    def test_duplicate_source_refresh_is_rejected(self):
        event = self.source_refresh_soak(); self.events.append(copy.deepcopy(event))
        self.assertFalse(self.result()["passed"])

    def test_source_refresh_must_advance_the_rendered_version(self):
        event = self.source_refresh_soak(); event["value"]["versionAfter"] = 4
        self.assertFalse(self.result()["passed"])

    def test_source_refresh_must_be_inside_its_correlated_ui_window(self):
        self.source_refresh_soak()["ticks"] = 30
        self.assertFalse(self.result()["passed"])

    def test_source_refresh_cannot_be_assigned_to_another_step(self):
        self.source_refresh_soak()["value"]["requestId"] = 4
        self.assertFalse(self.result()["passed"])

    def test_source_refresh_factory_and_cycle_must_match(self):
        self.source_refresh_soak()["value"]["defaultFactory"] = False
        self.assertFalse(self.result()["passed"])

    def test_unconfigured_source_refresh_is_rejected(self):
        self.source_refresh_soak(); self.report["options"]["RefreshSourceDuringSoak"] = False
        self.assertFalse(self.result()["passed"])

    def test_v2_rejects_missing_cross_metrics(self):
        self.soak(); self.report["protocol"] = "treedatagrid-native-render-v2"
        self.assertFalse(self.result()["passed"])

    def blocking_reference(self):
        readings = self.telemetry()
        self.report["options"]["BlockingGcReference"] = True
        self.report["memoryTelemetry"].update(requiredGcSnapshotKind="FullBlocking", inconsistentLastAnySnapshots=0)
        for r in readings:
            r["gc"]["concurrent"] = False
            r.update(gcSnapshotKind="FullBlocking", lastAnyGc=copy.deepcopy(r["gc"]), lastAnyGcCrossCheckErrors=[])
        return readings

    def test_blocking_gc_reference_keeps_raw_any_snapshot(self):
        self.blocking_reference()
        self.assertTrue(self.result()["passed"])

    def test_blocking_gc_kind_cannot_be_forged_from_concurrent_gc(self):
        self.blocking_reference()[1]["gc"]["concurrent"] = True
        self.assertFalse(self.result()["passed"])

    def test_blocking_gc_reference_does_not_accept_missing_any_values(self):
        del self.blocking_reference()[1]["lastAnyGc"]
        self.assertFalse(self.result()["passed"])

    def test_inconsistent_raw_any_is_preserved_and_warned_beside_strict_blocking_reference(self):
        r = self.blocking_reference()[1]
        r["lastAnyGc"]["generations"][0]["fragmentationAfterBytes"] += 1
        r["lastAnyGcCrossCheckErrors"] = ["Last-GC generation sums differ from the heap snapshot."]
        self.report["memoryTelemetry"]["inconsistentLastAnySnapshots"] = 1
        result = self.result(); self.assertTrue(result["passed"]); self.assertEqual(1, len(result["warnings"]))

    def test_raw_any_consistency_warning_cannot_be_hidden(self):
        self.blocking_reference()[1]["lastAnyGc"]["generations"][0]["fragmentationAfterBytes"] += 1
        self.assertFalse(self.result()["passed"])

    def test_blocking_gc_reference_still_rejects_required_generation_sum_mismatch(self):
        self.blocking_reference()[1]["gc"]["generations"][0]["fragmentationAfterBytes"] += 1
        self.assertFalse(self.result()["passed"])

    def test_negative_current_estimate_is_explicitly_unavailable_with_valid_last_gc(self):
        readings = self.telemetry()
        readings[-1].update(rawManagedBytes=-984152, managedBytes=None, managedEstimateStatus="unavailable-negative-runtime-value")
        self.report["memoryTelemetry"]["unavailableCurrentEstimates"] = 1
        result = self.result()
        self.assertTrue(result["passed"])
        self.assertEqual(1, len(result["warnings"]))

    def test_negative_current_estimate_cannot_be_claimed_usable(self):
        self.telemetry()[-1]["rawManagedBytes"] = -1
        self.assertFalse(self.result()["passed"])

    def test_negative_current_estimate_cannot_be_replaced_by_last_gc_live_bytes(self):
        readings = self.telemetry()
        readings[-1].update(rawManagedBytes=-1, managedBytes=900, managedEstimateStatus="unavailable-negative-runtime-value")
        self.report["memoryTelemetry"]["unavailableCurrentEstimates"] = 1
        self.assertFalse(self.result()["passed"])

    def test_last_gc_heap_fragmentation_and_commit_are_independently_checked(self):
        self.telemetry()[-1]["gc"]["totalCommittedBytes"] = 500
        self.assertFalse(self.result()["passed"])

    def test_generation_sums_cannot_hide_a_forged_heap_size(self):
        self.telemetry()[-1]["gc"]["generations"][0]["sizeAfterBytes"] += 1
        self.assertFalse(self.result()["passed"])

    def test_negative_generation_counter_is_rejected(self):
        self.telemetry()[-1]["gc"]["generations"][0]["sizeBeforeBytes"] = -1
        self.assertFalse(self.result()["passed"])

    def test_nonfinite_or_fractional_memory_counters_are_rejected(self):
        self.telemetry()[-1]["totalAllocatedBytes"] = float("nan")
        self.assertFalse(self.result()["passed"])

    def test_raw_memory_event_must_match_terminal_snapshot(self):
        self.telemetry()
        self.events[-1] = copy.deepcopy(self.events[-1]); self.events[-1]["value"]["privateBytes"] += 1
        self.assertFalse(self.result()["passed"])

    def test_missing_raw_memory_event_is_rejected(self):
        self.telemetry(); self.events.pop()
        self.assertFalse(self.result()["passed"])

    def test_duplicate_raw_memory_event_is_rejected(self):
        self.telemetry(); self.events.append(copy.deepcopy(self.events[-1]))
        self.assertFalse(self.result()["passed"])

    def test_memory_summary_cannot_hide_an_unavailable_estimate(self):
        self.telemetry(); self.report["memoryTelemetry"]["unavailableCurrentEstimates"] = 1
        self.assertFalse(self.result()["passed"])

    def test_gc_occurring_in_sample_is_recorded_without_equating_current_and_last_gc(self):
        readings = self.telemetry()
        readings[1]["gcCollections"][0] += 1; readings[1]["gcOccurredDuringSample"] = True
        self.assertTrue(self.result()["passed"])

    def test_unreported_gc_during_sample_is_rejected(self):
        self.telemetry()[1]["gcCollections"][0] += 1
        self.assertFalse(self.result()["passed"])

    def test_memory_counters_cannot_go_backwards(self):
        self.telemetry()[-1]["totalAllocatedBytes"] = 1
        self.assertFalse(self.result()["passed"])

    def test_invalid_last_gc_cross_check_is_not_hidden_by_valid_current_estimate(self):
        self.telemetry()[-1]["gc"]["fragmentedBytes"] = 1001
        self.assertFalse(self.result()["passed"])

    def test_v2_requires_actual_default_and_custom_scroll_cycle_records(self):
        self.telemetry()
        next(e for e in self.events if e["kind"] == "cycle-completed")["value"]["defaultFactoryScroll"] = False
        self.assertFalse(self.result()["passed"])

    def test_usable_heap_bytes_cannot_be_a_fractional_or_boolean_counter(self):
        self.telemetry()[-1]["managedBytes"] = 1100.0
        self.assertFalse(self.result()["passed"])

    def test_gc_snapshot_index_cannot_go_backwards(self):
        self.telemetry()[-1]["gc"]["index"] = 1
        self.assertFalse(self.result()["passed"])

    def test_baseline_memory_cannot_be_taken_inside_the_soak(self):
        self.telemetry()[0].update(ticks=1050, sampleEndTicks=1055)
        self.assertFalse(self.result()["passed"])

    def test_post_gc_memory_cannot_be_taken_before_soak_completion(self):
        self.telemetry()[-1].update(ticks=2080, sampleEndTicks=2085)
        self.assertFalse(self.result()["passed"])

    def test_protocol_cannot_be_downgraded_to_skip_v2_cross_checks(self):
        self.telemetry(); self.initial_protocol = self.report["protocol"]; self.report["protocol"] = "treedatagrid-native-render-v1"
        self.assertFalse(self.result()["passed"])

    def test_v2_ui_timing_must_match_raw_monotonic_ticks(self):
        self.telemetry()
        next(e for e in self.events if e["kind"] == "completed")["value"]["UiMs"] = 0.01
        self.assertFalse(self.result()["passed"])

    def test_v2_render_timing_must_match_raw_monotonic_ticks(self):
        self.telemetry()
        next(e for e in self.events if e["kind"] == "completed")["value"]["RenderCompletionMs"] = 0.01
        self.assertFalse(self.result()["passed"])

    def test_invalid_clock_frequency_is_rejected_without_a_division_error(self):
        self.telemetry(); self.frequency = 0
        self.assertFalse(self.result()["passed"])


if __name__ == "__main__":
    unittest.main()
