using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    private static readonly string[] C7IntegrationNames = ["Cartesian", "Radar", "Circular", "Gauge", "Spark", "Funnel", "Swarm", "Strip"];
    internal List<object> C7IntegrationDiagnostics { get; } = [];

    internal void VerifyC7IntegrationBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("C7IntegrationExamples")!;
        expander.IsExpanded = true; FlushBindings();
        var charts = C7IntegrationNames.Select(name => this.FindControl<ChartBase>("UsageC7" + name)!).ToArray();
        void Check(string name, bool value) => checks["c7-integration-compiled-" + name] = value;
        // The function-only fixture intentionally omits ChartsTheme. Resource lookup
        // from a Window cannot establish whether a nested ControlTheme was installed.
#if CHARTS_FUNCTION_CONSUMER
        var themed = false;
#else
        var themed = true;
#endif
        var normalTheme = RequestedThemeVariant;
        var swarm = (BeeswarmPlotChart)charts[6]; var strip = (StripPlotChart)charts[7];
        var originals = Model.C5Samples.ToArray();
        Check("all-seven-style-roots", charts.Length == 8 && charts.All(chart => chart.Bounds.Width > 20 && chart.Bounds.Height > 20));
        Check("aliases-are-real-compiled-controls", swarm.GetType() == typeof(BeeswarmPlotChart) && strip.GetType() == typeof(StripPlotChart));
        Model.C5SwarmSelected = 1; FlushBindings();
        var chosen = swarm.SelectedPoint?.Members[0].Item;
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            RequestedThemeVariant = theme;
            foreach (var mode in Enum.GetValues<ChartHighContrastMode>())
            {
                Model.C7Contrast = mode; FlushBindings();
                foreach (var (chart, name) in charts.Zip(C7IntegrationNames))
                {
                    Check(theme + "-" + mode + "-" + name + "-viewmodel-property", chart.HighContrastMode == mode);
                    if (themed && mode != ChartHighContrastMode.None)
                    {
                        var canvas = mode == ChartHighContrastMode.Light ? Colors.White : Colors.Black;
                        var foreground = mode == ChartHighContrastMode.Light ? Colors.Black : Colors.White;
                        Check(theme + "-" + mode + "-" + name + "-optional-theme", chart.Background is ISolidColorBrush background && background.Color == canvas && chart.Foreground is ISolidColorBrush text && text.Color == foreground);
                    }
                }
                Check(theme + "-" + mode + "-business-selection-stable", chosen is not null && ReferenceEquals(chosen, swarm.SelectedPoint?.Members[0].Item) && Model.C5SwarmSelected == 1);
                Check(theme + "-" + mode + "-raw-source-identities", swarm.GetMarks().Count == originals.Length && strip.GetMarks().Count == originals.Length && swarm.GetMarks().All(mark => ReferenceEquals(mark.Members[0].Item, originals[mark.Members[0].SourceIndex])));
            }
        }
        this.FindControl<ComboBox>("UsageC7Contrast")!.SelectedItem = ChartHighContrastMode.Dark; FlushBindings();
        Check("selector-two-way-to-viewmodel", Model.C7Contrast == ChartHighContrastMode.Dark && charts.All(chart => chart.HighContrastMode == ChartHighContrastMode.Dark));
        Model.C7Contrast = ChartHighContrastMode.None; RequestedThemeVariant = normalTheme; Model.C5SwarmSelected = -1; FlushBindings();
        Check("none-restores-palette-style", !themed || swarm.Palette is null && strip.Palette is null);
        C7IntegrationDiagnostics.Add(new { types = charts.Select(chart => chart.GetType().Name).ToArray(), optionalThemeInstalled = themed, modes = Enum.GetNames<ChartHighContrastMode>(), themes = new[] { "Light", "Dark" }, originalSourceIdentityValidated = true, physicalInputValidated = false });
    }
}
