using Avalonia;
using Avalonia.Media;
using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private long _trackballConfigurationRevision;

    private bool TrackballPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        var property = change.Property;
        if (property == TrackballReadingsProperty || property == HoveredSegmentsProperty || property == PrimarySegmentProperty) return true;
        if (property == TrackballModeProperty || property == TrackballSnapModeProperty ||
            property == TrackballDisplayModeProperty || property == TrackballContentTemplateProperty ||
            property == TrackballFormatterProperty)
        { TrackballConfigurationChanged(property); return true; }
        if (property == ShowTrackballLineProperty || property == TrackballLineStrokeProperty || property == TrackballLineStrokeThicknessProperty ||
            property == ShowTrackballMarkersProperty || property == TrackballMarkerSizeProperty || property == TrackballMarkerStrokeProperty ||
            property == TrackballMarkerFillProperty || property == TrackballMarkerStrokeThicknessProperty)
        { RequestRender(); return true; }
        if (IsTrackballActive && (property == IsTooltipEnabledProperty || property == TooltipBackgroundProperty ||
            property == TooltipForegroundProperty || property == TooltipBorderBrushProperty))
        {
            ClearTooltipPresentation();
            SynchronizeTrackballPresentation();
            RequestRender();
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TrackballConfigurationChanged(AvaloniaProperty property)
    {
        var configurationRevision = unchecked(++_trackballConfigurationRevision);
        ResetTrackballPresentationFailure();
        if (property == TrackballModeProperty || property == TrackballSnapModeProperty)
        {
            var anchor = _tooltipAnchor;
            var hadReading = _hoveredPoint is not null;
            var scene = _scene;
            var clearedRevision = unchecked(_readingsRevision + 1);
            ClearHover();
            // Clearing controls/notifying bindings can publish a new hover, clear a
            // source, or change this configuration again. Never restore the old
            // pointer after one of those callbacks has taken ownership of state.
            if (hadReading && ReadingsEnabled && configurationRevision == _trackballConfigurationRevision &&
                clearedRevision == _readingsRevision && ReferenceEquals(scene, _scene)) UpdateReadings(anchor);
        }
        else
        {
            ClearTrackballPresentation();
            if (IsTrackballActive && configurationRevision == _trackballConfigurationRevision) SynchronizeTrackballPresentation();
        }
        RequestRender();
    }

    private IReadOnlyList<ChartTrackballReading> BuildActiveReadings(ChartScene scene, Point position)
    {
        var readings = new List<ChartTrackballReading>();
        if (!IsTrackballActive && TooltipMode == ChartTooltipMode.NearestSegment)
        {
            if (NearestSceneSegment(scene, position, 12) is { } segment) readings.Add(new(segment.Original, segment));
            return Array.AsReadOnly(readings.ToArray());
        }
        var horizontal = IsTrackballActive ? TrackballMode == ChartTrackballMode.Horizontal : TooltipMode == ChartTooltipMode.InterpolateY;
        var interpolate = !IsTrackballActive || TrackballSnapMode == ChartTrackballSnapMode.LinearInterpolation;
        foreach (var snapshot in scene.Snapshots)
        {
            if (interpolate && ChartLineGeometry.IsBuiltIn(snapshot.Series))
            {
                if (FindSegment(scene, snapshot, position, horizontal ? SegmentQuery.Y : SegmentQuery.X) is { } segment)
                    readings.Add(new(segment.Original, segment));
                else
                {
                    // An isolated sample is still a real value at its exact shared
                    // coordinate. Do not bridge a gap or extrapolate to another X/Y.
                    var point = horizontal ? NearestYCore(snapshot, position, scene.Scales[snapshot.YAxis].HasStandardTransform, true) : NearestXCore(snapshot, position, true);
                    if (point is not null)
                    {
                        var coordinate = horizontal ? point.Position.Y : point.Position.X;
                        var requested = horizontal ? position.Y : position.X;
                        if (Math.Abs(coordinate - requested) <= 1e-9 * Math.Max(1, Math.Abs(requested))) readings.Add(new(point));
                    }
                }
            }
            else
            {
                var point = horizontal ? NearestYCore(snapshot, position, scene.Scales[snapshot.YAxis].HasStandardTransform, true) :
                    NearestXCore(snapshot, position, true);
                if (point is not null) readings.Add(new(point));
            }
        }
        return Array.AsReadOnly(readings.ToArray());
    }

    private string FormatTrackballReading(ChartScene scene, ChartTrackballReading reading)
    {
        var snapshot = scene.Snapshots.First(item => ReferenceEquals(item.Series, reading.Series));
        var x = scene.Scales[snapshot.XAxis].FormatLabel(reading.Point.X, snapshot.XAxis);
        var y = scene.Scales[snapshot.YAxis].FormatLabel(reading.Point.Y, snapshot.YAxis);
        if (reading.Series is IChartRangeSeries && reading.BaseValue is { } lower)
            y = $"{scene.Scales[snapshot.YAxis].FormatLabel(lower, snapshot.YAxis)}…{y}";
        var suffix = reading.Original.Analysis is not null || reading.Series is IChartLineSeries { StackMode: not ChartStackMode.None } or IChartComparisonSeries or IChartPointSeries
            ? BandReadingSuffix(scene, snapshot, reading.Original, reading.BaseValue) : "";
        return $"{reading.Series.Title ?? "数据点"}: {x}, {y}{suffix}";
    }

    private void RenderTrackball(DrawingContext drawing, ChartScene scene)
    {
        if (TrackballReadings.Count == 0 || _panPointer is not null || _selectionPointer is not null) return;
        using (drawing.PushClip(scene.Plot))
        {
            if (ShowTrackballLine && TrackballLineStrokeThickness > 0)
            {
                var pen = new Pen(TrackballLineStroke ?? CrosshairBrush ?? AxisBrush ?? Brushes.SlateGray, TrackballLineStrokeThickness);
                if (TrackballMode == ChartTrackballMode.Vertical)
                    drawing.DrawLine(pen, new Point(_tooltipAnchor.X, scene.Plot.Top), new Point(_tooltipAnchor.X, scene.Plot.Bottom));
                else drawing.DrawLine(pen, new Point(scene.Plot.Left, _tooltipAnchor.Y), new Point(scene.Plot.Right, _tooltipAnchor.Y));
            }
            if (ShowTrackballMarkers && TrackballMarkerSize > 0)
            {
                var radius = TrackballMarkerSize / 2;
                foreach (var reading in TrackballReadings)
                {
                    var pen = new Pen(TrackballMarkerStroke ?? GetSeriesBrush(reading.Series), TrackballMarkerStrokeThickness);
                    drawing.DrawEllipse(TrackballMarkerFill, pen, reading.Position, radius, radius);
                }
            }
        }
        if (ShowCrosshairLabels) RenderAxisReadingLabels(drawing, scene, _readingPosition);
    }
}
