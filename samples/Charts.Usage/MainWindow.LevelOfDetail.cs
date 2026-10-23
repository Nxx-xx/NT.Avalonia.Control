using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal CartesianChart LevelOfDetailChart => this.FindControl<CartesianChart>("UsageLevelOfDetailChart")!;
    internal List<object> LevelOfDetailBindingDiagnostics { get; } = [];
    internal void VerifyLevelOfDetailBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("LevelOfDetailExamples")!; expander.IsExpanded = true; FlushBindings();
        var chart = LevelOfDetailChart; var series = (LineSeries)chart.Series.Single(); var rows = Model.LodRecords;
        void Check(string name, bool value) => checks["lod-compiled-" + name] = value;
        Point Project(double x, double y)
        {
            var plot = chart.PlotArea; var range = chart.VisibleRange!.Value;
            return new(plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
                plot.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
        }
        var overview = chart.GetSamplingInfo(series);
        Check("explicit-minmax-mode-width-and-business-source", series.SamplingMode == ChartSamplingMode.MinMax && series.SamplingPixelWidth == 1 && ReferenceEquals(series.ItemsSource, rows) && series.CategoryPath == "X" && series.ValuePath == "Y" && series.IdPath == "Id" && !series.ShowMarkers && series.MarkerSize == 0);
        Check("overview-has-reduced-plan-and-two-continuous-figures", overview.IsApplied && overview.SourceCount == rows.Length && overview.RenderedCount < overview.VisibleCount && overview.FigureCount == 2 && chart.PlotArea.Width > 100);
        const int rawIndex = 2345;
        var hit = chart.HitTestPoint(Project(rows[rawIndex].X, rows[rawIndex].Y), .1);
        Check("overview-hit-retains-original-item-id-and-value", hit is { Index: rawIndex } && ReferenceEquals(hit.Item, rows[rawIndex]) && Equals(hit.StableId, rows[rawIndex].Id) && hit.Point == new ChartPoint(rows[rawIndex].X, rows[rawIndex].Y));
        chart.SetCurrentValue(CartesianChart.SelectedPointProperty, hit); FlushBindings();
        Check("original-selection-two-way-and-business-summary", Model.LodSelectedPoint is { Index: rawIndex } selected && ReferenceEquals(selected.Item, rows[rawIndex]) && Model.LodSelectionText.Contains(rows[rawIndex].Id, StringComparison.Ordinal));
        Model.LodPixelWidth = 4; FlushBindings(); var coarse = chart.GetSamplingInfo(series);
        Check("model-pixel-width-recomputes-physical-bucket-budget", series.SamplingPixelWidth == 4 && coarse.PixelWidth == 4 && coarse.PixelBucketCount < overview.PixelBucketCount && coarse.RenderedCount <= overview.RenderedCount);
        this.FindControl<ComboBox>("UsageLodPixelWidth")!.SelectedItem = 2; FlushBindings();
        Check("pixel-width-selector-is-two-way", Model.LodPixelWidth == 2 && series.SamplingPixelWidth == 2);
        this.FindControl<CheckBox>("UsageLodEnabled")!.IsChecked = false; FlushBindings(); var disabled = chart.GetSamplingInfo(series);
        Check("user-disable-keeps-full-source-and-existing-identity", !Model.LodEnabled && series.SamplingMode == ChartSamplingMode.None && !disabled.IsApplied && series.CreateSnapshot().Points.Count == rows.Length && ReferenceEquals(chart.SelectedPoint?.Item, rows[rawIndex]));
        Model.LodEnabled = true; Model.LodPixelWidth = 1; Model.LodDetailCommand.Execute(null); FlushBindings();
        var detail = chart.GetSamplingInfo(series); var peak = chart.HitTestPoint(Project(3334, 95), .1);
        Check("detail-command-restores-raw-peak-record", chart.Viewport == Model.LodViewport && !detail.IsApplied && detail.VisibleCount < 100 && peak is { Index: 3334, Point.Y: 95 } && ReferenceEquals(peak.Item, rows[3334]) && Equals(peak.StableId, rows[3334].Id));
        var center = Project(3334, 95); var rectangle = chart.SelectRectangle(new Rect(center.X - .1, center.Y - .1, .2, .2)); FlushBindings();
        Check("detail-rectangle-selects-original-peak-through-binding", rectangle.MatchedCount == 1 && rectangle.SelectedCount == 1 && Model.LodSelectedPoint is { Index: 3334 } && ReferenceEquals(Model.LodSelectedPoint.Item, rows[3334]));
        Model.LodGapCommand.Execute(null); FlushBindings();
        Check("gap-command-keeps-five-missing-original-records", chart.Viewport == Model.LodViewport && Enumerable.Range(4998, 5).All(index => series.CreateSnapshot().Points[index].IsEmpty) && chart.HitTestSegment(Project(5000, 0), .01) is null);
        series.SetCurrentValue(LineSeries.InterpolationProperty, ChartLineInterpolation.Smooth); FlushBindings(); var unsupported = chart.GetSamplingInfo(series);
        Check("unsupported-smooth-path-explicitly-falls-back", !unsupported.IsApplied && !string.IsNullOrEmpty(unsupported.FallbackReason) && unsupported.RequestedMode == ChartSamplingMode.MinMax);
        series.SetCurrentValue(LineSeries.InterpolationProperty, ChartLineInterpolation.Linear); Model.LodOverviewCommand.Execute(null); FlushBindings();
        chart.SetCurrentValue(CartesianChart.ViewportProperty, new ChartViewport(1200, 2400, -110, 110)); FlushBindings();
        Check("chart-view-change-updates-bound-viewport", Model.LodViewport == chart.Viewport && Model.LodViewport is { XMinimum: 1200, XMaximum: 2400 });
        Check("export-default-is-original-detail-and-opt-in-is-explicit", !new ChartExportOptions().UseSampling && new ChartExportOptions { UseSampling = true }.UseSampling);
        var canceled = false;
        try { chart.GetSamplingInfo(series, new CancellationToken(true)); } catch (OperationCanceledException) { canceled = true; }
        Check("sampling-diagnostics-honor-pre-cancellation", canceled && series.CreateSnapshot().Points.Count == rows.Length);
        LevelOfDetailBindingDiagnostics.Add(new { overview, coarse, disabled, detail, unsupported, originalHit = hit is null ? null : new { hit.Index, hit.StableId, hit.Point }, physicalInputValidated = false, performanceMeasured = false });
        Model.LodOverviewCommand.Execute(null); chart.ClearSelection(); expander.IsExpanded = false; FlushBindings();
    }
}
