using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartAccessibilityCompletionTests
{
    [AvaloniaFact]
    public void Unlaid_summary_is_explicit_and_does_not_enumerate_sources_or_create_peers()
    {
        var source = new CountingSource(100_000);
        var series = new LineSeries { ItemsSource = source };
        var chart = new CartesianChart { Title = "Source counts" };
        chart.Series.Add(series);
        var enumerations = source.Enumerations;
        var summary = chart.AccessibilitySummary;
        Assert.True(summary.IsLayoutPending); Assert.False(summary.IsDataComplete);
        Assert.False(Assert.Single(summary.Series).IsDataAvailable);
        Assert.Contains("data=pending", chart.AccessibilitySummaryText);
        Assert.Null(ControlAutomationPeer.FromElement(chart));
        Assert.Equal(enumerations, source.Enumerations);
    }

    [AvaloniaFact]
    public void Counts_distinguish_original_missing_from_filled_and_remaining_invalid_values()
    {
        var series = new LineSeries { Title = "Filled", ItemsSource = new double?[] { 2, null, 4 }, EmptyPointMode = EmptyPointMode.Zero };
        using var host = new Fixture(series);
        var summary = host.Chart.AccessibilitySummary;
        Assert.False(summary.IsLayoutPending); Assert.True(summary.IsDataComplete);
        var counts = Assert.Single(summary.Series);
        Assert.Equal(3, counts.TotalCount); Assert.Equal(3, counts.ValidCount);
        Assert.Equal(1, counts.MissingCount); Assert.Equal(0, counts.InvalidCount);
        Assert.Equal(2, summary.Axes.Count);
        Assert.Same(summary, host.Chart.AccessibilitySummary);
        Assert.Equal(summary.Text, host.Chart.AccessibilitySummaryText);
        Assert.Equal(summary.Text, Assert.IsAssignableFrom<IValueProvider>(host.Peer).Value);
    }

    [AvaloniaFact]
    public void Hidden_counts_are_reused_only_for_the_same_source_version()
    {
        var series = new LineSeries { Title = "Samples", ItemsSource = new ChartPoint[] { new(0, 1), new(1, double.NaN), new(2, 3) } };
        using var host = new Fixture(series);
        Assert.Equal(3, host.Chart.AccessibilitySummary.TotalCount);
        series.IsVisible = false; host.Layout();
        var hidden = Assert.Single(host.Chart.AccessibilitySummary.Series);
        Assert.True(hidden.IsDataAvailable); Assert.Equal(3, hidden.TotalCount); Assert.False(hidden.IsVisible);
        series.ItemsSource = new ChartPoint[] { new(3, 4) }; host.Layout();
        Assert.False(Assert.Single(host.Chart.AccessibilitySummary.Series).IsDataAvailable);
        Assert.False(host.Chart.AccessibilitySummary.IsDataComplete);
        series.IsVisible = true; host.Layout();
        Assert.Equal(1, host.Chart.AccessibilitySummary.TotalCount);
        Assert.True(host.Chart.AccessibilitySummary.IsDataComplete);
    }

    [AvaloniaFact]
    public void Summary_does_not_reenumerate_or_invalidate_layout_when_observed_repeatedly()
    {
        var source = new CountingSource(1000);
        using var host = new Fixture(new LineSeries { ItemsSource = source });
        _ = host.Chart.AccessibilitySummary;
        var enumerations = source.Enumerations;
        var layout = host.Chart.PlotArea;
        for (var index = 0; index < 50; index++) { _ = host.Chart.AccessibilitySummaryText; _ = host.Peer.Value; }
        Assert.Equal(enumerations, source.Enumerations);
        Assert.Equal(layout, host.Chart.PlotArea);
        Assert.True(host.Chart.IsMeasureValid); Assert.True(host.Chart.IsArrangeValid);
    }

    [AvaloniaFact]
    public void Series_children_expose_only_one_source_page_and_invokable_page_controls()
    {
        var series = new ScatterSeries { Title = "Paged", ItemsSource = Enumerable.Range(0, 150).Select(index => new ChartPoint(index, index)).ToArray() };
        using var host = new Fixture(series);
        Assert.Equal(0, host.Peer.CachedPointPeerCount);
        var group = Assert.Single(host.Peer.GetChildren());
        Assert.True(group.IsControlElement()); Assert.True(group.IsContentElement());
        Assert.Equal(0, host.Peer.CachedPointPeerCount);
        var children = group.GetChildren();
        Assert.Equal(64, children.OfType<ChartPointAutomationPeer>().Count()); Assert.Equal(65, children.Count);
        Assert.Equal(64, host.Peer.CachedPointPeerCount);
        var next = children.Last(); Assert.StartsWith("Next", next.GetName());
        Assert.True(next.IsControlElement()); Assert.True(next.IsContentElement());
        next.GetProvider<IInvokeProvider>()!.Invoke();
        children = group.GetChildren();
        Assert.Equal(66, children.Count); Assert.Equal(64, children.OfType<ChartPointAutomationPeer>().First().CurrentPoint!.Index);
        Assert.StartsWith("Previous", children.First().GetName());
        children.First().GetProvider<IInvokeProvider>()!.Invoke();
        Assert.Equal(0, group.GetChildren().OfType<ChartPointAutomationPeer>().First().CurrentPoint!.Index);
    }

    [AvaloniaFact]
    public void Invalid_rows_are_skipped_within_the_page_without_filling_it_from_later_rows()
    {
        var values = Enumerable.Range(0, 70).Select(index => new ChartPoint(index, index < 63 ? double.NaN : index)).ToArray();
        var series = new LineSeries { ItemsSource = values };
        using var host = new Fixture(series);
        var page = host.Peer.GetPointPage(series, 0);
        Assert.Single(page); Assert.Equal(63, page[0].CurrentPoint!.Index);
        Assert.Null(host.Peer.GetPointPeer(series, 0));
        Assert.NotNull(host.Peer.GetPointPeer(series, 69));
    }

    [AvaloniaFact]
    public void Point_nodes_report_original_data_formatted_values_bounds_and_read_only_value()
    {
        var item = new Row("first", 2, 4);
        var series = Mapped(new[] { item });
        using var host = new Fixture(series);
        host.Chart.XAxis.Minimum = 0; host.Chart.XAxis.Maximum = 10;
        host.Chart.YAxis.Minimum = 0; host.Chart.YAxis.Maximum = 10;
        host.Chart.XAxis.LabelFormat = "F1"; host.Chart.YAxis.LabelFormat = "F2"; host.Layout();
        var point = Assert.IsType<ChartPointAutomationPeer>(host.Peer.GetPointPeer(series, 0));
        Assert.Same(item, point.CurrentPoint!.Item); Assert.Equal("first", point.CurrentPoint.StableId);
        Assert.Equal(AutomationControlType.DataItem, point.GetAutomationControlType());
        Assert.True(point.IsControlElement()); Assert.True(point.IsContentElement());
        Assert.Contains("X=2.0", point.GetName()); Assert.Contains("Y=4.00", point.GetName());
        Assert.Contains("source index=0", point.GetName()); Assert.True(point.IsEnabled());
        Assert.False(point.IsOffscreen()); Assert.True(point.GetBoundingRectangle().Width >= 4);
        var value = point.GetProvider<IValueProvider>()!; Assert.True(value.IsReadOnly); Assert.Equal(point.GetName(), value.Value);
        Assert.Throws<InvalidOperationException>(() => value.SetValue("10")); Assert.Empty(point.GetChildren());
    }

    [AvaloniaFact]
    public void Point_selection_respects_multiple_selection_limit_and_exact_container_state()
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(0, 0), new(1, 1), new(2, 2) } };
        using var host = new Fixture(series);
        host.Chart.SelectionMode = ChartSelectionMode.Multiple; host.Chart.MaxSelectionCount = 2; host.Layout();
        var first = host.Peer.GetPointPeer(series, 0)!; var second = host.Peer.GetPointPeer(series, 1)!; var last = host.Peer.GetPointPeer(series, 2)!;
        first.Select(); second.AddToSelection();
        Assert.True(first.IsSelected); Assert.True(second.IsSelected); Assert.False(last.IsSelected);
        Assert.Same(host.Peer, first.SelectionContainer);
        var container = host.Peer.GetProvider<ISelectionProvider>()!;
        Assert.True(container.CanSelectMultiple); Assert.False(container.IsSelectionRequired); Assert.Equal(2, container.GetSelection().Count);
        Assert.Throws<InvalidOperationException>(last.AddToSelection);
        first.RemoveFromSelection(); last.AddToSelection();
        Assert.False(first.IsSelected); Assert.True(last.IsSelected); Assert.Equal(2, host.Chart.AccessibilitySummary.SelectedCount);
        second.Select(); Assert.Single(container.GetSelection()); Assert.True(second.IsSelected); Assert.False(last.IsSelected);
    }

    [AvaloniaTheory]
    [InlineData(ChartSelectionMode.Single)]
    [InlineData(ChartSelectionMode.SingleDeselect)]
    public void Single_modes_reject_additive_conflicts_but_select_and_remove_are_idempotent(ChartSelectionMode mode)
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(0, 0), new(1, 1) } };
        using var host = new Fixture(series); host.Chart.SelectionMode = mode; host.Layout();
        var first = host.Peer.GetPointPeer(series, 0)!; var second = host.Peer.GetPointPeer(series, 1)!;
        first.Select(); first.Select(); first.AddToSelection(); Assert.True(first.IsSelected);
        Assert.Throws<InvalidOperationException>(second.AddToSelection);
        first.RemoveFromSelection(); first.RemoveFromSelection(); Assert.False(first.IsSelected);
    }

    [AvaloniaFact]
    public void Selection_cancellation_does_not_suppress_independent_command_invocation()
    {
        var item = new Row("source", 1, 3); var series = Mapped(new[] { item });
        using var host = new Fixture(series);
        var calls = new List<ChartPointHit>(); host.Chart.PointInvokedCommand = new Command(value => calls.Add((ChartPointHit)value!));
        host.Chart.SelectionChanging += (_, args) => args.Cancel = true;
        var point = host.Peer.GetPointPeer(series, 0)!; point.Select(); Assert.False(point.IsSelected);
        point.Invoke(); var commandPoint = Assert.Single(calls);
        Assert.Same(item, commandPoint.Item); Assert.Equal("source", commandPoint.StableId); Assert.Equal(0, commandPoint.Index);
        Assert.Empty(host.Chart.SelectedPoints!);
    }

    [AvaloniaFact]
    public void Selection_callback_replacement_cannot_select_or_invoke_the_removed_identity()
    {
        var series = Mapped(new[] { new Row("old", 1, 2) });
        using var host = new Fixture(series); var point = host.Peer.GetPointPeer(series, 0)!;
        var calls = 0; host.Chart.PointInvokedCommand = new Command(_ => calls++);
        host.Chart.SelectionChanging += (_, _) => series.ItemsSource = new[] { new Row("new", 1, 4) };
        point.Select(); host.Layout();
        Assert.Empty(host.Chart.SelectedPoints!); Assert.Null(point.CurrentPoint);
        Assert.ThrowsAny<Exception>(point.Invoke); Assert.Equal(0, calls);
    }

    [AvaloniaFact]
    public void CanExecute_source_mutation_never_executes_a_stale_command_argument()
    {
        var series = Mapped(new[] { new Row("old", 1, 2) });
        using var host = new Fixture(series); var point = host.Peer.GetPointPeer(series, 0)!;
        var calls = 0;
        host.Chart.PointInvokedCommand = new Command(_ => calls++, _ => { series.ItemsSource = new[] { new Row("new", 1, 4) }; return true; });
        Assert.Throws<InvalidOperationException>(point.Invoke); host.Layout(); Assert.Equal(0, calls); Assert.Null(point.CurrentPoint);
    }

    [AvaloniaFact]
    public void Disabled_chart_and_selection_none_gate_selection_without_hiding_readable_values()
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(0, 1) } };
        using var host = new Fixture(series); var point = host.Peer.GetPointPeer(series, 0)!;
        host.Chart.SelectionMode = ChartSelectionMode.None; host.Layout();
        Assert.True(point.IsEnabled()); Assert.Throws<InvalidOperationException>(point.Select);
        host.Chart.IsEnabled = false; host.Layout();
        Assert.False(point.IsEnabled()); Assert.ThrowsAny<Exception>(point.Invoke); Assert.Contains("Y=", point.Value);
    }

    [AvaloniaFact]
    public void Stable_id_peer_migrates_across_replacement_and_small_reordering()
    {
        var items = new ObservableCollection<Row> { new("a", 0, 1), new("b", 1, 2), new("c", 2, 3) };
        var series = Mapped(items); using var host = new Fixture(series);
        var point = host.Peer.GetPointPeer(series, 1)!; var id = point.GetAutomationId();
        var replacement = new Row("b", 1, 8); items[1] = replacement; items.Move(1, 2); host.Layout();
        Assert.Same(replacement, point.CurrentPoint!.Item); Assert.Equal(2, point.CurrentPoint.Index);
        Assert.Same(point, host.Peer.GetPointPeer(series, 2)); Assert.Equal(id, point.GetAutomationId());
    }

    [AvaloniaFact]
    public void Reference_identity_never_rebinds_equal_values_from_a_replacement_source()
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(1, 2) } };
        using var host = new Fixture(series); var old = host.Peer.GetPointPeer(series, 0)!;
        series.ItemsSource = new ChartPoint[] { new(1, 2) }; host.Layout();
        Assert.Null(old.CurrentPoint); var current = host.Peer.GetPointPeer(series, 0)!;
        Assert.NotSame(old, current); Assert.NotEqual(old.GetAutomationId(), current.GetAutomationId());
    }

    [AvaloniaFact]
    public void Hidden_points_are_unavailable_and_recover_while_removed_or_invalid_points_are_retired()
    {
        var series = Mapped(new[] { new Row("a", 1, 2) });
        using var host = new Fixture(series); var point = host.Peer.GetPointPeer(series, 0)!;
        series.IsVisible = false; host.Layout(); Assert.Null(point.CurrentPoint); Assert.True(point.IsOffscreen()); Assert.False(point.IsEnabled());
        series.IsVisible = true; host.Layout(); Assert.Same(point, host.Peer.GetPointPeer(series, 0));
        series.ItemsSource = new[] { new Row("a", 1, double.NaN) }; host.Layout();
        Assert.Null(point.CurrentPoint); Assert.Null(host.Peer.GetPointPeer(series, 0)); Assert.Equal(0, host.Peer.CachedPointPeerCount);
        host.Chart.Series.Remove(series); host.Layout(); Assert.Empty(host.Peer.GetChildren()); Assert.Equal(0, host.Peer.CachedSeriesPeerCount);
    }

    [AvaloniaFact]
    public void Viewport_clipping_reports_offscreen_without_disabling_a_valid_source_item()
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(0, 0), new(5, 5), new(10, 10) } };
        using var host = new Fixture(series); var point = host.Peer.GetPointPeer(series, 0)!;
        host.Chart.Viewport = new(4, 8, 4, 8); host.Layout();
        Assert.NotNull(point.CurrentPoint); Assert.True(point.IsEnabled()); Assert.True(point.IsOffscreen()); Assert.Equal(default, point.GetBoundingRectangle());
    }

    [AvaloniaFact]
    public void Large_source_pages_keep_a_bounded_peer_cache_and_reuse_no_global_identity_index()
    {
        var source = new CountingSource(1_000_000); var series = new LineSeries { ItemsSource = source };
        using var host = new Fixture(series); var enumerations = source.Enumerations;
        Assert.Equal(0, host.Peer.CachedPointPeerCount);
        for (var start = 0; start < 1536; start += 256)
        { Assert.Equal(256, host.Peer.GetPointPage(series, start, 256).Count); Assert.InRange(host.Peer.CachedPointPeerCount, 1, 256); }
        Assert.Equal(256, host.Peer.CachedPointPeerCount); Assert.Equal(enumerations, source.Enumerations);
        Assert.Null(host.Peer.GetPointPeer(series, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => host.Peer.GetPointPage(series, 0, 257));
        Assert.Throws<ArgumentOutOfRangeException>(() => host.Peer.GetPointPage(series, -1));
    }

    [AvaloniaFact]
    public void Far_moved_identity_is_explicitly_stale_without_allocating_a_new_identity_index()
    {
        var items = Enumerable.Range(0, 600).Select(index => new Row(index.ToString(), index, index)).ToArray();
        var series = Mapped(items); using var host = new Fixture(series);
        var point = host.Peer.GetPointPeer(series, 0)!;
        var next = items.Skip(1).Append(items[0]).ToArray(); series.ItemsSource = next; host.Layout();
        var snapshot = series.CreateSnapshot();
        Assert.False(snapshot.TryFindExistingIdentity("0", out _, true));
        Assert.Null(point.CurrentPoint); Assert.False(point.IsEnabled());
        Assert.False(snapshot.TryFindExistingIdentity("0", out _, true));
        Assert.Same(items[0], host.Peer.GetPointPeer(series, 599)!.CurrentPoint!.Item);
    }

    [AvaloniaFact]
    public void Existing_identity_index_can_resolve_a_far_moved_item_without_building_another_index()
    {
        var items = Enumerable.Range(0, 600).Select(index => new Row(index.ToString(), index, index)).ToArray();
        var series = Mapped(items); using var host = new Fixture(series); var point = host.Peer.GetPointPeer(series, 0)!;
        series.ItemsSource = items.Skip(1).Append(items[0]).ToArray();
        var snapshot = series.CreateSnapshot(); Assert.True(snapshot.TryFindIdentity("0", out _, true));
        host.Layout(); Assert.Equal(599, point.CurrentPoint!.Index); Assert.Same(point, host.Peer.GetPointPeer(series, 599));
    }

    [AvaloniaFact]
    public void Evicted_point_peers_are_collectible_and_source_replacement_releases_old_items()
    {
        var series = Mapped(Enumerable.Range(0, 700).Select(index => new Row(index.ToString(), index, index)).ToArray());
        using var host = new Fixture(series);
        var weak = MakePeerWeakReference(host, series);
        _ = host.Peer.GetPointPage(series, 300, 256);
        Collect(); Assert.False(weak.IsAlive);
        var itemWeak = ReplaceSourceAndGetOldItemWeakReference(host, series);
        Collect(); Assert.False(itemWeak.IsAlive);
    }

    [AvaloniaFact]
    public void Browse_state_updates_summary_value_and_virtual_focus_while_selection_stays_independent()
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(0, 1), new(1, 2) } };
        using var host = new Fixture(series); host.Chart.IsPointBrowsingEnabled = true; host.Chart.IsKeyboardNavigationEnabled = true;
        var point = host.Peer.GetPointPeer(series, 1)!; var changes = new List<string>();
        host.Peer.PropertyChanged += (_, args) => { if (args.Property == ValuePatternIdentifiers.ValueProperty) changes.Add((string)args.NewValue!); };
        point.SetFocus();
        Assert.True(point.HasKeyboardFocus()); Assert.Equal(1, host.Chart.CurrentPoint!.Index); Assert.Null(host.Chart.SelectedPoint);
        Assert.Contains("current:", host.Peer.Value); Assert.Contains(changes, text => text.Contains("source index=1", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Reentrant_summary_and_point_callbacks_leave_latest_source_and_selection_authoritative()
    {
        var series = Mapped(new[] { new Row("a", 0, 1), new Row("b", 1, 2) });
        using var host = new Fixture(series); var point = host.Peer.GetPointPeer(series, 0)!;
        var replaced = false;
        host.Chart.PropertyChanged += (_, args) =>
        {
            if (args.Property != CartesianChart.AccessibilitySummaryProperty || replaced) return;
            replaced = true; series.ItemsSource = new[] { new Row("b", 1, 8) };
        };
        point.Select(); host.Layout();
        Assert.True(replaced); Assert.Null(point.CurrentPoint); Assert.Empty(host.Chart.SelectedPoints!);
        Assert.Equal(1, host.Chart.AccessibilitySummary.TotalCount); Assert.Equal(host.Chart.AccessibilitySummary.Text, host.Chart.AccessibilitySummaryText);
    }

    [AvaloniaFact]
    public void Point_value_and_name_notifications_describe_updates_and_retirement()
    {
        var series = Mapped(new[] { new Row("stable", 1, 2) }); using var host = new Fixture(series);
        var point = host.Peer.GetPointPeer(series, 0)!;
        var names = new List<string>(); var values = new List<string>();
        point.PropertyChanged += (_, args) =>
        {
            if (args.Property == AutomationElementIdentifiers.NameProperty) names.Add((string)args.NewValue!);
            if (args.Property == ValuePatternIdentifiers.ValueProperty) values.Add((string)args.NewValue!);
        };
        series.ItemsSource = new[] { new Row("stable", 1, 9) }; host.Layout();
        Assert.Same(point, host.Peer.GetPointPeer(series, 0));
        Assert.Contains(names, text => text.Contains("Y=9", StringComparison.Ordinal));
        Assert.Contains(values, text => text.Contains("Y=9", StringComparison.Ordinal));
        series.ItemsSource = Array.Empty<Row>(); host.Layout();
        Assert.Contains("unavailable", values); Assert.False(point.IsEnabled());
        Assert.ThrowsAny<Exception>(point.Invoke);
    }

    [AvaloniaFact]
    public void Selection_container_returns_all_explicit_selected_items_beyond_the_peer_cache_limit()
    {
        var series = new LineSeries { ItemsSource = Enumerable.Range(0, 300).Select(index => new ChartPoint(index, index)).ToArray() };
        using var host = new Fixture(series); host.Chart.SelectionMode = ChartSelectionMode.Multiple; host.Chart.MaxSelectionCount = 400; host.Layout();
        var selection = host.Chart.SelectRectangle(host.Chart.PlotArea);
        Assert.Equal(300, selection.SelectedCount);
        var nodes = host.Peer.GetSelection(); Assert.Equal(300, nodes.Count);
        Assert.All(nodes, point => Assert.True(point.GetProvider<ISelectionItemProvider>()!.IsSelected));
        Assert.Equal(256, host.Peer.CachedPointPeerCount);
        Assert.Equal(300, nodes.Select(point => point.GetAutomationId()).Distinct().Count());
    }

    [AvaloniaFact]
    public void Snapshot_accessibility_lookup_never_creates_a_source_or_identity_index()
    {
        var identities = Enumerable.Range(0, 600).Select(_ => new object()).ToArray();
        var points = Enumerable.Range(0, 600).Reverse().Select(index => new ChartDataPoint(index, identities[index], null, null, new(index, index), false)).ToArray();
        var snapshot = new ChartDataSnapshot(0, points);
        Assert.False(snapshot.TryGetPointWithoutIndex(0, out _));
        Assert.False(snapshot.TryFindExistingIdentity(identities[0], out _));
        Assert.True(snapshot.TryGetPoint(0, out var indexed)); Assert.Equal(0, indexed.SourceIndex);
        Assert.True(snapshot.TryGetPointWithoutIndex(0, out var reused)); Assert.Equal(indexed, reused);
        Assert.False(snapshot.TryFindExistingIdentity(identities[0], out _));
        Assert.True(snapshot.TryFindIdentity(identities[0], out _));
        Assert.True(snapshot.TryFindExistingIdentity(identities[0], out reused)); Assert.Equal(indexed, reused);
    }

    [AvaloniaFact]
    public void Root_children_change_only_for_series_membership_and_order_not_value_or_cursor_reads()
    {
        var first = new LineSeries { Title = "First", ItemsSource = new ChartPoint[] { new(0, 0), new(1, 1) } };
        var last = new ScatterSeries { Title = "Last", ItemsSource = new ChartPoint[] { new(0, 0), new(1, 1) } };
        using var host = new Fixture(first, last);
        host.Chart.IsPointBrowsingEnabled = true; host.Chart.IsKeyboardNavigationEnabled = true; host.Layout();
        var children = host.Peer.GetChildren(); Assert.Equal(2, children.Count);
        var changes = 0; host.Peer.ChildrenChanged += (_, _) => changes++;
        for (var index = 0; index < 10; index++)
        {
            _ = host.Peer.Value; _ = host.Chart.AccessibilitySummary; _ = host.Chart.PlotArea;
            Assert.True(host.Chart.NavigateToPoint(first, index % 2));
            _ = host.Peer.GetChildren()[0].GetChildren();
        }
        host.Peer.GetPointPeer(first, 0)!.Select();
        Assert.Equal(0, changes);
        var added = new BarSeries { Title = "Added" }; host.Chart.Series.Add(added);
        Assert.Equal(1, changes); Assert.Equal("Added", host.Peer.GetChildren()[2].GetName());
        host.Chart.Series.Move(0, 2);
        Assert.Equal(2, changes); Assert.Same(children[0], host.Peer.GetChildren()[2]);
        host.Chart.Series.Remove(last);
        Assert.Equal(3, changes); Assert.Equal(2, host.Peer.GetChildren().Count);
        Assert.Same(children[0], host.Peer.GetChildren()[1]);
    }

    [AvaloniaTheory]
    [InlineData(10, 0, 10)]
    [InlineData(130, 128, 2)]
    [InlineData(0, 0, 0)]
    public void Source_shrink_normalizes_only_a_published_page_to_its_last_aligned_start(int count, int expectedStart, int expectedPoints)
    {
        var series = new LineSeries { ItemsSource = PagePoints(2000) }; using var host = new Fixture(series);
        var group = Assert.IsType<ChartSeriesAutomationPeer>(Assert.Single(host.Peer.GetChildren()));
        _ = host.Peer.GetPointPage(series, 1920);
        Assert.Equal(1920, group.PageStart); Assert.Equal(64, group.GetChildren().OfType<ChartPointAutomationPeer>().Count());
        series.ItemsSource = PagePoints(count);
        Assert.Equal(1920, group.PageStart); // No scene: zero is unknown, rather than an empty source.
        host.Layout();
        Assert.Equal(expectedStart, group.PageStart);
        var children = group.GetChildren(); var points = children.OfType<ChartPointAutomationPeer>().ToArray();
        Assert.Equal(expectedPoints, points.Length);
        if (count > 0) Assert.Equal(expectedStart, points[0].CurrentPoint!.Index);
        Assert.DoesNotContain(children, point => point.GetName().StartsWith("Next", StringComparison.Ordinal));
        Assert.Equal(expectedStart > 0, children.Any(point => point.GetName().StartsWith("Previous", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public void Empty_source_regrowth_starts_with_available_points_and_stale_page_controls_are_disabled()
    {
        var series = new LineSeries { ItemsSource = PagePoints(2000) }; using var host = new Fixture(series);
        var group = Assert.IsType<ChartSeriesAutomationPeer>(Assert.Single(host.Peer.GetChildren()));
        _ = host.Peer.GetPointPage(series, 1920); var oldControls = group.GetChildren().Where(point => point is not ChartPointAutomationPeer).ToArray();
        Assert.Equal(2, oldControls.Length);
        series.ItemsSource = PagePoints(0); host.Layout();
        Assert.Equal(0, group.PageStart); Assert.Empty(group.GetChildren());
        foreach (var control in oldControls)
        { Assert.False(control.IsEnabled()); Assert.ThrowsAny<Exception>(() => control.GetProvider<IInvokeProvider>()!.Invoke()); }
        series.ItemsSource = PagePoints(3); host.Layout();
        var points = group.GetChildren().OfType<ChartPointAutomationPeer>().ToArray();
        Assert.Equal(3, points.Length); Assert.Equal(0, points[0].CurrentPoint!.Index); Assert.Equal(0, group.PageStart);
    }

    [AvaloniaFact]
    public void Hidden_series_keeps_its_page_until_new_visible_data_is_published()
    {
        var series = new LineSeries { ItemsSource = PagePoints(2000) }; using var host = new Fixture(series);
        var group = Assert.IsType<ChartSeriesAutomationPeer>(Assert.Single(host.Peer.GetChildren()));
        _ = host.Peer.GetPointPage(series, 1920);
        series.IsVisible = false; host.Layout();
        Assert.Equal(1920, group.PageStart); Assert.Empty(group.GetChildren());
        series.ItemsSource = PagePoints(10); host.Layout(); Assert.Equal(1920, group.PageStart);
        series.IsVisible = true; Assert.Equal(1920, group.PageStart);
        host.Layout(); Assert.Equal(0, group.PageStart); Assert.Equal(10, group.GetChildren().OfType<ChartPointAutomationPeer>().Count());
    }

    [AvaloniaFact]
    public void Paging_control_identity_is_stable_for_native_sibling_navigation_and_source_refresh()
    {
        var series = new LineSeries { ItemsSource = PagePoints(7) }; using var host = new Fixture(series);
        var group = Assert.IsType<ChartSeriesAutomationPeer>(Assert.Single(host.Peer.GetChildren()));
        _ = host.Peer.GetPointPage(series, 2, 2);
        var before = group.GetChildren(); var previous = before[0]; var next = before[^1];
        Assert.Equal(4, before.Count);
        for (var index = 0; index < 5; index++)
        {
            var current = group.GetChildren();
            Assert.Same(previous, current[0]); Assert.Same(next, current[^1]);
            // Win32's native sibling walker searches this fresh list by reference.
            var ordinal = current.ToList().FindIndex(point => ReferenceEquals(point, previous));
            Assert.Equal(0, ordinal); Assert.IsType<ChartPointAutomationPeer>(current[ordinal + 1]);
        }
        series.ItemsSource = PagePoints(7); host.Layout();
        Assert.Same(previous, group.GetChildren()[0]); Assert.Same(next, group.GetChildren()[^1]);
        series.ItemsSource = PagePoints(0); host.Layout();
        Assert.False(previous.IsEnabled()); Assert.False(next.IsEnabled());
        series.ItemsSource = PagePoints(7); host.Layout();
        Assert.Same(next, group.GetChildren()[^1]);
        next.GetProvider<IInvokeProvider>()!.Invoke();
        var restored = group.GetChildren(); Assert.Same(previous, restored[0]);
        Assert.Equal(2, restored.OfType<ChartPointAutomationPeer>().First().CurrentPoint!.Index);
    }

    [AvaloniaFact]
    public void Root_selection_notifications_invalidate_without_realizing_point_peers_or_duplicate_equivalent_selection()
    {
        var series = new LineSeries { ItemsSource = PagePoints(3) }; using var host = new Fixture(series);
        var notifications = new List<(object? OldValue, object? NewValue)>();
        host.Peer.PropertyChanged += (_, args) =>
        {
            if (args.Property == SelectionPatternIdentifiers.SelectionProperty) notifications.Add((args.OldValue, args.NewValue));
        };
        Assert.True(host.Chart.AccessibilityTryGetPoint(series, 1, out var hit));
        host.Chart.SelectedPoint = hit;
        Assert.Single(notifications); Assert.Equal(0, host.Peer.CachedPointPeerCount);
        host.Chart.SelectedPoint = hit;
        host.Chart.SelectedPoints = Array.AsReadOnly(new[] { hit });
        _ = host.Peer.Value; _ = host.Chart.PlotArea;
        Assert.Single(notifications);
        host.Chart.ClearSelection(); host.Chart.ClearSelection();
        Assert.Equal(2, notifications.Count); Assert.Equal(0, host.Peer.CachedPointPeerCount);
        Assert.All(notifications, notification => { Assert.Null(notification.OldValue); Assert.Null(notification.NewValue); });
    }

    [AvaloniaFact]
    public void Reentrant_root_selection_notification_keeps_latest_selection_and_remains_lazy()
    {
        var series = new LineSeries { ItemsSource = PagePoints(3) }; using var host = new Fixture(series);
        var changes = 0;
        host.Peer.PropertyChanged += (_, args) =>
        {
            if (args.Property != SelectionPatternIdentifiers.SelectionProperty) return;
            changes++;
            if (changes == 1) host.Chart.ClearSelection();
        };
        Assert.True(host.Chart.AccessibilityTryGetPoint(series, 1, out var hit));
        host.Chart.SelectedPoint = hit;
        Assert.Equal(2, changes); Assert.Empty(host.Chart.SelectedPoints!);
        Assert.Null(host.Chart.SelectedPoint); Assert.Equal(0, host.Peer.CachedPointPeerCount);
        Assert.Equal(0, host.Chart.AccessibilitySummary.SelectedCount);
    }

    [AvaloniaFact]
    public void Selection_mode_notifications_report_only_changes_in_multiple_selection_capability()
    {
        using var host = new Fixture(new LineSeries { ItemsSource = PagePoints(3) });
        var changes = new List<(bool Previous, bool Current)>();
        host.Peer.PropertyChanged += (_, args) =>
        {
            if (args.Property == SelectionPatternIdentifiers.CanSelectMultipleProperty)
                changes.Add(((bool)args.OldValue!, (bool)args.NewValue!));
        };
        host.Chart.SelectionMode = ChartSelectionMode.Multiple;
        _ = host.Peer.Value; _ = host.Chart.PlotArea; host.Layout();
        host.Chart.SelectionMode = ChartSelectionMode.SingleDeselect;
        host.Chart.SelectionMode = ChartSelectionMode.Single;
        host.Chart.SelectionMode = ChartSelectionMode.None;
        host.Layout();
        Assert.Equal(new[] { (false, true), (true, false) }, changes);
        Assert.Equal(0, host.Peer.CachedPointPeerCount);
    }

    [AvaloniaFact]
    public async Task Public_summary_point_page_and_providers_reject_background_access()
    {
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(0, 1) } };
        using var host = new Fixture(series); var point = host.Peer.GetPointPeer(series, 0)!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => _ = host.Chart.AccessibilitySummaryText));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => host.Peer.GetPointPage(series, 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(point.Select));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => _ = point.CurrentPoint));
    }

    private static LineSeries Mapped(IEnumerable<Row> source) => new()
    { Title = "Business", ItemsSource = source, IdPath = nameof(Row.Id), CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y) };
    private sealed record Row(string Id, double X, double Y);
    private static ChartPoint[] PagePoints(int count) => Enumerable.Range(0, count).Select(index => new ChartPoint(index, index % 100)).ToArray();
    private sealed class CountingSource(int count) : IEnumerable<ChartPoint>
    {
        public int Enumerations { get; private set; }
        public IEnumerator<ChartPoint> GetEnumerator()
        { Enumerations++; for (var index = 0; index < count; index++) yield return new(index, index % 100); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class Command(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
    {
        public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference MakePeerWeakReference(Fixture host, ChartSeries series) => new(host.Peer.GetPointPeer(series, 0)!);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ReplaceSourceAndGetOldItemWeakReference(Fixture host, LineSeries series)
    {
        var old = host.Peer.GetPointPeer(series, 400)!; var weak = new WeakReference(old.CurrentPoint!.Item!);
        series.ItemsSource = new[] { new Row("new", 0, 1) }; host.Layout(); Assert.Null(old.CurrentPoint); return weak;
    }
    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    private sealed class Fixture : IDisposable
    {
        public CartesianChart Chart { get; } = new();
        private Window Window { get; }
        public ChartAutomationPeer Peer { get; }
        public Fixture(params ChartSeries[] series)
        {
            foreach (var item in series) Chart.Series.Add(item);
            Window = new Window { Width = 800, Height = 600, Content = Chart };
            Window.Show(); Layout();
            Peer = Assert.IsType<ChartAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(Chart));
        }
        public void Layout()
        {
            Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = Chart.PlotArea;
            using (var drawing = new DrawingGroup().Open()) Chart.Render(drawing);
            Dispatcher.UIThread.RunJobs();
        }
        public void Dispose() => Window.Close();
    }
}
