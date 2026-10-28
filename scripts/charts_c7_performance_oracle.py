"""Independent C7 controlled-raster evidence checks and observed statistics.

This program never launches a benchmark, changes a target or fills an unavailable
official/present measurement. Original raw ticks and binary corpus files are the
inputs; summaries are recomputed rather than trusted.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

SCHEMA = "charts-c7-performance-oracle/v1"
MAX_JSON_BYTES = 256 * 1024 * 1024
MAX_CORPUS_BYTES = 32 * 1024 * 1024
FORMAL_TRACKS = ("S10-L-exact", "S10-S-exact", "S10-B-exact", "S100-L-minmax")
FIRST_TRACK = "S1M-L-minmax"
COUNTS = {"S10-L-exact": 10000, "S10-S-exact": 10000, "S10-B-exact": 10000,
          "S100-L-exact": 100000, "S1M-L-exact": 1000000,
          "S100-L-minmax": 100000, "S1M-L-minmax": 1000000}
SEEDS = {"S10-L": 0x1020304050607081, "S10-S": 0x1020304050607082,
         "S10-B": 0x1020304050607083, "S100-L": 0x1020304050607084,
         "S1M-L": 0x1020304050607085}
GENERATOR = "charts-c0-fixed-integer-xorshift64star-v1"
_CORPUS_CACHE = {}


class EvidenceError(ValueError):
    """Missing, contradictory or out-of-scope measurement evidence."""


def require(value, message):
    if not value:
        raise EvidenceError(message)


def load(path):
    path = Path(path)
    require(path.is_file() and path.stat().st_size <= MAX_JSON_BYTES, "Missing or oversized JSON: " + str(path))
    def pairs(items):
        result = {}
        for name, value in items:
            require(name not in result, "Duplicate JSON key: " + name)
            result[name] = value
        return result
    def constant(value):
        raise EvidenceError("Nonfinite JSON constant: " + value)
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=pairs, parse_constant=constant)


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().upper()


def hexadecimal(value, length=64):
    return isinstance(value, str) and len(value) == length and all(c in "0123456789ABCDEFabcdef" for c in value)


def number(value, name="number", minimum=0, integer=False):
    require(type(value) in (int, float) and math.isfinite(value) and value >= minimum,
            name + " must be a finite nonnegative JSON number")
    require(not integer or type(value) is int, name + " must be a JSON integer")
    return value


def field(obj, *names):
    require(isinstance(obj, dict), "Expected object for " + "/".join(names))
    matches = [obj[name] for name in names if name in obj]
    require(matches, "Missing field " + "/".join(names))
    require(all(value == matches[0] for value in matches), "Contradictory aliases " + "/".join(names))
    return matches[0]


def array(value, name, nonempty=True):
    require(isinstance(value, list) and (value or not nonempty), "Missing/empty array " + name)
    return value


def path_in(value, root):
    require(isinstance(value, str) and value and "\0" not in value, "Invalid evidence path")
    path = Path(value)
    if not path.is_absolute():
        path = root / path
    path = path.resolve(strict=True)
    require(path.is_file() and path.is_relative_to(root.resolve()), "Evidence escapes campaign: " + str(path))
    return path


def reference(path, root):
    path = Path(path)
    return {"path": path.relative_to(root).as_posix(), "sha256": sha(path), "bytes": path.stat().st_size}


def percentile(values, fraction):
    require(values and 0 < fraction <= 1, "Cannot compute an empty percentile")
    ordered = sorted(number(v) for v in values)
    return ordered[max(0, math.ceil(len(ordered) * fraction) - 1)]


def describe(values):
    require(values, "Missing observed metric samples")
    return {"count": len(values), "p50": percentile(values, .5), "p95": percentile(values, .95),
            "p99": percentile(values, .99), "maximum": max(values), "minimum": min(values),
            "percentileMethod": "nearest-rank-ceil(p*N)-one-based",
            "qualification": "Observed samples; p99 is descriptive, not a tail-latency SLA."}


def duration(start, end, frequency):
    start = number(start, "startTicks", integer=True)
    end = number(end, "endTicks", integer=True)
    frequency = number(frequency, "stopwatchFrequency", minimum=1, integer=True)
    require(end >= start, "Reversed measured interval")
    return (end - start) * 1000.0 / frequency


def timing(value, frequency):
    start = field(value, "startTicks")
    end = field(value, "endTicks")
    number(field(value, "allocatedBytes"), "allocatedBytes", integer=True)
    elapsed = duration(start, end, frequency)
    if "elapsedMilliseconds" in value:
        require(abs(number(value["elapsedMilliseconds"]) - elapsed) <= max(1e-6, elapsed * 1e-9), "Reported duration differs from ticks")
    return elapsed


def unsupported_claims(raw):
    require(field(raw, "referenceStatus") == "blocked-reference" and field(raw, "referenceMeasurements") is None,
            "Unavailable official reference must remain null/blocked-reference")
    require(field(raw, "presentMeasured") is False and field(raw, "present") is None,
            "Controlled raster cannot claim compositor present")
    require(field(raw, "framePipelineMeasured") is False and field(raw, "framePipeline") is None,
            "Controlled raster cannot claim frame pipeline")
    require(field(raw, "hardwareGpuMeasured") is False and field(raw, "physicalInputMeasured") is False,
            "Deferred physical/GPU/input qualification cannot be claimed")
    for name, value in raw.items():
        if name.lower().startswith("reference") and name not in {"referenceStatus", "referenceMeasurements"}:
            require(value is None, "Cannot calculate a ratio from an unavailable official reference")
        if name in {"presentMs", "requestToPresentMs", "framePipelineMs", "firstUsableToPresentMs"}:
            require(value is None, "Unmeasured frame/present metric must be null")


def _verify_c0_series(data, corpus_id, series_index):
    """Verify all original IEEE754 XY pairs independently of the C# generator."""
    state = SEEDS[corpus_id] + series_index * 37
    mask = (1 << 64) - 1
    kind = "scatter" if corpus_id == "S10-S" else "bar" if corpus_id == "S10-B" else "line"
    for index in range(len(data) // 16):
        state ^= state >> 12
        state ^= (state << 25) & mask
        state ^= state >> 27
        state &= mask
        random = (state * 2685821657736338717) & mask
        wave = index % 1024
        triangle = wave if wave < 512 else 1024 - wave
        y = (triangle * 32 + (random & 511) - 255) / 256.0 + series_index * 3
        if index % 997 == 431:
            y += 128
        if kind == "scatter":
            y = ((random >> 20) & 0xFFFFF) / 1024.0
        if kind == "bar" and index % 3 == 0:
            y = -y - 8
        require(data[index * 16:index * 16 + 16] == struct.pack("<dd", float(index), y), "Binary corpus differs from frozen C0 generator")


def validate_corpus(scene, root):
    track = field(scene, "id")
    require(track in COUNTS, "Unknown controlled-raster track")
    corpus_id = track.rsplit("-", 1)[0]
    manifest = field(scene, "datasetManifest")
    require(field(manifest, "id") == corpus_id and field(manifest, "generatorVersion") == GENERATOR,
            "Wrong source corpus/version")
    require(field(manifest, "seedHex").upper() == f"{SEEDS[corpus_id]:016X}", "Wrong corpus seed")
    expected_series = 2 if corpus_id == "S10-B" else 1
    counts = array(field(manifest, "seriesPointCounts"), "seriesPointCounts")
    require(len(counts) == expected_series and all(c == COUNTS[track] // expected_series for c in counts), "Input scale/series changed")
    require(field(manifest, "inputPointCount") == field(manifest, "validPointCount") == COUNTS[track], "Source data dropped")
    hashes = array(field(manifest, "seriesSha256"), "seriesSha256")
    files = array(field(scene, "datasetFiles"), "datasetFiles")
    require(len(files) == len(hashes) == expected_series, "Incomplete binary corpus files")
    buffers = []
    refs = []
    for index, row in enumerate(files):
        path = path_in(field(row, "path"), root)
        expected_bytes = counts[index] * 16
        require(path.stat().st_size == expected_bytes <= MAX_CORPUS_BYTES, "Binary corpus point count changed")
        require(field(row, "seriesIndex") == index and field(row, "pointCount") == counts[index] and field(row, "bytes") == expected_bytes,
                "Binary dataset receipt changed series identity/count/length")
        actual_sha = sha(path)
        require(hexadecimal(hashes[index]) and actual_sha == hashes[index].upper(), "Binary corpus SHA differs from manifest")
        require(field(row, "sha256").upper() == actual_sha, "Dataset file receipt SHA mismatch")
        if actual_sha not in _CORPUS_CACHE:
            data = path.read_bytes()
            _verify_c0_series(data, corpus_id, index)
            _CORPUS_CACHE[actual_sha] = (corpus_id, index, data)
        cached_id, cached_index, data = _CORPUS_CACHE[actual_sha]
        require(cached_id == corpus_id and cached_index == index, "Corpus reused with a different semantic identity")
        buffers.append(data)
        refs.append(reference(path, root))
    revision = number(field(manifest, "revision"), "dataset revision", integer=True)
    combined = "\n".join([GENERATOR, corpus_id, f"{SEEDS[corpus_id]:016X}", str(revision)] + [h.upper() for h in hashes])
    require(hashlib.sha256(combined.encode()).hexdigest().upper() == field(manifest, "sha256").upper(), "Aggregate dataset manifest SHA mismatch")
    return buffers, refs


def validate_query(query, buffers, frequency, track=None):
    require(field(query, "passed") is True and field(query, "referenceMatches") is True, "Original-data hit failed")
    series = number(field(query, "probeSeries"), "probeSeries", integer=True)
    index = number(field(query, "probeIndex"), "probeIndex", integer=True)
    require(series < len(buffers) and index < len(buffers[series]) // 16, "Query probe is outside original source")
    ordinal = number(field(query, "index"), "query index", integer=True)
    require(series == ordinal % len(buffers), "Query changed its fixed source-series trace")
    actual_series = number(field(query, "actualSeries"), "actualSeries", integer=True)
    actual_index = number(field(query, "actualIndex"), "actualIndex", integer=True)
    require(actual_series < len(buffers) and actual_index < len(buffers[actual_series]) // 16 and field(query, "actualStableId") == actual_index,
            "Hit no longer returns original series/index/stable ID")
    if track != "S10-B-exact":
        require(actual_series == series and actual_index == index, "Exact point query returned a different original point")
    revision = number(field(query, "revision"), "query revision", integer=True)
    x, y = struct.unpack_from("<dd", buffers[actual_series], actual_index * 16)
    if revision % 2:
        y += .25
    xb, yb = struct.unpack("<QQ", struct.pack("<dd", x, y))
    require(field(query, "originalXBits").upper() == field(query, "actualXBits").upper() == f"{xb:016X}" and
            field(query, "originalYBits").upper() == field(query, "actualYBits").upper() == f"{yb:016X}", "Hit coordinates differ from original binary data")
    plot = array(field(query, "plot"), "query plot")
    view = array(field(query, "range"), "query range")
    require(len(plot) == len(view) == 4 and all(type(v) in (int, float) and math.isfinite(v) for v in plot + view), "Invalid hit projection")
    require(plot[2] > 0 and plot[3] > 0 and view[1] > view[0] and view[3] > view[2], "Degenerate query viewport")
    lo = max(0, min(len(buffers[series]) // 16 - 1, math.ceil(view[0])))
    hi = max(lo, min(len(buffers[series]) // 16 - 1, math.floor(view[1])))
    require(index == lo + (ordinal * 104729 + 37) % (hi - lo + 1), "Fixed original-query index trace changed")
    x, y = struct.unpack_from("<dd", buffers[series], index * 16)
    if revision % 2:
        y += .25
    px = plot[0] + (x - view[0]) / (view[1] - view[0]) * plot[2]
    py = plot[1] + plot[3] - (y - view[2]) / (view[3] - view[2]) * plot[3]
    require(abs(field(query, "queryX") - px) < 1e-6 and abs(field(query, "queryY") - py) < 1e-6, "Query is not projected from its original point")
    if track == "S10-B-exact":
        validate_bar_geometry(query, buffers, plot, view, actual_series, actual_index, revision)
    elapsed = timing(field(query, "timing"), frequency)
    maintenance = field(query, "maintenance")
    if maintenance is not None:
        timing(maintenance, frequency)
        require(maintenance["endTicks"] <= query["timing"]["startTicks"], "Source/viewport maintenance overlaps an isolated query metric")
    return elapsed, maintenance is not None


def validate_bar_geometry(query, buffers, plot, view, series, index, revision):
    """Independently derive the frozen grouped linear-bar rectangle and tolerance.

    ID/XY consistency alone cannot prove a spatial hit. Min-width bars overlap;
    this checks the returned original bar's geometry, not a fabricated unique
    nearest-column guarantee.
    """
    require(len(buffers) == 2, "Grouped bar benchmark requires two original series")
    x, y = struct.unpack_from("<dd", buffers[series], index * 16)
    y += .25 if revision % 2 else 0
    sx = plot[0] + (x - view[0]) / (view[1] - view[0]) * plot[2]
    sy = plot[1] + plot[3] - (y - view[2]) / (view[3] - view[2]) * plot[3]
    baseline = max(plot[1], min(plot[1] + plot[3], plot[1] + plot[3] - (0 - view[2]) / (view[3] - view[2]) * plot[3]))
    group = max(1, min(120, plot[2] / (view[1] - view[0]) * .72))
    slot = group / 2
    width = max(.5, slot * .86)
    full_left = sx - group / 2 + series * slot + (slot - width) / 2
    left, right = max(plot[0], full_left), min(plot[0] + plot[2], full_left + width)
    top, bottom = max(plot[1], min(sy, baseline)), min(plot[1] + plot[3], max(sy, baseline))
    require(right > left and bottom > top, "Returned original bar has no visible geometry")
    qx, qy = field(query, "queryX"), field(query, "queryY")
    dx, dy = max(left - qx, 0, qx - right), max(top - qy, 0, qy - bottom)
    require(dx * dx + dy * dy <= 1 + 1e-7, "Returned original bar is outside the fixed geometric query tolerance")


def validate_counts(value, track):
    require(field(value, "inputCount") == field(value, "validCount") == COUNTS[track], "Runtime input/valid count differs from original data")
    visible = number(field(value, "visibleCount"), "visibleCount", minimum=1, integer=True)
    rendered = number(field(value, "renderedCount"), "renderedCount", minimum=1, integer=True)
    require(rendered <= visible <= COUNTS[track], "Invalid visible/rendered counts")
    number(field(value, "primitiveCount"), "primitiveCount", minimum=1, integer=True)
    info = array(field(value, "samplingInfo"), "samplingInfo", nonempty=False)
    require(len(info) == (2 if track == "S10-B-exact" else 1) and
            sum(field(row, "sourceCount") for row in info) == COUNTS[track] and
            sum(field(row, "visibleCount") for row in info) == visible and
            sum(field(row, "renderedCount") for row in info) == rendered, "Actual sampling/source counters differ from runtime totals")
    if track.endswith("-minmax"):
        require(len(info) == 1, "Missing actual line sampling information")
        require(type(field(info[0], "requestedMode")) in (int, str) and info[0]["requestedMode"] in (1, "MinMax") and field(info[0], "sourceCount") == COUNTS[track], "MinMax track did not request actual MinMax source plan")
        require(field(info[0], "visibleCount") == visible and field(info[0], "renderedCount") == rendered, "Sampling counters disagree")
        buckets = number(field(info[0], "pixelBucketCount"), "pixelBucketCount", minimum=1, integer=True)
        require(field(info[0], "isApplied") is True and rendered < visible and field(info[0], "figureCount") == 1 and
                field(info[0], "renderScale") == field(info[0], "pixelWidth") == 1 and field(info[0], "fallbackReason") is None and
                rendered <= 4 * buckets + 2, "Declared MinMax track is not an actual bounded first/last/min/max reduction")
    elif info:
        require(all(type(field(row, "requestedMode")) in (int, str) and row["requestedMode"] in (0, "None") and
                    field(row, "isApplied") is False and row["visibleCount"] == row["renderedCount"] for row in info), "Exact track enabled sampling")
    return {"inputCount": COUNTS[track], "visibleCount": visible, "renderedCount": rendered,
            "primitiveCount": value["primitiveCount"]}


def validate_operation(operation, track, buffers, frequency, first=False):
    require(field(operation, "passed") is True and field(operation, "error") is None, "Failed measured operation retained")
    ticks = field(operation, "ticks")
    stages = ("sourceApply", "prepare", "record", "controlledRaster", "hit")
    observed = {}
    previous_end = field(ticks, "start" if first else "request")
    number(previous_end, "request/start ticks", integer=True)
    for stage in stages:
        start_name, end_name = stage + "Start", stage + "End"
        if stage == "record" and start_name not in ticks and first:
            continue
        # Trace source/prepare share the previous stage's end when no explicit
        # start is emitted; this never sums overlapping percentile estimates.
        start = ticks.get(start_name, previous_end)
        end = field(ticks, end_name)
        require(start >= previous_end, "Overlapping/reordered measurement stages")
        observed[stage + "Ms"] = duration(start, end, frequency)
        previous_end = end
    require(field(ticks, "end") >= previous_end, "Operation terminal end does not contain all measured stages and original query")
    if first:
        require(field(ticks, "end") >= previous_end, "First-use interval excludes its successful hit")
        observed["firstUsableToControlledRasterMs"] = duration(ticks["start"], ticks["end"], frequency)
    else:
        observed["requestToControlledRasterMs"] = duration(ticks["request"], ticks["controlledRasterEnd"], frequency)
    query = field(operation, "hit")
    require(field(operation, "revision") == field(query, "revision"), "Operation and original hit revisions differ")
    require(ticks["hitStart"] == query["timing"]["startTicks"] and ticks["hitEnd"] == query["timing"]["endTicks"], "Operation/query timing boundaries disagree")
    validate_query(query, buffers, frequency, track)
    validate_counts(field(operation, "counts"), track)
    allocations = field(operation, "allocations")
    expected_allocations = {"sourceApply", "prepare", "controlledRaster", "firstUsableToControlledRaster"} if first else {"sourceApply", "prepare", "record", "controlledRaster"}
    require(set(allocations) == expected_allocations, "Missing synchronous allocation-stage observations")
    for name, allocated in allocations.items():
        number(allocated, "thread allocation " + name, integer=True)
        observed[name + "AllocatedBytes"] = allocated
    observed["hitAllocatedBytes"] = query["timing"]["allocatedBytes"]
    if first:
        require(allocations["firstUsableToControlledRaster"] >= sum(value for name, value in allocations.items() if name != "firstUsableToControlledRaster") + observed["hitAllocatedBytes"],
                "First-use allocation total excludes its isolated synchronous stages")
    return observed


def validate_quality(quality, track):
    require(field(quality, "samplingMode") == ("MinMax" if track.endswith("-minmax") else "None"), "Unequal sampling quality")
    require(field(quality, "strokeThicknessDip") == 2 and
            field(quality, "scatterMarkerSizeDip") == (4 if track == "S10-S-exact" else 0), "Unequal stroke/marker quality")
    require(field(quality, "showLegend") is False and field(quality, "showGridLines") is True and
            field(quality, "showDataLabels") is False and field(quality, "animations") == "None", "Unequal labels/grid/animation quality")
    require(field(quality, "fontSizeDip") == 12 and field(quality, "fontFamily") == "DejaVu Sans, Segoe UI" and field(quality, "chartDips") == [1200, 650] and
            field(quality, "rasterPixels") == [1200, 650] and field(quality, "rasterDpi") == [96, 96], "Unequal chart/font/raster dimensions")
    require(field(quality, "corpusManifestDownsamplingIsRuntimeQuality") is False and
            field(quality, "sourceReplacement") == "alternate original Y and Y+0.25; X/Id/order/count unchanged; arrays prepared outside measured operations",
            "Missing or changed source-adapter quality contract")
    bars = array(field(quality, "barOptions"), "actual bar options", nonempty=False)
    expected_bar = dict(orientation="Vertical", stackMode="None", baseline=0, barWidth=None, widthRatio=.86, showMarkers=False)
    require(bars == [expected_bar, expected_bar] if track == "S10-B-exact" else bars == [], "Actual bar options differ from independent geometry assumptions")
    return quality


def validate_environment(raw, platform):
    env = field(raw, "nativeEnvironment")
    description = field(env, "os").lower()
    inferred = "windows" if "windows" in description else "linux" if any(s in description for s in ("linux", "ubuntu", "debian", "fedora")) else "unknown"
    require(inferred == platform and field(env, "architecture") == "X64", "Producer execution platform differs from parent campaign")
    require(field(env, "nativeHandleDescriptor") == ("HWND" if platform == "windows" else "XID") and
            field(env, "backend").startswith("Avalonia.Win32." if platform == "windows" else "Avalonia.X11."), "Native backend differs from parent platform")
    number(field(env, "nativeHandle"), "nativeHandle", minimum=1, integer=True)
    require(field(env, "requestedRenderer") == "software" and field(env, "actualRenderer") == "software-raster", "Missing explicit software-renderer observation")
    surface = field(env, "nativeSurface")
    require(field(surface, "hasGrContext") is False and field(surface, "error") is None, "Native draw surface did not observe software rendering")
    require(field(env, "renderScaling") == 1, "Current protocol requires actual native scale 1")
    require(field(env, "tieredCompilationSwitchPresent") is True and field(env, "tieredCompilationSwitchValue") is False,
            "Measured JIT protocol differs from fixed non-tiered runtime")
    require(field(env, "dotnetTieredCompilation") in (None, "0", "false", "False") and
            field(env, "comPlusTieredCompilation") in (None, "0", "false", "False"), "Environment overrides the fixed JIT protocol")
    before = field(raw, "nativeTextBeforeUi")
    after = field(raw, "nativeTextAfterRealText")
    if platform == "linux":
        require(field(before, "status") == field(after, "status") == "Initialized" and
                field(before, "resolverObserved") is False and field(after, "resolverObserved") is True and
                field(after, "resolverInvocationCount") > 0 and field(after, "deepBindingEnabled") is True,
                "Linux ordinary early text binding was not exercised")
        require(field(after, "assetPath") == field(after, "symbolProviderPath") and
                field(after, "assetSha256") == "1D5C3AFEF13545BF34BF8F068B14E25EE619C3B6DEE235C44E260FE61CB24018" and
                field(after, "packageVersion") == "8.3.1.3" and field(after, "nativeVersion") == "8.3.1", "Unknown native text assets/provider")
    else:
        require(field(before, "status") == field(after, "status") == "NotApplicable", "Windows should use normal platform text loading")
    fonts = array(field(env, "fontProbes"), "actual numeric font probes")
    require(len(fonts) == 2 and {p.get("codepoint") for p in fonts} == {"U+30", "U+46"}, "Missing actual numeric/reference font probes")
    for font in fonts:
        require(field(font, "matched") is True and number(field(font, "glyphId"), minimum=1, integer=True) > 0 and
                hexadecimal(field(font, "fontSha256")) and number(field(font, "fontBytes"), minimum=1, integer=True) > 0 and
                isinstance(field(font, "family"), str) and font["family"], "Actual measurement font is unavailable/unfingerprinted")
    number(field(env, "cpuCount"), "cpuCount", minimum=1, integer=True)
    require(isinstance(field(env, "culture"), str), "Missing explicit axis-label culture")
    require(type(field(env, "gcServer")) is bool and isinstance(field(env, "gcLatencyMode"), str) and field(env, "stopwatchHighResolution") is True,
            "Missing measured CPU/GC/clock configuration")
    return {name: env[name] for name in ("os", "architecture", "runtime", "backend", "renderScaling", "requestedRenderer", "actualRenderer",
                                        "tieredCompilationSwitchPresent", "tieredCompilationSwitchValue", "defaultFont", "fontProbes", "cpuCount", "gcServer", "gcLatencyMode", "culture")}


def validate_artifacts(scene, root, first_only):
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from charts_png_oracle import decode_png
    artifacts = array(field(scene, "artifacts"), "representative raster artifacts")
    phases = {row.get("phase") for row in artifacts}
    require(len(artifacts) == (1 if first_only else 2) and phases == ({"first-use"} if first_only else {"first-use", "after-hit-batch"}),
            "Incomplete representative raster artifact set")
    result = []
    frames = {}
    seen = set()
    for artifact in artifacts:
        path = path_in(field(artifact, "path"), root)
        require(path not in seen, "Repeated raster artifact")
        seen.add(path)
        require(path.stat().st_size == field(artifact, "bytes") and sha(path) == field(artifact, "sha256").upper(), "Raster receipt mismatch")
        w, h, rgba = decode_png(path)
        require((w, h) == (1200, 650) == (field(artifact, "pixelWidth"), field(artifact, "pixelHeight")), "Raster dimensions differ from quality profile")
        blue = sum(rgba[i] < 100 and 40 < rgba[i + 1] < 160 and rgba[i + 2] > 120 and rgba[i + 2] > rgba[i] * 1.4 and rgba[i + 3] > 200
                   for i in range(0, len(rgba), 4))
        require(blue >= 10, "Representative raster contains no declared blue business geometry")
        frames[artifact["phase"]] = (w, h, rgba)
        result.append({**reference(path, root), "businessBluePixels": blue, "phase": artifact["phase"]})
    restoration = {"status": "not-applicable-first-use-only", "equalPixels": None}
    if not first_only:
        queries = array(field(scene, "hitQueries"), "final restoration query")
        restoration = raster_restoration(scene["firstUse"]["hit"], queries[-1], frames["first-use"], frames["after-hit-batch"])
    return result, restoration


def raster_restoration(first_query, final_query, first_frame, final_frame):
    eligible = field(final_query, "revision") % 2 == 0 and field(final_query, "range") == field(first_query, "range")
    if not eligible:
        return {"status": "not-comparable-different-final-state", "equalPixels": None,
                "finalRevision": final_query["revision"], "initialRange": first_query["range"], "finalRange": final_query["range"]}
    require(first_frame == final_frame, "Same original source/view did not restore the exact decoded raster")
    return {"status": "passed", "equalPixels": True, "finalRevision": final_query["revision"],
            "decodedRgbaSha256": hashlib.sha256(first_frame[2]).hexdigest().upper(),
            "qualification": "Same-state software raster restoration; not an official-runtime visual golden."}


def validate_memory(scene, frequency, first_only):
    endpoints = array(field(scene, "memoryEndpoints"), "memory endpoints")
    expected_phases = ["after-prepared-corpus-before-source-binding"] if first_only else [
        "after-prepared-corpus-before-source-binding", "after-warmup", "after-measured", "after-interaction-and-hit-batch"]
    require([row.get("phase") for row in endpoints] == expected_phases, "Memory endpoint observations are incomplete/reordered")
    observations = [field(row, "observation") for row in endpoints] + [field(scene["firstUse"], "memoryAfter")]
    for observation in observations:
        duration(field(observation, "startTicks"), field(observation, "endTicks"), frequency)
        for name in ("workingSetBytes", "privateBytes", "peakWorkingSetBytes", "managedHeapBytes", "allocatedBytes", "processCpuTicks"):
            number(field(observation, name), name, integer=True)
        require(field(observation, "privateMeasurementKind") in {"private-commit", "Process.PrivateMemorySize64-platform-definition"}, "Missing platform-specific private-memory semantics")
        require(field(observation, "peakPrivateBytes") is field(observation, "nativeBytes") is field(observation, "gpuBytes") is None,
                "Endpoint snapshots cannot invent peak private, native or GPU bytes")
    result = {"qualification": "Process endpoints and OS lifetime peak working set only. No sampled/matched-empty-host peak private/native/GPU budget is evaluated.",
              "endpoints": endpoints, "afterFirstUseAndMetadataRecord": scene["firstUse"]["memoryAfter"],
              "peakPrivateBudgetStatus": "unmeasured", "nativeBytes": None, "gpuBytes": None}
    if not first_only:
        before, after = endpoints[1]["observation"], endpoints[2]["observation"]
        wall = duration(before["endTicks"], after["endTicks"], frequency) / 1000
        require(wall > 0 and after["processCpuTicks"] >= before["processCpuTicks"] and after["allocatedBytes"] >= before["allocatedBytes"], "Non-monotonic process CPU/allocation endpoints")
        result["measuredPhaseProcessEndpointDelta"] = {
            "wallSeconds": wall, "cpuSeconds": (after["processCpuTicks"] - before["processCpuTicks"]) / 10000000,
            "coreEquivalent": (after["processCpuTicks"] - before["processCpuTicks"]) / 10000000 / wall,
            "totalAllocatedBytesEstimate": after["allocatedBytes"] - before["allocatedBytes"],
            "privateBytesDelta": after["privateBytes"] - before["privateBytes"],
            "managedHeapEstimateDelta": after["managedHeapBytes"] - before["managedHeapBytes"],
            "qualification": "Observed whole-process endpoints around the phase; includes instrumentation/scheduling, not isolated UI/raster-thread CPU or a retained-memory/peak budget."}
    return result


def validate_schedule(scene, options, buffers, frequency, formal):
    schedules = array(field(scene, "schedules"), "schedules")
    require(len(schedules) == 2 and [s.get("phase") for s in schedules] == ["warmup", "measured"], "Missing or repeated/reordered warmup/measured phase")
    traces = array(field(scene, "traceSamples"), "traceSamples")
    observed = []
    phase_results = []
    failed_operations = []
    revision = scene["firstUse"]["revision"]
    require([row.get("phase") for row in traces] == [s["phase"] for s in schedules for _ in range(s["attempted"])], "Raw phase ordering changed")
    previous_phase_end = 0
    for schedule in schedules:
        phase = field(schedule, "phase")
        seconds = field(options, "warmupSeconds" if phase == "warmup" else "measureSeconds")
        minimum = (10 if phase == "warmup" else 30) if formal else (2 if phase == "warmup" else 3)
        require(number(seconds) == minimum if formal else number(seconds) >= minimum,
                "Formal trace durations must exactly match frozen 10/30 seconds; development minimums cannot replace them")
        planned = math.floor(seconds * 60 + .5)
        attempted = number(field(schedule, "attempted"), "attempted", integer=True)
        completed = number(field(schedule, "completed"), "completed", integer=True)
        failed = number(field(schedule, "failed"), "failed", integer=True)
        pending = number(field(schedule, "pending"), "pending", integer=True)
        require(field(schedule, "requestedHz") == 60 and field(schedule, "planned") == planned and
                attempted <= planned and completed + failed == attempted and pending == planned - attempted,
                "Failed/dropped/pending scheduled requests cannot disappear")
        structural_success = completed == planned and failed == pending == 0
        require(type(field(schedule, "passed")) is bool and (schedule["passed"] is False or structural_success), "Failed schedule falsely declares all slots completed")
        start = number(field(schedule, "startTicks"), integer=True)
        target_end = number(field(schedule, "plannedEndTicks"), integer=True)
        actual_end = number(field(schedule, "actualEndTicks"), integer=True)
        require(start >= previous_phase_end, "Measured phase starts before warmup drained")
        require(abs((target_end - start) / frequency - seconds) <= 1 / frequency and actual_end >= start and
                field(schedule, "maximumDrainSeconds") == 15, "Schedule duration/drain changed")
        phase_traces = [t for t in traces if t.get("phase") == phase]
        require(len(phase_traces) == attempted, "Missing, silently filtered or duplicated operation samples")
        require(sum(t.get("passed") is True for t in phase_traces) == completed and
                sum(t.get("passed") is False for t in phase_traces) == failed, "Schedule outcome counts differ from retained attempts")
        previous_request = start
        previous_end = start
        phase_values = []
        for index, operation in enumerate(phase_traces):
            require(field(operation, "index") == index and field(operation, "action") == ("sourceReplace" if index % 10 == 0 else "viewport"), "Fixed trace action/sequence changed")
            revision += index % 10 == 0
            require(field(operation, "revision") == revision, "Source replacement/revision work disappeared from the fixed trace")
            ticks = field(operation, "ticks")
            due = start + math.floor(index * frequency / 60)
            require(abs(number(field(ticks, "scheduled"), integer=True) - due) <= 1 and
                    field(ticks, "request") >= previous_request and field(ticks, "request") >= previous_end and
                    field(ticks, "request") >= due - frequency / 1000, "Trace request times do not match the fixed input schedule")
            if operation["passed"]:
                values = validate_operation(operation, scene["id"], buffers, frequency)
                values["scheduledToControlledRasterMs"] = duration(ticks["scheduled"], ticks["controlledRasterEnd"], frequency)
                values["queueLatenessMs"] = duration(min(ticks["scheduled"], ticks["request"]), ticks["request"], frequency)
                phase_values.append(values)
            else:
                require(isinstance(operation.get("error"), str) and operation["error"], "Failed operation lacks its retained error")
                available = {}
                for stage in ("sourceApply", "prepare", "record", "controlledRaster", "hit"):
                    if stage + "Start" in ticks and stage + "End" in ticks:
                        require(ticks["request"] <= ticks[stage + "Start"] <= ticks[stage + "End"] <= field(ticks, "end"),
                                "Failed operation stage lies outside its retained request/end boundary")
                        available[stage + "Ms"] = duration(ticks[stage + "Start"], ticks[stage + "End"], frequency)
                failed_operations.append({"phase": phase, "index": index, "error": operation["error"], "availableStageMs": available,
                                          "statisticsQualification": "Failed correctness/operation sample, retained separately; not a successful performance sample."})
            previous_request, previous_end = ticks["request"], field(ticks, "end")
            require(previous_end <= actual_end, "Operation is outside its recorded schedule")
        if phase == "measured":
            observed = phase_values
        workload_passed = structural_success and schedule["passed"] and actual_end <= target_end + 15 * frequency
        phase_results.append({"phase": phase, "planned": planned, "attempted": attempted, "completed": completed,
                              "actualDurationMs": duration(start, actual_end, frequency), "pending": pending, "failed": failed,
                              "drainOverrunMs": max(0, duration(target_end, max(target_end, actual_end), frequency) - 15000),
                              "workloadPassed": workload_passed,
                              "statisticsQualification": "Complete fixed workload" if workload_passed else "Partial/failed fixed workload; observed completed samples only, never a full-workload pass."})
        previous_phase_end = actual_end
    require(len(traces) == sum(row["attempted"] for row in schedules), "Unexpected or hidden trace phase")
    return observed, phase_results, revision, failed_operations


def validate_scene(scene, raw, root, first_only, formal):
    require(type(field(scene, "passed")) is bool and (scene["error"] is None or isinstance(scene["error"], str)), "Malformed scene outcome")
    track = field(scene, "id")
    validate_quality(field(scene, "runtimeQuality"), track)
    buffers, corpus = validate_corpus(scene, root)
    frequency = raw["stopwatchFrequency"]
    timing(field(scene, "datasetGeneration"), frequency)
    first = validate_operation(field(scene, "firstUse"), track, buffers, frequency, first=True)
    require(_utc(field(raw, "startedUtc")) <= _utc(field(scene, "firstUseCompletedUtc")) <= _utc(field(raw, "finishedUtc")), "First-use wall-clock checkpoint is outside the process report")
    require(scene["firstUse"]["ticks"]["start"] >= scene["datasetGeneration"]["endTicks"], "External source generation is mixed into the first-use boundary")
    checks = field(scene, "checks")
    require(checks and all(type(value) is bool for value in checks.values()) and scene["passed"] == all(checks.values()), "Missing/contradictory scene correctness checks")
    artifacts, restoration = validate_artifacts(scene, root, first_only)
    result = {"id": track, "firstUse": first, "corpus": corpus,
              "runtimeQuality": scene["runtimeQuality"], "artifacts": artifacts, "rasterRestoration": restoration,
              "producerPassed": scene["passed"], "producerError": scene["error"], "producerChecks": checks,
              "memoryObservations": validate_memory(scene, frequency, first_only)}
    if first_only:
        require(scene["traceSamples"] == scene["hitWarmups"] == scene["hitQueries"] == scene["schedules"] == [], "First-use-only process contains warmed measurement work")
        require(scene["passed"], "Failed first-use process cannot qualify")
    else:
        values, schedules, revision, failed_operations = validate_schedule(scene, raw["options"], buffers, frequency, formal)
        warmups = array(field(scene, "hitWarmups"), "hit warmups")
        queries = array(field(scene, "hitQueries"), "hit queries")
        require(len(warmups) == 100 and len(queries) == 1000, "Hit warmup/query sample count changed")
        query_values = []
        updated = 0
        maintenance = []
        for index, query in enumerate(warmups + queries):
            require(field(query, "index") == index and field(query, "phase") == ("warmup" if index < 100 else "measured"), "Hit-batch sequence/phase changed")
            revision += index % 10 == 0
            require(field(query, "revision") == revision, "Hit-batch source update work disappeared")
            elapsed, has_update = validate_query(query, buffers, frequency, track)
            require(has_update == (index % 10 == 0), "Fixed source-maintenance trace changed")
            if index >= 100:
                query_values.append(elapsed)
                updated += has_update
                if has_update:
                    maintenance.append(timing(query["maintenance"], frequency))
        require(updated >= 100, "Insufficient post-maintenance query coverage")
        result.update({"metricSamples": values, "schedules": schedules,
                       "metrics": {name: describe([v[name] for v in values]) for name in values[0]} if values else {},
                       "hitQueryMs": describe(query_values), "postMaintenanceQueries": updated,
                       "maintenanceMs": describe(maintenance),
                       "failedOperations": failed_operations,
                       "workloadSchedulesPassed": scene["passed"] and all(s["workloadPassed"] for s in schedules),
                       "absoluteHitGate": {"status": "passed" if percentile(query_values, .95) <= 8 else "failed",
                                           "p95MaximumMs": 8, "observedP95Ms": percentile(query_values, .95)}})
    return result


def _utc(value):
    require(isinstance(value, str), "Missing UTC timestamp")
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise EvidenceError("Invalid timestamp") from error
    require(parsed.tzinfo is not None, "Timestamp needs an explicit UTC offset")
    return parsed.astimezone(dt.timezone.utc)


def validate_run(entry, root, platform, phase, expected_sources):
    raw_path = path_in(field(entry, "raw"), root)
    receipt_path = path_in(field(entry, "receipt"), root)
    raw, receipt = load(raw_path), load(receipt_path)
    require(field(raw, "schema") == "charts-c7-performance/v1", "Unknown measurement schema")
    require(field(receipt, "timedOut") is False and type(field(receipt, "exitCode")) is int and field(receipt, "exitCode") in (0, 1),
            "Crashed/timed-out child lacks complete qualifying evidence")
    producer_passed = field(raw, "passed")
    require(type(producer_passed) is bool and field(receipt, "passed") == producer_passed and
            receipt["exitCode"] == (0 if producer_passed else 1), "Child exit/terminal producer outcomes disagree")
    require(field(raw, "processId") == field(receipt, "processId") and field(raw, "status") == ("completed" if producer_passed else "failed") and
            (field(raw, "error") is None or isinstance(raw["error"], str)), "Missing/contradictory terminal raw or process mismatch")
    require(not producer_passed or raw["error"] is None, "Successful terminal report retains a fatal error")
    started, finished = _utc(raw["startedUtc"]), _utc(raw["finishedUtc"])
    launched, exited = _utc(receipt["startedUtc"]), _utc(receipt["finishedUtc"])
    process_started = _utc(field(raw, "processStartedUtc"))
    require(launched - dt.timedelta(seconds=2) <= process_started <= started <= finished <= exited + dt.timedelta(seconds=2), "Raw data is stale or outside its process launch interval")
    unsupported_claims(raw)
    env_key = validate_environment(raw, platform)
    for label, expected_name in (("charts", "chartsSha256"), ("driver", "harnessSha256")):
        fingerprint = field(field(raw, "sources"), label)
        expected = field(expected_sources, expected_name)
        require(hexadecimal(expected) and field(fingerprint, "sha256").upper() == expected.upper(), "Executed product/host differs from campaign build")
    options = field(raw, "options")
    first_only = field(options, "firstUseOnly")
    require(type(first_only) is bool and first_only == (field(entry, "kind") == "first-use"), "Wrong role/first-use process")
    scenes = array(field(raw, "scenes"), "scenes")
    requested = array(field(options, "scenarios"), "requested scenarios")
    require(len(scenes) == len(requested) and {s["id"] for s in scenes} == set(requested) and len(set(requested)) == len(requested), "Unexpected/missing requested scene")
    require(producer_passed == all(s.get("passed") is True for s in scenes), "Failed scenes were hidden by the process outcome")
    if first_only:
        require(requested == [FIRST_TRACK], "Fresh first-use process must contain only the critical MinMax million-point track")
    else:
        require(set(requested).issubset(FORMAL_TRACKS), "Formal interaction track includes an unbudgeted/deferred workload")
    number(raw["stopwatchFrequency"], "stopwatchFrequency", minimum=1, integer=True)
    result = {"kind": entry["kind"], "role": entry["role"], "processId": raw["processId"],
              "processStartedUtc": raw["processStartedUtc"], "raw": reference(raw_path, root),
              "receipt": reference(receipt_path, root), "environment": env_key,
              "producerPassed": producer_passed, "exitCode": receipt["exitCode"],
              "scenes": [validate_scene(scene, raw, raw_path.parent, first_only, phase == "formal") for scene in scenes]}
    return result, (raw["processId"], process_started.isoformat()), raw_path.resolve()


def validate_campaign(campaign_path, protocol_path):
    campaign_path = Path(campaign_path).resolve()
    root = campaign_path.parent
    campaign, protocol = load(campaign_path), load(protocol_path)
    require(field(campaign, "schemaVersion") == "charts-c7-performance-campaign/v1" and
            field(protocol, "schemaVersion") == "charts-c7-performance-protocol/v1", "Wrong campaign/protocol schema")
    formal_protocol = field(protocol, "formal")
    require(field(formal_protocol, "interactionTracks") == list(FORMAL_TRACKS) and
            field(formal_protocol, "independentProcessesPerInteractionTrack") == 5 and
            field(formal_protocol, "warmupSeconds") == 10 and field(formal_protocol, "measurementSeconds") == 30 and
            field(formal_protocol, "plannedMeasuredRequests") == 1800 and field(formal_protocol, "targetRequestsPerSecond") == 60 and
            field(formal_protocol, "hitWarmupsPerTrackProcess") == 100 and field(formal_protocol, "hitQueriesPerTrackProcess") == 1000 and
            field(formal_protocol, "firstUseIndependentProcesses") == 100 and field(formal_protocol, "firstUseCalibrationProcesses") == 2,
            "Frozen formal policy was weakened or changed without a new protocol implementation")
    require(field(campaign, "protocolSha256").upper() == sha(protocol_path), "Measured protocol changed")
    platform, phase = field(campaign, "platform"), field(campaign, "phase")
    require(platform in {"windows", "linux"} and phase in {"formal", "development"}, "Unknown campaign phase/platform")
    source_proof = None
    if "sourceManifest" in campaign:
        before_path = path_in(field(campaign["sourceManifest"], "path"), root)
        require(sha(before_path) == field(campaign["sourceManifest"], "sha256").upper(), "Source manifest changed after launch")
        before = array(load(before_path), "measured source inputs")
        after_path = root / "source-after.json"
        require(after_path.is_file() and load(after_path) == before, "Measured source inputs changed during the campaign")
        required_inputs = {"docs/charts-performance-budgets.json", "scripts/charts-c7-performance-protocol.json",
                           "scripts/charts_c7_performance_oracle.py", "benchmarks/Charts.Benchmarks/DatasetFactory.cs"}
        require(required_inputs.issubset({row.get("path") for row in before}), "Missing measured policy/source inputs")
        source_proof = {"before": reference(before_path, root), "after": reference(after_path, root), "files": len(before), "unchanged": True}
    else:
        require(phase == "development", "Formal campaign requires immutable before/after source manifests")
    official = field(campaign, "reference")
    require(field(official, "status") == "blocked-reference" and field(official, "measurements") is None and field(official, "ratios") is None,
            "No authorized official reference is available; ratios must remain null")
    present = field(campaign, "present")
    require(field(present, "status") == "blocked-present" and field(present, "measurements") is None, "Controlled raster is not present")
    entries = array(field(campaign, "runs"), "campaign runs")
    seen_identities, seen_paths = set(), set()
    rows, errors = [], []
    for index, entry in enumerate(entries):
        try:
            require(entry["kind"] in {"interaction", "first-use"} and entry["role"] in {"formal", "calibration", "development"}, "Invalid process role")
            require((phase == "formal" and entry["role"] in {"formal", "calibration"}) or (phase == "development" and entry["role"] == "development"), "Development samples cannot replace a formal run")
            require(entry["role"] != "calibration" or entry["kind"] == "first-use", "Only first-use calibration processes may be excluded from formal statistics")
            row, identity, raw_path = validate_run(entry, root, platform, phase, campaign["expectedSources"])
            require(identity not in seen_identities and raw_path not in seen_paths, "One process/raw cannot masquerade as multiple independent runs")
            seen_identities.add(identity); seen_paths.add(raw_path); rows.append(row)
        except Exception as error:
            errors.append({"index": index, "entry": entry, "reason": str(error)})
    track_rows = {track: [] for track in FORMAL_TRACKS}
    first_values, calibration_values = [], []
    env_keys = {}
    for row in rows:
        for scene in row["scenes"]:
            key = json.dumps(row["environment"], sort_keys=True)
            group = scene["id"]
            require(group not in env_keys or env_keys[group] == key, "Environment/JIT/font differs within a track")
            env_keys[group] = key
            if row["kind"] == "interaction":
                track_rows[group].append({"processId": row["processId"], "processStartedUtc": row["processStartedUtc"], "raw": row["raw"], "receipt": row["receipt"], **scene})
            else:
                target = calibration_values if row["role"] == "calibration" else first_values
                target.append(scene["firstUse"]["firstUsableToControlledRasterMs"])
    if phase == "formal":
        for track, group in track_rows.items():
            if len(group) != 5:
                errors.append({"track": track, "reason": "Expected exactly five independent formal interaction processes", "actual": len(group)})
        if len(first_values) != 100 or len(calibration_values) != 2:
            errors.append({"reason": "Expected 100 fresh formal first-use processes and exactly two excluded calibrations", "formal": len(first_values), "calibration": len(calibration_values)})
    elif not rows:
        errors.append({"reason": "No valid development measurement processes"})
    output_tracks = {}
    for track, group in track_rows.items():
        if not group:
            continue
        pooled = [sample for r in group for sample in r["metricSamples"]]
        summaries = [{name: value for name, value in r.items() if name != "metricSamples"} for r in group]
        output_tracks[track] = {"independentProcesses": len(group), "processes": summaries,
                                "workloadSchedulesPassed": all(r["workloadSchedulesPassed"] for r in group),
                                "statisticsQualification": "Full fixed workload" if all(r["workloadSchedulesPassed"] for r in group) else "Observed completed samples from a partial/failed workload; not a full-workload pass.",
                                "observedPooledMetrics": {name: describe([v[name] for v in pooled]) for name in pooled[0]} if pooled else {},
                                "absoluteHitGateStatus": "passed" if all(r["absoluteHitGate"]["status"] == "passed" for r in group) else "failed",
                                "framePipelineStatus": "unmeasured", "presentStatus": "blocked-present",
                                "officialRatio": None, "referenceStatus": "blocked-reference"}
    workloads_passed = all(row["workloadSchedulesPassed"] for row in output_tracks.values())
    hit_gates_passed = all(row["absoluteHitGateStatus"] == "passed" for row in output_tracks.values())
    passed = not errors and workloads_passed and hit_gates_passed
    return {"passed": passed, "error": None if passed else "Incomplete/failed workload or contradictory evidence; per-track outcomes are retained below.",
            "schemaVersion": SCHEMA, "createdUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
            "oracleTool": {"path": str(Path(__file__).resolve()), "sha256": sha(__file__), "bytes": Path(__file__).stat().st_size},
            "platform": platform, "phase": phase, "protocol": {"path": str(protocol_path), "sha256": sha(protocol_path)},
            "sourceProof": source_proof,
            "runsExpected": len(entries), "runsValidated": len(rows), "failures": errors,
            "tracks": output_tracks, "firstUse": {"independentFormalProcesses": len(first_values) if phase == "formal" else 0,
                "observedNonCalibrationProcesses": len(first_values),
                "calibrationsExcluded": len(calibration_values), "controlledRasterMs": describe(first_values) if first_values else None,
                "nominalSoftwareDiagnosticBudgetMs": 3000, "nominalDiagnosticTargetMet": percentile(first_values, .95) <= 3000 if first_values else None,
                "formalFirstUsableToRenderGate": "unmeasured-not-the-controlled-raster-boundary",
                "millionPointHitTailStatus": "deferred-by-user"},
            "integrityPassed": not errors, "evidenceIntegrityPassed": not errors,
            "workloadSchedulesPassed": workloads_passed, "absoluteHitGatesPassed": hit_gates_passed,
            "formalSlaPassed": None, "officialReference": official,
            "present": present, "memoryBudgetStatus": "unmeasured-endpoints-are-not-matched-process-peaks",
            "milestoneComplete": False, "formalMeasurementProtocolCompleted": phase == "formal" and passed,
            "campaignEvidenceComplete": not errors}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--campaign", type=Path, required=True)
    parser.add_argument("--protocol", type=Path, default=Path(__file__).with_name("charts-c7-performance-protocol.json"))
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    require(not args.output.exists(), "Use a new immutable oracle result path")
    result = {"schemaVersion": SCHEMA, "passed": False, "error": None,
              "oracleTool": {"path": str(Path(__file__).resolve()), "sha256": sha(__file__), "bytes": Path(__file__).stat().st_size},
              "officialReference": {"status": "blocked-reference", "measurements": None, "ratios": None},
              "present": {"status": "blocked-present", "measurements": None}, "milestoneComplete": False}
    try:
        result.update(validate_campaign(args.campaign, args.protocol))
    except Exception as error:
        result["error"] = str(error)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2, allow_nan=False)
        stream.write("\n")
    print(json.dumps({"passed": result["passed"], "error": result["error"], "output": str(args.output)}))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
