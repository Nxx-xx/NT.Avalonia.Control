using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private static readonly string[][] C6Groups = [
        ["gantt", "swimlane", "event", "spiral"],
        ["bullet", "kpi", "calendar", "matrix", "table", "waffle"],
        ["word", "bump", "slope", "parliament", "river"],
        ["flame", "icicle", "dendrogram", "indented", "radial"],
        ["map-marker", "map-line", "map-vector", "map-pie", "map-category", "map-seat"]];
    private static readonly string[] C6GroupNames = ["schedule", "operations-a", "operations-b", "hierarchy", "maps"];
    private readonly List<EngineeringChartBase> _c6Charts = [];
    private static readonly DateTimeOffset C6Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private sealed record C6Region(string Id, string RegionId, double Value, string Category, string Label);
    private static EngineeringChartBase CreateC6Chart(string kind)
    {
        var events = new[] { new ScheduleEvent("e-a", C6Start, "A", 1), new ScheduleEvent("e-b", C6Start.AddDays(2.5), "B", 2), new ScheduleEvent("e-c", C6Start.AddDays(5), "C", 3) };
        var ranks = new[] { new RankingPoint("r-a", "A", "P1", 10), new RankingPoint("r-b", "B", "P1", 20), new RankingPoint("r-c", "A", "P2", 30), new RankingPoint("r-d", "B", "P2", 15) };
        var cells = new[] { new MatrixPoint("m-a", "A", "X", 1, "AX"), new MatrixPoint("m-b", "A", "Y", 2, "AY"), new MatrixPoint("m-c", "B", "X", 3, "BX"), new MatrixPoint("m-d", "B", "Y", 4, "BY") };
        var root = new HierarchyNode(8, [new HierarchyNode(6, [new HierarchyNode(4, label: "C", id: "C"), new HierarchyNode(2, label: "D", id: "D")], "A", id: "A"), new HierarchyNode(2, label: "B", id: "B")], "R", id: "R");
        EngineeringChartBase chart = kind switch
        {
            "gantt" => new GanttChart { ItemsSource = new[] { new ScheduleTask("t-a", C6Start, C6Start.AddDays(2), 25, "Alpha", "A"), new ScheduleTask("t-b", C6Start.AddDays(1), C6Start.AddDays(3), 50, "Beta", "B", Dependencies: [new("t-a", ScheduleDependencyType.StartStart, TimeSpan.FromDays(1))]), new ScheduleTask("t-m", C6Start.AddDays(4), C6Start.AddDays(4), 0, "Alpha", "M") }, VisibleStart = C6Start, VisibleEnd = C6Start.AddDays(5) },
            "swimlane" => new SwimlaneChart { ItemsSource = new[] { new ScheduleSpan("s-a", 0, 4, "Alpha", "A"), new ScheduleSpan("s-b", 2, 6, "Alpha", "B"), new ScheduleSpan("s-c", 6, 8, "Alpha", "C"), new ScheduleSpan("s-d", 1, 5, "Beta", "D") }, Minimum = 0, Maximum = 10 },
            "event" => new EventTimelineChart { ItemsSource = events, VisibleStart = C6Start, VisibleEnd = C6Start.AddDays(5) },
            "spiral" => new SpiralTimelineChart { ItemsSource = events, VisibleStart = C6Start, VisibleEnd = C6Start.AddDays(5), Turns = 1, InnerRadius = 30, MarkerSize = 10 },
            "bullet" => new BulletChart { ItemsSource = new[] { new BulletPoint("b", 25, 75, 0, 100, [40, 70, 100], "B") } },
            "kpi" => new KpiCardChart { ItemsSource = new[] { new KpiPoint("k", 120, 100, [80, 100, 120], "K") } },
            "calendar" => new CalendarHeatmapChart { StartDate = new(2026, 1, 5), EndDate = new(2026, 1, 18), FirstDayOfWeek = DayOfWeek.Monday, ItemsSource = new[] { new CalendarPoint("c-a", new(2026, 1, 5), 1), new CalendarPoint("c-b", new(2026, 1, 8), 2), new CalendarPoint("c-c", new(2026, 1, 18), 4) } },
            "matrix" => new MatrixChart { ItemsSource = cells },
            "table" => new TableChart { ItemsSource = cells },
            "waffle" => new WaffleChart { Rows = 4, Columns = 4, ItemsSource = new[] { new WafflePoint("w-a", 1, "A"), new WafflePoint("w-b", 3, "B") } },
            "word" => new WordCloudChart { MinFontSize = 12, MaxFontSize = 24, ItemsSource = new[] { new WordPoint("word-a", "Alpha", 4), new WordPoint("word-b", "Beta", 1), new WordPoint("word-zero", "Zero", 0) } },
            "bump" => new BumpChart { ItemsSource = ranks },
            "slope" => new SlopeChart { ItemsSource = ranks },
            "parliament" => new ParliamentChart { Rows = 2, InnerRadiusFactor = .5, ItemsSource = new[] { new ParliamentParty("pa", 1, "A"), new ParliamentParty("pb", 3, "B") } },
            "river" => new ThemeRiverChart { RowPath = "Stream", ColumnPath = "Slot", ItemsSource = new[] { new ThemeRiverPoint("a-x", "A", "X", 1), new ThemeRiverPoint("a-y", "A", "Y", 2), new ThemeRiverPoint("a-z", "A", "Z", 3), new ThemeRiverPoint("b-x", "B", "X", 3), new ThemeRiverPoint("b-y", "B", "Y", 2), new ThemeRiverPoint("b-z", "B", "Z", 1) } },
            "flame" => new FlameGraph { ItemsSource = new[] { root } },
            "icicle" => new IcicleChart { ItemsSource = new[] { root } },
            "dendrogram" => new DendrogramChart { ItemsSource = new[] { root } },
            "indented" => new IndentedTreeChart { ItemsSource = new[] { root }, IndentSize = 20 },
            "radial" => new RadialTreeChart { ItemsSource = new[] { root }, StartAngle = -90, SweepAngle = 360 },
            _ when kind.StartsWith("map-", StringComparison.Ordinal) => CreateC6Map(kind),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        chart.IdPath = "Id"; chart.LabelPath = "Label";
        chart.Width = 420; chart.Height = 280;
        chart.Palette = [Brush("#4385F4"), Brush("#F2A33A"), Brush("#55B47A"), Brush("#D25573")];
        return chart;
    }
    private static ShapeMap CreateC6Map(string kind)
    {
        ShapeMap map = kind == "map-seat" ? new SeatMap { IsMultiSelectionEnabled = true } : new ShapeMap();
        map.GeoJson = MapFixtureJson; map.IsGeographic = false; map.Projection = MapProjection.Identity;
        var values = new[] { new C6Region("a", "a", 1, "Open", "Alpha"), new C6Region("b", "b", 2, "Closed", "Beta") };
        var shape = new ShapeLayer { Stroke = null, NoDataBrush = Brush("#D5DCE2") };
        map.Layers.Add(shape);
        switch (kind)
        {
            case "map-marker": map.Layers.Add(new MarkerLayer { MarkerSize = 12, MarkerType = MapMarkerType.Circle, Fill = Brush("#4385F4"), Stroke = null, ItemsSource = new[] { new MapObservation("p-a", new(-20, 10), 1), new MapObservation("p-b", new(0, 10), 2), new MapObservation("p-c", new(20, 10), 3) } }); break;
            case "map-line": map.Layers.Add(new LineLayer { IsCurved = false, ShowEndpoints = false, MinimumThickness = 2, MaximumThickness = 6, Stroke = Brush("#4385F4"), ItemsSource = new[] { new MapConnection("l-a", new(-20, 12), new(20, 12), 1), new MapConnection("l-b", new(-20, -12), new(20, -12), 2) } }); break;
            case "map-vector": map.Layers.Add(new VectorLayer { StrokeThickness = 3, Stroke = Brush("#4385F4"), Fill = Brush("#F2A33A"), ItemsSource = new MapVector[] { new MapLineVector("v-line", new(-25, 15), new(-15, 15)), new MapPolylineVector("v-polyline", [new(-5, 12), new(0, 18), new(5, 12)]), new MapPolygonVector("v-polygon", new([new(15, 12), new(25, 12), new(20, 18), new(15, 12)])), new MapCircleVector("v-circle", new(-20, -12), 10), new MapArcVector("v-arc", new(15, -12), 12, 0, 180) } }); break;
            case "map-pie": map.Layers.Add(new PieLayer { Radius = 20, ItemsSource = new[] { new MapPie("pie-a", new(-10, 10), [new(1, "A"), new(0, "Zero"), new(3, "B")]) } }); break;
            case "map-category": shape.ItemsSource = values; shape.ColorMode = MapColorMode.Categorical; shape.CategoryPath = "Category"; shape.CategoryColors = new Dictionary<object, IBrush> { ["Open"] = Brush("#4385F4"), ["Closed"] = Brush("#F2A33A") }; break;
            case "map-seat": shape.ItemsSource = values; break;
        }
        return map;
    }
    private Control CreateC6CompletionExamples() => new Expander { Name = "ChartC6CompletionExamples", Header = "C6 主项：排程、分析排名、层级与地图扩展", HorizontalAlignment = HorizontalAlignment.Stretch, Content = new StackPanel { Spacing = 18, Children = { CreateC6Board(0), CreateC6Board(1), CreateC6Board(2), CreateC6Board(3), CreateC6Board(4) } } };
    private Control CreateC6Board(int group, bool overview = false)
    {
        var names = new[] { "排程 · UTC 日期、依赖、泳道、事件和螺旋", "分析 · 目标、指标卡、日历、矩阵、表格和比例", "排名 · 词云、名次、斜率、议席和主题河流", "层级 · Flame、Icicle、Dendrogram、Indented、Radial", "地图扩展 · Marker、Line、Vector、Pie、分类和多座选择" };
        var charts = C6Groups[group].Select(CreateC6Chart).ToArray();
        if (!overview) _c6Charts.AddRange(charts);
        var grid = new Grid { ColumnDefinitions = new(group == 0 ? "*,*" : "*,*,*"), RowDefinitions = new("Auto,Auto"), ColumnSpacing = 12, RowSpacing = 12 };
        for (var i = 0; i < charts.Length; i++)
        {
            var chart = charts[i]; chart.Width = double.NaN; chart.Height = 300; chart.Title = C6Groups[group][i];
            var columnCount = group == 0 ? 2 : 3; Grid.SetColumn(chart, i % columnCount); Grid.SetRow(chart, i / columnCount); grid.Children.Add(chart);
        }
        var selected = new TextBlock { Text = "点击图形或方向键浏览，Space 选择；Tooltip 返回原始记录。", TextWrapping = TextWrapping.Wrap };
        foreach (var chart in charts) chart.PropertyChanged += (_, change) => { if (change.Property == EngineeringChartBase.SelectedPointProperty && chart.SelectedPoint is { } hit) selected.Text = $"{chart.Title} · {hit.Mark.TooltipText} · 原始序号 {string.Join(", ", hit.Members.Select(p => p.SourceIndex))}"; };
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        AddAction(actions, "缩放 / 平移", () => { foreach (var chart in charts) { chart.Zoom = 1.4; chart.PanX = .03; } }, "共享坐标变换保留原始数据。");
        AddAction(actions, "恢复视图", () => { foreach (var chart in charts) chart.ResetViewport(); }, "恢复初始范围。");
        AddAction(actions, "更新数据", () => { foreach (var chart in charts) UpdateC6Source(chart); }, "替换数据并重新计算视图。");
        if (group == 1)
        {
            AddAction(actions, "单值指标 / 集合", () =>
            {
                var bullet = (BulletChart)charts[0]; var kpi = (KpiCardChart)charts[1]; var waffle = (WaffleChart)charts[5];
                if (bullet.ItemsSource is null)
                {
                    bullet.ItemsSource = new[] { new BulletPoint("b", 25, 75, 0, 100, [40, 70, 100], "B") };
                    kpi.ItemsSource = new[] { new KpiPoint("k", 120, 100, [80, 100, 120], "K") };
                    waffle.ItemsSource = new[] { new WafflePoint("w-a", 1, "A"), new WafflePoint("w-b", 3, "B") };
                }
                else
                {
                    bullet.ItemsSource = null; bullet.Value = 68; bullet.Target = 85; bullet.Ranges = [40, 70, 100]; bullet.Label = "季度目标";
                    kpi.ItemsSource = null; kpi.Value = 1280; kpi.Previous = 1000; kpi.Delta = 28; kpi.Unit = "¥"; kpi.Subtitle = "较上期"; kpi.SparklineData = [800, 950, 900, 1100, 1280]; kpi.Label = "营业额";
                    waffle.ItemsSource = null; waffle.Value = 68; waffle.MaxValue = 100; waffle.Label = "完成率";
                }
            }, "演示 Value、Target、Delta 和 SparklineData 直接绑定入口。");
            AddAction(actions, "表格业务列 / 矩阵", () =>
            {
                var table = (TableChart)charts[4];
                if (table.Columns is null)
                {
                    table.Columns = new[] { new OperationalTableColumn("销量", "sales") { Id = "sales", Format = "0" }, new OperationalTableColumn("增幅", "growth") { Id = "growth", Format = "P0" }, new OperationalTableColumn("状态", "status") { Id = "status", UseColorScale = false } };
                    table.ItemsSource = new[] { new OperationalTableRow("apple", new Dictionary<string, object?> { ["sales"] = 120, ["growth"] = .2, ["status"] = "正常" }, "Apple"), new OperationalTableRow("orange", new Dictionary<string, object?> { ["sales"] = 85, ["growth"] = -.05, ["status"] = "观察" }, "Orange") };
                }
                else
                {
                    table.Columns = null;
                    table.ItemsSource = new[] { new MatrixPoint("m-a", "A", "X", 1, "AX"), new MatrixPoint("m-b", "A", "Y", 2, "AY"), new MatrixPoint("m-c", "B", "X", 3, "BX"), new MatrixPoint("m-d", "B", "Y", 4, "BY") };
                }
            }, "文本、数字、格式化和条件颜色保留原始业务行。");
        }
        if (group == 3)
        {
            foreach (var chart in charts.OfType<HierarchyChartBase>()) chart.AnimationTriggers = ChartAnimationTriggers.All;
            AddAction(actions, "折叠 A", () => { foreach (var chart in charts.OfType<HierarchyChartBase>()) chart.Collapse("A"); }, "折叠层级保留来源。");
            AddAction(actions, "展开 A", () => { foreach (var chart in charts.OfType<HierarchyChartBase>()) chart.Expand("A"); }, "展开完整层级。");
            AddAction(actions, "钻取 A", () => { foreach (var chart in charts.OfType<HierarchyChartBase>()) chart.DrillDown("A"); }, "钻取子树。");
            AddAction(actions, "返回父级", () => { foreach (var chart in charts.OfType<HierarchyChartBase>()) chart.DrillUp(); }, "返回父层级。");
        }
        if (group == 4)
        {
            var seat = (SeatMap)charts[^1];
            AddAction(actions, "切换座位 A / B", () => { seat.ToggleSeat("a"); seat.ToggleSeat("b"); selected.Text = $"已选 {seat.SelectedItems.Count} 个原始座位对象"; }, "多选和原始对象双向绑定。");
            AddAction(actions, "清空座位", seat.ClearSeatSelection, "清空多选。");
            AddAction(actions, "切换符号", () => { var marker = (MarkerLayer)((ShapeMap)charts[0]).Layers[1]; marker.MarkerType = (MapMarkerType)(((int)marker.MarkerType + 1) % 5); }, "五种固定 DIP 符号。");
            AddAction(actions, "直线 / 曲线", () => { var line = (LineLayer)((ShapeMap)charts[1]).Layers[1]; line.IsCurved = !line.IsCurved; }, "同一业务连接的两种表示。");
        }
        var labels = new CheckBox { Content = "标签", IsChecked = true, Margin = new Thickness(8, 0) }; labels.IsCheckedChanged += (_, _) => { foreach (var chart in charts) SetC6Labels(chart, labels.IsChecked == true); }; actions.Children.Add(labels);
        return new StackPanel { Spacing = 8, Children = { new TextBlock { Text = names[group], FontSize = 18, FontWeight = FontWeight.SemiBold }, actions, selected, grid } };
    }
    private static void SetC6Labels(EngineeringChartBase chart, bool enabled)
    {
        switch (chart) { case ScheduleChartBase schedule: schedule.ShowLabels = enabled; break; case OperationalChartBase operational: operational.ShowLabels = operational.ShowValues = enabled; break; case HierarchyChartBase hierarchy: hierarchy.ShowLabels = hierarchy.ShowValues = enabled; break; case ShapeMap map: map.ShowLabels = map.ShowLegend = enabled; break; }
    }
    private static void UpdateC6Source(EngineeringChartBase chart)
    {
        if (chart.ItemsSource is null)
        {
            if (chart is BulletChart bullet && bullet.Value.HasValue) bullet.Value = (bullet.Value.Value + 5) % 100;
            else if (chart is KpiCardChart kpi && kpi.Value.HasValue) kpi.Value += 10;
            else if (chart is WaffleChart waffle && waffle.Value.HasValue) waffle.Value = (waffle.Value.Value + 5) % waffle.MaxValue;
        }
        switch (chart.ItemsSource)
        {
            case ScheduleTask[] rows: chart.ItemsSource = rows.Select(row => row with { Progress = (row.Progress + 10) % 100 }).ToArray(); break;
            case ScheduleSpan[] rows: chart.ItemsSource = rows.Select(row => row with { End = row.End + .2 }).ToArray(); break;
            case ScheduleEvent[] rows: chart.ItemsSource = rows.Select(row => row with { Value = row.Value + 1 }).ToArray(); break;
            case BulletPoint[] rows: chart.ItemsSource = rows.Select(row => row with { Value = (row.Value + 10) % 100 }).ToArray(); break;
            case KpiPoint[] rows: chart.ItemsSource = rows.Select(row => row with { Value = row.Value + 10 }).ToArray(); break;
            case CalendarPoint[] rows: chart.ItemsSource = rows.Select(row => row with { Value = row.Value + 1 }).ToArray(); break;
            case MatrixPoint[] rows: chart.ItemsSource = rows.Select(row => row with { Value = row.Value + 1 }).ToArray(); break;
            case WafflePoint[] rows: chart.ItemsSource = rows.Select(row => row with { Value = row.Value + 1 }).ToArray(); break;
            case WordPoint[] rows: chart.ItemsSource = rows.Select(row => row with { Weight = row.Weight + 1 }).ToArray(); break;
            case RankingPoint[] rows: chart.ItemsSource = rows.Select(row => row with { Value = row.Value + (Equals(row.Entity, "A") ? 12 : 1) }).ToArray(); break;
            case ParliamentParty[] rows: chart.ItemsSource = rows.Select(row => row with { Seats = row.Seats + 1 }).ToArray(); break;
            case ThemeRiverPoint[] rows: chart.ItemsSource = rows.Select(row => row with { Value = row.Value + 1 }).ToArray(); break;
            case OperationalTableRow[] rows: chart.ItemsSource = rows.Select(row => row with { Values = row.Values.ToDictionary(pair => pair.Key, pair => pair.Key == "sales" && pair.Value is int value ? (object?)(value + 10) : pair.Value) }).ToArray(); break;
            case HierarchyNode[]: ((HierarchyChartBase)chart).ToggleExpanded("A"); break;
        }
        if (chart is ShapeMap map)
        {
            foreach (var layer in map.Layers)
                if (layer.ItemsSource is C6Region[] rows) layer.ItemsSource = rows.Select(row => row with { Value = row.Value + 1, Category = row.Category == "Open" ? "Closed" : "Open" }).ToArray();
                else if (layer.ItemsSource is MapObservation[] points) layer.ItemsSource = points.Select(point => new MapObservation(point.Id, point.Position, point.Value + 1, point.Source, point.Label)).ToArray();
                else if (layer.ItemsSource is MapConnection[] links) layer.ItemsSource = links.Select(link => link with { Value = link.Value + 1 }).ToArray();
                else if (layer.ItemsSource is MapPie[] pies) layer.ItemsSource = pies.Select(pie => pie with { Slices = pie.Slices.Select(slice => slice with { Value = slice.Value + 1 }).ToArray() }).ToArray();
                else if (layer.ItemsSource is MapVector[] vectors) layer.ItemsSource = vectors.Reverse().ToArray();
        }
    }
}
