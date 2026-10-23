"""Fail-closed, standard-library checks for the seven registered chart mechanisms.

The checked-in contracts pin fixture and check-set shape from the previous C4-07
candidate. They are not regenerated from a candidate report during CI. This oracle
also recomputes resource accounting and numeric path agreement from raw fields;
it does not treat a producer's top-level `passed` flag as sufficient evidence.
"""
from __future__ import annotations

import math
import re
from typing import Any


def _pointer(parent: str, key: str | int) -> str:
    return parent + "/" + str(key).replace("~", "~0").replace("/", "~1")


def _walk(value: Any, path: str = ""):
    yield path, value
    if type(value) is dict:
        for key, child in value.items():
            yield from _walk(child, _pointer(path, key))
    elif type(value) is list:
        for index, child in enumerate(value):
            yield from _walk(child, _pointer(path, index))


def _at(value: Any, path: str):
    for component in path.split("/")[1:]:
        component = component.replace("~1", "/").replace("~0", "~")
        if type(value) is list:
            value = value[int(component)]
        elif type(value) is dict:
            value = value[component]
        else:
            raise KeyError(path)
    return value


def _integer(value: Any) -> bool:
    return type(value) is int and value >= 0


def _number(value: Any) -> bool:
    try:
        return type(value) in (int, float) and math.isfinite(value)
    except OverflowError:
        return False


def _same(actual: Any, expected: Any) -> bool:
    return type(actual) is type(expected) and actual == expected


def _local_budget(value: Any) -> bool:
    if type(value) is not dict:
        return False
    layers = ("SeriesDrawingBytes", "AxisTextBytes", "InteractionTextBytes",
              "AxisPenBytes", "SegmentGeometryBytes")
    counts = ("SeriesDrawingCount", "AxisTextCount", "InteractionTextCount",
              "AxisPenCount", "SegmentGeometryCount", "SegmentProjectionCount")
    required = ("BudgetBytes", "EstimatedBytes", *layers, *counts)
    return (all(_integer(value.get(key)) for key in required)
            and sum(value[key] for key in layers) == value["EstimatedBytes"]
            and value["EstimatedBytes"] <= value["BudgetBytes"]
            and value["SegmentProjectionCount"] <= 4096
            and _same(value.get("ModelVersion"), 1)
            and value.get("NativeMemoryIncluded") is False
            and value.get("SharedIndexCachesIncluded") is False)


def _global_budget(value: Any) -> bool:
    if type(value) is not dict:
        return False
    fields = ("resourceCacheBudgetBytes", "estimatedResourceCacheBytes",
              "backgroundPayloadBudgetBytes", "estimatedBackgroundPayloadBytes",
              "maximumBackgroundJobs", "reservedBackgroundJobs", "runningBackgroundJobs")
    return (all(_integer(value.get(key)) for key in fields)
            and value["estimatedResourceCacheBytes"] <= value["resourceCacheBudgetBytes"]
            and value["estimatedBackgroundPayloadBytes"] <= value["backgroundPayloadBudgetBytes"]
            and value["reservedBackgroundJobs"] <= value["maximumBackgroundJobs"]
            and value["runningBackgroundJobs"] <= 2
            and _same(value.get("modelVersion"), 1)
            and value.get("nativeMemoryIncluded") is False
            and value.get("sharedIndexCachesIncluded") is False)


def validate_mechanism(stage_id: str, raw: dict, contracts: dict) -> dict[str, bool]:
    """Return nonempty literal boolean checks; malformed inputs never pass.

    Unknown or malformed trusted registration is a configuration error. Report
    shape errors become false checks with their JSON pointer in the key so the
    calling guard can retain a useful failure explanation without dropping stages.
    """
    if (type(contracts) is not dict
            or contracts.get("schemaVersion") != "charts-ci-mechanism-contracts-v1"
            or type(contracts.get("stages")) is not dict
            or stage_id not in contracts["stages"]):
        raise ValueError("Unknown mechanism stage or unsupported contract registry")
    contract = contracts["stages"][stage_id]
    if type(contract) is not dict or not contract.get("checksByPath"):
        raise ValueError("A mechanism must register at least one nonempty check dictionary")
    checks: dict[str, bool] = {"raw-is-object": type(raw) is dict}
    if type(raw) is not dict:
        return checks
    checks["schema-is-registered"] = _same(raw.get("schemaVersion"), contract.get("reportSchema"))
    checks["producer-passed-is-boolean-true"] = raw.get("passed") is True
    checks["producer-error-is-present-null"] = "error" in raw and raw["error"] is None
    found = {path: value for path, value in _walk(raw)
             if path.rsplit("/", 1)[-1] == "checks"}
    checks["check-dictionary-paths-exactly-registered"] = set(found) == set(contract["checksByPath"])
    for path, expected_keys in contract["checksByPath"].items():
        value = found.get(path)
        checks[f"registered-check-keys:{path}"] = (
            type(value) is dict and bool(value)
            and sorted(value) == expected_keys)
        checks[f"registered-check-values-boolean-true:{path}"] = (
            type(value) is dict and bool(value)
            and all(type(item) is bool and item is True for item in value.values()))
    for path, expected in contract.get("constantsByPath", {}).items():
        try:
            checks[f"registered-constant:{path}"] = _same(_at(raw, path), expected)
        except (KeyError, ValueError, TypeError, IndexError):
            checks[f"registered-constant:{path}"] = False
    for path, expected_ids in contract.get("scenarioIdsByPath", {}).items():
        try:
            values = _at(raw, path)
            checks[f"registered-scenario-order-and-count:{path}"] = (
                type(values) is list and len(values) == len(expected_ids)
                and all(type(item) is dict and type(item.get("id")) is str for item in values)
                and [item["id"] for item in values] == expected_ids)
        except (KeyError, ValueError, TypeError, IndexError):
            checks[f"registered-scenario-order-and-count:{path}"] = False
    for path, expected_count in contract.get("fixedArrayCountsByPath", {}).items():
        try:
            value = _at(raw, path)
            checks[f"registered-array-count:{path}"] = type(value) is list and len(value) == expected_count
        except (KeyError, ValueError, TypeError, IndexError):
            checks[f"registered-array-count:{path}"] = False
    observed_passed = [(path, value) for path, value in _walk(raw)
                       if path.rsplit("/", 1)[-1] == "passed"]
    checks["all-observed-passed-fields-boolean-true"] = bool(observed_passed) and all(
        value is True for _, value in observed_passed)
    qualification_names = {"formalSlaEvaluated", "actualPresentMeasured", "compositorPresentMeasured",
                           "performanceQualified", "totalMemoryQualified", "memoryLongStabilityQualified",
                           "processPeakOrLongSoakQualified", "pacedThroughputQualified"}
    for name in qualification_names:
        if name in raw:
            checks[f"scope-not-overclaimed:{name}"] = raw[name] is False
    if stage_id == "render-resource-budget":
        found_budgets = {path: value for path, value in _walk(raw)
                         if type(value) is dict and "BudgetBytes" in value}
        expected_paths = contract.get("localBudgetPaths", [])
        checks["local-budget-receipt-paths-exactly-registered"] = bool(expected_paths) and set(found_budgets) == set(expected_paths)
        for path in expected_paths:
            checks[f"raw-five-layer-budget-accounting:{path}"] = _local_budget(found_budgets.get(path))
        try:
            samples = raw["drawing"]["samples"]
            cached = [sample for sample in samples if sample.get("mode") == "cached"]
            disabled = [sample for sample in samples if sample.get("mode") == "disabled"]
            checks["raw-cached-disabled-sample-groups"] = len(cached) == 20 and len(disabled) == 20 and len(samples) == 40
            checks["raw-cached-allocation-limit-and-control-ratio"] = (
                bool(cached) and bool(disabled)
                and all(_integer(sample.get("allocatedBytes")) for sample in samples)
                and all(sample["allocatedBytes"] <= 2 * 1024 * 1024 for sample in cached)
                and sum(sample["allocatedBytes"] for sample in cached) * 2 <= sum(sample["allocatedBytes"] for sample in disabled))
            counters = {"preparedData", "views", "seriesDrawings", "axisTexts", "axisPens", "projectedPoints"}
            checks["raw-cached-work-zero"] = bool(cached) and all(
                type(sample.get("delta")) is dict and set(sample["delta"]) == counters
                and all(type(value) is int and value == 0 for value in sample["delta"].values()) for sample in cached)
        except (KeyError, TypeError, ValueError):
            checks["raw-resource-samples-complete"] = False
    if stage_id == "multichart-background-layout":
        try:
            synchronous, background = raw["synchronousPaths"], raw["backgroundPaths"]
            checks["raw-background-path-parts-match-synchronous"] = (
                type(synchronous) is list and type(background) is list
                and len(synchronous) == len(background) == 4
                and all(type(a) is dict and type(b) is dict
                        and _integer(a.get("partCount")) and a["partCount"] > 0
                        and type(a.get("sha256")) is str and re.fullmatch(r"[A-Fa-f0-9]{64}", a["sha256"]) is not None
                        and _same(a.get("partCount"), b.get("partCount"))
                        and _same(a.get("sha256"), b.get("sha256"))
                        for a, b in zip(synchronous, background)))
            results = raw["retargetResults"]
            checks["raw-retarget-obsolete-obsolete-current"] = (
                type(results) is list and len(results) == 3
                and all(type(value) is bool for value in results)
                and results == [False, False, True])
            hits = raw["hits"]
            checks["raw-original-hit-coordinates-and-indices"] = (
                type(hits) is list and len(hits) == 4
                and all(type(hit) is dict and _integer(hit.get("expectedIndex"))
                        and _same(hit.get("expectedIndex"), hit.get("actualIndex"))
                        and type(hit.get("expectedPoint")) is dict and type(hit.get("actualPoint")) is dict
                        and all(_number(hit["expectedPoint"].get(key))
                                and _number(hit["actualPoint"].get(key))
                                and hit["expectedPoint"][key] == hit["actualPoint"][key] for key in ("x", "y")) for hit in hits))
            budgets = raw["budgetSamples"]
            checks["raw-global-budget-samples-nonempty-and-valid"] = type(budgets) is list and bool(budgets) and all(_global_budget(value) for value in budgets)
            for name in ("hiddenBudget", "reducedCacheBudget", "trimmedBudget", "detachedBudget"):
                checks[f"raw-global-budget-accounting:{name}"] = _global_budget(raw.get(name))
        except (KeyError, TypeError, ValueError):
            checks["raw-multichart-receipts-complete"] = False
    return checks
