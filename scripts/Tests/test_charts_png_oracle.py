"""Independent small fixtures for the native chart PNG oracle (stdlib unittest)."""
from __future__ import annotations

import hashlib
from pathlib import Path
import random
import struct
import sys
import tempfile
import unittest
import zlib

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import charts_png_oracle as png


def chunk(kind: bytes, data: bytes) -> bytes:
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(data, zlib.crc32(kind)) & 0xFFFFFFFF)


def encode(width: int, height: int, rows: bytes, color: int = 6, filters: tuple[int, ...] = (0,)) -> bytes:
    bpp = 4 if color == 6 else 3
    previous = bytes(width * bpp)
    encoded = bytearray()
    for y in range(height):
        row = rows[y * width * bpp : (y + 1) * width * bpp]
        method = filters[y % len(filters)]
        encoded.append(method)
        for i, value in enumerate(row):
            a = row[i - bpp] if i >= bpp else 0
            b = previous[i]
            c = previous[i - bpp] if i >= bpp else 0
            if method == 0:
                prediction = 0
            elif method == 1:
                prediction = a
            elif method == 2:
                prediction = b
            elif method == 3:
                prediction = (a + b) // 2
            else:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                prediction = a if pa <= pb and pa <= pc else b if pb <= pc else c
            encoded.append((value - prediction) & 255)
        previous = row
    return png.PNG_SIGNATURE + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, color, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(encoded)) + chunk(b"IEND", b"")


class OracleTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def file(self, data: bytes, name: str = "sample.png") -> Path:
        path = self.root / name
        path.write_bytes(data)
        return path

    def test_rgba_each_filter_round_trip_with_boundary_carries(self):
        randomizer = random.Random(41)
        raw = bytes(randomizer.randrange(256) for _ in range(17 * 11 * 4))
        for method in range(5):
            with self.subTest(filter=method):
                self.assertEqual((17, 11, raw), png.decode_png(self.file(encode(17, 11, raw, filters=(method,)))))

    def test_rgb_odd_row_and_mixed_filters_expand_opaque_rgba(self):
        randomizer = random.Random(9)
        raw = bytes(randomizer.randrange(256) for _ in range(7 * 9 * 3))
        expected = b"".join(raw[i:i + 3] + b"\xff" for i in range(0, len(raw), 3))
        self.assertEqual((7, 9, expected), png.decode_png(self.file(encode(7, 9, raw, color=2, filters=(0, 1, 2, 3, 4)))))

    def test_bad_crc_and_truncation_are_rejected(self):
        valid = encode(2, 2, b"\xff" * 16)
        corrupted = bytearray(valid)
        corrupted[29] ^= 1
        with self.assertRaises(png.PngError):
            png.decode_png(self.file(corrupted))
        for trim in (1, 8, 20):
            with self.subTest(trim=trim), self.assertRaises(png.PngError):
                png.decode_png(self.file(valid[:-trim]))

    def test_extra_idat_stream_and_trailing_bytes_rejected(self):
        header = png.PNG_SIGNATURE + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0))
        first = zlib.compress(b"\x00\xff\xff\xff\xff")
        for compressed in (first + zlib.compress(b"garbage"), first + b"trailing"):
            with self.assertRaises(png.PngError):
                png.decode_png(self.file(header + chunk(b"IDAT", compressed) + chunk(b"IEND", b"")))
        with self.assertRaises(png.PngError):
            png.decode_png(self.file(encode(1, 1, b"\xff" * 4) + b"x"))

    def test_noncontiguous_idat_and_duplicate_ihdr_rejected(self):
        header_chunk = chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0))
        compressed = zlib.compress(b"\x00\xff\xff\xff\xff")
        invalid = png.PNG_SIGNATURE + header_chunk + chunk(b"IDAT", compressed[:3]) + chunk(b"tEXt", b"key\x00value") + chunk(b"IDAT", compressed[3:]) + chunk(b"IEND", b"")
        with self.assertRaises(png.PngError):
            png.decode_png(self.file(invalid))
        with self.assertRaises(png.PngError):
            png.decode_png(self.file(png.PNG_SIGNATURE + header_chunk + header_chunk + chunk(b"IDAT", compressed) + chunk(b"IEND", b"")))

    def test_dimensions_interlace_and_inflation_limit_rejected(self):
        for width, height, interlace in ((0, 1, 0), (png.MAX_DIMENSION + 1, 1, 0), (8192, 8192, 0), (1, 1, 1)):
            with self.subTest(width=width, height=height, interlace=interlace), self.assertRaises(png.PngError):
                header = chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, interlace))
                png.decode_png(self.file(png.PNG_SIGNATURE + header + chunk(b"IDAT", zlib.compress(b"\x00" * 5)) + chunk(b"IEND", b"")))
        header = chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0))
        with self.assertRaises(png.PngError):
            png.decode_png(self.file(png.PNG_SIGNATURE + header + chunk(b"IDAT", zlib.compress(b"\x00" * 1_000_000)) + chunk(b"IEND", b"")))

    def test_unsupported_semantic_chunks_and_filter_rejected(self):
        header = png.PNG_SIGNATURE + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0))
        for special in (chunk(b"tRNS", b"\x00\x00"), chunk(b"acTL", b"\x00" * 8), chunk(b"ABCD", b"")):
            with self.assertRaises(png.PngError):
                png.decode_png(self.file(header + special + chunk(b"IDAT", zlib.compress(b"\x00" * 5)) + chunk(b"IEND", b"")))
        with self.assertRaises(png.PngError):
            png.decode_png(self.file(header + chunk(b"IDAT", zlib.compress(b"\x05" + b"\xff" * 4)) + chunk(b"IEND", b"")))

    def test_sha_bgra_opaque_and_alpha_premultiplication(self):
        rgba = bytes((10, 20, 30, 255, 250, 100, 50, 128, 255, 255, 255, 0))
        expected = bytes((30, 20, 10, 255, 25, 50, 125, 128, 0, 0, 0, 0))
        self.assertEqual(hashlib.sha256(expected).hexdigest().upper(), png.sha_bgra(rgba))
        self.assertEqual(hashlib.sha256(bytes((30, 20, 10, 255))).hexdigest().upper(), png.sha_bgra(rgba[:4]))
        with self.assertRaises(ValueError):
            png.sha_bgra(b"bad")

    def test_blue_rois_exclude_axis_only_and_respect_fractional_edges(self):
        white = bytes((255, 255, 255, 255))
        blue = bytes((21, 92, 193, 255))
        raw = bytearray(white * 25)
        for x, y in ((0, 0), (2, 2), (3, 3), (4, 4)):
            raw[(y * 5 + x) * 4:(y * 5 + x + 1) * 4] = blue
        self.assertEqual((4, [2, 0, 4]), png.blue_counts(5, 5, bytes(raw), [[1.2, 1.2, 2.8, 2.8], [1, 0, 3, 1], [-1, -1, 7, 7]]))
        for bad in ([0, 0, 0, 5], [float("nan"), 0, 1, 1], [0, 0, 1], [False, 0, 1, 1]):
            with self.assertRaises(ValueError):
                png.blue_counts(5, 5, bytes(raw), [bad])

    def test_blue_tolerance_and_alpha_boundaries(self):
        colors = [(0, 68, 169, 250), (45, 116, 217, 255), (46, 92, 193, 255), (21, 92, 193, 249)]
        rgba = bytes(v for color in colors for v in color)
        self.assertEqual((2, [2]), png.blue_counts(4, 1, rgba, [[0, 0, 4, 1]]))

    def test_comparison_creates_nonempty_exact_diff_and_refuses_overwrite(self):
        baseline = self.file(encode(2, 1, bytes((10, 20, 30, 255, 40, 50, 60, 255))), "baseline.png")
        candidate = self.file(encode(2, 1, bytes((11, 20, 30, 255, 40, 50, 60, 255))), "candidate.png")
        out = self.root / "diff.png"
        result = png.compare_png(candidate, baseline, out)
        self.assertEqual((True, 1, 2, 1), (result["dimensionsEqual"], result["differentPixels"], result["pixelCount"], result["maxChannelDifference"]))
        self.assertEqual((2, 1, bytes((1, 0, 1, 255, 0, 0, 0, 255))), png.decode_png(out))
        with self.assertRaises(FileExistsError):
            png.compare_png(candidate, baseline, out)

    def test_identical_and_dimension_mismatch_artifacts(self):
        first = self.file(encode(1, 1, bytes((21, 92, 193, 255))), "first.png")
        same = png.compare_png(first, first, self.root / "same.png")
        self.assertEqual((True, 0, 0), (same["dimensionsEqual"], same["differentPixels"], same["maxChannelDifference"]))
        second = self.file(encode(2, 1, b"\xff" * 8), "second.png")
        mismatch = png.compare_png(first, second, self.root / "size.png")
        self.assertEqual((False, 2, 2, 255), (mismatch["dimensionsEqual"], mismatch["differentPixels"], mismatch["pixelCount"], mismatch["maxChannelDifference"]))
        self.assertEqual((1, 1, b"\xff\x00\xff\xff"), png.decode_png(self.root / "size.png"))


if __name__ == "__main__":
    unittest.main()
