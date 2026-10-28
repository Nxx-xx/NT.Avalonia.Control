"""Independent, bounded C7-02 native-window evidence checks.

PNG pixels are decoded by the standard-library oracle. RenderScaling and native
surface observations are assertions about the instrumented application, never
proof of physical input, a monitor transition, hardware GPU or desktop capture.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys

from charts_png_oracle import decode_png, sha_bgra

SCHEMA = "charts-platform-acceptance-oracle/v1"
MAX_JSON_BYTES = 32 * 1024 * 1024
MAX_ARTIFACTS = 256
MAX_CASE_DECODED_PIXELS = 64 * 1024 * 1024


def load(path):
    path = Path(path)
    if path.stat().st_size > MAX_JSON_BYTES:
        raise ValueError("JSON exceeds evidence limit")
    def bad_constant(value):
        raise ValueError("Nonfinite JSON value: " + value)
    def unique_pairs(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate JSON property: " + key)
            result[key] = value
        return result
    return json.loads(path.read_text(encoding="utf-8-sig"), parse_constant=bad_constant, object_pairs_hook=unique_pairs)


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def reference(path, root):
    path = Path(path).resolve()
    return {"path": path.relative_to(root).as_posix(), "sha256": sha(path), "bytes": path.stat().st_size}


def contained_path(value, root):
    if not isinstance(value, str) or not value or "\x00" in value:
        raise ValueError("Artifact path must be a nonempty string")
    path = Path(value)
    if not path.is_absolute():
        path = root / path
    path = path.resolve(strict=True)
    try:
        path.relative_to(root.resolve())
    except ValueError as error:
        raise ValueError("Artifact escapes its selected case directory") from error
    if not path.is_file():
        raise ValueError("Artifact is not a regular file")
    return path


def finite(value):
    return type(value) in (int, float) and math.isfinite(value)


def coordinates(value, fields):
    if isinstance(value, list):
        if len(value) != len(fields):
            raise ValueError("Incorrect coordinate array length")
        result = tuple(value)
    elif isinstance(value, dict):
        result = tuple(value[field] for field in fields)
    else:
        raise ValueError("Missing coordinate observation")
    if any(not finite(item) for item in result):
        raise ValueError("Nonfinite coordinate observation")
    return result


def platform_from_description(value):
    if not isinstance(value, str):
        return "unknown"
    text = value.casefold()
    if "windows" in text:
        return "windows"
    if any(tag in text for tag in ("linux", "ubuntu", "debian", "fedora", "opensuse", "suse", "red hat", "centos", "kylin", "uos", "deepin")):
        return "linux"
    return "unknown"


def same_manifest(before, after):
    def records(rows):
        if not isinstance(rows, list) or not rows:
            raise ValueError("A source/module manifest must be nonempty")
        result = {}
        for row in rows:
            path = row["path"]
            if not isinstance(path, str) or path in result or not re.fullmatch(r"[A-Fa-f0-9]{64}", row["sha256"]):
                raise ValueError("Duplicate or malformed source/module manifest entry")
            if type(row["bytes"]) is not int or row["bytes"] < 0:
                raise ValueError("Malformed manifest byte size")
            result[path] = (row["sha256"].upper(), row["bytes"])
        return result
    return records(before) == records(after)


def jpeg_size(path):
    """Validate bounded SOI/SOF/header structure and terminal EOI, not JPEG pixels."""
    path = Path(path)
    if path.stat().st_size > 128 * 1024 * 1024:
        raise ValueError("JPEG exceeds evidence limit")
    payload = path.read_bytes()
    if not payload.startswith(b"\xff\xd8") or not payload.endswith(b"\xff\xd9"):
        raise ValueError("Invalid JPEG SOI/EOI")
    offset = 2
    dimensions = None
    while offset < len(payload) - 2:
        if payload[offset] != 255:
            raise ValueError("Invalid JPEG header marker")
        while offset < len(payload) and payload[offset] == 255:
            offset += 1
        if offset >= len(payload):
            raise ValueError("Truncated JPEG marker")
        marker = payload[offset]
        offset += 1
        if marker == 0xD9:
            break
        if marker in (0, 0xD8) or 0xD0 <= marker <= 0xD7:
            raise ValueError("Unexpected JPEG header marker")
        if offset + 2 > len(payload):
            raise ValueError("Truncated JPEG segment")
        length = struct.unpack_from(">H", payload, offset)[0]
        if length < 2 or offset + length > len(payload) - 2:
            raise ValueError("Truncated JPEG segment contents")
        if marker in (0xC0, 0xC1, 0xC2):
            if length < 8 or dimensions is not None or payload[offset + 2] != 8:
                raise ValueError("Unsupported or duplicate JPEG frame")
            height, width = struct.unpack_from(">HH", payload, offset + 3)
            if not (0 < width <= 16384 and 0 < height <= 16384 and width * height <= 16 * 1024 * 1024):
                raise ValueError("JPEG dimensions exceed evidence limit")
            dimensions = (width, height)
        offset += length
        if marker == 0xDA:
            if dimensions is None:
                raise ValueError("JPEG scan precedes frame")
            return dimensions
    raise ValueError("Missing JPEG frame or scan")


def roi_changes(first, second, region):
    width, height, a = first
    w2, h2, b = second
    if (width, height) != (w2, h2):
        raise ValueError("Glyph frames have different dimensions")
    coordinates = tuple(region[key] for key in ("x", "y", "width", "height"))
    if any(not finite(v) for v in coordinates):
        raise ValueError("Nonfinite glyph ROI")
    x, y, w, h = coordinates
    if w <= 0 or h <= 0 or x < 0 or y < 0 or x + w > width + 1e-7 or y + h > height + 1e-7:
        raise ValueError("Glyph ROI must lie within the decoded image")
    left, top, right, bottom = math.floor(x), math.floor(y), math.ceil(x + w), math.ceil(y + h)
    changed = 0
    for row in range(top, min(height, bottom)):
        for column in range(left, min(width, right)):
            offset = (row * width + column) * 4
            changed += a[offset:offset + 4] != b[offset:offset + 4]
    return changed


def outside_region_changes(first, second, region, padding=2):
    width, height, a = first
    w2, h2, b = second
    if (width, height) != (w2, h2):
        raise ValueError("Glyph isolation frames have different dimensions")
    x, y, w, h = coordinates(region, ("x", "y", "width", "height"))
    left, top = max(0, math.floor(x) - padding), max(0, math.floor(y) - padding)
    right, bottom = min(width, math.ceil(x + w) + padding), min(height, math.ceil(y + h) + padding)
    changes = 0
    for row in range(height):
        segments = ((0, width),) if row < top or row >= bottom else ((0, left), (right, width))
        for start, end in segments:
            aa, bb = a[(row * width + start) * 4:(row * width + end) * 4], b[(row * width + start) * 4:(row * width + end) * 4]
            if aa != bb:
                changes += sum(aa[offset:offset + 4] != bb[offset:offset + 4] for offset in range(0, len(aa), 4))
    return changes


def region_pixels(frame, region):
    width, height, pixels = frame
    x, y, w, h = coordinates(region, ("x", "y", "width", "height"))
    if w <= 0 or h <= 0 or x < 0 or y < 0 or x + w > width + 1e-7 or y + h > height + 1e-7:
        raise ValueError("Glyph direction ROI is outside its actual raster")
    left, top, right, bottom = math.floor(x), math.floor(y), math.ceil(x + w), math.ceil(y + h)
    return right - left, bottom - top, b"".join(pixels[(row * width + left) * 4:(row * width + right) * 4] for row in range(top, bottom))


def lower_stem_analysis(width, height, weights):
    """Find a tight high-contrast ink box and classify the lower-third stem side."""
    if len(weights) != width * height:
        raise ValueError("Glyph weight dimensions disagree")
    ink = [(index % width, index // width) for index, weight in enumerate(weights) if weight >= 16]
    if len(ink) < 24:
        return {"validInk": False, "inkPixels": len(ink), "lowerStem": "unknown"}
    left = min(x for x, _ in ink); right = max(x for x, _ in ink) + 1
    top = min(y for _, y in ink); bottom = max(y for _, y in ink) + 1
    ink_width, ink_height = right - left, bottom - top
    if ink_width < 4 or ink_height < 8:
        return {"validInk": False, "inkPixels": len(ink), "lowerStem": "unknown"}
    lower = top + math.ceil(ink_height * .65)
    midpoint = left + ink_width / 2
    left_weight = right_weight = 0
    for y in range(lower, bottom):
        for x in range(left, right):
            weight = weights[y * width + x]
            if weight < 16:
                continue
            if x < midpoint: left_weight += weight
            else: right_weight += weight
    side = "left" if left_weight > 2 * right_weight + 3 * 255 else "right" if right_weight > 2 * left_weight + 3 * 255 else "ambiguous"
    return {"validInk": side in ("left", "right"), "inkPixels": len(ink), "inkBounds": [left, top, ink_width, ink_height], "lowerLeftWeight": left_weight, "lowerRightWeight": right_weight, "lowerStem": side}


def glyph_orientation_evidence(baseline, hidden, restored, glyph_region, reference_region):
    """Direction of a real self-drawn F relative to an upright native TextBlock F.

    The label mask comes only from pixels that disappear when that label is
    hidden. The reference uses explicit black text on white, not control flags.
    """
    if baseline[:2] != hidden[:2] or baseline[:2] != restored[:2]:
        raise ValueError("Glyph direction frames have different sizes")
    w, h, before = region_pixels(baseline, glyph_region)
    w2, h2, without = region_pixels(hidden, glyph_region)
    if (w, h) != (w2, h2):
        raise ValueError("Glyph direction regions disagree")
    weights = [max(abs(before[offset + channel] - without[offset + channel]) for channel in range(4)) for offset in range(0, len(before), 4)]
    glyph = lower_stem_analysis(w, h, weights)
    rw, rh, reference_pixels = region_pixels(baseline, reference_region)
    reference_weights = [((765 - sum(reference_pixels[offset:offset + 3])) * reference_pixels[offset + 3] + 382) // 765 for offset in range(0, len(reference_pixels), 4)]
    native = lower_stem_analysis(rw, rh, reference_weights)
    gx, gy, gw, gh = coordinates(glyph_region, ("x", "y", "width", "height"))
    rx, ry, rwidth, rheight = coordinates(reference_region, ("x", "y", "width", "height"))
    distinct_reference = gx + gw <= rx or rx + rwidth <= gx or gy + gh <= ry or ry + rheight <= gy
    return {"passed": glyph["validInk"] and native["validInk"] and glyph["lowerStem"] == "left" and native["lowerStem"] == "left" and baseline == restored and distinct_reference,
            "chartGlyph": glyph, "nativeTextBlockReference": native, "wholeFrameRestored": baseline == restored,
            "separateNonoverlappingNativeReference": distinct_reference,
            "scope": "Pixel evidence for the fixed uppercase F direction probe. This is not OCR, all-glyph shaping, compositor or physical-display certification."}


def deterministic_scene_checks(observations):
    """Recount fixed fixture identity, UTC and simple axis geometry independently."""
    checks = {}
    def check(name, condition):
        checks[name] = condition is True
    geometry = [row for row in observations if row.get("kind") == "cartesian-geometry"]
    names = [row.get("scene") for row in geometry]
    expected_names = ("desktop-main", "desktop-small", "desktop-restored")
    check("three-real-size-observations", len(geometry) == 3 and set(names) == set(expected_names))
    by_name = {row["scene"]: row for row in geometry}
    for row in geometry:
        label = row["scene"]
        plot = coordinates(row["plotDips"], ("x", "y", "width", "height"))
        point = coordinates(row["projectedPrimaryPlotDips"], ("x", "y"))
        check(label + "-independent-primary-coordinate", plot[2] > 0 and plot[3] > 0 and abs(point[0] - plot[2] / 2) <= .02 and abs(point[1] - plot[3] * .4) <= .02)
        hit = row["actualHit"]
        check(label + "-independent-original-hit", hit is not None and hit.get("id") == "B" and hit.get("sourceIndex") == 1 and hit.get("raw") == [2, 60])
        source = row["originalSource"]
        check(label + "-independent-original-source", source == [{"id": "A", "x": 1, "y": 20}, {"id": "B", "x": 2, "y": 60}, {"id": "C", "x": 3, "y": 80}])
    if set(by_name) == set(expected_names):
        main = coordinates(by_name["desktop-main"]["actualClientDips"], ("width", "height"))
        small = coordinates(by_name["desktop-small"]["actualClientDips"], ("width", "height"))
        restored = coordinates(by_name["desktop-restored"]["actualClientDips"], ("width", "height"))
        check("real-window-resize-shrinks-client", small[0] < main[0] - 1 and small[1] < main[1] - 1)
        check("real-window-resize-restores-client", abs(restored[0] - main[0]) <= 2 and abs(restored[1] - main[1]) <= 2)
    cultures = [row for row in observations if row.get("kind") == "culture-and-UTC-dates"]
    check("one-culture-date-observation", len(cultures) == 1)
    if len(cultures) == 1:
        row = cultures[0]
        expected_ticks = [1709251200000, 1709337600000, 1709424000000]
        check("independent-UTC-data-domain", row.get("originalDomainMilliseconds") == expected_ticks)
        check("independent-UTC-labels", row.get("labels") == ["2024-03-01 00:00:00Z", "2024-03-02 00:00:00Z", "2024-03-03 00:00:00Z"])
        hit = row["dateHit"]
        check("independent-UTC-original-hit", hit is not None and hit.get("id") == "D1" and hit.get("sourceIndex") == 1 and hit.get("raw") == [expected_ticks[1], 62.5])
        check("observed-number-separator-consistent", row.get("numericLabel") == "62" + row["decimalSeparator"] + "5")
        expected_rtl = {"ar-SA": True, "zh-CN": False, "en-US": False}.get(row.get("culture"))
        if expected_rtl is not None:
            check("fixed-culture-RTL-direction", row.get("flowDirection") == ("RightToLeft" if expected_rtl else "LeftToRight"))
    professional = [row for row in observations if row.get("kind") == "professional-native-frames"]
    check("one-professional-frame-observation", len(professional) == 1)
    if len(professional) == 1:
        row = professional[0]
        marks = row["funnelMarks"]
        check("independent-funnel-originals", [(mark.get("id"), mark.get("sourceIndex"), mark.get("value")) for mark in marks] == [("a", 0, 20), ("b", 1, 50), ("c", 2, 30), ("d", 3, 10)])
        members = [point for mark in row["mosaicMarks"] for point in mark["members"]]
        check("independent-mosaic-originals", sorted((point.get("id"), point.get("sourceIndex")) for point in members) == [("a", 0), ("b", 1), ("c", 2), ("d", 3)])
        check("independent-mosaic-aggregation", sorted(sorted(point["id"] for point in mark["members"]) for mark in row["mosaicMarks"]) == [["a", "d"], ["b"], ["c"]])
        hit = row["funnelHit"]
        check("independent-funnel-hit", hit is not None and hit.get("id") == "b" and hit.get("sourceIndex") == 1 and len(hit.get("raw", [])) == 2 and hit["raw"][1] == 50)
        check("independent-mosaic-hit-members", sorted(row.get("mosaicHitMembers", [])) == ["a", "d"])
    return checks


def logical_display_protocol(observations, environment, before_environment=None):
    """Derive extra coverage from actual reported topology, with blocked targets explicit."""
    checks = {}
    extra_keys, extra_artifacts = [], []
    def check(name, condition):
        checks[name] = condition is True
    records = [row for row in observations if row.get("kind") == "logical-display-migration-capability"]
    check("logical-topology-one-capability-record", len(records) == 1)
    if len(records) != 1:
        return {"checks": checks, "extraKeys": [], "extraArtifacts": [], "status": "missing-capability", "executedTargets": 0, "blockedTargets": []}
    capability = records[0]
    screens = environment["screens"]
    check("logical-topology-actual-screen-count", capability.get("logicalScreenCount") == len(screens) and len(screens) > 0)
    check("logical-topology-no-physical-certification", capability.get("physicalCrossMonitorCertified") is False and capability.get("mixedDpiCertified") is False)
    bounds = [coordinates(screen["bounds"], ("x", "y", "width", "height")) for screen in screens]
    original_source = "capability-record"
    try:
        if capability.get("originalScreenBounds") is not None:
            original = coordinates(capability["originalScreenBounds"], ("x", "y", "width", "height"))
        elif len(set(bounds)) == 1:
            original = bounds[0]
            original_source = "single-reported-logical-screen"
        elif before_environment is not None and coordinates(capability["originalPositionPixels"], ("x", "y")) == coordinates(before_environment["window"]["positionPixels"], ("x", "y")):
            original = coordinates(before_environment["window"]["currentScreen"]["bounds"], ("x", "y", "width", "height"))
            original_source = "native-before-window-screen-with-preserved-position"
        else:
            raise ValueError("Missing independently identified original logical screen")
    except (ValueError, KeyError, TypeError):
        check("logical-topology-original-screen-observed", False)
        return {"checks": checks, "extraKeys": [], "extraArtifacts": [], "status": "missing-original-screen", "executedTargets": 0, "blockedTargets": []}
    check("logical-topology-original-screen-observed", original in bounds)
    expected_indices = {index for index, item in enumerate(bounds) if item != original}
    targets = [row for row in observations if row.get("kind") == "logical-display-migration-target"]
    target_indices = [row.get("targetIndex") for row in targets]
    check("logical-topology-every-target-accounted", all(type(index) is int for index in target_indices) and len(set(target_indices)) == len(target_indices) and set(target_indices) == expected_indices)
    executed = 0
    blocked = []
    suffixes = ("target-screen-observed", "native-scale-and-client-valid", "current-original-B-hit-and-selection", "existing-viewport-preserved", "window-raster-actual-scale-dimensions")
    for row in targets:
        index = row.get("targetIndex")
        if type(index) is not int or index not in expected_indices:
            continue
        screen = screens[index]
        prefix = "logical-display-" + str(index)
        expected_bounds = bounds[index]
        working = coordinates(screen["workingArea"], ("x", "y", "width", "height"))
        factor = screen["scaling"]
        usable = finite(factor) and factor > 0 and working[2] >= 590 * factor and working[3] >= 370 * factor
        check(prefix + "-target-describes-real-screen", coordinates(row["expectedBounds"], ("x", "y", "width", "height")) == expected_bounds and row.get("reportedScreenScaling") == factor)
        check(prefix + "-not-self-certified-physical", row.get("physicalCrossMonitorCertified") is False and row.get("mixedDpiCertified") is False)
        if row.get("status") == "blocked-target-working-area-too-small":
            check(prefix + "-blocked-reason-matches-workarea", not usable)
            blocked.append(index)
            continue
        check(prefix + "-available-target-was-executed", row.get("status") == "executed" and usable)
        if row.get("status") != "executed":
            continue
        executed += 1
        check(prefix + "-exact-check-prefix", row.get("checkPrefix") == prefix)
        extra_keys.extend(prefix + "-" + suffix for suffix in suffixes)
        extra_artifacts.append({"name": "raster-window-" + prefix + ".png", "kind": "png"})
        check(prefix + "-observed-expected-screen", coordinates(row["observedBounds"], ("x", "y", "width", "height")) == expected_bounds)
        actual_scale = row["actualRenderScaling"]
        client = coordinates(row["actualClientDips"], ("width", "height"))
        check(prefix + "-actual-scale-and-client", finite(actual_scale) and actual_scale > 0 and abs(actual_scale - factor) <= .02 and all(item > 0 for item in client))
        hit = row.get("actualHit")
        check(prefix + "-current-original-identity", hit is not None and hit.get("id") == "B" and hit.get("sourceIndex") == 1 and hit.get("raw") == [2, 60] and row.get("selectedId") == "B")
        viewport = row["viewportBefore"]
        check(prefix + "-independent-viewport-preserved", viewport == row["viewportAfter"] and all(viewport.get(key) == value for key, value in (("xMinimum", .5), ("xMaximum", 3.5), ("yMinimum", 10), ("yMaximum", 90))))
    check("logical-topology-executed-count-recounted", capability.get("executedTargets") == executed)
    if executed:
        extra_keys.extend(("logical-display-original-position-restored", "logical-display-original-view-and-selection-restored"))
        check("logical-topology-original-position-restored", capability.get("originalPositionPixels") == capability.get("restoredPositionPixels"))
        check("logical-topology-status-matches-executed", capability.get("status") == "executed-logical-topology-only")
        try:
            origin = coordinates(capability["originalClientOriginScreenPixels"], ("x", "y"))
            center = coordinates(capability["originalClientCenterScreenPixels"], ("x", "y"))
            client = coordinates(capability["originalClientDips"], ("width", "height"))
            scale = capability["originalRenderScaling"]
            check("logical-topology-origin-client-geometry", finite(scale) and scale > 0 and all(value > 0 for value in client) and all(abs(center[i] - origin[i] - client[i] * scale / 2) <= 1 for i in (0, 1)))
            check("logical-topology-origin-center-on-recorded-screen", original[0] <= center[0] <= original[0] + original[2] and original[1] <= center[1] <= original[1] + original[3])
            check("logical-topology-restored-native-client-geometry", coordinates(capability["restoredScreenBounds"], ("x", "y", "width", "height")) == original and capability["restoredClientOriginScreenPixels"] == capability["originalClientOriginScreenPixels"] and capability["restoredClientCenterScreenPixels"] == capability["originalClientCenterScreenPixels"] and capability["restoredClientDips"] == capability["originalClientDips"] and capability["restoredRenderScaling"] == scale)
            trace = capability["placementTrace"]
            check("logical-topology-bounded-placement-trace", isinstance(trace, list) and 2 <= len(trace) <= 512 and all(type(row.get("sequence")) is int for row in trace) and [row.get("sequence") for row in trace] == list(range(1, len(trace) + 1)))
            first, last = trace[0], trace[-1]
            check("logical-topology-trace-origin-and-restoration", first.get("kind") == "original-placement-observed" and last.get("kind") == "restore-bounded-feedback-finished" and all(row.get("actualPositionPixels") == capability[prefix + "PositionPixels"] and row.get("actualPointToScreenOrigin") == capability[prefix + "ClientOriginScreenPixels"] and row.get("actualClientCenterScreenPixels") == capability[prefix + "ClientCenterScreenPixels"] and row.get("actualClientDips") == capability[prefix + "ClientDips"] and row.get("actualRenderScaling") == capability[prefix + "RenderScaling"] and row.get("actualScreenBounds") == capability[prefix + "ScreenBounds"] for row, prefix in ((first, "original"), (last, "restored"))))
        except (KeyError, TypeError, ValueError, IndexError, AttributeError):
            check("logical-topology-complete-native-origin-evidence", False)
    elif expected_indices:
        check("logical-topology-blocked-is-explicit", len(blocked) == len(expected_indices) and capability.get("status") == "blocked-no-target-workarea")
    else:
        check("logical-topology-single-screen-not-migration", not targets and capability.get("status") == "not-applicable-single-logical-screen")
    return {"checks": checks, "extraKeys": extra_keys, "extraArtifacts": extra_artifacts, "status": capability.get("status"), "executedTargets": executed, "blockedTargets": blocked, "originalScreenSource": original_source, "physicalCrossMonitorCertified": False, "mixedDpiCertified": False}


def inspect_case(directory, planned, contract, assembly_manifest, *, expected_platform, expected_architecture="X64", expected_os_description=None, expected_process_id=None, raw_override=None):
    directory = Path(directory).resolve()
    raw_path = directory / "raw.json"
    raw = load(raw_path) if raw_override is None else raw_override
    checks = {}
    def check(name, condition):
        if name in checks:
            raise ValueError("Duplicate oracle assertion")
        checks[name] = condition is True
    check("raw-schema", raw.get("schema") == contract["hostSchema"])
    check("producer-completed", raw.get("passed") is True and raw.get("status") in ("passed", "completed"))
    check("no-native-host-error", raw.get("error") is None)
    host_checks = raw["checks"]
    check("exact-host-check-keyset", isinstance(host_checks, dict) and set(host_checks) == set(contract["requiredHostCheckKeys"]))
    check("all-host-contracts", bool(host_checks) and all(flag is True for flag in host_checks.values()))
    options = raw["options"]
    requested_isolation = planned.get("isolateNativeText", False)
    actual_isolation = options.get("isolateNativeText", False)
    check("native-text-isolation-explicit-plan-matches", type(requested_isolation) is bool and type(actual_isolation) is bool and actual_isolation == requested_isolation)
    check("native-text-isolation-Linux-only", requested_isolation is False or expected_platform == "linux")
    for field in ("renderer", "culture", "theme", "scenario"):
        check("actual-options-" + field, options.get(field) == planned[field])
    check("requested-scale-not-substituted", options.get("requestedScale") == planned["expectedScale"])
    check("not-an-interactive-or-headless-substitute", options.get("interactive") is False)
    suite = raw["suite"]
    producer_checks = suite["checks"]
    if not isinstance(producer_checks, dict):
        raise ValueError("Native scene checks must be a dictionary")
    logical = logical_display_protocol(suite["observations"], raw["environmentAfter"], raw["environmentBefore"]) if contract.get("logicalDisplayProtocol") is True else {"checks": {}, "extraKeys": [], "extraArtifacts": [], "status": "not-in-contract", "executedTargets": 0, "blockedTargets": []}
    checks.update(logical["checks"])
    expected_keys = contract["requiredCheckKeys"] + logical["extraKeys"]
    if len(expected_keys) < 8 or len(expected_keys) != len(set(expected_keys)):
        raise ValueError("Expected native check contract is empty or ambiguous")
    check("exact-scene-check-keyset", set(producer_checks) == set(expected_keys))
    check("all-native-scene-assertions", bool(producer_checks) and all(flag is True for flag in producer_checks.values()))
    check("no-native-scene-faults", suite.get("faults") == [])
    qualification = raw["qualification"]
    for field in ("physicalInputValidated", "compositorPresentValidated"):
        check("no-self-certified-" + field, qualification.get(field) is False)
    environment = raw["environmentAfter"]
    before_environment = raw["environmentBefore"]
    if expected_platform not in ("windows", "linux"):
        raise ValueError("Expected platform must be supplied independently")
    expected_architecture = expected_architecture.upper()
    if expected_architecture not in ("X64", "ARM64"):
        raise ValueError("Only explicitly identified X64/ARM64 process architectures are supported")
    expected_backend, expected_type, expected_handle = ("Win32", "Avalonia.Win32.", "HWND") if expected_platform == "windows" else ("X11", "Avalonia.X11.", "XID")
    for phase, observed in (("before", before_environment), ("after", environment)):
        native = observed["window"]
        runtime = observed["runtime"]
        check(phase + "-backend-matches-independent-platform", native.get("actualBackend") == expected_backend and isinstance(native.get("backendType"), str) and native["backendType"].startswith(expected_type) and native.get("handleDescriptor") == expected_handle and native.get("isVisible") is True and isinstance(native.get("handle"), str) and re.fullmatch(r"0x[1-9A-Fa-f][0-9A-Fa-f]*", native["handle"]) is not None)
        check(phase + "-runtime-matches-independent-platform", platform_from_description(runtime.get("osDescription")) == expected_platform and (expected_os_description is None or runtime.get("osDescription") == expected_os_description))
        check(phase + "-runtime-matches-independent-architecture", str(runtime.get("processArchitecture", "")).upper() == expected_architecture and str(runtime.get("osArchitecture", "")).upper() == expected_architecture)
        check(phase + "-runtime-process-identified", type(runtime.get("processId")) is int and runtime["processId"] > 0 and (expected_process_id is None or runtime["processId"] == expected_process_id))
        if requested_isolation:
            isolation = (observed.get("session") or {}).get("nativeTextIsolation") or {}
            native_relative_path = "runtimes/linux-" + ("x64" if expected_architecture == "X64" else "arm64") + "/native/libHarfBuzzSharp.so"
            native_library = next((row for row in assembly_manifest if row.get("path") == native_relative_path), None)
            check(phase + "-native-text-isolation-applied", isolation.get("requested") is True and isolation.get("applied") is True and type(isolation.get("flags")) is int and isolation["flags"] == 10 and isolation.get("flagNames") == "RTLD_NOW|RTLD_LOCAL|RTLD_DEEPBIND" and isolation.get("error") is None and isolation.get("resolverRegistered") is True and isolation.get("resolverObserved") is True and isolation.get("resolvedLibraryName") == "libHarfBuzzSharp" and isinstance(isolation.get("handle"), str) and re.fullmatch(r"0x[1-9A-Fa-f][0-9A-Fa-f]*", isolation["handle"]) is not None)
            check(phase + "-native-text-library-pinned", native_library is not None and str(isolation.get("sha256", "")).upper() == native_library["sha256"].upper() and isolation.get("bytes") == native_library["bytes"] and str(isolation.get("path", "")).replace("\\", "/").endswith("/" + native_relative_path) and isolation.get("symbolProviderPath") == isolation.get("path") and isolation.get("hostOnly") is True and isolation.get("ordinaryConsumerNativeTextCertified") is False)
        if planned["expectedScale"] is not None:
            check(phase + "-actual-scale-matches-request", finite(native.get("renderScaling")) and abs(native["renderScaling"] - planned["expectedScale"]) <= .02)
        if expected_platform == "linux" and planned.get("processScreenScale") is not None:
            session = observed.get("session") or {}
            global_scale = session.get("globalScaleFactor")
            try:
                global_matches = isinstance(global_scale, str) and finite(float(global_scale)) and float(global_scale) == planned["processScreenScale"]
            except (ValueError, TypeError):
                global_matches = False
            check(phase + "-child-global-scale-multiplier-recorded", global_matches)
            check(phase + "-child-indexed-screen-override-cleared", session.get("screenScaleFactors") in (None, ""))
        if expected_platform == "windows":
            metrics = native.get("win32ReportedMetrics") or {}
            client_rect = metrics.get("clientRect") or {}
            native_pixels = native.get("physicalClientSize") or {}
            check(phase + "-user32-client-pixels-match", metrics.get("clientRectAvailable") is True and all(finite(client_rect.get(key)) and finite(native_pixels.get(key)) and abs(client_rect[key] - native_pixels[key]) <= 1 for key in ("width", "height")))
            dpi = metrics.get("dpiForWindow")
            native_scale = native.get("renderScaling")
            check(phase + "-user32-per-monitor-dpi-matches", metrics.get("windowDpiAwareness") == 2 and finite(dpi) and dpi > 0 and finite(native_scale) and abs(dpi / 96 - native_scale) <= .02)
    check("same-native-window-before-and-after", all(before_environment["window"].get(key) == environment["window"].get(key) for key in ("actualBackend", "backendType", "handleDescriptor", "handle")))
    check("same-runtime-before-and-after", all(before_environment["runtime"].get(key) == environment["runtime"].get(key) for key in ("osDescription", "osArchitecture", "processArchitecture", "processId", "framework", "runtime", "avalonia")))
    if requested_isolation:
        isolation = environment["session"]["nativeTextIsolation"]
        check("same-native-text-isolation-before-and-after", before_environment["session"].get("nativeTextIsolation") == isolation)
        check("terminal-native-text-bootstrap-matches-observed-window", raw.get("nativeTextIsolation") == isolation)
        module_matches = [row for row in raw.get("nativeModules", []) if row.get("path") == isolation.get("path") and str(row.get("sha256", "")).upper() == str(isolation.get("sha256", "")).upper()]
        check("native-text-library-actually-loaded", len(module_matches) == 1)
    else:
        inactive_states = [raw.get("nativeTextIsolation"), (before_environment.get("session") or {}).get("nativeTextIsolation"), (environment.get("session") or {}).get("nativeTextIsolation")]
        for index, state in enumerate(inactive_states):
            if state is not None:
                check("unrequested-native-text-isolation-inactive-" + str(index), isinstance(state, dict) and state.get("requested") is False and state.get("applied") is False and state.get("resolverRegistered") is False and state.get("resolverObserved") is False and type(state.get("flags")) is int and state["flags"] == 0 and state.get("hostOnly") is True and state.get("ordinaryConsumerNativeTextCertified") is False and all(state.get(key) is None for key in ("path", "sha256", "bytes", "handle", "symbolProviderPath", "symbolAddress", "resolvedLibraryName", "flagNames", "resolverAssembly", "libcVersion", "error")))
        if contract.get("deterministicSceneProtocol") is True:
            check("unrequested-native-text-isolation-snapshots-present", all(isinstance(state, dict) for state in inactive_states))
    check("observed-culture-matches-request", environment["culture"].get("currentCulture") == planned["culture"] and environment["culture"].get("currentUiCulture") == planned["culture"])
    window = environment["window"]
    scale = window["renderScaling"]
    check("actual-scale-finite", finite(scale) and .25 <= scale <= 8)
    if planned["expectedScale"] is not None:
        check("actual-scale-matches-request", finite(scale) and abs(scale - planned["expectedScale"]) <= .02)
    check("actual-native-window-backend", window.get("actualBackend") in ("Win32", "X11") and bool(window.get("backendType")) and bool(window.get("handleDescriptor")))
    for field in ("clientSize", "physicalClientSize"):
        size = window[field]
        check("positive-observed-" + field, all(finite(size.get(k)) and size[k] > 0 for k in ("width", "height")))
    check("reported-physical-client-size", all(abs(window["physicalClientSize"][k] - window["clientSize"][k] * scale) <= 1.01 for k in ("width", "height")))
    check("actual-screen-inventory", isinstance(environment.get("screens"), list) and len(environment["screens"]) > 0)
    rendering = environment["rendering"]
    samples = rendering["nativeWindowSurfaceObservations"]
    if not isinstance(samples, list):
        raise ValueError("Native surface observations must be a list")
    usable = [sample for sample in samples if type(sample.get("hasGrContext")) is bool]
    flags = {sample["hasGrContext"] for sample in usable}
    inferred = "gpu-context" if flags == {True} else "software-raster" if flags == {False} else "mixed" if len(flags) == 2 else "unknown"
    check("actual-renderer-follows-native-observations", rendering.get("actualRenderer") == inferred)
    check("native-renderer-observed", inferred in ("software-raster", "gpu-context") and len(usable) == len(samples) and len(usable) >= 2 and all(type(sample.get("epoch")) is int and sample["epoch"] > 0 and sample.get("error") is None for sample in usable) and len({sample["epoch"] for sample in usable}) >= 2 and {"before-suite", "after-suite"}.issubset({sample.get("phase") for sample in usable}))
    if planned["renderer"] in ("software", "gpu"):
        expected_renderer = "software-raster" if planned["renderer"] == "software" else "gpu-context"
        check("explicit-renderer-request-observed", inferred == expected_renderer)
    check("gpu-context-is-not-hardware-certification", rendering.get("hardwareGpuProven") is False)
    assemblies = {row["path"]: row for row in assembly_manifest}
    observed_assemblies = environment.get("assemblies", [])
    for module in ("Charts.PlatformAcceptance.dll", "NT.AvaloniaControl.Charts.dll", "NT.AvaloniaControl.Charts.Themes.dll", "Avalonia.Base.dll", "Avalonia.Skia.dll"):
        matches = [row for row in observed_assemblies if row.get("moduleName", row.get("name")) == module or Path(row.get("path", "")).name == module]
        check("loaded-module-pinned-" + module, len(matches) == 1 and module in assemblies and str(matches[0].get("sha256", matches[0].get("hash")) or "").upper() == assemblies[module]["sha256"].upper())
    artifacts = suite["artifacts"]
    if not isinstance(artifacts, list) or not 1 <= len(artifacts) <= MAX_ARTIFACTS:
        raise ValueError("Artifact count is outside evidence limits")
    images = []
    frames = {}
    frame_cache = {}
    unique_decoded_pixels = 0
    seen = set()
    for index, artifact in enumerate(artifacts):
        path = contained_path(artifact["path"], directory)
        if path in seen:
            raise ValueError("An artifact path occurs more than once")
        seen.add(path)
        encoded_sha = sha(path)
        kind = artifact["kind"].lower()
        if kind == "png":
            if encoded_sha not in frame_cache:
                decoded = decode_png(path)
                unique_decoded_pixels += decoded[0] * decoded[1]
                if unique_decoded_pixels > MAX_CASE_DECODED_PIXELS:
                    raise ValueError("Case PNG decode budget exceeded")
                frame_cache[encoded_sha] = decoded
            width, height, rgba = frame_cache[encoded_sha]
            frames[path] = frame_cache[encoded_sha]
            check(f"artifact-{index}-decoded-pixel-hash", str(artifact.get("decodedBgraSha256") or "").upper() == sha_bgra(rgba))
        elif kind in ("jpeg", "jpg"):
            width, height = jpeg_size(path)
        else:
            raise ValueError("Unsupported artifact kind")
        check(f"artifact-{index}-declared-size", artifact.get("pixelWidth") == width and artifact.get("pixelHeight") == height)
        check(f"artifact-{index}-encoded-hash", str(artifact.get("sha256") or "").upper() == encoded_sha)
        images.append(dict(reference(path, directory), kind=kind, width=width, height=height, purpose=artifact.get("purpose")))
    observations = suite["observations"]
    if not isinstance(observations, list):
        raise ValueError("Scene observations must be a list")
    if contract.get("deterministicSceneProtocol") is True:
        checks.update(deterministic_scene_checks(observations))
        expected_artifacts = {row["name"]: row for row in contract["expectedArtifacts"] + logical["extraArtifacts"]}
        check("exact-required-artifact-index", len(artifacts) == len(expected_artifacts) and {Path(row["path"]).name for row in artifacts} == set(expected_artifacts))
        for artifact in artifacts:
            name = Path(artifact["path"]).name
            expected = expected_artifacts.get(name)
            if expected is not None:
                check("artifact-contract-" + name, artifact["kind"].lower() == expected["kind"] and (expected.get("width") is None or artifact["pixelWidth"] == expected["width"]) and (expected.get("height") is None or artifact["pixelHeight"] == expected["height"]))
        for row in observations:
            if row.get("kind") != "software-window-raster":
                continue
            image = frames.get(contained_path(row["artifactPath"], directory))
            client = coordinates(row["actualClientDips"], ("width", "height"))
            factor = row["actualRenderScaling"]
            check("window-raster-observed-native-scale-" + row["scene"], image is not None and finite(factor) and image[:2] == (math.ceil(client[0] * factor), math.ceil(client[1] * factor)))
            source = coordinates(row["sourceClientRectDips"], ("x", "y", "width", "height"))
            destination = coordinates(row["destinationPixelRect"], ("x", "y", "width", "height"))
            check("window-raster-explicit-neutral-DPI-transform-" + row["scene"], row.get("targetDpi") == [96, 96] and row.get("explicitScale") == [factor, factor] and isinstance(row.get("captureMethod"), str) and row["captureMethod"].startswith("96-DPI RenderTargetBitmap; complete live Window VisualBrush") and source == (0, 0, *client) and all(abs(value - expected) <= .02 for value, expected in zip(destination, (0, 0, client[0] * factor, client[1] * factor))))
    oracle_rows = [row["oracle"] for row in observations if row.get("kind") == "oracle"]
    if len(oracle_rows) != 1:
        raise ValueError("Exactly one independent raster protocol is required")
    raster_protocol = oracle_rows[0]
    if contract.get("deterministicSceneProtocol") is True:
        def protocol_keys(rows, kind):
            if kind == "glyphProofs":
                return sorted((Path(row["baselinePath"]).name, Path(row["hiddenPath"]).name, Path(row["restoredPath"]).name, row["purpose"]) for row in rows)
            return sorted((Path(row["firstPath"]).name, Path(row["secondPath"]).name, row["purpose"]) for row in rows)
        for kind in ("pngEqualPairs", "pngDifferentPairs", "glyphProofs"):
            check("exact-raster-protocol-" + kind, protocol_keys(raster_protocol[kind], kind) == protocol_keys(contract["rasterProtocol"][kind], kind))
    def frame(value):
        path = contained_path(value, directory)
        if path not in frames:
            raise ValueError("Raster comparison references an unlisted/non-PNG artifact")
        return frames[path]
    for field, equal in (("pngEqualPairs", True), ("pngDifferentPairs", False)):
        pairs = raster_protocol[field]
        check("nonempty-" + field, len(pairs) >= contract["minimumRasterPairs"][field])
        for index, pair in enumerate(pairs):
            a, b = frame(pair["firstPath"]), frame(pair["secondPath"])
            check(f"{field}-{index}", (a == b) if equal else (a[:2] == b[:2] and a[2] != b[2]))
    glyphs = raster_protocol["glyphProofs"]
    check("real-template-glyph-count", len(glyphs) >= contract["minimumGlyphProofs"])
    glyph_results = []
    for index, glyph in enumerate(glyphs):
        a, hidden, restored = (frame(glyph[key]) for key in ("baselinePath", "hiddenPath", "restoredPath"))
        changed = roi_changes(a, hidden, glyph["pixelRegion"])
        returned = roi_changes(a, restored, glyph["pixelRegion"])
        check(f"glyph-{index}-actual-hidden-pixels", changed >= 3)
        check(f"glyph-{index}-restored-frame", a == restored and returned == 0)
        if contract.get("requireTemplateTextOpacityIsolation") is True:
            check(f"glyph-{index}-text-opacity-isolated", "TextBlock.Opacity" in str(glyph.get("isolation")) and finite(glyph.get("originalTextOpacity")) and glyph["originalTextOpacity"] > 0 and all(glyph.get(key) is True for key in ("parentBackdropPresent", "parentBackdropBoundsUnchanged", "parentBackdropBrushInstanceUnchanged")))
            outside_changes = outside_region_changes(a, hidden, glyph["pixelRegion"])
            check(f"glyph-{index}-background-outside-ink-region-unchanged", outside_changes == 0)
        else:
            outside_changes = None
        glyph_results.append({"purpose": glyph.get("purpose"), "hiddenChangedPixels": changed, "restoredChangedPixels": returned, "pixelRegion": glyph["pixelRegion"], "outsidePaddedGlyphRegionChanges": outside_changes})
    direction_results = []
    if contract.get("glyphOrientationProtocol"):
        direction_contract = contract["glyphOrientationProtocol"]
        directions = raster_protocol.get("glyphOrientationProofs", [])
        check("three-exact-RTL-direction-probes", len(directions) == len(direction_contract["names"]) and {row.get("name") for row in directions} == set(direction_contract["names"]))
        direction_observations = [row for row in observations if row.get("kind") == "RTL-asymmetric-glyph"]
        check("three-real-RTL-geometry-observations", len(direction_observations) == len(direction_contract["names"]) and {row.get("variant") for row in direction_observations} == set(direction_contract["names"]))
        for row in direction_observations:
            prefix = "RTL-geometry-" + row["variant"]
            a, c = row["originalPointA"], row["originalPointC"]
            window_a = coordinates(a["windowDips"], ("x", "y")); window_c = coordinates(c["windowDips"], ("x", "y"))
            check(prefix + "-original-identities", a.get("id") == "glyph-A" and a.get("sourceIndex") == 0 and c.get("id") == "glyph-C" and c.get("sourceIndex") == 1)
            check(prefix + "-off-centre-axis-order", window_a[0] < window_c[0])
            plot = coordinates(row["plotDips"], ("x", "y", "width", "height"))
            chart_a = coordinates(a["chartDips"], ("x", "y")); chart_c = coordinates(c["chartDips"], ("x", "y"))
            check(prefix + "-independent-asymmetric-chart-coordinates", abs(chart_a[0] - (plot[0] + plot[2] * .25)) <= .02 and abs(chart_c[0] - (plot[0] + plot[2] * .75)) <= .02 and abs(chart_a[1] - (plot[1] + plot[3] * .5)) <= .02 and abs(chart_c[1] - chart_a[1]) <= .02)
        for proof in directions:
            name = proof["name"]
            prefix = "RTL-direction-" + name
            check(prefix + "-known-actual-glyph-protocol", name in direction_contract["names"] and proof.get("expectedGlyph") == "F" and proof.get("expectedLowerStem") == "left" and proof.get("scope") == direction_contract["scope"] and proof.get("fontSizeDips") == 48)
            expected_proof = direction_contract["proofs"].get(name, {})
            check(prefix + "-exact-variant-artifact-roles", bool(expected_proof) and all(isinstance(proof.get(key), str) and Path(proof[key]).name == value for key, value in expected_proof.items()))
            geometry_rows = [row for row in direction_observations if row.get("variant") == name]
            if len(geometry_rows) == 1:
                row = geometry_rows[0]
                check(prefix + "-glyph-ROI-is-public-observed-anchor", proof["glyphRegion"] == row["glyphRegion"] and proof["referenceRegion"] == row["referenceRegion"] and Path(proof["baselinePath"]).name == Path(row["baselinePath"]).name)
                anchor = coordinates(row["labelAnchorWindowDips"], ("x", "y"))
                point_a = coordinates(row["originalPointA"]["windowDips"], ("x", "y"))
                factor = row["actualRenderScaling"]
                region = coordinates(proof["glyphRegion"], ("x", "y", "width", "height"))
                check(prefix + "-independent-off-centre-glyph-ROI", finite(factor) and factor > 0 and abs(anchor[0] - point_a[0]) <= .02 and abs(anchor[1] - point_a[1]) <= .02 and all(abs(actual - expected) <= .02 for actual, expected in zip(region, ((anchor[0] - 70) * factor, (anchor[1] - 80) * factor, 140 * factor, 160 * factor))))
            base, without, returned = (frame(proof[key]) for key in ("baselinePath", "hiddenPath", "restoredPath"))
            direction = glyph_orientation_evidence(base, without, returned, proof["glyphRegion"], proof["referenceRegion"])
            check(prefix + "-actual-window-ink-direction", direction["passed"])
            exported = frame(proof["exportPath"])
            ew, eh, export_pixels = region_pixels(exported, proof["exportGlyphRegion"])
            export_weights = [((765 - sum(export_pixels[offset:offset + 3])) * export_pixels[offset + 3] + 382) // 765 for offset in range(0, len(export_pixels), 4)]
            export_direction = lower_stem_analysis(ew, eh, export_weights)
            check(prefix + "-export-ink-direction", export_direction["validInk"] and export_direction["lowerStem"] == "left")
            runtime_direction = None
            if name == "runtime-switch-rtl":
                before = frame(proof["beforeRuntimeChangePath"])
                bw, bh, before_pixels = region_pixels(before, proof["beforeRuntimeChangeGlyphRegion"])
                before_weights = [((765 - sum(before_pixels[offset:offset + 3])) * before_pixels[offset + 3] + 382) // 765 for offset in range(0, len(before_pixels), 4)]
                runtime_direction = lower_stem_analysis(bw, bh, before_weights)
                check(prefix + "-before-runtime-change-ink-direction", runtime_direction["validInk"] and runtime_direction["lowerStem"] == "left")
            direction_results.append({"name": name, "window": direction, "export": export_direction, "beforeRuntimeChange": runtime_direction})
    return {"passed": all(checks.values()), "checks": checks, "failedChecks": [key for key, flag in checks.items() if not flag], "raw": reference(raw_path, directory),
            "nativeSceneAssertions": len(producer_checks), "actualBackend": window.get("actualBackend"), "actualRenderer": inferred, "actualRenderScaling": scale,
            "images": images, "glyphProofs": glyph_results, "glyphOrientationProofs": direction_results, "logicalDisplayMigration": {key: value for key, value in logical.items() if key not in ("checks", "extraKeys", "extraArtifacts")},
            "scope": "Native-window instrumented observations and independent PNG/JPEG structure/pixels; not physical input, compositor capture, hardware GPU, performance or full target-distribution certification."}


def inspect_run(directory, contract, *, expected_platform, expected_architecture="X64"):
    directory = Path(directory).resolve()
    run = load(directory / "run.json")
    plan = load(directory / "plan.json")
    checks = {}
    def check(name, condition):
        checks[name] = condition is True
    check("run-schema", run.get("schema") == "charts-platform-acceptance-run/v1")
    check("plan-schema", plan.get("schema") == "charts-platform-acceptance-plan/v1")
    check("runner-matches-independent-platform", run.get("platform") == expected_platform and platform_from_description(run.get("osDescription")) == expected_platform)
    check("runner-matches-independent-architecture", str(run.get("architecture", "")).upper() == expected_architecture.upper())
    check("native-not-plan-only", run.get("planOnly") is False)
    check("native-processes-completed", run.get("nativeProcessesPassed") is True and run.get("error") is None)
    check("source-set-and-hashes-unchanged", run.get("sourceUnchanged") is True and same_manifest(load(directory / "source-before.json"), load(directory / "source-after.json")))
    assembly_before = load(directory / "assemblies-before.json")
    check("module-set-and-hashes-unchanged", run.get("assembliesUnchanged") is True and same_manifest(assembly_before, load(directory / "assemblies-after.json")))
    planned_cases = plan["cases"]
    run_isolation = run.get("linuxNativeTextIsolationRequested", False)
    check("explicit-native-text-isolation-run-plan", type(run_isolation) is bool and (run_isolation is False or expected_platform == "linux") and all(type(case.get("isolateNativeText", False)) is bool and case.get("isolateNativeText", False) == run_isolation for case in planned_cases))
    ids = [row["id"] for row in planned_cases]
    check("nonempty-unique-case-plan", 1 <= len(ids) <= 96 and len(ids) == len(set(ids)) and all(re.fullmatch(r"[a-zA-Z0-9][a-zA-Z0-9._-]{0,99}", item) is not None for item in ids))
    recorded = {row["id"]: row for row in run["processExecutions"]}
    check("no-duplicate-process-receipt-id", len(recorded) == len(run["processExecutions"]))
    association = run.get("sourceAssociation", {})
    check("same-run-source-rebuild-required", association.get("method") == "same-run-locked-restore-and-nonincremental-source-build" and association.get("hostProject") == "benchmarks/Charts.PlatformAcceptance/Charts.PlatformAcceptance.csproj" and association.get("binaryOnlyQualificationAllowed") is False)
    for identifier, verb in (("restore", "restore"), ("build", "build")):
        try:
            receipt = load(directory / "processes" / identifier / "process.json")
            arguments = receipt["arguments"]
            check("source-association-process-" + identifier, receipt == recorded.get(identifier) and receipt.get("passed") is True and type(receipt.get("exitCode")) is int and receipt["exitCode"] == 0 and receipt.get("timedOut") is False and len(arguments) > 1 and arguments[0] == verb and arguments[1] == association.get("hostProject") and ("--locked-mode" in arguments if identifier == "restore" else "--no-incremental" in arguments))
        except (OSError, ValueError, KeyError, TypeError):
            check("source-association-process-" + identifier, False)
    check("one-receipt-per-planned-native-case", len([key for key in recorded if key.startswith("case-")]) == len(ids) and all("case-" + item in recorded for item in ids))
    results = {}
    for case in planned_cases:
        identifier = case["id"]
        receipt_path = directory / "processes" / ("case-" + identifier) / "process.json"
        try:
            receipt = load(receipt_path)
            arguments = receipt.get("arguments", [])
            isolation_expected = case.get("isolateNativeText", False)
            check("case-native-text-isolation-argv-" + identifier, isinstance(arguments, list) and arguments.count("--isolate-native-text") == (1 if isolation_expected else 0))
            if isolation_expected:
                terminal = load(directory / "cases" / identifier / "raw.json")
                host_parent = str(arguments[0]).replace("\\", "/").rsplit("/", 1)[0]
                native_relative = "runtimes/linux-" + ("x64" if expected_architecture.upper() == "X64" else "arm64") + "/native/libHarfBuzzSharp.so"
                check("case-native-text-library-belongs-to-built-host-" + identifier, (terminal.get("nativeTextIsolation") or {}).get("path") == host_parent + "/" + native_relative)
            check("case-process-" + identifier, receipt.get("passed") is True and type(receipt.get("exitCode")) is int and receipt["exitCode"] == 0 and receipt.get("timedOut") is False and type(receipt.get("processId")) is int and receipt["processId"] > 0 and receipt == recorded["case-" + identifier])
            results[identifier] = inspect_case(directory / "cases" / identifier, case, contract, assembly_before, expected_platform=expected_platform, expected_architecture=expected_architecture, expected_os_description=run.get("osDescription"), expected_process_id=receipt.get("processId"))
        except (OSError, ValueError, KeyError, TypeError) as error:
            results[identifier] = {"passed": False, "error": type(error).__name__ + ": " + str(error)}
    check("all-selected-case-evidence-passes", bool(results) and all(value["passed"] for value in results.values()))
    return {"schema": SCHEMA, "passed": all(checks.values()), "checks": checks, "failedChecks": [key for key, flag in checks.items() if not flag], "cases": results,
            "nativeCaseCount": len(results), "qualification": {"physicalInputValidated": False, "compositorPresentValidated": False, "hardwareGpuCertified": False, "targetDistributionCertified": False, "nativeSelectedItemBridgeCertified": False, "performanceSlaEvaluated": False, "longSoakValidated": False},
            "contract": {"hostSchema": contract["hostSchema"], "nativeCheckKeys": len(contract["requiredCheckKeys"])},
            "scope": "The selected software matrix only. Linux process-scale cases prove observed synthetic RenderScaling, not physical monitor DPI. External sky/system-input/compositor evidence and physical devices require separate receipts."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-directory", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--expected-platform", required=True, choices=("windows", "linux"))
    parser.add_argument("--expected-architecture", required=True, type=str.upper, choices=("X64", "ARM64"))
    parser.add_argument("--contract", type=Path, default=Path(__file__).with_name("charts_platform_acceptance_contract.json"))
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Choose a fresh output file; failed evidence is preserved")
    try:
        contract = load(args.contract)
        report = inspect_run(args.run_directory, contract, expected_platform=args.expected_platform, expected_architecture=args.expected_architecture)
    except (OSError, ValueError, KeyError, TypeError) as error:
        report = {"schema": SCHEMA, "passed": False, "error": type(error).__name__ + ": " + str(error)}
    report["oracleSourceSha256"] = sha(Path(__file__))
    report["pngDecoderSha256"] = sha(Path(__file__).with_name("charts_png_oracle.py"))
    report["contractReference"] = {"path": str(args.contract.resolve()), "sha256": sha(args.contract)} if args.contract.exists() else None
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8", newline="\n") as output:
        json.dump(report, output, ensure_ascii=False, indent=2)
        output.write("\n")
    print(json.dumps({"output": str(args.output), "passed": report["passed"], "sha256": sha(args.output), "caseCount": report.get("nativeCaseCount"), "failedChecks": report.get("failedChecks"), "error": report.get("error")}, ensure_ascii=True))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
