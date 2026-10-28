using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal List<object> ProfessionalTooltipDiagnostics { get; } = [];
    internal void VerifyProfessionalTooltipBindings(Dictionary<string, bool> checks)
    {
        this.FindControl<Expander>("ProfessionalTooltipExamples")!.IsExpanded = true; FlushBindings();
        var funnel = this.FindControl<FunnelChart>("UsageProfessionalFunnel")!;
        var mosaic = this.FindControl<MosaicChart>("UsageProfessionalMosaic")!;
        ChartBase[] charts = [funnel, mosaic];
        void Check(string name, bool value) => checks["c7-professional-tooltip-compiled-" + name] = value;
        Border? Card(ChartBase chart) => chart.GetVisualDescendants().OfType<Border>().SingleOrDefault(control => control.Name == "UsageProfessionalTooltipBody");
        void Move(ChartBase chart, int sourceIndex)
        {
            FlushBindings();
            var point = chart is AnalysisChartBase analysis ? analysis.GetMarks().Single(mark => mark.Data.SourceIndex == sourceIndex).LabelPosition :
                ((EngineeringChartBase)chart).GetMarks().Single(mark => mark.Members.Any(member => member.SourceIndex == sourceIndex)).Position;
            using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            var target = chart.TranslatePoint(point, this) ?? throw new InvalidOperationException("Professional chart has no window transform.");
            chart.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, chart, pointer, this, target, 1, default, KeyModifiers.None));
            FlushBindings();
        }
        var original = Model.C7TooltipRows; var template = Model.C7ProfessionalTemplate; var title = Model.C7TooltipTitle;
        var normalTheme = RequestedThemeVariant;
        var originalValue = original[0].Value; var originalSelection = Model.C7TooltipSelection;
        var originalContrast = Model.C7Contrast; var originalEnabled = Model.C7ProfessionalTooltipsEnabled;
        try
        {
            Check("template-and-source-are-real-bindings", template is not null && ReferenceEquals(funnel.TooltipContentTemplate, template) && ReferenceEquals(mosaic.TooltipContentTemplate, template) && ReferenceEquals(funnel.ItemsSource, original) && ReferenceEquals(mosaic.ItemsSource, original));
            Move(funnel, 0); var firstCard = Card(funnel); var single = firstCard?.DataContext as ChartDataTooltipContext;
            Check("analytical-original-point-context", single is { Members.Count: 1, PointHit: not null, EngineeringHit: null } && ReferenceEquals(single.Members[0].Item, original[0]) && single.Members[0].SourceIndex == 0 && Equals(single.Members[0].StableId, "tooltip-a") && ReferenceEquals(single.BusinessDataContext, Model));
            Move(funnel, 1);
            Check("control-reused-for-next-original-point", ReferenceEquals(firstCard, Card(funnel)) && Card(funnel)?.DataContext is ChartDataTooltipContext next && ReferenceEquals(next.Members[0].Item, original[1]));
            Move(mosaic, 0); var aggregate = Card(mosaic)?.DataContext as ChartDataTooltipContext;
            Check("aggregate-is-all-real-members", aggregate is { EngineeringHit: not null, PointHit: null } && aggregate.Members.Select(member => member.SourceIndex).SequenceEqual(new[] { 0, 2 }) && ReferenceEquals(aggregate.Members, aggregate.EngineeringHit.Members) && ReferenceEquals(aggregate.Members[0].Item, original[0]) && ReferenceEquals(aggregate.Members[1].Item, original[2]));
            Check("compiled-context-fields", Card(mosaic)?.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageProfessionalMemberCount" && text.Text == "原始成员：2") == true && Card(mosaic)?.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageProfessionalFirstId" && text.Text == "首项：tooltip-a") == true);
            Model.C7TooltipTitle = "Changed business heading"; FlushBindings();
            Check("nested-compiled-business-vm-context", charts.All(chart => Card(chart)?.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageProfessionalBusinessTitle" && text.Text == Model.C7TooltipTitle) == true));
            var oldCard = Card(mosaic); original[0].Value += 1; FlushBindings(); Move(mosaic, 0);
            Check("source-inpc-retires-reading", oldCard?.DataContext is null && Card(mosaic)?.DataContext is ChartDataTooltipContext updated && updated.Members[0].Point.Y == originalValue + 1 && ReferenceEquals(updated.Members[0].Item, original[0]));
            mosaic.SetCurrentValue(EngineeringChartBase.SelectedIndexProperty, 0); FlushBindings();
            Check("two-way-selection-remains-original", Model.C7TooltipSelection == 0 && mosaic.SelectedPoint?.Members.Any(member => ReferenceEquals(member.Item, original[0])) == true);
            foreach (var mode in new[] { ChartHighContrastMode.Light, ChartHighContrastMode.Dark, ChartHighContrastMode.None })
            {
                Model.C7Contrast = mode; RequestedThemeVariant = mode == ChartHighContrastMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light; FlushBindings();
                foreach (var chart in charts) Move(chart, 0);
                Check(mode + "-template-still-uses-current-source", charts.All(chart => Card(chart)?.DataContext is ChartDataTooltipContext context && ReferenceEquals(context.Members[0].Item, original[0]) && ReferenceEquals(context.BusinessDataContext, Model)));
#if !CHARTS_FUNCTION_CONSUMER
                if (mode != ChartHighContrastMode.None)
                {
                    var canvas = mode == ChartHighContrastMode.Dark ? Colors.Black : Colors.White;
                    Check(mode + "-shared-tooltip-chrome", charts.All(chart => chart.GetVisualDescendants().OfType<Border>().Any(control => control.Name == "PART_ChartDataTooltip" && control.Background is ISolidColorBrush background && background.Color == canvas)));
                }
#endif
            }
            Model.C7ProfessionalTooltipsEnabled = false; FlushBindings();
            Check("bound-disable-releases-both-presentations", charts.All(chart => Card(chart) is null));
            Model.C7ProfessionalTooltipsEnabled = true; FlushBindings(); foreach (var chart in charts) Move(chart, 0);
            Model.C7ProfessionalTemplate = null; FlushBindings();
            Check("template-binding-null-restores-legacy", charts.All(chart => Card(chart) is null) && funnel.TooltipContentTemplate is null && mosaic.TooltipContentTemplate is null);
            Model.C7ProfessionalTemplate = template; FlushBindings(); foreach (var chart in charts) Move(chart, 0);
            oldCard = Card(mosaic); Model.C7TooltipReplaceCommand.Execute(null); FlushBindings();
            Check("source-binding-replacement-releases-old-observers-and-context", original.All(row => row.SubscriberCount == 0) && oldCard?.DataContext is null && ReferenceEquals(funnel.ItemsSource, Model.C7TooltipRows) && ReferenceEquals(mosaic.ItemsSource, Model.C7TooltipRows));
            foreach (var chart in charts) Move(chart, 0);
            Check("replacement-tooltip-uses-new-originals", charts.All(chart => Card(chart)?.DataContext is ChartDataTooltipContext context && ReferenceEquals(context.Members[0].Item, Model.C7TooltipRows[0]) && !ReferenceEquals(context.Members[0].Item, original[0])));
            ProfessionalTooltipDiagnostics.Add(new { roots = charts.Select(chart => chart.GetType().Name), aggregateSourceIndices = new[] { 0, 2 }, contextsKeepAllOriginals = true, templateIsCompiledAxaml = true, physicalInputValidated = false, exportsValidated = false, exportValidationScope = "shared-tooltip-tests-and-native-demo" });
        }
        finally
        {
            Model.C7TooltipRows = original; original[0].Value = originalValue; Model.C7TooltipTitle = title;
            Model.C7ProfessionalTemplate = template; Model.C7ProfessionalTooltipsEnabled = originalEnabled;
            Model.C7Contrast = originalContrast; RequestedThemeVariant = normalTheme;
            foreach (var chart in charts) chart.ResetViewport();
            Model.C7TooltipSelection = originalSelection; FlushBindings();
        }
    }
}
