using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal CircularChart CircularPieChart => this.FindControl<CircularChart>("UsageCircularPieChart")!;
    internal CircularChart CircularDonutChart => this.FindControl<CircularChart>("UsageCircularDonutChart")!;
    internal CircularChart CircularSemiChart => this.FindControl<CircularChart>("UsageCircularSemiChart")!;
    internal List<object> CircularBindingDiagnostics { get; } = [];

    internal void VerifyCircularBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("CircularExamples")!; expander.IsExpanded = true; FlushBindings();
        var pieChart = CircularPieChart; var donutChart = CircularDonutChart; var semiChart = CircularSemiChart;
        var pie = (PieSeries)pieChart.Series[0]; var donut = (DonutSeries)donutChart.Series[0]; var semi = (SemiDonutSeries)semiChart.Series[0];
        var initial = pieChart.GetSlices(); _ = donutChart.GetSlices(); _ = semiChart.GetSlices(); FlushBindings();
        static bool Near(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) < 1e-9;
        checks["circular-compiled-types-and-real-business-sources"] = ReferenceEquals(pie.ItemsSource, Model.CircularRecords) && ReferenceEquals(donut.ItemsSource, Model.CircularRecords) && ReferenceEquals(semi.ItemsSource, Model.CircularRecords);
        checks["circular-compiled-label-value-id-color-paths"] = pie.ValuePath == "Value" && pie.CategoryPath == "Label" && pie.IdPath == "Id" && pie.ColorPath == "Color";
        checks["circular-compiled-zero-negative-ignore-and-original-shares"] = initial.Count == 3 && Near(initial[0].Fraction, .5) && Near(initial[1].Fraction, .25) && Near(initial[2].Fraction, .25) && initial.All(value => value.Data.SourceIndex < 3);
        checks["circular-compiled-slice-preserves-item-id-label-and-raw-value"] = initial[1].Data is { SourceIndex: 1, Point.Y: 25 } data && Equals(data.StableId, "circular-b") && Equals(data.Category, "门店") && ReferenceEquals(data.Item, Model.CircularRecords[1]);
        checks["circular-compiled-business-color-reaches-current-fill"] = initial[1].Fill is ISolidColorBrush fill && fill.Color == Color.Parse("#19A798");
        checks["circular-compiled-donut-semi-defaults-and-center-text"] = Near(donut.InnerRadius, .6) && Near(semi.InnerRadius, .6) && Near(semi.StartAngle, 180) && Near(semi.SweepAngle, 180) && donutChart.CenterText == "渠道总览";
        Model.CircularInnerRadius = .4; Model.CircularSweep = 270; FlushBindings();
        checks["circular-compiled-model-updates-inner-radius-and-sweep"] = Near(donut.InnerRadius, .4) && Near(donut.SweepAngle, 270);
        this.FindControl<ComboBox>("UsageCircularInnerRadius")!.SelectedItem = .8; this.FindControl<ComboBox>("UsageCircularSweep")!.SelectedItem = 180d; FlushBindings();
        checks["circular-compiled-radius-and-sweep-selectors-are-two-way"] = Near(Model.CircularInnerRadius, .8) && Near(Model.CircularSweep, 180) && Near(donut.InnerRadius, .8) && Near(donut.SweepAngle, 180);
        Model.CircularNegativeMode = ChartCircularNegativeValueMode.Absolute; FlushBindings(); var absolute = pieChart.GetSlices();
        checks["circular-compiled-absolute-mode-keeps-signed-raw-data"] = absolute.Count == 4 && absolute.Single(value => value.Data.SourceIndex == 4) is { Data.Point.Y: -10 } negative && Near(negative.Fraction, 10d / 110);
        this.FindControl<ComboBox>("UsageCircularNegativeMode")!.SelectedItem = ChartCircularNegativeValueMode.Ignore; FlushBindings();
        checks["circular-compiled-negative-policy-selector-is-two-way"] = Model.CircularNegativeMode == ChartCircularNegativeValueMode.Ignore && pieChart.GetSlices().Count == 3;
        Model.CircularLabels = false; FlushBindings();
        checks["circular-compiled-label-visibility-updates-all-charts"] = !pieChart.ShowDataLabels && !donutChart.ShowDataLabels && !semiChart.ShowDataLabels;
        this.FindControl<CheckBox>("UsageCircularLabels")!.IsChecked = true; FlushBindings();
        checks["circular-compiled-label-checkbox-is-two-way"] = Model.CircularLabels && pieChart.ShowDataLabels && donutChart.ShowDataLabels;
        Model.CircularReduceMotion = false; Model.CircularPaused = true; FlushBindings();
        checks["circular-compiled-animation-preferences-bind-to-controls"] = !pieChart.ReduceMotion && !donutChart.ReduceMotion && pieChart.IsAnimationPaused && donutChart.IsAnimationPaused;
        this.FindControl<CheckBox>("UsageCircularReduceMotion")!.IsChecked = true; this.FindControl<CheckBox>("UsageCircularPaused")!.IsChecked = false; FlushBindings();
        checks["circular-compiled-animation-checkboxes-are-two-way"] = Model.CircularReduceMotion && !Model.CircularPaused && pieChart.ReduceMotion && !pieChart.IsAnimationPaused;
        var selected = pieChart.GetSlices().Single(value => value.Data.SourceIndex == 1); pieChart.SelectSlice(selected); FlushBindings();
        checks["circular-compiled-selection-retains-source-item-and-id"] = pieChart.SelectedPoint is { Index: 1, Point.Y: 25 } hit && ReferenceEquals(hit.Item, Model.CircularRecords[1]) && Equals(hit.StableId, "circular-b");
        var before = pie.SourceVersion; Model.CircularRecords[1].Value = 50; var updated = pieChart.GetSlices(); FlushBindings();
        checks["circular-compiled-inpc-value-renormalizes-with-original-identity"] = pie.SourceVersion > before && Near(updated.Single(value => value.Data.SourceIndex == 1).Fraction, .4) && pieChart.SelectedPoint is { Index: 1, Point.Y: 50 } changed && ReferenceEquals(changed.Item, Model.CircularRecords[1]);
        Model.CircularRecords[1].Color = "#D7256A"; var recolored = pieChart.GetSlices(); FlushBindings();
        checks["circular-compiled-inpc-color-updates-current-slice"] = recolored.Single(value => value.Data.SourceIndex == 1).Fill is ISolidColorBrush newFill && newFill.Color == Color.Parse("#D7256A");
        var source = pie.CreateSnapshot();
        checks["circular-compiled-snapshot-keeps-zero-and-negative-business-rows"] = source.Points.Count == 5 && source.Points[3].Point.Y == 0 && source.Points[4].Point.Y == -10 && source.Points[1].SourceIndex == 1;
        checks["circular-compiled-series-inherits-viewmodel-datacontext"] = ReferenceEquals(pie.DataContext, Model) && ReferenceEquals(donut.DataContext, Model) && ReferenceEquals(semi.DataContext, Model);
        var content = Content; Content = null; FlushBindings();
        checks["circular-compiled-detach-releases-business-observers"] = Model.CircularRecords.All(value => value.SubscriberCount == 0);
        Content = content; FlushBindings(); _ = pieChart.GetSlices(); _ = donutChart.GetSlices(); _ = semiChart.GetSlices();
        checks["circular-compiled-reattach-restores-business-observers"] = Model.CircularRecords.All(value => value.SubscriberCount > 0);
        CircularBindingDiagnostics.Add(new { initial = initial.Select(value => new { value.Data.SourceIndex, value.Data.StableId, value.Data.Category, rawValue = value.Data.Point.Y, value.Fraction }), updated = updated.Select(value => new { value.Data.SourceIndex, rawValue = value.Data.Point.Y, value.Fraction }), physicalInputValidated = false, performanceMeasured = false, absoluteTolerance = 1e-9 });
        pieChart.ClearSelection(); Model.CircularRecords[1].Value = 25; Model.CircularRecords[1].Color = "#19A798"; Model.CircularInnerRadius = .6; Model.CircularSweep = 360; Model.CircularLabels = true; Model.CircularNegativeMode = ChartCircularNegativeValueMode.Ignore; Model.CircularReduceMotion = true; Model.CircularPaused = false;
        expander.IsExpanded = false; FlushBindings();
    }
}
