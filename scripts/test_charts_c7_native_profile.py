"""Synthetic parser fixtures only: no perf, native process or real capture input."""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("native_profile_parser", Path(__file__).with_name("charts_c7_native_profile.py"))
parser = importlib.util.module_from_spec(spec)
spec.loader.exec_module(parser)


def window(kind="draw", phase="measurement", index=0, start=200, end=300,
           case="paths-n10000-clip-plot", tid=437):
    return {"schema": "charts-c703-native-circle-window/v1", "processId": 430,
            "processStartedUtc": "2026-10-26T00:00:00.1234567Z", "osThreadId": tid,
            "case": case, "kind": kind, "phase": phase, "index": index,
            "clockMonotonic": True,
            "startMonotonicNs": start, "endMonotonicNs": end}


def coordinates_fixture():
    rows = []
    for ordinal, (x, y) in enumerate((("3FF0000000000000", "4000000000000000"), ("4008000000000000", "4010000000000000"))):
        row = {"ordinal": ordinal, "equal": True}
        row.update({prefix + "XBits": x for prefix in ("source", "linked", "production")})
        row.update({prefix + "YBits": y for prefix in ("source", "linked", "production")})
        rows.append(row)
    raw = b"".join(int(row["linked" + axis + "Bits"], 16).to_bytes(8, "little") for row in rows for axis in ("X", "Y"))
    sha = hashlib.sha256(raw).hexdigest().upper()
    comparison = {"schema": "charts-c703-native-coordinate-comparison/v1", "hashEncoding": "xy64le-ordinal", "passed": True,
                  "pointCount": 2, "mismatchCount": 0, "linkedCentersBitsSha256": sha, "productionCentersBitsSha256": sha, "rows": rows}
    probe = {"schema": "charts-c703-native-circle-probe/v3", "fixture": {"count": 2, "centersBitsSha256": sha, "sourcePointsSha256": sha},
             "coordinateQualification": {key: comparison[key] for key in ("passed", "pointCount", "mismatchCount", "linkedCentersBitsSha256", "productionCentersBitsSha256")}}
    probe["coordinateQualification"]["linkedImplementationSource"] = {"path": "/frozen/linked.cs", "bytes": 123, "sha256": "A" * 64}
    return probe, comparison


def save_comparison(root, probe, comparison):
    path = root / "coordinate-comparison.json"
    path.write_text(json.dumps(comparison), encoding="utf-8")
    probe["coordinateQualification"]["comparisonFile"] = {**parser.digest(path), "path": "/mnt/c/foreign-host/run/coordinate-comparison.json"}
    return path


class NativeProfileParserTests(unittest.TestCase):
    def test_v3_full_coordinate_bits_and_cross_host_sibling_pass(self):
        with tempfile.TemporaryDirectory(prefix="parser-fixture-", dir=Path(__file__).resolve().parent) as temporary:
            root = Path(temporary)
            probe, comparison = coordinates_fixture()
            path = save_comparison(root, probe, comparison)
            result, stamp = parser.validate_coordinates(probe, root / "report.json")
            self.assertTrue(result["qualified"])
            self.assertEqual((result["pointCount"], result["mismatchCount"]), (2, 0))
            self.assertEqual(stamp["sha256"], parser.digest(path)["sha256"])
            self.assertEqual(result["linkedCentersBitsSha256"], result["productionCentersBitsSha256"])

    def test_v3_inconsistent_coordinate_proofs_are_rejected(self):
        with tempfile.TemporaryDirectory(prefix="parser-fixture-", dir=Path(__file__).resolve().parent) as temporary:
            root = Path(temporary)
            base_probe, base_comparison = coordinates_fixture()
            mutations = ("failed", "count", "declared-hash", "rows", "ordinal", "equal", "bits", "source-hash", "file-hash", "sidecar-failed")
            for mutation in mutations:
                probe = copy.deepcopy(base_probe); comparison = copy.deepcopy(base_comparison)
                qualification = probe["coordinateQualification"]
                if mutation == "failed": qualification["passed"] = False
                elif mutation == "count": qualification["pointCount"] = 3
                elif mutation == "declared-hash": qualification["productionCentersBitsSha256"] = "B" * 64
                elif mutation == "rows": comparison["rows"].pop()
                elif mutation == "ordinal": comparison["rows"][1]["ordinal"] = 0
                elif mutation == "equal": comparison["rows"][0]["equal"] = False
                elif mutation == "bits": comparison["rows"][0]["linkedXBits"] = "3FF0000000000001"
                elif mutation == "source-hash": probe["fixture"]["sourcePointsSha256"] = "B" * 64
                elif mutation == "sidecar-failed": comparison["passed"] = False
                save_comparison(root, probe, comparison)
                if mutation == "file-hash": qualification["comparisonFile"]["sha256"] = "B" * 64
                with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                    parser.validate_coordinates(probe, root / "report.json")

    def test_observed_smoke_format_preserves_unknown(self):
        sample = parser.parse_sample("437 429.517816087: 10101010 5138e0 [unknown] (/usr/bin/python3.14)")
        self.assertEqual((sample["osThreadId"], sample["monotonicNs"], sample["period"], sample["ip"]),
                         (437, 429517816087, 10101010, "5138e0"))
        self.assertTrue(sample["unknownSymbol"])
        self.assertFalse(sample["unknownDso"])

    def test_demangled_parens_and_deleted_dso(self):
        sample = parser.parse_sample("  437 10.000000007: 99 0xabcdef SkCanvas::drawPath(SkPath const&, SkPaint const&) (/tmp/native dir/libSkiaSharp.so (deleted))")
        self.assertEqual(sample["symbol"], "SkCanvas::drawPath(SkPath const&, SkPaint const&)")
        self.assertEqual(sample["dso"], "/tmp/native dir/libSkiaSharp.so (deleted)")
        self.assertEqual(sample["monotonicNs"], 10000000007)
        self.assertFalse(sample["unknownSymbol"])

    def test_function_with_spaced_parentheses_not_split_as_dso(self):
        sample = parser.parse_sample("437 10.1: 3 abc test::operator() (int, std::function<void ()>) (/tmp/a (b)/native.so)")
        self.assertEqual(sample["symbol"], "test::operator() (int, std::function<void ()>)")
        self.assertEqual(sample["dso"], "/tmp/a (b)/native.so")
        self.assertEqual(sample["monotonicNs"], 10100000000)

    def test_hex_named_function_and_unknown_dso(self):
        self.assertFalse(parser.parse_sample("437 1.0: 9 123 fade (/lib/test.so)")["unknownSymbol"])
        sample = parser.parse_sample("437 1.0: 9 123 [unknown] ([unknown])")
        self.assertTrue(sample["unknownSymbol"] and sample["unknownDso"])

    def test_rejects_wrong_layout_nanoseconds_and_incomplete_dso(self):
        for line in ("437 1.1234567891: 9 123 f (/x)", "437 1.1: 0 123 f (/x)",
                     "437 1.1: 9 123 f (/x", "callchain continuation 123 f (/x)"):
            with self.subTest(line=line), self.assertRaises(ValueError):
                parser.parse_sample(line)

    def test_half_open_boundaries_and_all_excluded_scopes(self):
        rows = [window("draw", "warmup", 0, 10, 20), window("excluded-clear", start=100, end=200),
                window(), window(index=1, start=400, end=500, case="ovals-n10000-clip-plot")]
        index = parser.WindowIndex({437: rows}, "paths-n10000-clip-plot")
        for ns, tid, expected in ((10, 437, "warmup-draw"), (100, 437, "measurement-excluded-clear"),
                                  (199, 437, "measurement-excluded-clear"), (200, 437, "measurement-draw"),
                                  (299, 437, "measurement-draw"), (300, 437, "outside-window"),
                                  (400, 437, "other-case-window"), (250, 999, "other-thread")):
            with self.subTest(ns=ns, tid=tid):
                self.assertEqual(index.classify({"osThreadId": tid, "monotonicNs": ns})[0], expected)

    def test_overlapping_window_is_rejected(self):
        with tempfile.TemporaryDirectory(prefix="parser-fixture-", dir=Path(__file__).resolve().parent) as temporary:
            path = Path(temporary) / "windows.jsonl"
            path.write_text("\n".join(json.dumps(row) for row in (window(), window(index=1, start=299, end=400))))
            with self.assertRaisesRegex(ValueError, "Overlapping"):
                parser.load_windows(path, "charts-c703-native-circle-window/v1")

    def test_contradictory_monotonic_flag_is_rejected(self):
        with tempfile.TemporaryDirectory(prefix="parser-fixture-", dir=Path(__file__).resolve().parent) as temporary:
            path = Path(temporary) / "windows.jsonl"
            row = window()
            row["clockMonotonic"] = False
            path.write_text(json.dumps(row))
            with self.assertRaisesRegex(ValueError, "clockMonotonic"):
                parser.load_windows(path, "charts-c703-native-circle-window/v1")

    def test_loss_text_retained_without_cross_export_sum(self):
        loss = parser.loss_record("PERF_RECORD_LOST id: 12 lost:3", "dump", 9)
        self.assertEqual(loss["parsedLostCount"], 3)
        self.assertIsNone(parser.loss_record("PERF_RECORD_THROTTLE", "dump", 10)["parsedLostCount"])
        self.assertEqual(parser.loss_record("# Total Lost Samples: 0", "leaf", 11)["parsedLostCount"], 0)
        row = parser.loss_record("      LOST_SAMPLES events:         26", "stats", 12, "cpu-clock:u stats")
        self.assertIsNone(row["parsedLostCount"])
        self.assertEqual(row["statistic"]["value"], 26)
        self.assertEqual(row["section"], "cpu-clock:u stats")
        self.assertEqual(parser.loss_record("                LOST events:          2  ( 0.0%)", "stats", 13)["statistic"]["value"], 2)

    def test_end_to_end_synthetic_retains_unknown_unparsed_and_source_failure(self):
        with tempfile.TemporaryDirectory(prefix="parser-fixture-", dir=Path(__file__).resolve().parent) as temporary:
            root = Path(temporary)
            rows = [window("draw", "warmup", 0, 10, 20), window("excluded-clear", start=100, end=200), window()]
            (root / "windows.jsonl").write_text("\n".join(json.dumps(row) for row in rows))
            probe = {"schema": "charts-c703-native-circle-probe/v1", "process": {"pid": 430, "processStartedUtc": rows[0]["processStartedUtc"]},
                     "measurementCompleted": True, "nativeIdentityStable": True, "passed": False, "strictPixelParityPassed": False,
                     "candidatePixelDecision": "synthetic rejection retained",
                     "measurement": {"case": "paths-n10000-clip-plot", "frequency": 1_000_000_000,
                                     "clock": "Linux CLOCK_MONOTONIC nanoseconds", "batches": 1, "osThreadId": 437,
                                     "clearOutsideDrawWindows": True, "allocationReadbackHashAndJsonOutsideDrawWindows": True}}
            (root / "report.json").write_text(json.dumps(probe))
            (root / "leaf.txt").write_text("\n".join([
                "437 0.000000010: 10 abc f (/tmp/native.so)",
                "437 0.000000150: 11 abc clear (/tmp/native.so)",
                "437 0.000000200: 12 abc lost (/tmp/native.so)",
                "437 0.000000201: 13 def [unknown] (/tmp/native.so (deleted))",
                "437 0.000000202: 14 123 [unknown] ([unknown])",
                "437 0.000000300: 15 abc f (/tmp/native.so)",
                "999 0.000000250: 16 abc f (/tmp/native.so)",
                "PERF_RECORD_LOST id: 4 lost 2", "unparseable retained row", "# Total Lost Samples: 0"]))
            (root / "loss.txt").write_text("PERF_RECORD_LOST id: 4 lost 2\nPERF_RECORD_THROTTLE\n")
            args = argparse.Namespace(leaf=root / "leaf.txt", windows=root / "windows.jsonl", probe_report=root / "report.json",
                                      window_schema=rows[0]["schema"], case="paths-n10000-clip-plot", loss_file=root / "loss.txt", output=root / "out")
            result = parser.analyze(args)
            self.assertEqual(result["parsedLeafSamples"], 7)
            self.assertEqual(result["scopes"]["measurement-draw"]["leafSamples"], 3)
            self.assertEqual(result["scopes"]["measurement-draw"]["unknownSymbolLeafSamples"], 2)
            self.assertEqual(result["scopes"]["measurement-draw"]["periodDistribution"]["sum"], 39)
            self.assertEqual(result["unparsedLines"], 1)
            self.assertFalse(result["parsingQualified"])
            self.assertFalse(result["probeCorrectness"]["passed"])
            self.assertEqual(len(result["lossEvidence"]["records"]), 4)
            self.assertTrue((args.output / "unparsed-lines.jsonl").read_text().find("unparseable retained row") >= 0)
            self.assertEqual(len((args.output / "classified-leaf-samples.jsonl").read_text().splitlines()), 7)
            with self.assertRaisesRegex(ValueError, "fresh"):
                parser.analyze(args)
            probe.update(schema="charts-c703-native-circle-probe/v2", graphicsInitRequested=True,
                         graphicsInitCalled=True, graphicsInitCompleted=True, graphicsInitPixelParityPassed=True,
                         pathsOvalsPixelParityPassed=False, nativeIdentityAfterGraphicsInit={"qualified": True})
            (root / "report.json").write_text(json.dumps(probe))
            args.output = root / "out-v2"
            second = parser.analyze(args)
            self.assertTrue(second["probeCorrectness"]["graphicsInitPixelParityPassed"])
            self.assertFalse(second["probeCorrectness"]["pathsOvalsPixelParityPassed"])
            self.assertEqual(second["nativeIdentityFromProbe"]["afterGraphicsInit"], {"qualified": True})


if __name__ == "__main__":
    unittest.main(verbosity=2)
