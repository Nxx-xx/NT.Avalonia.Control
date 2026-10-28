import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("geometry", Path(__file__).resolve().parents[1] / "verify_treedatagrid_geometry.py")
GUARD = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GUARD)


class GeometryGuardTests(unittest.TestCase):
    def setUp(self):
        base = (Path(__file__).resolve().parents[2] / "artifacts/treedatagrid-geometry-guard-tests").resolve()
        base.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=base)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.assertTrue(self.root.resolve().is_relative_to(base))
        self.initial = {"protocol": "treedatagrid-native-geometry-v1", "native": True, "hostSha256": "host", "themeSha256": "theme",
                        "functionSha256": "after", "ClientSize": [1000, 650], "RenderScaling": 1}
        self.report = {"protocol": self.initial["protocol"], "passed": True, "error": None, "differences": [], "frames": 1128, "snapshots": 2256, "expectedSnapshots": 1128}
        geometry = {"offset": [0, 0], "extent": [11000, 280000], "rows": [{"RowIndex": 0, "height": 456, "desired": [998, 456], "bounds": [0, 0, 998, 456],
                    "cells": [{"ColumnIndex": 0, "desired": [110, 456], "bounds": [0, 0, 110, 456], "text": None, "template": []}]}]}
        self.rows = [{"plan": plan, "round": round_index, "step": step, "phase": phase, "scenario": scenario, "geometry": geometry}
                     for plan, round_index, step, phase in sorted(GUARD.expected_keys()) for scenario in ("auto-default", "auto-factory")]

    def write(self, root):
        root.mkdir(exist_ok=True)
        (root / "initial.json").write_text(json.dumps(self.initial), encoding="utf-8")
        (root / "report.json").write_text(json.dumps(self.report), encoding="utf-8")
        (root / "geometry.jsonl").write_text("\n".join(map(json.dumps, self.rows)), encoding="utf-8")

    def result(self):
        self.write(self.root)
        return GUARD.verify(self.root)

    def test_complete_native_geometry_is_valid(self):
        self.assertTrue(self.result()["passed"])

    def test_missing_raw_record_is_rejected(self):
        self.rows.pop(); self.assertFalse(self.result()["passed"])

    def test_duplicate_cannot_replace_a_missing_record(self):
        self.rows[-1] = self.rows[-2]; self.assertFalse(self.result()["passed"])

    def test_summary_pass_cannot_hide_a_geometry_difference(self):
        self.rows[0] = copy.deepcopy(self.rows[0]); self.rows[0]["geometry"]["offset"][1] = 428
        self.assertFalse(self.result()["passed"])

    def test_nonfinite_geometry_is_rejected(self):
        self.rows[0]["geometry"]["extent"][0] = float("nan")
        self.assertFalse(self.result()["passed"])

    def test_missing_terminal_report_is_rejected(self):
        self.write(self.root); (self.root / "report.json").unlink()
        self.assertFalse(GUARD.verify(self.root)["passed"])

    def test_binary_pair_must_use_identical_host_and_theme(self):
        baseline = self.root / "baseline"; self.write(baseline)
        self.initial.update(functionSha256="before", hostSha256="other")
        self.write(self.root)
        self.assertFalse(GUARD.verify(self.root, baseline)["passed"])

    def test_same_function_cannot_be_claimed_as_a_binary_pair(self):
        baseline = self.root / "baseline"; self.write(baseline); self.write(self.root)
        self.assertFalse(GUARD.verify(self.root, baseline)["passed"])

    def test_valid_binary_pair_is_accepted(self):
        baseline = self.root / "baseline"; self.write(baseline)
        self.initial["functionSha256"] = "before"; self.write(self.root)
        self.assertTrue(GUARD.verify(self.root, baseline)["passed"])
