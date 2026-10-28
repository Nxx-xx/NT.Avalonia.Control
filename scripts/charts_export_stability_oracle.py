"""Independent file/PNG quality gate for the fixed native exact-export protocol.

PNG bytes are decoded by the bounded stdlib decoder, not Avalonia. Pixel regions
are derived again from the deterministic fixture and recorded plot/range. The
producer's pixel counts and pixel-pass booleans never decide this oracle's result.
"""
from __future__ import annotations

import argparse
from collections import OrderedDict
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

from charts_png_oracle import decode_png


def schedule():
    """Stable 17-attempt contract: name, view, scale, transparency, sampling,
    async, JPEG, precancellation, original screen capture.
    """
    rows = [
        ("cold-overview-0.5x-transparent", "overview", .5, True, False, False, False, False, False),
        ("cold-overview-1.0x-opaque", "overview", 1, False, False, False, False, False, False),
        ("cold-overview-2.0x-opaque", "overview", 2, False, False, False, False, False, False),
        ("history-screen-original", "overview", 1, False, False, False, False, False, True),
        ("history-gap-exact-2x", "gap", 2, False, False, False, False, False, False),
        ("history-gap-sampled-2x", "gap", 2, False, True, False, False, False, False),
    ]
    for scale in (1, 2):
        for sampled in (False, True):
            rows.append((f"history-overview-{'sampled' if sampled else 'exact'}-{scale:.1f}x", "overview", scale, False, sampled, False, False, False, False))
    rows.extend([
        ("warm-overview-exact-half-transparent", "overview", .5, True, False, False, False, False, False),
        ("warm-overview-sampled-half-transparent", "overview", .5, True, True, False, False, False, False),
        ("cropped-peak-exact-half-transparent", "peak", .5, True, False, False, False, False, False),
        ("cropped-gap-exact-half-transparent", "gap", .5, True, False, False, False, False, False),
        ("async-overview-exact-half-transparent-png", "overview", .5, True, False, True, False, False, False),
        ("async-overview-exact-opaque-jpeg", "overview", 1, False, False, True, True, False, False),
        ("precanceled-async-png", "overview", 1, False, False, True, False, True, False),
    ])
    assert len(rows) == 17
    return rows


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


class PngCache:
    """Bounded byte-identical frame reuse; every file's encoded SHA is checked."""
    def __init__(self):
        self.frames = OrderedDict()
        self.bytes = 0
        self.decodes = 0

    def decode(self, path, encoded_sha):
        if encoded_sha in self.frames:
            self.frames.move_to_end(encoded_sha)
            return self.frames[encoded_sha]
        frame = decode_png(path)
        self.decodes += 1
        weight = len(frame[2])
        while self.frames and self.bytes + weight > 64 * 1024 * 1024:
            _, retired = self.frames.popitem(last=False)
            self.bytes -= len(retired[2])
        if weight <= 64 * 1024 * 1024:
            self.frames[encoded_sha] = frame
            self.bytes += weight
        return frame


def load(path):
    def reject(value):
        raise ValueError(f"Nonfinite JSON value {value}")
    return json.loads(path.read_text(encoding="utf-8-sig"), parse_constant=reject)


def near(a, b):
    return type(a) in (int, float) and math.isfinite(a) and abs(a - b) <= 1e-7 * max(1, abs(b))


def rect(value):
    result = tuple(value[key] for key in ("x", "y", "width", "height"))
    if not all(type(v) in (int, float) and math.isfinite(v) for v in result):
        raise ValueError("Nonfinite rectangle")
    return result


def projected_regions(row, points):
    x, y, width, height = rect(row["plot"])
    if not (0 <= x and 0 <= y and width > 700 and height > 200 and x + width <= 1120.001 and y + height <= 390.001):
        raise ValueError("Plot is outside the fixed 1120x390 DIP chart")
    view, scale = row["view"], row["scale"]
    if view == "overview":
        expected = (0, points - 1, -110, 110)
    else:
        center = points // 3 + 1 if view == "peak" else points // 2
        expected = (center - 24, center + 24, -110, 110)
    keys = ("xMinimum", "xMaximum", "yMinimum", "yMaximum")
    if not all(near(row["range"][key], value) for key, value in zip(keys, expected)):
        raise ValueError("Recorded viewport differs from the fixed scenario")
    low, high, bottom, top = expected

    def point(index, value):
        return (x + (index - low) / (high - low) * width, y + height - (value - bottom) / (top - bottom) * height)

    def region(index, value):
        px, py = point(index, value)
        return ((px - 3) * scale, (py - 3) * scale, 6 * scale, 6 * scale)

    gap_x, _ = point(points // 2, 0)
    regions = {
        "peakPixelRegion": region(points // 3 + 1, 95),
        "valleyPixelRegion": region(points * 2 // 3 + 1, -95),
        "gapPixelRegion": ((gap_x - 5) * scale, (y + 4) * scale, 10 * scale, (height - 8) * scale),
    }
    for side, index in (("gapLeftOriginalPointRegion", points // 2 - 12), ("gapRightOriginalPointRegion", points // 2 + 12)):
        value = 14 * math.sin(index * .0123) + 3 * math.sin(index * .173)
        regions[side] = region(index, value)
    return regions


def expected_guard_names(contract):
    keys = {"native-window-visible", "original-source-count", "original-selection-identity", "screen-plan-preserved-by-export", "no-unexpected-export-failure-event", "linear-no-marker-fixture-contract", "source-extrema-and-gap-original-identities"}
    if contract[7]:
        return keys | {"pre-cancel-throws-without-output"}
    keys |= {"export-returned-true", "format-signature", "decoded-pixel-size"}
    if not contract[6]:
        if contract[1] in ("overview", "peak"):
            keys.add("peak-extremum-ink")
        if contract[1] == "overview":
            keys.add("valley-extremum-ink")
        if contract[1] == "gap":
            keys.add("gap-no-invented-bridge")
        if contract[3]:
            keys.add("transparent-corner-alpha-zero")
    return keys


def count_blue(width, height, rgba, region, alpha_minimum):
    # PNG decoder returns straight RGBA. Do not unpremultiply these pixels again.
    x, y, w, h = region
    count = 0
    for py in range(max(0, math.ceil(y)), min(height, math.floor(y + h))):
        for px in range(max(0, math.ceil(x)), min(width, math.floor(x + w))):
            offset = (py * width + px) * 4
            if rgba[offset + 3] >= alpha_minimum and all(abs(rgba[offset + channel] - color) <= 24 for channel, color in enumerate((67, 133, 244))):
                count += 1
    return count


def jpeg_size(payload):
    """Read JPEG SOF dimensions; this is not a decoder/visual-quality claim."""
    if not payload.startswith(b"\xff\xd8") or not payload.endswith(b"\xff\xd9"):
        raise ValueError("JPEG SOI/EOI missing")
    offset = 2
    while offset < len(payload) - 2:
        if payload[offset] != 255:
            raise ValueError("Malformed JPEG marker")
        while offset < len(payload) and payload[offset] == 255:
            offset += 1
        if offset >= len(payload):
            break
        marker = payload[offset]
        offset += 1
        if marker in (0xD8, 0xD9) or 0xD0 <= marker <= 0xD7:
            continue
        if offset + 2 > len(payload):
            raise ValueError("Truncated JPEG length")
        length = struct.unpack_from(">H", payload, offset)[0]
        if length < 2 or offset + length > len(payload):
            raise ValueError("Truncated JPEG segment")
        if marker in (0xC0, 0xC1, 0xC2):
            if length < 8 or payload[offset + 2] != 8 or payload[offset + 7] not in (1,3,4) or length != 8 + 3 * payload[offset + 7]:
                raise ValueError("Unsupported JPEG frame")
            height, width = struct.unpack_from(">HH", payload, offset + 3)
            if width <= 0 or height <= 0:
                raise ValueError("Empty JPEG frame")
            return width, height
        if marker == 0xDA:
            break
        offset += length
    raise ValueError("JPEG dimensions not found")


def verify_attempt(root, row, expected_id, points, decoder_cache=None):
    checks = {}
    contract = schedule()[expected_id % 17]
    names = ("name", "view", "scale", "transparent", "sampled", "asynchronous", "jpeg", "precanceled", "screen")
    checks["fixed-scenario-and-original-versus-sampled-options"] = all(row.get(key) == value and (not isinstance(value, bool) or isinstance(row.get(key), bool)) for key, value in zip(names, contract))
    checks["sequential-id-and-round"] = type(row.get("id")) is int and type(row.get("round")) is int and row.get("id") == expected_id and row.get("round") == expected_id // 17
    checks["producer-assertions-present-and-all-true"] = isinstance(row.get("guards"), dict) and set(row["guards"]) == expected_guard_names(contract) and all(value is True for value in row["guards"].values()) and row.get("passed") is True
    selected = points // 3 - 7 if contract[1] == "peak" else points // 2 - 8 if contract[1] == "gap" else min(23_457, points // 4)
    checks["deterministic-original-selected-id"] = row.get("expectedSelectedIndex") == selected and row.get("expectedStableId") == f"sample-{selected}"
    checks["screen-plan-not-replaced"] = row.get("beforeScreenPlan") == row.get("afterScreenPlan") and row.get("beforeScreenPlan", {}).get("sourceCount") == points
    checks["no-export-exception"] = row.get("exportError") is None
    regions = projected_regions(row, points)
    if contract[7]:
        checks["canceled-attempt-produces-no-image"] = row.get("image") is None and row.get("rasterProof") is None
        return {"id": expected_id, "name": contract[0], "passed": all(checks.values()), "checks": checks, "png": False}

    suffix = ".jpg" if contract[6] else ".png"
    expected_name = f"{expected_id:05}-r{expected_id // 17:03}-{contract[0]}{suffix}"
    # Read only the expected local file. Platform-specific absolute paths in the
    # producer are identity metadata and never become arbitrary file reads.
    declared_name = str(row.get("image", "")).replace("\\", "/").rsplit("/", 1)[-1]
    checks["expected-image-identity"] = declared_name == expected_name
    image = root / "images" / expected_name
    payload = image.read_bytes()
    proof = row["rasterProof"]
    checks["encoded-file-sha-and-size"] = proof.get("imageSha256") == hashlib.sha256(payload).hexdigest().upper() and proof.get("encodedBytes") == len(payload)
    raster = proof["raster"]
    checks["producer-region-metadata-matches-independent-projection"] = all(all(near(a, b) for a, b in zip(rect(raster[key]), regions[key])) for key in ("peakPixelRegion", "valleyPixelRegion", "gapPixelRegion"))
    expected_size = (int(1120 * contract[2]), int(390 * contract[2]))
    if contract[6]:
        size = jpeg_size(payload)
        checks["jpeg-header-dimensions"] = size == expected_size
        return {"id": expected_id, "name": contract[0], "passed": all(checks.values()), "checks": checks, "png": False, "jpegDecodedOrVisualQualityAsserted": False, "imageSha256": sha(image)}

    encoded_sha = hashlib.sha256(payload).hexdigest().upper()
    width, height, rgba = decoder_cache.decode(image, encoded_sha) if decoder_cache is not None else decode_png(image)
    checks["png-crc-structure-and-dimensions"] = (width, height) == expected_size
    alpha_minimum = 16 if contract[3] else 250
    ink = {key: count_blue(width, height, rgba, region, alpha_minimum) for key, region in regions.items()}
    if contract[1] in ("overview", "peak"):
        checks["original-single-sample-peak-visible"] = ink["peakPixelRegion"] >= (1 if contract[3] else 4)
    if contract[1] == "overview":
        checks["original-single-sample-valley-visible"] = ink["valleyPixelRegion"] >= (1 if contract[3] else 4)
    if contract[1] == "gap":
        checks["no-fabricated-line-across-original-nan-gap"] = ink["gapPixelRegion"] == 0
        checks["original-signal-visible-on-both-sides-of-gap"] = ink["gapLeftOriginalPointRegion"] >= (1 if contract[3] else 4) and ink["gapRightOriginalPointRegion"] >= (1 if contract[3] else 4)
    if contract[3]:
        checks["transparent-canvas-corner"] = rgba[3] == 0
    return {"id": expected_id, "name": contract[0], "passed": all(checks.values()), "checks": checks, "png": True, "imageSha256": sha(image), "independentInkCounts": ink, "independentlyProjectedRegions": regions}


def verify(root, rounds, points, require_gc):
    errors, rows = [], []
    decoder_cache = PngCache()
    protocol = load(root / "protocol.json")
    result_path = root / "results.json"
    result = load(result_path) if result_path.exists() else None
    expected = rounds * 17
    final_attempts = result.get("attempts") if result is not None else None
    if result is not None and (not isinstance(final_attempts, list) or len(final_attempts) != expected):
        errors.append("Managed final attempts array must contain exactly the expected immutable receipts")
    options = protocol.get("options", {})
    if options.get("rounds") != rounds or options.get("points") != points or require_gc and options.get("forceGc") is not True:
        errors.append("Protocol options differ from externally required rounds/points/forced-GC policy")
    fixture = protocol.get("fixture", {})
    if fixture.get("count") != points or fixture.get("finiteVertices") != points - 5 or fixture.get("continuousFigures") != 2 or fixture.get("exactOverviewLineSegments") != points - 7:
        errors.append("Deterministic complete-geometry fixture contract differs")
    events = [json.loads(line) for line in (root / "events.jsonl").read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    starts = [event for event in events if event.get("phase") == "attempt-start"]
    ends = [event for event in events if event.get("phase") == "attempt-end"]
    if len(starts) != expected or len(ends) != expected or [row.get("id") for row in starts] != list(range(expected)) or [row.get("id") for row in ends] != list(range(expected)) or not all(row.get("passed") is True for row in ends):
        errors.append("Flushed per-attempt start/end sequence is incomplete or failed")
    interleaved = [(event.get("phase"), event.get("id")) for event in events if event.get("phase") in ("attempt-start", "attempt-end")]
    if interleaved != [(phase,index) for index in range(expected) for phase in ("attempt-start", "attempt-end")]:
        errors.append("Attempt starts/ends are not interleaved before/after each capture")
    attempt_files = sorted((root / "attempts").glob("*.json"))
    if len(attempt_files) != expected:
        errors.append(f"Expected {expected} immutable attempt receipts, found {len(attempt_files)}")
    for index, path in enumerate(attempt_files):
        try:
            row = load(path)
            evaluated = verify_attempt(root, row, index, points, decoder_cache)
            rows.append(evaluated)
            if not evaluated["passed"]:
                errors.append(f"Attempt {index} failed: {[key for key,value in evaluated['checks'].items() if not value]}")
            if isinstance(final_attempts, list) and index < len(final_attempts) and row != final_attempts[index]:
                errors.append(f"Attempt {index} immutable receipt differs from final summary")
        except (OSError, ValueError, TypeError, KeyError, OverflowError) as error:
            errors.append(f"Attempt {index}: {type(error).__name__}: {error}")
    if result is None:
        errors.append("Managed final result missing (fatal native crashes remain failures)")
    elif not (result.get("passed") is True and result.get("expectedAttempts") == expected and result.get("actualAttempts") == expected and result.get("forcedGcWorkerStopped") is True and result.get("errors") == []):
        errors.append("Managed final summary/worker shutdown incomplete or failed")
    if require_gc and result is not None and not (result.get("forcedCollections", 0) > 0 and result.get("collectionsBeginningDuringManagedCapture", 0) > 0):
        errors.append("Required background forced-GC/capture overlap was not observed")
    expected_images = rounds * 16
    if len(list((root / "images").glob("*"))) != expected_images:
        errors.append("Image directory has missing or unexpected files")
    return {
        "schema": "charts-native-export-independent-oracle/v1", "passed": not errors,
        "expectedAttempts": expected, "validatedAttemptReceipts": len(rows), "expectedImages": expected_images,
        "independentlyDecodedPngs": sum(row.get("png", False) for row in rows),
        "uniquePngDecodeOperations": decoder_cache.decodes, "decodedFrameCacheMaximumBytes":64 * 1024 * 1024,
        "independentChecks": sum(len(row["checks"]) for row in rows), "errors": errors, "attempts": rows,
        "limits": ["PNG quality derives from decoded bytes and independently projected fixed-fixture regions; producer ink counts are not trusted.",
                   "Recorded source identity assertions and complete geometry counters are contract evidence; this pixel oracle does not census all native path vertices.",
                   "JPEG header/dimensions/hash only; no independent JPEG decoder or JPEG visual-quality parity is claimed.",
                   "Forced-GC capture overlap is not exact native-call lifetime tracing; no Tier1 promotion, compositor present, physical platform, official performance or eight-hour qualification is inferred."],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--expected-rounds", type=int, default=16)
    parser.add_argument("--expected-points", type=int, default=100_000)
    parser.add_argument("--require-force-gc", action="store_true")
    args = parser.parse_args()
    if args.output.exists():
        raise SystemExit("Oracle output already exists; use a fresh immutable receipt path")
    try:
        if not (1 <= args.expected_rounds <= 128 and 8191 <= args.expected_points <= 1_000_000):
            raise ValueError("Invalid externally required protocol bounds")
        result = verify(args.input.resolve(), args.expected_rounds, args.expected_points, args.require_force_gc)
    except (OSError, ValueError, TypeError, KeyError, OverflowError) as error:
        result = {"schema": "charts-native-export-independent-oracle/v1", "passed": False, "errors": [f"{type(error).__name__}: {error}"]}
    result["oracleSha256"] = sha(Path(__file__))
    result["pngDecoderSha256"] = sha(Path(__file__).with_name("charts_png_oracle.py"))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as output:
        json.dump(result, output, ensure_ascii=False, indent=2)
        output.write("\n")
    print(json.dumps({key:value for key,value in result.items() if key != "attempts"}, ensure_ascii=True))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
