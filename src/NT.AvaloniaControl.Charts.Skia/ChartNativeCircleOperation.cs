using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.SceneGraph;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace NT.AvaloniaControl.Charts.Skia;

// A frame owns paint snapshots; the immutable scene plan can outlive that frame.
// This optional assembly explicitly pins the SDK unstable-API contract.
internal sealed class ChartNativeCircleOperation : ICustomDrawOperation
{
    private const int MinimumExpandedPoints = 2048;
    private const int MaximumHistoryCapacity = 32768;
    private static readonly object HistoryTableGate = new();
    private static readonly ConditionalWeakTable<ChartSeries, SharedNativeHistory> Histories = new();
    private static long _expandedFrameCount;
    private static long _fastPaintFrameCount;
    private static long _translatedExpandedFrameCount;
    private static long _translatedFastPaintFrameCount;
    private static object? _firstRejectedDeviceTransform;
    private static object? _firstTranslatedOptimizedDeviceTransform;
    internal static long ExpandedFrameCount => Interlocked.Read(ref _expandedFrameCount);
    internal static long FastPaintFrameCount => Interlocked.Read(ref _fastPaintFrameCount);
    internal static long TranslatedExpandedFrameCount => Interlocked.Read(ref _translatedExpandedFrameCount);
    internal static long TranslatedFastPaintFrameCount => Interlocked.Read(ref _translatedFastPaintFrameCount);
    internal static object? FirstRejectedDeviceTransform => Volatile.Read(ref _firstRejectedDeviceTransform);
    internal static object? FirstTranslatedOptimizedDeviceTransform => Volatile.Read(ref _firstTranslatedOptimizedDeviceTransform);
    private CirclePlan? _plan;
    private IBrush? _fill;
    private IPen? _stroke;
    private readonly bool _simplePaint;
    private readonly bool _snapshotPaint;

    internal ChartNativeCircleOperation(Point[] centers, double radius, IBrush fill, Pen stroke,
        long estimatedWeight, bool snapshotPaint = true)
        : this(new CirclePlan(centers, radius, stroke, estimatedWeight), fill, stroke, snapshotPaint)
    {
    }

    private ChartNativeCircleOperation(CirclePlan plan, IBrush fill, Pen stroke, bool snapshotPaint)
    {
        _plan = plan; _snapshotPaint = snapshotPaint;
        _simplePaint = IsSimplePaint(fill) && IsSimplePaint(stroke.Brush);
        _fill = snapshotPaint ? fill.ToImmutable() : fill;
        _stroke = snapshotPaint ? stroke.ToImmutable() : stroke;
        Bounds = plan.Bounds;
    }

    internal static long EstimateManagedBytes(int pointCount) => ChartNativeCircleBackend.EstimateManagedBytes(pointCount);
    internal long EstimatedBytes => _plan?.EstimatedBytes ?? 0;
    internal long SharedHistoryEstimatedBytes => _plan?.SharedHistoryEstimatedBytes ?? 0;
    internal bool SharedHistoryActive => _plan?.History?.Active ?? true;
    internal int RealizedCount => _plan?.RealizedCount ?? 0;
    internal int SharedHistoryCapacity => _plan?.History?.Capacity ?? 0;
    internal int SharedHistoryCreatedCount => _plan?.History?.CreatedCount ?? 0;

    internal static ChartNativeCircleOperation CreateForSeries(ChartSeries series, Point[] centers,
        double radius, IBrush fill, Pen stroke)
    {
        SharedNativeHistory? history = null;
        var capacity = 0;
        lock (HistoryTableGate)
        {
            if (series.CanShareRenderingGeometryHistory)
            {
                history = Histories.GetValue(series, static _ => new SharedNativeHistory());
                capacity = history.Prepare(centers.Length);
            }
            else ClearHistoryUnderLock(series);
        }
        // Reserve the whole shared history, including entries outside this
        // viewport, before its plan is admitted to the owner's resource cache.
        var weight = history is null ? EstimateManagedBytes(centers.Length) :
            checked(1024L + capacity * 576L + centers.LongLength * IntPtr.Size);
        return new(new CirclePlan(centers, radius, stroke, weight, history, capacity * 576L), fill, stroke, snapshotPaint: true);
    }

    internal static void ClearHistory(ChartSeries series)
    {
        lock (HistoryTableGate) ClearHistoryUnderLock(series);
    }

    private static void ClearHistoryUnderLock(ChartSeries series)
    {
        if (!Histories.TryGetValue(series, out var history)) return;
        Histories.Remove(series);
        history.Clear();
    }

    // Always create a fresh frame operation. In particular a mutable business
    // brush must not turn the retained scene into a permanently frozen color.
    internal static bool CanOptimize(IBrush fill, Pen stroke) => IsSimplePaint(fill) && IsSimplePaint(stroke.Brush) && StrokeKey.Capture(stroke).CanExpand;

    internal ChartNativeCircleOperation WithPaint(IBrush fill, Pen stroke, bool snapshotPaint = true)
    {
        var plan = _plan ?? throw new ObjectDisposedException(nameof(ChartNativeCircleOperation));
        if (!plan.Key.Equals(StrokeKey.Capture(stroke))) plan = new CirclePlan(plan.Centers, plan.Radius, stroke, plan.EstimatedBytes);
        return new(plan, fill, stroke, snapshotPaint);
    }

    public Rect Bounds { get; }
    public bool HitTest(Point p) => false;
    public bool Equals(ICustomDrawOperation? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (!_snapshotPaint || other is not ChartNativeCircleOperation { _snapshotPaint: true } operation ||
            _plan is null || !ReferenceEquals(_plan, operation._plan) || Bounds != operation.Bounds)
            return false;
        return PaintEquals(_fill, operation._fill) && PenEquals(_stroke, operation._stroke);
    }

    private static bool PaintEquals(IBrush? left, IBrush? right) =>
        left is ISolidColorBrush a && right is ISolidColorBrush b && a.Color == b.Color &&
        a.Opacity == b.Opacity && a.Transform is null && b.Transform is null;

    private static bool PenEquals(IPen? left, IPen? right) => left is not null && right is not null &&
        left.Thickness == right.Thickness && left.LineCap == right.LineCap && left.LineJoin == right.LineJoin &&
        left.MiterLimit == right.MiterLimit && left.DashStyle is null && right.DashStyle is null &&
        PaintEquals(left.Brush, right.Brush);

    public void Render(ImmediateDrawingContext context)
    {
        var plan = _plan; var fill = _fill; var stroke = _stroke;
        if (plan is null || fill is null || stroke is null) return;
        var native = context.PlatformImpl;
        DeviceTransform transform = default;
        var expanded = plan.Centers.Length >= MinimumExpandedPoints && _simplePaint && plan.Key.CanExpand &&
            NativeSurfaceGuard.IsUnitSurface(context, plan.Factory, out transform);
        if (expanded && TryDrawFast(context, plan, fill, stroke)) return;
        var countedExpandedFrame = false;
        for (var index = 0; index < plan.Centers.Length; index++)
        {
            var center = plan.Centers[index];
            var rect = new Rect(center.X - plan.Radius, center.Y - plan.Radius, plan.Radius * 2, plan.Radius * 2);
            var pair = expanded ? plan.GetShape(index, rect) : null;
            if (pair is null) native.DrawEllipse(fill, stroke, rect);
            else
            {
                native.DrawGeometry(fill, null, pair.Fill);
                native.DrawGeometry(stroke.Brush, null, pair.Outline);
                if (!countedExpandedFrame)
                {
                    Interlocked.Increment(ref _expandedFrameCount);
                    CountTranslatedFrame(transform, fastPaint: false);
                    countedExpandedFrame = true;
                }
            }
        }
    }

    private static bool TryDrawFast(ImmediateDrawingContext context, CirclePlan plan, IBrush fill, IPen stroke)
    {
        ShapePair?[]? shapes = null;
        ISkiaSharpApiLease? lease = null;
        SKPaint? fillPaint = null, strokePaint = null;
        SKCanvas? canvas = null;
        DeviceTransform transform = default;
        var ready = false;
        try
        {
            if (!ChartSkiaPublicPaintBridge.TryGetRenderOptions(context.PlatformImpl, out var options) ||
                fill is not ISolidColorBrush solidFill || stroke.Brush is not ISolidColorBrush solidStroke) return false;
            // All geometry and public path getters complete before leasing or
            // drawing. A rejected frame can still replay its original commands.
            shapes = plan.GetFastShapes();
            if (shapes is null) return false;
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null || feature.GetType().Assembly != typeof(ISkiaSharpApiLeaseFeature).Assembly) return false;
            lease = feature.Lease();
            if (!NativeSurfaceGuard.IsUnitSurface(lease, out transform) || lease.CurrentOpacity != 1) return false;
            canvas = lease.SkCanvas;
            fillPaint = new SKPaint { Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.SrcOver,
                IsAntialias = options.EdgeMode != EdgeMode.Aliased, Color = ChartSkiaPublicPaintBridge.SolidColor(solidFill) };
            strokePaint = new SKPaint { Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.SrcOver,
                IsAntialias = options.EdgeMode != EdgeMode.Aliased, Color = ChartSkiaPublicPaintBridge.SolidColor(solidStroke) };
            ready = true;
        }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException) { return false; }
        finally
        {
            if (!ready) { strokePaint?.Dispose(); fillPaint?.Dispose(); lease?.Dispose(); }
        }
        // Never catch and replay a partially painted frame. Only setup above
        // may fail closed; native drawing errors here propagate normally.
        using (lease)
        using (fillPaint)
        using (strokePaint)
        {
            foreach (var pair in shapes!)
            {
                canvas!.DrawPath(pair!.FillPath!, fillPaint!);
                canvas.DrawPath(pair.OutlinePath!, strokePaint!);
            }
            Interlocked.Increment(ref _expandedFrameCount);
            Interlocked.Increment(ref _fastPaintFrameCount);
            CountTranslatedFrame(transform, fastPaint: true);
            return true;
        }
    }

    private static void CountTranslatedFrame(DeviceTransform transform, bool fastPaint)
    {
        if (transform.TranslateX == 0 && transform.TranslateY == 0) return;
        Interlocked.Increment(ref _translatedExpandedFrameCount);
        if (fastPaint) Interlocked.Increment(ref _translatedFastPaintFrameCount);
        if (Volatile.Read(ref _firstTranslatedOptimizedDeviceTransform) is null)
            Interlocked.CompareExchange(ref _firstTranslatedOptimizedDeviceTransform,
                new DeviceTransformDiagnostic(transform, true, false, "translatedPaintCompleted"), null);
    }

    private sealed record DeviceTransformDiagnostic(DeviceTransform Transform, bool HasSurface, bool HasGpu, string Reason);

    public void Dispose()
    {
        // Public geometry interfaces have no disposal contract. Other frames
        // can still reference the plan, so release only this frame's references.
        _plan = null; _fill = null; _stroke = null;
    }

    private static bool IsSimplePaint(IBrush? brush) => brush is not null && brush.Transform is null &&
        (brush.GetType() == typeof(SolidColorBrush) || brush.GetType() == typeof(ImmutableSolidColorBrush));

    private readonly record struct StrokeKey(double Thickness, PenLineCap Cap, PenLineJoin Join, double Miter, bool HasDash)
    {
        internal static StrokeKey Capture(Pen pen) => new(pen.Thickness, pen.LineCap, pen.LineJoin, pen.MiterLimit, pen.DashStyle is not null);
        internal bool CanExpand => double.IsFinite(Thickness) && Thickness > 0 && !HasDash &&
            Cap == PenLineCap.Flat && Join == PenLineJoin.Miter && Miter == 10;
    }

    // The two extra managed path references fit the existing 576-byte logical
    // per-pair allowance; native path allocations remain excluded by model 1.
    private sealed record ShapePair(IGeometryImpl Fill, IGeometryImpl Outline, SKPath? FillPath, SKPath? OutlinePath);

    // Exact original geometry coordinates are part of the identity. A translated
    // origin ring does not preserve the rasterizer's edge pixels.
    private readonly record struct ShapeKey(long X, long Y, long Width, long Height, long Thickness,
        PenLineCap Cap, PenLineJoin Join, long Miter, bool HasDash)
    {
        internal static ShapeKey Capture(Rect rect, StrokeKey stroke) => new(
            BitConverter.DoubleToInt64Bits(rect.X), BitConverter.DoubleToInt64Bits(rect.Y),
            BitConverter.DoubleToInt64Bits(rect.Width), BitConverter.DoubleToInt64Bits(rect.Height),
            BitConverter.DoubleToInt64Bits(stroke.Thickness), stroke.Cap, stroke.Join,
            BitConverter.DoubleToInt64Bits(stroke.Miter), stroke.HasDash);
    }

    private sealed class SharedNativeHistory
    {
        private readonly object _gate = new();
        private readonly Dictionary<ShapeKey, LinkedListNode<(ShapeKey Key, ShapePair Pair)>> _entries = new();
        private readonly LinkedList<(ShapeKey Key, ShapePair Pair)> _lru = new();
        private IPlatformRenderInterface? _factory;
        private int _capacity;
        private int _created;
        private bool _active = true;
        internal int Capacity => Volatile.Read(ref _capacity);
        internal int CreatedCount => Volatile.Read(ref _created);
        internal bool Active => Volatile.Read(ref _active);

        internal int Prepare(int pointCount)
        {
            lock (_gate)
            {
                var requested = (int)Math.Min(MaximumHistoryCapacity, (long)pointCount * 2);
                if (requested > _capacity) Volatile.Write(ref _capacity, requested);
                return _capacity;
            }
        }

        internal ShapePair? GetOrCreate(ShapeKey key, IPlatformRenderInterface factory, Rect rect, IPen pen)
        {
            lock (_gate)
            {
                if (!_active || _capacity == 0) return null;
                // A new platform factory must never receive another backend's
                // native geometry. Existing frame slots retain their own pair.
                if (!ReferenceEquals(_factory, factory))
                {
                    _entries.Clear(); _lru.Clear(); _factory = factory;
                }
                if (_entries.TryGetValue(key, out var existing))
                {
                    _lru.Remove(existing); _lru.AddLast(existing);
                    return existing.Value.Pair;
                }
                var pair = CreateShape(factory, rect, pen);
                if (pair is null) return null;
                Volatile.Write(ref _created, _created + 1);
                if (_entries.Count == _capacity && _lru.First is { } first)
                {
                    _entries.Remove(first.Value.Key); _lru.RemoveFirst();
                }
                var node = _lru.AddLast((key, pair));
                _entries.Add(key, node);
                return pair;
            }
        }

        internal void Clear()
        {
            lock (_gate)
            {
                Volatile.Write(ref _active, false);
                _entries.Clear(); _entries.TrimExcess(); _lru.Clear(); _factory = null;
                Volatile.Write(ref _capacity, 0);
            }
        }
    }

    private static ShapePair? CreateShape(IPlatformRenderInterface factory, Rect rect, IPen pen)
    {
        try
        {
            var fill = factory.CreateEllipseGeometry(rect);
            var outline = fill.GetWidenedGeometry(pen);
            return new(fill, outline, ChartSkiaPublicPaintBridge.TryGetFillPath(fill), ChartSkiaPublicPaintBridge.TryGetFillPath(outline));
        }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException)
        {
            // Nothing has been painted for this point; its original ellipse
            // can still be emitted once, in the original order.
            return null;
        }
    }

    private sealed class CirclePlan
    {
        private readonly object _gate = new();
        private readonly IPen _geometryPen;
        private ShapePair?[]? _shapes;
        private bool _fastShapesReady;
        private int _realized;
        internal Point[] Centers { get; }
        internal double Radius { get; }
        internal StrokeKey Key { get; }
        internal long EstimatedBytes { get; }
        internal long SharedHistoryEstimatedBytes { get; }
        internal IPlatformRenderInterface? Factory { get; }
        internal Rect Bounds { get; }
        internal SharedNativeHistory? History { get; }
        internal int RealizedCount => Volatile.Read(ref _realized);

        internal CirclePlan(Point[] centers, double radius, Pen stroke, long estimatedWeight,
            SharedNativeHistory? history = null, long sharedHistoryEstimatedBytes = 0)
        {
            ArgumentNullException.ThrowIfNull(centers);
            if (!double.IsFinite(radius) || radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
            if (estimatedWeight < EstimateManagedBytes(centers.Length)) throw new ArgumentOutOfRangeException(nameof(estimatedWeight));
            Centers = centers; Radius = radius; Key = StrokeKey.Capture(stroke);
            History = history;
            EstimatedBytes = estimatedWeight;
            SharedHistoryEstimatedBytes = sharedHistoryEstimatedBytes;
            _geometryPen = stroke.ToImmutable();
            Factory = AvaloniaLocator.Current.GetService<IPlatformRenderInterface>();
            var extent = radius + Math.Max(0, Key.Thickness) / 2 + 1;
            var bounds = default(Rect);
            var first = true;
            foreach (var center in centers)
            {
                if (!double.IsFinite(center.X) || !double.IsFinite(center.Y)) continue;
                var current = new Rect(center.X - extent, center.Y - extent, extent * 2, extent * 2);
                bounds = first ? current : bounds.Union(current); first = false;
            }
            Bounds = bounds;
        }

        internal ShapePair? GetShape(int ordinal, Rect rect)
        {
            if (Factory is null || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y) ||
                !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height)) return null;
            var existing = Volatile.Read(ref _shapes);
            if (existing is not null && Volatile.Read(ref existing[ordinal]) is { } ready) return ready;
            lock (_gate)
            {
                var shapes = _shapes;
                if (shapes is null) Volatile.Write(ref _shapes, shapes = new ShapePair?[Centers.Length]);
                if (shapes[ordinal] is { } cached) return cached;
                var result = History is null ? CreateShape(Factory, rect, _geometryPen) :
                    History.GetOrCreate(ShapeKey.Capture(rect, Key), Factory, rect, _geometryPen);
                if (result is null) return null;
                Volatile.Write(ref shapes[ordinal], result);
                Volatile.Write(ref _realized, _realized + 1);
                return result;
            }
        }

        internal ShapePair?[]? GetFastShapes()
        {
            if (Volatile.Read(ref _fastShapesReady)) return Volatile.Read(ref _shapes);
            for (var index = 0; index < Centers.Length; index++)
            {
                var center = Centers[index];
                var rect = new Rect(center.X - Radius, center.Y - Radius, Radius * 2, Radius * 2);
                if (GetShape(index, rect) is not { FillPath: not null, OutlinePath: not null }) return null;
            }
            Volatile.Write(ref _fastShapesReady, true);
            return Volatile.Read(ref _shapes);
        }
    }

    internal readonly record struct DeviceTransform(double ScaleX, double ScaleY, double SkewX, double SkewY,
        double TranslateX, double TranslateY, double Perspective0, double Perspective1, double Perspective2)
    {
        internal bool IsUnitSurface => ScaleX == 1 && ScaleY == 1 && SkewX == 0 && SkewY == 0 &&
            TranslateX == 0 && TranslateY == 0 && Perspective0 == 0 && Perspective1 == 0 && Perspective2 == 1;
        internal bool IsUnitRasterTransform => ScaleX == 1 && ScaleY == 1 && SkewX == 0 && SkewY == 0 &&
            double.IsFinite(TranslateX) && double.IsFinite(TranslateY) &&
            TranslateX == Math.Truncate(TranslateX) && TranslateY == Math.Truncate(TranslateY) &&
            Perspective0 == 0 && Perspective1 == 0 && Perspective2 == 1;
    }

    private static class NativeSurfaceGuard
    {
        internal static bool IsUnitSurface(ImmediateDrawingContext context, IPlatformRenderInterface? factory, out DeviceTransform transform)
        {
            transform = default;
            var assembly = typeof(ISkiaSharpApiLeaseFeature).Assembly;
            var native = context.PlatformImpl.GetType();
            if (factory is null || factory.GetType().Assembly != assembly || native.Assembly != assembly ||
                native.FullName != "Avalonia.Skia.DrawingContextImpl") return false;
            try
            {
                var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (feature is null || feature.GetType().Assembly != assembly) return false;
                using var lease = feature.Lease();
                return IsUnitSurface(lease, out transform);
            }
            catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException) { return false; }
        }

        internal static bool IsUnitSurface(ISkiaSharpApiLease lease, out DeviceTransform transform)
        {
            var m = lease.SkCanvas.TotalMatrix;
            transform = new(m.ScaleX, m.ScaleY, m.SkewX, m.SkewY, m.TransX, m.TransY, m.Persp0, m.Persp1, m.Persp2);
            var surface = lease.SkSurface is not null;
            var gpu = lease.GrContext is not null;
            var accepted = surface && !gpu && transform.IsUnitRasterTransform;
            if (!accepted && Volatile.Read(ref _firstRejectedDeviceTransform) is null)
                Interlocked.CompareExchange(ref _firstRejectedDeviceTransform,
                    new DeviceTransformDiagnostic(transform, surface, gpu, !surface ? "missingSurface" : gpu ? "gpuSurface" : "nonIntegerUnitTransform"), null);
            return accepted;
        }
    }
}
