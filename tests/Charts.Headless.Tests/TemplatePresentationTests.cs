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

public sealed class TemplatePresentationTests
{
    [AvaloniaFact]
    public void Null_template_keeps_original_formatter_calls_and_creates_no_overlay()
    {
        var chart = CreateChart();
        var calls = 0;
        chart.TooltipFormatter = _ => { calls++; return "legacy"; };
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        Assert.Null(Overlay(chart));
        var before = calls;
        _ = Record(chart); _ = Record(chart);
        Assert.Equal(before + 2, calls);
        Assert.Null(Overlay(chart));
    }

    [AvaloniaFact]
    public void Template_reuses_its_control_with_fresh_original_identity_and_passive_layout()
    {
        var chart = CreateChart();
        var count = 0;
        var child = new TextBlock { Name = "CustomTooltip", Width = 90, Height = 24 };
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => { count++; return child; });
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        var first = Assert.IsType<ChartTooltipContext>(child.DataContext);
        Assert.Same(fixture.Rows[0], first.Primary.Item);
        Assert.NotNull(first.Primary.StableId);
        Assert.Same(chart.HoveredPoints, first.Hits);
        Assert.Equal(fixture.Point(2, 20), first.Anchor);
        var host = Assert.IsAssignableFrom<Control>(Overlay(chart));
        Assert.False(host.IsHitTestVisible); Assert.False(host.Focusable);
        Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(host));
        Assert.Equal(new Rect(chart.Bounds.Size), host.Bounds);

        fixture.Move(8, 80);
        var second = Assert.IsType<ChartTooltipContext>(child.DataContext);
        Assert.NotSame(first, second);
        Assert.Same(fixture.Rows[1], second.Primary.Item);
        Assert.Equal(1, count);
        Assert.Same(host, Overlay(chart));
        var chrome = Assert.IsType<Border>(child.GetVisualParent());
        Assert.True(chrome.Bounds.Left >= 8 && chrome.Bounds.Top >= 8);
        Assert.True(chrome.Bounds.Right <= chart.Bounds.Width - 8 && chrome.Bounds.Bottom <= chart.Bounds.Height - 8);
        _ = Record(chart); _ = Record(chart);
        Assert.Equal(1, count);
    }

    [AvaloniaFact]
    public void Null_build_suppresses_legacy_box_and_formatter_null_still_supplies_original_hits()
    {
        var chart = CreateChart();
        chart.TooltipBackground = Brushes.HotPink;
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        Assert.True(HasFill(Record(chart), Colors.HotPink));
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => null);
        Assert.Null(Overlay(chart));
        Assert.False(HasFill(Record(chart), Colors.HotPink));
        var child = new TextBlock();
        chart.TooltipFormatter = _ => null;
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => child);
        fixture.Drain();
        var context = Assert.IsType<ChartTooltipContext>(child.DataContext);
        Assert.Empty(context.Text);
        Assert.Same(fixture.Rows[0], Assert.Single(context.Hits).Item);
        chart.TooltipFormatter = _ => "更新后的文本";
        Assert.Equal("更新后的文本", Assert.IsType<ChartTooltipContext>(child.DataContext).Text);
    }

    [AvaloniaFact]
    public void Build_failure_propagates_after_cleanup_and_render_never_retries_it()
    {
        var chart = CreateChart(); chart.TooltipBackground = Brushes.HotPink;
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        var attempts = 0;
        var bad = new FuncDataTemplate<ChartTooltipContext>((_, _) =>
        {
            attempts++; throw new InvalidOperationException("application template failure");
        });
        Assert.Throws<InvalidOperationException>(() => chart.TooltipContentTemplate = bad);
        Assert.Null(Overlay(chart));
        Assert.True(HasFill(Record(chart), Colors.HotPink));
        _ = Record(chart);
        fixture.Move(8, 80);
        Assert.Equal(1, attempts);
        var recovered = new TextBlock();
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => recovered);
        fixture.Drain();
        Assert.Same(fixture.Rows[1], Assert.IsType<ChartTooltipContext>(recovered.DataContext).Primary.Item);
    }

    [AvaloniaFact]
    public void Parented_template_control_keeps_its_original_parent_and_context()
    {
        var chart = CreateChart();
        var applicationContext = new object();
        var child = new TextBlock();
        var owner = new Border { DataContext = applicationContext, Child = child };
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        Assert.Throws<InvalidOperationException>(() => chart.TooltipContentTemplate =
            new FuncDataTemplate<ChartTooltipContext>((_, _) => child));
        Assert.Same(child, owner.Child);
        Assert.Same(owner, child.GetVisualParent());
        Assert.Same(applicationContext, child.DataContext);
        Assert.Null(Overlay(chart));
    }

    [AvaloniaTheory]
    [InlineData("disable")]
    [InlineData("source")]
    [InlineData("template")]
    [InlineData("clear")]
    public void Build_reentrancy_cannot_publish_an_obsolete_control(string action)
    {
        var chart = CreateChart();
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        var obsolete = new TextBlock { Name = "ObsoleteTooltip" };
        var replacement = new TextBlock { Name = "ReplacementTooltip" };
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) =>
        {
            if (action == "disable") chart.IsTooltipEnabled = false;
            else if (action == "source") chart.Series[0].ItemsSource = new ChartPoint[] { new(5, 50) };
            else if (action == "template")
                chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => replacement);
            else chart.ResetViewport();
            return obsolete;
        });
        fixture.Drain();
        Assert.DoesNotContain(chart.GetVisualDescendants(), item => ReferenceEquals(item, obsolete));
        Assert.Null(((ILogical)obsolete).LogicalParent);
        Assert.Null(obsolete.GetVisualParent());
        if (action == "template") Assert.Contains(chart.GetVisualDescendants(), item => ReferenceEquals(item, replacement));
        else Assert.Null(Overlay(chart));
    }

    [AvaloniaFact]
    public void Inherited_context_callback_cannot_commit_a_cleared_child()
    {
        var chart = CreateChart();
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        var child = new TextBlock();
        child.DataContextChanged += (_, _) => { if (child.DataContext is ChartTooltipContext) chart.IsTooltipEnabled = false; };
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => child);
        fixture.Drain();
        Assert.Null(Overlay(chart));
        Assert.Null(child.GetVisualParent());
        Assert.Null(((ILogical)child).LogicalParent);
        Assert.Null(child.DataContext);
    }

    [AvaloniaFact]
    public void Visual_attachment_callback_cannot_leave_an_obsolete_subtree_or_context()
    {
        var chart = CreateChart();
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        var child = new TextBlock();
        child.AttachedToVisualTree += (_, _) => chart.IsTooltipEnabled = false;
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => child);
        fixture.Drain();
        Assert.Null(Overlay(chart));
        Assert.Null(child.GetVisualParent());
        Assert.Null(((ILogical)child).LogicalParent);
        Assert.Null(child.DataContext);
    }

    [AvaloniaFact]
    public void Formatter_reentrancy_clears_old_context_and_never_uses_old_fallback_lines()
    {
        var chart = CreateChart(); chart.TooltipBackground = Brushes.HotPink;
        var child = new TextBlock();
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => child);
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        chart.TooltipFormatter = _ =>
        {
            chart.Series[0].ItemsSource = new ChartPoint[] { new(5, 50) };
            return "obsolete text";
        };
        fixture.Drain();
        Assert.Empty(chart.HoveredPoints);
        Assert.Null(Overlay(chart));
        Assert.Null(child.DataContext);
        Assert.False(HasFill(Record(chart), Colors.HotPink));
    }

    [AvaloniaFact]
    public void Nested_export_during_build_cannot_capture_partial_content()
    {
        var chart = CreateChart();
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        var nestedSucceeded = true;
        using var destination = new MemoryStream();
        var child = new TextBlock();
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) =>
        {
            nestedSucceeded = chart.TryExportPng(destination);
            chart.IsTooltipEnabled = false;
            return child;
        });
        fixture.Drain();
        Assert.False(nestedSucceeded);
        Assert.Equal(0, destination.Length);
        Assert.Null(Overlay(chart));
        Assert.Null(child.DataContext);
    }

    [AvaloniaFact]
    public void Compiled_template_inherits_resources_and_rebinds_original_items_after_viewmodel_replacement()
    {
        var first = new TemplatePresentationModel();
        var view = new TemplatePresentationView { DataContext = first };
        var chart = view.FindControl<CartesianChart>("Chart")!;
        using var fixture = new Fixture(chart, view);
        fixture.Move(2, 20);
        Assert.Equal(first.Title, Find<TextBlock>(chart, "TooltipSeriesTitle").Text);
        Assert.Equal(first.Records[0].Name, Find<TextBlock>(chart, "TooltipOriginalName").Text);
        var root = Find<Border>(chart, "CompiledTooltipContent");
        Assert.Same(first.Records[0], Assert.IsType<ChartTooltipContext>(root.DataContext).Primary.Item);
        view.Resources["TooltipTemplateBrush"] = Brushes.Orange;
        fixture.Drain();
        Assert.Equal(Colors.Orange, Assert.IsAssignableFrom<ISolidColorBrush>(root.Background).Color);
        var second = new TemplatePresentationModel { Title = "替换后的业务", Records = [new(2, 20, "新源项")] };
        view.DataContext = second;
        fixture.Drain();
        Assert.Null(root.DataContext);
        fixture.Move(2, 20);
        Assert.Equal(second.Title, Find<TextBlock>(chart, "TooltipSeriesTitle").Text);
        Assert.Equal("新源项", Find<TextBlock>(chart, "TooltipOriginalName").Text);
        fixture.Window.Content = null;
        fixture.Drain();
        Assert.Null(Overlay(chart));
    }

    [AvaloniaFact]
    public void Replacing_and_clearing_chart_template_preserves_new_overlay_membership()
    {
        var chart = CreateChart();
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => new TextBlock { Text = "template" });
        using var fixture = new Fixture(chart);
        fixture.Move(2, 20);
        chart.Template = new FuncControlTemplate<CartesianChart>((_, _) => new Border());
        fixture.Drain(); fixture.Move(2, 20);
        Assert.NotNull(Overlay(chart));
        chart.Template = null;
        fixture.Drain(); fixture.Move(8, 80);
        var host = Assert.IsAssignableFrom<Control>(Overlay(chart));
        Assert.Same(chart, host.GetVisualParent());
        Assert.Same(chart, ((ILogical)host).LogicalParent);
        Assert.Equal(new Rect(chart.Bounds.Size), host.Bounds);
    }

    private static T Find<T>(CartesianChart chart, string name) where T : Control =>
        Assert.Single(chart.GetVisualDescendants().OfType<T>(), control => control.Name == name);
    private static Control? Overlay(CartesianChart chart) =>
        chart.GetVisualDescendants().OfType<Control>().SingleOrDefault(control => control.Name == "PART_ChartTooltipOverlay");
    private static DrawingGroup Record(CartesianChart chart)
    {
        var group = new DrawingGroup();
        using var drawing = group.Open();
        chart.Render(drawing);
        return group;
    }
    private static bool HasFill(Drawing drawing, Color color) => drawing switch
    {
        GeometryDrawing item => item.Brush is ISolidColorBrush brush && brush.Color == color,
        DrawingGroup group => group.Children.Any(child => HasFill(child, color)),
        _ => false,
    };
    private static CartesianChart CreateChart() => new()
    {
        ShowLegend = false, ShowGridLines = false,
        XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
    };
    private sealed class Fixture : IDisposable
    {
        public Fixture(CartesianChart chart, Control? content = null)
        {
            Chart = chart;
            if (chart.Series.Count == 0)
                chart.Series.Add(new ScatterSeries { ItemsSource = Rows, CategoryPath = "X", ValuePath = "Y", Stroke = Brushes.Blue });
            Window = new Window { Width = 740, Height = 450, Content = new Border { Padding = new Thickness(29, 17, 31, 19), Child = content ?? chart } };
            Window.Show(); Drain();
        }
        public TooltipTemplateRow[] Rows { get; } = [new(2, 20, "first"), new(8, 80, "second")];
        public CartesianChart Chart { get; }
        public Window Window { get; }
        public Point Point(double x, double y)
        {
            var range = Assert.IsType<ChartViewport>(Chart.VisibleRange);
            return new(Chart.PlotArea.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * Chart.PlotArea.Width,
                Chart.PlotArea.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * Chart.PlotArea.Height);
        }
        public void Move(double x, double y)
        {
            Window.MouseMove(Chart.TranslatePoint(Point(x, y), Window)!.Value);
            Drain();
        }
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() => Window.Close();
    }
}
