using Avalonia.Controls;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal CircularGaugeChart CircularGauge => this.FindControl<CircularGaugeChart>("UsageCircularGauge")!;
    internal SparklineChart MicroLine => this.FindControl<SparklineChart>("UsageMicroLine")!;
    internal List<object> GaugeMicroBindingDiagnostics { get; } = [];

    internal void VerifyGaugeMicroBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("GaugeMicroExamples")!; expander.IsExpanded = true; FlushBindings();
        GaugeBase[] gauges = [CircularGauge, this.FindControl<GaugeChart>("UsageSemiGauge")!, this.FindControl<GradientRingChart>("UsageGradientRing")!, this.FindControl<LinearGaugeChart>("UsageLinearGauge")!, this.FindControl<LiquidFillGaugeChart>("UsageLiquidGauge")!, this.FindControl<ProgressDonutChart>("UsageProgressDonut")!];
        SparklineChart[] micros = [MicroLine, this.FindControl<SparklineChart>("UsageMicroArea")!, this.FindControl<SparklineChart>("UsageMicroBar")!, this.FindControl<SparklineChart>("UsageMicroWinLoss")!];
        var linear = (LinearGaugeChart)gauges[3]; var liquid = (LiquidFillGaugeChart)gauges[4];
        void Check(string name, bool value) => checks["gauge-micro-compiled-" + name] = value;
        static bool Near(double? actual, double expected) => actual is { } number && double.IsFinite(number) && Math.Abs(number - expected) < 1e-8;
        Check("six-gauges-and-four-micro-types-have-real-layout", gauges.All(value => value.Bounds.Width > 100 && value.Bounds.Height > 100) && micros.All(value => value.PlotArea.Width > 100 && value.PlotArea.Height > 50) && micros.Select(value => value.SparklineType).SequenceEqual(Enum.GetValues<SparklineType>()));
        Check("gauge-value-domain-target-compiled-binding", gauges.All(value => value.Value == 68 && value.Minimum == 0 && value.Maximum == 100 && value.TargetValue == 80 && Near(value.NormalizedValue, .68)));
        Check("ranges-and-ring-collection-bind-original-source", ReferenceEquals(CircularGauge.RangesSource, Model.GaugeRanges) && ReferenceEquals(((GradientRingChart)gauges[2]).ValuesSource, Model.GaugeRings));
        Check("micro-sources-paths-and-series-identity", micros.All(value => ReferenceEquals(value.ItemsSource, Model.MicroRecords) && value.ValuePath == "Value" && value.CategoryPath == "Label" && value.IdPath == "Id" && ReferenceEquals(value.Series.ItemsSource, Model.MicroRecords)));
        var initial = MicroLine.GetPoints(); var second = initial.Single(value => value.Data.SourceIndex == 1);
        Check("micro-points-retain-business-item-id-and-source-order", initial.Count == 7 && ReferenceEquals(second.Data.Item, Model.MicroRecords[1]) && Equals(second.Data.StableId, "micro-b") && second.Data.Point.Y == 55 && ReferenceEquals(second.Hit.Series, MicroLine.Series));
        var pointHit = MicroLine.HitTest(second.Position, 1);
        Check("micro-hit-test-retains-business-identity", pointHit is { Index: 1 } && ReferenceEquals(pointHit.Item, Model.MicroRecords[1]) && Equals(pointHit.StableId, "micro-b"));
        Model.GaugeMinimum = 20; Model.GaugeMaximum = 120; Model.GaugeValue = 90; Model.GaugeTarget = 110; FlushBindings();
        Check("viewmodel-updates-all-gauge-domains-and-targets", gauges.All(value => value.Minimum == 20 && value.Maximum == 120 && value.Value == 90 && value.TargetValue == 110 && Near(value.NormalizedValue, .7) && value.NormalizedTarget is { } target && Near(target, .9)));
        Model.GaugeValue = 180; FlushBindings();
        var upperDomainBindingSnapshot = new { requestedValue = 180d, modelValue = Model.GaugeValue, sliderValue = this.FindControl<Slider>("UsageGaugeValue")!.Value, sliderMinimum = this.FindControl<Slider>("UsageGaugeValue")!.Minimum, sliderMaximum = this.FindControl<Slider>("UsageGaugeValue")!.Maximum, gauges = gauges.Select(value => new { kind = value.GetType().Name, value.Value, value.Minimum, value.Maximum, value.NormalizedValue }).ToArray() };
        Check("out-of-domain-gauge-value-preserves-raw-and-clamps-display", gauges.All(value => value.Value == 180 && Near(value.NormalizedValue, 1)));
        Model.GaugeValue = -20; FlushBindings(); Check("below-domain-gauge-value-preserves-raw-and-clamps-display", gauges.All(value => value.Value == -20 && Near(value.NormalizedValue, 0)));
        Model.GaugeTarget = null; FlushBindings(); Check("nullable-target-binding-clears-target", gauges.All(value => value.TargetValue is null && value.NormalizedTarget is null));
        Model.GaugeOrientation = Orientation.Vertical; Model.GaugeLiquidShape = GaugeLiquidShape.Rectangle; Model.GaugeWave = true; Model.GaugeReduceMotion = true; Model.GaugePaused = true; FlushBindings();
        Check("viewmodel-controls-orientation-liquid-shape-and-motion", linear.Orientation == Orientation.Vertical && liquid.Shape == GaugeLiquidShape.Rectangle && liquid.IsWaveAnimationEnabled && liquid.ReduceMotion && liquid.IsPaused);
        this.FindControl<ComboBox>("UsageGaugeOrientation")!.SelectedItem = Orientation.Horizontal; this.FindControl<ComboBox>("UsageGaugeLiquidShape")!.SelectedItem = GaugeLiquidShape.Circle;
        this.FindControl<CheckBox>("UsageGaugeWave")!.IsChecked = false; this.FindControl<CheckBox>("UsageGaugeReduceMotion")!.IsChecked = false; this.FindControl<CheckBox>("UsageGaugePaused")!.IsChecked = false; FlushBindings();
        Check("motion-and-orientation-inputs-are-two-way", Model.GaugeOrientation == Orientation.Horizontal && Model.GaugeLiquidShape == GaugeLiquidShape.Circle && !Model.GaugeWave && !Model.GaugeReduceMotion && !Model.GaugePaused && !liquid.IsWaveAnimationEnabled);
        var version = MicroLine.Series.SourceVersion; Model.MicroRecords[1].Value = -35; FlushBindings(); var changed = MicroLine.GetPoints().Single(value => value.Data.SourceIndex == 1);
        Check("business-inpc-refreshes-all-four-micro-projections", MicroLine.Series.SourceVersion > version && micros.All(value => value.GetPoints().Single(point => point.Data.SourceIndex == 1).Data.Point.Y == -35) && ReferenceEquals(changed.Data.Item, Model.MicroRecords[1]) && Equals(changed.Data.StableId, "micro-b"));
        var added = new MicroUsageRecord("micro-added", "H", 65); Model.MicroRecords.Add(added); FlushBindings();
        Check("collection-add-observes-new-real-business-item", micros.All(value => value.GetPoints().Count == 8 && ReferenceEquals(value.GetPoints()[7].Data.Item, added)) && added.SubscriberCount > 0);
        added.Value = double.NaN; FlushBindings(); Check("invalid-value-retains-slot-and-original-source", micros.All(value => value.GetPoints().Count == 8 && !value.GetPoints()[7].IsVisible && ReferenceEquals(value.GetPoints()[7].Data.Item, added)));
        Model.MicroRecords.Remove(added); FlushBindings(); Check("collection-remove-releases-item-observer", added.SubscriberCount == 0 && micros.All(value => value.GetPoints().Count == 7));
        Model.GaugeRanges[0].End = 30; Model.GaugeRings[0].Value = 88; Model.GaugeRanges.Add(new() { Start = 95, End = 100, Brush = Avalonia.Media.Brushes.Purple }); Model.GaugeRings.Add(new() { Value = 35, Label = "新增", Brush = Avalonia.Media.Brushes.Purple }); FlushBindings();
        Check("range-and-ring-inpc-and-collection-sources-stay-bound", ReferenceEquals(CircularGauge.RangesSource, Model.GaugeRanges) && Model.GaugeRanges.Count == 4 && Model.GaugeRanges[0].End == 30 && ReferenceEquals(((GradientRingChart)gauges[2]).ValuesSource, Model.GaugeRings) && Model.GaugeRings.Count == 4 && Model.GaugeRings[0].Value == 88);
        Check("attached-collection-and-item-observers-exist", Model.GaugeRanges.SubscriberCount > 0 && Model.GaugeRings.SubscriberCount > 0 && Model.MicroRecords.SubscriberCount > 0 && Model.MicroRecords.All(value => value.SubscriberCount > 0));
        var content = Content; Content = null; FlushBindings();
        Check("detach-releases-business-collection-and-item-observers", Model.GaugeRanges.SubscriberCount == 0 && Model.GaugeRings.SubscriberCount == 0 && Model.MicroRecords.SubscriberCount == 0 && Model.MicroRecords.All(value => value.SubscriberCount == 0));
        Content = content; FlushBindings(); foreach (var chart in micros) _ = chart.GetPoints();
        Check("reattach-restores-business-observers-without-losing-source", Model.GaugeRanges.SubscriberCount > 0 && Model.GaugeRings.SubscriberCount > 0 && Model.MicroRecords.SubscriberCount > 0 && Model.MicroRecords.All(value => value.SubscriberCount > 0) && ReferenceEquals(MicroLine.ItemsSource, Model.MicroRecords));
        GaugeMicroBindingDiagnostics.Add(new { upperDomainBindingSnapshot, initial = initial.Select(value => new { value.Data.SourceIndex, value.Data.StableId, rawValue = value.Data.Point.Y, value.Position }), changed = new { changed.Data.SourceIndex, changed.Data.StableId, rawValue = changed.Data.Point.Y }, physicalInputValidated = false, performanceMeasured = false, absoluteTolerance = 1e-8 });
        Model.GaugeMinimum = 0; Model.GaugeMaximum = 100; Model.GaugeValue = 68; Model.GaugeTarget = 80; Model.MicroRecords[1].Value = 55; Model.GaugeRanges[0].End = 40; Model.GaugeRings[0].Value = 72; Model.GaugeRanges.RemoveAt(3); Model.GaugeRings.RemoveAt(3);
        expander.IsExpanded = false; FlushBindings();
    }
}
