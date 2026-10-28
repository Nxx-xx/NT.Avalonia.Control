# TreeDataGrid Windows native measurements

The Windows x64 client now defaults to `explicit-com`. It uses the SDK's
IUIAutomation interfaces with deterministic, scoped native reference ownership;
it does not create AutomationElement/SafeHandle wrappers or force a client GC.
`--automation-client-backend managed-legacy` (driver `-AutomationClientBackend`)
retains the previous managed client for attribution. Backend version 1 records
the selected implementation, and unlike backends must remain separate cohorts.
The product DLL is unchanged by this test-client lifetime fix.

Every explicit-client reply carries ownership version 1: acquisitions/releases
must advance together, temporary query references and finalizer releases must
be zero, and the three client-lifetime references/two callback objects must
remain alive until shutdown. Shutdown removes subscriptions and explicitly
releases the automation object and callback owners; all ownership counts must
drain. The independent guard rejects missing, unbalanced, non-monotonic or
finalized receipts. This supplements full live Value/Selection pattern reads,
names/identities, subscriptions and retirement checks; it does not replace them
with cache-only elements. The cache requests AutomationElementMode_Full.

Run `dotnet <client.dll> --self-test-ownership` to check ABI layout, double
disposal, failed HRESULT cleanup, exception unwinding and borrowed callback
arguments. The interface slots/IIDs follow Windows SDK 10.0.26100.0
UIAutomationClient.idl. See Microsoft's [IUIAutomation interfaces](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/).
This Windows x64 tool path is separate from product/platform support claims.

Optional controlled external Windows UI Automation workload:

```powershell
dotnet build benchmarks/TreeDataGrid.AutomationClient/TreeDataGrid.AutomationClient.csproj -c Release
./scripts/Test-TreeDataGridNativePerformance.ps1 -OutputDirectory artifacts/treedatagrid-native/new-uia-run -DurationSeconds 1800 -AutoHeightDuringSoak -RefreshSourceDuringSoak -BlockingGcReference -AutomationClient benchmarks/TreeDataGrid.AutomationClient/bin/Release/net10.0-windows/TreeDataGrid.AutomationClient.dll
```

The separate MTA .NET Windows Desktop client loads Windows UI Automation, without
Avalonia/product references. It subscribes to structure and selected property
events on each current fixture grid, then queries the grid at attachment, after every
correlated native frame, and after retirement. Queries and replies record distinct
process IDs, QPC intervals, full row/cell/header IDs, names, values, selection and
event counters. Each tree must match the current viewport; retired fixtures must
be absent. Every query runs outside the native mutation/render interval, while
providers and event subscriptions remain active during the following mutation.
This is serial programmatic UI work interleaved with an external client, not a
screen reader, physical input or an arbitrary concurrent client workload.

`environment.automationSubscriptionVersion=1` requires subscriptions to be bound
to the current GridId and removed at retirement, before the next fixture. Every
reply identifies that subscription. Raw validation requires property-event
delivery inside each measured soak cycle and each measured navigation/edit
fixture, so warmup or a previous fixture cannot qualify later event coverage.
The initial window-only subscriber is retained as failed diagnostic evidence.

For memory attribution, `--automation-workload query|events|both` (driver
`-AutomationWorkload`) records `automationWorkloadVersion=1` and the exact group.
`both` is the default and retains every viewport and measured property-event
check above. `query` performs all reads without subscriptions; every subscription
receipt and event count must be empty/zero. `events` subscribes and verifies the
initial tree and retired absence, but skips subsequent frame traversal. Its
empty-frame receipts are checked explicitly; this diagnostic group does not
certify frame values or property-event delivery on newly realized descendants.
It must receive structure events. None/query/events/both are separate cohorts.
Replies preserve the raw client managed-memory estimate and monotonic Gen2
collection count. A negative estimate remains unavailable, never a zero.

Attribute host memory using current managed estimates, the separately timestamped
GC live/committed snapshot, process private bytes, and retired-object categories.
Private bytes minus GC committed bytes is only an unclassified residual: it also
includes runtime/JIT/stacks, renderer and UIA resources, and is not a native-UIA
allocation measurement. Allocation ticks/CPU sampling are diagnostic evidence;
profiled timings do not qualify performance. The profiler's host suspension
environment variable is removed from the protocol child so its startup text
cannot corrupt the JSON channel.

`environment.automationQueryVersion=1` records mode `none` or
`external-windows-uia-v1` and the initial peer state of every fixture. States must
match; unlike modes must not be pooled into one performance cohort. Hashes use
ordinal ID order, signed little-endian Int32 UTF-8 byte lengths (-1 for null),
then the ID/name/value bytes and one selection byte (0/1/2 for false/true/null).
The raw validator recomputes hashes, identities, counts, query/timing correlation,
event counter ordering, retirement and client shutdown independently.

At workload end the connected-retention snapshot is observational, without a
forced host GC. Outside the measured interval both clients remove event handlers.
The explicit client releases its owned COM interfaces without forcing a client GC;
only `managed-legacy` finalizes abandoned UIA managed SafeHandles
(AutomationElement has no Dispose). The client then exits. Only then does the host
perform the normal 12-second native settle and
full-GC retirement check. The shutdown receipt discloses this client cleanup;
final zero retention is a claim at that explicit boundary, not while a client
still holds automation objects. Earlier failed evidence remains failed.

`--shutdown-automation-providers` (driver `-ShutdownAutomationProviders`) is a
separate, explicit soak qualification boundary. After workload end, client exit,
12-second settle and full GC, it preserves the entire pre-shutdown retention
snapshot, checks that execution is outside SendMessage, and calls the public
Windows `UiaDisconnectAllProviders` API in this benchmark process. A final GC then
checks retirement. HRESULT, process ID, timestamps, both GC boundaries and
unchanged tracked counts are independently validated with
`environment.automationShutdownVersion=1`. It does not run during cycles or reset
product factories/host reuse. A passing post-provider-shutdown result does not
certify zero retention with the native bridge still connected. See Microsoft's
[provider shutdown contract](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiadisconnectallproviders).

`-AutomationClient` accepts a prebuilt DLL and hashes its directory before/after;
it does not install dependencies or build the client implicitly. The Windows
Desktop runtime is a test-tool requirement, not a product dependency.

`environment.containerMetricsVersion=1` records cumulative `CreatedRows` and
`CreatedCells` from the active rows presenter. They are observed in the render
snapshot outside the UI mutation interval. Raw validation rejects non-integral,
below-live, or decreasing counters within a performance fixture. These counters
separate actual host creation from preparation/recycling and retained capacity.

Current protocol: `treedatagrid-native-render-v2`. Historical v1 evidence is kept
in its original directories. Render timing/scenarios are unchanged. The v2 soak
alternates its five scrolling observations between the custom subscribing factory
and the default factory, after the existing custom-factory draft/source/template
checks, so both lifetime paths are exercised. This is a new soak workload; its
total allocation must not be treated as a directly matched v1 product comparison.

This separate Release desktop host loads only TreeDataGrid, its optional theme,
Avalonia Desktop and Fluent. It opens a non-activating, non-minimizable 1000×650
DIP window centered on a monitor. The window must remain visible, on-screen,
normal and at the original viewport/scaling. It never moves a measured sample
off-screen or restores/retries an invalidated sample.

From the repository root:

```powershell
./scripts/Test-TreeDataGridNativePerformance.ps1 -OutputDirectory artifacts/treedatagrid-native/new-run
# Optional local dependency nupkg feed for an offline cached build:
./scripts/Test-TreeDataGridNativePerformance.ps1 -OutputDirectory artifacts/treedatagrid-native/offline-run -DependencyFeed D:/packages/dependencies
```

The driver locks/restores/builds and publishes an isolated application. It runs
performance and soak in **separate processes** and enforces external timeouts,
captures exit/output/process receipts, hashes scope inputs before and after,
and validates terminal reports against append-only raw JSONL events. The default
soak is 600 measured seconds; `-DurationSeconds` accepts longer runs. `-Pilot`
is a reduced diagnostic, explicitly labeled and never a full qualification.

Six performance scenarios each use five fresh fixtures, 30 warmup updates and
60 measured updates per fixture: observer baseline; 100000 rows × 100 fixed
columns; 10000 rows × 100 columns with wrapped auto height and controlled binding;
the same with A2 events and a subscribing factory; a 100001-node expanded wide
tree × 20 columns; and programmatic navigation/cancel/commit in 10000 rows ×
20 columns. The observer baseline paints the same one-pixel witness without
scrolling. All fixtures use two frozen columns and the real product theme.

Every request has a unique ID and records the requested scenario, repetition,
source version and mutation revision. Timing begins immediately before UI work,
records synchronous layout, waits for an actual witness `Control.Render`, then
awaits the public native `CompositionBatch.Processed` and `.Rendered` tasks.
The render callback checks model/column identities, visible TextBlock contents,
A2/factory lifetimes and viewport container bounds. Applied/completed JSONL I/O
runs after the timed render receipt; request I/O runs before timing.

P50/P95/P99 describe native batch-render completion and UI work with observer,
batch and dispatcher overhead. Serial observation throughput includes driver
bookkeeping. **None is physical screen FPS, scan-out, GPU execution time or
physical input latency.** Those fields remain null. Programmatic navigation/edit
has exactly one commit and a zero-write cancellation, not hardware input.
PNG captures use RenderTargetBitmap outside measured update intervals; they are
visual checks of the native window tree, not desktop screenshots.

The fresh soak process performs ten warmup cycles and one full GC before its
baseline. During the timed interval it cycles default/template/column/header/theme
states, keeps a draft across factory/template/theme changes, cancels without a
write, commits once, moves two selected rows atomically, replaces a live source,
and completes five two-axis scroll observations. All eight native observations
must render per cycle. Cycles are paced at two seconds or actual work duration,
whichever is longer. A2 lifetimes, factories and model subscriptions must clear.

Every approximately 15 seconds it records managed heap, private bytes, working
set, handles, CPU, cumulative allocation and GC counts. **No forced GC occurs
in the measured soak.** Dead weak references are pruned; the tracker retains only
weak keys/references and aggregate counts, avoiding an ever-growing strong graph.
It tracks both sources/all 2000 models per cycle, both factories, preparation
contexts, grids, template presenters, rows, cells and headers, deduplicated by
weak object identity. After the no-inline async exercise returns, empty native
renders, a 12-second transport/disposal settle and full GC must leave zero tracked
retired objects. This final collection is outside the measured interval.

`--refresh-source` (runner switch `-RefreshSourceDuringSoak`) additionally refreshes
the same source in the second scroll request of every soak cycle, for both default
and custom factories. The option is recorded, retired containers are tracked before
refresh, and each `source-refresh` event is independently matched to its request,
cycle/factory, UI interval and rendered source version. This stronger workload is
reported separately from older soak runs; performance mode remains unchanged.

`--blocking-gc-reference` (runner switch `-BlockingGcReference`) explicitly selects
the last FullBlocking GC as the required heap/generation reference. It does not
induce a GC. `gcSnapshotKind` records the choice; `lastAnyGc` still preserves the
unmodified Any snapshot, with independently recomputed consistency errors and
warnings. A background-GC Any snapshot with non-additive fragmentation remains
unqualified; it is not replaced by the blocking values. FullBlocking may be older
than the current background/ephemeral GC, so its index must be read with its sizes.
The original Any mode retains its strict checks. Current estimates, process
counters and final retired-object checks retain their own validity rules.

v2 records baseline, steady and final resource snapshots in append-only JSONL and
the terminal report. `rawManagedBytes` preserves GC.GetTotalMemory(false) verbatim.
A negative runtime result makes `managedBytes` null with an explicit unavailable
status and a validator warning; it is never clamped or replaced by another metric.
The required cross-check records one GC.GetGCMemoryInfo snapshot: GC index/kind,
heap size, fragmentation, derived live bytes, committed bytes and all five
generation sizes/fragmentation before/after GC. Sizes must be nonnegative,
fragmentation cannot exceed its heap, committed bytes cover the heap, and the
generation sums must match. Collection counts and timestamps bracket acquisition
and disclose a concurrent GC. Current estimates, last-GC live bytes and process
private bytes have different meanings and are not asserted equal.

The independent validator recomputes these checks, matches every terminal sample
to its raw event and verifies sample/counter ordering and optional-estimate status.
Missing or inconsistent cross-check data fails; an unavailable optional current
estimate is reported separately. A valid last-GC snapshot does not certify the
unavailable current estimate or explain the runtime anomaly.

Investigation guards are declared before execution: the median private-byte
increase between the first/last three steady checkpoints (excluding the first
two) must not exceed 256 MiB; handle increase must not exceed 128. A crossing
fails the run for investigation. They are broad leak guards, **not a business
memory/latency SLA** or multi-hour endurance certification. Interpret full traces,
not just a post-GC endpoint. Renderer/driver caches and measurement overhead are
included. Loaded graphics module names/versions do not identify the active GPU.

An incomplete, timed-out, failed or minimized run remains failed. Initial JSON,
raw requests and supervisor process receipts preserve incomplete work; there is
no automatic measured retry. `scripts/verify_treedatagrid_native.py` rejects pilots
unless allowed, missing/duplicate/mismatched completions, invalid timestamps,
missing scenarios/rounds and incomplete soak/retention. Actual execution evidence
is recorded separately in `docs/treedatagrid-native-performance-a6.md`.
