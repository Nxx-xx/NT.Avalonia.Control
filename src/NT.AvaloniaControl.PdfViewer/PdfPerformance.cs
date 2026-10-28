using System.Diagnostics;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>
/// Measurement stages fixed by the performance plan (PERF01). Every stage is a wall-clock
/// span on a monotonic clock; names separate concerns the plan forbids conflating:
/// <see cref="DecodeComplete"/> is pixels returned to managed code, <see cref="UiSubmit"/>
/// is the UI thread submitting the visual update, and <see cref="CompositorRendered"/> is a
/// compositor batch actually rendered. None of them may be recorded as a physical scan-out
/// present, and a <c>LoadDocumentAsync</c> return is never a first-screen measurement.
/// </summary>
public enum PdfPerformanceStage
{
    /// <summary>Reading document bytes from a stream or staging input to a temp file.</summary>
    InputRead,
    /// <summary>PDFium open and metadata parse (pages, labels, outline), excluding queue wait.</summary>
    OpenParse,
    /// <summary>Time a work item spent queued before the native scheduler started it.</summary>
    QueueWait,
    /// <summary>The synchronous native execution of one scheduler work item.</summary>
    NativeExec,
    /// <summary>A page raster returned to managed pixels (the decode completing).</summary>
    DecodeComplete,
    /// <summary>Copying decoded pixels into the UI-side bitmap.</summary>
    BitmapUpload,
    /// <summary>Page layout build or rebuild.</summary>
    LayoutBuild,
    /// <summary>Search work: a page scan or the overall search operation.</summary>
    Search,
    /// <summary>Cancellation observed: a queued item dropped or a running call aborted.</summary>
    CancelObserved,
    /// <summary>Document or viewer release work (close latency and native teardown).</summary>
    Release,
    /// <summary>The UI thread submitted the visual update (not yet rendered by the compositor).</summary>
    UiSubmit,
    /// <summary>A composition batch rendered. This is a compositor notification, not scan-out.</summary>
    CompositorRendered
}

/// <summary>One monotonic span: <c>EndTimestamp - StartTimestamp</c> in Stopwatch ticks.</summary>
public readonly record struct PdfPerformanceSpan(
    string CorrelationId,
    PdfPerformanceStage Stage,
    long StartTimestamp,
    long EndTimestamp);

/// <summary>A named point on the monotonic clock. Mark names are unique per session.</summary>
public readonly record struct PdfPerformanceMark(string Name, string CorrelationId, long Timestamp);

/// <summary>Aggregated duration statistics for one stage within a session.</summary>
public sealed record PdfPerformanceStageTotals(PdfPerformanceStage Stage, int Count, double TotalMs, double MaxMs);

/// <summary>
/// One resource-audit sample (PERF16): process memory, managed heap, caches and optional
/// platform counters captured together at a single instant. <see cref="ManagedHeapBytes"/> is
/// read without a forced collection so normal running pressure is never hidden.
/// Unavailable counters are null rather than zero.
/// </summary>
public sealed record PdfResourceSample(
    long Timestamp,
    long ManagedHeapBytes,
    long ManagedAllocatedBytes,
    long PrivateBytes,
    long WorkingSetBytes,
    long? HandleCount,
    long? StagedInputBytes,
    long? GpuBytes,
    PdfViewerCacheStatistics? Cache)
{
    /// <summary>
    /// Captures the process-level counters now. Callers pass the viewer's
    /// <see cref="PdfViewerCacheStatistics"/> and staged-input bytes when those are in scope;
    /// <paramref name="gpuBytes"/> stays null unless a platform-specific counter was obtained.
    /// </summary>
    public static PdfResourceSample Capture(PdfViewerCacheStatistics? cache = null, long? stagedInputBytes = null,
        long? gpuBytes = null)
    {
        long? handles = null;
        long privateBytes;
        long workingSet;
        using (var process = Process.GetCurrentProcess())
        {
            process.Refresh();
            privateBytes = process.PrivateMemorySize64;
            workingSet = process.WorkingSet64;
            try
            {
                handles = process.HandleCount;
            }
            catch (PlatformNotSupportedException)
            {
            }
        }
        return new PdfResourceSample(Stopwatch.GetTimestamp(), GC.GetTotalMemory(forceFullCollection: false),
            GC.GetTotalAllocatedBytes(), privateBytes, workingSet, handles, stagedInputBytes, gpuBytes, cache);
    }
}

/// <summary>
/// A process-wide measurement session (PERF01/PERF16). While a session is
/// <see cref="Current"/>, the viewer records stage spans and marks into it with monotonic
/// timestamps and correlation ids; when no session is active the instrumentation is inert.
/// Hosts and benchmarks start a session around a scenario, then derive metrics from the
/// recorded spans — for example first-screen times relative to the <c>loadStart</c> mark,
/// never relative to <c>LoadDocumentAsync</c> returning.
/// </summary>
public sealed class PdfPerformanceSession : IDisposable
{
    /// <summary>Marked at document load entry, before any input read or native open.</summary>
    public const string LoadStartMark = "loadStart";
    /// <summary>Marked when the load call returns. This is not a first-screen measurement.</summary>
    public const string LoadReturnMark = "loadReturn";
    /// <summary>First page raster decoded into managed pixels inside the viewer pipeline.</summary>
    public const string FirstPageDecodedMark = "firstPageDecoded";
    /// <summary>First page bitmap uploaded and its visual update submitted on the UI thread.</summary>
    public const string FirstPageSubmittedMark = "firstPageSubmitted";
    /// <summary>Recorded only by a present harness when a compositor batch rendered; never by the product.</summary>
    public const string FirstPagePresentedMark = "firstPagePresented";
    /// <summary>Marked when the viewer's close/dispose entry runs (UI-side close latency).</summary>
    public const string CloseRequestedMark = "closeRequested";

    private static PdfPerformanceSession? s_current;
    private static int s_correlationCounter;

    private readonly object _gate = new();
    private readonly List<PdfPerformanceSpan> _spans = [];
    private readonly List<PdfPerformanceMark> _marks = [];
    private readonly List<PdfResourceSample> _samples = [];
    private readonly HashSet<string> _markedNames = [];
    private bool _disposed;

    private PdfPerformanceSession(string label)
    {
        Label = label;
        SessionId = Guid.NewGuid().ToString("N");
        StartTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>The one active session for this process, or null while instrumentation is inert.</summary>
    public static PdfPerformanceSession? Current => Volatile.Read(ref s_current);

    /// <summary>Monotonic timestamp source shared by every recorded measurement.</summary>
    public static long Now => Stopwatch.GetTimestamp();

    /// <summary>Milliseconds between two monotonic timestamps from <see cref="Now"/>.</summary>
    public static double MillisecondsBetween(long startTimestamp, long endTimestamp) =>
        Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;

    /// <summary>Host-supplied label for the scenario or measurement window.</summary>
    public string Label { get; }

    /// <summary>Unique id of this session; correlation ids are scoped under it.</summary>
    public string SessionId { get; }

    /// <summary>Session start on the monotonic clock; span times are usually reported relative to it.</summary>
    public long StartTimestamp { get; }

    /// <summary>
    /// Starts a session and makes it <see cref="Current"/>. One session per process at a time;
    /// dispose it before beginning the next.
    /// </summary>
    public static PdfPerformanceSession Begin(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var session = new PdfPerformanceSession(label);
        lock (s_gate)
        {
            if (s_current is not null)
                throw new InvalidOperationException("A PdfPerformanceSession is already active; dispose it first.");
            Volatile.Write(ref s_current, session);
        }
        return session;
    }

    private static readonly object s_gate = new();

    /// <summary>
    /// Records a named point. Mark names are unique per session: a second <see cref="Mark"/>
    /// of the same name is ignored and returns false, so "first*" milestones land exactly once.
    /// </summary>
    public bool Mark(string name, string correlationId = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate)
        {
            ThrowIfDisposed();
            if (!_markedNames.Add(name)) return false;
            _marks.Add(new PdfPerformanceMark(name, correlationId, Stopwatch.GetTimestamp()));
            return true;
        }
    }

    /// <summary>Mints a correlation id such as "render-3" that groups related spans.</summary>
    public string NextCorrelationId(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return prefix + "-" + Interlocked.Increment(ref s_correlationCounter);
    }

    /// <summary>Records one span. Zero-length spans mark point observations such as cancellation.</summary>
    public void Record(string correlationId, PdfPerformanceStage stage, long startTimestamp, long endTimestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        if (endTimestamp < startTimestamp)
            throw new ArgumentOutOfRangeException(nameof(endTimestamp), "Span end must not precede its start.");
        lock (_gate)
        {
            ThrowIfDisposed();
            _spans.Add(new PdfPerformanceSpan(correlationId, stage, startTimestamp, endTimestamp));
        }
    }

    /// <summary>Captures a resource sample (PERF16) and retains it with the session.</summary>
    public PdfResourceSample SampleResources(PdfViewerCacheStatistics? cache = null, long? stagedInputBytes = null,
        long? gpuBytes = null)
    {
        var sample = PdfResourceSample.Capture(cache, stagedInputBytes, gpuBytes);
        lock (_gate)
        {
            ThrowIfDisposed();
            _samples.Add(sample);
        }
        return sample;
    }

    /// <summary>All recorded spans in submission order.</summary>
    public IReadOnlyList<PdfPerformanceSpan> Spans
    {
        get { lock (_gate) return _spans.ToArray(); }
    }

    /// <summary>All recorded marks in submission order.</summary>
    public IReadOnlyList<PdfPerformanceMark> Marks
    {
        get { lock (_gate) return _marks.ToArray(); }
    }

    /// <summary>All resource samples in submission order.</summary>
    public IReadOnlyList<PdfResourceSample> ResourceSamples
    {
        get { lock (_gate) return _samples.ToArray(); }
    }

    /// <summary>Per-stage count and duration aggregates over the recorded spans.</summary>
    public IReadOnlyList<PdfPerformanceStageTotals> Totals
    {
        get
        {
            lock (_gate)
            {
                var byStage = new Dictionary<PdfPerformanceStage, (int Count, double Total, double Max)>();
                foreach (var span in _spans)
                {
                    var ms = MillisecondsBetween(span.StartTimestamp, span.EndTimestamp);
                    byStage.TryGetValue(span.Stage, out var totals);
                    byStage[span.Stage] = (totals.Count + 1, totals.Total + ms, Math.Max(totals.Max, ms));
                }
                return byStage
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new PdfPerformanceStageTotals(pair.Key, pair.Value.Count, pair.Value.Total, pair.Value.Max))
                    .ToArray();
            }
        }
    }

    /// <summary>Ends the session and clears <see cref="Current"/>. Idempotent.</summary>
    public void Dispose()
    {
        lock (s_gate)
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
            }
            if (ReferenceEquals(s_current, this))
                Volatile.Write(ref s_current, null);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
