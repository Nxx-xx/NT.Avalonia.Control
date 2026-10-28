using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal List<object> ProfessionalPlotTrackballDiagnostics { get; } = [];
    internal void VerifyProfessionalPlotTrackballBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("ProfessionalPlotTrackballExamples")!; var expanded = expander.IsExpanded; expander.IsExpanded = true; FlushBindings();
        var funnel = this.FindControl<FunnelChart>("UsagePlotTrackFunnel")!; var mosaic = this.FindControl<MosaicChart>("UsagePlotTrackMosaic")!; ChartBase[] charts = [funnel, mosaic];
        var rows = Model.ProfessionalOverlayRows; var firstValue = rows[0].Value; var caption = Model.ProfessionalPlotContent; var plotTemplate = Model.ProfessionalPlotTemplate; var trackTemplate = Model.ProfessionalTrackTemplate;
        var mode = Model.ProfessionalTrackMode; var display = Model.ProfessionalTrackDisplay; var limit = Model.ProfessionalTrackLimit; var heading = Model.ProfessionalOverlayHeading;
        var selection = Model.ProfessionalOverlaySelection; var contrast = Model.C7Contrast; var theme = RequestedThemeVariant;
        static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        void Check(string name, bool value) => checks["c7-professional-plot-trackball-compiled-" + name] = value;
        static Control[] Cards(ChartBase chart, string name) => chart.GetVisualDescendants().OfType<Control>().Where(control => control.Name == name).ToArray();
        static IReadOnlyList<ChartDataTrackballReading> Readings(ChartBase chart) => chart is EngineeringChartBase e ? e.TrackballReadings : ((AnalysisChartBase)chart).TrackballReadings;
        static ChartDataPlotAreaContext? Plot(ChartBase chart) => Cards(chart, "UsageProfessionalPlotBody").SingleOrDefault()?.DataContext as ChartDataPlotAreaContext;
        bool Original(IReadOnlyList<ChartDataPoint> members) => members.Count > 0 && members.All(member => member.SourceIndex >= 0 && member.SourceIndex < Model.ProfessionalOverlayRows.Count && ReferenceEquals(member.Item, Model.ProfessionalOverlayRows[member.SourceIndex]) && Equals(member.StableId, Model.ProfessionalOverlayRows[member.SourceIndex].Id));
        void ReadAll()
        {
            FlushBindings(); foreach (var chart in charts)
            {
                if (chart is EngineeringChartBase e) e.UpdateTrackball(e.GetMarks().First(mark => mark.Members.Any(member => member.SourceIndex == 0)).Position);
                else { var a = (AnalysisChartBase)chart; a.UpdateTrackball(a.GetMarks().First(mark => mark.Data.SourceIndex == 0).LabelPosition); }
            }
            FlushBindings();
        }
        try
        {
            ReadAll();
            Check("source-properties-are-real-bindings", ReferenceEquals(funnel.ItemsSource, rows) && ReferenceEquals(mosaic.ItemsSource, rows));
            Check("template-properties-are-real-bindings", plotTemplate is not null && trackTemplate is not null && ReferenceEquals(funnel.PlotAreaContentTemplate, plotTemplate) && ReferenceEquals(mosaic.PlotAreaContentTemplate, plotTemplate) && ReferenceEquals(funnel.TrackballContentTemplate, trackTemplate) && ReferenceEquals(mosaic.TrackballContentTemplate, trackTemplate));
            Check("content-and-mode-bound-both-roots", Equals(funnel.PlotAreaContent, caption) && Equals(mosaic.PlotAreaContent, caption) && funnel.TrackballMode == mode && mosaic.TrackballMode == mode && funnel.TrackballDisplayMode == display && mosaic.MaxTrackballReadings == limit);
            Check("both-roots-real-visual-controls", charts.All(chart => Cards(chart, "UsageProfessionalPlotBody").Length == 1 && Cards(chart, "UsageProfessionalTrackBody").Length == 1));
            Check("plot-original-business-VM", charts.All(chart => ReferenceEquals(Plot(chart)?.DataContext, Model) && Equals(Plot(chart)?.Content, caption)));
            Check("plot-roundtrip-uses-layout-fractions", charts.All(chart => Plot(chart) is { } plot && Distance(plot.UnprojectLayout(plot.ProjectLayout(.3, .7)), new Point(.3, .7)) < .000001));
            Check("family-specific-raw-marks", Plot(mosaic)?.EngineeringMarks.Count > 0 && Plot(mosaic)?.AnalysisMarks.Count == 0 && Plot(funnel)?.AnalysisMarks.Count > 0 && Plot(funnel)?.EngineeringMarks.Count == 0);
            Check("trackball-originals-and-business-VM", charts.All(chart => Readings(chart).Count > 0 && Readings(chart).All(reading => Original(reading.Members) && ReferenceEquals(reading.BusinessDataContext, Model))));
            var aggregate = Readings(mosaic).Single(reading => reading.Members.Select(member => member.SourceIndex).SequenceEqual(new[] { 0, 2 }));
            Check("aggregate-keeps-complete-members", aggregate.EngineeringHit is not null && aggregate.PointHit is null && ReferenceEquals(aggregate.Members, aggregate.EngineeringHit.Members) && aggregate.Value == 50);
            Check("analysis-keeps-original-point-hit", Readings(funnel).All(reading => reading.PointHit is not null && reading.EngineeringHit is null && reading.Members.Count == 1));
            Check("compiled-reading-members-and-identity", Cards(mosaic, "UsageProfessionalTrackBody").Any(card => card.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageProfessionalTrackCount" && text.Text?.StartsWith("切片读数：", StringComparison.Ordinal) == true) && card.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageProfessionalTrackId" && text.Text?.StartsWith("首项：overlay-", StringComparison.Ordinal) == true)));
            var controls = charts.SelectMany(chart => Cards(chart, "UsageProfessionalPlotBody").Concat(Cards(chart, "UsageProfessionalTrackBody"))).ToArray();
            Model.ProfessionalOverlayHeading = "Changed overlay business heading"; FlushBindings();
            Check("business-INPC-updates-live-controls", controls.All(control => control.GetVisualParent() is not null) && charts.All(chart => chart.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Name is "UsageProfessionalPlotHeading" or "UsageProfessionalTrackHeading").All(text => text.Text == Model.ProfessionalOverlayHeading)));
            Model.ProfessionalPlotContent = "Changed permanent content"; FlushBindings();
            Check("compiled-content-update", charts.All(chart => Equals(Plot(chart)?.Content, Model.ProfessionalPlotContent) && Cards(chart, "UsageProfessionalPlotBody").Single().GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageProfessionalPlotCaption" && text.Text == "Changed permanent content")));
            rows[0].Value += 1; ReadAll(); Check("source-INPC-updates-aggregate-value", Readings(mosaic).Single(reading => reading.Members.Select(member => member.SourceIndex).SequenceEqual(new[] { 0, 2 })).Value == 50 + rows[0].Value - firstValue);
            Model.ProfessionalTrackDisplay = ChartTrackballDisplayMode.FloatAllPoints; ReadAll();
            Check("compiled-float-mode-has-singleton-contexts", charts.All(chart => Cards(chart, "UsageProfessionalTrackBody").Length == Readings(chart).Count && Cards(chart, "UsageProfessionalTrackBody").All(card => card.DataContext is ChartDataTrackballContext { Readings.Count: 1 })));
            Model.ProfessionalTrackLimit = 1; ReadAll(); Check("compiled-reading-limit-bounds-cards", charts.All(chart => Readings(chart).Count == 1 && Cards(chart, "UsageProfessionalTrackBody").Length == 1) && funnel.MaxTrackballReadings == 1 && mosaic.MaxTrackballReadings == 1);
            Model.ProfessionalTrackLimit = 0; ReadAll(); Check("zero-limit-clears-visible-readings", charts.All(chart => Readings(chart).Count == 0 && Cards(chart, "UsageProfessionalTrackBody").Length == 0));
            Model.ProfessionalTrackLimit = limit; Model.ProfessionalTrackDisplay = display; Model.ProfessionalTrackMode = ChartTrackballMode.Horizontal; ReadAll(); Check("compiled-horizontal-mode-retains-identity", charts.All(chart => Readings(chart).Count > 0 && Readings(chart).All(reading => Original(reading.Members))));
            Model.ProfessionalTrackMode = mode; ReadAll();
            foreach (var value in new[] { ChartHighContrastMode.Light, ChartHighContrastMode.Dark, ChartHighContrastMode.None })
            {
                Model.C7Contrast = value; RequestedThemeVariant = value == ChartHighContrastMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light; ReadAll();
                Check(value + "-keeps-current-template-contexts", charts.All(chart => ReferenceEquals(Plot(chart)?.DataContext, Model) && Readings(chart).All(reading => Original(reading.Members))));
            }
            var center = Plot(mosaic)!.ProjectLayout(.5, .5); mosaic.Zoom = 1.25; mosaic.PanX = .05; ReadAll();
            Check("viewport-reprojects-layout-and-retains-originals", Distance(Plot(mosaic)!.ProjectLayout(.5, .5), center) > 1 && Readings(mosaic).All(reading => Original(reading.Members)));
            mosaic.SetCurrentValue(EngineeringChartBase.SelectedIndexProperty, 0); FlushBindings(); Check("selection-remains-original-two-way-binding", Model.ProfessionalOverlaySelection == 0 && mosaic.SelectedPoint?.Members.Any(member => ReferenceEquals(member.Item, rows[0])) == true);
            Model.ProfessionalTrackTemplate = null; ReadAll(); Check("bound-null-template-keeps-default-trackball", charts.All(chart => Readings(chart).Count > 0 && Cards(chart, "UsageProfessionalTrackBody").Length == 0));
            Model.ProfessionalTrackTemplate = trackTemplate; Model.ProfessionalTrackMode = ChartTrackballMode.None; FlushBindings(); Check("bound-off-mode-clears-transient-controls", charts.All(chart => Readings(chart).Count == 0 && Cards(chart, "UsageProfessionalTrackBody").Length == 0));
            Model.ProfessionalTrackMode = mode; ReadAll(); Model.ProfessionalPlotContent = null; FlushBindings(); Check("bound-null-content-removes-permanent-overlay", charts.All(chart => Cards(chart, "UsageProfessionalPlotBody").Length == 0 && Readings(chart).Count > 0));
            Model.ProfessionalPlotContent = caption; ReadAll(); Model.ProfessionalOverlayReplaceCommand.Execute(null); ReadAll();
            Check("replacement-releases-original-observers", rows.All(row => row.SubscriberCount == 0));
            Check("replacement-uses-new-original-members", charts.All(chart => Readings(chart).All(reading => Original(reading.Members) && reading.Members.All(member => !rows.Any(old => ReferenceEquals(member.Item, old))))));
            ProfessionalPlotTrackballDiagnostics.Add(new { roots = charts.Select(chart => chart.GetType().Name), aggregateMembers = new[] { 0, 2 }, realCompiledTemplates = true, coordinates = "family layout fractions, top-left 0,0 to bottom-right 1,1", originalSourcesRetained = true, exportPixelsValidated = false, nativePixelEvidence = "Demo professional plot/trackball smoke", physicalInputValidated = false });
        }
        finally
        {
            Model.ProfessionalOverlayRows = rows; rows[0].Value = firstValue; Model.ProfessionalOverlayHeading = heading; Model.ProfessionalPlotContent = caption; Model.ProfessionalPlotTemplate = plotTemplate;
            Model.ProfessionalTrackTemplate = trackTemplate; Model.ProfessionalTrackMode = mode; Model.ProfessionalTrackDisplay = display; Model.ProfessionalTrackLimit = limit;
            foreach (var chart in charts) { chart.ResetViewport(); if (chart is EngineeringChartBase e) e.ClearTrackball(); else ((AnalysisChartBase)chart).ClearTrackball(); }
            Model.ProfessionalOverlaySelection = selection; Model.C7Contrast = contrast; RequestedThemeVariant = theme; expander.IsExpanded = expanded; FlushBindings();
        }
    }
}
