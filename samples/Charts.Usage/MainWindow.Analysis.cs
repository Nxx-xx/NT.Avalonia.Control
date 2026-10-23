using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal FunnelChart AnalysisFunnel => this.FindControl<FunnelChart>("UsageAnalysisFunnel")!;
    internal HeatmapChart AnalysisHeatmap => this.FindControl<HeatmapChart>("UsageAnalysisHeatmap")!;
    internal CartesianChart AnalysisRiver => this.FindControl<CartesianChart>("UsageAnalysisRiver")!;
    internal List<object> AnalysisBindingDiagnostics { get; } = [];
    internal void VerifyAnalysisBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("AnalysisExamples")!; expander.IsExpanded = true; FlushBindings();
        var funnel = AnalysisFunnel; var pyramid = this.FindControl<PyramidChart>("UsageAnalysisPyramid")!; var heatmap = AnalysisHeatmap;
        var carts = new[] { this.FindControl<CartesianChart>("UsageAnalysisStretch")!, this.FindControl<CartesianChart>("UsageAnalysisClip")!, this.FindControl<CartesianChart>("UsageAnalysisRepeat")! };
        var river = AnalysisRiver; var pictorial = carts.Select(chart => (PictorialBarSeries)chart.Series[0]).ToArray();
        void Check(string name, bool value) => checks["analysis-compiled-" + name] = value;
        static bool Near(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) < 1e-8;
        Check("seven-charts-have-real-layout", new AnalysisChartBase[] { funnel, pyramid, heatmap }.All(chart => chart.PlotArea.Width > 100 && chart.Bounds.Height >= 230) && carts.Append(river).All(chart => chart.PlotArea.Width > 100 && chart.PlotArea.Height > 100));
        Check("stage-sources-paths-and-series-identity", ReferenceEquals(funnel.ItemsSource, Model.AnalysisRecords) && ReferenceEquals(pyramid.Series.ItemsSource, Model.AnalysisRecords) && funnel.CategoryPath == "Category" && funnel.ValuePath == "Value" && funnel.IdPath == "Id" && funnel.GetMarks().Count == 4 && pyramid.GetMarks().Count == 4);
        var original = funnel.GetMarks(); var second = original.Single(mark => mark.Data.SourceIndex == 1); var hit = funnel.HitTest(second.LabelPosition);
        Check("polygon-hit-retains-original-item-id-and-value", hit is { Index: 1, Point.Y: 85 } && ReferenceEquals(hit.Item, Model.AnalysisRecords[1]) && Equals(hit.StableId, "analysis-b") && ReferenceEquals(hit.Series, funnel.Series));
        Check("heatmap-retains-typed-keys-and-missing-cell-identity", heatmap.RowKeys.Count == 2 && heatmap.RowKeys[0] is int and 1 && heatmap.RowKeys[1] is string and "1" && heatmap.ColumnKeys.Count == 3 && heatmap.GetMarks().Count == 5 && heatmap.GetMarks().Single(mark => mark.Data.SourceIndex == 2).Data.IsEmpty && ReferenceEquals(heatmap.ItemsSource, Model.AnalysisCells));
        Check("heatmap-explicit-linear-color-domain", heatmap.IsColorDomainValid && heatmap.EffectiveMinimum == 0 && heatmap.EffectiveMaximum == 100 && heatmap.LowColor == Color.Parse("#DDEBFF") && heatmap.HighColor == Color.Parse("#2455B5"));
        Check("all-pictorial-modes-bind-business-source-and-shared-hole-geometry", pictorial.Select(series => series.RenderMode).SequenceEqual(Enum.GetValues<PictorialBarRenderMode>()) && pictorial.All(series => ReferenceEquals(series.ItemsSource, Model.AnalysisSymbols) && ReferenceEquals(series.Geometry, Model.AnalysisSymbolGeometry) && series.CreateSnapshot().Points[1].Point.Y == -55));
        Check("first-and-last-pictorial-symbol-widths-fit-the-real-plot", pictorial.Select((series, index) =>
        {
            var chart = carts[index]; var peer = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
            var first = peer.GetPointPeer(series, 0)?.CurrentPoint; var last = peer.GetPointPeer(series, Model.AnalysisSymbols.Count - 1)?.CurrentPoint;
            return chart.XAxis is CategoryAxis { PlotMode: CategoryPlotMode.BetweenTicks } && series.BarWidth is { } width && first is not null && last is not null && first.Position.X - width / 2 >= chart.PlotArea.Left && last.Position.X + width / 2 <= chart.PlotArea.Right;
        }).All(value => value));
        var areas = river.Series.Cast<AreaSeries>().ToArray(); var baseline = Model.AnalysisRiverBaseline;
        Check("river-composes-transparent-hidden-legend-spacer-and-two-stacked-areas", areas.Length == 3 && !areas[0].ShowInLegend && areas[0].Fill is ISolidColorBrush { Color.A: 0 } && areas.All(series => series.StackMode == ChartStackMode.Stacked && series.StackGroup == "usage-analysis-river"));
        Check("river-baseline-centers-every-original-slot-at-fifty", baseline.Count == 7 && baseline.Select((point, index) => Near(point.Y + (Model.AnalysisRiverFirst[index].Y + Model.AnalysisRiverSecond[index].Y) / 2, 50)).All(value => value));
        Model.AnalysisGap = 12; Model.AnalysisNeck = .5; Model.AnalysisLabels = false; Model.AnalysisSelection = false; FlushBindings();
        Check("viewmodel-gap-neck-label-and-selection-controls-update", funnel.SegmentGap == 12 && pyramid.SegmentGap == 12 && heatmap.CellGap == 12 && funnel.NeckWidth == .5 && new AnalysisChartBase[] { funnel, pyramid, heatmap }.All(chart => !chart.ShowLabels && !chart.ShowValues && !chart.IsSelectionEnabled) && carts.All(chart => chart.SelectionMode == ChartSelectionMode.None));
        this.FindControl<Slider>("UsageAnalysisGap")!.Value = 6; this.FindControl<Slider>("UsageAnalysisNeck")!.Value = .25; this.FindControl<CheckBox>("UsageAnalysisLabels")!.IsChecked = true; this.FindControl<CheckBox>("UsageAnalysisSelection")!.IsChecked = true; FlushBindings();
        Check("user-controls-have-real-two-way-bindings", Model.AnalysisGap == 6 && Model.AnalysisNeck == .25 && Model.AnalysisLabels && Model.AnalysisSelection && pictorial.All(series => series.ShowDataLabels));
        Model.AnalysisSelectedIndex = 1; FlushBindings(); Check("selected-index-binding-resolves-real-business-point", funnel.SelectedPoint is { Index: 1 } selected && ReferenceEquals(selected.Item, Model.AnalysisRecords[1]) && Equals(selected.StableId, "analysis-b"));
        funnel.SetCurrentValue(AnalysisChartBase.SelectedIndexProperty, 2); FlushBindings(); Check("target-selection-updates-viewmodel-and-business-summary", Model.AnalysisSelectedIndex == 2 && Model.AnalysisSelectionText.Contains("analysis-c", StringComparison.Ordinal));
        var version = funnel.Series.SourceVersion; Model.AnalysisRecords[1].Value = 105; FlushBindings(); var changed = funnel.GetMarks().Single(mark => mark.Data.SourceIndex == 1);
        Check("business-inpc-refreshes-both-stage-projections", funnel.Series.SourceVersion > version && changed.Data.Point.Y == 105 && pyramid.GetMarks().Single(mark => mark.Data.SourceIndex == 1).Data.Point.Y == 105 && ReferenceEquals(changed.Data.Item, Model.AnalysisRecords[1]));
        Model.AnalysisCells[1].Value = 95; FlushBindings(); Check("heatmap-inpc-recolors-real-cell", heatmap.GetMarks().Single(mark => mark.Data.SourceIndex == 1).Data.Point.Y == 95);
        Model.AnalysisSymbols[1].Value = -25; FlushBindings(); Check("pictorial-inpc-preserves-negative-raw-values", pictorial.All(series => series.CreateSnapshot().Points[1].Point.Y == -25 && ReferenceEquals(series.CreateSnapshot().Points[1].Item, Model.AnalysisSymbols[1])));
        var added = new AnalysisUsageRecord("analysis-added", "补充", 30); Model.AnalysisRecords.Add(added); FlushBindings(); Check("collection-add-creates-original-new-stage", funnel.GetMarks().Count == 5 && pyramid.GetMarks().Count == 5 && ReferenceEquals(funnel.GetMarks()[4].Data.Item, added) && added.SubscriberCount > 0);
        added.Value = 0; FlushBindings(); Check("zero-value-does-not-create-fictional-stage", funnel.GetMarks().Count == 4 && pyramid.GetMarks().Count == 4 && added.Value == 0);
        Model.AnalysisRecords.Remove(added); FlushBindings(); Check("removal-releases-item-observers", added.SubscriberCount == 0);
        var beforeBaseline = areas[0].CreateSnapshot().Points.Select(point => point.Point).ToArray(); Model.AnalysisRiverVariant = true; FlushBindings();
        Check("river-binding-recomputes-spacer-and-contributions", !beforeBaseline.SequenceEqual(areas[0].CreateSnapshot().Points.Select(point => point.Point)) && areas[1].CreateSnapshot().Points[0].Point.Y == 25 && Model.AnalysisRiverBaseline.Select((point, index) => Near(point.Y + (Model.AnalysisRiverFirst[index].Y + Model.AnalysisRiverSecond[index].Y) / 2, 50)).All(value => value));
        Check("attached-business-observers-exist", Model.AnalysisRecords.SubscriberCount > 0 && Model.AnalysisCells.SubscriberCount > 0 && Model.AnalysisSymbols.SubscriberCount > 0 && Model.AnalysisRecords.All(row => row.SubscriberCount > 0));
        var content = Content; Content = null; FlushBindings(); Check("detach-releases-all-new-source-observers", Model.AnalysisRecords.SubscriberCount == 0 && Model.AnalysisCells.SubscriberCount == 0 && Model.AnalysisSymbols.SubscriberCount == 0 && Model.AnalysisRecords.All(row => row.SubscriberCount == 0) && Model.AnalysisCells.All(row => row.SubscriberCount == 0) && Model.AnalysisSymbols.All(row => row.SubscriberCount == 0));
        Content = content; FlushBindings(); Check("reattach-restores-new-source-and-identity", Model.AnalysisRecords.SubscriberCount > 0 && Model.AnalysisCells.SubscriberCount > 0 && Model.AnalysisSymbols.SubscriberCount > 0 && ReferenceEquals(funnel.GetMarks()[1].Data.Item, Model.AnalysisRecords[1]));
        AnalysisBindingDiagnostics.Add(new { original = original.Select(mark => new { mark.Data.SourceIndex, mark.Data.StableId, value = mark.Data.Point.Y }), changed = new { changed.Data.SourceIndex, changed.Data.StableId, value = changed.Data.Point.Y }, typedKeys = heatmap.RowKeys.Select(key => new { type = key.GetType().FullName, value = key.ToString() }), river = new { center = 50, baseline }, nativePixelsValidated = false, physicalInputValidated = false, performanceMeasured = false });
        Model.AnalysisRecords[1].Value = 85; Model.AnalysisCells[1].Value = 55; Model.AnalysisSymbols[1].Value = -55; Model.AnalysisGap = 2; Model.AnalysisNeck = .2; Model.AnalysisLabels = true; Model.AnalysisSelection = true; Model.AnalysisRiverVariant = false; Model.AnalysisSelectedIndex = -1; expander.IsExpanded = false; FlushBindings();
    }
}
