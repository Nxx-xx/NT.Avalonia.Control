using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal CarpetPlot EngineeringCarpet => this.FindControl<CarpetPlot>("UsageEngineeringCarpet")!;
    internal HexbinChart EngineeringHexbin => this.FindControl<HexbinChart>("UsageEngineeringHexbin")!;
    internal SmithChart EngineeringSmith => this.FindControl<SmithChart>("UsageEngineeringSmith")!;
    internal TernaryChart EngineeringTernary => this.FindControl<TernaryChart>("UsageEngineeringTernary")!;
    internal WindRoseChart EngineeringWind => this.FindControl<WindRoseChart>("UsageEngineeringWind")!;
    internal List<object> EngineeringBindingDiagnostics { get; } = [];

    internal void VerifyEngineeringBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("EngineeringExamples")!;
        expander.IsExpanded = true; FlushBindings();
        EngineeringChartBase[] charts = [EngineeringCarpet, EngineeringHexbin, EngineeringSmith, EngineeringTernary, EngineeringWind];
        void Check(string key, bool value) => checks["engineering-compiled-" + key] = value;
        static bool Near(double expected, double actual) => double.IsFinite(actual) && Math.Abs(expected - actual) < 1e-8;
        IEnumerable<EngineeringBusinessRecord> Records() => Model.EngineeringCarpetRecords.Cast<EngineeringBusinessRecord>()
            .Concat(Model.EngineeringHexRecords).Concat(Model.EngineeringSmithRecords).Concat(Model.EngineeringMixtureRecords).Concat(Model.EngineeringWindRecords);
        Check("five-controls-have-arranged-plot-and-specialized-grid", charts.All(chart => chart.PlotArea.Width > 100 && chart.PlotArea.Height > 100 && chart.GetGridLines().Count > 0));
        Check("business-sources-and-paths-stay-bound", ReferenceEquals(EngineeringCarpet.ItemsSource, Model.EngineeringCarpetRecords) && EngineeringCarpet.APath == "ParameterA" && EngineeringCarpet.YPath == "Temperature" &&
            ReferenceEquals(EngineeringHexbin.ItemsSource, Model.EngineeringHexRecords) && EngineeringHexbin.XPath == "Easting" && EngineeringHexbin.ValuePath == "Load" &&
            ReferenceEquals(EngineeringSmith.ItemsSource, Model.EngineeringSmithRecords) && EngineeringSmith.ResistancePath == "Resistance" &&
            ReferenceEquals(EngineeringTernary.ItemsSource, Model.EngineeringMixtureRecords) && EngineeringTernary.CPath == "Silt" &&
            ReferenceEquals(EngineeringWind.ItemsSource, Model.EngineeringWindRecords) && EngineeringWind.DirectionPath == "Direction" && EngineeringWind.FrequencyPath == "Frequency");
        var originalSmith = EngineeringSmith.GetMarks(); var smithMark = originalSmith[1];
        Check("smith-mark-and-hit-retain-physical-item-id-index", ReferenceEquals(smithMark.Members[0].Item, Model.EngineeringSmithRecords[1]) &&
            Equals(smithMark.Members[0].StableId, "antenna-b") && smithMark.Members[0].SourceIndex == 1 && smithMark.Members[0].Point == new ChartPoint(50, 50) &&
            EngineeringSmith.HitTest(smithMark.Position)?.Members[0].Item == Model.EngineeringSmithRecords[1]);
        var physical = EngineeringSmith.Unproject(EngineeringSmith.Project(new(50, 50)));
        var composition = EngineeringTernary.Unproject(EngineeringTernary.Project(new(60, 25, 15)));
        var surface = EngineeringCarpet.Interpolate(.25, .75);
        var parameter = EngineeringCarpet.Unproject(EngineeringCarpet.Project(surface));
        Check("physical-and-parameter-inverses-use-real-chart-layout", Near(50, physical.R) && Near(50, physical.X) && Near(.6, composition.A) && Near(.25, composition.B) &&
            Near(.15, composition.C) && Near(11.84375, surface.Y) && Near(.25, parameter.A) && Near(.75, parameter.B));
        var sharedHex = EngineeringHexbin.GetMarks().First(mark => mark.Members.Any(member => member.SourceIndex == 0));
        Check("hexbin-keeps-all-real-contributing-members", sharedHex.Members.Any(member => member.SourceIndex == 1) &&
            sharedHex.Members.All(member => ReferenceEquals(member.Item, Model.EngineeringHexRecords[member.SourceIndex])) &&
            EngineeringHexbin.GetMarks().SelectMany(mark => mark.Members).Select(member => member.SourceIndex).Order().SequenceEqual(Enumerable.Range(0, 80)));
        Check("wind-compass-speed-stack-and-global-percentages", EngineeringWind.Rose.Count == 7 && EngineeringWind.Rose.OmittedCount == 0 &&
            Near(24, EngineeringWind.Rose.TotalFrequency) && Near(8, EngineeringWind.Rose.Sectors[0].Frequency) && Near(100, EngineeringWind.Rose.Bins.Sum(bin => bin.Percentage)) &&
            EngineeringWind.GetMarks().SelectMany(mark => mark.Members).All(member => ReferenceEquals(member.Item, Model.EngineeringWindRecords[member.SourceIndex])));
        EngineeringSmith.SelectedIndex = 1; FlushBindings();
        Check("selection-flows-to-viewmodel-with-business-identity", Model.EngineeringSelectedIndex == 1 && ReferenceEquals(EngineeringSmith.SelectedPoint?.Members[0].Item, Model.EngineeringSmithRecords[1]));
        Model.EngineeringSelectedIndex = 2; FlushBindings();
        Check("viewmodel-selection-flows-back-to-control", EngineeringSmith.SelectedIndex == 2 && Equals(EngineeringSmith.SelectedPoint?.Members[0].StableId, "antenna-c"));
        this.FindControl<ComboBox>("UsageEngineeringOffset")!.SelectedItem = 1d;
        this.FindControl<ComboBox>("UsageEngineeringHexAggregation")!.SelectedItem = HexbinAggregation.Sum;
        this.FindControl<ComboBox>("UsageEngineeringHexRadius")!.SelectedItem = 24d;
        this.FindControl<ComboBox>("UsageEngineeringReference")!.SelectedItem = 75d;
        this.FindControl<ComboBox>("UsageEngineeringSectors")!.SelectedItem = 8; FlushBindings();
        Check("option-controls-have-two-way-compiled-bindings", Model.EngineeringCarpetOffset == 1 && EngineeringCarpet.Offset == 1 &&
            Model.EngineeringHexAggregation == HexbinAggregation.Sum && EngineeringHexbin.Aggregation == HexbinAggregation.Sum && Model.EngineeringHexRadius == 24 && EngineeringHexbin.BinRadius == 24 &&
            Model.EngineeringReferenceImpedance == 75 && EngineeringSmith.ReferenceImpedance == 75 && Model.EngineeringSectorCount == 8 && EngineeringWind.SectorCount == 8);
        Model.EngineeringCarpetOffset = .5; Model.EngineeringHexAggregation = HexbinAggregation.Count; Model.EngineeringHexRadius = 16;
        Model.EngineeringReferenceImpedance = 50; Model.EngineeringSectorCount = 16; Model.EngineeringSelectedIndex = -1; FlushBindings();
        var oldTemperature = Model.EngineeringCarpetRecords[4].Temperature;
        var oldLoad = Model.EngineeringHexRecords[0].Load;
        var oldResistance = Model.EngineeringSmithRecords[1].Resistance;
        var oldSand = Model.EngineeringMixtureRecords[0].Sand;
        var oldFrequency = Model.EngineeringWindRecords[0].Frequency;
        Model.EngineeringUpdateCommand.Execute(null); FlushBindings();
        Check("business-inpc-rebuilds-five-projections-and-keeps-old-snapshot", EngineeringCarpet.Interpolate(1, 1).Y == oldTemperature + .25 &&
            EngineeringHexbin.GetMarks().SelectMany(mark => mark.Members).Single(member => member.SourceIndex == 0).Point == new ChartPoint(0, 0) &&
            EngineeringSmith.GetMarks()[1].Members[0].Point.X == oldResistance + 10 && originalSmith[1].Members[0].Point.X == oldResistance &&
            EngineeringTernary.GetMarks()[0].Members[0].Value is TernaryPoint { A: 65 } && Near(25, EngineeringWind.Rose.TotalFrequency));
        var added = new EngineeringHexRecord("sensor-added", 0, 0, 11); Model.EngineeringHexRecords.Add(added); FlushBindings();
        Check("collection-add-keeps-new-source-member-and-observer", EngineeringHexbin.Binning.Count == 81 && added.SubscriberCount > 0 &&
            EngineeringHexbin.GetMarks().SelectMany(mark => mark.Members).Any(member => ReferenceEquals(member.Item, added) && Equals(member.StableId, added.Id)));
        added.Load = double.NaN; FlushBindings();
        Check("nonfinite-aggregation-value-is-omitted-without-shifting-indices", EngineeringHexbin.Binning.Count == 80 && EngineeringHexbin.Binning.OmittedCount == 1 &&
            EngineeringHexbin.GetMarks().SelectMany(mark => mark.Members).All(member => member.SourceIndex < 80));
        Model.EngineeringHexRecords.Remove(added); FlushBindings(); Check("removed-item-releases-its-observer", added.SubscriberCount == 0);
        Check("attached-collections-and-business-records-are-observed", Records().All(record => record.SubscriberCount > 0) && Model.EngineeringHexRecords.SubscriberCount > 0 && Model.EngineeringWindRecords.SubscriberCount > 0);
        var content = Content; Content = null; FlushBindings();
        Check("unload-releases-all-five-business-sources", Records().All(record => record.SubscriberCount == 0) && Model.EngineeringCarpetRecords.SubscriberCount == 0 &&
            Model.EngineeringHexRecords.SubscriberCount == 0 && Model.EngineeringSmithRecords.SubscriberCount == 0 && Model.EngineeringMixtureRecords.SubscriberCount == 0 && Model.EngineeringWindRecords.SubscriberCount == 0);
        Content = content; FlushBindings(); foreach (var chart in charts) _ = chart.GetMarks();
        Check("reattach-restores-business-subscriptions", Records().All(record => record.SubscriberCount > 0) && ReferenceEquals(EngineeringSmith.ItemsSource, Model.EngineeringSmithRecords));
        using (var drawing = new DrawingGroup().Open()) foreach (var chart in charts) chart.Render(drawing);
        EngineeringBindingDiagnostics.Add(new { kinds = charts.Select(chart => chart.GetType().Name), markCounts = charts.Select(chart => chart.GetMarks().Count),
            sharedHexSourceIndices = sharedHex.Members.Select(member => member.SourceIndex).ToArray(), smithPhysical = physical, composition, carpetInverse = parameter,
            windFrequency = EngineeringWind.Rose.TotalFrequency, physicalInputValidated = false, nativePixelsValidated = false, performanceMeasured = false });
        Model.EngineeringCarpetRecords[4].Temperature = oldTemperature; Model.EngineeringHexRecords[0].Load = oldLoad;
        Model.EngineeringSmithRecords[1].Resistance = oldResistance; Model.EngineeringMixtureRecords[0].Sand = oldSand; Model.EngineeringWindRecords[0].Frequency = oldFrequency;
        expander.IsExpanded = false; FlushBindings();
    }
}
