"""Classify perf script -G leaf samples against Linux native-probe windows.

Pure Python; never launches a process. Source perf.data and text remain immutable.
Expected perf export: perf script -G --ns -F tid,time,ip,sym,dso,period
The observed perf 7 fixed order is TID TIME: PERIOD IP SYMBOL (DSO), regardless
of the order in the -F list. Check the archived command/help for every capture.
No nominal sampling interval is converted to CPU or wall-clock duration.
"""
from __future__ import annotations

import argparse
import bisect
from collections import Counter, defaultdict
import datetime as dt
import hashlib
import json
import math
from pathlib import Path
import re
import sys

SAMPLE = re.compile(r"^\s*(?P<tid>\d+)\s+(?P<seconds>\d+)\.(?P<fraction>\d{1,9}):\s+(?P<period>\d+)\s+(?P<ip>(?:0x)?[0-9a-fA-F]+)\s+(?P<tail>.+?)\s*$")
LOSS = re.compile(r"PERF_RECORD_(?:LOST(?:_SAMPLES)?|THROTTLE|UNTHROTTLE)|\bLOST(?:_SAMPLES)?\b|\bthrottl(?:e|ed|ing)\b", re.IGNORECASE)
LOSS_COUNT = re.compile(r"\blost\b(?:\s+samples?)?\s*[:=]?\s*(\d+)\b", re.IGNORECASE)
LOSS_STATISTIC = re.compile(r"^\s*(LOST(?:_SAMPLES)?)\s+events:\s*(\d+)\b", re.IGNORECASE)
UNKNOWN_SYMBOL = re.compile(r"^(?:\[unknown\]|\?\?|0x[0-9a-fA-F]+)$")
SCOPES = ("measurement-draw", "measurement-excluded-clear", "warmup-draw", "warmup-excluded-clear",
          "outside-window", "other-thread", "other-case-window")
SHA256 = re.compile(r"^[0-9A-F]{64}$")
BITS64 = re.compile(r"^[0-9A-F]{16}$")


def require(ok, message):
    if not ok:
        raise ValueError(message)


def strict_json(value):
    def pairs(items):
        result = {}
        for key, item in items:
            require(key not in result, "Duplicate JSON property: " + key)
            result[key] = item
        return result
    def constant(item):
        raise ValueError("Nonfinite JSON constant: " + item)
    return json.loads(value, object_pairs_hook=pairs, parse_constant=constant)


def positive_int(value, name, allow_zero=False):
    require(type(value) is int and value >= (0 if allow_zero else 1), "Invalid integer: " + name)
    return value


def utc(value):
    require(isinstance(value, str) and value, "Missing processStartedUtc")
    parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    require(parsed.tzinfo is not None and parsed.utcoffset() == dt.timedelta(0), "processStartedUtc must be UTC")
    return value


def suffix_dso(value):
    """Split the final balanced parenthesized DSO, preserving nested (deleted).

    Splitting on the first '(' would corrupt demangled symbols, and splitting
    on the last '(' would mistake a deleted mapping suffix for the whole DSO.
    """
    require(value.endswith(")"), "Missing final parenthesized DSO")
    depth = 0
    for index in range(len(value) - 1, -1, -1):
        character = value[index]
        if character == ")":
            depth += 1
        elif character == "(":
            depth -= 1
            if depth == 0:
                require(index > 0 and value[index - 1].isspace(), "DSO needs whitespace separator")
                symbol = value[:index].rstrip()
                require(symbol, "Missing symbol field")
                return symbol, value[index + 1:-1]
    raise ValueError("Unbalanced final DSO")


def parse_sample(line):
    match = SAMPLE.fullmatch(line)
    require(match is not None, "Not an observed perf leaf row")
    fields = match.groupdict()
    symbol, dso = suffix_dso(fields["tail"])
    require(dso, "Empty DSO")
    tid = positive_int(int(fields["tid"]), "sample TID")
    period = positive_int(int(fields["period"]), "sample period")
    return {"osThreadId": tid,
            "monotonicNs": int(fields["seconds"]) * 1_000_000_000 + int(fields["fraction"].ljust(9, "0")),
            "period": period, "ip": fields["ip"],
            "symbol": symbol, "dso": dso,
            "unknownSymbol": UNKNOWN_SYMBOL.fullmatch(symbol) is not None or symbol == fields["ip"],
            "unknownDso": dso in ("[unknown]", "unknown", "??")}


def load_windows(path, expected_schema):
    windows = []
    identities = set()
    with path.open(encoding="utf-8-sig") as source:
        for ordinal, line in enumerate(source, 1):
            require(len(line) <= 1_048_576 and line.strip(), f"Oversized/blank window row {ordinal}")
            row = strict_json(line)
            require(row.get("schema") == expected_schema, f"Unexpected window schema at {ordinal}")
            pid = positive_int(row.get("processId"), "window processId")
            started = utc(row.get("processStartedUtc"))
            tid = positive_int(row.get("osThreadId"), "window osThreadId")
            begin = positive_int(row.get("startMonotonicNs"), "window startMonotonicNs", True)
            end = positive_int(row.get("endMonotonicNs"), "window endMonotonicNs", True)
            require(row.get("clockMonotonic") is True, f"Window clockMonotonic is not true at {ordinal}")
            require(end > begin, f"Empty/reversed Linux window {ordinal}")
            require(row.get("kind") in ("draw", "excluded-clear"), f"Unknown window kind {ordinal}")
            require(row.get("phase") in ("warmup", "measurement"), f"Unknown window phase {ordinal}")
            require(isinstance(row.get("case"), str) and row["case"], "Missing window case")
            positive_int(row.get("index"), "window index", True)
            identities.add((pid, started))
            windows.append({**row, "sourceLine": ordinal, "osThreadId": tid})
    require(windows and len(identities) == 1, "Expected windows from one fresh Linux process")
    by_tid = defaultdict(list)
    keys = set()
    for row in windows:
        key = (row["case"], row["kind"], row["phase"], row["index"], row["osThreadId"])
        require(key not in keys, "Duplicate logical window identity")
        keys.add(key)
        by_tid[row["osThreadId"]].append(row)
    for tid, rows in by_tid.items():
        rows.sort(key=lambda row: row["startMonotonicNs"])
        require(all(a["endMonotonicNs"] <= b["startMonotonicNs"] for a, b in zip(rows, rows[1:])),
                f"Overlapping windows on TID {tid}")
    return windows, dict(by_tid), next(iter(identities))


class WindowIndex:
    def __init__(self, by_tid, case):
        self.rows = by_tid
        self.starts = {tid: [row["startMonotonicNs"] for row in rows] for tid, rows in by_tid.items()}
        self.case = case

    def classify(self, sample):
        tid = sample["osThreadId"]
        rows = self.rows.get(tid)
        if rows is None:
            return "other-thread", None
        index = bisect.bisect_right(self.starts[tid], sample["monotonicNs"]) - 1
        if index < 0 or sample["monotonicNs"] >= rows[index]["endMonotonicNs"]:
            return "outside-window", None
        row = rows[index]
        if row["case"] != self.case:
            return "other-case-window", row
        return row["phase"] + "-" + row["kind"], row


def distribution(values):
    if not values:
        return {"count": 0, "sum": 0, "minimum": None, "p50": None, "p95": None, "maximum": None}
    ordered = sorted(values)
    return {"count": len(values), "sum": sum(values), "minimum": ordered[0],
            "p50": ordered[math.ceil(.50 * len(values)) - 1],
            "p95": ordered[math.ceil(.95 * len(values)) - 1], "maximum": ordered[-1]}


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            result.update(chunk)
    return {"path": str(path.resolve()), "bytes": path.stat().st_size, "sha256": result.hexdigest().upper()}


def validate_coordinates(probe, report_path):
    """Validate v3's full ordinal bit comparison; legacy fixtures remain unqualified.

    The producer's comparison path may be Linux absolute while analysis runs on
    Windows. Only its fixed-name sibling is opened, then bytes/SHA are checked.
    No arbitrary source or native path from the report is read here.
    """
    if probe["schema"] != "charts-c703-native-circle-probe/v3":
        return {"qualified": False, "status": "legacy-fixture-no-production-coordinate-proof",
                "scope": "v1/v2 fixture parsing does not qualify production FMA coordinate identity."}, None
    declared = probe.get("coordinateQualification")
    require(isinstance(declared, dict) and declared.get("passed") is True, "v3 coordinate qualification did not pass")
    count = positive_int(declared.get("pointCount"), "coordinate pointCount")
    require(count == positive_int(probe["fixture"]["count"], "fixture coordinate count"), "Coordinate/fixture point counts differ")
    require(type(declared.get("mismatchCount")) is int and declared["mismatchCount"] == 0, "Coordinate mismatches reported")
    for key in ("linkedCentersBitsSha256", "productionCentersBitsSha256"):
        require(isinstance(declared.get(key), str) and SHA256.fullmatch(declared[key]), "Invalid coordinate SHA: " + key)
    require(declared["linkedCentersBitsSha256"] == declared["productionCentersBitsSha256"], "Coordinate reported hashes differ")
    source = declared.get("linkedImplementationSource")
    require(isinstance(source, dict) and isinstance(source.get("path"), str) and source["path"] and
            isinstance(source.get("sha256"), str) and SHA256.fullmatch(source["sha256"]), "Missing linked implementation fingerprint")
    positive_int(source.get("bytes"), "linked implementation bytes")
    fingerprint = declared.get("comparisonFile")
    require(isinstance(fingerprint, dict) and isinstance(fingerprint.get("path"), str) and
            fingerprint["path"].replace("\\", "/").rsplit("/", 1)[-1] == "coordinate-comparison.json", "Unexpected coordinate comparison member")
    path = report_path.resolve().parent / "coordinate-comparison.json"
    require(path.is_file() and path.stat().st_size <= 64 * 1024 * 1024, "Missing/oversized coordinate comparison file")
    actual = digest(path)
    require(fingerprint.get("bytes") == actual["bytes"] and fingerprint.get("sha256") == actual["sha256"], "Coordinate comparison file fingerprint differs")
    comparison = strict_json(path.read_text(encoding="utf-8-sig"))
    require(isinstance(comparison, dict) and comparison.get("schema") == "charts-c703-native-coordinate-comparison/v1" and
            comparison.get("hashEncoding") == "xy64le-ordinal", "Unexpected coordinate comparison schema/encoding")
    require(comparison.get("passed") is True, "Coordinate comparison sidecar did not pass")
    positive_int(comparison.get("pointCount"), "comparison pointCount")
    positive_int(comparison.get("mismatchCount"), "comparison mismatchCount", True)
    for key in ("pointCount", "mismatchCount", "linkedCentersBitsSha256", "productionCentersBitsSha256"):
        require(comparison.get(key) == declared[key], "Coordinate report/sidecar differs: " + key)
    rows = comparison.get("rows")
    require(isinstance(rows, list) and len(rows) == count, "Coordinate rows are incomplete")
    hashes = {prefix: hashlib.sha256() for prefix in ("source", "linked", "production")}
    mismatches = 0
    for ordinal, row in enumerate(rows):
        require(isinstance(row, dict), "Coordinate row is not an object")
        require(type(row.get("ordinal")) is int and row["ordinal"] == ordinal, "Coordinate ordinal sequence differs")
        for prefix in hashes:
            for axis in ("X", "Y"):
                bits = row.get(prefix + axis + "Bits")
                require(isinstance(bits, str) and BITS64.fullmatch(bits), "Invalid coordinate IEEE754 bit string")
                hashes[prefix].update(int(bits, 16).to_bytes(8, "little"))
        equal = all(row["linked" + axis + "Bits"] == row["production" + axis + "Bits"] for axis in ("X", "Y"))
        require(type(row.get("equal")) is bool and row["equal"] == equal, "Coordinate row equality flag differs")
        mismatches += int(not equal)
    require(mismatches == declared["mismatchCount"] == 0, "Recomputed coordinate mismatch count differs")
    for prefix in ("linked", "production"):
        require(hashes[prefix].hexdigest().upper() == declared[prefix + "CentersBitsSha256"], "Recomputed coordinate hash differs: " + prefix)
    fixture = probe["fixture"]
    require(fixture.get("centersBitsSha256") == declared["linkedCentersBitsSha256"], "Fixture is not the qualified linked centers")
    if "sourcePointsSha256" in fixture:
        require(fixture["sourcePointsSha256"] == hashes["source"].hexdigest().upper(), "Coordinate source point hash differs from fixture")
    return {"qualified": True, "status": "full-ordinal-bit-comparison-passed", "pointCount": count,
            "mismatchCount": mismatches, "linkedCentersBitsSha256": declared["linkedCentersBitsSha256"],
            "productionCentersBitsSha256": declared["productionCentersBitsSha256"],
            "sourcePointsBitsSha256": hashes["source"].hexdigest().upper(), "comparisonFile": actual,
            "linkedImplementationSource": source,
            "scope": "The v3 producer's full comparison was fingerprinted and all ordinal bits/hashes recomputed. Source/native archive provenance and product acceptance require separate verification."}, actual


def loss_record(line, origin, ordinal, section=None):
    if not LOSS.search(line):
        return None
    match = LOSS_COUNT.search(line)
    statistic = LOSS_STATISTIC.fullmatch(line.split("(", 1)[0].rstrip())
    return {"origin": origin, "line": ordinal, "text": line,
            "section": section,
            "parsedLostCount": int(match.group(1)) if match else None,
            "statistic": {"label": statistic.group(1).upper() + " events", "value": int(statistic.group(2)),
                          "interpretation": "Perf-reported statistic in this section; record count and missing-sample count are not assumed interchangeable."} if statistic else None,
            "note": "Raw diagnostic retained. Counts from different exports are not added; they can describe the same lost record."}


def analyze(args):
    require(not args.output.exists(), "Output directory must be fresh")
    windows, by_tid, identity = load_windows(args.windows, args.window_schema)
    require(any(row["case"] == args.case and row["phase"] == "measurement" and row["kind"] == "draw" for row in windows),
            "No target measurement draw windows")
    require(args.probe_report.is_file() and args.probe_report.stat().st_size <= 8 * 1024 * 1024, "Missing/oversized probe report")
    probe = strict_json(args.probe_report.read_text(encoding="utf-8-sig"))
    require(probe.get("schema") in ("charts-c703-native-circle-probe/v1", "charts-c703-native-circle-probe/v2", "charts-c703-native-circle-probe/v3"), "Unexpected probe report schema")
    coordinates, coordinate_file = validate_coordinates(probe, args.probe_report)
    require(probe["process"]["pid"] == identity[0] and probe["process"]["processStartedUtc"] == identity[1], "Probe/window process identity differs")
    require(probe.get("measurementCompleted") is True and probe.get("nativeIdentityStable") is True, "Incomplete measurement or unstable native identity")
    measurement = probe["measurement"]
    require(measurement["case"] == args.case and measurement["frequency"] == 1_000_000_000 and
            measurement["clock"] == "Linux CLOCK_MONOTONIC nanoseconds", "Probe case/clock differs")
    require(measurement["clearOutsideDrawWindows"] is True and measurement["allocationReadbackHashAndJsonOutsideDrawWindows"] is True,
            "Probe draw-window exclusion guard differs")
    target_windows = [row for row in windows if row["case"] == args.case and row["phase"] == "measurement"]
    require(sum(row["kind"] == "draw" for row in target_windows) == measurement["batches"] and
            sum(row["kind"] == "excluded-clear" for row in target_windows) == measurement["batches"], "Terminal probe/window counts differ")
    require(all(row["osThreadId"] == measurement["osThreadId"] for row in target_windows), "Probe/window native TID differs")
    inputs = {"leaf": digest(args.leaf), "windows": digest(args.windows), "probeReport": digest(args.probe_report)}
    if coordinate_file:
        inputs["coordinateComparison"] = coordinate_file
    if args.loss_file:
        inputs["loss"] = digest(args.loss_file)
    args.output.mkdir(parents=True)
    index = WindowIndex(by_tid, args.case)
    counts = Counter()
    unknown_symbols = Counter()
    unknown_dsos = Counter()
    periods = defaultdict(list)
    groups = defaultdict(lambda: {"count": 0, "periods": []})
    losses = []
    unparsed = metadata = 0
    with args.leaf.open(encoding="utf-8-sig") as source, \
            (args.output / "classified-leaf-samples.jsonl").open("x", encoding="utf-8") as parsed_output, \
            (args.output / "unparsed-lines.jsonl").open("x", encoding="utf-8") as rejected_output:
        for ordinal, raw in enumerate(source, 1):
            line = raw.rstrip("\r\n")
            if not line.strip() or line.lstrip().startswith("#"):
                metadata += 1
                loss = loss_record(line, "leaf", ordinal)
                if loss:
                    losses.append(loss)
                continue
            try:
                require(len(line) <= 1_048_576, "Oversized perf line")
                sample = parse_sample(line)
            except ValueError as error:
                loss = loss_record(line, "leaf", ordinal)
                if loss:
                    losses.append(loss)
                    continue
                unparsed += 1
                rejected_output.write(json.dumps({"line": ordinal, "text": line, "error": str(error)}, ensure_ascii=False) + "\n")
                continue
            scope, window = index.classify(sample)
            sample.update(sourceLine=ordinal, scope=scope, windowSourceLine=window["sourceLine"] if window else None)
            counts[scope] += 1
            periods[scope].append(sample["period"])
            unknown_symbols[scope] += int(sample["unknownSymbol"])
            unknown_dsos[scope] += int(sample["unknownDso"])
            unresolved_ip = sample["ip"] if sample["unknownSymbol"] else None
            group = groups[(scope, sample["dso"], sample["symbol"], sample["unknownSymbol"], sample["unknownDso"], unresolved_ip)]
            group["count"] += 1
            group["periods"].append(sample["period"])
            parsed_output.write(json.dumps(sample, ensure_ascii=False) + "\n")
    if args.loss_file:
        section = None
        with args.loss_file.open(encoding="utf-8-sig") as source:
            for ordinal, raw in enumerate(source, 1):
                line = raw.rstrip("\r\n")
                if line.strip().endswith("stats:"):
                    section = line.strip()[:-1]
                loss = loss_record(line, "loss-file", ordinal, section)
                if loss:
                    losses.append(loss)
    scope_summary = {scope: {"leafSamples": counts[scope], "unknownSymbolLeafSamples": unknown_symbols[scope],
                            "unknownDsoLeafSamples": unknown_dsos[scope], "periodDistribution": distribution(periods[scope])}
                     for scope in SCOPES}
    leaf_groups = [{"scope": scope, "dso": dso, "symbol": symbol, "unresolvedIp": unresolved_ip,
                    "unknownSymbol": unknown_symbol, "unknownDso": unknown_dso,
                    "leafSamples": value["count"], "periodDistribution": distribution(value["periods"])}
                   for (scope, dso, symbol, unknown_symbol, unknown_dso, unresolved_ip), value in groups.items()]
    leaf_groups.sort(key=lambda row: (row["scope"], -row["leafSamples"], row["dso"], row["symbol"]))
    result = {"schema": "charts-c703-native-leaf-profile-analysis/v1", "createdUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
              "analyzer": digest(Path(__file__)), "inputs": inputs, "processIdentityFromWindows": {"processId": identity[0], "processStartedUtc": identity[1]},
              "targetCase": args.case, "targetScope": "measurement-draw", "windowCount": len(windows),
              "coordinateQualification": coordinates,
              "productionPathStructureQualification": probe.get("productionPathStructureQualification"),
              "probeCorrectness": {"passed": probe.get("passed"), "strictPixelParityPassed": probe.get("strictPixelParityPassed"),
                                   "candidatePixelDecision": probe.get("candidatePixelDecision"), "nativeIdentityStable": probe["nativeIdentityStable"],
                                   "graphicsInitRequested": probe.get("graphicsInitRequested"), "graphicsInitCalled": probe.get("graphicsInitCalled"),
                                   "graphicsInitCompleted": probe.get("graphicsInitCompleted"), "graphicsInitPixelParityPassed": probe.get("graphicsInitPixelParityPassed"),
                                   "pathsOvalsPixelParityPassed": probe.get("pathsOvalsPixelParityPassed"),
                                   "note": "A declared alternative primitive can fail pixel parity while the baseline paths diagnostic remains useful. Source probe failure is retained, never promoted to product acceptance."},
              "nativeIdentityFromProbe": {"start": probe.get("nativeIdentityStart"), "afterGraphicsInit": probe.get("nativeIdentityAfterGraphicsInit"), "end": probe.get("nativeIdentityEnd")},
              "windowCounts": {key: value for key, value in sorted(Counter(row["phase"] + "-" + row["kind"] for row in windows).items())},
              "parsedLeafSamples": sum(counts.values()), "unparsedLines": unparsed, "metadataOrBlankLines": metadata,
              "parsingQualified": unparsed == 0 and counts["measurement-draw"] > 0,
              "scopes": scope_summary, "leafGroups": leaf_groups,
              "lossEvidence": {"separateLossFileProvided": args.loss_file is not None, "records": losses,
                               "zeroReportedLossEstablished": False,
                               "note": "No loss sum inferred. Absence of matching text is not proof of zero loss; inspect original perf receipts/raw records. Multiple exports can duplicate losses."},
              "scope": ["Half-open exact monotonic-nanosecond windows; native OS TID and explicit case/phase/kind. Clear, warmup, other threads/cases and outside-window samples remain separate.",
                        "Leaf counts only. The -G export has no caller chains, so no inclusive or AA/clip caller attribution is asserted.",
                        "No conversion of period sums or nominal frequency to exact CPU/wall time, no speedup ratio, no pooling with formal performance samples.",
                        "DSO/symbol text is preserved, including unresolved symbols and deleted mappings. Symbol names are producer perf resolution, not independently proven debug/source identity.",
                        "Leaf text exports TID, not PID/start identity. Window identity is checked internally; capture supervision and MMAP/native-library identity must be independently verified before function claims."]}
    (args.output / "analysis.json").write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--leaf", type=Path, required=True)
    parser.add_argument("--windows", type=Path, required=True)
    parser.add_argument("--probe-report", type=Path, required=True)
    parser.add_argument("--window-schema", default="charts-c703-native-circle-window/v1")
    parser.add_argument("--case", required=True)
    parser.add_argument("--loss-file", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = analyze(args)
        print(json.dumps({"output": str(args.output.resolve()), "parsedLeafSamples": result["parsedLeafSamples"],
                          "targetLeafSamples": result["scopes"].get("measurement-draw", {}).get("leafSamples", 0),
                          "unparsedLines": result["unparsedLines"], "parsingQualified": result["parsingQualified"]}))
        return 0 if result["parsingQualified"] else 1
    except (ValueError, OSError, KeyError, TypeError) as error:
        print(str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
