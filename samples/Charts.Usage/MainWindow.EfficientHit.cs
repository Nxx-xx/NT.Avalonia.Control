using Avalonia;
using Avalonia.Controls;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal List<object> EfficientHitBindingDiagnostics { get; } = [];
    internal void VerifyEfficientHitBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("EfficientHitExamples")!; expander.IsExpanded = true; FlushBindings();
        var points = this.FindControl<CartesianChart>("UsageEfficientHitPoints")!; var dense = this.FindControl<CartesianChart>("UsageEfficientHitDense")!;
        void Check(string name, bool value) => checks["efficient-hit-compiled-" + name] = value;
        Point Project(CartesianChart chart, double x, double y, double? ymin = null, double? ymax = null)
        {
            var range = chart.VisibleRange!.Value; var plot = chart.PlotArea;
            return new(plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (y - (ymin ?? range.YMinimum)) / ((ymax ?? range.YMaximum) - (ymin ?? range.YMinimum)) * plot.Height);
        }
        static bool Original(ChartPointHit? hit, ChartSeries series, int index, LevelOfDetailUsageRecord row) => hit is not null && ReferenceEquals(hit.Series, series) && hit.Index == index && ReferenceEquals(hit.Item, row) && Equals(hit.StableId, row.Id) && hit.Point == new ChartPoint(row.X, row.Y) && hit.RawPoint is null;
        var overlap = points.HitTestPoint(Project(points, 2, 2), .001);
        Check("overlap-later-series-and-physical-record", Original(overlap, points.Series[1], 1, Model.HitFrontRecords[1]));
        var sameX = points.HitTestPoint(Project(points, 5, 6.9), 10);
        Check("same-x-nearest-screen-distance", Original(sameX, points.Series[1], 3, Model.HitFrontRecords[3]));
        var right = points.HitTestPoint(Project(points, 8, 800, 0, 1000), .001);
        Check("independent-axis-original-y-and-item", Original(right, points.Series[2], 0, Model.HitRightRecords[0]));
        points.SetCurrentValue(CartesianChart.SelectedPointProperty, overlap); FlushBindings();
        Check("selection-two-way-original-record-and-summary", ReferenceEquals(Model.HitSelectedPoint?.Item, Model.HitFrontRecords[1]) && Model.HitSelectionText.Contains("front-1", StringComparison.Ordinal));
        var series = (LineSeries)dense.Series.Single(); var rows = Model.HitDenseRecords; var info = dense.GetSamplingInfo(series);
        Check("sampled-view-keeps-complete-bound-business-source", info.IsApplied && info.RenderedCount < rows.Length / 3 && ReferenceEquals(series.ItemsSource, rows) && series.IdPath == "Id");
        const int index = 2345; var hit = dense.HitTestPoint(Project(dense, rows[index].X, rows[index].Y), .001);
        Check("sampled-original-point-index-id-and-item", Original(hit, series, index, rows[index]));
        dense.SetCurrentValue(CartesianChart.SelectedPointProperty, hit); FlushBindings();
        Check("sampled-original-selection-two-way", ReferenceEquals(Model.HitDenseSelectedPoint?.Item, rows[index]) && Model.HitSelectionText.Contains(rows[index].Id, StringComparison.Ordinal));
        Model.HitDetailCommand.Execute(null); FlushBindings();
        Check("detail-command-invalidates-view-index-only", dense.Viewport == Model.HitViewport && Original(dense.HitTestPoint(Project(dense, rows[index].X, rows[index].Y), .001), series, index, rows[index]));
        var a = Project(dense, rows[index].X, rows[index].Y); var b = Project(dense, rows[index + 1].X, rows[index + 1].Y); var segment = dense.HitTestSegment(a + (b - a) * .25, .001);
        Check("segment-adjacent-original-endpoints", segment is not null && Original(segment.Start, series, index, rows[index]) && Original(segment.End, series, index + 1, rows[index + 1]) && Math.Abs(segment.Fraction - .25) < 1e-6);
        var selected = dense.SelectRectangle(new Rect(a.X - .001, a.Y - .001, .002, .002)); FlushBindings();
        Check("original-rectangle-selection-is-bound", selected.MatchedCount == 1 && ReferenceEquals(Model.HitDenseSelectedPoint?.Item, rows[index]));
        Model.HitReplaceCommand.Execute(null); FlushBindings(); var replacement = dense.HitTestPoint(a, .001);
        Check("same-coordinate-source-replacement-updates-item-and-id", Original(replacement, series, index, Model.HitDenseRecords[index]) && !ReferenceEquals(replacement!.Item, rows[index]));
        dense.SetCurrentValue(CartesianChart.ViewportProperty, new ChartViewport(2200, 2450, 40, 55)); FlushBindings();
        Check("chart-viewport-two-way", Model.HitViewport == dense.Viewport && Model.HitViewport is { XMinimum: 2200, XMaximum: 2450 });
        EfficientHitBindingDiagnostics.Add(new { info, overlapIndex = overlap?.Index, overlapId = overlap?.StableId, sameXIndex = sameX?.Index, sameXId = sameX?.StableId, rightIndex = right?.Index, rightId = right?.StableId, rawIndex = hit?.Index, rawId = hit?.StableId, replacementId = replacement?.StableId, physicalInputValidated = false, performanceMeasured = false });
        Model.HitOverviewCommand.Execute(null); points.ClearSelection(); dense.ClearSelection(); expander.IsExpanded = false; FlushBindings();
    }
}
