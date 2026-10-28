using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class SharedPresentationFamiliesTests
{
    private static readonly IDataTemplate LabelTemplate = new FuncDataTemplate<ChartDataLabelItemContext>((item, _) => new TextBlock { Text = item?.Text });
    private static readonly IDataTemplate LegendTemplate = new FuncDataTemplate<ChartDataLegendItemContext>((item, _) => new TextBlock { Text = item?.Text });

    [AvaloniaFact]
    public void Mosaic_aggregate_labels_keep_full_original_members_and_do_not_replace_column_headers()
    {
        MosaicObservation[] rows = [new("a", "ColumnA", "RowA", 2), new("b", "ColumnB", "RowB", 4), new("c", "ColumnA", "RowA", 3)];
        var chart = new MosaicChart { ItemsSource = rows, DataLabelTemplate = LabelTemplate };
        using var host = new Host(chart);
        var label = Assert.Single(chart.GetDataLabels(), label => label.Members.Count == 2);
        Assert.Equal([0, 2], label.Members.Select(member => member.SourceIndex));
        Assert.Same(rows[0], label.Members[0].Item); Assert.Same(rows[2], label.Members[1].Item);
        Assert.Same(label.EngineeringHit!.Members, label.Members);
        Assert.DoesNotContain("ColumnA", string.Join("|", chart.GetDataLabels().Select(label => label.Text)), StringComparison.Ordinal);
        Assert.Contains("ColumnA", Text(Record(chart)), StringComparison.Ordinal);
        Assert.DoesNotContain("RowA:", Text(Record(chart)), StringComparison.Ordinal);
        chart.ShowLabels = false; chart.ShowValues = false; host.Drain(); Assert.Empty(chart.GetDataLabels());
    }

    [AvaloniaFact]
    public void Hierarchy_legend_preserves_terminal_categories_and_decorative_size_key()
    {
        var a = new Node("a", 1, "west"); var b = new Node("b", 9, "east"); var c = new Node("c", 4, "west");
        var chart = new PackedBubbleChart { ItemsSource = new[] { a, b, c }, IdPath = "Id", GroupPath = "Group", ShowLegend = true, LegendItemTemplate = LegendTemplate };
        using var host = new Host(chart);
        var west = Assert.Single(chart.GetLegendItems(), item => item.Kind == ChartDataLegendKind.Category && item.Text.StartsWith("west", StringComparison.Ordinal));
        Assert.Equal(5, west.Value); Assert.Equal(new object?[] { a, c }, west.Members.Select(member => member.Item));
        Assert.Equal(3, chart.GetLegendItems().Count);
        var size = Assert.Single(chart.GetLegendItems(), item => item.Kind == ChartDataLegendKind.Size);
        Assert.Empty(size.Members); Assert.Equal(1d, size.Minimum); Assert.Equal(9d, size.Maximum);
        Assert.DoesNotContain("Relative area", Text(Record(chart)), StringComparison.Ordinal);
        chart.ShowSizeLegend = false; host.Drain(); Assert.DoesNotContain(chart.GetLegendItems(), item => item.Kind == ChartDataLegendKind.Size);
        chart.ShowLegend = false; host.Drain(); Assert.Empty(chart.GetLegendItems());
    }

    [AvaloniaFact]
    public void Hierarchy_shared_labels_use_current_terminals_after_collapse_and_drill()
    {
        var child = new Node("child", 3, "west"); var parent = new Node("parent", 0, "west", child);
        var chart = new TreemapChart { ItemsSource = new[] { parent }, IdPath = "Id", GroupPath = "Group", ShowLegend = true, DataLabelTemplate = LabelTemplate };
        using var host = new Host(chart);
        Assert.Contains(chart.GetDataLabels(), label => Equals(label.Members[0].StableId, "child"));
        Assert.DoesNotContain(chart.GetDataLabels(), label => Equals(label.Members[0].StableId, "parent"));
        chart.Collapse("parent"); host.Drain();
        Assert.Same(parent, Assert.Single(chart.GetDataLabels()).Members[0].Item);
        Assert.True(chart.DrillDown("parent")); host.Drain();
        Assert.All(chart.GetDataLabels(), label => Assert.Contains(label.Members[0].Item, new object[] { parent, child }));
    }

    [AvaloniaFact]
    public void Hierarchy_category_keys_keep_original_terminals_when_visual_gap_collapses_their_glyphs()
    {
        var a = new Node("a", 2, "west"); var b = new Node("b", 3, "west");
        var chart = new TreemapChart { ItemsSource = new[] { a, b }, IdPath = "Id", GroupPath = "Group", Gap = 4096, ShowLegend = true, LegendItemTemplate = LegendTemplate };
        using var host = new Host(chart); Assert.Null(chart.DataError); Assert.Empty(chart.GetMarks());
        var key = Assert.Single(chart.GetLegendItems()); Assert.Equal(5, key.Value);
        Assert.Equal(new object?[] { a, b }, key.Members.Select(member => member.Item));
        Assert.Equal(new object?[] { "a", "b" }, key.Members.Select(member => member.StableId));
    }

    [AvaloniaFact]
    public void Map_continuous_legend_retains_the_layer_range_gradient_and_original_member_domain()
    {
        var a = new object(); var b = new object();
        using var chart = Map();
        var layer = new ShapeLayer { ItemsSource = new[] { new MapValue("a", 2, a), new MapValue("b", 7, b) }, Opacity = .4 };
        chart.Layers.Add(layer); chart.LegendItemTemplate = LegendTemplate; chart.DataLabelTemplate = LabelTemplate;
        using var host = new Host(chart);
        var key = Assert.Single(chart.GetLegendItems());
        Assert.Equal(ChartDataLegendKind.Continuous, key.Kind); Assert.Equal(2d, key.Minimum); Assert.Equal(7d, key.Maximum);
        var brush = Assert.IsType<LinearGradientBrush>(key.Brush); Assert.Equal(.4, brush.Opacity); Assert.Equal(2, brush.GradientStops.Count);
        Assert.Equal(new object?[] { a, b }, key.Members.Select(member => member.Item)); Assert.Equal(2, chart.GetDataLabels().Count);
        Assert.All(chart.GetDataLabels(), label => Assert.Same(label.EngineeringHit!.Members, label.Members));
        var withTemplate = chart.PlotArea; chart.LegendItemTemplate = null; host.Drain(); Assert.True(chart.PlotArea.Height > withTemplate.Height);
        chart.ShowLegend = false; host.Drain(); Assert.Empty(chart.GetLegendItems());
    }

    [AvaloniaFact]
    public void Map_no_data_feature_labels_remain_backgrounds_without_fabricated_label_members()
    {
        using var chart = Map(); chart.Layers.Add(new ShapeLayer { ItemsSource = new[] { new MapValue("a", 2) } });
        chart.DataLabelTemplate = LabelTemplate; using var host = new Host(chart);
        Assert.Single(chart.GetDataLabels()); Assert.Single(chart.GetMarks());
        Assert.Contains("NoDataB", Text(Record(chart)), StringComparison.Ordinal);
        Assert.DoesNotContain("DataA:", Text(Record(chart)), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Map_continuous_legend_retains_collapsed_originals_that_determine_its_domain()
    {
        var visible = new object(); var collapsed = new object();
        using var chart = new ShapeMap
        {
            Projection = MapProjection.WebMercator, ShowLegend = true, LegendItemTemplate = LegendTemplate,
            FeaturesSource = new[] { new MapFeature("visible", [new MapPolygon(Ring(0, 0, 10, 10))]), new MapFeature("pole", [new MapPolygon(Ring(20, 86, 30, 89))]) },
        };
        chart.Layers.Add(new ShapeLayer { ItemsSource = new[] { new MapValue("visible", 2, visible), new MapValue("pole", 100, collapsed) } });
        using var host = new Host(chart); Assert.Null(chart.DataError); Assert.Single(chart.GetMarks());
        Assert.Equal(1, chart.ProjectedGeometry!.CollapsedFeatureCount);
        var key = Assert.Single(chart.GetLegendItems()); Assert.Equal(100d, key.Maximum);
        Assert.Equal(new object?[] { visible, collapsed }, key.Members.Select(member => member.Item));
        Assert.Equal(new[] { 0, 1 }, key.Members.Select(member => member.SourceIndex));
        Assert.Equal(100d, key.Members[1].Value);
    }

    [AvaloniaFact]
    public void Map_categorical_keys_keep_layers_and_categories_separate_even_when_source_indices_match()
    {
        using var chart = Map();
        var first = new[] { new MapRow("a", 2, "same"), new MapRow("b", 3, "same") };
        var second = new[] { new MapRow("a", 4, "same") };
        chart.Layers.Add(new ShapeLayer { ItemsSource = first, ColorMode = MapColorMode.Categorical, CategoryPath = "Category" });
        chart.Layers.Add(new ShapeLayer { ItemsSource = second, ColorMode = MapColorMode.Categorical, CategoryPath = "Category" });
        chart.LegendItemTemplate = LegendTemplate; using var host = new Host(chart);
        var keys = chart.GetLegendItems(); Assert.Equal(2, keys.Count); Assert.NotEqual(keys[0].Key, keys[1].Key);
        Assert.Equal(new object?[] { first[0], first[1] }, keys[0].Members.Select(member => member.Item));
        Assert.Same(second[0], Assert.Single(keys[1].Members).Item);
    }

    [AvaloniaFact]
    public void Relation_labels_and_legends_keep_node_and_edge_identity_domains_separate()
    {
        var nodeA = new object(); var nodeB = new object(); var edge = new object();
        using var chart = new FlowChart
        {
            Mode = RelationFlowMode.Explicit, ShowGroups = true, ShowLabels = true, ShowEdgeLabels = true,
            ItemsSource = new[] { new GraphNode("same", "NodeA", nodeA, "Group", position: new(.25, .5)), new GraphNode("b", "NodeB", nodeB, "Group", position: new(.75, .5)) },
            EdgesSource = new[] { new GraphEdge("same", "b", 5, "EdgeLabel", edge, "same") },
            LegendItemTemplate = LegendTemplate, DataLabelTemplate = LabelTemplate,
        };
        using var host = new Host(chart); Assert.Null(chart.DataError);
        var labels = chart.GetDataLabels(); Assert.Equal(3, labels.Count);
        Assert.Contains(labels, label => ReferenceEquals(Assert.Single(label.Members).Item, nodeA));
        Assert.Contains(labels, label => ReferenceEquals(Assert.Single(label.Members).Item, edge));
        var group = Assert.Single(chart.GetLegendItems()); Assert.Equal("Group", group.Text);
        Assert.Equal(new object?[] { nodeA, nodeB }, group.Members.Select(member => member.Item));
        var painted = Text(Record(chart)); Assert.Contains("Group", painted, StringComparison.Ordinal);
        Assert.DoesNotContain("NodeA", painted, StringComparison.Ordinal); Assert.DoesNotContain("EdgeLabel", painted, StringComparison.Ordinal);
        chart.ShowEdgeLabels = false; host.Drain(); Assert.Equal(2, chart.GetDataLabels().Count);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Schedule_labels_keep_existing_collision_or_bar_placement_and_original_sources(int kind)
    {
        var source = new object(); var day = DateTimeOffset.UnixEpoch;
        using ScheduleChartBase chart = kind switch
        {
            0 => new GanttChart { ItemsSource = new[] { new ScheduleTask("task", day, day.AddDays(2), Label: "TaskLabel", Source: source) } },
            1 => new EventTimelineChart { ItemsSource = new[] { new ScheduleEvent("event", day, "EventLabel", Source: source) } },
            2 => new SpiralTimelineChart { ItemsSource = new[] { new ScheduleEvent("event", day, "SpiralLabel", Source: source) } },
            _ => new SwimlaneChart { ItemsSource = new[] { new ScheduleSpan("task", 0, 2, "Lane", "SpanLabel", source) } },
        };
        chart.DataLabelTemplate = LabelTemplate; using var host = new Host(chart);
        Assert.Null(chart.DataError); var label = Assert.Single(chart.GetDataLabels()); Assert.Same(source, Assert.Single(label.Members).Item);
        Assert.Same(label.EngineeringHit!.Members, label.Members);
        Assert.DoesNotContain(label.Text, Text(Record(chart)), StringComparison.Ordinal);
        if (chart is EventTimelineChart events) Assert.Equal(Assert.Single(events.LabelPlacements).Bounds.Center, label.Anchor);
        chart.ShowLabels = false; host.Drain(); Assert.Empty(chart.GetDataLabels());
    }

    [AvaloniaFact]
    public void Paired_endpoints_keep_distinct_marks_for_the_same_original_record()
    {
        var source = new PairedComparisonValue(2, 8, "Pair");
        var chart = new DumbbellChart { ItemsSource = new[] { source }, DataLabelTemplate = LabelTemplate };
        using var host = new Host(chart);
        var labels = chart.GetDataLabels(); Assert.Equal(3, labels.Count);
        Assert.Equal(new object?[] { "low", "high", "high" }, labels.Select(label => label.EngineeringHit!.Mark.Key));
        Assert.All(labels, label => Assert.Same(label.EngineeringHit!.Members, label.Members));
    }

    [AvaloniaFact]
    public void Table_templates_replace_cell_data_while_headers_stay_drawn_and_all_cells_share_real_row_identity()
    {
        var row = new { Id = "a", Label = "RowHeader", Amount = 3, Name = "BusinessName" };
        var chart = new TableChart
        {
            ItemsSource = new[] { row }, DataLabelTemplate = LabelTemplate,
            Columns = new[] { new OperationalTableColumn("AmountHeader", "Amount"), new OperationalTableColumn("NameHeader", "Name") { UseColorScale = false } },
        };
        using var host = new Host(chart);
        Assert.Equal(2, chart.GetDataLabels().Count); Assert.All(chart.GetDataLabels(), label => Assert.Same(row, Assert.Single(label.Members).Item));
        var painted = Text(Record(chart)); Assert.Contains("AmountHeader", painted, StringComparison.Ordinal); Assert.DoesNotContain("BusinessName", painted, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void KPI_templates_preserve_label_value_and_signed_delta_as_separate_readings_of_the_actual_card()
    {
        var chart = new KpiCard { Value = 120, Previous = 100, Label = "Revenue", Subtitle = "Monthly", ShowSparkline = false, DataLabelTemplate = LabelTemplate };
        using var host = new Host(chart);
        var labels = chart.GetDataLabels(); Assert.Equal(4, labels.Count); Assert.All(labels, label => Assert.Same(chart, Assert.Single(label.Members).Item));
        Assert.Single(labels, label => label.Text.StartsWith("Δ ", StringComparison.Ordinal));
        Assert.DoesNotContain("Revenue", Text(Record(chart)), StringComparison.Ordinal); Assert.DoesNotContain("Δ ", Text(Record(chart)), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Empty_comparison_label_metadata_does_not_bypass_the_family_label_gate()
    {
        var chart = new MosaicChart { ItemsSource = new[] { new MosaicObservation("a", "Column", "Row", 5) }, ShowLabels = false, ShowValues = true, DataLabelTemplate = LabelTemplate };
        using var host = new Host(chart); Assert.NotEmpty(chart.GetMarks()); Assert.Empty(chart.GetDataLabels());
        chart.ShowLabels = true; host.Drain(); Assert.Single(chart.GetDataLabels());
        chart.ShowLabels = false; host.Drain(); Assert.Empty(chart.GetDataLabels());
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Explicit_point_label_families_respect_category_and_value_text_gates(int kind)
    {
        ComparisonChartBase chart = kind switch
        {
            0 => new StripPlot { ItemsSource = new[] { new CategoricalSample("a", "Category", 7, "BusinessLabel") } },
            1 => new CalendarHeatmapChart { ItemsSource = new[] { new CalendarPoint("a", new(2024, 2, 29), 7) } },
            _ => new ParallelCoordinatesChart { ItemsSource = new[] { new ParallelRecord("a", new double[] { 2, 8 }, "BusinessLabel") }, Axes = [new ParallelAxis { Header = "First" }, new ParallelAxis { Header = "Second" }] },
        };
        chart.DataLabelTemplate = LabelTemplate; chart.ShowLabels = false; chart.ShowValues = true;
        using var host = new Host(chart);
        Assert.Equal(kind == 2 ? "2, 8" : "7", Assert.Single(chart.GetDataLabels()).Text);
        chart.ShowLabels = true; chart.ShowValues = false; host.Drain();
        var text = Assert.Single(chart.GetDataLabels()).Text;
        Assert.NotEqual(kind == 2 ? "2, 8" : "7", text); Assert.DoesNotContain(":", text, StringComparison.Ordinal);
        chart.ShowLabels = false; host.Drain(); Assert.Empty(chart.GetDataLabels());
    }

    private static SeatMap Map() => new()
    {
        FeaturesSource = new[] { new MapFeature("a", [new MapPolygon(Ring(0, 0, 5, 5))], "DataA"), new MapFeature("b", [new MapPolygon(Ring(6, 0, 11, 5))], "NoDataB") },
        ShowLegend = true, ShowLabels = true,
    };
    private static MapCoordinate[] Ring(double x, double y, double right, double top) => [new(x, y), new(right, y), new(right, top), new(x, top), new(x, y)];
    private sealed record Node(string Id, double Value, string Group, params Node[] Children) { public string Label => Id; }
    private sealed record MapRow(string RegionId, double Value, string Category);
    private static DrawingGroup Record(Control chart) { var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context); return group; }
    private static string Text(Drawing drawing) => string.Concat(Glyphs(drawing).Select(glyph => glyph.GlyphRun is { } run ? new string(run.Characters.Span) : ""));
    private static IEnumerable<GlyphRunDrawing> Glyphs(Drawing drawing) { if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var glyph in Glyphs(child)) yield return glyph; else if (drawing is GlyphRunDrawing glyph) yield return glyph; }
    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        public Host(Control chart) { _window = new Window { Width = 640, Height = 420, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
