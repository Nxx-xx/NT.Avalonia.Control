# Matched process-memory diagnostic

This is an independent software-rendering diagnostic. It does not change the original C7 benchmark entry, protocol, oracle or budgets. The runner executes three fresh matched pairs in empty/full, full/empty, empty/full order:

```powershell
pwsh -NoProfile -File scripts/Invoke-ChartsMemoryProbe.ps1 -OutputDirectory artifacts/charts-memory/new-run
```

The runner builds this net10.0 project unless `-SkipBuild` is specified. `-HostAssembly` selects an already built host, `-PythonExecutable` selects Python, and `-FontDirectory` adds an explicit Linux font directory. Every run needs a new output directory. Build and verifier stages reuse the existing process supervisor; host and sampler are separately supervised while their handshake overlaps.

Both modes initialize the same visible 1200×650-DIP chart/window, one empty line series, fonts, software Skia lease and 96-DPI RenderTargetBitmap. The common axes are X=[-1,1000000] and Y=[-2,194]. Full mode links the original `DatasetFactory` S1M-L corpus, creates the original numeric object-row adapter, uses MinMax, validates every finite original point against those axes, then applies the source, prepares layout, records DrawingGroup, renders RTB and verifies an original-point hit. It never filters an out-of-range point to make the check pass. Empty mode skips source construction and performs the corresponding empty-chart operations.

The host writes `ready.json` before corpus generation. The external sampler validates process creation identity and takes a first sample before acknowledging readiness. Only then can the host generate the source. After `work-complete.json`, the host keeps its source, chart and bitmap alive until the sampler has taken its terminal samples and exited successfully, and the runner releases the host. Failed processes, timeouts, raw data, logs and receipts are retained without retry. The complete JSON and CLI contract is in [ChartsMemoryProbe.Contract.json](../../scripts/ChartsMemoryProbe.Contract.json).

Windows private commit/working set and Linux RSS/PSS/private-resident have distinct measurement kinds. Results describe sampled maxima, not exact instantaneous peaks. The original source arrays and rows remain included in the full process observations; the reported value-byte count is not a complete object/native memory estimate. Managed endpoints use `GetTotalMemory(false)` and last-GC information without forcing collection. Native/GPU bytes remain unavailable; subtracting managed estimates from private memory does not produce accurate native usage.

The unchanged 512 MiB value is only a separate nominal diagnostic comparison of same-kind matched sampled full-minus-empty maxima. Linux resident/PSS and Windows commit values are not interchangeable. Evidence integrity and the diagnostic target result are separate outcomes. This track provides no original C7 throughput/pixel result, present, physical-device, hardware-GPU or official-reference qualification, and it cannot change the original oracle's peakPrivate/native/GPU null fields.
