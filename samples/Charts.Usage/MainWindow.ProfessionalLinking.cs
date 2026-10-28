using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    private static void ProfessionalUsageTrace(string phase)
    {
        if (!Program.Arguments.Contains("--smoke", StringComparer.Ordinal)) return;
        var index = Array.IndexOf(Program.Arguments, "--smoke-output");
        var output = index >= 0 && index + 1 < Program.Arguments.Length ? Program.Arguments[index + 1] : "artifacts/platform/charts-usage.json";
        var trace = Path.ChangeExtension(Path.GetFullPath(output), ".trace.log"); Directory.CreateDirectory(Path.GetDirectoryName(trace)!);
        File.AppendAllText(trace, $"{DateTimeOffset.UtcNow:O} {phase}{Environment.NewLine}");
    }
    private readonly ChartDataSynchronizationGroup _professionalLinks = new();
    internal List<object> ProfessionalLinkingDiagnostics { get; } = [];
    private void ConfigureProfessionalLinking()
    {
        var funnel = this.FindControl<FunnelChart>("UsageLinkFunnel")!; var mosaic = this.FindControl<MosaicChart>("UsageLinkMosaic")!;
        object? Key(ChartDataPoint point) => (point.Item as ProfessionalTooltipRecord)?.Id;
        _professionalLinks.Add(funnel, Key); _professionalLinks.Add(mosaic, Key);
        Closed += (_, _) => _professionalLinks.Dispose();
        ProfessionalUsageTrace("professional-linking-configured");
    }
    internal void VerifyProfessionalLinkingBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("ProfessionalLinkingExamples")!; var expanded = expander.IsExpanded; expander.IsExpanded = true; FlushBindings();
        var funnel = this.FindControl<FunnelChart>("UsageLinkFunnel")!; var mosaic = this.FindControl<MosaicChart>("UsageLinkMosaic")!; var selector = this.FindControl<ChartDataRangeSelector>("UsageProfessionalRange")!;
        var rows = Model.ProfessionalLinkRows; var analysisKeys = Model.ProfessionalLinkAnalysisKeys; var engineeringKeys = Model.ProfessionalLinkEngineeringKeys; var range = Model.ProfessionalLinkRange; var selectorRange = Model.ProfessionalLinkSelectorRange; var contrast = Model.C7Contrast; var theme = RequestedThemeVariant;
        void Check(string name, bool value) { checks["c7-professional-linking-compiled-" + name] = value; ProfessionalUsageTrace($"linking: {name}={value}"); }
        bool Original(IReadOnlyList<ChartDataPoint> selected) => selected.All(point => point.SourceIndex >= 0 && point.SourceIndex < Model.ProfessionalLinkRows.Count && ReferenceEquals(point.Item, Model.ProfessionalLinkRows[point.SourceIndex]) && Equals(point.StableId, Model.ProfessionalLinkRows[point.SourceIndex].Id));
        string[] Ids(IReadOnlyList<ChartDataPoint> selected) => selected.Select(point => ((ProfessionalTooltipRecord)point.Item!).Id).Order().ToArray();
        bool Same() => Ids(funnel.SelectedData).SequenceEqual(Ids(mosaic.SelectedData));
        try
        {
            Check("source-and-target-properties-bound", ReferenceEquals(funnel.ItemsSource, rows) && ReferenceEquals(mosaic.ItemsSource, rows) && ReferenceEquals(selector.EngineeringChart, mosaic));
            Model.ProfessionalLinkAnalysisKeys = ["linked-a"]; FlushBindings();
            Check("VM-analysis-key-binds-and-links", funnel.SelectedData.Count == 1 && Ids(funnel.SelectedData).SequenceEqual(new[] { "linked-a" }) && Same() && Original(funnel.SelectedData) && Original(mosaic.SelectedData));
            Check("group-write-updates-engineering-two-way-VM", Model.ProfessionalLinkEngineeringKeys is { Count: 1 } && Equals(Model.ProfessionalLinkEngineeringKeys[0], "linked-a"));
            Model.ProfessionalLinkEngineeringKeys = ["linked-a", "linked-c"]; FlushBindings();
            Check("VM-aggregate-members-reverse-link", mosaic.SelectedData.Count == 2 && Same() && Original(mosaic.SelectedData) && Original(funnel.SelectedData));
            Check("reverse-group-write-updates-analysis-VM", Model.ProfessionalLinkAnalysisKeys?.Count == 2);
            Model.ProfessionalLinkSelectorRange = new(.1, .7); FlushBindings();
            Check("range-selector-compiled-range-binding", selector.Range == Model.ProfessionalLinkSelectorRange && mosaic.DataSelectionRange is not null && Model.ProfessionalLinkRange == mosaic.DataSelectionRange);
            Check("range-expands-real-originals-without-analysis-coordinate-copy", Original(mosaic.SelectedData) && Original(funnel.SelectedData) && Same() && funnel.DataSelectionRange is null);
            var lower = selector.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "PART_LowerHandle"); var before = selector.Range; lower.Focus(); lower.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }); FlushBindings();
            Check("native-keyboard-range-writes-two-way-VM", selector.Range.Lower > before.Lower && selector.Range.Upper == before.Upper && Model.ProfessionalLinkSelectorRange == selector.Range && Model.ProfessionalLinkRange == mosaic.DataSelectionRange);
            Model.ProfessionalLinkRange = null; Model.ProfessionalLinkAnalysisKeys = ["linked-a"]; FlushBindings();
            var first = rows[0].Value; rows[0].Value += 1; FlushBindings();
            Check("INPC-update-keeps-real-selected-identity", Same() && Original(funnel.SelectedData) && Original(mosaic.SelectedData) && funnel.SelectedData.Any(point => ReferenceEquals(point.Item, rows[0])));
            rows[0].Value = first; FlushBindings(); Model.ProfessionalLinkReplaceCommand.Execute(null); FlushBindings();
            Check("source-replacement-releases-old-observers", rows.All(row => row.SubscriberCount == 0));
            Check("source-replacement-remaps-selected-explicit-IDs", Same() && Original(funnel.SelectedData) && Original(mosaic.SelectedData) && Ids(funnel.SelectedData).SequenceEqual(new[] { "linked-a" }) && funnel.SelectedData.All(point => rows.All(row => !ReferenceEquals(row, point.Item))));
            foreach (var value in new[] { ChartHighContrastMode.Light, ChartHighContrastMode.Dark, ChartHighContrastMode.None }) { Model.C7Contrast = value; RequestedThemeVariant = value == ChartHighContrastMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light; FlushBindings(); Check(value + "-selection-and-range-bindings-retained", Original(funnel.SelectedData) && Original(mosaic.SelectedData) && Same() && funnel.HighContrastMode == value && mosaic.HighContrastMode == value); }
            var beforeRange = funnel.DataSelectionRange; mosaic.Zoom = 1.2; mosaic.PanX = .04; FlushBindings(); Check("different-layouts-dont-copy-raw-viewport", funnel.DataSelectionRange == beforeRange && mosaic.Zoom == 1.2 && Same());
            _professionalLinks.SynchronizeSelection = false; Model.ProfessionalLinkEngineeringKeys = ["linked-b"]; FlushBindings(); Check("disabled-link-keeps-analysis-selection", Ids(funnel.SelectedData).SequenceEqual(new[] { "linked-a" }) && Ids(mosaic.SelectedData).SequenceEqual(new[] { "linked-b" }));
            _professionalLinks.SynchronizeSelection = true; _professionalLinks.SynchronizeFrom(mosaic); FlushBindings(); Check("explicit-sync-uses-current-original-business-IDs", Same() && Ids(funnel.SelectedData).SequenceEqual(new[] { "linked-b" }));
            Model.ProfessionalLinkClearCommand.Execute(null); FlushBindings(); Check("compiled-clear-command-clears-both-roots-and-range", funnel.SelectedData.Count == 0 && mosaic.SelectedData.Count == 0 && mosaic.DataSelectionRange is null && Model.ProfessionalLinkRange is null);
            ProfessionalLinkingDiagnostics.Add(new { roots = new[] { funnel.GetType().Name, mosaic.GetType().Name }, realCompiledBindings = true, rangeCoordinates = "local family plot layout fractions", selectionCoordinates = "explicit business IDs", originalSourcesRetained = true, nativeKeyboardRouted = true, physicalInputValidated = false, exportPixelEvidence = "Demo professional linking smoke" });
        }
        finally
        {
            _professionalLinks.SynchronizeSelection = false; Model.ProfessionalLinkRows = rows; Model.ProfessionalLinkRange = range; Model.ProfessionalLinkSelectorRange = selectorRange; Model.ProfessionalLinkAnalysisKeys = analysisKeys; Model.ProfessionalLinkEngineeringKeys = engineeringKeys;
            mosaic.ResetViewport(); funnel.ClearTrackball(); mosaic.ClearTrackball(); Model.C7Contrast = contrast; RequestedThemeVariant = theme; expander.IsExpanded = expanded; FlushBindings(); _professionalLinks.SynchronizeSelection = true;
        }
    }
}
