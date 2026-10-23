using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Control CreateAdvancedComparisonExamples() => new Expander
    {
        Name = "ChartAdvancedComparisonExamples", Header = "进阶比较：发散条、哑铃、Mekko、镜像、人口、龙卷风与 Venn",
        HorizontalAlignment = HorizontalAlignment.Stretch, Content = CreateAdvancedComparisonBoard(),
    };

    private Control CreateAdvancedComparisonBoard(bool overview = false)
    {
        var values = new ObservableCollection<DivergingValue>(new[] { new DivergingValue(-6, "产品 A"), new(3, "产品 B"), new(9, "产品 C"), new(-4, "产品 D") });
        var diverging = new DivergingBarChart { Title = "Diverging · 中心基线与正负差异", Height = 260, ItemsSource = values };
        var dumbbell = new DumbbellChart { Title = "Dumbbell · 计划与实际", Height = 260, ItemsSource = ComparisonDemoPairs(), MarkerSize = 12 };
        var mirror = new MirrorBarChart { Title = "Mirror · 两侧共用尺度", Height = 260, ItemsSource = ComparisonDemoPairs(), LeftTitle = "西部", RightTitle = "东部" };
        var population = new PopulationPyramidChart { Title = "Population · 按输入年龄段排列", Height = 260,
            ItemsSource = new PairedComparisonValue[] { new(9, 10, "0–19 岁"), new(16, 18, "20–39 岁"), new(14, 12, "40–59 岁"), new(6, 8, "60+ 岁") }, LeftTitle = "人口组 A", RightTitle = "人口组 B" };
        var tornado = new TornadoChart { Title = "Tornado · 按双侧总影响稳定排序", Height = 260, ItemsSource = ComparisonDemoPairs(), LeftTitle = "下行情景", RightTitle = "上行情景" };
        var mekko = new MekkoChart { Title = "Mekko · 市场宽度 × 内部份额", Height = 260, ItemsSource = ComparisonDemoColumns(), ColumnGap = 4 };
        var venn2 = new VennDiagramChart { Title = "Venn · 两集合解析交集", Height = 260, SetCount = 2,
            ItemsSource = new VennItem[] { new(1, 60, "A 总量"), new(2, 40, "B 总量"), new(3, 20, "A∩B 总量") } };
        var venn3 = new VennDiagramChart { Title = "Venn · 三集合圆形近似与误差", Height = 260, SetCount = 3, ItemsSource = ComparisonDemoVennThree() };
        ComparisonChartBase[] charts = [diverging, dumbbell, mekko, mirror, population, tornado, venn2, venn3];
        Avalonia.Media.IBrush[] palette = [Brush("#1598B6"), Brush("#D25573"), Brush("#55B47A"), Brush("#E4AB47"), Brush("#8C73C6"), Brush("#D77F43"), Brush("#477DB3")];
        foreach (var chart in charts) chart.Palette = palette;
        venn3.ShowValues = false;
        if (overview) foreach (var chart in charts) chart.Height = 250;
        var selected = new TextBlock { Text = "选择条形、端点、子段或独占交集，查看原始来源；中缝与连接线不作为数据项。", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        foreach (var chart in charts) chart.PropertyChanged += (_, change) =>
        {
            if (change.Property != EngineeringChartBase.SelectedPointProperty || chart.SelectedPoint is not { } hit) return;
            selected.Text = $"{chart.Title} · {hit.Mark.TooltipText} · 来源索引 [{string.Join(", ", hit.Members.Select(member => member.SourceIndex))}]";
        };
        var status = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        void UpdateStatus()
        {
            var layout = venn3.Layout;
            status.Text = layout is null ? "三集合布局等待有效数据。" : $"Venn 输入为包含交集的集合总量；显示区域是扣除更高阶交集后的独占量。三圆最大区域面积偏差 / 最大集合总量：{layout.MaximumRelativeAreaError:P1}；无可见内部的正区域数：{venn3.UnrepresentedRegionMasks.Count}。";
        }
        venn3.LayoutUpdated += (_, _) => UpdateStatus();
        var order = new ComboBox { ItemsSource = Enum.GetValues<ComparisonSort>(), SelectedItem = ComparisonSort.Source, Width = 160 };
        order.SelectionChanged += (_, _) =>
        {
            var sort = (ComparisonSort)(order.SelectedItem ?? ComparisonSort.Source);
            diverging.Sort = dumbbell.Sort = mirror.Sort = sort;
        };
        var gap = new ComboBox { ItemsSource = new[] { 0d, 12d, 30d }, SelectedIndex = 1, Width = 70 };
        gap.SelectionChanged += (_, _) => mirror.CenterGap = population.CenterGap = tornado.CenterGap = (double)(gap.SelectedItem ?? 12d);
        var columnGap = new ComboBox { ItemsSource = new[] { 0d, 4d, 8d }, SelectedIndex = 1, Width = 65 };
        columnGap.SelectionChanged += (_, _) => mekko.ColumnGap = (double)(columnGap.SelectedItem ?? 4d);
        var labels = new CheckBox { Content = "显示标签和值", IsChecked = true };
        labels.IsCheckedChanged += (_, _) => { foreach (var chart in charts) { chart.ShowLabels = labels.IsChecked == true; chart.ShowValues = labels.IsChecked == true && !ReferenceEquals(chart, venn3); } };
        var actions = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        foreach (var (label, picker) in new[] { ("排序", order), ("中心间距 DIP", gap), ("列间距 DIP", columnGap) })
        { actions.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }); actions.Children.Add(picker); }
        actions.Children.Add(labels);
        AddAction(actions, "追加正负记录", () => { var i = values.Count; values.Add(new((i % 2 == 0 ? 1 : -1) * (3 + i % 7), "新增 " + i)); }, "发散条保持中心基线和原始来源索引。");
        var revision = 0;
        AddAction(actions, "替换比较数据", () =>
        {
            revision++; var rows = ComparisonDemoPairs(revision); dumbbell.ItemsSource = mirror.ItemsSource = tornado.ItemsSource = rows; mekko.ItemsSource = ComparisonDemoColumns(revision);
        }, "替换成另一组双侧值和市场份额，观察统一尺度及稳定排序。");
        AddAction(actions, "局部视图", () => { foreach (var chart in charts) { chart.Zoom = 1.4; chart.PanX = .03; } }, "所有图形和命中使用同一缩放平移。");
        AddAction(actions, "恢复视图", () => { foreach (var chart in charts) chart.ResetViewport(); }, "恢复范围并清除当前选择。");
        var columns = overview ? 3 : 2;
        var grid = new Grid { ColumnDefinitions = new(overview ? "*,*,*" : "*,*"), RowDefinitions = new(overview ? "Auto,Auto,Auto" : "Auto,Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 12 };
        for (var i = 0; i < charts.Length; i++) { Grid.SetColumn(charts[i], i % columns); Grid.SetRow(charts[i], i / columns); grid.Children.Add(charts[i]); }
        var regionDetails = new StackPanel { Spacing = 5, Margin = new Avalonia.Thickness(12) };
        regionDetails.Children.Add(new TextBlock { Text = "三集合独占值 / 圆形表示值", FontWeight = Avalonia.Media.FontWeight.SemiBold });
        var legend = new List<TextBlock>();
        for (var mask = 1; mask <= 7; mask++)
        {
            var text = new TextBlock(); legend.Add(text);
            regionDetails.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children =
            { new Border { Width = 10, Height = 10, Background = palette[mask - 1], VerticalAlignment = VerticalAlignment.Center }, text } });
        }
        regionDetails.Children.Add(new TextBlock { Text = "颜色区分独占区域。目标值由包含排除法精确计算；右值表示圆形实际面积。选择图中区域可追溯汇总来源。", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        void UpdateRegionDetails()
        {
            if (venn3.Layout is not { } layout) return;
            foreach (var region in layout.Regions)
            {
                var name = string.Join("∩", Enumerable.Range(0, 3).Where(i => (region.Mask & (1 << i)) != 0).Select(i => ((char)('A' + i)).ToString()));
                legend[region.Mask - 1].Text = $"{name}: {region.Value:0.#} / {region.EstimatedValue:0.#}";
            }
        }
        venn3.LayoutUpdated += (_, _) => UpdateRegionDetails();
        if (overview) { Grid.SetColumn(regionDetails, 2); Grid.SetRow(regionDetails, 2); grid.Children.Add(regionDetails); }
        else grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (!overview) { Grid.SetRow(regionDetails, 4); Grid.SetColumnSpan(regionDetails, 2); grid.Children.Add(regionDetails); }
        return new StackPanel { Spacing = 10, Children =
        {
            new TextBlock { Text = "滚轮缩放 · 中键平移 · 方向键浏览，Enter 选择，Esc 恢复。Mekko 宽度与段高分别归一化；列间距为 0 时显示面积精确对应份额。Venn 的独占值由输入精确计算，三圆面积近似单独披露。", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            actions, selected, status, grid,
        } };
    }

    private static PairedComparisonValue[] ComparisonDemoPairs(int revision = 0) => new[]
    {
        new PairedComparisonValue(6 + revision % 3, 4, "需求"), new(3, 8 + revision % 4, "价格"), new(9, 5, "成本"), new(4, 4, "渠道"),
    };
    private static MekkoColumn[] ComparisonDemoColumns(int revision = 0) => new[]
    {
        new MekkoColumn(2 + revision % 2, new[] { new MekkoSegment(2, "零售"), new(3, "线上") }, "北区"),
        new MekkoColumn(3, new[] { new MekkoSegment(4, "零售"), new(1, "线上") }, "南区"),
        new MekkoColumn(5, new[] { new MekkoSegment(1, "零售"), new(1, "线上") }, "西区"),
    };
    private static VennItem[] ComparisonDemoVennThree() => new[] { new VennItem(1, 100), new(2, 100), new(4, 100), new(3, 30), new(5, 30), new(6, 30), new(7, 10) };
}
