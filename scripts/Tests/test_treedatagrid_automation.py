import copy
import json
import importlib.util
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location("native_automation_guard", Path(__file__).resolve().parents[1] / "verify_treedatagrid_native.py")
GUARD = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GUARD)


class ControlledAutomationTests(unittest.TestCase):
    def setUp(self):
        self.report = {"environment": {"automationQueryVersion": 1, "automationClientMode": "external-windows-uia-v1", "hostPid": 101},
                       "options": {"Mode": "soak", "AutomationClient": "client.dll"}, "pilot": True, "cycles": 0}
        self.completed = {1: {"StartTicks": 10, "RenderedTicks": 20, "Snapshot": {"RealizedRows": 1, "RealizedCells": 1}}}
        self.items = [{"Id": "TreeDataGrid.Cell.1.1", "Name": "C0: 中文", "Value": "中文", "Selected": True},
                      {"Id": "TreeDataGrid.Row.1", "Name": "Row 1", "Value": None, "Selected": True}]
        self.events = [{"kind": "automation-client-started", "ticks": 1, "value": {"hostPid": 101, "clientPid": 202}},
                       {"kind": "fixture-automation-state", "ticks": 9, "value": {"fixtureId": "F1", "peerPresent": True}}]
        for identity, (phase, frame, start) in enumerate((("attach", 0, 2), ("frame", 1, 21), ("retired", 1, 30)), 1):
            items = [] if phase == "retired" else copy.deepcopy(self.items)
            query = {"Id": identity, "HostPid": 101, "GridId": "F1", "Retired": phase == "retired"}
            reply = {**query, "ClientPid": 202, "GridFound": phase != "retired", "Hash": GUARD.automation_hash(items), "Items": items,
                     "StartTicks": start + 2, "EndTicks": start + 3, "Error": None, "StructureEvents": identity, "PropertyEvents": identity}
            self.events += [{"kind": "automation-query-request", "ticks": start + 1,
                             "value": {"query": query, "frameId": frame, "phase": phase, "expectedHash": GUARD.automation_hash(items), "expected": copy.deepcopy(items)}},
                            {"kind": "automation-query-completed", "ticks": start + 5,
                             "value": {"frameId": frame, "phase": phase, "reply": reply, "start": start, "end": start + 4}}]
        self.events.append({"kind": "soak-end", "ticks": 39, "value": {}})
        self.events.append({"kind": "automation-client-exited", "ticks": 40, "value": {"clientPid": 202, "exitCode": 0, "stderr": "", "queries": 3,
                            "shutdown": json.dumps({"Shutdown": True, "ClientPid": 202, "ManagedHandleDrain": True, "EventHandlersRemoved": True, "StructureEvents": 3, "PropertyEvents": 3})}})

    def errors(self):
        result = []
        GUARD.verify_automation(self.report, self.events, self.completed, result)
        return result

    def reply(self, phase="frame"):
        return next(e["value"] for e in self.events if e["kind"] == "automation-query-completed" and e["value"]["phase"] == phase)

    def test_complete_external_readonly_bridge_queries(self): self.assertEqual([], self.errors())
    def test_duplicate_reply(self):
        duplicate = next(e for e in self.events if e["kind"] == "automation-query-completed")
        self.events.insert(-1, copy.deepcopy(duplicate)); self.assertTrue(self.errors())
    def test_missing_frame_query(self):
        self.events = [e for e in self.events if e["value"].get("phase") != "frame"]; self.assertTrue(self.errors())
    def test_wrong_host_process(self):
        self.reply()["reply"]["HostPid"] = 303; self.assertTrue(self.errors())
    def test_in_process_substitute(self):
        self.events[0]["value"]["clientPid"] = 101; self.assertTrue(self.errors())
    def test_query_inside_render_measurement(self):
        self.completed[1]["RenderedTicks"] = 28; self.assertTrue(self.errors())
    def test_query_overlaps_next_mutation(self):
        self.completed[2] = {"StartTicks": 24, "RenderedTicks": 28, "Snapshot": {"RealizedRows": 1, "RealizedCells": 1}}
        self.assertTrue(self.errors())
    def test_wrong_selection_with_forged_hash(self):
        reply = self.reply()["reply"]; reply["Items"][0]["Selected"] = False; reply["Hash"] = GUARD.automation_hash(reply["Items"])
        self.assertTrue(self.errors())
    def test_wrong_viewport_count(self):
        self.completed[1]["Snapshot"]["RealizedRows"] = 2; self.assertTrue(self.errors())
    def test_retired_grid_still_visible(self):
        self.reply("retired")["reply"]["GridFound"] = True; self.assertTrue(self.errors())
    def test_client_exception(self):
        self.reply()["reply"]["Error"] = "RPC failed"; self.assertTrue(self.errors())
    def test_wrong_initial_peer_state(self):
        self.events[1]["value"]["peerPresent"] = False; self.assertTrue(self.errors())
    def test_boolean_version(self):
        self.report["environment"]["automationQueryVersion"] = True; self.assertTrue(self.errors())
    def test_client_exit_count(self):
        self.events[-1]["value"]["queries"] = 2; self.assertTrue(self.errors())
    def test_unconfigured_client_activity(self):
        self.report["environment"]["automationClientMode"] = "none"; self.report["options"]["AutomationClient"] = None
        self.assertTrue(self.errors())
    def test_unversioned_client_activity(self):
        del self.report["environment"]["automationQueryVersion"]; self.assertTrue(self.errors())
    def test_current_and_null_values_have_distinct_hashes(self):
        original = GUARD.automation_hash(self.items); self.items[1]["Value"] = ""
        self.assertNotEqual(original, GUARD.automation_hash(self.items))
    def test_shutdown_before_measured_soak_end(self):
        self.events[-2]["ticks"] = 41; self.assertTrue(self.errors())
    def test_missing_client_handle_drain_receipt(self):
        del self.events[-1]["value"]["shutdown"]; self.assertTrue(self.errors())
    def test_missing_native_event_delivery(self):
        value = json.loads(self.events[-1]["value"]["shutdown"]); value["StructureEvents"] = 0
        self.events[-1]["value"]["shutdown"] = json.dumps(value); self.assertTrue(self.errors())
    def test_decreasing_event_count(self):
        self.reply()["reply"]["PropertyEvents"] = 0; self.assertTrue(self.errors())

    def scoped(self):
        self.report["environment"]["automationSubscriptionVersion"] = 1
        self.completed[1].update(Warmup=False, Scenario="soak")
        for event in self.events:
            if event["kind"] == "automation-query-completed":
                reply = event["value"]["reply"]
                reply["SubscriptionGridId"] = None if reply["Retired"] else reply["GridId"]

    def test_fixture_scoped_events(self):
        self.scoped(); self.assertEqual([], self.errors())
    def test_subscription_must_follow_current_fixture(self):
        self.scoped(); self.reply()["reply"]["SubscriptionGridId"] = "old"; self.assertTrue(self.errors())
    def test_measured_fixture_must_deliver_property_events(self):
        self.scoped(); self.reply()["reply"]["PropertyEvents"] = 1; self.assertTrue(self.errors())
    def test_subscription_version_is_not_a_boolean(self):
        self.scoped(); self.report["environment"]["automationSubscriptionVersion"] = True; self.assertTrue(self.errors())

    def workload(self, group):
        self.report["environment"].update(automationWorkloadVersion=1, automationWorkload=group)
        self.report["options"]["AutomationWorkload"] = group
        if group != "query": self.scoped()
        for event in self.events:
            if event["kind"] == "automation-query-request":
                value = event["value"]
                value["query"]["ReadTree"] = not (group == "events" and value["phase"] == "frame")
                if not value["query"]["ReadTree"]:
                    value["expected"] = []; value["expectedHash"] = GUARD.automation_hash([])
            elif event["kind"] == "automation-query-completed":
                value = event["value"]; reply = value["reply"]
                reply.update(Workload=group, ClientManagedBytes=100, ClientGen2Collections=0)
                if group == "query": reply.update(SubscriptionGridId=None, StructureEvents=0, PropertyEvents=0)
                if group == "events" and value["phase"] == "frame":
                    reply.update(Items=[], Hash=GUARD.automation_hash([]))
        shutdown = json.loads(self.events[-1]["value"]["shutdown"])
        if group == "query": shutdown.update(StructureEvents=0, PropertyEvents=0)
        self.events[-1]["value"]["shutdown"] = json.dumps(shutdown)

    def test_query_only_has_full_snapshots_without_subscriptions(self):
        self.workload("query"); self.assertEqual([], self.errors())
    def test_event_only_skips_frame_traversal_but_checks_lifetimes(self):
        self.workload("events"); self.assertEqual([], self.errors())
    def test_combined_group_retains_all_checks(self):
        self.workload("both"); self.assertEqual([], self.errors())
    def test_forged_workload_receipt(self):
        self.workload("both"); self.reply()["reply"]["Workload"] = "events"; self.assertTrue(self.errors())
    def test_query_only_cannot_skip_viewport_read(self):
        self.workload("query"); self.reply()["reply"]["Items"] = []; self.assertTrue(self.errors())
    def test_query_only_cannot_subscribe(self):
        self.workload("query"); self.reply()["reply"]["SubscriptionGridId"] = "F1"; self.assertTrue(self.errors())
    def test_event_only_cannot_claim_a_frame_traversal(self):
        self.workload("events")
        next(e["value"] for e in self.events if e["kind"] == "automation-query-request" and e["value"]["phase"] == "frame")["query"]["ReadTree"] = True
        self.assertTrue(self.errors())
    def test_workload_marker_is_required(self):
        self.workload("query"); del self.report["environment"]["automationWorkloadVersion"]; self.assertTrue(self.errors())
    def test_workload_marker_cannot_be_boolean(self):
        self.workload("query"); self.report["environment"]["automationWorkloadVersion"] = True; self.assertTrue(self.errors())
    def test_client_memory_estimate_must_be_raw_integer(self):
        self.workload("both"); self.reply()["reply"]["ClientManagedBytes"] = "100"; self.assertTrue(self.errors())
    def test_negative_client_estimate_is_preserved(self):
        self.workload("both"); self.reply()["reply"]["ClientManagedBytes"] = -1; self.assertEqual([], self.errors())
    def test_client_gc_count_must_be_monotonic(self):
        self.workload("both"); self.reply("attach")["reply"]["ClientGen2Collections"] = 1; self.assertTrue(self.errors())

    def explicit(self):
        self.workload("both")
        self.report["environment"].update(automationClientBackendVersion=1, automationClientBackend="explicit-com")
        self.report["options"]["AutomationClientBackend"] = "explicit-com"
        for event in self.events:
            if event["kind"] == "automation-query-completed":
                reply = event["value"]["reply"]
                reply["Ownership"] = dict(Version=1, Acquired=reply["Id"]*10, Released=reply["Id"]*10,
                    LiveQueryReferences=0, ClientLifetimeReferences=3, FinalizerReleases=0, HandlerObjects=2)
        shutdown = json.loads(self.events[-1]["value"]["shutdown"])
        shutdown.update(ManagedHandleDrain=False, ExplicitComReferenceDrain=True,
            Ownership=dict(Version=1, Acquired=30, Released=30, LiveQueryReferences=0, ClientLifetimeReferences=0, FinalizerReleases=0, HandlerObjects=0))
        self.events[-1]["value"]["shutdown"] = json.dumps(shutdown)

    def test_explicit_com_has_balanced_per_query_and_shutdown_receipts(self):
        self.explicit(); self.assertEqual([], self.errors())
    def test_explicit_com_cannot_omit_ownership(self):
        self.explicit(); del self.reply()["reply"]["Ownership"]; self.assertTrue(self.errors())
    def test_explicit_com_query_cannot_leave_live_references(self):
        self.explicit(); self.reply()["reply"]["Ownership"]["LiveQueryReferences"] = 1; self.assertTrue(self.errors())
    def test_explicit_com_acquisition_must_balance_release(self):
        self.explicit(); self.reply()["reply"]["Ownership"]["Released"] -= 1; self.assertTrue(self.errors())
    def test_explicit_com_receipts_must_advance(self):
        self.explicit(); self.reply()["reply"]["Ownership"].update(Acquired=10, Released=10); self.assertTrue(self.errors())
    def test_explicit_com_cannot_depend_on_finalizers(self):
        self.explicit(); self.reply()["reply"]["Ownership"]["FinalizerReleases"] = 1; self.assertTrue(self.errors())
    def test_explicit_com_must_keep_client_lifetime_references_until_shutdown(self):
        self.explicit(); self.reply()["reply"]["Ownership"]["ClientLifetimeReferences"] = 0; self.assertTrue(self.errors())
    def test_explicit_com_must_keep_handlers_alive(self):
        self.explicit(); self.reply()["reply"]["Ownership"]["HandlerObjects"] = 0; self.assertTrue(self.errors())
    def test_explicit_com_ownership_version_is_not_boolean(self):
        self.explicit(); self.reply()["reply"]["Ownership"]["Version"] = True; self.assertTrue(self.errors())
    def test_explicit_com_counter_is_not_a_float(self):
        self.explicit(); self.reply()["reply"]["Ownership"]["Acquired"] = 20.0; self.assertTrue(self.errors())
    def test_explicit_com_shutdown_must_release_client_references(self):
        self.explicit(); shutdown=json.loads(self.events[-1]["value"]["shutdown"]); shutdown["Ownership"]["ClientLifetimeReferences"] = 1
        self.events[-1]["value"]["shutdown"] = json.dumps(shutdown); self.assertTrue(self.errors())
    def test_explicit_com_shutdown_cannot_use_managed_drain_receipt(self):
        self.explicit(); shutdown=json.loads(self.events[-1]["value"]["shutdown"]); shutdown["ManagedHandleDrain"] = True
        self.events[-1]["value"]["shutdown"] = json.dumps(shutdown); self.assertTrue(self.errors())
    def test_explicit_com_backend_is_versioned(self):
        self.explicit(); self.report["environment"]["automationClientBackendVersion"] = True; self.assertTrue(self.errors())
    def test_explicit_com_backend_matches_options(self):
        self.explicit(); self.report["options"]["AutomationClientBackend"] = "managed-legacy"; self.assertTrue(self.errors())
    def test_explicit_com_cannot_skip_live_pattern_values(self):
        self.explicit(); self.reply()["reply"]["Items"][0]["Value"] = None; self.assertTrue(self.errors())


class ProviderShutdownTests(unittest.TestCase):
    def setUp(self):
        prior = [{"kind": "grids", "tracked": 1, "collected": 0, "retained": 1}]
        self.report = {"options": {"Mode": "soak", "ShutdownAutomationProviders": True},
                       "environment": {"automationClientMode": "external-windows-uia-v1", "automationShutdownVersion": 1, "hostPid": 101},
                       "automationRetirement": {"beforeProviderShutdown": copy.deepcopy(prior)},
                       "retention": [{"kind": "grids", "tracked": 1, "collected": 1, "retained": 0}]}
        self.events = [{"kind": "soak-end", "ticks": 10}, {"kind": "automation-client-exited", "ticks": 20},
                       {"kind": "forced-gc", "ticks": 30, "value": {"phase": "post-soak-drain"}},
                       {"kind": "automation-provider-retention-before-shutdown", "ticks": 40, "value": prior},
                       {"kind": "automation-providers-shutdown", "ticks": 50, "value": {"hostPid": 101, "started": 45, "hresult": 0, "inSendMessage": 0}},
                       {"kind": "forced-gc", "ticks": 60, "value": {"phase": "post-automation-provider-shutdown"}}]

    def errors(self):
        result = []; GUARD.verify_provider_shutdown(self.report, self.events, result); return result
    def test_explicit_post_workload_shutdown_preserves_prior_retention(self): self.assertEqual([], self.errors())
    def test_unconfigured_shutdown(self):
        self.report["options"]["ShutdownAutomationProviders"] = False; self.assertTrue(self.errors())
    def test_failed_shutdown(self):
        self.events[-2]["value"]["hresult"] = -1; self.assertTrue(self.errors())
    def test_inside_send_message(self):
        self.events[-2]["value"]["inSendMessage"] = 1; self.assertTrue(self.errors())
    def test_shutdown_before_client_exit(self):
        self.events[1]["ticks"] = 55; self.assertTrue(self.errors())
    def test_pre_shutdown_retention_cannot_be_rewritten(self):
        self.report["automationRetirement"]["beforeProviderShutdown"][0]["retained"] = 0; self.assertTrue(self.errors())
    def test_tracking_cannot_be_discarded(self):
        self.report["retention"][0]["tracked"] = 0; self.assertTrue(self.errors())
    def test_boolean_shutdown_version(self):
        self.report["environment"]["automationShutdownVersion"] = True; self.assertTrue(self.errors())


if __name__ == "__main__": unittest.main()
