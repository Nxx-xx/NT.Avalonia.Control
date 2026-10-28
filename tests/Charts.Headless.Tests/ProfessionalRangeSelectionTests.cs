using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ProfessionalRangeSelectionTests
{
    [AvaloniaFact]
    public void Professional_selection_has_shared_TwoWay_properties_and_rejects_invalid_layout_ranges()
    {
        Assert.Same(EngineeringChartBase.SelectedDataKeysProperty, AnalysisChartBase.SelectedDataKeysProperty);
        Assert.Same(EngineeringChartBase.DataSelectionRangeProperty, AnalysisChartBase.DataSelectionRangeProperty);
        foreach (var chart in new ChartBase[] { new EngineeringProbe(), new AnalysisProbe() })
        {
            Assert.Null(Keys(chart)); Assert.Null(Range(chart)); Assert.Empty(Selected(chart));
            foreach (var range in new[] { new Rect(-.1, 0, .2, 1), new Rect(0, 0, 0, 1), new Rect(0, 0, 1.1, 1), new Rect(double.NaN, 0, 1, 1) })
                Assert.Throws<ArgumentOutOfRangeException>(() => SelectRange(chart, range));
        }
        var selector = new ChartDataRangeSelector { MinimumSpan = .2, Range = new(.95, 2) };
        Assert.Equal(new ChartRange(.8, 1), selector.Range);
        Assert.Throws<ArgumentException>(() => selector.Axis = ChartInteractionAxes.Both);
        Assert.Throws<ArgumentException>(() => selector.Range = new(double.NaN, 1));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Range_brush_returns_complete_real_members_in_paint_order_and_deduplicates_shared_members(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows); using var host = new Host(chart);
        SelectRange(chart, new(0, 0, .4, 1)); host.Drain();
        Assert.Equal(engineering ? new[] { "r0", "r3", "r1" } : new[] { "r0", "r1" }, Selected(chart).Select(point => point.StableId));
        Assert.All(Selected(chart), point => Assert.Same(rows[point.SourceIndex], point.Item));
        Assert.Equal(-1, SingleIndex(chart)); Assert.NotNull(Range(chart));
        var overlay = Assert.Single(chart.GetVisualChildren().OfType<Control>(), control => control.Name == "PART_ChartDataSelectionOverlay");
        Assert.False(overlay.IsHitTestVisible); Assert.False(overlay.Focusable); Assert.Equal(28, overlay.ZIndex); Assert.Equal(new Rect(chart.Bounds.Size), overlay.Bounds);
        Clear(chart); host.Drain(); Assert.Empty(Selected(chart)); Assert.Null(Range(chart)); Assert.DoesNotContain(chart.GetVisualChildren().OfType<Control>(), control => control.Name == "PART_ChartDataSelectionOverlay");
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Requested_business_keys_preserve_unknown_ids_and_publish_only_matching_visible_original_observations(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows); using var host = new Host(chart);
        SetKeys(chart, new object[] { "r3", "future" }); host.Drain();
        if (engineering) Assert.Equal("r3", Assert.Single(Selected(chart)).StableId); else Assert.Empty(Selected(chart));
        Assert.Equal(new object[] { "r3", "future" }, Keys(chart)); Assert.Null(Range(chart)); Assert.Equal(-1, SingleIndex(chart));
        rows[0] = new("future", "Future", 101); host.Drain();
        Assert.Contains(Selected(chart), point => Equals(point.StableId, "future") && ReferenceEquals(point.Item, rows[0]));
        Assert.Equal(new object[] { "r3", "future" }, Keys(chart));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Explicit_identity_follows_replacement_and_reordering_without_modifying_the_source_or_legacy_selection(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows); using var host = new Host(chart);
        SetKeys(chart, new object[] { "r1" }); host.Drain(); var replacement = new Row("r1", "Replacement", 123);
        Source(chart, new[] { replacement, rows[0], rows[2], rows[3] }); host.Drain();
        var selected = Assert.Single(Selected(chart)); Assert.Same(replacement, selected.Item); Assert.Equal(0, selected.SourceIndex); Assert.Equal(123d, selected.Point.Y);
        Assert.Equal(-1, SingleIndex(chart)); Assert.Equal(4, rows.Count);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Implicit_identity_compares_original_instances_even_when_business_records_are_equal(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows);
        if (chart is EngineeringChartBase e) e.IdPath = null; else ((AnalysisChartBase)chart).IdPath = null;
        using var host = new Host(chart); SetKeys(chart, new object[] { rows[0] }); host.Drain(); Assert.Same(rows[0], Assert.Single(Selected(chart)).Item);
        var equal = rows[0] with { }; Source(chart, new[] { equal, rows[1], rows[2], rows[3] }); host.Drain(); Assert.Empty(Selected(chart));
        SetKeys(chart, new object[] { equal }); host.Drain(); Assert.Same(equal, Assert.Single(Selected(chart)).Item);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Source_updates_release_published_references_and_stable_pointer_hover_does_not_reenumerate_source(bool engineering)
    {
        var rows = Rows(); var source = new CountingRows(rows); var chart = Create(engineering, source); using var host = new Host(chart);
        SetKeys(chart, new object[] { "r0" }); host.Drain(); var reads = source.Enumerations;
        host.Move(Anchor(chart)); host.Move(Anchor(chart) + new Vector(2, 2)); _ = Selected(chart); host.Drain(); Assert.Equal(reads, source.Enumerations);
        rows[0] = new("new", "New", 111); Assert.Empty(ReadOnlySelected(chart)); host.Drain(); Assert.Empty(Selected(chart));
        Assert.Equal(new object[] { "r0" }, Keys(chart));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Removing_every_selected_original_publishes_a_coherent_empty_selection_and_clears_linked_targets(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows); var target = Create(!engineering, Rows());
        chart.Height = 200; target.Height = 200; using var host = new Host(chart, new StackPanel { Children = { chart, target } });
        using var group = new ChartDataSynchronizationGroup();
        if (chart is EngineeringChartBase e) group.Add(e); else group.Add((AnalysisChartBase)chart);
        if (target is EngineeringChartBase other) group.Add(other); else group.Add((AnalysisChartBase)target);
        var events = new List<IReadOnlyList<ChartDataPoint>>(); Subscribe(chart, (_, _) => events.Add(ReadOnlySelected(chart)));
        SetKeys(chart, new object[] { "r0" }); host.Drain(); Assert.Equal("r0", Assert.Single(Selected(target)).StableId);
        rows[0] = new("replacement", "Replacement", 111); Assert.Empty(ReadOnlySelected(chart)); host.Drain();
        Assert.Empty(Selected(chart)); Assert.Empty(Selected(target)); Assert.Equal(new object[] { "r0" }, Keys(chart));
        Assert.Contains(events, snapshot => snapshot.Count == 0); Assert.Empty(events.Last());
        Assert.DoesNotContain(Selected(target), point => ReferenceEquals(point.Item, rows[0]));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void A_selected_data_notification_can_replace_source_and_keys_before_semantic_publication(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows); using var host = new Host(chart);
        var replacement = new[] { new Row("new0", "New", 80), new Row("new1", "Second", 90) }; var requested = false;
        var published = new List<object?>(); Subscribe(chart, (_, _) => published.AddRange(Selected(chart).Select(point => point.StableId)));
        chart.PropertyChanged += (_, change) =>
        {
            if (change.Property.Name != "SelectedData" || requested || ReadOnlySelected(chart).Count == 0) return;
            requested = true; Source(chart, replacement); SetKeys(chart, new object[] { "new1" }); _ = Selected(chart);
        };
        SetKeys(chart, new object[] { "r0" }); host.Drain();
        Assert.True(requested); var selected = Assert.Single(Selected(chart)); Assert.Same(replacement[1], selected.Item); Assert.Equal("new1", selected.StableId);
        Assert.DoesNotContain("r0", published); Assert.Contains("new1", published);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void A_cleared_data_notification_can_request_new_keys_without_restoring_stale_original_members(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); SetKeys(chart, new object[] { "r0" }); host.Drain();
        var requested = false;
        chart.PropertyChanged += (_, change) =>
        {
            if (change.Property.Name != "SelectedData" || requested || ReadOnlySelected(chart).Count != 0) return;
            requested = true; SetKeys(chart, new object[] { "r2" }); _ = Selected(chart);
        };
        chart.FontSize += 1; host.Drain(); Assert.True(requested); Assert.Equal("r2", Assert.Single(Selected(chart)).StableId);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Semantic_event_reentry_preserves_the_newest_source_and_key_request(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); var requested = false;
        var replacement = new[] { new Row("latest0", "New", 50), new Row("latest1", "Second", 60) };
        Subscribe(chart, (_, _) =>
        {
            if (requested || Selected(chart).Count == 0) return;
            requested = true; Source(chart, replacement); SetKeys(chart, new object[] { "latest1" });
        });
        SetKeys(chart, new object[] { "r0" }); host.Drain(); Assert.True(requested); Assert.Same(replacement[1], Assert.Single(Selected(chart)).Item);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Binding_override_of_range_derived_keys_wins_atomically_and_clears_the_old_brush(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); var requested = false;
        chart.PropertyChanged += (_, change) =>
        {
            if (change.Property.Name != "SelectedDataKeys" || requested || Range(chart) is null) return;
            requested = true; SetKeys(chart, new object[] { "r2" });
        };
        SelectRange(chart, new(0, 0, .4, 1)); host.Drain(); Assert.True(requested); Assert.Null(Range(chart)); Assert.Equal("r2", Assert.Single(Selected(chart)).StableId);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Clearing_the_brush_does_not_overwrite_a_new_selection_installed_by_a_range_binding(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); SelectRange(chart, new(0, 0, .4, 1)); host.Drain(); var requested = false;
        chart.PropertyChanged += (_, change) =>
        {
            if (change.Property.Name != "DataSelectionRange" || requested || Range(chart) is not null) return;
            requested = true; SetKeys(chart, new object[] { "r2" });
        };
        Clear(chart); host.Drain(); Assert.True(requested); Assert.Equal("r2", Assert.Single(Selected(chart)).StableId);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Hidden_ancestor_and_detach_release_readings_and_restore_only_current_requested_keys(bool engineering)
    {
        var rows = Rows(); var chart = Create(engineering, rows); var container = new Border { Child = chart }; using var host = new Host(chart, container);
        SetKeys(chart, new object[] { "r0" }); host.Drain(); Assert.NotEmpty(Selected(chart));
        container.IsVisible = false; Assert.Empty(ReadOnlySelected(chart)); host.Drain(); Assert.Empty(Selected(chart));
        rows[0] = new("r0", "Current", 77); container.IsVisible = true; host.Drain(); Assert.Same(rows[0], Assert.Single(Selected(chart)).Item);
        chart.IsEnabled = false; host.Drain(); Assert.Empty(Selected(chart)); Assert.Equal(new object[] { "r0" }, Keys(chart));
        chart.IsEnabled = true; host.Drain(); Assert.Same(rows[0], Assert.Single(Selected(chart)).Item);
        host.Detach(); Assert.Empty(ReadOnlySelected(chart)); SetKeys(chart, new object[] { "r2" }); host.Attach(); Assert.Equal("r2", Assert.Single(Selected(chart)).StableId);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Existing_click_and_keyboard_selection_also_publish_real_multi_selection_without_changing_single_point_semantics(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); host.Click(Anchor(chart));
        Assert.Equal(0, SingleIndex(chart)); Assert.Equal(engineering ? 2 : 1, Selected(chart).Count);
        host.Click(Anchor(chart)); Assert.Equal(-1, SingleIndex(chart)); Assert.Empty(Selected(chart));
        chart.Focus(); host.Key(Key.Home); host.Key(Key.Enter); Assert.Equal(0, SingleIndex(chart)); Assert.NotEmpty(Selected(chart));
        host.Key(Key.Escape); Assert.Equal(-1, SingleIndex(chart)); Assert.Empty(Selected(chart));
    }

    [AvaloniaTheory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)]
    public void Legacy_single_point_callbacks_can_install_new_keys_brush_or_clear_without_old_selection_echo(bool engineering, int intent)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); SetKeys(chart, new object[] { "r1" }); host.Drain();
        var requested = false; var published = new List<object?>(); Subscribe(chart, (_, _) => published.AddRange(ReadOnlySelected(chart).Select(point => point.StableId)));
        chart.PropertyChanged += (_, change) =>
        {
            if (change.Property.Name != "SelectedPoint" || requested) return;
            var selected = chart is EngineeringChartBase e ? e.SelectedPoint is not null : ((AnalysisChartBase)chart).SelectedPoint is not null;
            if (!selected) return;
            requested = true; Assert.Throws<InvalidOperationException>(() => RecordExport(chart));
            if (intent == 0) SetKeys(chart, new object[] { "r2" });
            else if (intent == 1) SelectRange(chart, new(.5, 0, .5, 1));
            else Clear(chart);
        };
        host.Click(Anchor(chart)); Assert.True(requested); Assert.Equal(0, SingleIndex(chart));
        if (intent == 2) { Assert.Empty(Selected(chart)); Assert.Null(Range(chart)); }
        else Assert.Equal("r2", Assert.Single(Selected(chart)).StableId);
        Assert.DoesNotContain("r1", published); Assert.DoesNotContain("r0", published); Assert.DoesNotContain("r3", published);
        Assert.Equal(intent == 1, Range(chart) is not null);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void A_changed_range_is_semantically_published_even_when_the_same_original_members_remain_selected(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); var events = 0; Subscribe(chart, (_, _) => events++);
        SelectRange(chart, new(0, 0, .4, 1)); host.Drain(); var first = Selected(chart).Select(point => point.StableId).ToArray(); var count = events;
        SelectRange(chart, new(0, 0, .41, 1)); host.Drain(); Assert.Equal(first, Selected(chart).Select(point => point.StableId)); Assert.True(events > count);
        var snapshot = Assert.IsAssignableFrom<IList<ChartDataPoint>>(Selected(chart)); Assert.Throws<NotSupportedException>(() => snapshot.Clear());
    }

    [AvaloniaFact]
    public void Duplicate_explicit_ids_match_all_original_observations_without_truncating_an_aggregate()
    {
        var first = new Row("duplicate", "First", 10); var second = new Row("duplicate", "Second", 20);
        var rows = new[] { first, second, new Row("other", "Other", 30), new Row("duplicate", "Fourth", 40) };
        // Custom engineering projections can supply valid raw member snapshots independently of generic mapping's unique-ID policy.
        // Keep that mapper's duplicate-ID validation intact while checking complete aggregate membership.
        var chart = new RawAggregateProbe(rows); using var host = new Host(chart);
        SetKeys(chart, new object[] { "duplicate" }); host.Drain();
        Assert.Equal(new[] { 0, 3, 1 }, Selected(chart).Select(point => point.SourceIndex));
        Assert.All(Selected(chart), point => Assert.Same(rows[point.SourceIndex], point.Item));
        SelectRange(chart, new(0, 0, .4, 1)); host.Drain(); Assert.Equal("duplicate", Assert.Single(Keys(chart)!));
        Assert.Equal(3, Selected(chart).Count);
    }

    [AvaloniaFact]
    public void Relation_node_and_edge_keys_and_map_layer_domains_never_alias_equal_business_ids()
    {
        var node = new RelationGlyphKey(RelationElementKind.Node, "same", true); var edge = new RelationGlyphKey(RelationElementKind.Edge, "same", true);
        var first = new MarkerLayer(); var second = new MarkerLayer();
        var chart = new DomainProbe(node, edge, new MapGlyphKey(first, MapElementKind.Marker, "same", true), new MapGlyphKey(second, MapElementKind.Marker, "same", true));
        using var host = new Host(chart); chart.SelectedDataKeys = new object[] { node }; host.Drain(); Assert.Equal(0, Assert.Single(chart.GetSelectedData()).SourceIndex);
        chart.SelectedDataKeys = new object[] { edge }; host.Drain(); Assert.Equal(1, Assert.Single(chart.GetSelectedData()).SourceIndex);
        chart.SelectedDataKeys = new object[] { new MapGlyphKey(second, MapElementKind.Marker, "same", true) }; host.Drain(); Assert.Equal(3, Assert.Single(chart.GetSelectedData()).SourceIndex);
        chart.SelectedDataKeys = new object[] { "same" }; host.Drain(); Assert.Empty(chart.GetSelectedData());
        chart.SelectDataRange(new(0, 0, 1, 1)); host.Drain(); Assert.Equal(4, chart.GetSelectedData().Count); Assert.Equal(4, chart.SelectedDataKeys!.Count);
    }

    [AvaloniaFact]
    public void Default_queries_render_export_and_hover_add_no_full_aggregate_walk_until_selection_is_requested()
    {
        var chart = new CountedAggregateProbe(); using var host = new Host(chart);
        Assert.Equal(chart.BuildCalls, chart.Members.CompletedEnumerations);
        for (var iteration = 0; iteration < 8; iteration++)
        {
            _ = chart.GetMarks(); _ = chart.GetSelectedData(); _ = RecordExport(chart);
            host.Move(chart.PlotArea.Center + new Vector(iteration * .1, 0)); host.Drain();
        }
        // Building the legacy native identity index walks each original member once. Following a hover may stop at its first member;
        // the new disabled selection feature must add no complete source walk or aggregate copy.
        Assert.Equal(chart.BuildCalls, chart.Members.CompletedEnumerations);
        var callbacks = 0; Assert.Empty(chart.PrepareSynchronizedSelection(Array.Empty<object>(), point => { callbacks++; return point.StableId; })!);
        Assert.Equal(0, callbacks); Assert.Equal(chart.BuildCalls, chart.Members.CompletedEnumerations);
        chart.SelectedDataKeys = new object[] { "row999" }; host.Drain(); var selected = Assert.Single(chart.GetSelectedData());
        Assert.Same(chart.Rows[999], selected.Item); Assert.Equal(999, selected.SourceIndex); Assert.True(chart.Members.CompletedEnumerations > chart.BuildCalls);
    }

    [AvaloniaFact]
    public void Typed_hierarchy_ids_compare_by_business_value_without_requiring_an_IdPath()
    {
        var original = new object(); var other = new object();
        var chart = new TreemapChart { ItemsSource = new[] { new HierarchyNode(6, source: original, id: new string("alpha".ToCharArray())), new HierarchyNode(4, source: other, id: "beta") } };
        using var host = new Host(chart); Assert.Null(chart.IdPath);
        chart.SelectedDataKeys = new object[] { new string("alpha".ToCharArray()) }; host.Drain(); Assert.Same(original, Assert.Single(chart.GetSelectedData()).Item);
        var replacement = new object(); chart.ItemsSource = new[] { new HierarchyNode(4, source: other, id: "beta"), new HierarchyNode(9, source: replacement, id: new string("alpha".ToCharArray())) }; host.Drain();
        var selected = Assert.Single(chart.GetSelectedData()); Assert.Same(replacement, selected.Item); Assert.Equal(1, selected.SourceIndex);
        Assert.Equal("alpha", Assert.Single(chart.SelectedDataKeys!));
    }

    [AvaloniaFact]
    public void A_zero_height_visible_line_can_be_selected_but_offplot_and_empty_geometry_cannot()
    {
        var chart = new LineProbe(); using var host = new Host(chart); chart.SelectDataRange(new(0, 0, 1, 1)); host.Drain();
        Assert.Equal(0, Assert.Single(chart.GetSelectedData()).SourceIndex);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Layout_selector_preserves_other_axis_and_reuses_real_two_handle_pointer_keyboard_input(bool engineering)
    {
        var chart = Create(engineering, Rows()); var x = new ChartDataRangeSelector(); var y = new ChartDataRangeSelector { Axis = ChartInteractionAxes.Y, Height = 100 };
        if (chart is EngineeringChartBase e) { x.EngineeringChart = e; y.EngineeringChart = e; } else { x.AnalysisChart = (AnalysisChartBase)chart; y.AnalysisChart = (AnalysisChartBase)chart; }
        var panel = new StackPanel { Children = { chart, x, y } }; chart.Height = 220; using var host = new Host(chart, panel);
        x.Range = new(.1, .6); y.Range = new(.2, .7); host.Drain(); AssertRect(new(.1, .2, .5, .5), Range(chart)!.Value);
        Assert.Equal(Orientation.Vertical, y.Selector.Orientation); Assert.True(y.Selector.IsDirectionReversed); Assert.Equal(Orientation.Horizontal, x.Selector.Orientation);
        x.Selector.Focus(); host.Key(Key.Right); AssertRect(new(.11, .2, .5, .5), Range(chart)!.Value);
        var lower = x.Selector.GetVisualChildren().OfType<Control>().Single(control => control.Name == "PART_LowerHandle"); lower.Focus(); host.Key(Key.Right);
        AssertRect(new(.12, .2, .49, .5), Range(chart)!.Value);
        var position = x.Selector.TranslatePoint(lower.Bounds.Center, host.Window)!.Value;
        host.Drag(position, position + new Vector(40, 0)); Assert.True(x.Range.Lower > .12); Assert.Equal(.2, Range(chart)!.Value.Top, 8);
        Clear(chart); host.Drain(); Assert.Equal(new ChartRange(0, 1), x.Range); Assert.Equal(new ChartRange(0, 1), y.Range);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Selector_detach_and_hidden_parent_release_target_observation_and_rejoin_current_brush(bool engineering)
    {
        var chart = Create(engineering, Rows()); var selector = new ChartDataRangeSelector();
        if (chart is EngineeringChartBase e) selector.EngineeringChart = e; else selector.AnalysisChart = (AnalysisChartBase)chart;
        var container = new Border { Child = selector }; var panel = new StackPanel { Children = { chart, container } }; chart.Height = 250; using var host = new Host(chart, panel);
        SelectRange(chart, new(.2, 0, .4, 1)); host.Drain(); Assert.Equal(.2, selector.Range.Lower, 8); Assert.Equal(.6, selector.Range.Upper, 8);
        container.IsVisible = false; SelectRange(chart, new(.3, 0, .4, 1)); host.Drain(); Assert.Equal(.2, selector.Range.Lower, 8); Assert.Equal(.6, selector.Range.Upper, 8);
        container.IsVisible = true; host.Drain(); Assert.Equal(.3, selector.Range.Lower, 8); Assert.Equal(.7, selector.Range.Upper, 8);
        container.Child = null; SelectRange(chart, new(.1, 0, .3, 1)); host.Drain(); Assert.Equal(.3, selector.Range.Lower, 8);
        container.Child = selector; host.Drain(); Assert.Equal(.1, selector.Range.Lower, 8); Assert.Equal(.4, selector.Range.Upper, 8);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Initial_TwoWay_range_binding_initializes_an_empty_chart_and_replacement_view_models_remain_live(bool engineering)
    {
        var chart = Create(engineering, Rows()); var selector = new ChartDataRangeSelector(); var model = new RangeModel(new(.2, .6));
        selector.DataContext = model; selector.Bind(ChartDataRangeSelector.RangeProperty, new Binding(nameof(RangeModel.Range)) { Mode = BindingMode.TwoWay });
        if (chart is EngineeringChartBase e) selector.EngineeringChart = e; else selector.AnalysisChart = (AnalysisChartBase)chart;
        chart.Height = 250; using var host = new Host(chart, new StackPanel { Children = { chart, selector } });
        AssertRect(new(.2, 0, .4, 1), Range(chart)!.Value); Assert.Equal(.2, model.Range.Lower, 8); Assert.Equal(.6, model.Range.Upper, 8);
        SelectRange(chart, new(.4, 0, .3, 1)); host.Drain(); Assert.Equal(.4, model.Range.Lower, 8); Assert.Equal(.7, model.Range.Upper, 8);
        var replacement = new RangeModel(new(.1, .3)); selector.DataContext = replacement; host.Drain();
        AssertRect(new(.1, 0, .2, 1), Range(chart)!.Value); Assert.Equal(new ChartRange(.1, .3), replacement.Range);
        model.Range = new(.6, .9); host.Drain(); Assert.Equal(.1, selector.Range.Lower, 8); Assert.Equal(.3, selector.Range.Upper, 8);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Selector_receipt_reentry_preserves_the_newest_bound_range_request(bool engineering)
    {
        var chart = Create(engineering, Rows()); var selector = new ChartDataRangeSelector();
        if (chart is EngineeringChartBase e) selector.EngineeringChart = e; else selector.AnalysisChart = (AnalysisChartBase)chart;
        chart.Height = 250; using var host = new Host(chart, new StackPanel { Children = { chart, selector } }); var requested = false;
        selector.PropertyChanged += (_, change) =>
        {
            if (change.Property != ChartDataRangeSelector.RangeProperty || requested) return;
            requested = true; selector.Range = new(.4, .8);
        };
        SelectRange(chart, new(.2, 0, .4, 1)); host.Drain(); Assert.True(requested);
        Assert.Equal(.4, selector.Range.Lower, 8); Assert.Equal(.8, selector.Range.Upper, 8); AssertRect(new(.4, 0, .4, 1), Range(chart)!.Value);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Selector_displays_an_external_brush_narrower_than_its_local_minimum_without_expanding_the_chart(bool engineering)
    {
        var chart = Create(engineering, Rows()); var selector = new ChartDataRangeSelector { MinimumSpan = .2 };
        if (chart is EngineeringChartBase e) selector.EngineeringChart = e; else selector.AnalysisChart = (AnalysisChartBase)chart;
        chart.Height = 250; using var host = new Host(chart, new StackPanel { Children = { chart, selector } });
        SelectRange(chart, new(.5, 0, .001, 1)); host.Drain();
        Assert.Equal(.5, selector.Range.Lower, 8); Assert.Equal(.501, selector.Range.Upper, 8);
        Assert.Equal(selector.Range, selector.Selector.Range); AssertRect(new(.5, 0, .001, 1), Range(chart)!.Value);
        selector.Range = new(.2, .21); host.Drain(); Assert.Equal(.2, selector.Range.Span, 8); Assert.Equal(.2, Range(chart)!.Value.Width, 8);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Detached_export_draws_current_permanent_selection_without_retaining_detached_selected_data(bool engineering)
    {
        var chart = Create(engineering, Rows()); chart.Measure(new(640, 400)); chart.Arrange(new(0, 0, 640, 400));
        SetKeys(chart, new object[] { "r0" }); Assert.Empty(ReadOnlySelected(chart));
        var drawing = RecordExport(chart);
        Assert.Single(Shapes(drawing), shape => shape.Pen?.Thickness == 5);
        Assert.Empty(ReadOnlySelected(chart)); Assert.Equal(new object[] { "r0" }, Keys(chart));
        SetKeys(chart, new object[] { "r2" }); drawing = RecordExport(chart);
        var outline = Assert.Single(Shapes(drawing), shape => shape.Pen?.Thickness == 5);
        var plot = Plot(chart); Assert.True(outline.Geometry!.Bounds.Center.X > plot.Center.X);
        Assert.Empty(ReadOnlySelected(chart));
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Selection_publication_rejects_nested_export_and_brush_outline_is_excluded_from_permanent_capture(bool engineering)
    {
        var chart = Create(engineering, Rows()); using var host = new Host(chart); var attempted = false;
        chart.PropertyChanged += (_, change) =>
        {
            if (change.Property.Name != "SelectedData" || attempted || ReadOnlySelected(chart).Count == 0) return;
            attempted = true; Assert.Throws<InvalidOperationException>(() => RecordExport(chart));
        };
        SelectRange(chart, new(0, 0, .4, 1)); host.Drain(); Assert.True(attempted);
        var withBrush = Shapes(RecordExport(chart)).ToArray(); var keys = Keys(chart)!.ToArray();
        SetKeys(chart, keys); host.Drain(); Assert.Null(Range(chart)); var withoutBrush = Shapes(RecordExport(chart)).ToArray();
        Assert.Equal(withBrush.Length, withoutBrush.Length);
        Assert.Equal(withBrush.Select(shape => shape.Pen?.Thickness), withoutBrush.Select(shape => shape.Pen?.Thickness));
        Assert.Contains(withoutBrush, shape => shape.Pen?.Thickness == 5);
    }

    private static DrawingGroup RecordExport(ChartBase chart)
    {
        var group = new DrawingGroup(); using var context = group.Open();
        if (chart is EngineeringChartBase e) e.RenderExport(context, e.GetExportLayout(null), null);
        else { var a = (AnalysisChartBase)chart; a.RenderExport(context, a.GetExportLayout(null), null); }
        return group;
    }
    private static IEnumerable<GeometryDrawing> Shapes(Drawing drawing)
    {
        if (drawing is GeometryDrawing shape) yield return shape;
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var nested in Shapes(child)) yield return nested;
    }
    private static void AssertRect(Rect expected, Rect actual) { Assert.Equal(expected.X, actual.X, 8); Assert.Equal(expected.Y, actual.Y, 8); Assert.Equal(expected.Width, actual.Width, 8); Assert.Equal(expected.Height, actual.Height, 8); }
    private static ObservableCollection<Row> Rows() => [new("r0", "First", 10), new("r1", "Second", 20), new("r2", "Third", 30), new("r3", "Fourth", 40)];
    private static ChartBase Create(bool engineering, IEnumerable rows) { ChartBase chart = engineering ? new EngineeringProbe() : new AnalysisProbe(); Source(chart, rows); return chart; }
    private static void Source(ChartBase chart, IEnumerable rows) { if (chart is EngineeringChartBase e) e.ItemsSource = rows; else ((AnalysisChartBase)chart).ItemsSource = rows; }
    private static IReadOnlyList<ChartDataPoint> Selected(ChartBase chart) => chart is EngineeringChartBase e ? e.GetSelectedData() : ((AnalysisChartBase)chart).GetSelectedData();
    private static IReadOnlyList<ChartDataPoint> ReadOnlySelected(ChartBase chart) => chart is EngineeringChartBase e ? e.SelectedData : ((AnalysisChartBase)chart).SelectedData;
    private static IReadOnlyList<object>? Keys(ChartBase chart) => chart is EngineeringChartBase e ? e.SelectedDataKeys : ((AnalysisChartBase)chart).SelectedDataKeys;
    private static void SetKeys(ChartBase chart, IReadOnlyList<object> keys) { if (chart is EngineeringChartBase e) e.SelectedDataKeys = keys; else ((AnalysisChartBase)chart).SelectedDataKeys = keys; }
    private static Rect? Range(ChartBase chart) => chart is EngineeringChartBase e ? e.DataSelectionRange : ((AnalysisChartBase)chart).DataSelectionRange;
    private static void SelectRange(ChartBase chart, Rect? range) { if (chart is EngineeringChartBase e) e.SelectDataRange(range); else ((AnalysisChartBase)chart).SelectDataRange(range); }
    private static void Clear(ChartBase chart) { if (chart is EngineeringChartBase e) e.ClearDataSelection(); else ((AnalysisChartBase)chart).ClearDataSelection(); }
    private static void Subscribe(ChartBase chart, EventHandler handler) { if (chart is EngineeringChartBase e) e.DataSelectionChanged += handler; else ((AnalysisChartBase)chart).DataSelectionChanged += handler; }
    private static int SingleIndex(ChartBase chart) => chart is EngineeringChartBase e ? e.SelectedIndex : ((AnalysisChartBase)chart).SelectedIndex;
    private static Rect Plot(ChartBase chart) => chart is EngineeringChartBase e ? e.PlotArea : ((AnalysisChartBase)chart).PlotArea;
    private static Point Anchor(ChartBase chart) { var plot = Plot(chart); return new(plot.Left + plot.Width * .2, plot.Top + plot.Height * .2); }
    private sealed class RangeModel(ChartRange range) : System.ComponentModel.INotifyPropertyChanged
    {
        private ChartRange _range = range; public ChartRange Range { get => _range; set { _range = value; PropertyChanged?.Invoke(this, new(nameof(Range))); } }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed record Row(string Id, string Label, double Value);
    private sealed class EngineeringProbe : EngineeringChartBase
    {
        public EngineeringProbe() { IdPath = "Id"; LabelPath = "Label"; ObservePaths("Value"); ShowGrid = false; }
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => data.Points.Select((point, index) =>
        {
            var rectangle = Box(index, plot); var members = index == 0 && data.Count > 3 ? Array.AsReadOnly(new[] { point, data.Points[3] }) : Array.AsReadOnly(new[] { point });
            return new EngineeringMarkInfo(members, new RectangleGeometry(rectangle), Brushes.Blue, rectangle.Center);
        }).ToArray();
    }
    private sealed class AnalysisProbe : AnalysisChartBase
    {
        public AnalysisProbe() { IdPath = "Id"; CategoryPath = "Label"; ValuePath = "Value"; }
        protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => data.Points.Select((point, index) =>
        { var rectangle = Box(index, plot); return new AnalysisMarkInfo(point, new RectangleGeometry(rectangle), Brushes.Blue, rectangle.Center); }).ToArray();
    }
    private static Rect Box(int index, Rect plot) => index switch
    {
        0 => new(plot.Left + plot.Width * .1, plot.Top + plot.Height * .1, plot.Width * .2, plot.Height * .2),
        1 => new(plot.Left + plot.Width * .1, plot.Top + plot.Height * .6, plot.Width * .2, plot.Height * .2),
        2 => new(plot.Left + plot.Width * .6, plot.Top + plot.Height * .1, plot.Width * .2, plot.Height * .2),
        _ => new(plot.Right + 20, plot.Top + 40, 30, 20),
    };
    private sealed class CountedAggregateProbe : EngineeringChartBase
    {
        internal readonly Row[] Rows = Enumerable.Range(0, 1000).Select(index => new Row($"row{index}", $"Row {index}", index)).ToArray();
        internal CountedMembers Members { get; }
        internal int BuildCalls { get; private set; }
        internal CountedAggregateProbe()
        {
            Members = new(Rows.Select((row, index) => new ChartDataPoint(index, row.Id, row, row.Label, new(index, row.Value), false)).ToArray());
            ShowGrid = false;
        }
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
        { BuildCalls++; return [new(Members, new RectangleGeometry(new Rect(plot.Center.X - 30, plot.Center.Y - 20, 60, 40)), Brushes.Blue, plot.Center)]; }
    }
    private sealed class CountedMembers(ChartDataPoint[] points) : IReadOnlyList<ChartDataPoint>
    {
        internal int CompletedEnumerations { get; private set; }
        public int Count => points.Length; public ChartDataPoint this[int index] => points[index];
        public IEnumerator<ChartDataPoint> GetEnumerator() { foreach (var point in points) yield return point; CompletedEnumerations++; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class RawAggregateProbe : EngineeringChartBase
    {
        private readonly IReadOnlyList<Row> _rows;
        internal RawAggregateProbe(IReadOnlyList<Row> rows) { _rows = rows; IdPath = "Id"; ItemsSource = rows; ShowGrid = false; }
        protected override ChartDataSnapshot CreateEngineeringSnapshot() => new(0, _rows.Select((row, index) =>
            new ChartDataPoint(index, row.Id, row, row.Label, new(index, row.Value), false)).ToArray());
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => data.Points.Select((point, index) =>
        {
            var rectangle = Box(index, plot); var members = index == 0 ? Array.AsReadOnly(new[] { point, data.Points[3], point }) : Array.AsReadOnly(new[] { point });
            return new EngineeringMarkInfo(members, new RectangleGeometry(rectangle), Brushes.Blue, rectangle.Center);
        }).ToArray();
    }
    private sealed class DomainProbe(params object[] keys) : EngineeringChartBase
    {
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => keys.Select((key, index) =>
        {
            var rectangle = new Rect(plot.Left + index * 60, plot.Top + 40, 40, 40);
            var member = new ChartDataPoint(index, "same", new object(), null, new(index, index), false);
            return new EngineeringMarkInfo(Array.AsReadOnly(new[] { member }), new RectangleGeometry(rectangle), Brushes.Blue, rectangle.Center) { Key = key };
        }).ToArray();
    }
    private sealed class LineProbe : EngineeringChartBase
    {
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) =>
        [new(Array.AsReadOnly(new[] { new ChartDataPoint(0, "line", new object(), null, new(0, 1), false) }),
             new PolylineGeometry(new[] { new Point(plot.Left + 20, plot.Top + 30), new Point(plot.Right - 20, plot.Top + 30) }, false), null, plot.Center) { Stroke = Brushes.Blue }];
    }
    private sealed class CountingRows(ObservableCollection<Row> rows) : IEnumerable, INotifyCollectionChanged
    { public int Enumerations { get; private set; } public IEnumerator GetEnumerator() { Enumerations++; return rows.GetEnumerator(); } public event NotifyCollectionChangedEventHandler? CollectionChanged { add => rows.CollectionChanged += value; remove => rows.CollectionChanged -= value; } }
    private sealed class Host : IDisposable
    {
        private readonly ChartBase _chart; private readonly Control _content;
        internal Window Window { get; }
        internal Host(ChartBase chart, Control? content = null) { _chart = chart; _content = content ?? chart; Window = new() { Content = _content, Width = 640, Height = 480 }; Window.Show(); Drain(); }
        internal void Drain() { Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        internal void Move(Point point) { Window.MouseMove(_chart.TranslatePoint(point, Window) ?? point); Drain(); }
        internal void Click(Point point) { var target = _chart.TranslatePoint(point, Window) ?? point; Window.MouseDown(target, MouseButton.Left); Window.MouseUp(target, MouseButton.Left); Drain(); }
        internal void Drag(Point from, Point to) { Window.MouseDown(from, MouseButton.Left); Window.MouseMove(to); Window.MouseUp(to, MouseButton.Left); Drain(); }
        internal void Key(Key key) { Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null); Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null); Drain(); }
        internal void Detach() { Window.Content = null; Drain(); }
        internal void Attach() { Window.Content = _content; Drain(); }
        public void Dispose() { Window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
