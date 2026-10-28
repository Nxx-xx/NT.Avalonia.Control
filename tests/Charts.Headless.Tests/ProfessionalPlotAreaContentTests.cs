using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ProfessionalPlotAreaContentTests
{
    [AvaloniaFact]
    public void Both_professional_roots_share_the_Cartesian_plot_content_property_identity()
    {
        Assert.Same(CartesianChart.PlotAreaContentProperty, EngineeringChartBase.PlotAreaContentProperty);
        Assert.Same(CartesianChart.PlotAreaContentProperty, AnalysisChartBase.PlotAreaContentProperty);
        Assert.Same(CartesianChart.PlotAreaContentTemplateProperty, EngineeringChartBase.PlotAreaContentTemplateProperty);
        Assert.Same(CartesianChart.PlotAreaContentTemplateProperty, AnalysisChartBase.PlotAreaContentTemplateProperty);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Template_content_has_actual_frame_original_data_local_projection_and_real_passive_membership(bool engineering)
    {
        var rows = new SharedTooltipPresentationModel().Records; var chart = Create(engineering, rows);
        var model = new object(); var data = new object(); chart.DataContext = model;
        chart.FontSize = 19; chart.Foreground = Brushes.Purple; chart.Resources["PlotContentBrush"] = Brushes.Pink;
        var builds = 0; var child = new TextBlock { Text = "Plot note" };
        child.Bind(TextBlock.BackgroundProperty, new DynamicResourceExtension("PlotContentBrush"));
        Set(chart, data, new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => { builds++; return child; }));
        using var host = new Host(chart);
        Assert.Equal(1, builds); var reading = Assert.IsType<ChartDataPlotAreaContext>(child.DataContext);
        Assert.Same(model, reading.DataContext); Assert.Same(data, reading.Content); Assert.Equal(Plot(chart), reading.PlotArea);
        var overlay = Overlay(chart); var border = Item(chart);
        Assert.Equal(new Rect(chart.Bounds.Size), overlay.Bounds); Assert.Equal(reading.PlotArea, border.Bounds);
        Assert.True(border.ClipToBounds); Assert.True(overlay.ClipToBounds); Assert.False(overlay.IsHitTestVisible); Assert.False(border.IsHitTestVisible);
        Assert.False(overlay.Focusable); Assert.Equal(10, overlay.ZIndex);
        Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(overlay));
        Assert.Same(chart, ((ILogical)overlay).LogicalParent); Assert.Same(chart, overlay.GetVisualParent());
        Assert.Equal(19, child.FontSize); Assert.Same(Brushes.Purple, child.Foreground); Assert.Same(Brushes.Pink, child.Background);
        Assert.Equal(new Point(reading.PlotArea.Width / 2, reading.PlotArea.Height / 2), reading.ProjectLayout(.5, .5));
        var local = reading.ProjectLayout(.2, .7); var roundTrip = reading.UnprojectLayout(local);
        Assert.Equal(.2, roundTrip.X, 10); Assert.Equal(.7, roundTrip.Y, 10);
        var localRoundTrip = reading.ToLocal(reading.ToChart(local));
        Assert.Equal(local.X, localRoundTrip.X, 10); Assert.Equal(local.Y, localRoundTrip.Y, 10);
        Assert.Equal(new Rect(0, 0, 1, 1), reading.VisibleLayoutBounds);
        if (engineering)
        {
            Assert.Empty(reading.AnalysisMarks); Assert.Same(((EngineeringChartBase)chart).GetMarks(), reading.EngineeringMarks);
            Assert.Equal(rows.Count, Assert.Single(reading.EngineeringMarks).Members.Count);
            Assert.Same(rows[0], reading.EngineeringMarks[0].Members[0].Item); Assert.Equal(0, reading.EngineeringMarks[0].Members[0].SourceIndex);
        }
        else
        {
            Assert.Empty(reading.EngineeringMarks); Assert.Same(((AnalysisChartBase)chart).GetMarks(), reading.AnalysisMarks);
            Assert.Same(rows[0], reading.AnalysisMarks[0].Data.Item); Assert.Equal(0, reading.AnalysisMarks[0].Data.SourceIndex);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => reading.ProjectLayout(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => reading.UnprojectLayout(new(double.PositiveInfinity, 0)));
        chart.Resources["PlotContentBrush"] = Brushes.Blue; host.Drain(); Assert.Same(Brushes.Blue, child.Background);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Direct_controls_inherit_the_chart_VM_and_plain_objects_require_a_template(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); var vm = new MutableBusiness(); chart.DataContext = vm;
        Set(chart, new object(), null); using var host = new Host(chart); Assert.Null(MaybeItem(chart));
        var child = new TextBlock(); child.Bind(TextBlock.TextProperty, new Binding("Title")); Set(chart, child, null); host.Drain();
        Assert.Same(vm, child.DataContext); Assert.Equal("Business title", child.Text);
        vm.Title = "Updated title"; host.Drain(); Assert.Equal("Updated title", child.Text);
        Set(chart, null, null); Assert.Null(child.GetVisualParent()); host.Drain(); Assert.Null(child.DataContext); Assert.Null(MaybeItem(chart));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Hover_business_source_and_view_updates_reuse_the_control_and_release_old_readings_immediately(bool engineering)
    {
        var rows = new SharedTooltipPresentationModel().Records; var source = new CountingRows(rows); var chart = Create(engineering, source);
        var vm = new MutableBusiness(); chart.DataContext = vm; var builds = 0; var child = new TextBlock();
        child.Bind(TextBlock.TextProperty, new Binding("DataContext.Title"));
        Set(chart, new object(), new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => { builds++; return child; }));
        using var host = new Host(chart); var reads = source.Enumerations; var first = child.DataContext;
        host.Move(); vm.Title = "Updated business"; host.Drain();
        Assert.Equal(1, builds); Assert.Equal(reads, source.Enumerations); Assert.Same(first, child.DataContext); Assert.Equal("Updated business", child.Text);
        rows[0].Value = 17; Assert.Null(child.DataContext); host.Drain(); Assert.Equal(1, builds); Assert.NotSame(first, child.DataContext);
        var reading = Assert.IsType<ChartDataPlotAreaContext>(child.DataContext);
        var members = engineering ? reading.EngineeringMarks.SelectMany(mark => mark.Members) : reading.AnalysisMarks.Select(mark => mark.Data);
        Assert.Contains(members, member => ReferenceEquals(member.Item, rows[0]) && member.Point.Y == 17);
        if (chart is EngineeringChartBase e)
        {
            e.Zoom = 2; e.PanX = .125; e.PanY = -.1; host.Drain(); reading = Assert.IsType<ChartDataPlotAreaContext>(child.DataContext);
            var mark = Assert.Single(reading.EngineeringMarks);
            Assert.Equal(mark.Position, reading.ToChart(reading.ProjectLayout(.5, .5)));
            Assert.Equal(.1875, reading.VisibleLayoutBounds.Left, 10); Assert.Equal(.3, reading.VisibleLayoutBounds.Top, 10);
            Assert.Equal(.5, reading.VisibleLayoutBounds.Width, 10); Assert.Equal(.5, reading.VisibleLayoutBounds.Height, 10);
            Assert.Equal(1, builds);
        }
        var next = new MutableBusiness { Title = "Replacement VM" }; chart.DataContext = next; host.Drain();
        Assert.Equal(1, builds); Assert.Same(next, Assert.IsType<ChartDataPlotAreaContext>(child.DataContext).DataContext); Assert.Equal("Replacement VM", child.Text);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Conditional_null_build_is_stable_until_a_new_reading_and_then_recovers(bool engineering)
    {
        var rows = new SharedTooltipPresentationModel().Records; var chart = Create(engineering, rows); var builds = 0; var show = false;
        var initialFrames = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Set(chart, new object(), new FuncDataTemplate<ChartDataPlotAreaContext>((reading, _) =>
        {
            builds++;
            // First-window layout may publish successive frames. A null template is retried only for a new actual frame.
            Assert.NotNull(reading);
            Assert.True(initialFrames.Add(engineering ? reading.EngineeringMarks : reading.AnalysisMarks));
            return show ? new TextBlock { Text = "Shown" } : null;
        }));
        using var host = new Host(chart); var initialBuilds = builds; Assert.True(initialBuilds >= 1); Assert.Null(MaybeItem(chart));
        host.Move(); host.Drain(); Assert.Equal(initialBuilds, builds);
        show = true; rows[0].Value++; host.Drain(); Assert.Equal(initialBuilds + 1, builds); Assert.NotNull(MaybeItem(chart));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Ancestor_hiding_detach_and_replacement_release_old_owned_subtrees(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); var container = new Border { Child = chart };
        Set(chart, new object(), Template()); using var host = new Host(chart, container); var child = Item(chart).Child!;
        container.IsVisible = false; Assert.Null(child.DataContext); Assert.Null(child.GetVisualParent()); host.Drain(); Assert.Null(MaybeItem(chart));
        container.IsVisible = true; host.Drain(); child = Item(chart).Child!; chart.IsEnabled = false; host.Drain(); Assert.Same(child, Item(chart).Child);
        Set(chart, new object(), Template()); Assert.Null(child.DataContext); Assert.Null(child.GetVisualParent()); host.Drain(); child = Item(chart).Child!;
        host.DetachWithoutDrain(); Assert.Null(child.DataContext); Assert.Null(child.GetVisualParent()); host.Drain(); Assert.Null(MaybeItem(chart));
        host.Attach(); Assert.NotNull(MaybeItem(chart));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void A_parented_template_result_is_rejected_without_stealing_its_parent_or_context(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart);
        var business = new object(); var child = new TextBlock { DataContext = business }; var parent = new Border { Child = child };
        Set(chart, new object(), new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => child));
        Assert.Throws<InvalidOperationException>(host.Drain); Assert.Same(parent, child.GetVisualParent()); Assert.Same(business, child.DataContext);
        Assert.Null(MaybeItem(chart)); Set(chart, new object(), Template()); host.Drain(); Assert.NotNull(MaybeItem(chart));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Build_failure_is_cleaned_and_quarantined_until_the_template_is_replaced(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart); var builds = 0;
        Set(chart, new object(), new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => { builds++; throw new InvalidOperationException("plot template failed"); }));
        Assert.Throws<InvalidOperationException>(host.Drain); Assert.Null(MaybeItem(chart)); host.Drain(); Assert.Equal(1, builds);
        Set(chart, new object(), Template()); host.Drain(); Assert.NotNull(MaybeItem(chart));
    }

    [AvaloniaTheory]
    [InlineData(false, "source")] [InlineData(true, "source")]
    [InlineData(false, "template")] [InlineData(true, "template")]
    [InlineData(false, "data-context")] [InlineData(true, "data-context")]
    [InlineData(false, "detach")] [InlineData(true, "detach")]
    public void Reentrant_build_does_not_publish_an_obsolete_content_control(bool engineering, string operation)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart);
        var stale = new TextBlock(); var once = false;
        Set(chart, new object(), new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) =>
        {
            if (once) return new TextBlock { Text = "Current" }; once = true;
            if (operation == "source") Replace(chart, new[] { new SharedTooltipRow("new", "New", 4, "R", "C") });
            if (operation == "template") Set(chart, new object(), Template());
            if (operation == "data-context") chart.DataContext = new object();
            if (operation == "detach") host.DetachWithoutDrain();
            return stale;
        }));
        host.Drain(); Assert.True(once); Assert.Null(stale.DataContext); Assert.Null(stale.GetVisualParent()); Assert.Null(((ILogical)stale).LogicalParent);
        if (operation != "detach") Assert.NotNull(MaybeItem(chart));
    }

    [AvaloniaTheory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Measure_and_arrange_reentry_cleanup_obsolete_children_and_recover_after_an_exception(bool engineering, bool arrange)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart); var once = false;
        var child = new LayoutCallback(() =>
        {
            if (once) return; once = true; Set(chart, new object(), Template()); throw new InvalidOperationException("obsolete plot layout");
        }, arrange);
        Set(chart, new object(), new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => child));
        Assert.Throws<InvalidOperationException>(host.Drain); Assert.Null(child.DataContext); Assert.Null(child.GetVisualParent());
        host.Drain(); Assert.NotNull(MaybeItem(chart));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Permanent_plot_content_exports_as_a_visual_and_nested_export_during_build_is_rejected(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart); var attempted = false;
        Set(chart, new object(), new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) =>
        {
            attempted = true; Assert.Throws<InvalidOperationException>(() => RecordExport(chart)); return new TextBlock { Text = "Permanent plot note" };
        }));
        host.Drain(); Assert.True(attempted); var child = Item(chart).Child;
        var drawing = RecordExport(chart);
        Assert.Contains(Shapes(drawing).Select(shape => shape.Brush).OfType<VisualBrush>(), brush => brush.Visual is Control { Name: "PART_ChartDataPlotAreaContentOverlay" });
        Assert.Same(child, Item(chart).Child);
    }

    private static IDataTemplate Template() => new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => new TextBlock { Text = "Plot note" });
    private static ChartBase Create(bool engineering, IEnumerable source) => engineering ? new PlotEngineeringProbe { ItemsSource = source } : new FunnelChart { ItemsSource = source, IdPath = "Id", CategoryPath = "Label", ValuePath = "Value" };
    private static void Set(ChartBase chart, object? content, IDataTemplate? template)
    { if (chart is EngineeringChartBase e) { e.PlotAreaContent = content; e.PlotAreaContentTemplate = template; } else { var a = (AnalysisChartBase)chart; a.PlotAreaContent = content; a.PlotAreaContentTemplate = template; } }
    private static Rect Plot(ChartBase chart) => chart is EngineeringChartBase e ? e.PlotArea : ((AnalysisChartBase)chart).PlotArea;
    private static void Replace(ChartBase chart, IEnumerable source) { if (chart is EngineeringChartBase e) e.ItemsSource = source; else ((AnalysisChartBase)chart).ItemsSource = source; }
    private static Control Overlay(ChartBase chart) => Assert.Single(chart.GetVisualChildren().OfType<Control>(), child => child.Name == "PART_ChartDataPlotAreaContentOverlay");
    private static Border? MaybeItem(ChartBase chart) => chart.GetVisualDescendants().OfType<Border>().SingleOrDefault(item => item.Name == "PART_ChartDataPlotAreaContent");
    private static Border Item(ChartBase chart) => Assert.IsType<Border>(MaybeItem(chart));
    private static DrawingGroup RecordExport(ChartBase chart)
    { var group = new DrawingGroup(); using var context = group.Open(); if (chart is EngineeringChartBase e) e.RenderExport(context, e.GetExportLayout(null), null); else { var a = (AnalysisChartBase)chart; a.RenderExport(context, a.GetExportLayout(null), null); } return group; }
    private static IEnumerable<GeometryDrawing> Shapes(Drawing drawing)
    { if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var shape in Shapes(child)) yield return shape; else if (drawing is GeometryDrawing shape) yield return shape; }
    private sealed class PlotEngineeringProbe : EngineeringChartBase
    {
        public PlotEngineeringProbe() { IdPath = "Id"; LabelPath = "Label"; ObservePaths("Value"); ShowGrid = false; }
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
        { var point = ApplyViewport(plot.Center, plot); return data.Count == 0 ? [] : [new(data.Points, new EllipseGeometry(new Rect(point.X - 4, point.Y - 4, 8, 8)), Brushes.Blue, point)]; }
    }
    private sealed class LayoutCallback(Action callback, bool arrange) : Control
    {
        protected override Size MeasureOverride(Size availableSize) { if (!arrange) callback(); return new(50, 20); }
        protected override Size ArrangeOverride(Size finalSize) { if (arrange) callback(); return finalSize; }
    }
    private sealed class MutableBusiness : System.ComponentModel.INotifyPropertyChanged
    {
        private string _title = "Business title";
        public string Title { get => _title; set { _title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); } }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class CountingRows(IEnumerable rows) : IEnumerable
    { public int Enumerations { get; private set; } public IEnumerator GetEnumerator() { Enumerations++; return rows.GetEnumerator(); } }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly ChartBase _chart; private readonly Control _content;
        internal Host(ChartBase chart, Control? content = null) { _chart = chart; _content = content ?? chart; _window = new Window { Content = _content, Width = 640, Height = 400 }; _window.Show(); Drain(); }
        internal void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); }
        internal void Move() { var point = _chart is EngineeringChartBase e ? e.GetMarks()[0].Position : ((AnalysisChartBase)_chart).GetMarks()[0].LabelPosition; _window.MouseMove(_chart.TranslatePoint(point, _window) ?? point); Drain(); }
        internal void DetachWithoutDrain() => _window.Content = null;
        internal void Attach() { _window.Content = _content; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
