using Avalonia.Controls;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    private static readonly string[] C5CompletionNames = ["Beeswarm", "Strip", "Violin", "Ridge", "Contour", "Mosaic", "Parallel"];
    internal EngineeringChartBase[] C5CompletionCharts => C5CompletionNames.Select(name => this.FindControl<EngineeringChartBase>("UsageC5" + name)!).ToArray();
    internal List<object> C5CompletionDiagnostics { get; } = [];

    internal void VerifyC5CompletionBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("C5CompletionExamples")!; expander.IsExpanded = true; FlushBindings();
        var charts = C5CompletionCharts;
        T Chart<T>(string name) where T : EngineeringChartBase => this.FindControl<T>("UsageC5" + name)!;
        void Check(string name, bool value) => checks["c5-completion-compiled-" + name] = value;
        void Capture(bool allowParallelDimensionError = false)
        {
            FlushBindings(); foreach (var chart in charts) _ = chart.GetMarks(); FlushBindings();
            foreach (var chart in charts)
                if (chart.DataError is { } error && !(allowParallelDimensionError && chart is ParallelCoordinatesChart))
                    throw new InvalidOperationException($"Unexpected {chart.GetType().Name} data error in the compiled C5 binding fixture: {error.Message}", error);
        }
        Capture();
        foreach (var (chart, name) in charts.Zip(C5CompletionNames))
            Check(name + "-arranged-original-scene", chart.DataError is null && chart.PlotArea.Width > 100 && chart.GetMarks().Count > 0 && chart.GetMarks().All(mark => mark.Members.Count > 0 && mark.Members.All(member => member.Item is not null && member.SourceIndex >= 0)));
        Check("seven-distinct-compiled-controls", charts.Length == 7 && charts.Distinct().Count() == 7);
        var swarm = Chart<BeeswarmChart>("Beeswarm"); var strip = Chart<StripPlot>("Strip"); var violin = Chart<ViolinPlot>("Violin"); var ridge = Chart<RidgelineChart>("Ridge"); var contour = Chart<ContourPlot>("Contour"); var mosaic = Chart<MosaicChart>("Mosaic"); var parallel = Chart<ParallelCoordinatesChart>("Parallel");
        Check("categorical-custom-business-paths", swarm.ValuePath == "Amount" && strip.CategoryPath == "Group" && swarm.LabelPath == "Name" && swarm.GetMarks().All(mark => ReferenceEquals(mark.Members[0].Item, Model.C5Samples[mark.Members[0].SourceIndex]) && mark.Value == Model.C5Samples[mark.Members[0].SourceIndex].Amount));
        Check("violin-custom-sample-path-originals", violin.SamplesPath == "Observations" && violin.Layout!.Groups.All(group => ReferenceEquals(group.SourceItem, Model.C5Profiles[group.SourceIndex])) && violin.GetMarks().All(mark => ReferenceEquals(mark.Members[0].Item, Model.C5Profiles[mark.Members[0].SourceIndex])));
        Check("ridge-typed-samples-originals", ridge.Layout!.Groups.All(group => ReferenceEquals(group.SourceItem, Model.C5Ridges[group.SourceIndex])) && ridge.GetMarks().All(mark => ReferenceEquals(mark.Members[0].Item, Model.C5Ridges[mark.Members[0].SourceIndex])));
        Check("contour-custom-grid-corner-members", contour.XPath == "East" && contour.YPath == "North" && contour.ValuePath == "Reading" && ReferenceEquals(contour.Levels, Model.C5Levels) && contour.GetMarks().All(mark => mark.Members.All(member => ReferenceEquals(member.Item, Model.C5Grid[member.SourceIndex]) && member.Value is double raw && raw == Model.C5Grid[member.SourceIndex].Reading)));
        Check("mosaic-duplicate-pair-aggregates-keep-all-raw-rows", mosaic.Layout!.Cells.Count == 4 && mosaic.Layout.Cells[0].SourceIndices.SequenceEqual(new[] { 0, 1 }) && mosaic.Layout.Cells[0].Value == 3 && mosaic.GetMarks().All(mark => mark.Members.All(member => ReferenceEquals(member.Item, Model.C5Mosaic[member.SourceIndex]))));
        Check("parallel-typed-values-and-axis-binding", ReferenceEquals(parallel.Axes, Model.C5ParallelAxes) && parallel.Layout!.Dimensions.Count == 3 && parallel.Layout.Records.Count == Model.C5Parallel.Count && parallel.GetMarks().All(mark => ReferenceEquals(mark.Members[0].Item, Model.C5Parallel[mark.Members[0].SourceIndex])));

        var selectionBindings = new (string Name, EngineeringChartBase Chart, Func<int> Read, Action<int> Write)[]
        {
            ("Beeswarm", swarm, () => Model.C5SwarmSelected, value => Model.C5SwarmSelected = value),
            ("Strip", strip, () => Model.C5StripSelected, value => Model.C5StripSelected = value),
            ("Violin", violin, () => Model.C5ViolinSelected, value => Model.C5ViolinSelected = value),
            ("Ridge", ridge, () => Model.C5RidgeSelected, value => Model.C5RidgeSelected = value),
            ("Contour", contour, () => Model.C5ContourSelected, value => Model.C5ContourSelected = value),
            ("Mosaic", mosaic, () => Model.C5MosaicSelected, value => Model.C5MosaicSelected = value),
            ("Parallel", parallel, () => Model.C5ParallelSelected, value => Model.C5ParallelSelected = value),
        };
        foreach (var binding in selectionBindings)
        {
            binding.Chart.SetCurrentValue(EngineeringChartBase.SelectedIndexProperty, 0); Capture();
            Check(binding.Name + "-two-way-chart-to-model", binding.Read() == 0 && binding.Chart.SelectedPoint is not null);
            binding.Write(1); Capture(); Check(binding.Name + "-two-way-model-to-chart", binding.Chart.SelectedIndex == 1 && binding.Chart.SelectedPoint?.Index == 1);
            binding.Write(-1); Capture();
        }
        Model.C5SwarmSelected = 1; var chosen = Model.C5Samples[1]; Capture(); Model.C5Samples.Move(1, 0); Capture();
        Check("categorical-bound-selection-follows-original-reorder", ReferenceEquals(swarm.SelectedPoint?.Members[0].Item, chosen) && swarm.SelectedPoint!.Members[0].SourceIndex == 0 && Model.C5SwarmSelected == 0);
        Model.C5Samples.Move(0, 1); Model.C5SwarmSelected = -1; Capture();
        var valueBefore = Model.C5Samples[0].Amount; var groupBefore = Model.C5Samples[0].Group; var nameBefore = Model.C5Samples[0].Name;
        Model.C5Samples[0].Amount += 1; Model.C5Samples[0].Group = "C"; Model.C5Samples[0].Name = "Changed business label"; Capture();
        Check("categorical-inpc-value-category-label", swarm.GetMarks()[0].Value == valueBefore + 1 && strip.Layout!.Categories.Count == 3 && swarm.GetMarks()[0].TooltipText!.Contains("Changed business label", StringComparison.Ordinal));
        Model.C5Samples[0].Amount = valueBefore; Model.C5Samples[0].Group = groupBefore; Model.C5Samples[0].Name = nameBefore;
        var extra = new C5CategoryRecord("cat-extra", "B", 9, "Added"); Model.C5Samples.Add(extra); Capture();
        Check("categorical-incc-add-original", swarm.GetMarks().Count == 6 && strip.GetMarks().Count == 6 && extra.SubscriberCount > 0 && ReferenceEquals(swarm.GetMarks()[5].Members[0].Item, extra));
        Model.C5Samples.Remove(extra); Capture(); Check("categorical-removed-row-released", extra.SubscriberCount == 0 && swarm.GetMarks().Count == 5);
        var jitterBefore = Model.C5Jitter; var radiusBefore = Model.C5PointRadius; var bandwidthBefore = Model.C5Bandwidth;
        Model.C5Jitter = .12; Model.C5PointRadius = 6; Model.C5Bandwidth = .8; Model.C5Labels = false; Model.C5Mean = false; Capture();
        Check("viewmodel-styled-properties-real-bindings", swarm.Jitter == .12 && strip.JitterAmount == .12 && swarm.MarkerSize == 12 && strip.PointRadius == 6 && violin.Bandwidth == .8 && ridge.Bandwidth == .8 && !swarm.ShowCategoryLabels && !strip.ShowMeanLine && !violin.ShowLabels && !mosaic.ShowLabels);
        Model.C5Jitter = jitterBefore; Model.C5PointRadius = radiusBefore; Model.C5Bandwidth = bandwidthBefore; Model.C5Labels = true; Model.C5Mean = true; Capture();
        Model.C5Jitter = 0; Capture();
        foreach (var (chart, name) in new (CategoricalDistributionChartBase Chart, string Name)[] { (swarm, "Beeswarm"), (strip, "Strip") })
        {
            var position = chart.GetMarks().Single(mark => mark.Members[0].SourceIndex == 1).Position;
            Check(name + "-bound-zero-jitter-overlapping-originals", chart.HitTest(position)?.Members[0].SourceIndex == 2 && chart.HitTestAll(position).Select(hit => hit.Members[0].SourceIndex).SequenceEqual(new[] { 2, 1 }) && chart.HitTestAll(position).All(hit => ReferenceEquals(hit.Members[0].Item, Model.C5Samples[hit.Members[0].SourceIndex])));
        }
        Model.C5Jitter = jitterBefore; Capture();
        var originalSamples = Model.C5Profiles[0].Observations; var sampleCount = originalSamples.Count;
        originalSamples.Add(6); Capture(); Check("violin-nested-samples-incc", violin.Layout!.Groups[0].Observations.Count == sampleCount + 1 && violin.Layout.Groups[0].Observations[^1].OriginalIndex == sampleCount); originalSamples.RemoveAt(sampleCount);
        var nestedReplacement = new EngineeringObserverCollection<double>([2, 4, 6]); Model.C5Profiles[0].Observations = nestedReplacement; Capture();
        Check("violin-nested-source-property-replacement", violin.Layout!.Groups[0].Observations.Count == 3 && originalSamples.SubscriberCount == 0 && nestedReplacement.SubscriberCount > 0);
        Model.C5Profiles[0].Observations = originalSamples; Capture(); Check("violin-retired-nested-source-released", nestedReplacement.SubscriberCount == 0);
        var typedSamples = (EngineeringObserverCollection<double>)Model.C5Ridges[0].Samples; var typedCount = typedSamples.Count; typedSamples.Add(8); Capture();
        Check("ridge-typed-nested-incc-original-indices", ridge.Layout!.Groups[0].Observations.Count == typedCount + 1 && ridge.Layout.Groups[0].Observations[^1].OriginalIndex == typedCount); typedSamples.RemoveAt(typedCount); Capture();
        var scalar = Model.C5Grid[0]; var readingBefore = scalar.Reading; scalar.Reading += .25; Capture();
        Check("contour-grid-dto-inpc-original-value", contour.Contours!.Grid[0].Value == readingBefore + .25 && contour.GetMarks().SelectMany(mark => mark.Members).Any(member => ReferenceEquals(member.Item, scalar) && member.Value is double raw && raw == readingBefore + .25)); scalar.Reading = readingBefore;
        Model.C5Levels.Insert(2, 2.5); Capture(); Check("contour-bound-levels-incc", contour.Contours!.Levels.SequenceEqual(new[] { 1d, 2, 2.5, 3 })); Model.C5Levels.Remove(2.5); Capture();
        var originalLevels = Model.C5Levels; var replacementLevels = new EngineeringObserverCollection<double>([.5, 1.5, 2.5]); Model.C5Levels = replacementLevels; Capture();
        Check("contour-levels-binding-replacement-releases", ReferenceEquals(contour.Levels, replacementLevels) && originalLevels.SubscriberCount == 0 && replacementLevels.SubscriberCount > 0 && contour.Contours!.Levels.SequenceEqual(replacementLevels));
        Model.C5Levels = originalLevels; Capture(); Check("contour-retired-levels-released", replacementLevels.SubscriberCount == 0);
        var parallelValues = (EngineeringObserverCollection<double>)Model.C5Parallel[0].Values; var parallelBefore = parallelValues[0]; parallelValues[0] += 1; Capture();
        Check("parallel-typed-values-incc", parallel.Layout!.Records[0].Coordinates.Single(point => point.DimensionIndex == 0).Value == parallelBefore + 1); parallelValues[0] = parallelBefore;
        var reverseBefore = parallel.Layout!.Records[0].Coordinates.Single(point => point.DimensionIndex == 1).Position.Y; Model.C5ParallelAxes[1].IsReversed = true; Capture();
        Check("parallel-bound-axis-inpc", Math.Abs(parallel.Layout!.Records[0].Coordinates.Single(point => point.DimensionIndex == 1).Position.Y - (1 - reverseBefore)) < 1e-9 && parallel.Layout.Dimensions[1].IsReversed); Model.C5ParallelAxes[1].IsReversed = false;
        var omittedBeforeAxis = parallel.Layout!.OmittedValues;
        Model.C5ParallelAxes.Add(new() { Id = "missing", Header = "Missing dimension" }); Capture(allowParallelDimensionError: true);
        Check("parallel-bound-axis-mismatch-clears-frame", parallel.DataError is ArgumentException && parallel.Layout is null && parallel.GetMarks().Count == 0);
        foreach (var row in Model.C5Parallel) ((EngineeringObserverCollection<double>)row.Values).Add(double.NaN);
        Capture();
        Check("parallel-bound-axes-incc", parallel.DataError is null && parallel.Layout!.Dimensions.Count == 4 && parallel.Layout.OmittedValues == omittedBeforeAxis + Model.C5Parallel.Count && parallel.Layout.Records.All(row => row.Coordinates.All(point => point.DimensionIndex != 3)));
        Model.C5ParallelAxes.RemoveAt(3);
        foreach (var row in Model.C5Parallel) ((EngineeringObserverCollection<double>)row.Values).RemoveAt(3);
        Capture(); Check("parallel-axis-and-values-restore-together", parallel.DataError is null && parallel.Layout!.Dimensions.Count == 3 && parallel.Layout.OmittedValues == omittedBeforeAxis && parallel.GetMarks().Count == Model.C5Parallel.Count);
        var originalMosaicRow = Model.C5Mosaic[0]; Model.C5Mosaic[0] = originalMosaicRow with { Value = 5 }; Capture();
        Check("mosaic-collection-replace-recomputes-aggregate", mosaic.Layout!.Cells[0].Value == 6 && mosaic.GetMarks().SelectMany(mark => mark.Members).Any(member => ReferenceEquals(member.Item, Model.C5Mosaic[0]))); Model.C5Mosaic[0] = originalMosaicRow; Capture();

        var oldCategorical = Model.C5Samples; var newCategorical = new EngineeringObserverCollection<C5CategoryRecord>([new("cat-new", "New", 3, "Replacement")]); Model.C5Samples = newCategorical; Capture();
        Check("categorical-bound-source-replacement-releases", oldCategorical.SubscriberCount == 0 && oldCategorical.All(row => row.SubscriberCount == 0) && ReferenceEquals(swarm.GetMarks()[0].Members[0].Item, newCategorical[0]) && ReferenceEquals(strip.GetMarks()[0].Members[0].Item, newCategorical[0]));
        Model.C5Samples = oldCategorical; Capture(); Check("categorical-retired-source-released", newCategorical.SubscriberCount == 0 && newCategorical[0].SubscriberCount == 0);
        var oldProfiles = Model.C5Profiles; var newProfiles = new EngineeringObserverCollection<C5ProfileRecord>([new("dist-new", "Replacement", [2, 3, 4])]); Model.C5Profiles = newProfiles; Capture();
        Check("violin-bound-source-replacement-releases", oldProfiles.SubscriberCount == 0 && oldProfiles.All(row => row.SubscriberCount == 0 && row.Observations.SubscriberCount == 0) && ReferenceEquals(violin.GetMarks()[0].Members[0].Item, newProfiles[0])); Model.C5Profiles = oldProfiles; Capture();
        Check("violin-retired-source-released", newProfiles.SubscriberCount == 0 && newProfiles[0].SubscriberCount == 0 && newProfiles[0].Observations.SubscriberCount == 0);
        var oldRidges = Model.C5Ridges; var newRidges = new EngineeringObserverCollection<DistributionSamples>([new("ridge-new", "Replacement", new EngineeringObserverCollection<double>([2, 3, 4]))]); Model.C5Ridges = newRidges; Capture();
        Check("ridge-bound-source-replacement-releases", oldRidges.SubscriberCount == 0 && oldRidges.All(row => ((EngineeringObserverCollection<double>)row.Samples).SubscriberCount == 0) && ReferenceEquals(ridge.GetMarks()[0].Members[0].Item, newRidges[0])); Model.C5Ridges = oldRidges; Capture();
        Check("ridge-retired-source-released", newRidges.SubscriberCount == 0 && ((EngineeringObserverCollection<double>)newRidges[0].Samples).SubscriberCount == 0);
        var oldGrid = Model.C5Grid; Model.C5Grid = new(oldGrid.Select(point => new C5ScalarRecord(point.Id + "-new", point.East, point.North, point.Reading))); Capture(); var newGrid = Model.C5Grid;
        Check("contour-bound-source-replacement-releases", oldGrid.SubscriberCount == 0 && oldGrid.All(row => row.SubscriberCount == 0) && contour.GetMarks().SelectMany(mark => mark.Members).All(member => newGrid.Any(row => ReferenceEquals(row, member.Item)))); Model.C5Grid = oldGrid; Capture(); Check("contour-retired-source-released", newGrid.SubscriberCount == 0 && newGrid.All(row => row.SubscriberCount == 0));
        var oldMosaic = Model.C5Mosaic; Model.C5Mosaic = new([new("m-new-a", "A", "X", 2), new("m-new-b", "B", "Y", 3)]); Capture(); var newMosaic = Model.C5Mosaic;
        Check("mosaic-bound-source-replacement-releases", oldMosaic.SubscriberCount == 0 && mosaic.GetMarks().SelectMany(mark => mark.Members).All(member => newMosaic.Any(row => ReferenceEquals(row, member.Item)))); Model.C5Mosaic = oldMosaic; Capture(); Check("mosaic-retired-source-released", newMosaic.SubscriberCount == 0);
        var oldParallel = Model.C5Parallel; Model.C5Parallel = new([new("p-new-a", new EngineeringObserverCollection<double>([1, 2, 3]), "New A"), new("p-new-b", new EngineeringObserverCollection<double>([3, 2, 1]), "New B")]); Capture(); var newParallel = Model.C5Parallel;
        Check("parallel-bound-source-replacement-releases", oldParallel.SubscriberCount == 0 && oldParallel.All(row => ((EngineeringObserverCollection<double>)row.Values).SubscriberCount == 0) && parallel.GetMarks().All(mark => newParallel.Any(row => ReferenceEquals(row, mark.Members[0].Item)))); Model.C5Parallel = oldParallel; Capture();
        Check("parallel-retired-source-released", newParallel.SubscriberCount == 0 && newParallel.All(row => ((EngineeringObserverCollection<double>)row.Values).SubscriberCount == 0));
        var commandAmount = Model.C5Samples[0].Amount; var commandCount = Model.C5Profiles[0].Observations.Count; var commandReading = Model.C5Grid[0].Reading; var commandParallel = ((EngineeringObserverCollection<double>)Model.C5Parallel[0].Values)[0]; Model.C5CompletionUpdateCommand.Execute(null); Capture();
        Check("bound-command-updates-four-data-contracts", swarm.GetMarks()[0].Value == commandAmount + 1 && violin.Layout!.Groups[0].Observations.Count == commandCount + 1 && contour.Contours!.Grid[0].Value == commandReading + .25 && parallel.Layout!.Records[0].Coordinates[0].Value == commandParallel + 1);
        Model.C5Samples[0].Amount = commandAmount; Model.C5Profiles[0].Observations.RemoveAt(commandCount); Model.C5Grid[0].Reading = commandReading; ((EngineeringObserverCollection<double>)Model.C5Parallel[0].Values)[0] = commandParallel; Capture();
        var content = Content; Content = null; FlushBindings();
        Check("detach-all-c5-source-observers", Model.C5Samples.SubscriberCount == 0 && Model.C5Samples.All(row => row.SubscriberCount == 0) && Model.C5Profiles.SubscriberCount == 0 && Model.C5Profiles.All(row => row.SubscriberCount == 0 && row.Observations.SubscriberCount == 0) && Model.C5Ridges.SubscriberCount == 0 && Model.C5Ridges.All(row => ((EngineeringObserverCollection<double>)row.Samples).SubscriberCount == 0) && Model.C5Grid.SubscriberCount == 0 && Model.C5Grid.All(row => row.SubscriberCount == 0) && Model.C5Levels.SubscriberCount == 0 && Model.C5Mosaic.SubscriberCount == 0 && Model.C5Parallel.SubscriberCount == 0 && Model.C5Parallel.All(row => ((EngineeringObserverCollection<double>)row.Values).SubscriberCount == 0) && ((EngineeringObserverCollection<ParallelAxis>)Model.C5ParallelAxes).SubscriberCount == 0);
        Content = content; Capture(); Check("reattach-all-seven-bound-scenes", charts.All(chart => chart.DataError is null && chart.GetMarks().Count > 0) && Model.C5Samples[0].SubscriberCount > 0 && Model.C5Profiles[0].Observations.SubscriberCount > 0 && Model.C5Levels.SubscriberCount > 0 && ((EngineeringObserverCollection<ParallelAxis>)Model.C5ParallelAxes).SubscriberCount > 0);
        C5CompletionDiagnostics.Add(new { kinds = C5CompletionNames, types = charts.Select(chart => chart.GetType().Name).ToArray(), markCounts = charts.Select(chart => chart.GetMarks().Count).ToArray(), contourLevels = contour.Contours!.Levels, parallelAxisIds = parallel.Axes.Select(axis => axis.Id).ToArray(), categoricalIds = Model.C5Samples.Select(row => row.Id).ToArray(), physicalInputValidated = false, performanceMeasured = false });
        expander.IsExpanded = false; FlushBindings();
    }
}
