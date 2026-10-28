"""Independent matched-process sampled-memory evidence checks.

Each pair keeps empty/full totals and their signed same-kind difference. No
processes are pooled, and neither native bytes nor instantaneous peak is inferred.
"""
from __future__ import annotations

import argparse
import datetime as dt
from functools import lru_cache
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys

SCHEMA = "charts-memory-oracle/v1"
IDENTITY = ("runToken", "processId", "processStartedUtc", "mode", "windowsCreationFileTimeUtc", "linuxProcStartTicks", "linuxBootId")
PHASES = ["generation", "sourceApply", "prepare", "record", "controlledRaster", "hit"]
MANAGED_PHASES = ["before-generation"] + ["after-" + name for name in PHASES] + ["after-work"]
ORDER = [(1, 1, "empty"), (1, 2, "full"), (2, 1, "full"), (2, 2, "empty"), (3, 1, "empty"), (3, 2, "full")]
EXPECTED_DEPENDENCIES = {"Charts.MemoryProbe.dll", "Charts.MemoryProbe.runtimeconfig.json", "Charts.MemoryProbe.deps.json", "NT.AvaloniaControl.Charts.dll", "NT.AvaloniaControl.Charts.Skia.dll", "NT.AvaloniaControl.Charts.Themes.dll", "Avalonia.Base.dll", "Avalonia.Skia.dll", "SkiaSharp.dll", "HarfBuzzSharp.dll"}


def require(value, message):
    if not value:
        raise ValueError(message)


def load(path):
    path = Path(path)
    require(path.is_file() and path.stat().st_size <= 128 * 1024 * 1024, "Missing/oversized evidence " + str(path))
    def pairs(items):
        result = {}
        for name, value in items:
            require(name not in result, "Duplicate JSON key " + name)
            result[name] = value
        return result
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=pairs,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError("Nonfinite JSON " + value)))


def integer(value, name, minimum=0):
    require(type(value) is int and value >= minimum, name + " must be an integer >= " + str(minimum))
    return value


def fingerprint(path):
    with Path(path).open("rb") as stream:
        return {"path": str(Path(path).resolve()), "bytes": Path(path).stat().st_size, "sha256": hashlib.file_digest(stream, "sha256").hexdigest().upper()}


def bounded(root, value, directory=False):
    require(isinstance(value, str) and value and not Path(value).is_absolute() and ":" not in value and "\0" not in value, "Campaign paths must be relative")
    path = (root / value).resolve(strict=True)
    require(path.is_relative_to(root.resolve()) and (path.is_dir() if directory else path.is_file()), "Evidence escapes/misses campaign")
    return path


def complete_utc_filetime(value):
    match = re.fullmatch(r"(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})\.(\d{7})(?:Z|\+00:00)", value)
    require(match is not None, "Complete process UTC start was truncated")
    year, month, day, hour, minute, second = map(int, match.groups()[:6])
    require(hour < 24 and minute < 60 and second < 60, "Process UTC timestamp range")
    return (((dt.date(year, month, day).toordinal() - dt.date(1601, 1, 1).toordinal()) * 24 + hour) * 3600 + minute * 60 + second) * 10_000_000 + int(match[7])


def identity(envelope):
    require(envelope["schema"] == "charts-memory-probe/v1", "Host schema")
    result = {key: envelope[key] for key in IDENTITY}
    require(re.fullmatch(r"[A-Fa-f0-9]{32}", result["runToken"]) is not None, "Nonce token")
    integer(result["processId"], "PID", 1)
    filetime = complete_utc_filetime(result["processStartedUtc"])
    if result["windowsCreationFileTimeUtc"] is not None:
        require(result["windowsCreationFileTimeUtc"] == str(filetime) and result["linuxProcStartTicks"] is None and result["linuxBootId"] is None, "Windows full FILETIME and UTC start disagree")
    else:
        integer(result["linuxProcStartTicks"], "Linux process start jiffies")
        require(re.fullmatch(r"[a-fA-F0-9]{8}(?:-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12}", result["linuxBootId"] or "") is not None, "Linux boot identity missing")
    require(result["mode"] in ("empty", "full"), "Unknown mode")
    return result


def native_process_key(value):
    if value["windowsCreationFileTimeUtc"] is not None:
        return (value["processId"], value["windowsCreationFileTimeUtc"])
    return (value["processId"], value["linuxProcStartTicks"], value["linuxBootId"])


def validate_host_receipt(expected, receipt, platform, frequency=None):
    """UTC conversions are observations; exact native identity proves ownership.

    A Linux parent's .NET StartTime conversion need not equal the child's .NET
    conversion. Preserve both verbatim; never round or rewrite the parent value.
    """
    require(receipt["schemaVersion"] == "charts-memory-process-v2" and receipt["hostReadyIdentityValidated"] is True and receipt["processId"] == expected["processId"], "Host native-ready receipt was not validated for this PID")
    parent = receipt["parentObservedProcessStartedUtc"]
    require(receipt["processStartedUtc"] == parent, "Parent UTC receipt was rewritten into the child canonical UTC")
    complete_utc_filetime(parent)
    require(receipt["canonicalProcessStartedUtc"] == expected["processStartedUtc"], "Canonical UTC differs from exact ready-file text")
    fields = {"processId", "windowsCreationFileTimeUtc", "linuxProcStartTicks", "linuxBootId", "readStartedUtc", "readFinishedUtc", "readStartQpc", "readEndQpc", "qpcFrequency"}
    for name in ("nativeIdentityAtLaunch", "nativeIdentityAtReady"):
        observed = receipt[name]
        require(isinstance(observed, dict) and set(observed) == fields, "Missing/changed native receipt identity schema")
        require(all(observed[key] == expected[key] for key in ("processId", "windowsCreationFileTimeUtc", "linuxProcStartTicks", "linuxBootId")), "Native launch/ready PID/start/boot proof differs")
        require(complete_utc_filetime(observed["readStartedUtc"]) <= complete_utc_filetime(observed["readFinishedUtc"]), "Native identity read timestamps reversed")
        require(integer(observed["readStartQpc"], "native identity read QPC") <= integer(observed["readEndQpc"], "native identity end QPC"), "Native identity QPC timestamps reversed")
        integer(observed["qpcFrequency"], "native identity QPC frequency", 1)
        if frequency is not None:
            require(observed["qpcFrequency"] == frequency, "Native identity and host QPC frequency differ")
    require(receipt["nativeIdentityAtLaunch"]["qpcFrequency"] == receipt["nativeIdentityAtReady"]["qpcFrequency"] and receipt["nativeIdentityAtLaunch"]["readEndQpc"] <= receipt["nativeIdentityAtReady"]["readStartQpc"], "Native launch/ready read order or frequency changed")
    if platform == "windows":
        require(parent == expected["processStartedUtc"] and str(complete_utc_filetime(parent)) == expected["windowsCreationFileTimeUtc"], "Windows complete creation FILETIME/UTC must match exactly")
    else:
        require(platform == "linux" and expected["windowsCreationFileTimeUtc"] is None, "Native identity platform differs")
    return {"method": "exact-windows-creation-filetime" if platform == "windows" else "exact-linux-pid-proc-start-jiffies-boot-id",
            "parentObservedProcessStartedUtc": parent, "canonicalProcessStartedUtc": expected["processStartedUtc"],
            "utcConversionsEqual": parent == expected["processStartedUtc"],
            "nativeIdentityAtLaunch": receipt["nativeIdentityAtLaunch"], "nativeIdentityAtReady": receipt["nativeIdentityAtReady"],
            "qualification": "Parent and child UTC strings remain independent observations. Only exact native identity permits canonical child UTC for the handshake; no rounding or tolerance."}


def same_identity(expected, envelope):
    require(identity(envelope) == expected, "Host nonce/PID/full start identity differs")


def control_identity(expected, value, status):
    require(all(value.get(key) == expected[key] for key in ("runToken", "processId", "processStartedUtc")) and value.get("status") == status, "Control identity/status mismatch")


def verify_reference(value, path):
    observed = fingerprint(path)
    require(value["bytes"] == observed["bytes"] and value["sha256"] == observed["sha256"], "File reference fingerprint mismatch")


@lru_cache(maxsize=4)
def expected_dataset(count=1_000_000):
    """Independent streaming C0 generation/hash; does not allocate source arrays."""
    state, mask = 0x1020304050607085, (1 << 64) - 1
    digest = hashlib.sha256()
    for index in range(count):
        state ^= state >> 12; state ^= (state << 25) & mask; state ^= state >> 27; state &= mask
        random = (state * 2685821657736338717) & mask
        wave = index % 1024; triangle = wave if wave < 512 else 1024 - wave
        y = (triangle * 32 + (random & 511) - 255) / 256.0
        if index % 997 == 431:
            y += 128
        digest.update(struct.pack("<dd", float(index), y))
    series = digest.hexdigest().upper()
    combined = "\n".join(["charts-c0-fixed-integer-xorshift64star-v1", "S1M-L", "1020304050607085", "0", series])
    return series, hashlib.sha256(combined.encode()).hexdigest().upper()


def validate_host(ready, work, raw, contract):
    expected = identity(ready); same_identity(expected, work); same_identity(expected, raw)
    frequency = integer(ready["stopwatchFrequency"], "Stopwatch frequency", 1)
    require(work["stopwatchFrequency"] == raw["stopwatchFrequency"] == frequency, "Host clock frequency drift")
    payload = work["payload"]; final = raw["payload"]
    require(payload["status"] == final["status"] == "completed" and final["error"] is None, "Host did not complete normally")
    require(final["phases"] == payload["phases"] and final["managed"] == payload["managed"], "Terminal phase/managed evidence changed")
    first, start, end = (integer(payload[key], key) for key in ("samplerReadyObservedQpc", "workStartQpc", "workEndQpc"))
    require(integer(ready["payload"]["readyQpc"], "host ready QPC") <= first <= start <= end <= integer(final["finishedQpc"], "host terminal QPC"), "Host handshake/work clock order")
    require([row["name"] for row in payload["phases"]] == PHASES, "Missing/reordered source-through-hit phase")
    previous = start
    for row in payload["phases"]:
        begin, finish = integer(row["startQpc"], "phase start"), integer(row["endQpc"], "phase end")
        require(previous <= begin <= finish <= end, "Phase clock order")
        integer(row["uiThreadAllocatedBytes"], "phase allocation"); previous = finish
    require([row["phase"] for row in payload["managed"]] == MANAGED_PHASES, "Managed observation phases differ")
    for row in payload["managed"]:
        require(start <= integer(row["startQpc"], "managed start") <= integer(row["endQpc"], "managed end") <= end, "Managed clock order")
        for key in ("estimatedManagedBytes", "totalAllocatedBytes", "lastGcIndex", "lastGcGeneration", "lastGcHeapSizeBytes", "lastGcFragmentedBytes", "lastGcTotalCommittedBytes"):
            integer(row[key], key)
        require(len(row["collections"]) == 3, "GC generation counters")
        for value in row["collections"]:
            integer(value, "GC collection count")
    require(payload["nativeBytes"] is None and payload["gpuBytes"] is None, "Unavailable native/GPU accounting must remain null")
    require(payload["hitPassed"] is True and payload["allFinitePointsInsideAxes"] is True and payload["sourceGeneratedAfterSamplerReady"] is True, "Original point/range/handshake guard failed")
    integer(payload["primitiveCount"], "recorded primitives", 1)
    sampling = payload["sampling"]
    require(sampling["requestedMode"] == "MinMax" and sampling["renderScale"] == 1 and sampling["pixelWidth"] == 1, "MinMax quality/scale changed")
    for key in ("sourceCount", "visibleCount", "renderedCount", "figureCount", "pixelBucketCount"):
        integer(sampling[key], key)
    if expected["mode"] == "full":
        require(payload["inputCount"] == sampling["sourceCount"] == sampling["visibleCount"] == 1_000_000, "Full source was clipped/reduced before MinMax")
        require(sampling["isApplied"] is True and 0 < sampling["renderedCount"] < 1_000_000 and sampling["figureCount"] == 1 and sampling["pixelBucketCount"] > 0, "MinMax display reduction absent")
        require(payload["rawInputRetained"] is True and payload["originalPointValueBytes"] == 16_000_000 and payload["emptySourceNoGeneration"] is False, "Original input retention/accounting changed")
        manifest = payload["datasetManifest"]; series_hash, aggregate_hash = expected_dataset()
        require(manifest["id"] == "S1M-L" and manifest["generatorVersion"] == "charts-c0-fixed-integer-xorshift64star-v1" and manifest["seedHex"] == "1020304050607085" and manifest["revision"] == 0, "Wrong source generator")
        require(manifest["inputPointCount"] == manifest["validPointCount"] == 1_000_000 and manifest["seriesPointCounts"] == [1_000_000] and manifest["seriesSha256"] == [series_hash] and manifest["sha256"] == aggregate_hash, "Original source count/hash mismatch")
        require(manifest["monotonicX"] is True and manifest["containsGaps"] is False and manifest["markerSize"] == 0 and manifest["realtimePoc"] is False, "Source quality variant changed")
    else:
        require(payload["inputCount"] == sampling["sourceCount"] == sampling["visibleCount"] == sampling["renderedCount"] == 0 and sampling["isApplied"] is False, "Empty comparator contains source")
        require(payload["datasetManifest"] is None and payload["rawInputRetained"] is False and payload["originalPointValueBytes"] == 0 and payload["emptySourceNoGeneration"] is True, "Empty comparator generated source")
    environment = ready["payload"]["environment"]
    require(environment["fixedXAxis"] == contract["fixedXAxis"] and environment["fixedYAxis"] == contract["fixedYAxis"], "Fixed axes changed")
    require(environment["renderer"] == "software" and environment["renderScaling"] == 1 and environment["dpi"] == [96, 96] and environment["chartDips"] == environment["rasterPixels"] == [1200, 650], "Window/raster/DPI changed")
    require(environment["surface"]["hasGrContext"] is False and environment["surface"]["error"] is None and environment["tieredCompilation"] is False, "Backend/runtime mode changed")
    require(environment["samplingMode"] == "MinMax" and environment["strokeThickness"] == 2 and environment["markerSize"] == 0 and environment["showMarkers"] is False and environment["showLegend"] is False and environment["showGridLines"] is True and environment["showDataLabels"] is False and environment["seriesCount"] == 1, "Matched chart style changed")
    probes = environment["fontProbes"]
    require([row["scalar"] for row in probes] == [0x30, 0x46] and all(row["matched"] is True and row["glyph"] > 0 and re.fullmatch(r"[A-F0-9]{64}", row["sha256"] or "") for row in probes), "Real font content proof missing")
    sources = environment["sources"]
    require(len({row["name"] for row in sources}) == len(sources) and EXPECTED_DEPENDENCIES.issubset({row["name"] for row in sources}), "Dependency fingerprints incomplete/duplicate")
    for row in sources:
        integer(row["bytes"], "dependency bytes", 1)
        require(re.fullmatch(r"[A-F0-9]{64}", row["sha256"]) is not None, "Dependency SHA")
    return expected, payload, environment


def validate_stream(rows, expected, kind, platform, clock):
    require(len(rows) >= 2, "Prework and terminal samples required")
    maxima, gaps, durations = {}, [], []
    required_metrics = ({"privateCommitBytes", "workingSetBytes", "osLifetimePeakCommitChargeBytes", "osLifetimePeakWorkingSetBytes"} if platform == "windows" else {"rssBytes", "osLifetimePeakRssBytes", "statusSwapBytes"}) if kind == "process" else {"rssBytes", "pssBytes", "privateResidentBytes", "privateCleanBytes", "privateDirtyBytes", "privateHugetlbBytes", "swapBytes", "swapPssBytes"}
    for index, row in enumerate(rows):
        require(all(row[key] == value for key, value in expected.items()), "Sample nonce/PID/complete-start changed")
        require(row["kind"] == kind and row["index"] == index, "Sample kind/index order")
        require(row["reason"] == ("before-source-generation" if index == 0 else "terminal-hold" if index == len(rows) - 1 else "periodic"), "Sample reason sequence")
        start, end = integer(row["readStartNs"], "read start"), integer(row["readEndNs"], "read end")
        scheduled = integer(row["scheduledNs"], "scheduled timestamp")
        require(scheduled <= start <= end and row["deadlineLatenessNs"] == start - scheduled, "Sample read/schedule interval")
        if index:
            require(start >= rows[index - 1]["readEndNs"] and row["gapNs"] == start - rows[index - 1]["readStartNs"], "Sample gap/order mismatch")
            gaps.append(row["gapNs"])
        else:
            require(row["gapNs"] is None, "First gap must be null")
        require(row["errors"] == [] and isinstance(row["metrics"], dict) and set(row["metrics"]) == required_metrics, "Missing/error/changed metric schema")
        observed = row["observedIdentity"]
        require(all(observed[key] == expected[key] for key in ("windowsCreationFileTimeUtc", "linuxProcStartTicks", "linuxBootId")), "External native process start not matched")
        if platform == "windows":
            require(clock["qpcFrequency"] > 0 and integer(row["readStartQpc"], "QPC start") <= integer(row["readEndQpc"], "QPC end"), "Native QPC timing missing")
        else:
            require(row["readStartQpc"] is None and row["readEndQpc"] is None and clock["qpcFrequency"] is None and "CLOCK_MONOTONIC" in clock["perfCounterImplementation"], "Linux monotonic clock differs")
        for name, value in row["metrics"].items():
            if name == "swapPssBytes" and value is None:
                continue
            integer(value, name); maxima[name] = max(maxima.get(name, value), value)
        if kind == "smaps":
            require(row["metrics"]["privateResidentBytes"] == sum(row["metrics"][key] for key in ("privateCleanBytes", "privateDirtyBytes", "privateHugetlbBytes")), "Private-resident components mismatch")
        elif platform == "windows":
            require(row["metrics"]["osLifetimePeakCommitChargeBytes"] >= row["metrics"]["privateCommitBytes"] and row["metrics"]["osLifetimePeakWorkingSetBytes"] >= row["metrics"]["workingSetBytes"], "OS lifetime counters smaller than their current process counters")
        else:
            require(row["metrics"]["osLifetimePeakRssBytes"] >= row["metrics"]["rssBytes"], "OS RSS high-water counter smaller than current accounting")
        durations.append(end - start)
    return {"sampleCount": len(rows), "maxObserved": maxima, "largestGapNs": max(gaps, default=0), "longestReadNs": max(durations), "observationWindowNs": rows[-1]["readEndNs"] - rows[0]["readStartNs"], "qualification": "Observed sample maxima; scheduler gaps and read durations remain visible. No exact instantaneous peak."}


def read_rows(path):
    with Path(path).open(encoding="utf-8-sig") as stream:
        return [json.loads(line) for line in stream if line.strip()]


def host_clock(row, field, platform):
    return row["read" + field + ("Qpc" if platform == "windows" else "Ns")]


def validate_run(root, entry, contract):
    host, sampler, control = (bounded(root, entry[key], True) for key in ("host", "sampler", "control"))
    ready, work, raw = (load(host / name) for name in ("ready.json", "work-complete.json", "raw.json"))
    expected, payload, environment = validate_host(ready, work, raw, contract)
    require(expected["mode"] == entry["mode"] and expected["runToken"] == entry["runToken"] and entry["passed"] is True and entry["error"] is None, "Campaign attempt failed/identity mismatch")
    result, started = (load(sampler / name) for name in ("result.json", "started.json"))
    require(result["schema"] == started["schema"] == "charts-memory-sampling/v1" and result["passed"] is True and result["error"] is None and result["identity"] == started["identity"] == expected, "Sampler did not complete for exact host")
    require(result["intervalMs"] == 20 and result["smapsIntervalMs"] == 100 and result["clock"] == started["clock"] and result["clock"]["monotonicNs"] is True, "Sampling interval/clock changed")
    require(all(started["observedIdentity"][key] == expected[key] for key in ("windowsCreationFileTimeUtc", "linuxProcStartTicks", "linuxBootId")), "Initial external start identity differs")
    require(result["nativeBytes"] is None and result["gpuBytes"] is None and result["originalC7PeakMemory"] is None, "Unavailable memory qualification claimed")
    platform = result["platform"]; require(platform in ("windows", "linux"), "Unsupported sampler platform")
    require(ready["stopwatchFrequency"] == (result["clock"]["qpcFrequency"] if platform == "windows" else 1_000_000_000), "Shared host/sampler clock missing")
    verify_reference(result["readyReference"], host / "ready.json"); verify_reference(started["readyReference"], host / "ready.json"); verify_reference(result["workCompleteReference"], host / "work-complete.json")
    controls = {name: load(control / filename) for name, filename in (("ready", "sampler-ready.json"), ("completed", "sampler-complete.json"), ("release", "release.json"))}
    for status, value in controls.items():
        control_identity(expected, value, status)
    require(controls["completed"]["resultSha256"] == fingerprint(sampler / "result.json")["sha256"], "Sampler-complete references another result")
    streams = {"process": read_rows(sampler / "samples.jsonl"), "smaps": read_rows(sampler / "smaps.jsonl")}
    audited = {}
    for kind in ("process", "smaps"):
        if platform == "windows" and kind == "smaps":
            require(streams[kind] == [] and result["maxObserved"][kind] == {} and result["sampleCounts"][kind] == 0 and controls["completed"]["finalSmapsSampleIndex"] is None, "Windows must not invent smaps")
            continue
        audited[kind] = validate_stream(streams[kind], expected, kind, platform, result["clock"])
        require(audited[kind]["sampleCount"] == result["sampleCounts"][kind] and audited[kind]["maxObserved"] == result["maxObserved"][kind], "Sampler summary differs from raw reads")
        require(host_clock(streams[kind][0], "End", platform) <= payload["samplerReadyObservedQpc"] <= payload["workStartQpc"], "First sample was not before source generation")
        require(payload["workEndQpc"] <= host_clock(streams[kind][-1], "Start", platform) <= host_clock(streams[kind][-1], "End", platform) <= raw["payload"]["finishedQpc"], "Terminal sample was not taken while host held source roots")
    require(controls["ready"]["firstProcessSampleIndex"] == 0 and controls["completed"]["finalProcessSampleIndex"] == len(streams["process"]) - 1, "Handshake sample indices")
    require(controls["ready"]["clock"] == result["clock"] and controls["ready"]["preworkReadEndNs"] == max(values[0]["readEndNs"] for values in streams.values() if values), "Prework handshake clock proof differs from first samples")
    require(integer(started["startedNs"], "sampler start") <= streams["process"][0]["readStartNs"], "Sampler start is after its first read")
    require(controls["ready"]["preworkReadEndQpc"] == streams["process"][0]["readEndQpc"], "Native prework QPC differs")
    if platform == "linux":
        require(controls["completed"]["finalSmapsSampleIndex"] == len(streams["smaps"]) - 1, "smaps final index")
    host_receipt_proof = None
    for key, pid in (("hostReceipt", expected["processId"]), ("samplerReceipt", started["samplerProcessId"])):
        receipt = load(bounded(root, entry[key]))
        require(receipt["schemaVersion"] == "charts-memory-process-v2" and receipt["passed"] is True and receipt["exitCode"] == 0 and receipt["timedOut"] is False and receipt["processId"] == pid, "Process receipt did not complete exact launched PID")
        if key == "hostReceipt":
            host_receipt_proof = validate_host_receipt(expected, receipt, platform, ready["stopwatchFrequency"])
        else:
            require(receipt["parentObservedProcessStartedUtc"] == receipt["processStartedUtc"] and receipt["canonicalProcessStartedUtc"] is None and receipt["nativeIdentityAtReady"] is None and receipt["hostReadyIdentityValidated"] is False, "Sampler receipt must preserve its own parent observation and not borrow host canonical UTC")
            complete_utc_filetime(receipt["processStartedUtc"])
    return {"pairIndex": entry["pairIndex"], "position": entry["position"], "mode": expected["mode"], "identity": expected, "platform": platform,
            "environment": environment, "streams": audited, "managedObservations": payload["managed"], "phases": payload["phases"],
            "originalPointValueBytes": payload["originalPointValueBytes"], "rawInputRetained": payload["rawInputRetained"],
            "sampling": payload["sampling"], "hostReceiptIdentityProof": host_receipt_proof, "evidence": {name: fingerprint(path) for name, path in (("hostReady", host / "ready.json"), ("hostWork", host / "work-complete.json"), ("hostRaw", host / "raw.json"), ("samplerResult", sampler / "result.json"), ("processSamples", sampler / "samples.jsonl"), ("smapsSamples", sampler / "smaps.jsonl"))}}


def compare_pair(empty, full, matched_fields, nominal):
    require(empty["platform"] == full["platform"], "Pair crosses operating systems")
    for field in matched_fields + ["display", "waylandDisplay"]:
        require(empty["environment"][field] == full["environment"][field], "Matched environment differs: " + field)
    increments = {}
    for kind, baseline in empty["streams"].items():
        observed = full["streams"][kind]
        require(set(baseline["maxObserved"]) == set(observed["maxObserved"]), "Available metric kinds differ within pair")
        for name, before in baseline["maxObserved"].items():
            after = observed["maxObserved"][name]; difference = after - before
            increments[kind + "." + name] = {"measurementKind": "os-reported-lifetime-counter" if name.startswith("osLifetime") else "sampled-current-accounting",
                "emptyMaxObservedBytes": before, "fullMaxObservedBytes": after, "fullMinusEmptyObservedBytes": difference, "withinNominalDiagnosticIncrement": difference <= nominal}
    return {"pairIndex": empty["pairIndex"], "order": ["empty", "full"] if empty["position"] == 1 else ["full", "empty"], "increments": increments,
            "diagnosticIncrementTargetPassed": all(row["withinNominalDiagnosticIncrement"] for row in increments.values()),
            "qualification": "Same-kind signed maxObserved(full)-maxObserved(empty), including retained original input and all process memory. Negative differences remain negative. Not exclusive chart/native bytes or an instantaneous peak."}


def diagnostic_exit_code(evidence_passed, target_passed, evidence_only=False):
    return 2 if not evidence_passed else 0 if target_passed or evidence_only else 1


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument("--campaign", type=Path, required=True); parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--evidence-only", action="store_true", help="Exit0 for intact evidence even when its independent sampled-increment diagnostic target fails.")
    args = parser.parse_args(argv); require(not args.output.exists(), "Fresh oracle output required")
    contract = load(Path(__file__).with_name("ChartsMemoryProbe.Contract.json"))
    result = {"schema": SCHEMA, "passed": False, "evidenceIntegrityPassed": False, "diagnosticOnly": True, "diagnosticIncrementTargetPassed": None,
              "originalC7PeakMemory": None, "nativeBytes": None, "gpuBytes": None, "present": None, "formalPeakMemoryQualified": False, "runs": [], "pairs": [], "errors": []}
    try:
        campaign = load(args.campaign); root = args.campaign.resolve().parent
        require(campaign["schema"] == "charts-memory-campaign/v1" and campaign["diagnosticOnly"] is True and campaign["pairOrder"] == [["empty", "full"], ["full", "empty"], ["empty", "full"]] and campaign["nominalIncrementBytes"] == 536_870_912, "Memory diagnostic protocol changed")
        require([(row["pairIndex"], row["position"], row["mode"]) for row in campaign["runs"]] == ORDER, "Exactly three independent AB/BA/AB pairs required")
        tokens, processes = set(), set()
        for entry in campaign["runs"]:
            audited = validate_run(root, entry, contract)
            process = native_process_key(audited["identity"])
            require(audited["identity"]["runToken"] not in tokens and process not in processes, "Run reused nonce or process instance")
            tokens.add(audited["identity"]["runToken"]); processes.add(process); result["runs"].append(audited)
        require(len({row["platform"] for row in result["runs"]}) == 1, "Campaign crosses OS; compare within each OS separately")
        for index in (1, 2, 3):
            pair = [row for row in result["runs"] if row["pairIndex"] == index]
            result["pairs"].append(compare_pair(next(row for row in pair if row["mode"] == "empty"), next(row for row in pair if row["mode"] == "full"), contract["matchedEnvironment"], campaign["nominalIncrementBytes"]))
        result["evidenceIntegrityPassed"] = True
        result["diagnosticIncrementTargetPassed"] = all(pair["diagnosticIncrementTargetPassed"] for pair in result["pairs"])
        result["passed"] = result["diagnosticIncrementTargetPassed"]
        result["nominalDiagnosticIncrementBytes"] = campaign["nominalIncrementBytes"]
        result["campaign"] = fingerprint(args.campaign); result["contract"] = fingerprint(Path(__file__).with_name("ChartsMemoryProbe.Contract.json"))
    except Exception as error:
        result["errors"].append({"type": type(error).__name__, "message": str(error)})
    result["qualification"] = "Process-total diagnostic with sampled current metrics and separately named OS-reported lifetime counters, per-pair increments only. Linux RSS/HWM kernel accounting is approximate. No pooling across processes; raw source remains in full totals. 512MiB is only an observed-increment diagnostic target. Original C7 timing/budget/peak qualification is unchanged."
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(result, stream, ensure_ascii=False, allow_nan=False, indent=2); stream.write("\n")
    print(json.dumps({"evidenceIntegrityPassed": result["evidenceIntegrityPassed"], "diagnosticIncrementTargetPassed": result["diagnosticIncrementTargetPassed"], "errors": result["errors"], "output": str(args.output)}))
    return diagnostic_exit_code(result["evidenceIntegrityPassed"], result["diagnosticIncrementTargetPassed"], args.evidence_only)


if __name__ == "__main__":
    raise SystemExit(main())
