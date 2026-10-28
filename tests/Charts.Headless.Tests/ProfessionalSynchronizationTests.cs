using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ProfessionalSynchronizationTests
{
    [AvaloniaFact]
    public void Registration_and_configuration_do_not_choose_a_source_and_disposal_removes_membership()
    {
        var first = Engineering(Rows()); var second = Analysis(Rows()); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup();
        Assert.True(group.SynchronizeSelection); Assert.False(group.SynchronizeRange); Assert.False(group.SynchronizeViewport);
        first.SelectedDataKeys = new object[] { "a" }; group.Add(first); group.Add(second); group.Add(second);
        Assert.Empty(second.GetSelectedData()); group.SynchronizeViewport = true; Assert.Empty(second.GetSelectedData());
        group.SynchronizeFrom(first); Assert.Equal(new[] { "a" }, Ids(second));
        Assert.True(group.Remove(second)); Assert.False(group.Remove(second));
        first.SelectedDataKeys = new object[] { "b" }; Assert.Equal(new[] { "a" }, Ids(second));
        group.Add(second); Assert.Equal(new[] { "a" }, Ids(second)); group.SynchronizeFrom(first); Assert.Equal(new[] { "b" }, Ids(second));
        group.Dispose(); group.Dispose(); first.SelectedDataKeys = new object[] { "c" }; Assert.Equal(new[] { "b" }, Ids(second));
        Assert.Throws<ObjectDisposedException>(() => group.Add(first)); Assert.Throws<ObjectDisposedException>(() => group.SynchronizeFrom(first));
    }

    [AvaloniaFact]
    public void Business_key_mapping_links_different_ordered_sources_and_keeps_each_original_identity()
    {
        var sourceRows = Rows(); var targetRows = new ObservableCollection<Row>(Rows().Reverse());
        var first = Engineering(sourceRows); var second = Analysis(targetRows); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup(); group.Add(first, point => ((Row)point.Item!).Business); group.Add(second, point => ((Row)point.Item!).Business);
        first.SelectedDataKeys = new object[] { "a", "c" }; host.Drain();
        Assert.Equal(new[] { "c", "a" }, Ids(second));
        Assert.Equal(new[] { 0, 2 }, second.GetSelectedData().Select(point => point.SourceIndex));
        Assert.All(second.GetSelectedData(), point => Assert.Same(targetRows[point.SourceIndex], point.Item));
        second.SelectedDataKeys = new object[] { "b" }; host.Drain(); Assert.Equal(new[] { "b" }, Ids(first));
    }

    [AvaloniaFact]
    public void Missing_keys_and_overlapping_glyphs_never_feed_a_target_subset_back_into_the_source()
    {
        var first = Engineering(Rows(), aggregate: true); var second = Analysis(new ObservableCollection<Row> { Rows()[2] }); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(second);
        first.SelectedDataKeys = new object[] { "a", "c" }; host.Drain();
        Assert.Equal(new[] { "a", "c" }, Ids(first)); Assert.Equal(new[] { "c" }, Ids(second));
        first.SelectedDataKeys = new object[] { "b" }; host.Drain(); Assert.Empty(second.GetSelectedData()); Assert.Equal(new[] { "b" }, Ids(first));
        second.SelectedDataKeys = Array.Empty<object>(); group.SynchronizeFrom(second); Assert.Empty(first.GetSelectedData());
    }

    [AvaloniaFact]
    public void Aggregate_members_map_to_all_matching_real_target_rows_without_fabricating_a_glyph_index()
    {
        var rows = Rows(); var first = Engineering(rows, aggregate: true); var second = Analysis(Rows()); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(second);
        host.Click(first, first.GetMarks()[0].Position);
        Assert.Equal(new[] { "a", "c" }, Ids(first)); Assert.Equal(new[] { "a", "c" }, Ids(second));
        Assert.Equal(new[] { 0, 2 }, second.GetSelectedData().Select(point => point.SourceIndex));
    }

    [AvaloniaFact]
    public void Target_source_replacement_and_moves_keep_current_items_and_original_indices()
    {
        var first = Engineering(Rows()); var rows = Rows(); var second = Analysis(rows); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(second);
        first.SelectedDataKeys = new object[] { "b" }; rows.Move(1, 0); host.Drain();
        Assert.Equal("b", Assert.Single(second.GetSelectedData()).StableId); Assert.Equal(0, Assert.Single(second.GetSelectedData()).SourceIndex);
        var replacement = new ObservableCollection<Row> { new("c", "Business C", 33), new("b", "Business B", 22) };
        second.ItemsSource = replacement; host.Drain(); group.SynchronizeFrom(first);
        var selected = Assert.Single(second.GetSelectedData()); Assert.Same(replacement[1], selected.Item); Assert.Equal(1, selected.SourceIndex);
        replacement.RemoveAt(1); host.Drain(); Assert.Empty(second.GetSelectedData());
    }

    [AvaloniaFact]
    public void Implicit_identity_is_reference_based_and_custom_null_keys_are_excluded()
    {
        var rows = Rows(); var first = Engineering(rows); var second = Analysis(new ObservableCollection<Row>(rows.Select(row => row with { })));
        first.IdPath = null; second.IdPath = null; using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(second);
        host.Click(first, first.GetMarks()[0].Position); Assert.NotEmpty(first.GetSelectedData()); Assert.Empty(second.GetSelectedData());
        second.ItemsSource = rows; host.Drain(); group.SynchronizeFrom(first); Assert.Same(rows[0], Assert.Single(second.GetSelectedData()).Item);
        group.Remove(first); group.Remove(second); group.Add(first, _ => null); group.Add(second, point => point.StableId);
        second.SelectedIndex = 1; group.SynchronizeFrom(first); Assert.Empty(second.GetSelectedData());
    }

    [AvaloniaFact]
    public void A_new_implicit_key_request_before_initial_publication_is_not_suppressed_by_value_equality()
    {
        var a = new Row("same", "Equal value", 10); var b = a with { }; var rows = new[] { a, b };
        var first = Engineering(rows); var second = Analysis(rows); first.IdPath = null; second.IdPath = null; using var host = new Host(first, second);
        first.SelectedDataKeys = new object[] { a }; host.Drain();
        using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(second);
        first.SelectedDataKeys = new object[] { b }; host.Drain();
        Assert.Same(b, Assert.Single(first.GetSelectedData()).Item); Assert.Same(b, Assert.Single(second.GetSelectedData()).Item);
    }

    [AvaloniaFact]
    public void Ancestor_hide_detach_and_return_do_not_apply_stale_group_state()
    {
        var first = Engineering(Rows()); var second = Analysis(Rows()); var border = new Border { Child = second }; using var host = new Host(first, border);
        using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(second);
        first.SelectedDataKeys = new object[] { "a" }; host.Drain(); Assert.Equal(new[] { "a" }, Ids(second));
        border.IsVisible = false; first.SelectedDataKeys = new object[] { "b" }; host.Drain();
        Assert.Empty(second.GetSelectedData()); Assert.Equal("a", Assert.Single(second.SelectedDataKeys!));
        border.IsVisible = true; host.Drain();
        Assert.DoesNotContain(second.GetSelectedData(), point => Equals(point.StableId, "b"));
        group.SynchronizeFrom(first); Assert.Equal(new[] { "b" }, Ids(second));
        border.Child = null; first.SelectedDataKeys = new object[] { "c" }; host.Drain(); border.Child = second; host.Drain();
        Assert.DoesNotContain(second.GetSelectedData(), point => Equals(point.StableId, "c"));
        group.SynchronizeFrom(first); Assert.Equal(new[] { "c" }, Ids(second));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Disabled_members_and_disabled_ancestors_do_not_replay_a_subset_when_reenabled(bool ancestor)
    {
        var first = Engineering(Rows()); var second = Analysis(new[] { Rows()[0] }); var border = new Border { Child = second };
        using var host = new Host(first, border); using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(second);
        first.SelectedDataKeys = new object[] { "c" }; host.Drain(); Assert.Empty(second.GetSelectedData());
        if (ancestor) border.IsEnabled = false; else second.IsEnabled = false;
        first.SelectedDataKeys = new object[] { "a", "b" }; host.Drain(); Assert.Empty(second.GetSelectedData()); Assert.Empty(second.SelectedDataKeys!);
        if (ancestor) border.IsEnabled = true; else second.IsEnabled = true;
        host.Drain(); Assert.Empty(second.GetSelectedData()); Assert.Equal(new[] { "a", "b" }, Ids(first));
        group.SynchronizeFrom(first); host.Drain(); Assert.Equal(new[] { "a" }, Ids(second)); Assert.Equal(new[] { "a", "b" }, Ids(first));
        if (ancestor) border.IsEnabled = false; else second.IsEnabled = false;
        host.Drain(); if (ancestor) border.IsEnabled = true; else second.IsEnabled = true;
        host.Drain(); Assert.Equal(new[] { "a", "b" }, Ids(first)); Assert.Equal(new[] { "a" }, Ids(second));
    }

    [AvaloniaFact]
    public void Viewport_linking_requires_opt_in_and_a_comparable_non_null_domain()
    {
        var first = Engineering(Rows()); var second = Engineering(Rows()); var unrelated = Engineering(Rows()); var analysis = Analysis(Rows());
        using var host = new Host(first, second, unrelated, analysis); using var group = new ChartDataSynchronizationGroup();
        group.Add(first, layoutDomain: "same-layout"); group.Add(second, layoutDomain: "same-layout"); group.Add(unrelated, layoutDomain: "other-layout"); group.Add(analysis, layoutDomain: "same-layout");
        first.Zoom = 2; Assert.Equal(1, second.Zoom); group.SynchronizeViewport = true; Assert.Equal(1, second.Zoom);
        group.SynchronizeFrom(first); Assert.Equal(2, second.Zoom); Assert.Equal(1, unrelated.Zoom);
        second.PanX = .2; second.PanY = -.15; host.Drain(); Assert.Equal(.2, first.PanX); Assert.Equal(-.15, first.PanY);
        first.ResetViewport(); host.Drain(); Assert.Equal(1, second.Zoom); Assert.Equal(0, second.PanX); Assert.Equal(0, second.PanY);
        Assert.Empty(analysis.GetSelectedData());
    }

    [AvaloniaFact]
    public void Enabling_viewport_linking_does_not_replay_an_older_queued_intent()
    {
        var first = Engineering(Rows()); var second = Engineering(Rows()); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup(); group.Add(first, layoutDomain: "same"); group.Add(second, layoutDomain: "same");
        first.Zoom = 2; group.SynchronizeViewport = true; host.Drain(); Assert.Equal(1, second.Zoom);
        group.SynchronizeFrom(first); Assert.Equal(2, second.Zoom);
        first.PanX = .2; host.Drain(); Assert.Equal(.2, second.PanX);
    }

    [AvaloniaFact]
    public void Independent_local_views_preserve_business_keys_for_current_real_marks_outside_the_plot()
    {
        var first = new EngineeringProbe { ItemsSource = Rows(), UseViewport = true }; var second = new EngineeringProbe { ItemsSource = Rows(), UseViewport = true };
        using var host = new Host(first, second); using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(second);
        first.SelectedDataKeys = new object[] { "a", "b" }; host.Drain(); Assert.Equal(new[] { "a", "b" }, Ids(second));
        first.PanX = 2; host.Drain(); Assert.Empty(first.GetSelectedData()); Assert.Equal(new object[] { "a", "b" }, first.SelectedDataKeys!); Assert.Equal(new[] { "a", "b" }, Ids(second));
        second.PanX = -2; host.Drain(); Assert.Empty(second.GetSelectedData());
        first.SelectedDataKeys = new object[] { "c" }; host.Drain(); Assert.Equal(new object[] { "c" }, second.SelectedDataKeys!); Assert.Empty(second.GetSelectedData());
        first.PanX = 0; second.PanX = 0; host.Drain(); Assert.Equal(new[] { "c" }, Ids(first)); Assert.Equal(new[] { "c" }, Ids(second));
        Assert.Equal(2, Assert.Single(second.GetSelectedData()).SourceIndex);
        first.ItemsSource = Array.Empty<Row>(); host.Drain(); Assert.Empty(second.GetSelectedData()); Assert.Empty(second.SelectedDataKeys!);
    }

    [AvaloniaFact]
    public void Normalized_brush_linking_is_explicit_and_different_domains_keep_business_selection_only()
    {
        var first = Engineering(Rows()); var second = Analysis(Rows()); var unrelated = Analysis(Rows()); using var host = new Host(first, second, unrelated);
        using var group = new ChartDataSynchronizationGroup { SynchronizeRange = true };
        group.Add(first, layoutDomain: "bands"); group.Add(second, layoutDomain: "bands"); group.Add(unrelated);
        first.SelectDataRange(new Rect(0, 0, .3, 1)); host.Drain();
        Assert.Equal(first.DataSelectionRange, second.DataSelectionRange); Assert.Null(unrelated.DataSelectionRange);
        Assert.Equal(new[] { "a" }, Ids(first)); Assert.Equal(new[] { "a" }, Ids(second)); Assert.Equal(new[] { "a" }, Ids(unrelated));
        first.SelectedDataKeys = new object[] { "c" }; host.Drain(); Assert.Null(second.DataSelectionRange); Assert.Equal(new[] { "c" }, Ids(second));
    }

    [AvaloniaFact]
    public void A_business_selection_listener_can_replace_an_applied_selection_and_newest_intent_wins()
    {
        var first = Engineering(Rows()); var second = Analysis(Rows()); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(second);
        var replaced = false;
        second.DataSelectionChanged += (_, _) =>
        {
            if (replaced || !Ids(second).Contains("a")) return;
            replaced = true; second.SelectedDataKeys = new object[] { "c" };
        };
        first.SelectedDataKeys = new object[] { "a" }; host.Drain();
        Assert.True(replaced); Assert.Equal(new[] { "c" }, Ids(first)); Assert.Equal(new[] { "c" }, Ids(second));
    }

    [AvaloniaFact]
    public void A_target_viewport_listener_can_install_a_newer_complete_intent_without_being_overwritten()
    {
        var first = Engineering(Rows()); var second = Engineering(Rows()); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup { SynchronizeViewport = true }; group.Add(first, layoutDomain: "plot"); group.Add(second, layoutDomain: "plot");
        var replaced = false;
        second.PropertyChanged += (_, change) =>
        {
            if (replaced || change.Property != EngineeringChartBase.ZoomProperty || second.Zoom != 2) return;
            replaced = true; second.Zoom = 3; second.PanX = .25; second.PanY = -.2;
        };
        first.Zoom = 2; host.Drain();
        Assert.True(replaced); Assert.Equal(3, first.Zoom); Assert.Equal(3, second.Zoom); Assert.Equal(.25, first.PanX); Assert.Equal(-.2, first.PanY);
    }

    [AvaloniaFact]
    public void Key_selector_reentry_removing_a_member_or_replacing_a_source_abandons_the_old_capture()
    {
        var first = Engineering(Rows()); var second = Analysis(Rows()); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup(); var removed = false;
        group.Add(first, point => { if (!removed) { removed = true; group.Remove(second); } return point.StableId; }); group.Add(second);
        first.SelectedDataKeys = new object[] { "a" }; host.Drain(); Assert.True(removed); Assert.Empty(second.GetSelectedData());
        group.Remove(first); group.Add(first); var replaced = false;
        group.Add(second, point => { if (!replaced) { replaced = true; second.ItemsSource = new[] { new Row("new", "new", 99) }; } return point.StableId; });
        group.SynchronizeFrom(first); host.Drain(); Assert.True(replaced); Assert.DoesNotContain(second.GetSelectedData(), point => Equals(point.StableId, "a"));
    }

    [AvaloniaFact]
    public void Detached_weak_members_do_not_keep_the_chart_alive()
    {
        using var group = new ChartDataSynchronizationGroup(); var weak = AddUnowned(group);
        for (var attempt = 0; attempt < 4; attempt++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(weak.TryGetTarget(out _));
    }

    [AvaloniaFact]
    public void Forced_GC_between_view_invalidation_and_publication_cannot_turn_a_target_subset_into_a_source()
    {
        var first = Engineering(Rows()); var second = Engineering(new ObservableCollection<Row> { Rows()[2] }); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup { SynchronizeViewport = true }; group.Add(first, layoutDomain: "same"); group.Add(second, layoutDomain: "same");
        first.SelectedDataKeys = new object[] { "a", "c" }; host.Drain(); var collections = 0;
        second.PropertyChanged += (_, change) =>
        {
            if (change.Property != EngineeringChartBase.ZoomProperty) return;
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); collections++;
        };
        first.Zoom = 2; host.Drain(); Assert.True(collections > 0);
        Assert.Equal(new[] { "a", "c" }, Ids(first)); Assert.Equal(new[] { "c" }, Ids(second));
    }

    [AvaloniaFact]
    public void A_native_key_and_original_model_that_reference_the_chart_do_not_defeat_nonvisual_weak_membership()
    {
        using var group = new ChartDataSynchronizationGroup(); var weak = AddCyclicMember(group);
        for (var attempt = 0; attempt < 4; attempt++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(weak.TryGetTarget(out _));
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<EngineeringChartBase> AddCyclicMember(ChartDataSynchronizationGroup group)
    {
        var chart = new EngineeringProbe { IdPath = null }; var row = new CyclicRow(chart); chart.ItemsSource = new[] { row };
        chart.SelectedDataKeys = new object[] { row }; group.Add(chart);
        // Isolate group/anchor ownership from the native renderer's own control caches.
        // Actual active-window forced-GC synchronization is covered separately above.
        var original = new ChartDataPoint(0, row, row, row.Business, new(0, row.Value), false);
        chart.SetDataSynchronizationStateAnchor(group, new ChartDataSynchronizationStateAnchor
        { LastKeys = chart.SelectedDataKeys, LastSelection = Array.AsReadOnly(new[] { original }) });
        return new(chart);
    }

    [AvaloniaFact]
    public void Queued_viewport_echoes_do_not_broadcast_a_target_subset_as_a_new_selection()
    {
        var first = Engineering(Rows()); var second = Engineering(new ObservableCollection<Row> { Rows()[2] }); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup { SynchronizeViewport = true };
        group.Add(first, layoutDomain: "same"); group.Add(second, layoutDomain: "same");
        first.SelectedDataKeys = new object[] { "a", "c" }; first.Zoom = 2; first.PanX = .1; host.Drain();
        Assert.Equal(new[] { "a", "c" }, Ids(first)); Assert.Equal(new[] { "c" }, Ids(second)); Assert.Equal(2, second.Zoom); Assert.Equal(.1, second.PanX);
        host.Drain(); Assert.Equal(new[] { "a", "c" }, Ids(first));
    }

    [AvaloniaFact]
    public void Two_brushes_with_the_same_members_still_copy_the_new_normalized_range()
    {
        var first = Engineering(Rows()); var second = Analysis(Rows()); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup { SynchronizeRange = true }; group.Add(first, layoutDomain: "same"); group.Add(second, layoutDomain: "same");
        first.SelectDataRange(new Rect(0, 0, .25, 1)); host.Drain(); Assert.Equal(first.DataSelectionRange, second.DataSelectionRange);
        first.SelectDataRange(new Rect(0, 0, .3, 1)); host.Drain(); Assert.Equal(first.DataSelectionRange, second.DataSelectionRange); Assert.Equal(new[] { "a" }, Ids(second));
    }

    [AvaloniaFact]
    public void Disposing_from_a_business_selector_abandons_the_capture_and_unsubscribes_every_member()
    {
        var first = Engineering(Rows()); var second = Analysis(Rows()); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup(); var disposed = false;
        group.Add(first, point => { disposed = true; group.Dispose(); return point.StableId; }); group.Add(second);
        first.SelectedDataKeys = new object[] { "a" }; host.Drain(); Assert.True(disposed); Assert.Empty(second.GetSelectedData());
        second.SelectedDataKeys = new object[] { "c" }; Assert.Equal(new[] { "a" }, Ids(first));
    }

    [AvaloniaFact]
    public void Map_layer_explicit_ids_link_by_business_value_even_without_a_root_IdPath()
    {
        var first = Engineering(Rows()); using var map = new ShapeMap
        { FeaturesSource = new[] { new MapFeature("area", [new MapPolygon(new MapCoordinate[] { new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0) })]) } };
        var row = new { Id = "a", Longitude = 5d, Latitude = 5d, Value = 7d }; var layer = new BubbleLayer { ItemsSource = new[] { row } }; map.Layers.Add(layer);
        using var host = new Host(first, map); using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(map);
        Assert.Null(map.IdPath); first.SelectedDataKeys = new object[] { "a" }; host.Drain();
        var selected = Assert.Single(map.GetSelectedData()); Assert.Same(row, selected.Item); Assert.Equal("a", selected.StableId);
        var native = Assert.IsType<MapGlyphKey>(Assert.Single(map.SelectedDataKeys!)); Assert.Same(layer, native.Layer);
    }

    [AvaloniaFact]
    public void Mixed_hierarchy_explicit_and_missing_ids_do_not_match_distinct_value_equal_fallback_nodes()
    {
        var firstFallback = new Node(null, "Fallback", 5); var secondFallback = firstFallback with { };
        var first = new TreemapChart { ItemsSource = new[] { new HierarchyNode(5, label: "Explicit", id: "a"), new HierarchyNode(5, label: "Fallback", source: firstFallback) } };
        var second = new TreemapChart { ItemsSource = new[] { new HierarchyNode(5, label: "Explicit", id: "a"), new HierarchyNode(5, label: "Fallback", source: secondFallback) } };
        using var host = new Host(first, second); using var group = new ChartDataSynchronizationGroup(); group.Add(first); group.Add(second);
        first.SelectedDataKeys = new object[] { "a", firstFallback }; host.Drain();
        Assert.Equal(2, first.GetSelectedData().Count); var selected = Assert.Single(second.GetSelectedData()); Assert.Equal("a", selected.StableId);
        Assert.DoesNotContain(second.GetSelectedData(), point => ReferenceEquals(point.Item, secondFallback));
    }

    [AvaloniaFact]
    public void Contradictory_viewport_bindings_have_a_finite_sixty_four_change_guard()
    {
        var first = Engineering(Rows()); var second = Engineering(Rows()); using var host = new Host(first, second);
        using var group = new ChartDataSynchronizationGroup { SynchronizeViewport = true }; group.Add(first, layoutDomain: "same"); group.Add(second, layoutDomain: "same");
        void FirstChanged(object? _, AvaloniaPropertyChangedEventArgs change) { if (change.Property == EngineeringChartBase.ZoomProperty && first.Zoom == 3) first.Zoom = 2; }
        void SecondChanged(object? _, AvaloniaPropertyChangedEventArgs change) { if (change.Property == EngineeringChartBase.ZoomProperty && second.Zoom == 2) second.Zoom = 3; }
        first.PropertyChanged += FirstChanged; second.PropertyChanged += SecondChanged;
        try
        {
            first.Zoom = 2;
            var error = Assert.Throws<InvalidOperationException>(host.Drain); Assert.Contains("64", error.Message);
        }
        finally { first.PropertyChanged -= FirstChanged; second.PropertyChanged -= SecondChanged; group.Dispose(); host.Drain(); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<EngineeringChartBase> AddUnowned(ChartDataSynchronizationGroup group)
    { var chart = Engineering(Rows()); group.Add(chart); return new(chart); }

    private static ObservableCollection<Row> Rows() => [new("a", "Business A", 10), new("b", "Business B", 20), new("c", "Business C", 30)];
    private static EngineeringProbe Engineering(IEnumerable<Row> rows, bool aggregate = false) => new() { ItemsSource = rows, Aggregate = aggregate };
    private static AnalysisProbe Analysis(IEnumerable<Row> rows) => new() { ItemsSource = rows };
    private static string[] Ids(EngineeringChartBase chart) => chart.GetSelectedData().Select(point => (string)point.StableId!).ToArray();
    private static string[] Ids(AnalysisChartBase chart) => chart.GetSelectedData().Select(point => (string)point.StableId!).ToArray();
    private sealed record Row(string Id, string Business, double Value);
    private sealed record Node(string? Id, string Label, double Value) { public Node[] Children => []; }
    private sealed record CyclicRow(ChartBase Chart) { public string Business => "Cyclic original"; public double Value => 7; }
    private sealed class EngineeringProbe : EngineeringChartBase
    {
        public bool Aggregate { get; init; }
        public bool UseViewport { get; init; }
        public EngineeringProbe() { IdPath = "Id"; LabelPath = "Business"; ObservePaths("Value"); }
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
        {
            if (Aggregate && data.Count >= 3) return [new(Array.AsReadOnly(new[] { data.Points[0], data.Points[2] }), new RectangleGeometry(Position(0)), Brushes.Blue, Position(0).Center), new(Array.AsReadOnly(new[] { data.Points[1] }), new RectangleGeometry(Position(1)), Brushes.Blue, Position(1).Center)];
            return data.Points.Select((point, index) => new EngineeringMarkInfo(Array.AsReadOnly(new[] { point }), new RectangleGeometry(Position(index)), Brushes.Blue, Position(index).Center)).ToArray();
            Rect Position(int index) { var rectangle = Box(plot, index); return UseViewport ? new Rect(ApplyViewport(rectangle.TopLeft, plot), ApplyViewport(rectangle.BottomRight, plot)) : rectangle; }
        }
    }
    private sealed class AnalysisProbe : AnalysisChartBase
    {
        public AnalysisProbe() { IdPath = "Id"; CategoryPath = "Business"; ValuePath = "Value"; }
        protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => data.Points.Select((point, index) => new AnalysisMarkInfo(point, new RectangleGeometry(Box(plot, index)), Brushes.Blue, Box(plot, index).Center)).ToArray();
    }
    private static Rect Box(Rect plot, int index) => new(plot.Left + plot.Width * (.05 + index * .32), plot.Top + plot.Height * .2, plot.Width * .15, plot.Height * .5);
    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        public Host(params Control[] children)
        { var panel = new StackPanel(); foreach (var child in children) { child.Width = 600; child.Height = 240; panel.Children.Add(child); } _window = new Window { Content = panel, Width = 640, Height = Math.Max(260, children.Length * 240) }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Click(Control chart, Point point) { var target = chart.TranslatePoint(point, _window)!.Value; _window.MouseDown(target, MouseButton.Left); _window.MouseUp(target, MouseButton.Left); Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
