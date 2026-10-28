"""Independently verify raw native automatic-height geometry and optional binary pair."""
import argparse
import hashlib
import json
import math
from pathlib import Path


def expected_keys():
    return {(plan, round_index, step, phase) for plan, rounds, steps in
            (("scroll", 5, 90), ("mutations", 1, 90), ("lifecycle", 1, 24))
            for round_index in range(rounds) for step in range(steps) for phase in ("layout", "render")}


def read(directory):
    errors = []
    records = {"auto-default": {}, "auto-factory": {}}
    try:
        initial = json.loads((directory / "initial.json").read_text(encoding="utf-8-sig"))
        report = json.loads((directory / "report.json").read_text(encoding="utf-8-sig"))
        if initial["protocol"] != "treedatagrid-native-geometry-v1" or report["protocol"] != initial["protocol"] or initial["native"] is not True:
            errors.append("Invalid native geometry protocol/environment")
        if report["passed"] is not True or report["error"] is not None or report["differences"]:
            errors.append("Native geometry execution reported a failure")
        if (report["frames"], report["snapshots"], report["expectedSnapshots"]) != (1128, 2256, 1128):
            errors.append("Incomplete native geometry execution")
        with (directory / "geometry.jsonl").open(encoding="utf-8") as stream:
            for line in stream:
                record = json.loads(line)
                key = (record["plan"], record["round"], record["step"], record["phase"])
                group = records[record["scenario"]]
                if key in group:
                    errors.append(f"Duplicate geometry record: {record['scenario']}/{key}")
                geometry = record["geometry"]
                validate_geometry(geometry)
                group[key] = hashlib.sha256(json.dumps(geometry, sort_keys=True, separators=(",", ":"), allow_nan=False).encode()).hexdigest()
        for scenario, group in records.items():
            if set(group) != expected_keys():
                errors.append(f"Missing/unexpected raw geometry records: {scenario}")
        differences = sorted(key for key in records["auto-default"].keys() & records["auto-factory"].keys()
                             if records["auto-default"][key] != records["auto-factory"][key])
        if differences:
            errors.append(f"Retained/detached geometry differs: {len(differences)} records; first {differences[0]}")
    except (OSError, ValueError, KeyError, TypeError) as error:
        errors.append(str(error))
        initial = None
    return errors, initial, records


def validate_geometry(geometry):
    def numbers(values, size=False):
        if not isinstance(values, list) or any(type(v) not in (int, float) or not math.isfinite(v) or size and v < 0 for v in values):
            raise ValueError("Invalid native geometry dimensions")
    numbers(geometry["offset"], True)
    numbers(geometry["extent"], True)
    rows = geometry["rows"]
    indices = [r["RowIndex"] for r in rows]
    if not indices or indices != sorted(set(indices)):
        raise ValueError("Missing/duplicate/unsorted native rows")
    for row in rows:
        if type(row["height"]) not in (int, float) or not math.isfinite(row["height"]) or row["height"] < 28:
            raise ValueError("Invalid measured native row height")
        numbers(row["desired"], True)
        numbers(row["bounds"])
        columns = [c["ColumnIndex"] for c in row["cells"]]
        if not columns or columns != sorted(set(columns)):
            raise ValueError("Missing/duplicate/unsorted native cells")
        for cell in row["cells"]:
            for element in [cell, cell["text"], *cell["template"]]:
                if element is not None:
                    numbers(element["desired"], True)
                    numbers(element["bounds"])


def verify(directory, baseline=None):
    errors, initial, records = read(directory)
    pair_differences = []
    if baseline is not None:
        base_errors, base_initial, base_records = read(baseline)
        errors += ["Baseline: " + error for error in base_errors]
        if initial and base_initial:
            for key in ("hostSha256", "themeSha256", "ClientSize", "RenderScaling"):
                if initial[key] != base_initial[key]: errors.append("Native geometry pair changed " + key)
            if initial["functionSha256"] == base_initial["functionSha256"]:
                errors.append("Native geometry binary pair used the same function DLL")
        for scenario, group in records.items():
            other = base_records[scenario]
            pair_differences += [(scenario, *key) for key in group.keys() & other.keys() if group[key] != other[key]]
        if pair_differences:
            errors.append(f"Before/after geometry differs: {len(pair_differences)} records; first {sorted(pair_differences)[0]}")
    return {"passed": not errors, "errors": errors, "rawRecords": sum(map(len, records.values())), "pairDifferences": len(pair_differences)}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--baseline", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = verify(args.directory, args.baseline)
    content = json.dumps(result, indent=2)
    if args.output: args.output.write_text(content + "\n", encoding="utf-8")
    print(content)
    raise SystemExit(0 if result["passed"] else 1)
