# Native exact export stability

This isolated producer uses Charts, its optional theme and Avalonia Desktop. It
does not reference the full Demo or another product. Run from a frozen repository
root to record source hashes. Use a new output directory for every invocation.

```powershell
dotnet run --project benchmarks/Charts.ExportStability -c Release -- --export-stability --rounds 16 --force-gc --output artifacts/export-stability/native-gc-v1
```

`--rounds` defaults to 16 and accepts 1–128. `--points` defaults to 100000 and accepts
8191–1000000 for a separately recorded size investigation. This is a stability
stress run; operation durations under forced collections are diagnostic values.
The project retains the runtime's default tiered compilation configuration.

Every round has 17 attempts: three fresh-control exact PNG captures, followed by a
history sequence on another chart. The order starts with the previously failing
half-size transparent original-geometry capture; the history sequence includes
original screen rendering, cropped gap exports, 1×/2× exact and sampled controls,
half-size transparent peak/gap views, asynchronous PNG/JPEG and a pre-canceled
asynchronous PNG. Exact export requests always set `UseSampling=false`. There is
no implicit simplification, decimation or path chunking in the producer.

`protocol.json` is created before Avalonia startup. Each attempt appends a start
record to `events.jsonl` and flushes it to disk before entering rendering. Its
image, independent receipt and end record are created after completion. A fatal
native crash therefore leaves the last attempted scenario without pretending it
finished. `results.json` is written only if control returns to the managed driver.
The producer stops on the first failed assertion; it does not retry failures.

The deterministic fixture retains a single-sample peak, valley and five NaN gap
rows. Receipts include output pixel regions, dimensions, encoded/pixel hashes,
original source/selection identity and screen-plan preservation. PNG checks use
the existing blue-pixel thresholds: opaque extrema ≥4 pixels at alpha ≥250;
transparent half-size extrema ≥1 pixel at alpha ≥16 after unpremultiplication;
the cropped gap must have zero blue pixels and transparent canvas alpha is zero.
JPEG checks cover successful encoding, signature, dimensions and data identity;
PNG thresholds are not silently applied to lossy JPEG. Raw PNGs and region metadata
are retained for an independent pixel oracle.

`--force-gc` starts one owned background worker and stops/joins it in `finally`.
Its collection counters distinguish collections beginning during the managed
capture interval. They do not prove overlap with one exact native draw call or
establish the cause of an access violation. The driver inventories actual loaded
Skia/HarfBuzz native modules after a completed run.

A shown native window is required; loading Avalonia.Headless is rejected. Bitmap
readback does not qualify compositor present, GPU timing, physical input,
official runtime equivalence, the full Demo regression or an eight-hour soak.

The supervised CI/local gate builds with the committed dependency lock, preserves
the default JIT, runs 16 rounds with forced GC, then invokes the independent Python
PNG oracle even after a fatal native exit:

```powershell
./scripts/Test-ChartsExportStability.ps1 -NativeMode DesktopSynthetic -OutputDirectory artifacts/export-stability/gate-v1
```

Linux CI uses `-NativeMode XvfbSynthetic`; a configured Linux desktop can use
`NativeSynthetic`. The `export-stability` job in `.github/workflows/charts.yml`
uploads successful and failed evidence on Windows and Linux. Source/host manifests,
per-stage process receipts, starts without ends, raw images and the independent
oracle report are retained. Merely configuring this job does not assert that a
remote CI run has executed.

`scripts/charts_export_stability_oracle.py` decodes PNGs with the bounded standard
library decoder, derives regions from the fixture and viewport, and checks peak,
valley, transparency and gap pixels without trusting the producer's pixel counts.
Both real signal flanks of a cropped gap must be visible, so an empty image cannot
pass a zero-bridge test. Its mutation tests run without .NET and reject fabricated
ink flags, missing raw receipts, altered viewports, changed sampling modes, corrupt
CRC and redirected image identities. JPEG is independently checked for file hash,
frame header and dimensions; full JPEG visual quality remains outside this gate.
