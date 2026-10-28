"""Check correlated native render observations; pilot runs cannot qualify by default."""
import argparse
import json
import math
import hashlib
import struct
from pathlib import Path

SCENARIOS = {"observer", "flat-100k", "auto-default", "auto-factory", "wide-tree", "navigation-edit"}


def automation_hash(items):
    digest = hashlib.sha256()
    for item in sorted(items, key=lambda i: i["Id"]):
        for key in ("Id", "Name", "Value"):
            value = item[key]
            if value is not None and not isinstance(value, str):
                raise ValueError("UIA identity/name/value must be strings")
            data = b"" if value is None else value.encode("utf-8")
            digest.update(struct.pack("<i", -1 if value is None else len(data)))
            digest.update(data)
        selected = item["Selected"]
        if selected is not None and type(selected) is not bool:
            raise ValueError("UIA selected state must be boolean or null")
        digest.update(bytes([2 if selected is None else int(selected)]))
    return digest.hexdigest().upper()


def verify_automation(report, events, completed, errors):
    env = report.get("environment", {})
    version = env.get("automationQueryVersion")
    queries = [e for e in events if e.get("kind", "").startswith("automation-query-")]
    if version is None:
        if queries: errors.append("Unversioned external automation queries")
        return
    if type(version) is not int or version != 1:
        errors.append("Invalid automation query version")
    mode = env.get("automationClientMode")
    if mode not in ("none", "external-windows-uia-v1"):
        errors.append("Invalid controlled automation mode")
        return
    enabled = mode == "external-windows-uia-v1"
    backend_version = env.get("automationClientBackendVersion")
    backend = env.get("automationClientBackend")
    explicit = backend_version == 1 and backend == "explicit-com"
    if backend_version is not None:
        if type(backend_version) is not int or backend_version != 1 or backend not in (("explicit-com", "managed-legacy") if enabled else ("none",)):
            errors.append("Invalid automation client backend version/kind")
        if report["options"].get("AutomationClientBackend") != (backend if enabled else "explicit-com"):
            errors.append("Automation client backend differs from runner options")
    elif backend is not None or "AutomationClientBackend" in report["options"]:
        errors.append("Unversioned automation client backend")
    workload_version = env.get("automationWorkloadVersion")
    workload = env.get("automationWorkload", "both" if enabled else "none")
    if workload_version is not None:
        if type(workload_version) is not int or workload_version != 1:
            errors.append("Invalid automation workload version")
        if workload not in (("query", "events", "both") if enabled else ("none",)):
            errors.append("Invalid automation workload group")
        if report["options"].get("AutomationWorkload") != (workload if enabled else "both"):
            errors.append("Automation workload differs from runner options")
    elif "automationWorkload" in env or "AutomationWorkload" in report["options"]:
        errors.append("Unversioned automation workload group")
    subscription_version = env.get("automationSubscriptionVersion")
    if subscription_version is not None and (type(subscription_version) is not int or subscription_version != 1 or not enabled):
        errors.append("Invalid fixture-scoped automation subscription version")
    if workload_version == 1 and ((subscription_version == 1) is not (enabled and workload != "query")):
        errors.append("Automation workload has the wrong subscription boundary")
    if enabled != bool(report["options"].get("AutomationClient")):
        errors.append("Automation mode differs from runner options")
    states = [e["value"] for e in events if e.get("kind") == "fixture-automation-state"]
    expected_fixtures = (6 * report["options"]["Samples"] if report["options"]["Mode"] == "performance"
                         else report["cycles"] + (1 if report["pilot"] else 10))
    ids = [s["fixtureId"] for s in states]
    if len(ids) != expected_fixtures or len(set(ids)) != len(ids) or any(s["peerPresent"] is not enabled for s in states):
        errors.append("Missing/duplicate fixture or wrong initial automation peer state")
    starts = [e for e in events if e.get("kind") == "automation-client-started"]
    exits = [e for e in events if e.get("kind") == "automation-client-exited"]
    if not enabled:
        if queries or starts or exits: errors.append("Unconfigured external UIA client activity")
        return
    if len(starts) != 1 or len(exits) != 1:
        errors.append("Missing external UIA client process boundaries")
        return
    started, exited = starts[0]["value"], exits[0]["value"]
    host, client = env["hostPid"], started["clientPid"]
    if any(type(n) is not int or n <= 0 for n in (host, client)) or host == client or started["hostPid"] != host:
        errors.append("UIA client must be a distinct process")
    requests = [e for e in queries if e["kind"] == "automation-query-request"]
    replies = [e for e in queries if e["kind"] == "automation-query-completed"]
    request_ids = [e["value"]["query"]["Id"] for e in requests]
    reply_ids = [e["value"]["reply"]["Id"] for e in replies]
    if request_ids != list(range(1, len(requests) + 1)) or reply_ids != request_ids:
        errors.append("Missing/duplicate/out-of-order external UIA query")
        return
    frames, attaches, retires = [], [], []
    event_counts = (0, 0)
    client_gen2 = 0
    owned_acquisitions = 0
    for request_event, reply_event in zip(requests, replies):
        request, response = request_event["value"], reply_event["value"]
        query, reply = request["query"], response["reply"]
        if explicit:
            owned_acquisitions = verify_ownership(reply.get("Ownership"), owned_acquisitions, False, errors)
        elif reply.get("Ownership") is not None:
            errors.append("Unconfigured explicit UIA ownership receipt")
        counts = (reply["StructureEvents"], reply["PropertyEvents"])
        if any(type(c) is not int or c < p for c, p in zip(counts, event_counts)):
            errors.append("UIA event counters are invalid/non-monotonic")
        event_counts = counts
        phase, frame_id = request["phase"], request["frameId"]
        reads_tree = not (workload == "events" and phase == "frame")
        if workload_version == 1:
            if query.get("ReadTree") is not reads_tree or reply.get("Workload") != workload:
                errors.append("UIA workload/read-tree receipt differs")
            # Preserve a negative runtime current estimate as unavailable, as on
            # the host. It is not substituted with a last-GC heap estimate.
            if type(reply.get("ClientManagedBytes")) is not int:
                errors.append("Missing raw client managed-memory estimate")
            count = reply.get("ClientGen2Collections")
            if type(count) is not int or count < client_gen2:
                errors.append("Invalid/non-monotonic client collection count")
            else: client_gen2 = count
            if workload == "query" and (counts != (0, 0) or reply.get("SubscriptionGridId") is not None):
                errors.append("Query-only UIA group subscribed to events")
        if subscription_version == 1 and reply.get("SubscriptionGridId") != (None if phase == "retired" else query["GridId"]):
            errors.append("UIA subscription belongs to a different fixture lifetime")
        if response["phase"] != phase or response["frameId"] != frame_id or phase not in ("attach", "frame", "retired"):
            errors.append("UIA phase/frame correlation differs")
        if any(reply[k] != query[k] for k in ("Id", "HostPid", "GridId", "Retired")) or query["HostPid"] != host or reply["ClientPid"] != client or reply["Error"] is not None:
            errors.append("External UIA reply identity/error is invalid")
        if reply["GridFound"] is not (phase != "retired") or query["Retired"] is not (phase == "retired"):
            errors.append("UIA current/retired grid visibility is invalid")
        expected, actual = request["expected"], reply["Items"]
        for items in (expected, actual):
            item_ids = [i["Id"] for i in items]
            if len(set(item_ids)) != len(item_ids) or any(not i.startswith(("TreeDataGrid.Row.", "TreeDataGrid.Cell.", "TreeDataGrid.Column.")) for i in item_ids):
                errors.append("UIA duplicate/unknown row/column identity")
        if expected != actual or request["expectedHash"] != automation_hash(expected) or reply["Hash"] != automation_hash(actual):
            errors.append("UIA raw identities/names/values/selections differ")
        if not response["start"] <= request_event["ticks"] <= reply["StartTicks"] <= reply["EndTicks"] <= response["end"] <= reply_event["ticks"]:
            errors.append("UIA query timestamp interval is invalid")
        if phase == "frame":
            frames.append(frame_id)
            done = completed[frame_id]
            if request_event["ticks"] < done["RenderedTicks"]:
                errors.append("UIA query overlaps timed native frame")
            counts = {kind: sum(i["Id"].startswith("TreeDataGrid." + kind + ".") for i in actual) for kind in ("Row", "Cell")}
            if reads_tree and (counts["Row"] != done["Snapshot"]["RealizedRows"] or counts["Cell"] != done["Snapshot"]["RealizedCells"]):
                errors.append("UIA tree differs from rendered viewport counts")
            if not reads_tree and (actual or expected):
                errors.append("Event-only UIA frame unexpectedly traversed the tree")
        elif phase == "attach": attaches.append(query["GridId"])
        else:
            retires.append(query["GridId"])
            if actual or expected: errors.append("UIA retired grid still exposes items")
        next_frame = completed.get(frame_id + 1)
        if next_frame and response["end"] > next_frame["StartTicks"]:
            errors.append("UIA query overlaps next native mutation")
    if sorted(frames) != sorted(completed) or attaches != ids or retires != ids:
        errors.append("External UIA client missed a frame or fixture lifetime")
    if subscription_version == 1 and workload != "events":
        replies_by_frame = {e["value"]["frameId"]: e["value"]["reply"] for e in replies if e["value"]["phase"] == "frame"}
        for grid_id in ids:
            fixture_replies = [e["value"] for e in replies if e["value"]["reply"]["GridId"] == grid_id]
            measured = [r for r in fixture_replies if r["phase"] == "frame" and not completed[r["frameId"]]["Warmup"]]
            if not measured: continue
            if report["options"]["Mode"] == "soak" or completed[measured[0]["frameId"]]["Scenario"] == "navigation-edit":
                first_id = measured[0]["frameId"]
                preceding = replies_by_frame.get(first_id - 1) if report["options"]["Mode"] == "performance" else fixture_replies[0]["reply"]
                if preceding is None or measured[-1]["reply"]["PropertyEvents"] <= preceding["PropertyEvents"]:
                    errors.append("UIA property events were not delivered within the measured fixture")
    if exited["clientPid"] != client or exited["exitCode"] != 0 or exited["stderr"] or exited["queries"] != len(requests):
        errors.append("External UIA client exit/count is invalid")
    shutdown = json.loads(exited.get("shutdown") or "null")
    if not isinstance(shutdown, dict) or any(shutdown.get(k) != v for k, v in {"Shutdown": True, "ClientPid": client, "EventHandlersRemoved": True}.items()):
        errors.append("External UIA client did not record its managed-handle shutdown drain")
    elif any(type(shutdown.get(k)) is not int or shutdown[k] < p for k, p in zip(("StructureEvents", "PropertyEvents"), event_counts)):
        errors.append("External UIA client shutdown event counters are invalid")
    elif (workload == "query" and (shutdown["StructureEvents"], shutdown["PropertyEvents"]) != (0, 0)) or (workload == "events" and shutdown["StructureEvents"] <= 0) or (workload == "both" and min(shutdown["StructureEvents"], shutdown["PropertyEvents"]) <= 0):
        errors.append("External UIA client did not observe required structure/property events")
    if isinstance(shutdown, dict):
        if explicit:
            if shutdown.get("ManagedHandleDrain") is not False or shutdown.get("ExplicitComReferenceDrain") is not True:
                errors.append("Explicit UIA client used the wrong shutdown drain")
            verify_ownership(shutdown.get("Ownership"), owned_acquisitions, True, errors)
        elif shutdown.get("ManagedHandleDrain") is not True or shutdown.get("ExplicitComReferenceDrain") is True:
            errors.append("Legacy UIA client did not record its managed-handle drain")
    if not replies or exits[0]["ticks"] < replies[-1]["ticks"]:
        errors.append("External UIA client exited before its final reply")
    if report["options"]["Mode"] == "soak":
        ends = [e["ticks"] for e in events if e.get("kind") == "soak-end"]
        if len(ends) != 1 or exits[0]["ticks"] <= ends[0]:
            errors.append("External UIA shutdown/drain occurred inside the measured soak")


def verify_ownership(receipt, previous, shutdown, errors):
    keys = ("Version", "Acquired", "Released", "LiveQueryReferences", "ClientLifetimeReferences", "FinalizerReleases", "HandlerObjects")
    if not isinstance(receipt, dict) or any(type(receipt.get(k)) is not int or receipt[k] < 0 for k in keys):
        errors.append("Missing/invalid native UIA ownership receipt")
        return previous
    expected_lifetime, expected_handlers = (0, 0) if shutdown else (3, 2)
    if (receipt["Version"] != 1 or receipt["LiveQueryReferences"] != 0 or receipt["Acquired"] != receipt["Released"]
            or receipt["FinalizerReleases"] != 0 or receipt["ClientLifetimeReferences"] != expected_lifetime or receipt["HandlerObjects"] != expected_handlers
            or (receipt["Acquired"] != previous if shutdown else receipt["Acquired"] <= previous)):
        errors.append("Native UIA references were leaked, unbalanced, finalized or out of order")
    return receipt["Acquired"]


def verify_provider_shutdown(report, events, errors):
    enabled = report["options"].get("ShutdownAutomationProviders", False)
    if type(enabled) is not bool: errors.append("Invalid provider-shutdown option")
    calls = [e for e in events if e.get("kind") == "automation-providers-shutdown"]
    before = [e for e in events if e.get("kind") == "automation-provider-retention-before-shutdown"]
    marker = report.get("environment", {}).get("automationShutdownVersion")
    if not enabled:
        if calls or before or marker is not None or report.get("automationRetirement") is not None:
            errors.append("Unconfigured native provider shutdown")
        return
    if report["options"]["Mode"] != "soak" or report["environment"].get("automationClientMode") != "external-windows-uia-v1" or type(marker) is not int or marker != 1:
        errors.append("Invalid native provider shutdown scope/version")
    if len(calls) != 1 or len(before) != 1:
        errors.append("Missing/duplicate native provider shutdown boundaries")
        return
    call, prior = calls[0], before[0]
    value = call["value"]
    if value["hostPid"] != report["environment"]["hostPid"] or type(value["hresult"]) is not int or value["hresult"] != 0 or type(value["inSendMessage"]) is not int or value["inSendMessage"] != 0:
        errors.append("Provider shutdown failed or ran inside SendMessage/wrong process")
    ends = [e["ticks"] for e in events if e.get("kind") == "soak-end"]
    exits = [e["ticks"] for e in events if e.get("kind") == "automation-client-exited"]
    pre_gc = [e["ticks"] for e in events if e.get("kind") == "forced-gc" and e["value"]["phase"] == "post-soak-drain"]
    post_gc = [e["ticks"] for e in events if e.get("kind") == "forced-gc" and e["value"]["phase"] == "post-automation-provider-shutdown"]
    if len(ends) != 1 or len(exits) != 1 or len(pre_gc) != 1 or len(post_gc) != 1 or not ends[0] < exits[0] < pre_gc[0] <= prior["ticks"] <= value["started"] <= call["ticks"] < post_gc[0]:
        errors.append("Provider shutdown/GC occurred before the workload/client ended")
    retirement = report.get("automationRetirement") or {}
    if retirement.get("beforeProviderShutdown") != prior["value"]:
        errors.append("Pre-shutdown retention was missing or rewritten")
    previous = {i["kind"]: i for i in prior["value"]}
    final = {i["kind"]: i for i in report["retention"]}
    if previous.keys() != final.keys() or any(previous[k]["tracked"] != final[k]["tracked"] or previous[k]["collected"] > final[k]["collected"]
        or previous[k]["retained"] != previous[k]["tracked"] - previous[k]["collected"] for k in previous.keys() & final.keys()):
        errors.append("Provider shutdown altered tracking counts or hid pre-shutdown objects")


def verify_gc_reference_kind(report, readings, errors, warnings):
    blocking = report["options"].get("BlockingGcReference", False)
    if type(blocking) is not bool:
        errors.append("Invalid blocking-GC reference option")
    any_inconsistent = 0
    for reading in readings:
        if blocking and (reading.get("gcSnapshotKind") != "FullBlocking" or reading["gc"]["generation"] != 2 or reading["gc"].get("concurrent") is not False):
            errors.append("Required FullBlocking GC reference was not recorded")
        if "lastAnyGc" not in reading:
            if blocking: errors.append("Raw Any GC snapshot is missing beside the blocking reference")
            continue
        gc = reading["lastAnyGc"]; generations = gc["generations"]
        scalar_keys = ("index", "heapSizeBytes", "fragmentedBytes", "liveBytes", "totalCommittedBytes")
        if any(type(gc[k]) is not int for k in scalar_keys) or any(type(v) is not int for g in generations for v in g.values()):
            errors.append("Raw Any GC sizes must be integers")
        if gc["liveBytes"] != gc["heapSizeBytes"] - gc["fragmentedBytes"]:
            errors.append("Raw Any GC derived live value was altered")
        expected = []
        if gc["index"] <= 0 or gc["heapSizeBytes"] < 0 or gc["fragmentedBytes"] < 0 or gc["fragmentedBytes"] > gc["heapSizeBytes"] or gc["totalCommittedBytes"] < gc["heapSizeBytes"]:
            expected.append("Invalid last-GC heap/fragmentation/commit values.")
        if len(generations) != 5 or any(min(g.values()) < 0 or g["fragmentationBeforeBytes"] > g["sizeBeforeBytes"] or g["fragmentationAfterBytes"] > g["sizeAfterBytes"] for g in generations):
            expected.append("Invalid generation size/fragmentation values.")
        if sum(g["sizeAfterBytes"] for g in generations) != gc["heapSizeBytes"] or sum(g["fragmentationAfterBytes"] for g in generations) != gc["fragmentedBytes"]:
            expected.append("Last-GC generation sums differ from the heap snapshot.")
        if reading["lastAnyGcCrossCheckErrors"] != expected:
            errors.append("Raw Any GC consistency status differs from its recorded values")
        if not blocking and (reading.get("gcSnapshotKind") != "Any" or gc != reading["gc"]):
            errors.append("Any GC reference was silently replaced")
        if expected:
            any_inconsistent += 1
            warnings.append(f"Raw Any GC snapshot internally inconsistent at sample {reading['id']}; no usable Any-heap conclusion")
    quality = report["memoryTelemetry"]
    if blocking or "requiredGcSnapshotKind" in quality:
        if quality.get("requiredGcSnapshotKind") != ("FullBlocking" if blocking else "Any") or quality.get("inconsistentLastAnySnapshots") != any_inconsistent:
            errors.append("GC reference kind/Any consistency summary differs from raw snapshots")


def verify_memory(report, events, errors, warnings):
    """v2: recompute validity, then match terminal fields to append-only samples."""
    raw = [e for e in events if e.get("kind") == "resource-snapshot"]
    expected = [report["baselineMemory"]] + [m["resources"] for m in report["memory"]] + [report["drift"]["finalAfterGc"]]
    if len(raw) != len(expected) or not raw:
        errors.append("Missing/extra raw memory snapshots")
    by_id = {e["value"]["id"]: e for e in raw}
    if len(by_id) != len(raw) or sorted(by_id) != list(range(1, len(raw) + 1)):
        errors.append("Duplicate/nonsequential memory snapshot ids")
    unavailable = 0
    for reading in expected:
        event = by_id.get(reading["id"])
        if event is None or event["value"] != reading:
            errors.append("Terminal memory differs from raw snapshot")
        if reading["ticks"] <= 0 or reading["sampleEndTicks"] < reading["ticks"] or event and event["ticks"] < reading["sampleEndTicks"]:
            errors.append("Invalid memory sample timestamp window")
        value = reading["rawManagedBytes"]
        if type(value) is not int:
            errors.append("Raw managed heap estimate must be an integer")
        elif value < 0:
            unavailable += 1
            if reading["managedBytes"] is not None or reading["managedEstimateStatus"] != "unavailable-negative-runtime-value":
                errors.append("Negative managed estimate was silently accepted/replaced")
            warnings.append(f"Current managed estimate unavailable at snapshot {reading['id']}: {value}")
        elif reading["managedBytes"] != value or reading["managedEstimateStatus"] != "available":
            errors.append("Managed heap estimate availability/value mismatch")
        gc = reading["gc"]
        if type(reading["id"]) is not int or type(gc["index"]) is not int or type(gc["generation"]) is not int or reading["managedBytes"] is not None and type(reading["managedBytes"]) is not int:
            errors.append("Memory ids and usable byte values must be integers")
        byte_keys = ("heapSizeBytes", "fragmentedBytes", "liveBytes", "totalCommittedBytes")
        if any(type(gc[k]) is not int for k in byte_keys) or any(type(reading[k]) is not int for k in ("ticks", "sampleEndTicks", "privateBytes", "workingSetBytes", "handles", "processPeakWorkingSetBytes", "totalAllocatedBytes")):
            errors.append("Memory sizes/counters must be finite integers")
        if any(type(v) is not int for g in gc["generations"] for v in g.values()):
            errors.append("Generation memory counters must be integers")
        if gc["index"] <= 0 or gc["generation"] not in (0, 1, 2) or min(gc["heapSizeBytes"], gc["fragmentedBytes"], gc["liveBytes"], gc["totalCommittedBytes"]) < 0:
            errors.append("Invalid last-GC memory values")
        if gc["fragmentedBytes"] > gc["heapSizeBytes"] or gc["liveBytes"] != gc["heapSizeBytes"] - gc["fragmentedBytes"] or gc["totalCommittedBytes"] < gc["heapSizeBytes"]:
            errors.append("Inconsistent last-GC heap/fragmentation/commit")
        generations = gc["generations"]
        if len(generations) != 5 or any(min(g.values()) < 0 or g["fragmentationBeforeBytes"] > g["sizeBeforeBytes"] or g["fragmentationAfterBytes"] > g["sizeAfterBytes"] for g in generations):
            errors.append("Invalid generation memory values")
        if sum(g["sizeAfterBytes"] for g in generations) != gc["heapSizeBytes"] or sum(g["fragmentationAfterBytes"] for g in generations) != gc["fragmentedBytes"]:
            errors.append("Generation sums differ from last-GC heap")
        before, after = reading["gcCollectionsBefore"], reading["gcCollections"]
        if len(before) != 3 or len(after) != 3 or any(type(a) is not int or type(b) is not int or a < 0 or b < a for a, b in zip(before, after)) or reading["gcOccurredDuringSample"] is not (before != after):
            errors.append("Invalid GC sample window/counts")
        if min(reading[k] for k in ("privateBytes", "workingSetBytes", "handles", "processPeakWorkingSetBytes")) <= 0 or reading["totalAllocatedBytes"] < 0 or type(reading["cpuMilliseconds"]) not in (int, float) or not math.isfinite(reading["cpuMilliseconds"]) or reading["cpuMilliseconds"] < 0:
            errors.append("Invalid process resource counters")
        if reading["crossChecksValid"] is not True or reading["crossCheckErrors"]:
            errors.append("Memory sample cross-check did not pass")
    if expected[0]["phase"] != "baseline-after-gc" or expected[-1]["phase"] != "after-final-gc" or expected[-2]["phase"] != "steady-final" or any(r["phase"] != "steady" for r in expected[1:-2]):
        errors.append("Missing memory baseline/steady/final phases")
    starts = [e["ticks"] for e in events if e.get("kind") == "soak-start"]
    ends = [e["ticks"] for e in events if e.get("kind") == "soak-end"]
    if len(starts) != 1 or len(ends) != 1 or expected[0]["sampleEndTicks"] > starts[0] or expected[-1]["ticks"] < ends[0] or any(r["ticks"] < starts[0] or r["sampleEndTicks"] > ends[0] for r in expected[1:-1]):
        errors.append("Memory phases fall outside their raw soak interval")
    if any(b["ticks"] < a["sampleEndTicks"] or b["totalAllocatedBytes"] < a["totalAllocatedBytes"] or b["gc"]["index"] < a["gc"]["index"]
           or b["cpuMilliseconds"] < a["cpuMilliseconds"] or any(y < x for x, y in zip(a["gcCollections"], b["gcCollectionsBefore"])) for a, b in zip(expected, expected[1:])):
        errors.append("Nonmonotonic memory samples/counters")
    quality = report["memoryTelemetry"]
    if quality["snapshots"] != len(expected) or quality["unavailableCurrentEstimates"] != unavailable or quality["allCrossChecksValid"] is not True:
        errors.append("Memory telemetry summary differs from raw samples")
    verify_gc_reference_kind(report, expected, errors, warnings)


def verify(directory, allow_pilot=False):
    errors = []
    warnings = []
    try:
        report = json.loads((directory / "report.json").read_text(encoding="utf-8-sig"))
        initial = json.loads((directory / "initial.json").read_text(encoding="utf-8-sig"))
        if type(initial.get("stopwatchFrequency")) is not int or initial["stopwatchFrequency"] <= 0:
            raise ValueError("Invalid monotonic clock frequency")
        if initial.get("protocol") != report.get("protocol"):
            errors.append("Initial/terminal protocol mismatch or downgrade")
        if report.get("protocol") not in {"treedatagrid-native-render-v1", "treedatagrid-native-render-v2"} or report.get("evidenceKind") != "NativeMeasured":
            errors.append("Wrong native measurement protocol")
        if report.get("passed") is not True or report.get("error") or not report.get("checks") or not all(value is True for value in report["checks"].values()):
            errors.append("Native report/checks did not pass")
        if report.get("pilot") and not allow_pilot:
            errors.append("Pilot evidence cannot qualify as a full run")
        for key in ("nativePresentTicks", "gpuTicks", "physicalInputLatencyMs"):
            if report.get(key) is not None:
                errors.append("This protocol does not measure " + key)
        rows = [json.loads(line) for line in (directory / "events.jsonl").read_text(encoding="utf-8-sig").splitlines()]
        requests, applied, completed = {}, {}, {}
        for event in rows:
            kind = event.get("kind")
            if kind not in {"request", "applied", "completed"}:
                continue
            value = event["value"]
            identity = value["Id"] if kind == "completed" else value["id"]
            target = {"request": requests, "applied": applied, "completed": completed}[kind]
            if identity in target:
                errors.append(f"Duplicate {kind}: {identity}")
            target[identity] = value
        if not requests or requests.keys() != applied.keys() or requests.keys() != completed.keys():
            errors.append("Missing/open native request, applied record, or render completion")
        if report.get("requests") != len(requests) or report.get("completed") != len(completed):
            errors.append("Native request counts differ from raw events")
        for identity in requests.keys() & applied.keys() & completed.keys():
            req, app, done = requests[identity], applied[identity], completed[identity]
            if (req["scenario"], req["round"], req["step"], req["warmup"]) != (done["Scenario"], done["Round"], done["Step"], done["Warmup"]):
                errors.append(f"Wrong request correlation: {identity}")
            ticks = [done[key] for key in ("StartTicks", "LayoutEndTicks", "UiRenderTicks", "BatchRequestedTicks", "ProcessedTicks", "RenderedTicks")]
            if ticks != sorted(ticks) or ticks[0] <= 0 or ticks[-1] <= ticks[0]:
                errors.append(f"Invalid native render timestamps: {identity}")
            scene = done["Snapshot"]
            if app["start"] != ticks[0] or app["layoutEnd"] != ticks[1] or app["expectedRevision"] != scene["Revision"] or app["expectedVersion"] != scene["SourceVersion"] or app["projectionRows"] != scene["ProjectionRows"]:
                errors.append(f"Wrong rendered scene/source: {identity}")
            if min(scene["RealizedRows"], scene["RetainedRows"], scene["RealizedCells"]) <= 0 or done["UiMs"] < 0 or done["RenderCompletionMs"] <= 0:
                errors.append(f"Invalid native realization/timing: {identity}")
            if report["protocol"] == "treedatagrid-native-render-v2":
                frequency = initial["stopwatchFrequency"]
                if frequency <= 0 or not math.isclose(done["UiMs"], (ticks[1] - ticks[0]) * 1000 / frequency, abs_tol=0.0001) or not math.isclose(done["RenderCompletionMs"], (ticks[-1] - ticks[0]) * 1000 / frequency, abs_tol=0.0001):
                    errors.append(f"Reported timings differ from raw monotonic ticks: {identity}")
        options = report["options"]
        verify_automation(report, rows, completed, errors)
        verify_provider_shutdown(report, rows, errors)
        metric_version = report.get("environment", {}).get("containerMetricsVersion")
        if metric_version is not None and (type(metric_version) is not int or metric_version != 1):
            errors.append("Invalid native container metrics version")
        if type(metric_version) is int and metric_version == 1:
            counts = {}
            for done in completed.values():
                scene = done["Snapshot"]
                created = (scene.get("CreatedRows"), scene.get("CreatedCells"))
                if any(type(n) is not int for n in created) or created[0] < scene["RealizedRows"] or created[1] < scene["RealizedCells"]:
                    errors.append("Invalid native container creation counters")
                elif options["Mode"] == "performance":
                    key = (done["Scenario"], done["Round"])
                    previous = counts.get(key, (0, 0))
                    if any(n < p for n, p in zip(created, previous)):
                        errors.append("Native container counters decreased within one fixture")
                    counts[key] = created
        if "RefreshSourceDuringSoak" in options and type(options["RefreshSourceDuringSoak"]) is not bool:
            errors.append("Invalid same-source refresh option")
        if "AutoHeightDuringSoak" in options and type(options["AutoHeightDuringSoak"]) is not bool:
            errors.append("Invalid automatic-height soak option")
        required_checks = {"nativeWindowOnScreen", "allNativeRequestsCompleted"}
        required_checks |= {"sixScenariosAndCorrelatedRenderSamples"} if options["Mode"] == "performance" else {"retiredNativeObjectsCollected", "nativeSoakInvestigationGuards", "requestedSoakDuration"}
        if not required_checks.issubset(report.get("checks", {})):
            errors.append("Missing native execution checks")
        if options["Mode"] == "performance":
            scenarios = report.get("scenarios", [])
            if len(scenarios) != 6 or {item["scenario"] for item in scenarios} != SCENARIOS:
                errors.append("Missing native performance scenarios")
            for scenario in SCENARIOS:
                observed = [item for item in completed.values() if item["Scenario"] == scenario and not item["Warmup"]]
                if len(observed) != options["Samples"] * options["Frames"]:
                    errors.append(f"Incomplete measured samples: {scenario}")
                for round_index in range(options["Samples"]):
                    measured_steps = sorted(item["Step"] for item in observed if item["Round"] == round_index)
                    if measured_steps != list(range(options["Warmup"], options["Warmup"] + options["Frames"])):
                        errors.append(f"Incomplete round: {scenario}/{round_index}")
                    warmup_steps = sorted(item["Step"] for item in completed.values() if item["Scenario"] == scenario and item["Round"] == round_index and item["Warmup"])
                    if warmup_steps != list(range(options["Warmup"])):
                        errors.append(f"Incomplete warmup: {scenario}/{round_index}")
            if not report["pilot"] and (options["Samples"] < 5 or options["Frames"] < 30 or options["Warmup"] < 20):
                errors.append("Insufficient native performance repetitions")
        elif options["Mode"] == "soak":
            if options.get("AutoHeightDuringSoak"):
                detachments = [e for e in rows if e.get("kind") == "host-automation-detached"]
                expected_detachments = {i for i, d in completed.items() if d["Scenario"] == "soak" and d["Step"] == 7}
                detached_ids = [e["value"]["lastRequestId"] for e in detachments]
                if len(detached_ids) != len(expected_detachments) or set(detached_ids) != expected_detachments:
                    errors.append("Missing/duplicate configured host automation retirement boundary")
                for event in detachments:
                    value = event["value"]
                    if value.get("retiredGridStillChild") is not False or type(value.get("automationPeerPresent")) is not bool:
                        errors.append("Host automation cache did not release its retired fixture")
                    if value["lastRequestId"] not in completed or event["ticks"] < completed[value["lastRequestId"]]["RenderedTicks"]:
                        errors.append("Host automation retirement boundary precedes its completed cycle")
                for event in rows:
                    if event.get("kind") != "cycle-completed":
                        continue
                    cycle = event["value"]
                    frames = [d["Snapshot"] for d in completed.values() if d["Scenario"] == "soak" and d["Round"] == cycle["cycle"] and d["Warmup"] is cycle["warmup"]]
                    if len(frames) != 8 or cycle.get("autoRowHeight") is not True or any(f.get("AutoRowHeight") is not True for f in frames):
                        errors.append("Automatic-height soak missed its configured frame/cycle mode")
                    heights = [f.get("MaxRowHeight") for f in frames]
                    if not heights or any(type(h) not in (int, float) or not math.isfinite(h) or h < 28 for h in heights):
                        errors.append("Invalid automatic-height frame geometry")
                    elif max(heights) <= 28 or cycle.get("maxRowHeight") != max(heights):
                        errors.append("Automatic-height cycle did not measure/correlate a wrapped tall row")
            refreshes = [e for e in rows if e.get("kind") == "source-refresh"]
            if options.get("RefreshSourceDuringSoak"):
                expected_refreshes = {i for i, r in requests.items() if r["scenario"] == "soak" and r["step"] == 4}
                ids = [e["value"]["requestId"] for e in refreshes]
                if report["checks"].get("sameSourceRefreshEveryCycle") is not True or len(ids) != len(expected_refreshes) or set(ids) != expected_refreshes:
                    errors.append("Missing/duplicate configured same-source refresh")
                for event in refreshes:
                    value = event["value"]; request = requests[value["requestId"]]; done = completed[value["requestId"]]
                    if value["cycle"] != request["round"] or value["warmup"] is not request["warmup"] or value["defaultFactory"] is not (value["cycle"] % 2 == 0):
                        errors.append("Same-source refresh differs from its cycle/factory")
                    if type(value["versionBefore"]) is not int or type(value["versionAfter"]) is not int or value["versionBefore"] < 1 or value["versionAfter"] <= value["versionBefore"] or value["versionAfter"] != done["Snapshot"]["SourceVersion"]:
                        errors.append("Same-source refresh did not publish the rendered version")
                    if not done["StartTicks"] <= event["ticks"] <= done["LayoutEndTicks"]:
                        errors.append("Same-source refresh falls outside its UI request")
                if any(e["value"].get("sameSourceRefresh") is not True for e in rows if e.get("kind") == "cycle-completed"):
                    errors.append("Cycle did not execute its configured same-source refresh")
            elif refreshes:
                errors.append("Unconfigured same-source refresh events")
            if report["protocol"] == "treedatagrid-native-render-v2":
                if report["checks"].get("memoryTelemetryCrossChecks") is not True:
                    errors.append("Missing required memory telemetry execution check")
                if report["checks"].get("defaultAndCustomFactoryScrollCycles") is not True:
                    errors.append("Missing default/custom scroll lifecycle execution check")
                verify_memory(report, rows, errors, warnings)
            start = [item["ticks"] for item in rows if item.get("kind") == "soak-start"]
            end = [item["ticks"] for item in rows if item.get("kind") == "soak-end"]
            frequency = initial["stopwatchFrequency"]
            if len(start) != 1 or len(end) != 1 or end[0] <= start[0] or (end[0] - start[0]) / frequency < options["DurationSeconds"]:
                errors.append("Raw monotonic soak interval is incomplete")
            elif any(item.get("kind") == "forced-gc" and start[0] <= item["ticks"] <= end[0] for item in rows):
                errors.append("Forced GC occurred inside the measured native soak")
            if report.get("measuredSoakSeconds", 0) < options["DurationSeconds"] or report.get("cycles", 0) < 1:
                errors.append("Soak ended before requested duration/cycles")
            if not report["pilot"] and (options["DurationSeconds"] < 600 or len(report.get("memory", [])) < 30):
                errors.append("Insufficient native soak duration/checkpoints")
            retention = report.get("retention") or []
            if not retention or any(item["retained"] != 0 or item["collected"] != item["tracked"] for item in retention):
                errors.append("Retired native objects remain alive")
            if (report.get("drift") or {}).get("investigationRequired") is not False:
                errors.append("Native soak requires drift investigation")
            finished_cycles = [item["value"] for item in rows if item.get("kind") == "cycle-completed" and not item["value"]["warmup"]]
            if len(finished_cycles) != report["cycles"] or any(item["commits"] != 1 or item["nativeFrames"] != 8 for item in finished_cycles):
                errors.append("Native soak cycle/edit outcomes are incomplete")
            if report["protocol"] == "treedatagrid-native-render-v2" and (sorted(c["cycle"] for c in finished_cycles) != list(range(report["cycles"]))
                    or any(c["defaultFactoryScroll"] is not (c["cycle"] % 2 == 0) for c in finished_cycles)):
                errors.append("Default/custom scroll cycle coverage differs from raw protocol")
        else:
            errors.append("Unknown native mode")
    except (OSError, ValueError, KeyError, TypeError) as error:
        errors.append(str(error))
    return {"schemaVersion": 2, "passed": not errors, "errors": errors, "warnings": warnings}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--allow-pilot", action="store_true")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = verify(args.directory, args.allow_pilot)
    content = json.dumps(result, indent=2, ensure_ascii=False)
    if args.output:
        args.output.write_text(content + "\n", encoding="utf-8")
    print(content)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
