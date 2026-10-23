"""Portable structural/semantic mutants; optional historical raw verification.

Run with --historical-root <C4-07/windows> to also validate the seven immutable
previous producer reports. CI tests use compact fixtures and never fetch or
regenerate the registration from candidate output.
"""
from __future__ import annotations

import argparse
import copy
import json
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from charts_mechanism_oracle import validate_mechanism

CONTRACTS = json.loads((Path(__file__).resolve().parents[1] / "charts-ci-mechanism-contracts.json").read_text(encoding="utf-8"))


def put(root, path, value):
    components = [part.replace("~1", "/").replace("~0", "~") for part in path.split("/")[1:]]
    current = root
    for index, component in enumerate(components):
        final = index == len(components) - 1
        child = value if final else ([] if components[index + 1].isdigit() else {})
        if type(current) is list:
            ordinal = int(component)
            while len(current) <= ordinal:
                current.append({})
            if final:
                current[ordinal] = child
            current = current[ordinal]
        else:
            if final or component not in current:
                current[component] = child
            current = current[component]


def get(root, path):
    for part in path.split("/")[1:]:
        part = part.replace("~1", "/").replace("~0", "~")
        root = root[int(part)] if type(root) is list else root[part]
    return root


def local_budget():
    return dict(BudgetBytes=100, EstimatedBytes=5, SeriesDrawingBytes=1, AxisTextBytes=1,
                InteractionTextBytes=1, AxisPenBytes=1, SegmentGeometryBytes=1,
                SeriesDrawingCount=1, AxisTextCount=1, InteractionTextCount=1,
                AxisPenCount=1, SegmentGeometryCount=1, SegmentProjectionCount=1,
                ModelVersion=1, NativeMemoryIncluded=False, SharedIndexCachesIncluded=False)


def global_budget():
    return dict(resourceCacheBudgetBytes=100, estimatedResourceCacheBytes=5,
                backgroundPayloadBudgetBytes=100, estimatedBackgroundPayloadBytes=5,
                maximumBackgroundJobs=4, reservedBackgroundJobs=1, runningBackgroundJobs=1,
                modelVersion=1, nativeMemoryIncluded=False, sharedIndexCachesIncluded=False)


def fixture(stage):
    contract = CONTRACTS["stages"][stage]
    raw = dict(schemaVersion=contract["reportSchema"], passed=True, error=None)
    for path, count in sorted(contract["fixedArrayCountsByPath"].items(), key=lambda pair: pair[0].count("/")):
        put(raw, path, [{} for _ in range(count)])
    for path, ids in contract["scenarioIdsByPath"].items():
        for index, value in enumerate(ids):
            put(raw, f"{path}/{index}/id", value)
    for path, value in contract["constantsByPath"].items():
        put(raw, path, copy.deepcopy(value))
    for path, names in contract["checksByPath"].items():
        put(raw, path, dict.fromkeys(names, True))
    if stage == "render-resource-budget":
        for index, sample in enumerate(raw["drawing"]["samples"]):
            sample.update(mode="cached" if index < 20 else "disabled", allocatedBytes=1 if index < 20 else 4,
                          delta=dict.fromkeys(("preparedData", "views", "seriesDrawings", "axisTexts", "axisPens", "projectedPoints"), 0))
        for path in contract["localBudgetPaths"]:
            put(raw, path, local_budget())
    if stage == "multichart-background-layout":
        raw.update(synchronousPaths=[dict(partCount=1, sha256="a" * 64) for _ in range(4)],
                   backgroundPaths=[dict(partCount=1, sha256="a" * 64) for _ in range(4)],
                   retargetResults=[False, False, True], budgetSamples=[global_budget()],
                   hits=[dict(expectedIndex=200, actualIndex=200, expectedPoint=dict(x=200, y=1.0), actualPoint=dict(x=200, y=1.0)) for _ in range(4)])
        for name in ("hiddenBudget", "reducedCacheBudget", "trimmedBudget", "detachedBudget"):
            raw[name] = global_budget()
    return raw


class MechanismOracleTests(unittest.TestCase):
    def passed(self, stage, raw):
        result = validate_mechanism(stage, raw, CONTRACTS)
        self.assertTrue(result)
        self.assertTrue(all(type(value) is bool for value in result.values()))
        return all(result.values())

    def test_registered_minimal_fixtures(self):
        for stage in CONTRACTS["stages"]:
            with self.subTest(stage=stage):
                self.assertTrue(self.passed(stage, fixture(stage)))

    def test_top_level_true_cannot_hide_removed_check_dictionary(self):
        for stage, contract in CONTRACTS["stages"].items():
            raw = fixture(stage)
            path = next(iter(contract["checksByPath"]))
            parent, name = path.rsplit("/", 1)
            del get(raw, parent)[name]
            with self.subTest(stage=stage):
                self.assertFalse(self.passed(stage, raw))

    def test_empty_missing_false_string_and_unknown_check_keys(self):
        for mutation in ("empty", "removed", "false", "string", "added"):
            for stage, contract in CONTRACTS["stages"].items():
                raw = fixture(stage)
                path = next(iter(contract["checksByPath"]))
                values = get(raw, path)
                name = next(iter(values))
                if mutation == "empty":
                    values.clear()
                elif mutation == "removed":
                    del values[name]
                elif mutation == "false":
                    values[name] = False
                elif mutation == "string":
                    values[name] = "true"
                else:
                    values["unregistered-check"] = True
                with self.subTest(stage=stage, mutation=mutation):
                    self.assertFalse(self.passed(stage, raw))

    def test_unregistered_additional_check_dictionary_rejected(self):
        raw = fixture("layer-cache")
        raw["extra"] = dict(checks=dict(invented=True))
        self.assertFalse(self.passed("layer-cache", raw))

    def test_removed_reordered_and_duplicate_scenarios_rejected(self):
        for mutation in ("removed", "reordered", "duplicate"):
            raw = fixture("layer-cache")
            if mutation == "removed":
                raw["scenarios"].pop()
            elif mutation == "reordered":
                raw["scenarios"].reverse()
            else:
                raw["scenarios"][1]["id"] = raw["scenarios"][0]["id"]
            self.assertFalse(self.passed("layer-cache", raw))

    def test_missing_samples_and_changed_fixture_count_rejected(self):
        raw = fixture("efficient-hit")
        raw["scenarios"][0]["queries"].pop()
        self.assertFalse(self.passed("efficient-hit", raw))
        raw = fixture("efficient-hit")
        raw["scenarios"][0]["sourceCountPerSeries"] -= 1
        self.assertFalse(self.passed("efficient-hit", raw))

    def test_schema_error_flags_and_scope_are_strict(self):
        for key, value in (("schemaVersion", "unknown"), ("passed", 1), ("passed", "true"), ("error", "exception"), ("formalSlaEvaluated", True), ("actualPresentMeasured", "false")):
            raw = fixture("layer-cache")
            raw[key] = value
            self.assertFalse(self.passed("layer-cache", raw))

    def test_resource_budget_raw_forgery_despite_all_flags_true(self):
        for key, value in (("EstimatedBytes", 101), ("SeriesDrawingBytes", 2), ("BudgetBytes", True), ("SegmentProjectionCount", 4097), ("ModelVersion", True)):
            raw = fixture("render-resource-budget")
            raw["drawing"]["samples"][0]["info"][key] = value
            self.assertFalse(self.passed("render-resource-budget", raw))

    def test_resource_allocation_and_cached_work_forgery(self):
        raw = fixture("render-resource-budget")
        raw["drawing"]["samples"][0]["allocatedBytes"] = 9 * 1024 * 1024
        self.assertFalse(self.passed("render-resource-budget", raw))
        raw = fixture("render-resource-budget")
        raw["drawing"]["samples"][0]["delta"]["projectedPoints"] = 1
        self.assertFalse(self.passed("render-resource-budget", raw))

    def test_multichart_raw_paths_and_retarget_forgery(self):
        raw = fixture("multichart-background-layout")
        raw["backgroundPaths"][0]["sha256"] = "b" * 64
        self.assertFalse(self.passed("multichart-background-layout", raw))
        for results in ([True, True, True], [0, 0, 1], [False, True]):
            raw = fixture("multichart-background-layout")
            raw["retargetResults"] = results
            self.assertFalse(self.passed("multichart-background-layout", raw))

    def test_multichart_hit_and_budget_forgery(self):
        raw = fixture("multichart-background-layout")
        raw["hits"][0]["actualPoint"]["y"] = 2
        self.assertFalse(self.passed("multichart-background-layout", raw))
        raw = fixture("multichart-background-layout")
        raw["budgetSamples"][0]["estimatedBackgroundPayloadBytes"] = 101
        self.assertFalse(self.passed("multichart-background-layout", raw))
        raw["budgetSamples"] = []
        self.assertFalse(self.passed("multichart-background-layout", raw))

    def test_unsupported_contract_and_nonobject_report_fail_closed(self):
        with self.assertRaises(ValueError):
            validate_mechanism("unknown", {}, CONTRACTS)
        with self.assertRaises(ValueError):
            validate_mechanism("layer-cache", {}, {})
        self.assertFalse(self.passed("layer-cache", []))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--historical-root", type=Path)
    options, rest = parser.parse_known_args()
    if options.historical_root:
        directories = dict(zip(CONTRACTS["stages"], ("layer-cache-v1", "visible-clipping-v1", "level-of-detail-v1", "efficient-hit-v1", "incremental-streaming-v1", "render-resource-budgets-v1", "multichart-layout-v1")))
        for stage, directory in directories.items():
            raw = json.loads((options.historical_root / directory / "raw.json").read_text(encoding="utf-8-sig"))
            result = validate_mechanism(stage, raw, CONTRACTS)
            failed = [name for name, passed in result.items() if passed is not True]
            if failed:
                raise AssertionError(f"Historical {stage} failed: {failed}")
            print(f"historical {stage}: {len(result)}/{len(result)} PASS", flush=True)
    unittest.main(argv=[sys.argv[0], *rest])
