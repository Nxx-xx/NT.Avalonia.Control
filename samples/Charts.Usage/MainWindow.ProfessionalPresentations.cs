using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal List<object> ProfessionalPresentationDiagnostics { get; } = [];
    internal void VerifyProfessionalPresentationBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("ProfessionalPresentationExamples")!;
        var expanded = expander.IsExpanded; expander.IsExpanded = true; FlushBindings();
        var funnel = this.FindControl<FunnelChart>("UsageSharedFunnel")!;
        var mosaic = this.FindControl<MosaicChart>("UsageSharedMosaic")!;
        ChartBase[] charts = [funnel, mosaic];
        var rows = Model.ProfessionalRows; var firstValue = rows[0].Value;
        var legend = Model.ProfessionalLegend; var label = Model.ProfessionalLabel;
        var legendLimit = Model.ProfessionalLegendLimit; var labelLimit = Model.ProfessionalLabelLimit;
        var heading = Model.ProfessionalHeading; var selection = Model.ProfessionalSelection;
        var contrast = Model.C7Contrast; var theme = RequestedThemeVariant;
        void Check(string name, bool value) => checks["c7-shared-presentation-compiled-" + name] = value;
        static Control[] Cards(ChartBase chart, string name) => chart.GetVisualDescendants().OfType<Control>().Where(control => control.Name == name).ToArray();
        static IReadOnlyList<ChartDataLegendItemContext> Legends(ChartBase chart) => chart is EngineeringChartBase e ? e.GetLegendItems() : ((AnalysisChartBase)chart).GetLegendItems();
        static IReadOnlyList<ChartDataLabelItemContext> Labels(ChartBase chart) => chart is EngineeringChartBase e ? e.GetDataLabels() : ((AnalysisChartBase)chart).GetDataLabels();
        bool Original(IReadOnlyList<ChartDataPoint> members) => members.Count > 0 && members.All(member => member.SourceIndex >= 0 && member.SourceIndex < Model.ProfessionalRows.Count && ReferenceEquals(member.Item, Model.ProfessionalRows[member.SourceIndex]) && Equals(member.StableId, Model.ProfessionalRows[member.SourceIndex].Id));
        void Refresh() { foreach (var chart in charts) { _ = Legends(chart); _ = Labels(chart); } FlushBindings(); }
        try
        {
            Refresh();
            Check("template-properties-are-real-bindings", legend is not null && label is not null && ReferenceEquals(funnel.LegendItemTemplate, legend) && ReferenceEquals(mosaic.LegendItemTemplate, legend) && ReferenceEquals(funnel.DataLabelTemplate, label) && ReferenceEquals(mosaic.DataLabelTemplate, label));
            Check("source-properties-are-real-bindings", ReferenceEquals(funnel.ItemsSource, rows) && ReferenceEquals(mosaic.ItemsSource, rows));
            Check("both-roots-real-visual-controls", charts.All(chart => Cards(chart, "UsageSharedLegendBody").Length > 0 && Cards(chart, "UsageSharedLabelBody").Length > 0));
            Check("legend-originals-and-business-context", charts.All(chart => Legends(chart).All(context => Original(context.Members) && ReferenceEquals(context.BusinessDataContext, Model))));
            Check("labels-originals-and-business-context", charts.All(chart => Labels(chart).All(context => Original(context.Members) && ReferenceEquals(context.BusinessDataContext, Model))));
            var aggregate = Labels(mosaic).Single(context => context.Members.Select(member => member.SourceIndex).SequenceEqual(new[] { 0, 2 }));
            Check("aggregate-keeps-full-members", aggregate.EngineeringHit is not null && aggregate.PointHit is null && ReferenceEquals(aggregate.Members, aggregate.EngineeringHit.Members) && aggregate.Value == 50);
            Check("analysis-point-is-not-fake-series", Labels(funnel).All(context => context.PointHit is not null && context.EngineeringHit is null && context.Members.Count == 1));
            Check("compiled-members-and-first-id", Cards(mosaic, "UsageSharedLabelBody").Any(card => card.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageSharedLabelCount" && text.Text == "来源 2 项") && card.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageSharedLabelId" && text.Text == "首项：shared-a")));
            var controls = charts.SelectMany(chart => Cards(chart, "UsageSharedLegendBody").Concat(Cards(chart, "UsageSharedLabelBody"))).ToArray();
            Model.ProfessionalHeading = "Changed compiled business VM heading"; FlushBindings();
            Check("business-inpc-updates-without-rebuilding", controls.All(control => control.GetVisualParent() is not null) && charts.All(chart => Cards(chart, "UsageSharedLegendBody").All(card => card.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageSharedLegendHeading" && text.Text == Model.ProfessionalHeading))));
            rows[0].Value += 1; Refresh();
            Check("source-inpc-updates-aggregate", Labels(mosaic).Single(context => context.Members.Any(member => member.SourceIndex == 0)).Value == 50 + rows[0].Value - firstValue);
            Model.ProfessionalLegendLimit = 1; Model.ProfessionalLabelLimit = 1; Refresh();
            Check("compiled-limits-bound-both-roots", charts.All(chart => Legends(chart).Count <= 1 && Labels(chart).Count <= 1 && Cards(chart, "UsageSharedLegendBody").Length <= 1 && Cards(chart, "UsageSharedLabelBody").Length <= 1) && funnel.MaxLegendItems == 1 && mosaic.MaxDataLabels == 1);
            Model.ProfessionalLegendLimit = 0; Model.ProfessionalLabelLimit = 0; Refresh();
            Check("zero-limits-clear-both-presentations", charts.All(chart => Cards(chart, "UsageSharedLegendBody").Length == 0 && Cards(chart, "UsageSharedLabelBody").Length == 0));
            Model.ProfessionalLegendLimit = legendLimit; Model.ProfessionalLabelLimit = labelLimit; Refresh();
            foreach (var mode in new[] { ChartHighContrastMode.Light, ChartHighContrastMode.Dark, ChartHighContrastMode.None })
            {
                Model.C7Contrast = mode; RequestedThemeVariant = mode == ChartHighContrastMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light; Refresh();
                Check(mode + "-keeps-original-template-context", charts.All(chart => Cards(chart, "UsageSharedLabelBody").All(card => card.DataContext is ChartDataLabelItemContext context && Original(context.Members)) && Cards(chart, "UsageSharedLegendBody").All(card => card.DataContext is ChartDataLegendItemContext context && ReferenceEquals(context.BusinessDataContext, Model))));
            }
            mosaic.Zoom = 1.25; mosaic.PanX = .05; Refresh();
            Check("viewport-keeps-raw-identities", Labels(mosaic).All(context => Original(context.Members)) && Legends(mosaic).All(context => Original(context.Members)));
            mosaic.SetCurrentValue(EngineeringChartBase.SelectedIndexProperty, 0); FlushBindings();
            Check("two-way-selection-stays-original", Model.ProfessionalSelection == 0 && mosaic.SelectedPoint?.Members.Any(member => ReferenceEquals(member.Item, rows[0])) == true);
            Model.ProfessionalLegend = null; Model.ProfessionalLabel = null; Refresh();
            Check("bound-null-templates-remove-controls", charts.All(chart => Cards(chart, "UsageSharedLegendBody").Length == 0 && Cards(chart, "UsageSharedLabelBody").Length == 0));
            Model.ProfessionalLegend = legend; Model.ProfessionalLabel = label; Refresh();
            Model.ProfessionalReplaceCommand.Execute(null); Refresh();
            Check("replacement-releases-original-observers", rows.All(row => row.SubscriberCount == 0));
            Check("replacement-context-contains-new-originals", charts.All(chart => Labels(chart).All(context => Original(context.Members) && context.Members.All(member => !rows.Any(old => ReferenceEquals(member.Item, old))))));
            ProfessionalPresentationDiagnostics.Add(new { roots = charts.Select(chart => chart.GetType().Name), aggregateMembers = new[] { 0, 2 }, realCompiledTemplates = true, checksUseOriginalMembers = true, exportPixelsValidated = false, nativePixelEvidence = "Demo professional presentations smoke", physicalInputValidated = false });
        }
        finally
        {
            Model.ProfessionalRows = rows; rows[0].Value = firstValue; Model.ProfessionalHeading = heading;
            Model.ProfessionalLegend = legend; Model.ProfessionalLabel = label;
            Model.ProfessionalLegendLimit = legendLimit; Model.ProfessionalLabelLimit = labelLimit;
            Model.C7Contrast = contrast; RequestedThemeVariant = theme;
            foreach (var chart in charts) chart.ResetViewport();
            Model.ProfessionalSelection = selection; expander.IsExpanded = expanded; FlushBindings();
        }
    }
}
