using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<RelationsChartBase> _relationsCharts = [];
    private Control CreateRelationsExamples() => new Expander
    {
        Name = "ChartRelationsExamples", Header = "流向和关系：Sankey、Alluvial、网络、力导向、弧、弦与流程组合",
        HorizontalAlignment = HorizontalAlignment.Stretch, Content = CreateRelationsBoard(),
    };
    private static RelationsChartBase RelationChart(string kind) => kind switch
    {
        "sankey" => new SankeyChart(), "alluvial" => new SankeyChart { Mode = RelationSankeyMode.Alluvial },
        "network" => new NetworkChart { Mode = RelationNetworkMode.Circular },
        "force" => new NetworkChart { Mode = RelationNetworkMode.ForceDirected, Seed = 17, Iterations = 100 },
        "arc" => new NetworkChart { Mode = RelationNetworkMode.Arc }, "chord" => new ChordDiagramChart(),
        "flow" => new FlowChart { Mode = RelationFlowMode.Explicit }, "process" => new FlowChart { Mode = RelationFlowMode.Process },
        _ => new FlowChart { Mode = RelationFlowMode.Mindmap },
    };
    private static void ConfigureRelationPaths(RelationsChartBase chart)
    {
        chart.IdPath = "Id"; chart.LabelPath = "Label"; chart.GroupPath = "Group"; chart.ShapePath = "Shape";
        chart.XPath = chart is FlowChart { Mode: RelationFlowMode.Explicit } ? "X" : null;
        chart.YPath = chart.XPath is null ? null : "Y";
        chart.StagePath = "Stage"; chart.WidthPath = "Width"; chart.HeightPath = "Height";
        chart.NodeWeightPath = "Weight";
        chart.SourcePath = "SourceId"; chart.TargetPath = "TargetId"; chart.WeightPath = "Weight"; chart.EdgeIdPath = "Id"; chart.EdgeLabelPath = "Label";
        chart.Palette = HierarchyPalette();
    }
    private Control CreateRelationsBoard(bool overview = false)
    {
        var (nodes, edges) = RelationDemoData();
        var kinds = new[] { "sankey", "alluvial", "network", "force", "arc", "chord", "flow", "process", "mindmap" };
        var titles = new[] { "Sankey · 循环与流量", "Alluvial · 阶段迁移", "Network · 圆周关系", "Force · 碰撞与确定性种子", "Arc · 平行边与自环", "Chord · 加权关系带", "Flow · 显式节点与分组", "Process · 有向层级", "Mindmap · 发散布局" };
        var charts = kinds.Select(RelationChart).ToArray();
        for (var i = 0; i < charts.Length; i++)
        {
            var chart = charts[i]; ConfigureRelationPaths(chart); chart.ItemsSource = nodes; chart.EdgesSource = edges;
            chart.Title = titles[i]; chart.Height = overview ? 275 : 300;
        }
        if (!overview) _relationsCharts.AddRange(charts);
        var selected = new TextBlock { Text = "选择节点或关系带，查看原始节点 / 边身份；平行边分别保留。", TextWrapping = TextWrapping.Wrap };
        var status = new TextBlock { Text = "后台重排可以取消。循环通过返回路径表示；流量不平衡记录单独保留。", TextWrapping = TextWrapping.Wrap, Opacity = .8 };
        foreach (var chart in charts) chart.PropertyChanged += (_, change) =>
        {
            if (change.Property == EngineeringChartBase.SelectedPointProperty && chart.SelectedPoint is { } hit)
            {
                var item = hit.Members[0].Item;
                selected.Text = item switch
                {
                    RelationDemoNode node => $"节点 {node.Id} · {node.Label} · 原始序号 {hit.Members[0].SourceIndex}",
                    RelationDemoEdge edge => $"边 {edge.Id} · {edge.SourceId} → {edge.TargetId} · 权重 {edge.Weight:0.#} · 原始序号 {hit.Members[0].SourceIndex}",
                    _ => "原始数据：" + hit.Mark.TooltipText,
                };
            }
        };
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        AddAction(actions, "修改流量", () => { edges[0].Weight = edges[0].Weight >= 10 ? 6 : edges[0].Weight + 2; }, "只修改原始边权重，不合并平行边。");
        AddAction(actions, "追加节点", () =>
        {
            var id = "N" + nodes.Count; nodes.Add(new(id, "扩展", .8, .8, 4, "扩展", RelationNodeShape.RoundedRectangle));
            edges.Add(new("edge-" + edges.Count, "E", id, 2, "扩展"));
        }, "节点与边的可观察集合分别更新。");
        AddAction(actions, "切换显式位置", () => { nodes[1].Y = nodes[1].Y < .5 ? .65 : .25; }, "显式流程位置以归一化中心坐标给出。");
        AddAction(actions, "后台重排", async () =>
        {
            try { status.Text = "正在后台计算…"; var results = await Task.WhenAll(charts.Select(chart => chart.LayoutAsync())); status.Text = $"后台重排完成 {results.Count(value => value)} / {results.Length}"; }
            catch (Exception error) { status.Text = "后台重排：" + error.Message; }
        }, "不可变快照在后台计算，只有最新版本可发布。");
        AddAction(actions, "取消布局", () => { foreach (var chart in charts) chart.CancelLayout(); status.Text = "已请求取消当前布局。"; }, "取消后不发布旧候选。");
        AddAction(actions, "换力导向种子", () => { foreach (var chart in charts.OfType<NetworkChart>().Where(chart => chart.Mode == RelationNetworkMode.ForceDirected)) chart.Seed++; }, "相同种子和输入生成可重复布局。");
        AddAction(actions, "局部缩放", () => { foreach (var chart in charts) { chart.Zoom = 1.3; chart.PanX = .02; } }, "节点、边、箭头和命中使用同一视图。");
        AddAction(actions, "恢复视图", () => { foreach (var chart in charts) chart.ResetViewport(); }, "恢复缩放平移并清除选择。");
        var labels = new CheckBox { Content = "标签", IsChecked = true, Margin = new Thickness(8, 0) };
        labels.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.ShowLabels = labels.IsChecked == true; }; actions.Children.Add(labels);
        var edgeLabels = new CheckBox { Content = "关系标签", IsChecked = false, Margin = new Thickness(8, 0) };
        edgeLabels.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.ShowEdgeLabels = edgeLabels.IsChecked == true; }; actions.Children.Add(edgeLabels);
        var groups = new CheckBox { Content = "分组", IsChecked = true, Margin = new Thickness(8, 0) };
        groups.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.ShowGroups = groups.IsChecked == true; }; actions.Children.Add(groups);
        var grid = new Grid { ColumnDefinitions = new(overview ? "*,*,*" : "*,*"), RowDefinitions = new(overview ? "Auto,Auto,Auto" : "Auto,Auto,Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 12 };
        for (var i = 0; i < charts.Length; i++) { Grid.SetColumn(charts[i], i % (overview ? 3 : 2)); Grid.SetRow(charts[i], i / (overview ? 3 : 2)); grid.Children.Add(charts[i]); }
        return new StackPanel { Spacing = 10, Children =
        {
            new TextBlock { Text = "滚轮缩放 · 中键平移 · 方向键浏览，Space / Enter 选择。示例包含自环、平行边和循环；流程形状与分组保留原始业务节点，流量守恒、迭代收敛及碰撞结果可从布局读取。", TextWrapping = TextWrapping.Wrap },
            actions, selected, status, grid,
        } };
    }
    private static (ObservableCollection<RelationDemoNode> Nodes, ObservableCollection<RelationDemoEdge> Edges) RelationDemoData() =>
    ([new("A", "输入", .1, .5, 0, "入口", RelationNodeShape.Ellipse), new("B", "审核", .35, .25, 1, "处理", RelationNodeShape.Diamond),
      new("C", "处理", .35, .75, 1, "处理"), new("D", "交付", .65, .5, 2, "输出", RelationNodeShape.RoundedRectangle), new("E", "归档", .88, .5, 3, "输出", RelationNodeShape.Hexagon)],
     [new("ab", "A", "B", 6), new("ab-2", "A", "B", 2), new("ac", "A", "C", 4), new("bc", "B", "C", 2), new("bd", "B", "D", 6),
      new("cd", "C", "D", 4), new("dc", "D", "C", 1, "返回"), new("cc", "C", "C", 1, "自环"), new("de", "D", "E", 1)]);
    private sealed class RelationDemoNode(string id, string label, double x, double y, int stage = 0, string? group = null, RelationNodeShape shape = RelationNodeShape.Rectangle, double width = .16, double height = .12) : INotifyPropertyChanged
    {
        private double _x = x, _y = y;
        public string Id { get; } = id;
        public string Label { get; } = label;
        public string? Group { get; } = group;
        public RelationNodeShape Shape { get; } = shape;
        public int Stage { get; } = stage;
        public double Width { get; } = width;
        public double Height { get; } = height;
        public double Weight => 4;
        public double X { get => _x; set { if (_x == value) return; _x = value; PropertyChanged?.Invoke(this, new(nameof(X))); } }
        public double Y { get => _y; set { if (_y == value) return; _y = value; PropertyChanged?.Invoke(this, new(nameof(Y))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class RelationDemoEdge(string id, string sourceId, string targetId, double weight, string? label = null) : INotifyPropertyChanged
    {
        private double _weight = weight;
        public string Id { get; } = id;
        public string SourceId { get; } = sourceId;
        public string TargetId { get; } = targetId;
        public string? Label { get; } = label;
        public double Weight { get => _weight; set { if (_weight == value) return; _weight = value; PropertyChanged?.Invoke(this, new(nameof(Weight))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
