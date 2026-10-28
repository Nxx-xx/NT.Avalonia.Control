"""Independent pixel corruption and protocol mutation checks; no .NET needed."""
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch
import zlib

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import charts_export_stability_oracle as oracle


def chunk(kind, value):
    return struct.pack(">I", len(value)) + kind + value + struct.pack(">I", zlib.crc32(kind + value) & 0xffffffff)


def png(width, height, rgba):
    rows = b"".join(b"\0" + rgba[y * width * 4:(y + 1) * width * 4] for y in range(height))
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")


class ExportOracleTests(unittest.TestCase):
    def setUp(self):
        self.fixture_parent = (Path(__file__).resolve().parents[2] / "artifacts/charts-export-stability-ci/oracle-fixtures").resolve()
        self.fixture_parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="fixture-",dir=self.fixture_parent)
        self.root = Path(self.temp.name).resolve()
        self.assertEqual(self.fixture_parent,self.root.parent)
        (self.root / "images").mkdir()
        self.points = 100_000

    def tearDown(self):
        # Verify the exact generated target before recursive fixture cleanup.
        self.assertEqual(self.fixture_parent,self.root.resolve().parent)
        self.temp.cleanup()

    def fixture(self, index=0, blank=False, bridge=False):
        contract = oracle.schedule()[index]
        name, view, scale, transparent, sampled, asynchronous, jpeg, canceled, screen = contract
        center = self.points // 3 + 1 if view == "peak" else self.points // 2
        low, high = (0, self.points - 1) if view == "overview" else (center - 24, center + 24)
        selected = self.points // 3 - 7 if view == "peak" else self.points // 2 - 8 if view == "gap" else min(23_457, self.points // 4)
        row = {
            **dict(zip(("name","view","scale","transparent","sampled","asynchronous","jpeg","precanceled","screen"),contract)),
            "id":index,"round":0,"expectedSelectedIndex":selected,"expectedStableId":f"sample-{selected}",
            "beforeScreenPlan":{"sourceCount":self.points},"afterScreenPlan":{"sourceCount":self.points},"passed":True,"exportError":None,
            "plot":{"x":70,"y":40,"width":1030,"height":280},
            "range":{"xMinimum":low,"xMaximum":high,"yMinimum":-110,"yMaximum":110},
            "guards":{key:True for key in oracle.expected_guard_names(contract)},
        }
        if canceled:
            row.update(image=None,rasterProof=None)
            return row, None, None
        width,height=int(1120*scale),int(390*scale)
        regions=oracle.projected_regions(row,self.points)
        rgba=bytearray((b"\0\0\0\0" if transparent else b"\xff\xff\xff\xff")*(width*height))

        def paint(region):
            x,y,w,h=region
            for py in range(max(0,math.ceil(y)),min(height,math.floor(y+h))):
                for px in range(max(0,math.ceil(x)),min(width,math.floor(x+w))):
                    rgba[(py*width+px)*4:(py*width+px)*4+4]=bytes((67,133,244,128 if transparent else 255))
        if not blank:
            if view in ("overview","peak"):
                paint(regions["peakPixelRegion"])
            if view=="overview":
                paint(regions["valleyPixelRegion"])
            if view=="gap":
                paint(regions["gapLeftOriginalPointRegion"])
                paint(regions["gapRightOriginalPointRegion"])
        if bridge:
            paint(regions["gapPixelRegion"])
        filename=f"{index:05}-r000-{name}.png"
        image=self.root/"images"/filename
        encoded=png(width,height,bytes(rgba));image.write_bytes(encoded)
        row["image"]=str(image)
        row["rasterProof"]={"imageSha256":hashlib.sha256(encoded).hexdigest().upper(),"encodedBytes":len(encoded),"raster":{
            **{key:dict(zip(("x","y","width","height"),regions[key])) for key in ("peakPixelRegion","valleyPixelRegion","gapPixelRegion")},
            "peakInk":999,"valleyInk":999,"gapInk":0,"cornerAlpha":0,
        }}
        return row,image,rgba

    def evaluate(self,row):
        return oracle.verify_attempt(self.root,row,row["id"],self.points)

    def test_schedule_is_fixed_and_matches_all_17_modes(self):
        rows=oracle.schedule()
        self.assertEqual(17,len(rows));self.assertEqual(17,len({row[0] for row in rows}))
        self.assertEqual(4,sum(row[4] for row in rows));self.assertEqual(1,sum(row[7] for row in rows))
        self.assertEqual(1,sum(row[6] for row in rows));self.assertEqual(1,sum(row[8] for row in rows))

    def test_halfscale_straight_rgba_preserves_independent_extrema(self):
        row,_,_=self.fixture()
        self.assertTrue(self.evaluate(row)["passed"])

    def test_blank_png_cannot_pass_with_forged_producer_ink_and_pass_flags(self):
        row,_,_=self.fixture(blank=True)
        result=self.evaluate(row)
        self.assertFalse(result["passed"])
        self.assertFalse(result["checks"]["original-single-sample-peak-visible"])
        self.assertFalse(result["checks"]["original-single-sample-valley-visible"])

    def test_cropped_gap_requires_real_signal_on_both_sides(self):
        row,_,_=self.fixture(index=13)
        self.assertTrue(self.evaluate(row)["passed"])
        blank,_,_=self.fixture(index=13,blank=True)
        self.assertFalse(self.evaluate(blank)["checks"]["original-signal-visible-on-both-sides-of-gap"])

    def test_fabricated_gap_bridge_fails_independent_pixels(self):
        row,_,_=self.fixture(index=13,bridge=True)
        result=self.evaluate(row)
        self.assertFalse(result["passed"])
        self.assertFalse(result["checks"]["no-fabricated-line-across-original-nan-gap"])

    def test_tampered_producer_region_does_not_move_the_independent_quality_roi(self):
        row,_,_=self.fixture()
        row["rasterProof"]["raster"]["peakPixelRegion"]["x"]+=100
        result=self.evaluate(row)
        self.assertFalse(result["checks"]["producer-region-metadata-matches-independent-projection"])
        self.assertTrue(result["checks"]["original-single-sample-peak-visible"])

    def test_original_export_cannot_be_relabeled_sampled(self):
        row,_,_=self.fixture()
        row["sampled"]=True
        self.assertFalse(self.evaluate(row)["checks"]["fixed-scenario-and-original-versus-sampled-options"])

    def test_boolean_cannot_replace_an_integer_attempt_id(self):
        row,_,_=self.fixture()
        row["id"]=False
        result=oracle.verify_attempt(self.root,row,0,self.points)
        self.assertFalse(result["checks"]["sequential-id-and-round"])

    def test_missing_identity_assertion_cannot_be_replaced_with_invented_true_flag(self):
        row,_,_=self.fixture()
        del row["guards"]["original-selection-identity"]
        row["guards"]["fake-identity"]=True
        self.assertFalse(self.evaluate(row)["checks"]["producer-assertions-present-and-all-true"])

    def test_changed_viewport_is_rejected_before_pixel_queries(self):
        row,_,_=self.fixture()
        row["range"]["xMaximum"]-=1
        with self.assertRaises(ValueError):self.evaluate(row)

    def test_crc_corruption_fails_even_after_rehashing_encoded_producer_proof(self):
        row,image,_=self.fixture()
        payload=bytearray(image.read_bytes());payload[-1]^=1;image.write_bytes(payload)
        row["rasterProof"]["imageSha256"]=hashlib.sha256(payload).hexdigest().upper()
        with self.assertRaises(ValueError):self.evaluate(row)

    def test_precancel_requires_no_image_and_immutable_source_plan(self):
        row,_,_=self.fixture(index=16)
        self.assertTrue(self.evaluate(row)["passed"])
        row["image"]="forged.png"
        self.assertFalse(self.evaluate(row)["checks"]["canceled-attempt-produces-no-image"])

    def test_image_identity_cannot_redirect_reads_to_an_arbitrary_file(self):
        row,_,_=self.fixture()
        row["image"]="../../elsewhere/not-the-expected-image.png"
        self.assertFalse(self.evaluate(row)["checks"]["expected-image-identity"])

    def test_straight_rgba_is_not_unpremultiplied_twice(self):
        self.assertEqual(1,oracle.count_blue(1,1,bytes((67,133,244,16)),(0,0,1,1),16))
        self.assertEqual(0,oracle.count_blue(1,1,bytes((67,133,244,15)),(0,0,1,1),16))

    def test_jpeg_dimensions_are_structural_only_and_truncation_rejected(self):
        payload=b"\xff\xd8\xff\xc0"+struct.pack(">H B HH B",11,8,390,1120,1)+b"\x01\x11\x00\xff\xd9"
        self.assertEqual((1120,390),oracle.jpeg_size(payload))
        with self.assertRaises(ValueError):oracle.jpeg_size(payload[:-1])

    def test_incomplete_native_crash_evidence_cannot_pass(self):
        protocol={"options":{"rounds":1,"points":self.points,"forceGc":True},"fixture":{"count":self.points,"finiteVertices":self.points-5,"continuousFigures":2,"exactOverviewLineSegments":self.points-7}}
        (self.root/"protocol.json").write_text(json.dumps(protocol),encoding="utf-8")
        (self.root/"events.jsonl").write_text(json.dumps({"phase":"attempt-start","id":0})+"\n",encoding="utf-8")
        (self.root/"attempts").mkdir()
        result=oracle.verify(self.root,1,self.points,True)
        self.assertFalse(result["passed"])
        self.assertTrue(any("final result missing" in error for error in result["errors"]))

    def test_final_summary_requires_the_complete_attempt_array(self):
        # Isolate summary reconciliation from pixel quality, which the other
        # tests cover using actual PNG bytes. All external receipts remain
        # complete while only the managed summary array is corrupted.
        rows=[{"id":index} for index in range(17)]
        protocol={"options":{"rounds":1,"points":self.points,"forceGc":True},"fixture":{"count":self.points,"finiteVertices":self.points-5,"continuousFigures":2,"exactOverviewLineSegments":self.points-7}}
        (self.root/"protocol.json").write_text(json.dumps(protocol),encoding="utf-8")
        events=[{"phase":phase,"id":index,"passed":True} for index in range(17) for phase in ("attempt-start","attempt-end")]
        (self.root/"events.jsonl").write_text("\n".join(json.dumps(event) for event in events)+"\n",encoding="utf-8")
        (self.root/"attempts").mkdir()
        for index,row in enumerate(rows):
            (self.root/"attempts"/f"{index:05}.json").write_text(json.dumps(row),encoding="utf-8")
        for index in range(16):
            (self.root/"images"/f"{index:05}.png").write_bytes(b"")
        summary={"passed":True,"expectedAttempts":17,"actualAttempts":17,"attempts":rows,"forcedGcWorkerStopped":True,"errors":[],"forcedCollections":1,"collectionsBeginningDuringManagedCapture":1}
        result_path=self.root/"results.json"
        def evaluated(_root,row,index,_points,_cache):
            return {"id":index,"passed":True,"checks":{},"png":False}
        with patch.object(oracle,"verify_attempt",side_effect=evaluated):
            result_path.write_text(json.dumps(summary),encoding="utf-8")
            self.assertTrue(oracle.verify(self.root,1,self.points,True)["passed"])
            for label,value in (("missing",None),("null",None),("object",{}),("empty",[]),("truncated",rows[:-1]),("extra",rows+[rows[-1]])):
                with self.subTest(label=label):
                    mutated={**summary,"attempts":value}
                    if label=="missing":del mutated["attempts"]
                    result_path.write_text(json.dumps(mutated),encoding="utf-8")
                    result=oracle.verify(self.root,1,self.points,True)
                    self.assertFalse(result["passed"])
                    self.assertTrue(any("Managed final attempts array" in error for error in result["errors"]))


if __name__=="__main__":
    unittest.main()
