using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Control CreateEngineeringExamples() => new Expander
    {
        Name = "ChartEngineeringExamples", Header = "工程图：参数网格、六边形分箱、Smith、三元与风玫瑰",
        HorizontalAlignment = HorizontalAlignment.Stretch, Content = CreateEngineeringBoard(),
    };

    private Control CreateEngineeringBoard()
    {
        var carpet = new CarpetPlot { Title = "Carpet · A / B 参数网格", Height = 285, ItemsSource = EngineeringCarpetRows() };
        var cloud = new ObservableCollection<HexbinPoint>(Enumerable.Range(0, 240).Select(i => new HexbinPoint(Math.Sin(i * .7) * (1 + i % 5), Math.Cos(i * .43) * (1 + i % 7))));
        var hex = new HexbinChart { Title = "Hexbin · 原始成员密度", Height = 285, ItemsSource = cloud };
        SmithPoint[] impedance = [new(25, 25), new(50, 50), new(100, -50), new(50, 0), new(10, -20)];
        var smith = new SmithChart { Title = "Smith · 阻抗 Ω", Height = 285, ItemsSource = impedance };
        var admittance = new SmithChart { Title = "Smith · 导纳 S（同一负载）", Height = 285, Quantity = SmithQuantity.Admittance,
            ItemsSource = impedance.Select(p => new SmithPoint(p.R / (p.R * p.R + p.X * p.X), -p.X / (p.R * p.R + p.X * p.X))).ToArray() };
        var ternary = new TernaryChart { Title = "Ternary · 配比归一化", Height = 285, ALabel = "组分 A", BLabel = "组分 B", CLabel = "组分 C",
            ItemsSource = new TernaryPoint[] { new(1, 1, 2), new(2, 1, 1), new(1, 2, 1), new(5, 2, 1), new(1, 1, 1) } };
        var wind = new WindRoseChart { Title = "Wind Rose · 方向 / 速度 / 频数", Height = 285, ItemsSource = EngineeringWindRows(), SpeedUpperBounds = new double[] { 3, 6 }, SectorCount = 8 };
        EngineeringChartBase[] charts = [carpet, hex, smith, admittance, ternary, wind];
        var detail = new TextBlock { Text = "选择标记查看原始数据；聚合标记显示所有来源索引。", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        foreach (var chart in charts) chart.PropertyChanged += (_, change) =>
        {
            if (change.Property != EngineeringChartBase.SelectedPointProperty) return;
            var hit = chart.SelectedPoint;
            if (hit is null) return;
            detail.Text = $"{chart.Title} · {hit.Mark.TooltipText} · 原始索引 [{string.Join(", ", hit.Members.Select(p => p.SourceIndex))}]";
        };
        var radius = new ComboBox { ItemsSource = new[] { 12d, 18d, 30d }, SelectedIndex = 1, Width = 70 };
        radius.SelectionChanged += (_, _) => hex.BinRadius = (double)(radius.SelectedItem ?? 18d);
        var offset = new ComboBox { ItemsSource = new[] { .2, .5, .8 }, SelectedIndex = 1, Width = 70 };
        offset.SelectionChanged += (_, _) => carpet.Offset = (double)(offset.SelectedItem ?? .5);
        var reference = new ComboBox { ItemsSource = new[] { 25d, 50d, 75d }, SelectedIndex = 1, Width = 70 };
        reference.SelectionChanged += (_, _) => smith.ReferenceImpedance = admittance.ReferenceImpedance = (double)(reference.SelectedItem ?? 50d);
        var sectors = new ComboBox { ItemsSource = new[] { 4, 8, 16 }, SelectedIndex = 1, Width = 70 };
        sectors.SelectionChanged += (_, _) => wind.SectorCount = (int)(sectors.SelectedItem ?? 8);
        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        foreach (var (label, picker) in new[] { ("分箱半径", radius), ("Carpet 偏移", offset), ("参考阻抗 Ω", reference), ("方向数", sectors) })
        { actions.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }); actions.Children.Add(picker); }
        AddAction(actions, "追加密度观测", () => { var i = cloud.Count; cloud.Add(new(Math.Sin(i * .7), Math.Cos(i * .43))); }, "向可观察数据源追加原始观测，重新聚合并保留成员身份。");
        AddAction(actions, "局部视图", () => { foreach (var chart in charts) { chart.Zoom = 1.5; chart.PanX = .06; } }, "同时缩放平移六张工程图。");
        AddAction(actions, "恢复视图", () => { foreach (var chart in charts) chart.ResetViewport(); }, "恢复完整投影范围并清除选择。");
        var grid = new Grid { ColumnDefinitions = new("*,*"), RowDefinitions = new("Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 12 };
        for (var i = 0; i < charts.Length; i++) { Grid.SetColumn(charts[i], i % 2); Grid.SetRow(charts[i], i / 2); grid.Children.Add(charts[i]); }
        return new StackPanel { Spacing = 10, Children =
        {
            new TextBlock { Text = "滚轮缩放 · 鼠标中键平移 · 方向键浏览，Enter 选择，Esc 恢复。Smith 使用物理 Ω / S；三元逆解返回比例；风玫瑰北向起、顺时针，颜色表示速度分档。", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            actions, detail, grid,
        } };
    }

    private static CarpetPoint[] EngineeringCarpetRows() => (from b in new[] { 0d, 1d, 2d } from a in new[] { 0d, 1d, 2d } select new CarpetPoint(a, b, 10 + a + 2 * b + .5 * a * b)).ToArray();
    private static WindObservation[] EngineeringWindRows() => Enumerable.Range(0, 8).SelectMany(i => new[] { new WindObservation(i * 45, 1, 2 + i % 3), new WindObservation(i * 45, 4, 1 + i % 2), new WindObservation(i * 45, 7, .5) }).ToArray();
}
