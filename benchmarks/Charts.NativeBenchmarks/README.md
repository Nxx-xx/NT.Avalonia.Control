# Native chart window driver (C4-08)

## C7 controlled software-raster performance campaign

The separate C7 entry point is enabled explicitly at build time. The default C4
build excludes `C7Performance*.cs`, so copying the historical C4 host onto an
older product does not acquire a dependency on the newer native-text API.

```powershell
dotnet build benchmarks/Charts.NativeBenchmarks -c Release -p:EnableC7Performance=true
dotnet benchmarks/Charts.NativeBenchmarks/bin/Release/net10.0/Charts.NativeBenchmarks.dll `
  --c7-performance --output <new-directory> --source-root <frozen-source-root> `
  --c7-renderer software --c7-warmup-seconds 10 --c7-measure-seconds 30
```

The default tracks are `S10-L-exact`, `S10-S-exact`, `S10-B-exact` and
`S100-L-minmax`. Optional `S100-L-exact`, `S1M-L-exact` and `S1M-L-minmax`
tracks are selected with `--c7-scenarios <comma-separated-track-ids>`. The
numeric corpus links the unchanged C0 `DatasetFactory.cs`; per-series XY64LE
files, sizes, hashes and the original generator manifest accompany every run.
The adapter supplies object rows with original integer IDs, numeric X/Y, and
unchanged source ordering. A second prepared source uses Y+0.25, with X/ID/order
unchanged. Replacing these two sources exercises actual value and source
revision changes. Generation, adaptation and corpus IO are outside measured
chart operations. The legacy generator's `downsampling=none-current-runtime`
is not the C7 rendering declaration: `runtimeQuality` and every operation's
public `samplingInfo` state the actual None/MinMax plan and source, visible and
rendered counts. Different quality tracks must never be pooled.

The C7 application calls the production early native-text API before Avalonia
setup and requests only Win32/X11 software rendering. Before any chart or RTB
capture, a native composition draw operation obtains a Skia API lease and
requires no GRContext. Its observed native surface and the actual native window
handle/backend/scaling are recorded. This confirms the selected software path;
it does not measure native composition latency or physical presentation.

Each track first attaches a new chart, assigns the prepared original sources,
prepares its layout, renders the entire chart to an RTB and verifies its first
original-row hit. `firstUse.ticks.start/end` covers source assignment through the
first verified hit and includes RTB construction. The optional fresh-process
first-use campaign uses one track per process:

```powershell
dotnet <built-host>/Charts.NativeBenchmarks.dll --c7-performance `
  --output <new-directory> --source-root <frozen-source-root> `
  --c7-renderer software --c7-first-use-only --c7-scenarios S1M-L-minmax
```

That mode performs no interaction warmup/trace or 1,000-query million-point
tail-latency campaign. A caller must supervise and independently associate the
required number of distinct process starts; one new chart in a warm process
does not establish that qualification.

Interaction schedules exactly 60 requested slots per second, including 600
warmup and 1,800 measured requests with the formal 10/30-second options. Every
tenth request replaces the source, the remaining requests follow deterministic
full-range and one-tenth-view pan trajectories. Raw scheduled, request, apply,
prepare, record, controlled-raster and hit ticks are retained. Slow work may
drain for at most 15 seconds after each phase's planned end; then the scene
fails and reports attempted/completed/failed/pending slots. It never silently
drops slow requests or reduces the requested count. Separate query tests retain
100 warmups and 1,000 measured queries, with source replacement and separately
timed preparation before every tenth query. Dense bars disclose a returned
original-row consistency contract, rather than claiming an arbitrary overlapped
probe must win; line/scatter probes additionally require exact original index.

`controlledRasterStart/End` measures only `RenderTargetBitmap.Render(chart)`.
Bitmap encoding, saving and hashing are outside it. A reusable bitmap is used
for interaction; first-use includes constructing its bitmap. `record` is a
separate DrawingGroup interval. Request/queue/drain timing also includes host
instrumentation and UI scheduling; it is not a frame-present metric. Endpoint
managed/private/working-set/CPU observations and OS lifetime peak working set
are explicitly not a measured peak-private/native/GPU budget. The inherited
C4 runtimeconfig still sets tiered compilation false and its actual declaration
and overrides are recorded; default application JIT behavior is a separate
track. Formal hardware frame/present budgets and licensed official relative
performance remain unevaluated by this producer.

Each raw request is also appended to `requests.jsonl`; immutable checkpoints
precede expensive work and follow phase completion. Failure, incomplete
schedules and terminal exceptions remain in the final report. Short duration
overrides are only development diagnostics; the independent C7 protocol must
reject them for formal qualification. `raw.json` is written once, output paths
must be new, and the existing C4 entry point and its four-scene policy are not
reinterpreted as this C7 campaign.

This executable uses `Avalonia.Desktop`, Fluent and Charts themes. It has no
Headless package reference. Every request requires a visible desktop `Window`,
a nonzero native platform handle and a client area of at least 800 × 400 DIP.
Windows uses Win32; Linux uses the available Avalonia X11 desktop backend with
WSLg or Xvfb. The display label is explicitly declared by the caller.

The controlled runtime protocol sets `System.Runtime.TieredCompilation=false` in
the generated runtimeconfig. The driver hashes that actual file, records the
declaration and `AppContext.TryGetSwitch` result, and requires the switch to be
present and false. `DOTNET_TieredCompilation` and `COMPlus_TieredCompilation`
must be unset or literal `0`; enabled/unknown overrides fail with their values
retained. This controls one runtime setting; it does not establish the cause of
earlier latency variation or replace JIT/GC/scheduling traces.

```powershell
dotnet restore benchmarks/Charts.NativeBenchmarks/Charts.NativeBenchmarks.csproj --locked-mode
dotnet build benchmarks/Charts.NativeBenchmarks/Charts.NativeBenchmarks.csproj -c Release --no-restore
dotnet benchmarks/Charts.NativeBenchmarks/bin/Release/net10.0/Charts.NativeBenchmarks.dll --source-root . --output artifacts/native-unique-run --mode DesktopSynthetic --warmups 2 --samples 12
```

Use `NativeSynthetic` for WSLg/X11 or `XvfbSynthetic` under `xvfb-run` on Linux.
The output directory must not exist, or may contain only supervisor files
`process.json`, `stdout.log` and `stderr.log`. The driver refuses old producer
evidence. Unknown/duplicate arguments, invalid counts and unknown/duplicate
scenarios fail before initializing Avalonia.

The four fixed scenes are `line-10k`, `scatter-10k`, `bar-10k` and
`multi-4x512`. `--scenarios` accepts a comma-separated subset. Each scene runs
two warmups and twelve measured requests by default. Each measured set includes
four viewport changes, four whole-source replacements and four synthetic routed
Ctrl+Home key events. Source rows use `Id=row-{i}`, `X=i`, and
`Y=20+(i*37%71)+revision*0.25`. No request is retried and no measured sample is
removed. Counts may be increased, but cannot fall below 2/12. A cooperative
120-second run deadline and five-second native animation callback timeout fail
the run; an external supervisor must also bound a hung native process.

`raw.initial.json` (`charts-native-window-v1`) is created before desktop startup;
immutable `snapshots/report-NNNNNN.json` files preserve every request/terminal
state, and terminal `raw.json` is created exactly once. A request record is
inserted before work starts, so externally killed work remains `started` in the
last snapshot instead of disappearing. This avoids replacing open JSON files
on Windows. None of these evidence files are overwritten or retried.
`events.jsonl` and `driver.log` flush every lifecycle event. Failure, canceled,
timeout and late outcomes remain in the raw records and return a nonzero exit.
An externally killed process can leave terminal `raw.json` absent and the last
snapshot incomplete; its supervisor process receipt is authoritative for that
failure. The guard rejects absent terminal raw output.

Each request stores correlated monotonic ticks for request, UI apply, layout,
native animation callback, capture start/completion, hit start/completion and
terminal state; source versions and requested scene/data revisions; allocations
for UI apply/layout, controlled raster readback and hit work; and known original
point identity, index, coordinates, projection and viewport. The animation
callback is a scheduling callback, not evidence that a frame was rendered.
Capture duration includes `RenderTargetBitmap.Render`, PNG encoding/saving,
BGRA pixel readback and hashing. It is not a presentation latency.

Every request saves its PNG and a control PNG reconstructed from the same
deterministic source and viewport in a fresh chart tree outside measured
intervals. Their decoded BGRA hashes must match exactly. Native output must
contain nonwhite pixels and the known blue series paint in every plot. For the
10,000-bar scene, minimum screen bar widths overlap: the exact hit oracle queries
a deterministic maximum-height bar separated from equal-height bars by 71 raw
rows, keeping original identity/index/coordinate assertions unambiguous.

Dataset JSON/SHA, separate native-host and product source manifests, loaded
managed assembly SHA/size paths, native module inventory where available,
OS/architecture/runtime, display environment, DPI/scaling and installed font
family names accompany the evidence. The CI wrapper provides the source checkout
and build/process manifests and checks source immutability. Use the identical
native host source on candidate and PR-base product checkouts for comparisons;
the host intentionally uses stable public chart APIs.

Native presentation/GPU times and physical input latency are **not measured**.
`nativePresentTicks` and `gpuTicks` remain null. UI allocation counters exclude
render-thread/process allocations. The canonical comparison is an independent
cold control tree with the same renderer; it is not an external chart reference.
PNG equality is required within one request/platform. Cross-platform raster
equality, physical display behavior, device/GPU equivalence and long soak memory
qualification require separate validation.

## C7-03 optimization fidelity

Build with `-p:EnableC7Performance=true` to enable the independent, untimed
`--c7-raster-fidelity --output <new-directory>` entry. It captures 168 images at
96/144 DPI across mapped line/scatter/bar data, equal X and coincident points,
NaN breaks, clipping, translucent and gradient paint, live paint changes, old
recordings, source replacement and viewport changes. Run exactly the same host
against byte-verified baseline and candidate libraries on the same OS, then
compare the saved PNG pixels exactly. These samples are separate from the C7
fixed-load protocol; they neither replace its guards nor enter its timing pool.

`--c7-circle-paint-experiment --output <new-directory>` is an untimed rejection
fixture for a proposed same-color fill/stroke merge. It compares 1,024 cases at
each of 96/120/144 DPI, including coincident points and fractional edges. Any
different pixel returns 1 and retains all evidence. The experiment found
differences, so the production renderer keeps the original ordered primitives.
All `C7*.cs` sources are excluded when the C7 build flag is disabled.
