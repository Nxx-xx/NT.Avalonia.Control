using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

/// <summary>Counts for a mapped source version, independent of viewport clipping. Missing values may have been filled by an empty-point policy.</summary>
public sealed record ChartAccessibilitySeriesSummary(ChartSeries Series, string Title, bool IsVisible,
    bool IsDataAvailable, int TotalCount, int ValidCount, int MissingCount, int InvalidCount, int SelectedCount);

/// <summary>The current displayed range of one active axis.</summary>
public sealed record ChartAccessibilityAxisSummary(string? Key, string? Title, string AxisType,
    bool IsHorizontal, double Minimum, double Maximum, bool IsInversed);

/// <summary>An immutable, bindable snapshot. Unavailable source counts are excluded from totals and explicitly identified.</summary>
public sealed record ChartAccessibilitySummary(string Title, bool IsLayoutPending, bool IsDataComplete,
    IReadOnlyList<ChartAccessibilitySeriesSummary> Series, IReadOnlyList<ChartAccessibilityAxisSummary> Axes,
    long TotalCount, long ValidCount, long MissingCount, long InvalidCount, int SelectedCount, string Text);

public sealed partial class CartesianChart
{
    public static readonly DirectProperty<CartesianChart, ChartAccessibilitySummary> AccessibilitySummaryProperty =
        AvaloniaProperty.RegisterDirect<CartesianChart, ChartAccessibilitySummary>(nameof(AccessibilitySummary), chart => chart.AccessibilitySummary);
    public static readonly DirectProperty<CartesianChart, string> AccessibilitySummaryTextProperty =
        AvaloniaProperty.RegisterDirect<CartesianChart, string>(nameof(AccessibilitySummaryText), chart => chart.AccessibilitySummaryText);

    private ChartAccessibilitySummary _accessibilitySummary = new("Cartesian chart", true, false,
        Array.Empty<ChartAccessibilitySeriesSummary>(), Array.Empty<ChartAccessibilityAxisSummary>(), 0, 0, 0, 0, 0, "Cartesian chart; layout=pending");
    private string _accessibilitySummaryText = "Cartesian chart; layout=pending";
    private bool _accessibilitySummaryRequested;
    private bool _updatingAccessibilitySummary;
    private readonly Dictionary<ChartSeries, AccessibilityCounts> _accessibilityCounts = new(ReferenceEqualityComparer.Instance);
    private sealed record AccessibilityCounts(long Version, long Publication, int Total, int Valid, int Missing, int Invalid);

    /// <summary>Reading or binding opts in to summary maintenance. It never enumerates ItemsSource or creates automation peers.</summary>
    public ChartAccessibilitySummary AccessibilitySummary
    {
        get { VerifyAccess(); _accessibilitySummaryRequested = true; UpdateAccessibilitySummary(); return _accessibilitySummary; }
    }
    public string AccessibilitySummaryText
    {
        get { VerifyAccess(); _accessibilitySummaryRequested = true; UpdateAccessibilitySummary(); return _accessibilitySummaryText; }
    }

    private void UpdateAccessibilitySummary()
    {
        if (!_accessibilitySummaryRequested || _updatingAccessibilitySummary) return;
        _updatingAccessibilitySummary = true;
        try
        {
            var active = AutomationSeries;
            foreach (var old in _accessibilityCounts.Keys.Where(item => !active.Contains(item)).ToArray()) _accessibilityCounts.Remove(old);
            var summaries = new List<ChartAccessibilitySeriesSummary>(active.Length);
            foreach (var series in active)
            {
                var snapshot = _scene?.Snapshots.FirstOrDefault(item => ReferenceEquals(item.Series, series));
                if (!_accessibilityCounts.TryGetValue(series, out var counts) || counts.Version != series.SourceVersion || counts.Publication != series.PublishedSnapshotRevision)
                {
                    _accessibilityCounts.Remove(series);
                    counts = null;
                    if (snapshot is not null)
                    {
                        // Attached series already own a canonical cached snapshot. Reading it
                        // avoids counting categorical/domain/animation overlays as source values.
                        var data = _observedSeries.Contains(series) ? series.CreateSnapshot() : snapshot.Data;
                        var valid = 0; var missing = 0; var invalid = 0;
                        for (var index = 0; index < data.Count; index++)
                        {
                            var value = data.GetValue(index);
                            if (value.WasEmpty) missing++;
                            if (!value.IsEmpty && IsValid(value.Point)) valid++; else invalid++;
                        }
                        _accessibilityCounts[series] = counts = new(series.SourceVersion, series.PublishedSnapshotRevision, data.Count, valid, missing, invalid);
                    }
                }
                summaries.Add(new(series, series.Title ?? $"Series {summaries.Count + 1}", series.EffectiveVisibility, counts is not null,
                    counts?.Total ?? 0, counts?.Valid ?? 0, counts?.Missing ?? 0, counts?.Invalid ?? 0,
                    _publishedSelection.Count(point => ReferenceEquals(point.Series, series))));
            }
            var axes = _scene is { } scene ? scene.Scales.Select(pair => new ChartAccessibilityAxisSummary(pair.Key.Key,
                pair.Key.Title, pair.Key.GetType().Name, ReferenceEquals(pair.Key, XAxis) || scene.ExtraXAxes.Contains(pair.Key),
                pair.Value.Minimum, pair.Value.Maximum, pair.Key.IsInversed)).ToArray() : [];
            var total = summaries.Sum(item => (long)item.TotalCount);
            var validTotal = summaries.Sum(item => (long)item.ValidCount);
            var missingTotal = summaries.Sum(item => (long)item.MissingCount);
            var invalidTotal = summaries.Sum(item => (long)item.InvalidCount);
            var title = Title ?? "Cartesian chart";
            var text = new StringBuilder(title).Append(CultureInfo.InvariantCulture,
                $"; series={active.Length}; visible={summaries.Count(item => item.IsVisible)}; rows={total}; valid={validTotal}; missing={missingTotal}; invalid={invalidTotal}");
            if (_scene is { } current)
                text.Append(CultureInfo.InvariantCulture, $"; x=[{current.Range.XMinimum:G6}, {current.Range.XMaximum:G6}]; y=[{current.Range.YMinimum:G6}, {current.Range.YMaximum:G6}]");
            else text.Append("; layout=pending");
            foreach (var item in summaries)
                text.Append(CultureInfo.InvariantCulture, $"; {item.Title}: {(item.IsDataAvailable ? $"total={item.TotalCount}, valid={item.ValidCount}, missing={item.MissingCount}, invalid={item.InvalidCount}" : "data=pending")}, {(item.IsVisible ? "visible" : "hidden")}, selected={item.SelectedCount}");
            foreach (var axis in axes)
                text.Append(CultureInfo.InvariantCulture, $"; axis {axis.Key ?? (axis.IsHorizontal ? "X" : "Y")}: [{axis.Minimum:G6}, {axis.Maximum:G6}]");
            text.Append(CultureInfo.InvariantCulture, $"; selected={_publishedSelection.Count}");
            if (CurrentPoint is { } currentPoint && AccessibilityTryResolvePoint(currentPoint, out var browsing))
                text.Append("; current: ").Append(AccessibilityPointName(browsing));
            var next = new ChartAccessibilitySummary(title, _scene is null, summaries.All(item => item.IsDataAvailable),
                Array.AsReadOnly(summaries.ToArray()), Array.AsReadOnly(axes), total, validTotal, missingTotal, invalidTotal, _publishedSelection.Count, text.ToString());
            if (_accessibilitySummary.Text == next.Text && _accessibilitySummary.IsLayoutPending == next.IsLayoutPending &&
                _accessibilitySummary.Series.SequenceEqual(next.Series) && _accessibilitySummary.Axes.SequenceEqual(next.Axes)) return;
            SetAndRaise(AccessibilitySummaryProperty, ref _accessibilitySummary, next);
            SetAndRaise(AccessibilitySummaryTextProperty, ref _accessibilitySummaryText, next.Text);
        }
        finally { _updatingAccessibilitySummary = false; }
    }

    internal void AccessibilityEnsureScene() { VerifyAccess(); _ = GetScene(); }
    internal int AccessibilityPointCount(ChartSeries series) => _scene?.Snapshots.FirstOrDefault(item => ReferenceEquals(item.Series, series))?.Data.Count ?? 0;
    internal bool AccessibilityHasScene => _scene is not null;
    internal IReadOnlyList<ChartPointHit> AccessibilitySelection => _publishedSelection;

    internal IReadOnlyList<ChartPointHit> AccessibilityPointPage(ChartSeries series, int start, int count)
    {
        VerifyAccess();
        var snapshot = _scene?.Snapshots.FirstOrDefault(item => ReferenceEquals(item.Series, series));
        if (snapshot is null || !series.EffectiveVisibility) return [];
        var points = new List<ChartPointHit>(count);
        var end = (int)Math.Min(snapshot.Data.Count, (long)start + count);
        for (var index = start; index < end; index++)
        {
            var value = snapshot.Data.GetValue(index);
            if (value.IsEmpty || !IsValid(value.Point)) continue;
            var position = SelectionPosition(snapshot, value.Point);
            if (!double.IsFinite(position.X) || !double.IsFinite(position.Y)) continue;
            var data = snapshot.Data.Points[index];
            points.Add(ChartPointHit.FromData(series, data, position));
        }
        return points;
    }

    internal bool AccessibilityTryGetPoint(ChartSeries series, int sourceIndex, out ChartPointHit hit)
    {
        VerifyAccess();
        var snapshot = _scene?.Snapshots.FirstOrDefault(item => ReferenceEquals(item.Series, series));
        if (snapshot is not null && series.EffectiveVisibility && snapshot.Data.TryGetPointWithoutIndex(sourceIndex, out var data) &&
            !data.IsEmpty && IsValid(data.Point))
        {
            var position = SelectionPosition(snapshot, data.Point);
            if (double.IsFinite(position.X) && double.IsFinite(position.Y))
            { hit = ChartPointHit.FromData(series, data, position); return true; }
        }
        hit = null!; return false;
    }

    internal bool AccessibilityTryResolvePoint(ChartPointHit point, out ChartPointHit resolved)
    {
        VerifyAccess();
        var snapshot = _scene?.Snapshots.FirstOrDefault(item => ReferenceEquals(item.Series, point.Series));
        if (snapshot is null || !point.Series.EffectiveVisibility) { resolved = null!; return false; }
        var valueEquality = point.Series.UsesValueIdentity;
        bool Match(ChartDataPoint item) => valueEquality ? Equals(item.StableId, point.StableId) : ReferenceEquals(item.StableId, point.StableId);
        var found = snapshot.Data.TryGetPointWithoutIndex(point.Index, out var data) && Match(data);
        if (!found) found = snapshot.Data.TryFindExistingIdentity(point.StableId, out data, valueEquality);
        if (!found)
        {
            var start = Math.Clamp(point.Index - 128, 0, Math.Max(0, snapshot.Data.Count - 256));
            var end = (int)Math.Min(snapshot.Data.Count, (long)start + 256);
            for (var ordinal = start; ordinal < end; ordinal++)
                if (Match(snapshot.Data.Points[ordinal])) { data = snapshot.Data.Points[ordinal]; found = true; break; }
        }
        if (!found || data.IsEmpty || !IsValid(data.Point)) { resolved = null!; return false; }
        var screen = SelectionPosition(snapshot, data.Point);
        if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y)) { resolved = null!; return false; }
        resolved = ChartPointHit.FromData(point.Series, data, screen);
        return true;
    }

    internal bool AccessibilitySamePoint(ChartPointHit left, ChartPointHit right) => SameIdentity(left, right);
    internal bool AccessibilityPointIsSelected(ChartPointHit point) => _publishedSelection.Any(item => SameIdentity(item, point));
    internal bool AccessibilitySelectPoint(ChartPointHit point, bool additive = false)
    {
        VerifyAccess();
        if (!IsEffectivelyEnabled || SelectionMode == ChartSelectionMode.None || !AccessibilityTryResolvePoint(point, out var current)) return false;
        if (additive && SelectionMode != ChartSelectionMode.Multiple && _publishedSelection.Any(item => !SameIdentity(item, current))) return false;
        if (additive && SelectionMode == ChartSelectionMode.Multiple && !AccessibilityPointIsSelected(current) && _publishedSelection.Count >= MaxSelectionCount) return false;
        return PublishSelection(additive ? _publishedSelection.Append(current) : new[] { current }, ChartSelectionChangeReason.Keyboard);
    }
    internal bool AccessibilityRemovePoint(ChartPointHit point)
    {
        VerifyAccess();
        if (!IsEffectivelyEnabled || SelectionMode == ChartSelectionMode.None || !AccessibilityTryResolvePoint(point, out var current)) return false;
        return PublishSelection(_publishedSelection.Where(item => !SameIdentity(item, current)).ToArray(), ChartSelectionChangeReason.Keyboard);
    }
    internal bool AccessibilityInvokePoint(ChartPointHit point)
    {
        VerifyAccess();
        if (!IsEffectivelyEnabled || !AccessibilityTryResolvePoint(point, out var current)) return false;
        var command = PointInvokedCommand;
        var scene = _scene;
        var revision = _inputTransformRevision;
        if (command?.CanExecute(current) != true || !ReferenceEquals(command, PointInvokedCommand) || revision != _inputTransformRevision || !ReferenceEquals(_scene, scene) ||
            !AccessibilityTryResolvePoint(current, out var after) || !SameIdentity(current, after)) return false;
        command.Execute(after);
        return true;
    }
    internal string AccessibilityPointName(ChartPointHit point)
    {
        VerifyAccess();
        var snapshot = _scene?.Snapshots.FirstOrDefault(item => ReferenceEquals(item.Series, point.Series));
        if (snapshot is null) return $"{point.Series.Title ?? "Series"}; source index={point.Index}; unavailable";
        var x = _scene!.Scales[snapshot.XAxis].FormatLabel(point.Point.X);
        var y = _scene.Scales[snapshot.YAxis].FormatLabel(point.Point.Y);
        return $"{point.Series.Title ?? "Series"}; X={x}; Y={y}{BandReadingSuffix(_scene, snapshot, point)}; source index={point.Index}; {(AccessibilityPointIsSelected(point) ? "selected" : "not selected")}";
    }
    internal Rect AccessibilityPointBounds(ChartPointHit point)
    {
        VerifyAccess();
        if (_scene is not { } scene || !scene.Plot.Contains(point.Position) || !IsEffectivelyVisible ||
            TopLevel.GetTopLevel(this) is not Visual root || this.TransformToVisual(root) is not { } transform) return default;
        var diameter = point.MarkerDiameter is null ? Math.Max(8, point.Series.MarkerSize) : Math.Max(8, DisplayedPointRadius(point.Series, point.MarkerDiameter) * 2);
        return new Rect(point.Position.X - diameter / 2, point.Position.Y - diameter / 2, diameter, diameter).Intersect(scene.Plot).TransformToAABB(transform);
    }
}
