using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal DivergingBarChart AdvancedDiverging => this.FindControl<DivergingBarChart>("UsageAdvancedDiverging")!;
    internal DumbbellChart AdvancedDumbbell => this.FindControl<DumbbellChart>("UsageAdvancedDumbbell")!;
    internal MekkoChart AdvancedMekko => this.FindControl<MekkoChart>("UsageAdvancedMekko")!;
    internal MirrorBarChart AdvancedMirror => this.FindControl<MirrorBarChart>("UsageAdvancedMirror")!;
    internal PopulationPyramidChart AdvancedPopulation => this.FindControl<PopulationPyramidChart>("UsageAdvancedPopulation")!;
    internal TornadoChart AdvancedTornado => this.FindControl<TornadoChart>("UsageAdvancedTornado")!;
    internal VennDiagramChart AdvancedVenn => this.FindControl<VennDiagramChart>("UsageAdvancedVenn")!;
    internal List<object> AdvancedComparisonBindingDiagnostics { get; } = [];

    internal void VerifyAdvancedComparisonBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("AdvancedComparisonExamples")!;
        expander.IsExpanded = true; FlushBindings();
        ComparisonChartBase[] charts = [AdvancedDiverging, AdvancedDumbbell, AdvancedMekko, AdvancedMirror, AdvancedPopulation, AdvancedTornado, AdvancedVenn];
        void Check(string key, bool value) => checks["advanced-comparison-compiled-" + key] = value;
        static bool Near(double expected, double actual) => double.IsFinite(actual) && Math.Abs(expected - actual) < 1e-8;
        IEnumerable<AdvancedBusinessRecord> Records() => Model.AdvancedSignedRecords.Cast<AdvancedBusinessRecord>().Concat(Model.AdvancedPairRecords)
            .Concat(Model.AdvancedMekkoRecords).Concat(Model.AdvancedMekkoRecords.SelectMany(column => column.Parts)).Concat(Model.AdvancedVennRecords);
        Check("seven-controls-arranged-with-valid-data", charts.All(chart => chart.PlotArea.Width > 100 && chart.PlotArea.Height > 100 && chart.DataError is null) &&
            charts.Select(chart => chart.GetMarks().Count).SequenceEqual(new[] { 3, 6, 4, 6, 6, 6, 3 }));
        Check("business-sources-and-paths-bound", ReferenceEquals(AdvancedDiverging.ItemsSource, Model.AdvancedSignedRecords) && AdvancedDiverging.ValuePath == "Amount" &&
            ReferenceEquals(AdvancedDumbbell.ItemsSource, Model.AdvancedPairRecords) && AdvancedDumbbell.LowValuePath == "Before" && AdvancedDumbbell.HighValuePath == "After" &&
            ReferenceEquals(AdvancedMekko.ItemsSource, Model.AdvancedMekkoRecords) && AdvancedMekko.SegmentsPath == "Parts" && AdvancedMekko.SegmentIdPath == "Id" &&
            AdvancedPopulation.MaleValuePath == "Before" && AdvancedPopulation.FemaleValuePath == "After" && AdvancedVenn.SetsPath == "Sets" && AdvancedVenn.ValuePath == "Amount");
        var signed = AdvancedDiverging.GetMarks()[0];
        Check("diverging-retains-signed-value-and-centered-baseline", signed.Value == -12 && signed.Members[0].SourceIndex == 0 &&
            ReferenceEquals(signed.Members[0].Item, Model.AdvancedSignedRecords[0]) && Equals(signed.Members[0].StableId, "retail") &&
            Near(AdvancedDiverging.PlotArea.Center.X, signed.Bounds.Right));
        var endpoints = AdvancedDumbbell.GetMarks();
        Check("dumbbell-endpoints-have-distinct-side-key-and-original-row", Equals(endpoints[0].Key, "low") && Equals(endpoints[1].Key, "high") &&
            endpoints[0].Value == 18 && endpoints[1].Value == 28 && ReferenceEquals(endpoints[1].Members[0].Item, Model.AdvancedPairRecords[0]) && endpoints[1].Members[0].SourceIndex == 0);
        var originalSegments = AdvancedMekko.GetMarks(); var paid = Model.AdvancedMekkoRecords[0].Parts[1];
        Check("mekko-members-are-real-parent-and-child-with-original-indices", originalSegments[1].Members.Count == 2 &&
            ReferenceEquals(originalSegments[1].Members[0].Item, Model.AdvancedMekkoRecords[0]) && ReferenceEquals(originalSegments[1].Members[1].Item, paid) &&
            originalSegments[1].Members[0].SourceIndex == 0 && originalSegments[1].Members[1].SourceIndex == 1 && Equals(originalSegments[1].Members[1].StableId, "a-paid") &&
            originalSegments[1].Value == 30 && Near(.6, originalSegments[1].Members[1].Weight ?? double.NaN));
        var vennMarks = AdvancedVenn.GetMarks();
        Check("venn-inclusive-input-produces-exclusive-regions-and-summary-dependencies", AdvancedVenn.Layout is { IsApproximate: false } layout &&
            Near(40, layout.Regions[0].Value) && Near(20, layout.Regions[1].Value) && Near(20, layout.Regions[2].Value) &&
            vennMarks.Single(mark => Equals(mark.Key, 1)).Members.Select(member => member.SourceIndex).SequenceEqual(new[] { 0, 2 }) &&
            vennMarks.All(mark => mark.Members.All(member => ReferenceEquals(member.Item, Model.AdvancedVennRecords[member.SourceIndex]))));
        Check("all-glyph-hit-probes-return-same-original-members", charts.All(chart => chart.GetMarks().All(mark =>
            chart.HitTest(mark.Position) is { } hit && Equals(hit.Mark.Key, mark.Key) && hit.Members.Select(member => member.Item).SequenceEqual(mark.Members.Select(member => member.Item)))));
        AdvancedDumbbell.SelectedIndex = 1; FlushBindings();
        Check("endpoint-selection-flows-to-viewmodel", Model.AdvancedSelectedIndex == 1 && Equals(AdvancedDumbbell.SelectedPoint?.Mark.Key, "high") &&
            ReferenceEquals(AdvancedDumbbell.SelectedPoint?.Members[0].Item, Model.AdvancedPairRecords[0]));
        Model.AdvancedSelectedIndex = 3; FlushBindings();
        Check("viewmodel-selection-flows-to-endpoint", AdvancedDumbbell.SelectedIndex == 3 && Equals(AdvancedDumbbell.SelectedPoint?.Mark.Key, "high") &&
            ReferenceEquals(AdvancedDumbbell.SelectedPoint?.Members[0].Item, Model.AdvancedPairRecords[1]));
        // Deliberately no GetMarks/SelectedPoint call between either setter and the source move.
        Model.AdvancedSelectedIndex = 0; Model.AdvancedSelectedIndex = 5; Model.AdvancedPairRecords.Move(2, 0); FlushBindings();
        Check("consecutive-selection-setters-without-getters-follow-final-side-and-row", Equals(AdvancedDumbbell.SelectedPoint?.Mark.Key, "high") &&
            Equals(AdvancedDumbbell.SelectedPoint?.Members[0].StableId, "west"));
        Model.AdvancedPairRecords.Move(0, 2); FlushBindings();
        // UpdateLayout does not render the invalidated restored scene. Publish its followed
        // index first, otherwise the VM may still hold 1 (west in the former source order)
        // and its equality guard would suppress the intended new selection of north.
        _ = AdvancedDumbbell.GetMarks();
        Model.AdvancedSelectedIndex = 1; FlushBindings();
        var beforeSort = AdvancedDumbbell.SelectedPoint;
        Check("sorting-precondition-selects-north-high-before-reordering", Equals(beforeSort?.Mark.Key, "high") &&
            Equals(beforeSort?.Members[0].StableId, "north") && beforeSort?.Members[0].SourceIndex == 0);
        Model.AdvancedSort = ComparisonSort.ValueAscending; FlushBindings();
        AdvancedComparisonBindingDiagnostics.Add(new { stage = "sort-selection-observation", modelSelectedIndex = Model.AdvancedSelectedIndex,
            controlSelectedIndex = AdvancedDumbbell.SelectedIndex, selectedKey = AdvancedDumbbell.SelectedPoint?.Mark.Key,
            selectedStableId = AdvancedDumbbell.SelectedPoint?.Members[0].StableId, selectedSourceIndex = AdvancedDumbbell.SelectedPoint?.Members[0].SourceIndex,
            beforeSortStableId = beforeSort?.Members[0].StableId, beforeSortKey = beforeSort?.Mark.Key,
            rawSourceOrder = Model.AdvancedPairRecords.Select(record => record.Id).ToArray(),
            sortedGlyphs = AdvancedDumbbell.GetMarks().Select(mark => new { key = mark.Key, sourceIndex = mark.Members[0].SourceIndex, id = mark.Members[0].StableId }).ToArray() });
        Check("sorting-follows-selected-endpoint-with-original-index", Equals(AdvancedDumbbell.SelectedPoint?.Mark.Key, "high") &&
            Equals(AdvancedDumbbell.SelectedPoint?.Members[0].StableId, "north") && AdvancedDumbbell.SelectedPoint?.Members[0].SourceIndex == 0);
        Model.AdvancedSort = ComparisonSort.Source; Model.AdvancedSelectedIndex = -1; FlushBindings();
        AdvancedMirror.SelectedIndex = 1; Model.AdvancedPairRecords.Move(0, 2); FlushBindings();
        Check("mirror-selection-retains-right-side-after-source-move", Equals(AdvancedMirror.SelectedPoint?.Mark.Key, "right") &&
            Equals(AdvancedMirror.SelectedPoint?.Members[0].StableId, "north") && AdvancedMirror.SelectedPoint?.Members[0].SourceIndex == 2);
        Model.AdvancedPairRecords.Move(2, 0); AdvancedMirror.SelectedIndex = -1; FlushBindings();
        AdvancedMekko.SelectedIndex = 1; Model.AdvancedMekkoRecords[0].Parts.Move(1, 0); Model.AdvancedMekkoRecords.Move(0, 1); FlushBindings();
        Check("mekko-selection-follows-child-through-parent-and-child-reorder", ReferenceEquals(AdvancedMekko.SelectedPoint?.Members[1].Item, paid) &&
            AdvancedMekko.SelectedPoint?.Members[0].SourceIndex == 1 && AdvancedMekko.SelectedPoint?.Members[1].SourceIndex == 0);
        Model.AdvancedMekkoRecords.Move(1, 0); Model.AdvancedMekkoRecords[0].Parts.Move(0, 1); AdvancedMekko.SelectedIndex = -1; FlushBindings();
        var oldAmount = paid.Amount; paid.Amount += 2; FlushBindings();
        Check("nested-inpc-updates-mekko-with-immutable-old-snapshot", AdvancedMekko.GetMarks().Single(mark => ReferenceEquals(mark.Members[1].Item, paid)).Value == oldAmount + 2 &&
            originalSegments[1].Value == oldAmount && paid.SubscriberCount > 0);
        paid.Amount = double.NaN; FlushBindings();
        Check("invalid-nested-item-clears-frame-and-reports-data-error", AdvancedMekko.GetMarks().Count == 0 && AdvancedMekko.DataError is ArgumentOutOfRangeException && paid.SubscriberCount > 0);
        paid.Amount = oldAmount; FlushBindings();
        Check("same-nested-item-inpc-recovers-without-refresh", AdvancedMekko.GetMarks().Count == 4 && AdvancedMekko.DataError is null);
        var child = new AdvancedMekkoSegment("a-partner", "合作", 10); Model.AdvancedMekkoRecords[0].Parts.Add(child); FlushBindings();
        Check("nested-collection-add-preserves-real-child-and-subscriber", AdvancedMekko.GetMarks().Count == 5 && child.SubscriberCount > 0 &&
            AdvancedMekko.GetMarks().Any(mark => ReferenceEquals(mark.Members[1].Item, child) && mark.Members[1].SourceIndex == 2));
        Model.AdvancedMekkoRecords[0].Parts.Remove(child); FlushBindings();
        Check("nested-remove-releases-child-observation", AdvancedMekko.GetMarks().Count == 4 && child.SubscriberCount == 0);
        var oldParts = Model.AdvancedMekkoRecords[0].Parts;
        var replacement = new EngineeringObserverCollection<AdvancedMekkoSegment> { new("a-replacement", "替换", 12) };
        Model.AdvancedMekkoRecords[0].Parts = replacement; FlushBindings();
        Check("nested-collection-replacement-releases-old-and-observes-new", AdvancedMekko.GetMarks().Count == 3 && oldParts.SubscriberCount == 0 &&
            oldParts.All(record => record.SubscriberCount == 0) && replacement.SubscriberCount > 0 && replacement[0].SubscriberCount > 0);
        Model.AdvancedMekkoRecords[0].Parts = oldParts; FlushBindings();
        Check("nested-source-restore-releases-replacement", AdvancedMekko.GetMarks().Count == 4 && replacement.SubscriberCount == 0 && replacement[0].SubscriberCount == 0);
        this.FindControl<ComboBox>("UsageAdvancedSort")!.SelectedItem = ComparisonSort.ValueDescending;
        this.FindControl<ComboBox>("UsageAdvancedOrientation")!.SelectedItem = Orientation.Vertical;
        this.FindControl<ComboBox>("UsageAdvancedBaseline")!.SelectedItem = 10d;
        this.FindControl<ComboBox>("UsageAdvancedCenterGap")!.SelectedItem = 24d;
        this.FindControl<ComboBox>("UsageAdvancedColumnGap")!.SelectedItem = 10d;
        this.FindControl<CheckBox>("UsageAdvancedUseMasks")!.IsChecked = true; FlushBindings();
        Check("parameter-controls-use-two-way-compiled-bindings", Model.AdvancedSort == ComparisonSort.ValueDescending && AdvancedDiverging.Sort == ComparisonSort.ValueDescending &&
            Model.AdvancedOrientation == Orientation.Vertical && AdvancedDumbbell.Orientation == Orientation.Vertical && AdvancedDiverging.Baseline == 10 &&
            Model.AdvancedBaseline == 10 && Model.AdvancedCenterGap == 24 && AdvancedMirror.CenterGap == 24 && AdvancedPopulation.CenterGap == 24 &&
            Model.AdvancedColumnGap == 10 && AdvancedMekko.ColumnGap == 10 && Model.AdvancedUseMasks && AdvancedVenn.MaskPath == "Mask");
        Check("maskpath-and-named-setspath-have-equivalent-regions", AdvancedVenn.Layout is { } maskLayout && Near(40, maskLayout.Regions[0].Value) &&
            AdvancedVenn.GetMarks().Count == 3 && AdvancedVenn.DataError is null);
        Model.AdvancedUseMasks = false; FlushBindings();
        Model.AdvancedVennRecords[2].Sets[1] = "Unknown"; FlushBindings();
        Check("unknown-nested-set-name-reports-error-and-keeps-observation", AdvancedVenn.GetMarks().Count == 0 && AdvancedVenn.DataError is ArgumentOutOfRangeException &&
            Model.AdvancedVennRecords[2].Sets.SubscriberCount > 0);
        Model.AdvancedVennRecords[2].Sets[1] = "Store"; FlushBindings();
        Check("same-set-collection-notification-recovers-venn", AdvancedVenn.GetMarks().Count == 3 && AdvancedVenn.DataError is null);
        Model.AdvancedSort = ComparisonSort.Source; Model.AdvancedOrientation = Orientation.Horizontal; Model.AdvancedBaseline = 0;
        Model.AdvancedCenterGap = 12; Model.AdvancedColumnGap = 4; FlushBindings();
        var oldSigned = Model.AdvancedSignedRecords[0].Amount; var oldAfter = Model.AdvancedPairRecords[0].After; var oldVenn = Model.AdvancedVennRecords[0].Amount;
        Model.AdvancedUpdateCommand.Execute(null); FlushBindings();
        Check("business-update-command-rebuilds-four-source-families", AdvancedDiverging.GetMarks()[0].Value == oldSigned - 1 && AdvancedDumbbell.GetMarks()[1].Value == oldAfter + 2 &&
            AdvancedMekko.GetMarks()[1].Value == oldAmount + 3 && AdvancedVenn.Layout is { } updatedVenn && Near(oldVenn + 5 - 20, updatedVenn.Regions[0].Value));
        Check("all-attached-business-parents-children-and-collections-observed", Records().All(record => record.SubscriberCount > 0) && Model.AdvancedSignedRecords.SubscriberCount > 0 &&
            Model.AdvancedPairRecords.SubscriberCount > 0 && Model.AdvancedMekkoRecords.All(column => column.Parts.SubscriberCount > 0) && Model.AdvancedVennRecords.All(record => record.Sets.SubscriberCount > 0));
        var content = Content; Content = null; FlushBindings();
        Check("unload-releases-top-level-and-nested-business-sources", Records().All(record => record.SubscriberCount == 0) &&
            Model.AdvancedSignedRecords.SubscriberCount == 0 && Model.AdvancedPairRecords.SubscriberCount == 0 && Model.AdvancedMekkoRecords.SubscriberCount == 0 &&
            Model.AdvancedVennRecords.SubscriberCount == 0 && Model.AdvancedMekkoRecords.All(column => column.Parts.SubscriberCount == 0) && Model.AdvancedVennRecords.All(record => record.Sets.SubscriberCount == 0));
        Content = content; FlushBindings(); foreach (var chart in charts) _ = chart.GetMarks();
        Check("reattach-restores-seven-charts-and-nested-subscriptions", charts.All(chart => chart.DataError is null && chart.GetMarks().Count > 0) &&
            Records().All(record => record.SubscriberCount > 0) && Model.AdvancedMekkoRecords.All(column => column.Parts.SubscriberCount > 0) && Model.AdvancedVennRecords.All(record => record.Sets.SubscriberCount > 0));
        using (var drawing = new DrawingGroup().Open()) foreach (var chart in charts) chart.Render(drawing);
        AdvancedComparisonBindingDiagnostics.Add(new { kinds = charts.Select(chart => chart.GetType().Name).ToArray(), markCounts = charts.Select(chart => chart.GetMarks().Count).ToArray(),
            inclusiveVenn = Model.AdvancedVennRecords.Select(row => new { row.Mask, row.Amount }).ToArray(),
            vennRegions = AdvancedVenn.Layout?.Regions.Select(region => new { region.Mask, region.Value, region.EstimatedValue, region.SourceIndices }).ToArray(),
            maximumRelativeAreaError = AdvancedVenn.MaximumRelativeAreaError,
            nativeWindowValidated = false, physicalInputValidated = false, nativePixelsValidated = false, performanceMeasured = false });
        Model.AdvancedSignedRecords[0].Amount = oldSigned; Model.AdvancedPairRecords[0].After = oldAfter; paid.Amount = oldAmount; Model.AdvancedVennRecords[0].Amount = oldVenn;
        AdvancedDumbbell.SelectedIndex = -1; AdvancedMirror.SelectedIndex = -1; AdvancedMekko.SelectedIndex = -1;
        expander.IsExpanded = false; FlushBindings();
    }
}
