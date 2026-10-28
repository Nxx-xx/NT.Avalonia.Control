# Native managed stack attribution

This external diagnostic tool uses pinned Microsoft TraceEvent 3.2.6 and its
DiagnosticsClient dependency. It starts the isolated native benchmark suspended,
enables runtime GC/allocation/loader/JIT/stack events and the managed sample
profiler, resumes it, and writes an EventPipe trace with rundown. No product or
benchmark-host instrumentation or administrator ETW session is required.

```powershell
./scripts/Trace-TreeDataGridNative.ps1 -HostDll PATH/application/TreeDataGrid.NativeBenchmarks.dll -OutputDirectory artifacts/tdg-profile/performance
./scripts/Trace-TreeDataGridNative.ps1 -HostDll PATH/application/TreeDataGrid.NativeBenchmarks.dll -OutputDirectory artifacts/tdg-profile/soak -Mode soak
```

The soak diagnostic defaults to a 60-second pilot. It is not endurance evidence.
`-NuGetConfig` supports an existing offline feed configuration; the tool has a
separate locked dependency graph and is never a product dependency. The driver
builds an isolated collector copy, preserves source/binary/trace hashes, supervises
its child, validates native raw records and rejects empty or dropped-event traces.

Analysis identifies the UI thread from sampled NativeRunner/product call stacks.
It maps trace wall-clock timestamps into synchronous UI intervals using the
completed JSONL UTC/monotonic anchors and the host Stopwatch frequency. Boundary
samples are approximate; other UI work and background threads are separate.
Each event belongs to its nearest actual product method (generic framework
methods containing a product type are not product-owned methods), then harness,
then framework/native. Allocation types and example stacks support inspection.

Allocation ticks estimate bytes since the preceding tick; their sampled type
and stack do not count exact allocated objects. Inclusive method totals overlap.
Profiler samples are counts, not exact CPU milliseconds; background wait/native
stacks can appear. Native symbols are not downloaded or resolved. `missingStacks`
counts events without any stack, not the number of unresolved native frames.

**Profiling perturbs timings and GC.** Use these results to locate causes, then
compare separate unprofiled executions of the unchanged native render protocol.
Do not use diagnostic timings or a short diagnostic soak for release acceptance.
