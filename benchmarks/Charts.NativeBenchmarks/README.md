# Native chart window driver (C4-08)

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
