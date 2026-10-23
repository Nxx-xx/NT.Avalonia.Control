using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class DataLabelTemplateTests
{
    [AvaloniaFact]
    public void A_null_template_keeps_the_existing_drawn_formatter_path_without_controls()
    {
        var (chart, series) = CreateChart("line");
        var calls = 0;
        series.DataLabelFormatter = _ => { calls++; return "drawn label"; };
        using var host = new TemplateFeaturesHost(chart);
        var before = calls;
        _ = TemplateFeaturesHost.Record(chart);
        _ = TemplateFeaturesHost.Record(chart);
        Assert.Equal(before + 4, calls);
        Assert.Empty(LabelCards(chart));
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Control>(), control => control.Name == "PART_ChartDataLabelsOverlay");
    }

    [AvaloniaTheory]
    [InlineData("line")]
    [InlineData("bar")]
    [InlineData("scatter")]
    public void Templates_receive_original_finite_visible_items_even_when_the_formatter_returns_null(string kind)
    {
        var (chart, series) = CreateChart(kind);
        TemplateFeatureRow[] source = [new("first", 2, 20, "first item"), new("gap", 4, double.NaN, "gap"), new("second", 8, 70, "second item"), new("outside", 20, 50, "outside")];
        series.ItemsSource = source;
        series.DataLabelFormatter = _ => null;
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => Card());
        using var host = new TemplateFeaturesHost(chart);
        var contexts = LabelCards(chart).Select(card => Assert.IsType<ChartDataLabelContext>(card.DataContext)).OrderBy(context => context.Data.SourceIndex).ToArray();
        Assert.Equal(2, contexts.Length);
        Assert.Equal(new[] { 0, 2 }, contexts.Select(context => context.Data.SourceIndex));
        foreach (var context in contexts)
        {
            var original = source[context.Data.SourceIndex];
            Assert.Same(series, context.Series);
            Assert.Same(original, context.Data.Item);
            Assert.Equal(original.Id, context.Data.StableId);
            Assert.Equal(new ChartPoint(original.X, original.Y), context.Data.Point);
            Assert.Null(context.Text);
            var expected = Project(chart, original.X, original.Y);
            Assert.Equal(expected.X, context.Position.X, 8);
            Assert.Equal(expected.Y, context.Position.Y, 8);
        }
        var overlay = Assert.Single(chart.GetVisualDescendants().OfType<Control>(), control => control.Name == "PART_ChartDataLabelsOverlay");
        Assert.False(overlay.IsHitTestVisible);
        Assert.False(overlay.Focusable);
        Assert.Equal(new Rect(chart.Bounds.Size), overlay.Bounds);
        Assert.All(LabelCards(chart), card => Assert.True(card.Bounds.Width > 0 && card.Bounds.Height > 0));
    }

    [AvaloniaFact]
    public void The_same_source_index_reuses_its_control_and_updates_point_item_and_text_after_notifications()
    {
        var (chart, series) = CreateChart("scatter");
        var row = new TemplateFeatureRow("business-id", 3, 25, "original");
        series.ItemsSource = new[] { row };
        series.DataLabelFormatter = context => context.Data.Point.Y.ToString("F0");
        var builds = 0;
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => { builds++; return Card(); });
        using var host = new TemplateFeaturesHost(chart);
        var originalCard = Assert.Single(LabelCards(chart));
        var originalContext = Assert.IsType<ChartDataLabelContext>(originalCard.DataContext);
        Assert.Equal("25", originalContext.Text);
        row.Y = 45;
        host.Drain();
        _ = TemplateFeaturesHost.Record(chart);
        _ = TemplateFeaturesHost.Record(chart);
        Assert.Same(originalCard, Assert.Single(LabelCards(chart)));
        Assert.Equal(1, builds);
        var current = Assert.IsType<ChartDataLabelContext>(originalCard.DataContext);
        Assert.Same(row, current.Data.Item);
        Assert.Equal(originalContext.Data.StableId, current.Data.StableId);
        Assert.Equal(0, current.Data.SourceIndex);
        Assert.Equal(new ChartPoint(3, 45), current.Data.Point);
        Assert.Equal("45", current.Text);
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(2)]
    public void The_candidate_budget_limits_real_builds_and_repeated_render_does_not_rebuild(int budget)
    {
        var (chart, series) = CreateChart("scatter");
        series.ItemsSource = Enumerable.Range(0, 10).Select(index => new TemplateFeatureRow($"id-{index}", .5 + index, 20 + index * 5, $"row {index}")).ToArray();
        series.MaxDataLabels = budget;
        var builtContexts = new List<ChartDataLabelContext>();
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((context, _) => { builtContexts.Add(context); return Card(); });
        using var host = new TemplateFeaturesHost(chart);
        Assert.Equal(budget, LabelCards(chart).Length);
        Assert.Equal(budget, builtContexts.Count);
        Assert.Equal(budget, builtContexts.Select(context => context.Data.SourceIndex).Distinct().Count());
        var before = builtContexts.Count;
        _ = TemplateFeaturesHost.Record(chart);
        host.Drain();
        _ = TemplateFeaturesHost.Record(chart);
        Assert.Equal(before, builtContexts.Count);
    }

    [AvaloniaTheory]
    [InlineData("disable")]
    [InlineData("template")]
    [InlineData("remove")]
    [InlineData("detach")]
    public void Disabling_replacing_removing_and_detaching_release_the_old_control_and_context(string action)
    {
        var (chart, series) = CreateChart("line");
        series.ItemsSource = new[] { new TemplateFeatureRow("id", 5, 50, "item") };
        var original = Card();
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => original);
        using var host = new TemplateFeaturesHost(chart);
        Assert.Same(original, Assert.Single(LabelCards(chart)));
        if (action == "disable") series.ShowDataLabels = false;
        else if (action == "template") series.DataLabelTemplate = null;
        else if (action == "remove") chart.Series.Remove(series);
        else host.Window.Content = null;
        host.Drain();
        TemplateFeaturesHost.AssertReleased(original);
    }

    [AvaloniaFact]
    public void Null_build_hides_a_label_without_falling_back_and_is_not_retried_by_render()
    {
        var (chart, series) = CreateChart("scatter");
        series.DataLabelBackground = Brushes.HotPink;
        var builds = 0;
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => { builds++; return null; });
        using var host = new TemplateFeaturesHost(chart);
        Assert.Empty(LabelCards(chart));
        var before = builds;
        var drawing = TemplateFeaturesHost.Record(chart);
        _ = TemplateFeaturesHost.Record(chart);
        Assert.Equal(before, builds);
        Assert.DoesNotContain(TemplateFeaturesHost.Walk(drawing).OfType<GeometryDrawing>(), item => item.Brush is ISolidColorBrush brush && brush.Color == Colors.HotPink);
    }

    [AvaloniaTheory]
    [InlineData("disable")]
    [InlineData("source")]
    [InlineData("template")]
    public void Build_reentrancy_does_not_publish_a_child_for_an_obsolete_source_or_template(string action)
    {
        var (chart, series) = CreateChart("scatter");
        series.ItemsSource = new[] { new TemplateFeatureRow("old", 3, 30, "old source") };
        var obsolete = Card();
        var changed = false;
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) =>
        {
            if (changed) return Card();
            changed = true;
            if (action == "disable") series.ShowDataLabels = false;
            else if (action == "source") series.ItemsSource = new[] { new TemplateFeatureRow("new", 7, 70, "new source") };
            else series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => Card());
            return obsolete;
        });
        using var host = new TemplateFeaturesHost(chart);
        host.Drain();
        Assert.DoesNotContain(chart.GetVisualDescendants(), item => ReferenceEquals(item, obsolete));
        TemplateFeaturesHost.AssertReleased(obsolete);
        if (action == "disable") Assert.Empty(LabelCards(chart));
        else
        {
            var current = Assert.IsType<ChartDataLabelContext>(Assert.Single(LabelCards(chart)).DataContext);
            Assert.Equal(action == "source" ? "new" : "old", current.Data.StableId);
        }
    }

    [AvaloniaFact]
    public void Inherited_context_callbacks_cannot_leave_an_orphaned_visual_or_business_context()
    {
        var (chart, series) = CreateChart("scatter");
        series.ItemsSource = new[] { new TemplateFeatureRow("id", 5, 50, "record") };
        var obsolete = Card();
        obsolete.DataContextChanged += (_, _) => { if (obsolete.DataContext is ChartDataLabelContext) series.ShowDataLabels = false; };
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => obsolete);
        using var host = new TemplateFeaturesHost(chart);
        host.Drain();
        Assert.Empty(LabelCards(chart));
        TemplateFeaturesHost.AssertReleased(obsolete);
    }

    [AvaloniaFact]
    public void A_parented_template_result_cannot_steal_a_business_control_or_its_context()
    {
        var (chart, series) = CreateChart("scatter");
        series.ShowDataLabels = false;
        using var host = new TemplateFeaturesHost(chart);
        var externalContext = new object();
        var child = Card();
        var originalOwner = new Border { DataContext = externalContext, Child = child };
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => child);
        Assert.Throws<InvalidOperationException>(() => { series.ShowDataLabels = true; host.Drain(); });
        Assert.Same(child, originalOwner.Child);
        Assert.Same(originalOwner, child.GetVisualParent());
        Assert.Same(originalOwner, ((ILogical)child).LogicalParent);
        Assert.Same(externalContext, child.DataContext);
        Assert.Empty(LabelCards(chart));
    }

    [AvaloniaFact]
    public void Compiled_templates_inherit_resources_and_rebind_the_original_business_item()
    {
        var view = new TemplateFeaturesConsumerView();
        var first = new TemplateFeaturesModel();
        first.LegendTemplate = (IDataTemplate)view.Resources["BusinessLegendTemplate"]!;
        first.LabelTemplate = (IDataTemplate)view.Resources["BusinessLabelTemplate"]!;
        view.DataContext = first;
        var chart = view.FindControl<CartesianChart>("TemplateChart")!;
        using var host = new TemplateFeaturesHost(chart, view);
        var label = Assert.Single(chart.GetVisualDescendants().OfType<Border>(), control => control.Name == "CompiledLabelRoot");
        Assert.Same(first.Records[0], Assert.IsType<ChartDataLabelContext>(label.DataContext).Data.Item);
        Assert.Equal("original item", Assert.Single(label.GetVisualDescendants().OfType<TextBlock>(), control => control.Name == "CompiledOriginalName").Text);
        var legend = Assert.Single(chart.GetVisualDescendants().OfType<Border>(), control => control.Name == "CompiledLegendRoot");
        Assert.Equal(first.Title, Assert.Single(legend.GetVisualDescendants().OfType<TextBlock>(), control => control.Name == "CompiledSeriesTitle").Text);
        first.Records[0].Name = "updated original item";
        first.Title = "updated series";
        host.Drain();
        Assert.Equal("updated original item", Assert.Single(label.GetVisualDescendants().OfType<TextBlock>(), control => control.Name == "CompiledOriginalName").Text);
        Assert.Equal("updated series", Assert.Single(legend.GetVisualDescendants().OfType<TextBlock>(), control => control.Name == "CompiledSeriesTitle").Text);
        view.Resources["BusinessTemplateAccent"] = Brushes.Orange;
        host.Drain();
        Assert.Equal(Colors.Orange, Assert.IsAssignableFrom<ISolidColorBrush>(label.Background).Color);
        Assert.Equal(Colors.Orange, Assert.IsAssignableFrom<ISolidColorBrush>(legend.Background).Color);
        var second = new TemplateFeaturesModel { Title = "new VM", Records = [new("second-id", 4, 40, "new VM item")], LegendTemplate = first.LegendTemplate, LabelTemplate = first.LabelTemplate };
        view.DataContext = second;
        host.Drain();
        var current = Assert.Single(chart.GetVisualDescendants().OfType<Border>(), control => control.Name == "CompiledLabelRoot");
        Assert.Same(second.Records[0], Assert.IsType<ChartDataLabelContext>(current.DataContext).Data.Item);
        Assert.Equal("new VM item", Assert.Single(current.GetVisualDescendants().OfType<TextBlock>(), control => control.Name == "CompiledOriginalName").Text);
    }

    [AvaloniaFact]
    public void A_negative_bar_places_its_inside_end_control_between_the_baseline_and_the_value()
    {
        var (chart, series) = CreateChart("bar");
        chart.YAxis.Minimum = -100;
        var row = new TemplateFeatureRow("negative", 5, -60, "negative order");
        series.ItemsSource = new[] { row };
        series.DataLabelPosition = ChartDataLabelPosition.InsideEnd;
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => Card());
        using var host = new TemplateFeaturesHost(chart);
        var card = Assert.Single(LabelCards(chart));
        var chrome = Assert.IsType<Border>(card.GetVisualParent());
        var actual = TemplateFeaturesHost.BoundsInChart(chrome, chart);
        var zeroY = chart.PlotArea.Top + chart.PlotArea.Height / 2;
        var valueY = chart.PlotArea.Bottom - chart.PlotArea.Height * .2;
        Assert.True(actual.Top >= zeroY && actual.Bottom <= valueY);
        Assert.True(actual.Center.Y > zeroY);
        var context = Assert.IsType<ChartDataLabelContext>(card.DataContext);
        Assert.Same(row, context.Data.Item);
        Assert.Equal(-60, context.Data.Point.Y);
        Assert.Equal(valueY, context.Position.Y, 8);
    }

    [AvaloniaFact]
    public void Auto_labels_remain_inside_plot_edges_and_share_the_correct_compressed_break_clip()
    {
        var (chart, series) = CreateChart("scatter");
        chart.XAxis.ScaleBreaks.Add(new ChartScaleBreak { Start = 4, End = 6, CompressionRatio = .1 });
        series.ItemsSource = new[]
        {
            new TemplateFeatureRow("lower-edge", .0001, .0001, "edge"),
            new TemplateFeatureRow("upper-edge", 9.9999, 99.9999, "edge"),
            new TemplateFeatureRow("near-gap", 3.9, 50, "near gap"),
            new TemplateFeatureRow("inside-gap", 5, 50, "must not build")
        };
        series.DataLabelPosition = ChartDataLabelPosition.Auto;
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => Card());
        using var host = new TemplateFeaturesHost(chart);
        var cards = LabelCards(chart);
        Assert.Equal(3, cards.Length);
        Assert.DoesNotContain(cards, card => Equals(((ChartDataLabelContext)card.DataContext!).Data.StableId, "inside-gap"));
        foreach (var card in cards)
        {
            var chrome = Assert.IsType<Border>(card.GetVisualParent());
            Assert.True(chart.PlotArea.Contains(TemplateFeaturesHost.BoundsInChart(chrome, chart)));
            var clippedLayer = Assert.Single(chrome.GetVisualAncestors().OfType<Control>(), control => control.Clip is CombinedGeometry);
            var clip = Assert.IsType<CombinedGeometry>(clippedLayer.Clip);
            Assert.Equal(GeometryCombineMode.Exclude, clip.GeometryCombineMode);
            Assert.Equal(chart.PlotArea, Assert.IsType<RectangleGeometry>(clip.Geometry1).Rect);
            var excluded = Assert.IsType<RectangleGeometry>(clip.Geometry2).Rect;
            // A two-unit break compressed to 10% leaves 8 + .2 screen units.
            // Check the recorded clip structure, not the Headless dummy geometry's
            // platform FillContains implementation.
            Assert.Equal(chart.PlotArea.Left + chart.PlotArea.Width * 4 / 8.2, excluded.Left, 7);
            Assert.Equal(chart.PlotArea.Width * .2 / 8.2, excluded.Width, 7);
            Assert.Equal(chart.PlotArea.Top, excluded.Top);
            Assert.Equal(chart.PlotArea.Height, excluded.Height);
        }
    }

    [AvaloniaFact]
    public void Plain_and_control_labels_share_overlap_avoidance_and_release_the_space_when_disabled()
    {
        var (chart, series) = CreateChart("scatter");
        series.ItemsSource = new[] { new TemplateFeatureRow("template", 5, 50, "template") };
        series.DataLabelPosition = ChartDataLabelPosition.Center;
        series.DataLabelOverlap = ChartDataLabelOverlap.Hide;
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => Card());
        var plain = new ScatterSeries
        {
            ItemsSource = new ChartPoint[] { new(5, 50) }, ShowDataLabels = true,
            DataLabelPosition = ChartDataLabelPosition.Center, DataLabelOverlap = ChartDataLabelOverlap.Hide,
            DataLabelFormatter = _ => "plain", DataLabelBackground = Brushes.HotPink,
        };
        chart.Series.Add(plain);
        using var host = new TemplateFeaturesHost(chart);
        int PlainBoxes() => TemplateFeaturesHost.Walk(TemplateFeaturesHost.Record(chart)).OfType<GeometryDrawing>()
            .Count(item => item.Brush is ISolidColorBrush brush && brush.Color == Colors.HotPink);
        Assert.Single(LabelCards(chart));
        Assert.Equal(0, PlainBoxes());
        plain.DataLabelOverlap = ChartDataLabelOverlap.Allow;
        host.Drain();
        Assert.Equal(1, PlainBoxes());
        plain.DataLabelOverlap = ChartDataLabelOverlap.Hide;
        series.ShowDataLabels = false;
        host.Drain();
        Assert.Empty(LabelCards(chart));
        Assert.Equal(1, PlainBoxes());
    }

    [AvaloniaFact]
    public void A_failed_build_is_cleaned_up_not_retried_by_render_and_a_replacement_template_recovers()
    {
        var (chart, series) = CreateChart("scatter");
        series.ShowDataLabels = false;
        series.DataLabelBackground = Brushes.HotPink;
        using var host = new TemplateFeaturesHost(chart);
        var attempts = 0;
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) =>
        { attempts++; throw new InvalidOperationException("application data-label template failed"); });
        Assert.Throws<InvalidOperationException>(() => { series.ShowDataLabels = true; host.Drain(); });
        Assert.Equal(1, attempts);
        Assert.Empty(LabelCards(chart));
        host.Drain();
        var fallback = TemplateFeaturesHost.Record(chart);
        _ = TemplateFeaturesHost.Record(chart);
        Assert.Equal(1, attempts);
        Assert.Contains(TemplateFeaturesHost.Walk(fallback).OfType<GeometryDrawing>(), item => item.Brush is ISolidColorBrush brush && brush.Color == Colors.HotPink);
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => Card());
        host.Drain();
        Assert.Equal(2, LabelCards(chart).Length);
        Assert.All(LabelCards(chart), card => Assert.IsType<ChartDataLabelContext>(card.DataContext));
    }

    [AvaloniaTheory]
    [InlineData(96d)]
    [InlineData(192d)]
    public void Export_records_each_template_subtree_once_without_rebuilding_or_changing_the_view(double dpi)
    {
        var (chart, series) = CreateChart("scatter");
        var builds = 0;
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => { builds++; return Card(); });
        using var host = new TemplateFeaturesHost(chart);
        var cards = LabelCards(chart);
        var bounds = chart.Bounds;
        var plot = chart.PlotArea;
        var view = chart.VisibleRange;
        var before = builds;
        var recording = new DrawingGroup();
        using (var context = recording.Open()) chart.RenderExport(context, chart.GetExportLayout(new() { PixelSize = new(960, 540), Dpi = dpi }), Brushes.White);
        var visualBrushes = TemplateFeaturesHost.Walk(recording).OfType<GeometryDrawing>().Select(drawing => drawing.Brush).OfType<VisualBrush>().ToArray();
        foreach (var card in cards)
            Assert.Single(visualBrushes, brush => ReferenceEquals(brush.Visual, card) || brush.Visual?.GetVisualDescendants().Any(visual => ReferenceEquals(visual, card)) == true);
        Assert.Equal(before, builds);
        Assert.Equal(bounds, chart.Bounds);
        Assert.Equal(plot, chart.PlotArea);
        Assert.Equal(view, chart.VisibleRange);
        Assert.Null(chart.Viewport);
    }

    private static Border Card() => new() { Name = "LabelTemplateRoot", Width = 72, Height = 20, Child = new TextBlock { Text = "business label" } };
    private static Border[] LabelCards(CartesianChart chart) => chart.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "LabelTemplateRoot").ToArray();
    private static (CartesianChart Chart, ChartSeries Series) CreateChart(string kind)
    {
        var chart = new CartesianChart { ShowLegend = false, Padding = new Thickness(8), XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        ChartSeries series = kind switch { "line" => new LineSeries(), "bar" => new BarSeries(), _ => new ScatterSeries() };
        series.ItemsSource = new[] { new TemplateFeatureRow("first", 2, 20, "first"), new TemplateFeatureRow("second", 8, 70, "second") };
        series.CategoryPath = nameof(TemplateFeatureRow.X);
        series.ValuePath = nameof(TemplateFeatureRow.Y);
        series.IdPath = nameof(TemplateFeatureRow.Id);
        series.ShowDataLabels = true;
        series.DataLabelOverlap = ChartDataLabelOverlap.Allow;
        chart.Series.Add(series);
        return (chart, series);
    }
    private static Point Project(CartesianChart chart, double x, double y) =>
        new(chart.PlotArea.Left + x / 10 * chart.PlotArea.Width, chart.PlotArea.Bottom - y / 100 * chart.PlotArea.Height);
}
