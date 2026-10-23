using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class PlotAreaContentTests
{
    [AvaloniaFact]
    public void Null_content_and_untemplated_data_do_not_create_a_visual_or_call_build()
    {
        var chart = CreateChart(); var builds = 0;
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => { builds++; return new Border(); });
        using var host = new Host(chart);
        Assert.Null(Overlay(chart)); Assert.Equal(0, builds);
        chart.PlotAreaContentTemplate = null;
        chart.PlotAreaContent = new object(); host.Drain();
        Assert.Null(Overlay(chart)); Assert.Equal(0, builds);
    }

    [AvaloniaFact]
    public void Direct_control_inherits_application_context_and_is_arranged_clipped_and_passive()
    {
        var model = new object(); var child = new Border { Background = Brushes.HotPink };
        var chart = CreateChart(); chart.DataContext = model; chart.PlotAreaContent = child;
        using var host = new Host(chart);
        Assert.Same(model, child.DataContext);
        var overlay = Assert.IsAssignableFrom<Control>(Overlay(chart));
        var clip = Assert.IsType<Border>(child.GetVisualParent());
        Assert.Equal(chart.PlotArea, clip.Bounds);
        Assert.Equal(new Rect(chart.PlotArea.Size), child.Bounds);
        Assert.True(clip.ClipToBounds); Assert.False(overlay.IsHitTestVisible); Assert.False(overlay.Focusable);
        Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(overlay));
        Assert.Equal(new Rect(chart.Bounds.Size), overlay.Bounds);
        Assert.Equal(10, overlay.ZIndex);
        var replacement = new object(); chart.DataContext = replacement; host.Drain();
        Assert.Same(replacement, child.DataContext);
        chart.PlotAreaContent = null; host.Drain();
        Assert.Null(child.DataContext); Assert.Null(child.GetVisualParent()); Assert.Null(((ILogical)child).LogicalParent);
        Assert.Null(Overlay(chart));
    }

    [AvaloniaFact]
    public void Template_receives_fresh_context_and_reuses_control_for_view_axis_layout_and_application_context_changes()
    {
        var chart = CreateChart(); var content = new object(); var model = new object(); var builds = 0;
        var child = new Border(); chart.DataContext = model; chart.PlotAreaContent = content;
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((context, _) =>
        { builds++; Assert.Same(content, context!.Content); return child; });
        using var host = new Host(chart);
        var before = Context(child);
        Assert.Same(model, before.DataContext); Assert.Same(content, before.Content);
        Assert.Equal(chart.PlotArea, before.PlotArea);
        EqualPoint(new(chart.PlotArea.Width / 2, chart.PlotArea.Height / 2), before.Project(50, 50));
        Assert.Equal(new Rect(chart.PlotArea.Size), before.Projection.PlotArea);
        chart.Viewport = new(20, 80, 20, 80); host.Drain();
        var zoomed = Context(child);
        Assert.NotSame(before, zoomed); Assert.Equal(new ChartViewport(20, 80, 20, 80), zoomed.VisibleRange);
        Assert.Equal(new ChartViewport(0, 100, 0, 100), zoomed.Extent);
        EqualPoint(new(0, chart.PlotArea.Height), zoomed.Project(20, 20));
        chart.XAxis.IsInversed = true; host.Drain();
        var inversed = Context(child);
        EqualPoint(new(chart.PlotArea.Width, chart.PlotArea.Height), inversed.Project(inversed.VisibleRange.XMinimum, inversed.VisibleRange.YMinimum));
        var newModel = new object(); chart.DataContext = newModel; host.Drain();
        Assert.Same(newModel, Context(child).DataContext);
        host.Window.Width = 930; host.Drain();
        Assert.Equal(chart.PlotArea, Context(child).PlotArea);
        Assert.Equal(1, builds);
    }

    [AvaloniaFact]
    public void Named_projections_use_active_dimension_unique_axes_and_independent_view_ranges()
    {
        var chart = CreateChart(); var child = new Border(); SetTemplate(chart, child);
        var extraX = new NumericAxis { Key = "secondary-x", Minimum = 100, Maximum = 200, ViewMinimum = 120, ViewMaximum = 180, IsInversed = true };
        var extraY = new LogarithmicAxis { Key = "secondary-y", Minimum = 1, Maximum = 10_000, ViewMinimum = 10, ViewMaximum = 1000 };
        chart.Axes.Add(extraX); chart.Axes.Add(extraY);
        chart.Axes.Add(new NumericAxis { Key = "unused", Minimum = 0, Maximum = 1 });
        chart.Series.Add(new LineSeries { XAxisKey = extraX.Key, YAxisKey = extraY.Key,
            ItemsSource = new ChartPoint[] { new(120, 10), new(180, 1000) } });
        using var host = new Host(chart);
        var context = Context(child);
        Assert.True(context.TryGetProjection(extraX.Key, extraY.Key, out var projection));
        Assert.True(context.TryGetVisibleRange(extraX.Key, extraY.Key, out var range));
        Assert.Equal(new ChartViewport(120, 180, 10, 1000), range);
        EqualPoint(new(chart.PlotArea.Width / 2, chart.PlotArea.Height / 2), projection.Project(new(150, 100)));
        var original = projection.Unproject(projection.Project(new(135, 50)));
        Assert.Equal(135, original.X, 8); Assert.Equal(50, original.Y, 8);
        Assert.False(context.TryGetProjection("unused", null, out _));
        Assert.False(context.TryGetProjection(extraY.Key, extraX.Key, out _));
        Assert.False(context.TryGetVisibleRange("missing", null, out _));
        extraY.ViewMinimum = 100; extraY.ViewMaximum = 10_000; host.Drain();
        Assert.True(Context(child).TryGetVisibleRange(extraX.Key, extraY.Key, out range));
        Assert.Equal(new ChartViewport(120, 180, 100, 10_000), range);
        Assert.Equal(new ChartViewport(120, 180, 10, 1000), context.TryGetVisibleRange(extraX.Key, extraY.Key, out var oldRange) ? oldRange : default);
    }

    [AvaloniaFact]
    public void Ambiguous_named_primary_axis_is_rejected_even_when_duplicate_axis_is_not_used_by_a_series()
    {
        var chart = CreateChart(); chart.XAxis.Key = "duplicate";
        chart.Axes.Add(new NumericAxis { Key = "duplicate", Minimum = 0, Maximum = 100 });
        var child = new Border(); SetTemplate(chart, child);
        using var host = new Host(chart);
        Assert.False(Context(child).TryGetProjection("duplicate", null, out _));
        Assert.True(Context(child).TryGetProjection(null, null, out _));
    }

    [AvaloniaFact]
    public void Projection_uses_scale_break_validity_and_inverse_mapping()
    {
        var chart = CreateChart(); var child = new Border(); SetTemplate(chart, child);
        chart.XAxis.ScaleBreaks.Add(new ChartScaleBreak { Start = 40, End = 60, CompressionRatio = .2 });
        using var host = new Host(chart);
        var projection = Context(child).Projection;
        Assert.True(double.IsNaN(projection.MapX(50)));
        var point = projection.Project(new(75, 25)); var roundtrip = projection.Unproject(point);
        Assert.Equal(75, roundtrip.X, 8); Assert.Equal(25, roundtrip.Y, 8);
        chart.XAxis.IsInversed = true; host.Drain();
        Assert.Equal(chart.PlotArea.Width - point.X, Context(child).Projection.MapX(75), 8);
    }

    [AvaloniaFact]
    public void Source_and_template_replacement_release_old_control_and_context()
    {
        var chart = CreateChart(); var children = new List<Border>();
        chart.PlotAreaContent = new object();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => { var child = new Border(); children.Add(child); return child; });
        using var host = new Host(chart);
        var first = Assert.Single(children);
        chart.PlotAreaContent = new object(); host.Drain();
        Assert.Equal(2, children.Count); Assert.Null(first.DataContext); Assert.Null(first.GetVisualParent());
        var second = children[1]; var last = new Border();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => last); host.Drain();
        Assert.Null(second.DataContext); Assert.Null(second.GetVisualParent()); Assert.NotNull(last.GetVisualParent());
        chart.PlotAreaContentTemplate = null; host.Drain();
        Assert.Null(last.DataContext); Assert.Null(last.GetVisualParent()); Assert.Null(Overlay(chart));
    }

    [AvaloniaFact]
    public void Null_build_is_cached_until_source_or_template_changes()
    {
        var chart = CreateChart(); var builds = 0; chart.PlotAreaContent = new object();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => { builds++; return null; });
        using var host = new Host(chart);
        Assert.Null(Overlay(chart)); Assert.Equal(1, builds);
        chart.Viewport = new(20, 80, 20, 80); host.Drain();
        Assert.Equal(1, builds);
        chart.PlotAreaContent = new object(); host.Drain(); Assert.Equal(2, builds);
    }

    [AvaloniaTheory]
    [InlineData("source")]
    [InlineData("template")]
    [InlineData("viewport")]
    [InlineData("clear")]
    [InlineData("hide")]
    public void Build_reentrancy_does_not_publish_obsolete_control(string action)
    {
        var chart = CreateChart(); using var host = new Host(chart);
        var obsolete = new Border(); var replacement = new Border(); var first = true;
        chart.PlotAreaContent = new object();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) =>
        {
            if (!first) return replacement;
            first = false;
            if (action == "source") chart.PlotAreaContent = new object();
            else if (action == "template") chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => replacement);
            else if (action == "viewport") chart.Viewport = new(20, 80, 20, 80);
            else if (action == "clear") chart.PlotAreaContent = null;
            else chart.IsVisible = false;
            return obsolete;
        });
        host.Drain();
        Assert.Null(obsolete.GetVisualParent()); Assert.Null(((ILogical)obsolete).LogicalParent); Assert.Null(obsolete.DataContext);
        if (action is "clear" or "hide") Assert.Null(Overlay(chart));
        else { Assert.NotNull(replacement.GetVisualParent()); Assert.DoesNotContain(chart.GetVisualDescendants(), item => ReferenceEquals(item, obsolete)); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Context_and_attachment_callbacks_cannot_leave_orphaned_controls(bool attachment)
    {
        var chart = CreateChart(); using var host = new Host(chart);
        var child = new Border();
        if (attachment) child.AttachedToVisualTree += (_, _) => chart.PlotAreaContent = null;
        else child.DataContextChanged += (_, _) => { if (child.DataContext is ChartPlotAreaContext) chart.PlotAreaContent = null; };
        SetTemplate(chart, child); host.Drain();
        Assert.Null(child.GetVisualParent()); Assert.Null(((ILogical)child).LogicalParent); Assert.Null(child.DataContext);
        Assert.Null(Overlay(chart));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Context_callback_view_reentry_rebuilds_without_reusing_an_empty_stale_container(bool duringUpdate)
    {
        var chart = CreateChart(); using var host = new Host(chart);
        var children = new List<Border>(); var reentered = false; var armed = !duringUpdate;
        chart.PlotAreaContent = new object();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) =>
        {
            var child = new Border(); children.Add(child);
            child.DataContextChanged += (_, _) =>
            {
                if (armed && !reentered && child.DataContext is ChartPlotAreaContext)
                { reentered = true; chart.Viewport = new(30, 70, 30, 70); }
            };
            return child;
        });
        host.Drain();
        if (duringUpdate) { armed = true; chart.Viewport = new(20, 80, 20, 80); host.Drain(); }
        Assert.True(reentered); Assert.Equal(2, children.Count);
        Assert.Null(children[0].GetVisualParent()); Assert.Null(children[0].DataContext);
        Assert.NotNull(children[1].GetVisualParent());
        Assert.Equal(new ChartViewport(30, 70, 30, 70), Context(children[1]).VisibleRange);
    }

    [AvaloniaTheory]
    [InlineData("chart")]
    [InlineData("ancestor")]
    [InlineData("detach")]
    public void Hidden_and_detached_charts_release_context_and_rebuild_on_return(string action)
    {
        var chart = CreateChart(); var children = new List<Border>(); chart.PlotAreaContent = new object();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => { var child = new Border(); children.Add(child); return child; });
        using var host = new Host(chart);
        var before = Assert.Single(children);
        if (action == "chart") chart.IsVisible = false;
        else if (action == "ancestor") host.Container.IsVisible = false;
        else host.Container.Child = null;
        host.Drain();
        Assert.Null(Overlay(chart)); Assert.Null(before.GetVisualParent()); Assert.Null(before.DataContext);
        if (action == "chart") chart.IsVisible = true;
        else if (action == "ancestor") host.Container.IsVisible = true;
        else host.Container.Child = chart;
        host.Drain();
        Assert.Equal(2, children.Count); Assert.NotNull(children[1].GetVisualParent());
    }

    [AvaloniaFact]
    public void Parented_application_control_is_rejected_without_mutating_its_parent_or_context()
    {
        var chart = CreateChart(); using var host = new Host(chart);
        var child = new Border(); var model = new object(); var owner = new Border { DataContext = model, Child = child };
        chart.PlotAreaContent = child;
        Assert.Throws<InvalidOperationException>(() => chart.Measure(new Size(801, 450)));
        Assert.Same(owner, child.GetVisualParent()); Assert.Same(model, child.DataContext); Assert.Same(child, owner.Child);
        Assert.Null(Overlay(chart));
        chart.PlotAreaContent = null;
    }

    [AvaloniaFact]
    public void Build_failure_propagates_once_after_cleanup_and_new_template_recovers()
    {
        var chart = CreateChart(); using var host = new Host(chart);
        var builds = 0; chart.PlotAreaContent = new object();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => { builds++; throw new InvalidOperationException("application content failure"); });
        Assert.Throws<InvalidOperationException>(() => chart.Measure(new Size(802, 450)));
        Assert.Null(Overlay(chart));
        chart.Measure(new Size(802, 450)); Assert.Equal(1, builds);
        var replacement = new Border(); chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => replacement);
        host.Drain(); Assert.NotNull(replacement.GetVisualParent());
    }

    [AvaloniaFact]
    public void Template_callback_export_fails_without_writing_a_partial_control_tree()
    {
        var chart = CreateChart(); using var host = new Host(chart);
        var attempts = 0; ChartExportFailedEventArgs? failure = null; chart.ExportFailed += (_, args) => failure = args;
        var child = new Border(); chart.PlotAreaContent = new object();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) =>
        {
            attempts++; using var destination = new MemoryStream();
            Assert.False(chart.TryExportPng(destination)); Assert.Equal(0, destination.Length);
            return child;
        });
        host.Drain(); Assert.Equal(1, attempts); Assert.IsType<InvalidOperationException>(failure?.Exception);
        Assert.NotNull(child.GetVisualParent());
    }

    [AvaloniaFact]
    public void Export_records_exact_current_passive_subtree_after_scaling_and_preserves_layout()
    {
        var chart = CreateChart(); var child = new Border { Background = Brushes.HotPink }; SetTemplate(chart, child);
        using var host = new Host(chart);
        chart.Viewport = new(20, 80, 20, 80); host.Drain();
        var bounds = chart.Bounds; var plot = chart.PlotArea; var range = chart.Viewport;
        foreach (var dpi in new[] { 96d, 144d, 192d })
        {
            var group = new DrawingGroup(); var layout = chart.GetExportLayout(new() { PixelSize = new(900, 560), Dpi = dpi });
            using (var drawing = group.Open()) chart.RenderExport(drawing, layout, null);
            var brush = Assert.Single(VisualBrushes(group), item => ReferenceEquals(item.Visual, Overlay(chart)));
            Assert.Equal(Stretch.Fill, brush.Stretch);
            Assert.Equal(bounds, chart.Bounds); Assert.Equal(plot, chart.PlotArea); Assert.Equal(range, chart.Viewport);
        }
        chart.PlotAreaContent = null; host.Drain();
        var hidden = new DrawingGroup();
        using (var drawing = hidden.Open()) chart.RenderExport(drawing, chart.GetExportLayout(null), null);
        Assert.Empty(VisualBrushes(hidden));
    }

    [AvaloniaFact]
    public void Offline_export_releases_temporary_content_and_context_even_when_raster_backend_fails()
    {
        var chart = CreateChart(); var children = new List<Border>();
        chart.PlotAreaContent = new object(); chart.DataContext = new object();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) =>
        { var child = new Border(); children.Add(child); return child; });
        chart.Measure(new Size(800, 450)); chart.Arrange(new Rect(0, 0, 800, 450));
        Assert.Empty(children); Assert.Null(Overlay(chart));
        using var output = new MemoryStream(); _ = chart.TryExportPng(output);
        var temporary = Assert.Single(children);
        Assert.Null(temporary.DataContext); Assert.Null(temporary.GetVisualParent()); Assert.Null(((ILogical)temporary).LogicalParent);
        Assert.Null(Overlay(chart));
    }

    [AvaloniaFact]
    public void Offline_exports_rebuild_annotation_axis_membership_and_mutated_axis_views()
    {
        var chart = CreateChart(); var contexts = new List<ChartPlotAreaContext>();
        var first = new NumericAxis { Key = "first-offline", Minimum = 0, Maximum = 1000 };
        var second = new NumericAxis { Key = "second-offline", Minimum = 100, Maximum = 500 };
        chart.Axes.Add(first); chart.Axes.Add(second);
        var annotation = new LineAnnotation { YAxisKey = first.Key, Value = 500 };
        chart.Annotations.Add(annotation); chart.PlotAreaContent = new object();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((context, _) =>
        { contexts.Add(context!); return new Border(); });
        chart.IsLegendFloating = true; chart.ShowLegend = true;
        chart.Measure(new Size(800, 450)); chart.Arrange(new Rect(0, 0, 800, 450));
        using (var output = new MemoryStream()) _ = chart.TryExportPng(output);
        Assert.True(contexts[0].TryGetVisibleRange(null, first.Key, out var firstRange));
        Assert.Equal(0, firstRange.YMinimum); Assert.Equal(1000, firstRange.YMaximum);
        annotation.YAxisKey = second.Key; second.ViewMinimum = 200; second.ViewMaximum = 400;
        using (var output = new MemoryStream()) _ = chart.TryExportPng(output);
        Assert.Equal(2, contexts.Count);
        Assert.False(contexts[1].TryGetProjection(null, first.Key, out _));
        Assert.True(contexts[1].TryGetVisibleRange(null, second.Key, out var secondRange));
        Assert.Equal(200, secondRange.YMinimum); Assert.Equal(400, secondRange.YMaximum);
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Control>(), item =>
            item.Name is "PART_ChartPlotAreaContentOverlay" or "PART_ChartForegroundTooltip" or "PART_ChartLegend");
    }

    [AvaloniaFact]
    public void Default_tooltip_is_above_plot_content_and_export_keeps_the_same_order()
    {
        var chart = CreateChart(); chart.TooltipBackground = Brushes.HotPink;
        chart.PlotAreaContent = new Border { Background = Brushes.Cyan };
        using var host = new Host(chart);
        var content = Assert.IsAssignableFrom<Control>(Overlay(chart));
        var foreground = Find(chart, "PART_ChartForegroundTooltip");
        Assert.True(foreground.ZIndex > content.ZIndex); Assert.False(foreground.IsHitTestVisible);
        host.Window.MouseMove(chart.TranslatePoint(chart.PlotArea.Center, host.Window)!.Value); host.Drain();
        Assert.NotNull(chart.HoveredPoint);
        Assert.False(HasFill(Record(chart), Colors.HotPink)); Assert.True(HasFill(Record(foreground), Colors.HotPink));
        var export = new DrawingGroup();
        using (var drawing = export.Open()) chart.RenderExport(drawing, chart.GetExportLayout(null), null);
        var brushes = VisualBrushes(export);
        Assert.Equal(2, brushes.Count); Assert.Same(content, brushes[0].Visual); Assert.Same(foreground, brushes[1].Visual);
        chart.PlotAreaContent = null; host.Drain();
        Assert.Null(foreground.GetVisualParent()); Assert.Null(((ILogical)foreground).LogicalParent);
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Control>(), item => item.Name == "PART_ChartForegroundTooltip");
        host.Window.MouseMove(chart.TranslatePoint(chart.PlotArea.Center, host.Window)!.Value); host.Drain();
        Assert.True(HasFill(Record(chart), Colors.HotPink));
    }

    [AvaloniaFact]
    public void Floating_legend_moves_above_plot_content_and_returns_to_drawing_when_content_is_cleared()
    {
        var chart = CreateChart(); chart.ShowLegend = true; chart.IsLegendFloating = true;
        chart.LegendPosition = ChartLegendPosition.Right;
        chart.LegendRenderer = (drawing, item) => drawing.DrawRectangle(Brushes.Magenta, null, item.Bounds);
        using var host = new Host(chart);
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Control>(), item => item.Name == "PART_ChartLegend");
        Assert.True(HasFill(Record(chart), Colors.Magenta));
        chart.PlotAreaContent = new object(); host.Drain();
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Control>(), item => item.Name == "PART_ChartLegend");
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => new Border { Background = Brushes.Cyan }); host.Drain();
        var content = Assert.IsAssignableFrom<Control>(Overlay(chart)); var legend = Find(chart, "PART_ChartLegend");
        Assert.True(legend.ZIndex > content.ZIndex); Assert.False(HasFill(Record(chart), Colors.Magenta));
        var export = new DrawingGroup();
        using (var drawing = export.Open()) chart.RenderExport(drawing, chart.GetExportLayout(null), null);
        var brushes = VisualBrushes(export);
        Assert.Equal(2, brushes.Count); Assert.Same(content, brushes[0].Visual); Assert.Same(legend, brushes[1].Visual);
        chart.PlotAreaContentTemplate = null; host.Drain();
        Assert.Null(legend.GetVisualParent()); Assert.Null(((ILogical)legend).LogicalParent);
        Assert.Null(Overlay(chart)); Assert.True(HasFill(Record(chart), Colors.Magenta));
        chart.PlotAreaContent = new Border(); host.Drain();
        legend = Find(chart, "PART_ChartLegend"); Assert.NotNull(Overlay(chart));
        chart.PlotAreaContent = null; host.Drain();
        Assert.Null(legend.GetVisualParent()); Assert.Null(((ILogical)legend).LogicalParent);
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Control>(), item =>
            item.Name is "PART_ChartLegend" or "PART_ChartForegroundTooltip");
    }

    [AvaloniaFact]
    public void Passive_content_does_not_obstruct_wheel_navigation_or_point_selection()
    {
        var chart = CreateChart(); chart.PlotAreaContent = new Border { Background = Brushes.HotPink };
        chart.IsZoomEnabled = true;
        using var host = new Host(chart);
        var local = chart.PlotArea.Center; var position = chart.TranslatePoint(local, host.Window)!.Value;
        host.Window.MouseWheel(position, new Vector(0, 1)); host.Drain();
        Assert.True(chart.Viewport is { } viewport && viewport.XMaximum - viewport.XMinimum < 100);
        host.Window.MouseDown(position, MouseButton.Left); host.Window.MouseUp(position, MouseButton.Left); host.Drain();
        Assert.NotNull(chart.SelectedPoint);
    }

    private static List<VisualBrush> VisualBrushes(Drawing drawing) => drawing switch
    {
        GeometryDrawing { Brush: VisualBrush brush } => [brush],
        DrawingGroup group => group.Children.SelectMany(VisualBrushes).ToList(),
        _ => [],
    };
    private static Control Find(CartesianChart chart, string name) => Assert.Single(chart.GetVisualDescendants().OfType<Control>(), item => item.Name == name);
    private static DrawingGroup Record(Control control)
    { var result = new DrawingGroup(); using (var drawing = result.Open()) control.Render(drawing); return result; }
    private static bool HasFill(Drawing drawing, Color color) => drawing switch
    {
        GeometryDrawing { Brush: ISolidColorBrush brush } => brush.Color == color,
        DrawingGroup group => group.Children.Any(child => HasFill(child, color)),
        _ => false,
    };
    private static ChartPlotAreaContext Context(Control child) => Assert.IsType<ChartPlotAreaContext>(child.DataContext);
    private static Control? Overlay(CartesianChart chart) => chart.GetVisualDescendants().OfType<Control>()
        .SingleOrDefault(control => control.Name == "PART_ChartPlotAreaContentOverlay");
    private static void SetTemplate(CartesianChart chart, Control child)
    {
        chart.PlotAreaContent = new object();
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => child);
    }
    private static CartesianChart CreateChart()
    {
        var chart = new CartesianChart
        {
            ShowLegend = false, ShowGridLines = false,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 100 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
        };
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(0, 0), new(50, 50), new(100, 100) } });
        return chart;
    }
    private static void EqualPoint(Point expected, Point actual)
    { Assert.Equal(expected.X, actual.X, 7); Assert.Equal(expected.Y, actual.Y, 7); }
    private sealed class Host : IDisposable
    {
        public Host(CartesianChart chart)
        {
            Container = new Border { Padding = new Thickness(17, 13, 23, 19), Child = chart };
            Window = new Window { Width = 800, Height = 450, Content = Container }; Window.Show(); Drain();
        }
        public Border Container { get; }
        public Window Window { get; }
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() => Window.Close();
    }
}
