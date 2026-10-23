using Avalonia;
using Avalonia.Media;
using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

public enum ChartTooltipMode { NearestPoint, SharedX, SharedY, NearestSegment, InterpolateX, InterpolateY }

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<ChartTooltipMode> TooltipModeProperty = AvaloniaProperty.Register<CartesianChart, ChartTooltipMode>(nameof(TooltipMode), validate: Enum.IsDefined);
    public static readonly StyledProperty<bool> ShowCrosshairProperty = AvaloniaProperty.Register<CartesianChart, bool>(nameof(ShowCrosshair));
    public static readonly StyledProperty<ChartCrosshairDirection> CrosshairDirectionProperty = AvaloniaProperty.Register<CartesianChart, ChartCrosshairDirection>(nameof(CrosshairDirection), validate: Enum.IsDefined);
    public static readonly StyledProperty<bool> ShowCrosshairLabelsProperty = AvaloniaProperty.Register<CartesianChart, bool>(nameof(ShowCrosshairLabels));
    public static readonly DirectProperty<CartesianChart, IReadOnlyList<ChartAxisReading>> AxisReadingsProperty = AvaloniaProperty.RegisterDirect<CartesianChart, IReadOnlyList<ChartAxisReading>>(nameof(AxisReadings), chart => chart.AxisReadings);
    public static readonly StyledProperty<IBrush?> CrosshairBrushProperty = AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(CrosshairBrush));
    public static readonly StyledProperty<Func<IReadOnlyList<ChartPointHit>, string?>?> TooltipFormatterProperty = AvaloniaProperty.Register<CartesianChart, Func<IReadOnlyList<ChartPointHit>, string?>?>(nameof(TooltipFormatter));
    public static readonly DirectProperty<CartesianChart, IReadOnlyList<ChartPointHit>> HoveredPointsProperty = AvaloniaProperty.RegisterDirect<CartesianChart, IReadOnlyList<ChartPointHit>>(nameof(HoveredPoints), chart => chart.HoveredPoints);
    private IReadOnlyList<ChartPointHit> _hoveredPoints = Array.Empty<ChartPointHit>();
    private IReadOnlyList<ChartAxisReading> _axisReadings = Array.Empty<ChartAxisReading>();
    private long _readingsRevision;

    public ChartTooltipMode TooltipMode { get => GetValue(TooltipModeProperty); set => SetValue(TooltipModeProperty, value); }
    public bool ShowCrosshair { get => GetValue(ShowCrosshairProperty); set => SetValue(ShowCrosshairProperty, value); }
    public ChartCrosshairDirection CrosshairDirection { get => GetValue(CrosshairDirectionProperty); set => SetValue(CrosshairDirectionProperty, value); }
    public bool ShowCrosshairLabels { get => GetValue(ShowCrosshairLabelsProperty); set => SetValue(ShowCrosshairLabelsProperty, value); }
    public IReadOnlyList<ChartAxisReading> AxisReadings => _axisReadings ?? Array.Empty<ChartAxisReading>();
    public IBrush? CrosshairBrush { get => GetValue(CrosshairBrushProperty); set => SetValue(CrosshairBrushProperty, value); }
    /// <summary>Formats the entire tooltip. Null hides the default box; a content template still receives hits with empty text. Hits retain original item identity and values.</summary>
    public Func<IReadOnlyList<ChartPointHit>, string?>? TooltipFormatter { get => GetValue(TooltipFormatterProperty); set => SetValue(TooltipFormatterProperty, value); }
    public IReadOnlyList<ChartPointHit> HoveredPoints => _hoveredPoints;

    private void UpdateReadings(Point position)
    {
        IReadOnlyList<ChartPointHit> hits = Array.Empty<ChartPointHit>();
        var scene = GetScene();
        if (ReadingsEnabled && scene is not null && scene.Plot.Contains(position))
        {
            if (IsTrackballActive || TooltipMode >= ChartTooltipMode.NearestSegment)
            {
                var readings = BuildActiveReadings(scene, position);
                hits = Array.AsReadOnly(readings.Select(reading => reading.Original).ToArray());
                PublishReadings(hits, position, scene, readings);
                return;
            }
            if (TooltipMode == ChartTooltipMode.SharedX)
                hits = Array.AsReadOnly(scene.Snapshots.Select(snapshot => NearestX(snapshot, position)).OfType<ChartPointHit>().ToArray());
            else if (TooltipMode == ChartTooltipMode.SharedY)
                hits = Array.AsReadOnly(scene.Snapshots.Select(snapshot => NearestY(snapshot, position, scene.Scales[snapshot.YAxis].HasStandardTransform)).OfType<ChartPointHit>().ToArray());
            else if (HitTestPoint(position) is { } hit) hits = Array.AsReadOnly(new[] { hit });
        }
        PublishReadings(hits, position, scene);
    }

    private bool ReadingPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (TrackballPropertyChanged(change)) return true;
        if (change.Property == AxisReadingsProperty) return true;
        if (change.Property != TooltipModeProperty && change.Property != CrosshairDirectionProperty &&
            change.Property != ShowCrosshairLabelsProperty) return false;
        ReadingPropertyChangedCore(change.Property);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ReadingPropertyChangedCore(AvaloniaProperty property)
    {
        if (property == TooltipModeProperty)
        {
            if (_hoveredPoint is not null) UpdateReadings(_tooltipAnchor);
        }
        else if (_hoveredPoint is not null) SynchronizeAxisReadings();
        RequestRender();
    }

    private void ClearAxisReadings()
    {
        unchecked { _readingsRevision++; }
        if (_axisReadings is not { Count: > 0 }) return;
        SetAndRaise(AxisReadingsProperty, ref _axisReadings, Array.Empty<ChartAxisReading>());
    }

    private void ClearReadings()
    {
        var revision = unchecked(++_readingsRevision);
        if (_hoveredPoint is null && _hoveredPoints is not { Count: > 0 } && _axisReadings is not { Count: > 0 } &&
            _trackballReadings is not { Count: > 0 } && _hoveredSegments is not { Count: > 0 })
        { ClearTooltipPresentation(); ClearTrackballPresentation(); return; }
        ClearReadingsCore(revision);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ClearReadingsCore(long revision)
    {
        var hits = _hoveredPoints ?? Array.Empty<ChartPointHit>(); var primary = _hoveredPoint;
        var axes = _axisReadings ?? Array.Empty<ChartAxisReading>();
        var readings = TrackballReadings; var segments = HoveredSegments; var segment = _primarySegment;
        _hoveredPoints = Array.Empty<ChartPointHit>(); _hoveredPoint = null;
        _axisReadings = Array.Empty<ChartAxisReading>();
        _trackballReadings = Array.Empty<ChartTrackballReading>(); _hoveredSegments = Array.Empty<ChartSegmentHit>(); _primarySegment = null;
        ClearTooltipPresentation();
        ClearTrackballPresentation();
        if (!CanFinishClearing(revision)) return;
        if (axes.Count > 0) RaisePropertyChanged(AxisReadingsProperty, axes, _axisReadings);
        if (!CanFinishClearing(revision)) return;
        if (hits.Count > 0) RaisePropertyChanged(HoveredPointsProperty, hits, _hoveredPoints);
        if (!CanFinishClearing(revision)) return;
        if (primary is not null) RaisePropertyChanged(HoveredPointProperty, primary, (ChartPointHit?)null);
        if (!CanFinishClearing(revision)) return;
        if (readings.Count > 0) RaisePropertyChanged(TrackballReadingsProperty, readings, _trackballReadings);
        if (!CanFinishClearing(revision)) return;
        if (segments.Count > 0) RaisePropertyChanged(HoveredSegmentsProperty, segments, _hoveredSegments);
        if (!CanFinishClearing(revision)) return;
        if (segment is not null) RaisePropertyChanged(PrimarySegmentProperty, segment, (ChartSegmentHit?)null);
        if (CanFinishClearing(revision) && (hits.Count > 0 || primary is not null || axes.Count > 0)) RequestRender();
    }

    // A nested clear may have no values left to notify. Finish the original empty
    // notifications so bindings clear too, but never publish over a newer hover.
    private bool CanFinishClearing(long revision) => revision == _readingsRevision ||
        _hoveredPoint is null && _hoveredPoints is not { Count: > 0 } && _axisReadings is not { Count: > 0 } &&
        _trackballReadings is not { Count: > 0 } && _hoveredSegments is not { Count: > 0 };

    private bool ReadingStateIsCurrent(long revision, ChartScene scene)
    {
        if (revision != _readingsRevision || !ReferenceEquals(_scene, scene) || !ReadingsEnabled ||
            _panPointer is not null || _selectionPointer is not null) return false;
        foreach (var snapshot in scene.Snapshots)
            if (snapshot.Series.SourceVersion != snapshot.Data.Version) return false;
        return true;
    }

    private void PublishReadings(IReadOnlyList<ChartPointHit> hits, Point anchor, ChartScene? scene,
        IReadOnlyList<ChartTrackballReading>? readings = null)
    {
        if (scene is null || !ReadingsEnabled || hits.Count == 0 || !ReferenceEquals(_scene, scene) ||
            _panPointer is not null || _selectionPointer is not null)
        { ClearHover(); return; }
        var revision = unchecked(++_readingsRevision);
        readings ??= Array.AsReadOnly(hits.Select(hit => new ChartTrackballReading(hit)).ToArray());
        var primaryReading = readings.MinBy(reading => Square(reading.Position.X - anchor.X) + Square(reading.Position.Y - anchor.Y))!;
        var primary = primaryReading.Original;
        if (!ReadingStateIsCurrent(revision, scene)) return;
        var previousHits = _hoveredPoints; var previousPrimary = _hoveredPoint;
        var previousReadings = TrackballReadings; var previousSegments = HoveredSegments; var previousSegment = _primarySegment;
        _hoveredPoints = hits; _hoveredPoint = primary; _tooltipAnchor = anchor;
        _trackballReadings = readings;
        _hoveredSegments = Array.AsReadOnly(readings.Select(reading => reading.Segment).OfType<ChartSegmentHit>().ToArray());
        _primarySegment = primaryReading.Segment; _readingPosition = primaryReading.Position;
        if (!EqualityComparer<IReadOnlyList<ChartPointHit>>.Default.Equals(previousHits, hits))
            RaisePropertyChanged(HoveredPointsProperty, previousHits, hits);
        if (!ReadingStateIsCurrent(revision, scene)) return;
        if (!Equals(previousPrimary, primary)) RaisePropertyChanged(HoveredPointProperty, previousPrimary, primary);
        if (!ReadingStateIsCurrent(revision, scene)) return;
        if (!ReferenceEquals(previousReadings, readings)) RaisePropertyChanged(TrackballReadingsProperty, previousReadings, readings);
        if (!ReadingStateIsCurrent(revision, scene)) return;
        if (!ReferenceEquals(previousSegments, _hoveredSegments)) RaisePropertyChanged(HoveredSegmentsProperty, previousSegments, _hoveredSegments);
        if (!ReadingStateIsCurrent(revision, scene)) return;
        if (!Equals(previousSegment, _primarySegment)) RaisePropertyChanged(PrimarySegmentProperty, previousSegment, _primarySegment);
        if (!ReadingStateIsCurrent(revision, scene)) return;
        SynchronizeAxisReadings(scene);
        if (!ReadingStateIsCurrent(revision, scene)) return;
        SynchronizeTooltipPresentation();
        if (!ReadingStateIsCurrent(revision, scene)) return;
        SynchronizeTrackballPresentation();
        RequestRender();
    }

    private void SynchronizeAxisReadings(ChartScene? scene = null)
    {
        scene ??= _scene;
        if (scene is null || _hoveredPoint is null || !ReadingsEnabled ||
            _panPointer is not null || _selectionPointer is not null)
        { ClearAxisReadings(); return; }
        var revision = _readingsRevision;
        if (!ReadingStateIsCurrent(revision, scene)) return;
        var rows = new List<ChartAxisReading>(scene.Scales.Count);
        foreach (var pair in scene.Scales)
        {
            var horizontal = ReferenceEquals(pair.Key, XAxis) || scene.ExtraXAxes.Contains(pair.Key);
            var fraction = horizontal ? (_readingPosition.X - scene.Plot.Left) / scene.Plot.Width :
                (scene.Plot.Bottom - _readingPosition.Y) / scene.Plot.Height;
            var value = pair.Value.Denormalize(fraction);
            if (!double.IsFinite(value)) continue;
            rows.Add(new(pair.Key, horizontal, value, pair.Value.FormatLabel(value, pair.Key)));
        }
        if (!ReadingStateIsCurrent(revision, scene)) return;
        SetAndRaise(AxisReadingsProperty, ref _axisReadings, Array.AsReadOnly(rows.ToArray()));
    }

    private static ChartPointHit? NearestX(SeriesSnapshot snapshot, Point position) => NearestXCore(snapshot, position, false);

    private static ChartPointHit? NearestXCore(SeriesSnapshot snapshot, Point position, bool earliest)
    {
        var projection = snapshot.Context!.Projection;
        var raw = projection.Unproject(position);
        if (snapshot.XAxis.IsInScaleBreak(raw.X) || snapshot.YAxis.IsInScaleBreak(raw.Y)) return null;
        var data = snapshot.Data;
        var index = ChartDataIndex.For(data);
        if (index.Count == 0) return null;
        var bestOrdinal = -1; var bestX = double.PositiveInfinity; var bestY = double.PositiveInfinity; var bestScreen = default(Point);
        void Consider(int ordinal)
        {
            var value = data.GetValue(ordinal);
            if (!IsValid(value.Point)) return;
            var screen = SelectionPosition(snapshot, value.Point);
            if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y) || !projection.PlotArea.Contains(screen)) return;
            var x = Math.Abs(screen.X - position.X); var y = Math.Abs(screen.Y - position.Y);
            if (x > bestX || x == bestX && (y > bestY || y == bestY && (earliest ? bestOrdinal >= 0 && ordinal > bestOrdinal : ordinal < bestOrdinal))) return;
            bestOrdinal = ordinal; bestX = x; bestY = y; bestScreen = screen;
        }
        var context = snapshot.Context!;
        var offset = !HorizontalComparison(snapshot.Series) && snapshot.Series.Renderer.UsesCategorySlots && context.GroupCount > 0
            ? -context.GroupWidth / 2 + context.GroupWidth / context.GroupCount * (context.GroupIndex + .5) : 0;
        if (projection is IChartSeparableProjection { HasMonotonicX: true } && double.IsFinite(offset) &&
            projection.PlotArea.Left > -1_000_000 && projection.PlotArea.Right < 1_000_000)
        {
            var queryX = position.X - offset;
            var guard = 1e-9 * Math.Max(1, Math.Max(Math.Abs(projection.PlotArea.Left), Math.Abs(projection.PlotArea.Right)));
            var visible = index.ScreenRange(projection, projection.PlotArea.Left - offset - guard, projection.PlotArea.Right - offset + guard);
            var range = index.ScreenRange(projection, queryX, queryX);
            int Ordinal(int item) => index.Ordinal(visible.Reverse ? index.Count - 1 - item : item);
            double Distance(int item)
            {
                var screenX = SelectionPosition(snapshot, data.GetValue(Ordinal(item)).Point).X;
                return double.IsFinite(screenX) ? Math.Abs(screenX - position.X) : double.PositiveInfinity;
            }
            var right = Math.Clamp(range.Start, visible.Start, visible.End);
            var left = right - 1;
            if (projection is CartesianProjection { HasStableTransform: true } && !snapshot.Series.Renderer.UsesCategorySlots &&
                DenseReadingGroup(left, right, visible.Start, visible.End, Distance))
            {
                var hit = ChartVisibilityIndex.For(data).NearestReading(projection, position, nearestY: false, earliest);
                return hit is { } winner ? ChartPointHit.FromData(snapshot.Series, data.Points[winner.Ordinal], winner.Position) : null;
            }
            // Walk outwards until the nearest *visible* X group is exhausted. A clipped Y
            // at the nearest X must not hide a farther visible point. All equal X/distance
            // candidates participate in the original Y/physical-order tie rule.
            while (left >= visible.Start || right < visible.End)
            {
                var a = left >= visible.Start ? Distance(left) : double.PositiveInfinity;
                var b = right < visible.End ? Distance(right) : double.PositiveInfinity;
                if (Math.Min(a, b) > bestX) break;
                Consider(Ordinal(left >= visible.Start && (right >= visible.End || a <= b) ? left-- : right++));
            }
        }
        else for (var ordinal = 0; ordinal < data.Count; ordinal++) Consider(ordinal);
        if (bestOrdinal < 0) return null;
        var point = data.Points[bestOrdinal];
        return ChartPointHit.FromData(snapshot.Series, point, bestScreen);
    }

    private static ChartPointHit? NearestY(SeriesSnapshot snapshot, Point position, bool standardTransform) => NearestYCore(snapshot, position, standardTransform, false);

    private static ChartPointHit? NearestYCore(SeriesSnapshot snapshot, Point position, bool standardTransform, bool earliest)
    {
        var projection = snapshot.Context!.Projection;
        var raw = projection.Unproject(position);
        if (snapshot.XAxis.IsInScaleBreak(raw.X) || snapshot.YAxis.IsInScaleBreak(raw.Y)) return null;
        var data = snapshot.Data;
        var bestOrdinal = -1; var bestY = double.PositiveInfinity; var bestX = double.PositiveInfinity; var bestScreen = default(Point);
        void Consider(int ordinal)
        {
            var point = data.GetValue(ordinal).Point;
            if (!IsValid(point)) return;
            var screen = SelectionPosition(snapshot, point);
            if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y) || !projection.PlotArea.Contains(screen)) return;
            var y = Math.Abs(screen.Y - position.Y); var x = Math.Abs(screen.X - position.X);
            if (y > bestY || y == bestY && (x > bestX || x == bestX && (earliest ? bestOrdinal >= 0 && ordinal > bestOrdinal : ordinal < bestOrdinal))) return;
            bestOrdinal = ordinal; bestY = y; bestX = x; bestScreen = screen;
        }
        if (projection is CartesianProjection && standardTransform && !HorizontalComparison(snapshot.Series) &&
            projection.PlotArea.Top > -1_000_000 && projection.PlotArea.Bottom < 1_000_000)
        {
            var index = ChartReadingIndex.For(data);
            if (index.Count == 0) return null;
            var guard = 1e-9 * Math.Max(1, Math.Max(Math.Abs(projection.PlotArea.Top), Math.Abs(projection.PlotArea.Bottom)));
            var visible = index.ScreenRange(projection, projection.PlotArea.Top - guard, projection.PlotArea.Bottom + guard);
            var range = index.ScreenRange(projection, position.Y, position.Y);
            int Ordinal(int item) => index.Ordinal(visible.Reverse ? index.Count - 1 - item : item);
            double Distance(int item)
            {
                var y = SelectionPosition(snapshot, data.GetValue(Ordinal(item)).Point).Y;
                return double.IsFinite(y) ? Math.Abs(y - position.Y) : double.PositiveInfinity;
            }
            var right = Math.Clamp(range.Start, visible.Start, visible.End); var left = right - 1;
            if (projection is CartesianProjection { HasStableTransform: true } && !snapshot.Series.Renderer.UsesCategorySlots &&
                DenseReadingGroup(left, right, visible.Start, visible.End, Distance))
            {
                var hit = ChartVisibilityIndex.For(data).NearestReading(projection, position, nearestY: true, earliest);
                return hit is { } winner ? ChartPointHit.FromData(snapshot.Series, data.Points[winner.Ordinal], winner.Position) : null;
            }
            while (left >= visible.Start || right < visible.End)
            {
                var a = left >= visible.Start ? Distance(left) : double.PositiveInfinity;
                var b = right < visible.End ? Distance(right) : double.PositiveInfinity;
                if (Math.Min(a, b) > bestY) break;
                Consider(Ordinal(left >= visible.Start && (right >= visible.End || a <= b) ? left-- : right++));
            }
        }
        else for (var ordinal = 0; ordinal < data.Count; ordinal++) Consider(ordinal);
        if (bestOrdinal < 0) return null;
        var selected = data.Points[bestOrdinal];
        return ChartPointHit.FromData(snapshot.Series, selected, bestScreen);
    }

    private static bool DenseReadingGroup(int left, int right, int start, int end, Func<int, double> distance)
    {
        const int limit = 512;
        if (end - start <= limit) return false;
        var a = left >= start ? distance(left) : double.PositiveInfinity;
        var b = right < end ? distance(right) : double.PositiveInfinity;
        var closest = Math.Min(a, b);
        if (!double.IsFinite(closest)) return false;
        // Each side is monotonic in screen order. Equality 512 positions away
        // proves at least 513 equal primary-coordinate candidates; ordinary
        // small groups keep their existing binary/outward path.
        return a == closest && left - start >= limit && distance(left - limit) == a ||
            b == closest && end - 1 - right >= limit && distance(right + limit) == b;
    }

    private string[] TooltipLines(ChartScene scene)
    {
        if (TooltipFormatter is { } format) return (format(HoveredPoints) ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (HoveredSegments.Count > 0)
            return TrackballReadings.Select(reading => FormatTrackballReading(scene, reading)).ToArray();
        if (TooltipMode != ChartTooltipMode.NearestPoint)
            return HoveredPoints.Select(hit =>
            {
                var snapshot = scene.Snapshots.First(item => ReferenceEquals(item.Series, hit.Series));
                return $"{hit.Series.Title ?? "数据点"}: {scene.Scales[snapshot.XAxis].FormatLabel(hit.Point.X, snapshot.XAxis)}, {scene.Scales[snapshot.YAxis].FormatLabel(hit.Point.Y, snapshot.YAxis)}{BandReadingSuffix(scene, snapshot, hit)}";
            }).ToArray();
        var primary = HoveredPoint!;
        var current = scene.Snapshots.First(item => ReferenceEquals(item.Series, primary.Series));
        return [primary.Series.Title ?? "数据点",
            $"{current.XAxis.Title ?? "X"}: {scene.Scales[current.XAxis].FormatLabel(primary.Point.X, current.XAxis)}",
            $"{current.YAxis.Title ?? "Y"}: {scene.Scales[current.YAxis].FormatLabel(primary.Point.Y, current.YAxis)}{BandReadingSuffix(scene, current, primary)}"];
    }

    private void RenderReadings(DrawingContext context, ChartScene scene)
    {
        if (IsTrackballActive) { RenderTrackball(context, scene); return; }
        if (!IsTooltipEnabled || HoveredPoint is not { } hit || _panPointer is not null || _selectionPointer is not null) return;
        RenderReadingsCore(context, scene, hit);
    }

    private void RenderReadingsCore(DrawingContext context, ChartScene scene, ChartPointHit hit)
    {
        var position = HoveredSegments.Count > 0 ? _readingPosition : hit.Position;
        using (context.PushClip(scene.Plot))
        {
            if (ShowCrosshair)
            {
                var pen = new Pen(CrosshairBrush ?? AxisBrush ?? Brushes.SlateGray, 1, DashStyle.Dash);
                if (CrosshairDirection != ChartCrosshairDirection.Horizontal)
                    context.DrawLine(pen, new Point(position.X, scene.Plot.Top), new Point(position.X, scene.Plot.Bottom));
                if (CrosshairDirection != ChartCrosshairDirection.Vertical)
                    context.DrawLine(pen, new Point(scene.Plot.Left, position.Y), new Point(scene.Plot.Right, position.Y));
            }
            foreach (var point in TrackballReadings)
            {
                var radius = point.Original.MarkerDiameter is null || point.IsInterpolated ? 7 : Math.Max(7, DisplayedPointRadius(point.Series, point.Original.MarkerDiameter) + 3);
                context.DrawEllipse(null, new Pen(GetSeriesBrush(point.Series), 2), point.Position, radius, radius);
            }
        }
        if (ShowCrosshairLabels) RenderAxisReadingLabels(context, scene, position);
    }

    private void RenderAxisReadingLabels(DrawingContext context, ChartScene scene, Point position)
    {
        foreach (var reading in AxisReadings)
        {
            if (!reading.Axis.IsVisible || reading.IsHorizontal && CrosshairDirection == ChartCrosshairDirection.Horizontal ||
                !reading.IsHorizontal && CrosshairDirection == ChartCrosshairDirection.Vertical ||
                !scene.Layouts.TryGetValue(reading.Axis, out var layout)) continue;
            var text = new FormattedText(reading.Text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(reading.Axis.LabelFontFamily ?? FontFamily, FontStyle, FontWeight), reading.Axis.LabelFontSize ?? FontSize,
                reading.Axis.LabelBrush ?? TooltipForeground ?? Foreground ?? Brushes.Black);
            var width = Math.Min(text.Width + 8, Math.Max(1, Bounds.Width - 2));
            var height = Math.Min(text.Height + 8, Math.Max(1, Bounds.Height - 2));
            var near = reading.Axis.Position == ChartAxisPosition.Near;
            var x = reading.IsHorizontal ? position.X - width / 2 : near ? layout.Bounds.Right - width - 4 : layout.Bounds.Left + 4;
            var y = reading.IsHorizontal ? near ? layout.Bounds.Top + 4 : layout.Bounds.Bottom - height - 4 : position.Y - height / 2;
            x = Math.Clamp(x, 1, Math.Max(1, Bounds.Width - width - 1));
            y = Math.Clamp(y, 1, Math.Max(1, Bounds.Height - height - 1));
            var box = new Rect(x, y, width, height);
            context.DrawRectangle(TooltipBackground, new Pen(TooltipBorderBrush, 1), box, 2, 2);
            using var clip = context.PushClip(box);
            context.DrawText(text, new Point(x + 4, y + 4));
        }
    }
}
