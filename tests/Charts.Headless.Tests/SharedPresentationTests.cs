using System.Collections;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

public sealed class SharedPresentationTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public void Professional_roots_share_properties_without_changing_Cartesian_series_templates()
    {
        Assert.Same(EngineeringChartBase.LegendItemTemplateProperty, AnalysisChartBase.LegendItemTemplateProperty);
        Assert.Same(EngineeringChartBase.DataLabelTemplateProperty, AnalysisChartBase.DataLabelTemplateProperty);
        Assert.Same(EngineeringChartBase.MaxLegendItemsProperty, AnalysisChartBase.MaxLegendItemsProperty);
        Assert.Same(EngineeringChartBase.MaxDataLabelsProperty, AnalysisChartBase.MaxDataLabelsProperty);
        Assert.NotSame(ChartSeries.DataLabelTemplateProperty, EngineeringChartBase.DataLabelTemplateProperty);
        var chart = new MosaicChart(); Assert.Equal(32, chart.MaxLegendItems); Assert.Equal(200, chart.MaxDataLabels);
        Assert.Throws<ArgumentException>(() => chart.MaxLegendItems = 1025);
        Assert.Throws<ArgumentException>(() => chart.MaxDataLabels = 4097);
        Assert.Throws<ArgumentException>(() => chart.MaxDataLabels = -1);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Real_controls_keep_original_members_business_context_resources_and_passive_bounded_layout(bool engineering)
    {
        var rows = new SharedTooltipPresentationModel().Records; var chart = Create(engineering, rows);
        var model = new object(); chart.DataContext = model; chart.FontSize = 18; chart.Foreground = Brushes.Purple;
        chart.Resources["SharedPresentationBrush"] = Brushes.Pink;
        SetTemplates(chart, Template(true), Template(false));
        using var host = new Host(chart);
        var legend = LegendItems(chart); var labels = Labels(chart);
        Assert.NotEmpty(legend); Assert.NotEmpty(labels);
        Assert.All(legend, item => Assert.Same(model, item.BusinessDataContext));
        Assert.All(labels, item => Assert.Same(model, item.BusinessDataContext));
        var first = labels[0];
        if (engineering)
        {
            Assert.Null(first.PointHit); Assert.NotNull(first.EngineeringHit);
            Assert.Same(first.EngineeringHit!.Members, first.Members);
            Assert.Equal(new[] { 0, 2 }, first.Members.Select(member => member.SourceIndex));
            Assert.Same(rows[0], first.Members[0].Item); Assert.Same(rows[2], first.Members[1].Item);
        }
        else { Assert.Null(first.EngineeringHit); Assert.NotNull(first.PointHit); Assert.Same(rows[0], Assert.Single(first.Members).Item); }
        Assert.Equal(legend.Count, Items(chart, true).Length); Assert.Equal(labels.Count, Items(chart, false).Length);
        foreach (var overlay in chart.GetVisualChildren().OfType<Control>().Where(item => item.Name is "PART_ChartDataLegendOverlay" or "PART_ChartDataLabelsOverlay"))
        {
            Assert.False(overlay.IsHitTestVisible); Assert.False(overlay.Focusable);
            Assert.Same(chart, ((ILogical)overlay).LogicalParent); Assert.Equal(new Rect(chart.Bounds.Size), overlay.Bounds);
            Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(overlay));
        }
        foreach (var item in Items(chart, false).Concat(Items(chart, true)))
        {
            var child = Assert.IsType<TextBlock>(item.Child);
            Assert.Equal(18, child.FontSize); Assert.Same(Brushes.Purple, child.Foreground);
            Assert.Same(Brushes.Pink, child.Background);
            Assert.True(item.Bounds.Width <= chart.Bounds.Width); Assert.True(item.Bounds.Height <= chart.Bounds.Height);
        }
        chart.Resources["SharedPresentationBrush"] = Brushes.Blue; host.Drain();
        Assert.All(Items(chart, true), item => Assert.Same(Brushes.Blue, Assert.IsType<TextBlock>(item.Child).Background));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Stable_hover_and_business_updates_reuse_presentations_without_source_enumeration(bool engineering)
    {
        var rows = new SharedTooltipPresentationModel().Records; var model = new MutableBusiness(); var source = new CountingRows(rows);
        var chart = Create(engineering, source); chart.DataContext = model;
        var builds = 0;
        var legend = new FuncDataTemplate<ChartDataLegendItemContext>((context, _) => { builds++; var child = new TextBlock(); child.Bind(TextBlock.TextProperty, new Binding("BusinessDataContext.Title")); return child; });
        var label = new FuncDataTemplate<ChartDataLabelItemContext>((context, _) => { builds++; var child = new TextBlock(); child.Bind(TextBlock.TextProperty, new Binding("BusinessDataContext.Title")); return child; });
        SetTemplates(chart, legend, label); using var host = new Host(chart);
        var controls = Items(chart, false).Concat(Items(chart, true)).Select(item => item.Child).ToArray(); var count = builds; var reads = source.Enumerations;
        host.Move(0); host.Move(1); model.Title = "Updated business title"; host.Drain();
        Assert.All(Items(chart, false).Concat(Items(chart, true)), item => Assert.Equal("Updated business title", Assert.IsType<TextBlock>(item.Child).Text));
        Assert.Equal(count, builds); Assert.Equal(reads, source.Enumerations);
        Assert.Equal(controls, Items(chart, false).Concat(Items(chart, true)).Select(item => item.Child));
        chart.IsEnabled = false; host.Drain(); Assert.Equal(count, builds); Assert.Equal(controls, Items(chart, false).Concat(Items(chart, true)).Select(item => item.Child));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Source_INPC_and_replacement_release_old_readings_and_keep_true_identity(bool engineering)
    {
        var rows = new SharedTooltipPresentationModel().Records; var chart = Create(engineering, rows);
        SetTemplates(chart, Template(true), Template(false)); using var host = new Host(chart);
        var old = Items(chart, false).Select(item => item.Child!).ToArray();
        rows[0].Value = 17; Assert.All(old, child => Assert.Null(child.DataContext)); host.Drain();
        Assert.Contains(Labels(chart).SelectMany(item => item.Members), member => ReferenceEquals(member.Item, rows[0]) && member.Point.Y == 17);
        var replacement = new SharedTooltipRow("new", "New", 7, "R", "C");
        Replace(chart, new[] { replacement }); Assert.All(old, child => Assert.Null(child.DataContext)); host.Drain();
        Assert.All(Labels(chart).SelectMany(item => item.Members), member => Assert.Same(replacement, member.Item));
        Assert.All(LegendItems(chart).SelectMany(item => item.Members), member => Assert.Equal("new", member.StableId));
        var newModel = new object(); chart.DataContext = newModel; host.Drain();
        Assert.All(Labels(chart), item => Assert.Same(newModel, item.BusinessDataContext));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Limits_family_gates_template_replacement_hiding_and_detach_release_owned_subtrees(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); var container = new Border { Child = chart };
        SetTemplates(chart, Template(true), Template(false)); using var host = new Host(chart, container);
        SetLimits(chart, 1, 1); host.Drain(); Assert.Single(Items(chart, true)); Assert.Single(Items(chart, false));
        Assert.Single(LegendItems(chart)); Assert.Single(Labels(chart));
        var previous = Items(chart, false)[0].Child!; SetTemplates(chart, Template(true), Template(false)); host.Drain();
        Assert.Null(previous.DataContext); Assert.Null(previous.GetVisualParent()); Assert.Null(((ILogical)previous).LogicalParent);
        previous = Items(chart, false)[0].Child!; container.IsVisible = false;
        Assert.Null(previous.DataContext); Assert.Null(previous.GetVisualParent()); host.Drain(); Assert.Empty(Items(chart, false));
        container.IsVisible = true; host.Drain(); Assert.Single(Items(chart, false));
        chart.IsEnabled = false; host.Drain(); Assert.Single(Items(chart, false));
        SetGates(chart, false); host.Drain(); Assert.Empty(Labels(chart)); Assert.Empty(Items(chart, false)); Assert.Single(Items(chart, true));
        SetGates(chart, true); host.Drain(); Assert.Single(Items(chart, false));
        SetLimits(chart, 0, 0); host.Drain(); Assert.Empty(Labels(chart)); Assert.Empty(LegendItems(chart)); Assert.Empty(Items(chart, false)); Assert.Empty(Items(chart, true));
        SetLimits(chart, 3, 3); host.Drain(); previous = Items(chart, false)[0].Child!; host.Detach();
        Assert.Null(previous.DataContext); Assert.Null(previous.GetVisualParent()); Assert.Empty(Items(chart, false));
        host.Attach(); Assert.NotEmpty(Items(chart, false));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Null_build_suppresses_legacy_labels_and_build_failure_is_quarantined_until_replacement(bool engineering)
    {
        var chart = Create(engineering, new[] { new SharedTooltipRow("a", "UniqueDataLabel", 2, "UniqueDataLabel", "C") });
        using var host = new Host(chart); Assert.Contains("UniqueDataLabel", Text(Record(chart)), StringComparison.Ordinal);
        SetTemplates(chart, null, new FuncDataTemplate<ChartDataLabelItemContext>((_, _) => null)); host.Drain();
        Assert.Empty(Items(chart, false)); Assert.DoesNotContain("UniqueDataLabel", Text(Record(chart)), StringComparison.Ordinal);
        var attempts = 0; SetTemplates(chart, null, new FuncDataTemplate<ChartDataLabelItemContext>((_, _) => { attempts++; throw new InvalidOperationException("template failed"); }));
        Assert.Throws<InvalidOperationException>(host.Drain); Assert.Empty(Items(chart, false));
        host.Drain(); Assert.Equal(1, attempts);
        SetTemplates(chart, null, Template(false)); host.Drain(); Assert.NotEmpty(Items(chart, false));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Conditional_null_build_is_retried_once_for_a_new_reading(bool engineering)
    {
        var row = new SharedTooltipRow("a", "A", 2, "R", "C"); var chart = Create(engineering, new[] { row });
        var builds = 0;
        SetTemplates(chart, null, new FuncDataTemplate<ChartDataLabelItemContext>((context, _) =>
        { builds++; return context?.Value > 5 ? new TextBlock { Text = "visible" } : null; }));
        using var host = new Host(chart); Assert.Empty(Items(chart, false)); var initial = builds;
        host.Drain(); Assert.Equal(initial, builds);
        row.Value = 9; host.Drain(); Assert.Single(Items(chart, false)); Assert.Equal(initial + 1, builds);
        Assert.Equal(9, Assert.IsType<ChartDataLabelItemContext>(Items(chart, false)[0].DataContext).Value);
        Replace(chart, new[] { new SharedTooltipRow("new", "New", 11, "R", "C") }); host.Drain();
        Assert.Equal("new", Assert.Single(Assert.IsType<ChartDataLabelItemContext>(Items(chart, false)[0].DataContext).Members).StableId);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Large_labels_remain_above_a_permanent_legend_without_changing_original_anchors(bool engineering)
    {
        ChartBase chart = engineering ? new PresentationEngineeringProbe() : new PresentationAnalysisProbe();
        Replace(chart, new[] { new SharedTooltipRow("a", "A", 2, "R", "C") });
        var labelTemplate = new FuncDataTemplate<ChartDataLabelItemContext>((_, _) => new TextBlock { Text = "Large label", Width = 100, Height = 90 });
        SetTemplates(chart, new FuncDataTemplate<ChartDataLegendItemContext>((_, _) => new TextBlock { Text = "Legend", Width = 100, Height = 40 }), labelTemplate);
        using var host = new Host(chart);
        var item = Assert.Single(Items(chart, false)); var label = Assert.Single(Labels(chart));
        var legend = Assert.Single(Items(chart, true));
        Assert.True(item.Bounds.Bottom <= legend.Bounds.Top - 4);
        var previousBottom = item.Bounds.Bottom;
        var originalAnchor = label.Anchor;
        Assert.Equal(engineering ? ((EngineeringChartBase)chart).GetMarks()[0].Position : ((AnalysisChartBase)chart).GetMarks()[0].LabelPosition, originalAnchor);
        SetTemplates(chart, null, labelTemplate); host.Drain();
        Assert.Empty(Items(chart, true));
        var restored = Assert.Single(Items(chart, false));
        output.WriteLine($"Legend removal: engineering={engineering}; before bottom={previousBottom}; restored bounds={restored.Bounds}; original anchor={originalAnchor}; restored anchor={Assert.Single(Labels(chart)).Anchor}.");
        Assert.True(restored.Bounds.Bottom > previousBottom, $"Before bottom={previousBottom}; restored bounds={restored.Bounds}; original anchor={originalAnchor}; restored anchor={Assert.Single(Labels(chart)).Anchor}; chart={chart.Bounds}; engineering={engineering}.");
    }

    [AvaloniaFact]
    public void Viewport_changes_move_the_reused_label_control_with_its_original_observation()
    {
        var row = new SharedTooltipRow("a", "A", 2, "R", "C");
        var chart = new ViewportPresentationProbe { ItemsSource = new[] { row } };
        SetTemplates(chart, Template(true), new FuncDataTemplate<ChartDataLabelItemContext>((_, _) => new TextBlock { Text = "Point", Width = 80, Height = 20 }));
        using var host = new Host(chart);
        var border = Assert.Single(Items(chart, false)); var child = border.Child;
        var bounds = border.Bounds; var anchor = Assert.Single(chart.GetDataLabels()).Anchor;
        chart.Zoom = 1.5; chart.PanX = .1; chart.PanY = -.1; host.Drain();
        var updated = Assert.Single(chart.GetDataLabels()); var moved = Assert.Single(Items(chart, false));
        Assert.NotEqual(anchor, updated.Anchor); Assert.NotEqual(bounds.TopLeft, moved.Bounds.TopLeft);
        Assert.Same(border, moved); Assert.Same(child, moved.Child);
        Assert.Same(row, Assert.Single(updated.Members).Item); Assert.Equal("a", updated.Members[0].StableId);
        Assert.InRange(Math.Abs(moved.Bounds.Center.X - updated.Anchor.X), 0, 1);
        Assert.InRange(Math.Abs(moved.Bounds.Center.Y - updated.Anchor.Y), 0, 1);
        output.WriteLine($"Viewport: anchor {anchor} -> {updated.Anchor}; bounds {bounds} -> {moved.Bounds}; original source and control identities retained.");
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Family_context_builders_can_query_both_APIs_without_recursion_or_partial_publication(bool engineering)
    {
        ChartBase chart = engineering ? new ReentrantEngineeringProbe() : new ReentrantAnalysisProbe();
        Replace(chart, new[] { new SharedTooltipRow("a", "A", 2, "R", "C") });
        using var host = new Host(chart); var calls = 0;
        void Query()
        {
            calls++; Assert.Empty(LegendItems(chart)); Assert.Empty(Labels(chart));
            Assert.Throws<InvalidOperationException>(() => RecordExport(chart));
        }
        if (chart is ReentrantEngineeringProbe e) e.DuringLegend = e.DuringLabels = Query;
        else { var a = (ReentrantAnalysisProbe)chart; a.DuringLegend = a.DuringLabels = Query; }
        Assert.Single(LegendItems(chart)); Assert.Single(Labels(chart)); Assert.Equal(2, calls);
        Assert.Single(LegendItems(chart)); Assert.Equal(2, calls);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Business_category_formatters_can_query_both_APIs_without_recursion_or_partial_publication(bool engineering)
    {
        var category = new ReentrantCategory(); var row = new CategoryRow("a", category, 2);
        ChartBase chart = engineering ? new ReentrantEngineeringProbe() : new ReentrantAnalysisProbe();
        Replace(chart, new[] { row }); using var host = new Host(chart); var calls = 0;
        category.Callback = () => { calls++; Assert.Empty(LegendItems(chart)); Assert.Empty(Labels(chart)); };
        Assert.Single(LegendItems(chart)); Assert.Single(Labels(chart)); Assert.InRange(calls, 1, 4);
        Assert.Same(row, Assert.Single(Assert.Single(Labels(chart)).Members).Item);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Reentrant_source_changes_during_context_generation_do_not_commit_a_mixed_batch(bool engineering)
    {
        var old = new SharedTooltipRow("old", "Old", 2, "R", "C"); var current = new SharedTooltipRow("current", "Current", 4, "R", "C");
        ChartBase chart = engineering ? new ReentrantEngineeringProbe() : new ReentrantAnalysisProbe(); Replace(chart, new[] { old });
        using var host = new Host(chart); var once = false;
        void ReplaceDuringGeneration()
        {
            if (once) return; once = true; Replace(chart, new[] { current });
            Assert.Empty(LegendItems(chart)); Assert.Empty(Labels(chart));
        }
        if (chart is ReentrantEngineeringProbe e) e.DuringLabels = ReplaceDuringGeneration; else ((ReentrantAnalysisProbe)chart).DuringLabels = ReplaceDuringGeneration;
        Assert.Empty(LegendItems(chart)); host.Drain();
        Assert.Same(current, Assert.Single(Assert.Single(LegendItems(chart)).Members).Item);
        Assert.Same(current, Assert.Single(Assert.Single(Labels(chart)).Members).Item);
    }

    [AvaloniaTheory]
    [InlineData(false, "source")] [InlineData(true, "source")]
    [InlineData(false, "template")] [InlineData(true, "template")]
    [InlineData(false, "data-context")] [InlineData(true, "data-context")]
    [InlineData(false, "detach")] [InlineData(true, "detach")]
    public void Reentrant_build_never_publishes_a_stale_control(bool engineering, string operation)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart);
        var stale = new TextBlock(); var entered = false;
        SetTemplates(chart, null, new FuncDataTemplate<ChartDataLabelItemContext>((_, _) =>
        {
            if (entered) return new TextBlock(); entered = true;
            if (operation == "source") Replace(chart, new[] { new SharedTooltipRow("new", "New", 4, "R", "C") });
            if (operation == "template") SetTemplates(chart, null, Template(false));
            if (operation == "data-context") chart.DataContext = new object();
            if (operation == "detach") host.DetachWithoutDrain();
            return stale;
        }));
        host.Drain(); Assert.True(entered); Assert.Null(stale.DataContext); Assert.Null(stale.GetVisualParent()); Assert.Null(((ILogical)stale).LogicalParent);
        if (operation != "detach") Assert.NotEmpty(Items(chart, false));
    }

    [AvaloniaTheory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Measure_and_arrange_failures_cleanup_and_reentrant_replacement_recovers(bool engineering, bool arrange)
    {
        var chart = Create(engineering, new[] { new SharedTooltipRow("a", "A", 2, "R", "C") }); using var host = new Host(chart);
        var once = false;
        void ReplaceAndThrow()
        {
            if (once) return; once = true; SetTemplates(chart, null, Template(false)); throw new InvalidOperationException("obsolete layout");
        }
        var child = new LayoutCallback(ReplaceAndThrow, arrange); SetTemplates(chart, null, new FuncDataTemplate<ChartDataLabelItemContext>((_, _) => child));
        Assert.Throws<InvalidOperationException>(host.Drain); Assert.Null(child.DataContext); host.Drain(); Assert.NotEmpty(Items(chart, false));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Export_draws_prepared_permanent_visuals_and_rejects_nested_capture_during_build(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); using var host = new Host(chart);
        var attempted = false;
        SetTemplates(chart, Template(true), new FuncDataTemplate<ChartDataLabelItemContext>((_, _) =>
        {
            if (!attempted) { attempted = true; Assert.Throws<InvalidOperationException>(() => RecordExport(chart)); }
            return new TextBlock { Text = "Permanent exported" };
        }));
        host.Drain(); Assert.True(attempted);
        var before = Items(chart, false).Select(item => item.Child).ToArray();
        var drawing = RecordExport(chart);
        var visuals = Shapes(drawing).Select(shape => shape.Brush).OfType<VisualBrush>().Select(brush => brush.Visual).OfType<Control>().ToArray();
        Assert.Contains(visuals, visual => visual.Name == "PART_ChartDataLegendOverlay");
        Assert.Contains(visuals, visual => visual.Name == "PART_ChartDataLabelsOverlay");
        Assert.DoesNotContain(visuals, visual => visual.Name == "PART_ChartDataTooltipOverlay");
        Assert.Equal(before, Items(chart, false).Select(item => item.Child));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Permanent_controls_keep_the_legacy_tooltip_in_a_foreground_visual(bool engineering)
    {
        var chart = Create(engineering, new SharedTooltipPresentationModel().Records); SetTemplates(chart, Template(true), Template(false));
        using var host = new Host(chart); host.Move(0);
        var overlay = Assert.Single(chart.GetVisualChildren().OfType<Control>(), child => child.Name == "PART_ChartDataLegacyTooltipOverlay");
        Assert.Equal(30, overlay.ZIndex); Assert.False(overlay.IsHitTestVisible);
        var drawing = new DrawingGroup(); using (var context = drawing.Open()) overlay.Render(context);
        Assert.NotEmpty(Text(drawing));
        var tooltipText = Text(drawing);
        Assert.DoesNotContain(tooltipText, Text(Record(chart)), StringComparison.Ordinal);
    }

    private static IDataTemplate Template(bool legend) => legend
        ? new FuncDataTemplate<ChartDataLegendItemContext>((context, _) => Content(context?.Text))
        : new FuncDataTemplate<ChartDataLabelItemContext>((context, _) => Content(context?.Text));
    private static TextBlock Content(string? text)
    { var item = new TextBlock { Text = text, MaxWidth = 120, Height = 18 }; item.Bind(TextBlock.BackgroundProperty, new DynamicResourceExtension("SharedPresentationBrush")); return item; }
    private static ChartBase Create(bool engineering, IEnumerable source) => engineering ? new MosaicChart { ItemsSource = source } : new FunnelChart { ItemsSource = source, IdPath = "Id", CategoryPath = "Label", ValuePath = "Value" };
    private static void SetTemplates(ChartBase chart, IDataTemplate? legend, IDataTemplate? label)
    { if (chart is EngineeringChartBase e) { e.LegendItemTemplate = legend; e.DataLabelTemplate = label; } else { var a = (AnalysisChartBase)chart; a.LegendItemTemplate = legend; a.DataLabelTemplate = label; } }
    private static void SetLimits(ChartBase chart, int legend, int labels)
    { if (chart is EngineeringChartBase e) { e.MaxLegendItems = legend; e.MaxDataLabels = labels; } else { var a = (AnalysisChartBase)chart; a.MaxLegendItems = legend; a.MaxDataLabels = labels; } }
    private static void SetGates(ChartBase chart, bool enabled)
    { if (chart is ComparisonChartBase e) { e.ShowLabels = enabled; e.ShowValues = enabled; } else { var a = (AnalysisChartBase)chart; a.ShowLabels = enabled; a.ShowValues = enabled; } }
    private static void Replace(ChartBase chart, IEnumerable source) { if (chart is EngineeringChartBase e) e.ItemsSource = source; else ((AnalysisChartBase)chart).ItemsSource = source; }
    private static IReadOnlyList<ChartDataLegendItemContext> LegendItems(ChartBase chart) => chart is EngineeringChartBase e ? e.GetLegendItems() : ((AnalysisChartBase)chart).GetLegendItems();
    private static IReadOnlyList<ChartDataLabelItemContext> Labels(ChartBase chart) => chart is EngineeringChartBase e ? e.GetDataLabels() : ((AnalysisChartBase)chart).GetDataLabels();
    private static Border[] Items(ChartBase chart, bool legend) => chart.GetVisualDescendants().OfType<Border>().Where(item => item.Name == (legend ? "PART_ChartDataLegendItem" : "PART_ChartDataLabel")).ToArray();
    private static DrawingGroup Record(ChartBase chart) { var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context); return group; }
    private static DrawingGroup RecordExport(ChartBase chart)
    { var group = new DrawingGroup(); using var context = group.Open(); if (chart is EngineeringChartBase e) e.RenderExport(context, e.GetExportLayout(null), null); else { var a = (AnalysisChartBase)chart; a.RenderExport(context, a.GetExportLayout(null), null); } return group; }
    private static string Text(Drawing drawing) => string.Concat(Glyphs(drawing).Select(glyph => glyph.GlyphRun is { } run ? new string(run.Characters.Span) : ""));
    private static IEnumerable<GlyphRunDrawing> Glyphs(Drawing drawing) { if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var glyph in Glyphs(child)) yield return glyph; else if (drawing is GlyphRunDrawing glyph) yield return glyph; }
    private static IEnumerable<GeometryDrawing> Shapes(Drawing drawing) { if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var shape in Shapes(child)) yield return shape; else if (drawing is GeometryDrawing shape) yield return shape; }
    private sealed class MutableBusiness : System.ComponentModel.INotifyPropertyChanged
    {
        private string _title = "Business title";
        public string Title { get => _title; set { _title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); } }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class PresentationEngineeringProbe : EngineeringChartBase
    {
        public PresentationEngineeringProbe() { IdPath = "Id"; LabelPath = "Label"; ObservePaths("Value"); ShowGrid = false; }
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => data.Count == 0 ? [] :
            [new(Array.AsReadOnly(new[] { data.Points[0] }), new RectangleGeometry(plot), Brushes.Blue, new(plot.Center.X, plot.Bottom))];
    }
    private sealed class PresentationAnalysisProbe : AnalysisChartBase
    {
        public PresentationAnalysisProbe() { IdPath = "Id"; CategoryPath = "Label"; ValuePath = "Value"; }
        protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => data.Count == 0 ? [] :
            [new(data.Points[0], new RectangleGeometry(plot), Brushes.Blue, new(plot.Center.X, plot.Bottom))];
    }
    private sealed class ViewportPresentationProbe : EngineeringChartBase
    {
        public ViewportPresentationProbe() { IdPath = "Id"; LabelPath = "Label"; ObservePaths("Value"); ShowGrid = false; }
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
        {
            if (data.Count == 0) return [];
            var point = ApplyViewport(plot.Center, plot);
            return [new(Array.AsReadOnly(new[] { data.Points[0] }), new EllipseGeometry(new Rect(point.X - 4, point.Y - 4, 8, 8)), Brushes.Blue, point)];
        }
    }
    private sealed class ReentrantEngineeringProbe : EngineeringChartBase
    {
        public Action? DuringLegend { get; set; }
        public Action? DuringLabels { get; set; }
        public ReentrantEngineeringProbe() { IdPath = "Id"; LabelPath = "Label"; ObservePaths("Value"); ShowGrid = false; }
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => data.Count == 0 ? [] :
            [new(Array.AsReadOnly(new[] { data.Points[0] }), new RectangleGeometry(plot), Brushes.Blue, plot.Center)];
        protected override IEnumerable<ChartDataLegendItemContext> CreateLegendItems(IReadOnlyList<EngineeringMarkInfo> marks)
        { DuringLegend?.Invoke(); return base.CreateLegendItems(marks); }
        protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<EngineeringMarkInfo> marks)
        { DuringLabels?.Invoke(); return base.CreateDataLabels(marks); }
    }
    private sealed class ReentrantAnalysisProbe : AnalysisChartBase
    {
        public Action? DuringLegend { get; set; }
        public Action? DuringLabels { get; set; }
        public ReentrantAnalysisProbe() { IdPath = "Id"; CategoryPath = "Label"; ValuePath = "Value"; }
        protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => data.Count == 0 ? [] :
            [new(data.Points[0], new RectangleGeometry(plot), Brushes.Blue, plot.Center)];
        protected override IEnumerable<ChartDataLegendItemContext> CreateLegendItems(IReadOnlyList<AnalysisMarkInfo> marks)
        { DuringLegend?.Invoke(); return base.CreateLegendItems(marks); }
        protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<AnalysisMarkInfo> marks)
        { DuringLabels?.Invoke(); return base.CreateDataLabels(marks); }
    }
    private sealed record CategoryRow(string Id, object Label, double Value);
    private sealed class ReentrantCategory
    {
        public Action? Callback { get; set; }
        public override string ToString() { Callback?.Invoke(); return "Business category"; }
    }
    private sealed class LayoutCallback(Action callback, bool arrange) : Control
    {
        protected override Size MeasureOverride(Size availableSize) { if (!arrange) callback(); return new(50, 20); }
        protected override Size ArrangeOverride(Size finalSize) { if (arrange) callback(); return finalSize; }
    }
    private sealed class CountingRows(ObservableCollection<SharedTooltipRow> rows) : IEnumerable, INotifyCollectionChanged
    { public int Enumerations { get; private set; } public IEnumerator GetEnumerator() { Enumerations++; return rows.GetEnumerator(); } public event NotifyCollectionChangedEventHandler? CollectionChanged { add => rows.CollectionChanged += value; remove => rows.CollectionChanged -= value; } }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly ChartBase _chart; private readonly Control _content;
        internal Host(ChartBase chart, Control? content = null) { _chart = chart; _content = content ?? chart; _window = new Window { Content = _content, Width = 640, Height = 400 }; _window.Show(); Drain(); }
        internal void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); }
        internal void Move(int index) { var point = _chart is EngineeringChartBase e ? e.GetMarks()[index].Position : ((AnalysisChartBase)_chart).GetMarks()[index].LabelPosition; _window.MouseMove(_chart.TranslatePoint(point, _window) ?? point); Drain(); }
        internal void DetachWithoutDrain() => _window.Content = null;
        internal void Detach() { DetachWithoutDrain(); Drain(); }
        internal void Attach() { _window.Content = _content; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
