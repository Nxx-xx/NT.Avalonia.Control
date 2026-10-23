using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal PolarChart PolarLineChart => this.FindControl<PolarChart>("UsagePolarLineChart")!;
    internal PolarChart PolarScatterChart => this.FindControl<PolarChart>("UsagePolarScatterChart")!;
    internal RadarChart RadarChart => this.FindControl<RadarChart>("UsageRadarChart")!;
    internal List<object> PolarBindingDiagnostics { get; } = [];

    internal void VerifyPolarBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("PolarExamples")!; expander.IsExpanded = true; FlushBindings();
        var chart = PolarLineChart; var line = (PolarLineSeries)chart.Series[0]; var radar = (RadarSeries)RadarChart.Series[0];
        var initial = chart.GetPoints(); var radarInitial = RadarChart.GetPoints();
        var charts = new[] { chart, PolarScatterChart, this.FindControl<PolarChart>("UsagePolarFilledChart")!, RadarChart, this.FindControl<PolarChart>("UsageRadialLineChart")!, this.FindControl<PolarChart>("UsagePolarAreaChart")!, this.FindControl<PolarChart>("UsageRoseChart")!, this.FindControl<PolarChart>("UsageRadialBarChart")! };
        void Check(string name, bool result) => checks["polar-compiled-" + name] = result;
        static bool Near(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) < 1e-8;
        Check("eight-series-bind-real-business-sources", charts.Length == 8 && charts.All(value => value.Series.Count > 0) && ReferenceEquals(line.ItemsSource, Model.PolarRecords) && ReferenceEquals(radar.ItemsSource, Model.RadarRecords));
        Check("angle-radius-aliases-and-id-color-paths", line.AnglePath == "Angle" && line.RadiusPath == "Value" && line.CategoryPath == "Angle" && line.ValuePath == "Value" && line.IdPath == "Id" && line.ColorPath == "Color");
        Check("numeric-axes-and-degree-defaults", chart.AngularAxis is NumericAxis { Minimum: 0, Maximum: 360 } && chart.RadialAxis is NumericAxis { Minimum: 0, Maximum: 100 } && chart.AngleUnit == ChartAngleUnit.Degrees);
        Check("points-retain-raw-angle-radius-item-and-stable-id", initial.Count == 4 && initial[1].Data is { SourceIndex: 1, Point.X: 90, Point.Y: 50 } data && Equals(data.StableId, "polar-b") && ReferenceEquals(data.Item, Model.PolarRecords[1]));
        Check("business-color-reaches-projected-point", initial[1].Fill is ISolidColorBrush fill && fill.Color == Color.Parse("#19A798"));
        Check("radar-host-defaults-to-polygon-grid", RadarChart.GridShape == PolarGridShape.Polygon && radar.MaxValue == 100);
        var firstQuality = radarInitial.Single(value => ReferenceEquals(value.Series, radar) && Equals(value.Data.Category, "质量"));
        var secondQuality = radarInitial.Single(value => !ReferenceEquals(value.Series, radar) && Equals(value.Data.Category, "质量"));
        Check("radar-reordered-series-share-category-angle", Near(firstQuality.Angle, secondQuality.Angle) && firstQuality.Data.SourceIndex == 1 && secondQuality.Data.SourceIndex == 3);
        Check("area-rose-and-bars-create-source-sectors", charts.Skip(5).All(value => value.GetSectors().Count == 4 && value.GetSectors().All(sector => sector.Data.Item is PolarUsageRecord)));
        Model.PolarMixedReference = true; FlushBindings();
        Check("mixed-radius-reference-toggle-keeps-both-source-projections", charts[5].Series.OfType<PolarLineSeries>().Single().IsVisible && charts[5].GetPoints().Count == 4 && charts[5].GetSectors().Count == 4);
        this.FindControl<CheckBox>("UsagePolarMixedReference")!.IsChecked = false; FlushBindings();
        Check("mixed-radius-reference-toggle-is-two-way", !Model.PolarMixedReference && !charts[5].Series.OfType<PolarLineSeries>().Single().IsVisible && charts[5].GetSectors().Count == 4);
        Model.PolarClockwise = false; Model.PolarClosed = false; Model.PolarStartAngle = 45; Model.PolarSweep = 270; Model.PolarGrid = PolarGridShape.Polygon; FlushBindings();
        Check("viewmodel-updates-direction-start-sweep-grid-and-closure", !chart.Clockwise && !line.IsClosed && Near(chart.StartAngle, 45) && Near(chart.SweepAngle, 270) && chart.GridShape == PolarGridShape.Polygon);
        this.FindControl<CheckBox>("UsagePolarClockwise")!.IsChecked = true; this.FindControl<CheckBox>("UsagePolarClosed")!.IsChecked = true;
        this.FindControl<ComboBox>("UsagePolarStart")!.SelectedItem = -90d; this.FindControl<ComboBox>("UsagePolarSweep")!.SelectedItem = 360d; this.FindControl<ComboBox>("UsagePolarGrid")!.SelectedItem = PolarGridShape.Circle; FlushBindings();
        Check("direction-start-sweep-grid-closure-selectors-are-two-way", Model.PolarClockwise && Model.PolarClosed && Model.PolarStartAngle == -90 && Model.PolarSweep == 360 && Model.PolarGrid == PolarGridShape.Circle && chart.Clockwise && line.IsClosed);
        Model.PolarLabels = true; FlushBindings(); Check("viewmodel-shows-data-labels-on-eight-charts", charts.All(value => value.ShowDataLabels));
        this.FindControl<CheckBox>("UsagePolarLabels")!.IsChecked = false; FlushBindings(); Check("data-label-checkbox-is-two-way", !Model.PolarLabels && charts.All(value => !value.ShowDataLabels));
        Model.PolarUnit = ChartAngleUnit.Radians; FlushBindings(); var radians = chart.GetPoints();
        Check("radian-unit-and-angular-domain-are-compiled-bindings", chart.AngleUnit == ChartAngleUnit.Radians && Near(chart.AngularAxis.Maximum!.Value, 2 * Math.PI) && Near(line.CreateSnapshot().Points[1].Point.X, Math.PI / 2));
        Check("degrees-to-radians-preserves-display-and-source-id", radians.Count == initial.Count && Near(radians[1].Angle, initial[1].Angle) && Equals(radians[1].Data.StableId, "polar-b") && ReferenceEquals(radians[1].Data.Item, Model.PolarRecords[1]));
        this.FindControl<ComboBox>("UsagePolarUnit")!.SelectedItem = ChartAngleUnit.Degrees; FlushBindings();
        Check("unit-selector-converts-business-angles-two-way", Model.PolarUnit == ChartAngleUnit.Degrees && chart.AngleUnit == ChartAngleUnit.Degrees && Near(line.CreateSnapshot().Points[1].Point.X, 90));
        var candidate = chart.GetPoints().Single(value => value.Data.SourceIndex == 1); chart.SelectPoint(candidate.Hit); FlushBindings();
        Check("selectedpoint-two-way-binding-retains-original-business-record", Model.PolarSelectedPoint is { Index: 1, Point.Y: 50 } selected && ReferenceEquals(selected.Item, Model.PolarRecords[1]) && Equals(selected.StableId, "polar-b"));
        Model.PolarSelectedPoint = chart.GetPoints().Single(value => value.Data.SourceIndex == 2).Hit; FlushBindings();
        Check("viewmodel-can-select-another-source-item", chart.SelectedPoint is { Index: 2 } selectedFromModel && Equals(selectedFromModel.StableId, "polar-c"));
        var before = line.SourceVersion; Model.PolarRecords[2].Value = 95; Model.PolarRecords[2].Color = "#D7256A"; var changed = chart.GetPoints().Single(value => value.Data.SourceIndex == 2); FlushBindings();
        Check("inpc-radius-and-color-refresh-projection-and-selection", line.SourceVersion > before && changed.Data.Point.Y == 95 && changed.Fill is ISolidColorBrush newFill && newFill.Color == Color.Parse("#D7256A") && chart.SelectedPoint is { Index: 2, Point.Y: 95 });
        Model.PolarRecords[2].Angle = 225; var moved = chart.GetPoints().Single(value => value.Data.SourceIndex == 2); FlushBindings();
        Check("inpc-angle-reprojects-without-losing-source-identity", Near(moved.Angle, 270 + 225) && Equals(moved.Data.StableId, "polar-c") && ReferenceEquals(moved.Data.Item, Model.PolarRecords[2]));
        Check("series-and-axis-inherit-viewmodel-context", charts.All(value => value.Series.All(series => ReferenceEquals(series.DataContext, Model)) && ReferenceEquals(value.AngularAxis.DataContext, Model) && ReferenceEquals(value.RadialAxis.DataContext, Model)));
        var content = Content; Content = null; FlushBindings();
        Check("detach-releases-business-observers", Model.PolarRecords.Concat(Model.RadarRecords).Concat(Model.RadarComparisonRecords).All(value => value.SubscriberCount == 0));
        Content = content; FlushBindings(); foreach (var value in charts) { _ = value.GetPoints(); _ = value.GetSectors(); }
        Check("reattach-restores-business-observers", Model.PolarRecords.Concat(Model.RadarRecords).Concat(Model.RadarComparisonRecords).All(value => value.SubscriberCount > 0));
        PolarBindingDiagnostics.Add(new { initial = initial.Select(value => new { value.Data.SourceIndex, value.Data.StableId, rawAngle = value.Data.Point.X, rawRadius = value.Data.Point.Y, value.Angle, value.Radius }), radians = radians.Select(value => new { value.Data.SourceIndex, rawAngle = value.Data.Point.X, value.Angle }), radarCategories = radarInitial.Select(value => new { value.Series.Title, value.Data.Category, value.Data.SourceIndex, value.Angle }), physicalInputValidated = false, performanceMeasured = false, absoluteTolerance = 1e-8 });
        chart.ClearSelection(); Model.PolarRecords[2].Value = 70; Model.PolarRecords[2].Angle = 180; Model.PolarRecords[2].Color = "#F2A33A"; Model.PolarSelectedPoint = null;
        expander.IsExpanded = false; FlushBindings();
    }
}
