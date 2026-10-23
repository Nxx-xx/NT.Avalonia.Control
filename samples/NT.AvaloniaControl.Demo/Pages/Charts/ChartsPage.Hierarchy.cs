using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Control CreateHierarchyExamples() => new Expander
    {
        Name = "ChartHierarchyExamples", Header = "层级和打包：Treemap、旭日、组织树、气泡云、打包气泡和圆打包",
        HorizontalAlignment = HorizontalAlignment.Stretch, Content = CreateHierarchyBoard(),
    };

    private Control CreateHierarchyBoard(bool overview = false)
    {
        var source = HierarchyDemoData();
        HierarchyChartBase[] charts =
        [
            new TreemapChart { Title = "Treemap · 叶节点面积合计" }, new SunburstChart { Title = "Sunburst · 层级扇环" },
            new OrganizationChart { Title = "Organization · 组织连线与折叠" }, new BubbleCloudChart { Title = "Bubble cloud · 无轴面积气泡" },
            new PackedBubbleChart { Title = "Packed bubble · 紧凑打包" }, new CirclePackingChart { Title = "Circle packing · 圆形层级嵌套" },
        ];
        foreach (var chart in charts)
        {
            chart.ItemsSource = source; chart.IdPath = "Id"; chart.LabelPath = "Label"; chart.ChildrenPath = "Children"; chart.ValuePath = "Value";
            chart.Height = overview ? 290 : 310; chart.AnimationTriggers = ChartAnimationTriggers.All;
            chart.AnimationDuration = TimeSpan.FromMilliseconds(650); chart.Palette = HierarchyPalette();
            if (chart is BubbleCloudChart or PackedBubbleChart) { chart.GroupPath = "Group"; chart.ShowLegend = true; }
        }
        object selectedId = "platform";
        var selected = new TextBlock { Text = "选择节点查看原始来源；默认操作“平台研发”分支。", TextWrapping = TextWrapping.Wrap };
        var path = new TextBlock { Text = "路径：全部部门", TextWrapping = TextWrapping.Wrap, Opacity = .8 };
        void UpdatePath() => path.Text = "路径：" + (charts[0].DrillPath.Count == 0 ? "全部部门" : string.Join(" / ", charts[0].DrillPath.Select(entry => entry.Label)));
        foreach (var chart in charts) chart.PropertyChanged += (_, change) =>
        {
            if (change.Property == EngineeringChartBase.SelectedPointProperty && chart.SelectedPoint is { } hit)
            {
                var member = hit.Members[0]; selectedId = hit.Mark.Key ?? member.Item!;
                selected.Text = $"{chart.GetType().Name} · 合计 {hit.Mark.Value:0.#} · 原始兄弟序号 {member.SourceIndex} · ID {hit.Mark.Key}";
            }
        };
        foreach (var chart in charts)
        {
            void ShowPath() => path.Text = chart.GetType().Name + " · 路径：" + (chart.DrillPath.Count == 0 ? "全部部门" : string.Join(" / ", chart.DrillPath.Select(entry => entry.Label)));
            chart.AddHandler(InputElement.PointerReleasedEvent, (_, _) => ShowPath(), handledEventsToo: true);
            chart.AddHandler(InputElement.KeyDownEvent, (_, _) => ShowPath(), handledEventsToo: true);
        }
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        AddAction(actions, "钻取选中分支", () => { foreach (var chart in charts) chart.DrillDown(selectedId); UpdatePath(); }, "双击或 Enter 也可钻取当前分支。");
        AddAction(actions, "返回上层", () => { foreach (var chart in charts) chart.DrillUp(); UpdatePath(); }, "Backspace 返回父节点。");
        AddAction(actions, "恢复全部", () => { foreach (var chart in charts) { chart.ResetDrill(); chart.ResetViewport(); } UpdatePath(); }, "恢复整个森林与初始视图。");
        AddAction(actions, "展开 / 折叠", () => { foreach (var chart in charts) chart.ToggleExpanded(selectedId); }, "+ / - 操作当前分支。");
        var revision = 0;
        AddAction(actions, "更新叶节点", () => { revision++; source[0].Children[0].Children[0].Value = 18 + revision % 4 * 5; }, "修改可观察数据，显示保留身份的几何过渡。");
        AddAction(actions, "追加团队", () => { var children = source[1].Children; children.Add(new("team-" + children.Count, "新团队 " + children.Count, 5)); }, "追加节点并重新布局。");
        AddAction(actions, "暂停 / 继续", () => { var pause = !charts[0].IsAnimationPaused; foreach (var chart in charts) chart.IsAnimationPaused = pause; }, "暂停当前帧与延迟计时。");
        AddAction(actions, "取消动画", () => { foreach (var chart in charts) chart.CancelAnimations(); }, "直接显示最新目标。");
        AddAction(actions, "重播入场", () => { foreach (var chart in charts) chart.ReplayEntranceAnimation(); }, "再次展示入场动画。");
        var reduce = new CheckBox { Content = "减少动态效果", Margin = new Thickness(8, 0) };
        reduce.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.ReduceMotion = reduce.IsChecked == true; };
        actions.Children.Add(reduce);
        var labels = new CheckBox { Content = "节点标签", IsChecked = true, Margin = new Thickness(8, 0) };
        labels.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.ShowLabels = labels.IsChecked == true; };
        actions.Children.Add(labels);
        var duration = new ComboBox { ItemsSource = new[] { "250 ms", "650 ms", "1200 ms" }, SelectedIndex = 1, Margin = new Thickness(8, 0), MinWidth = 100 };
        duration.SelectionChanged += (_, _) => { foreach (var chart in charts) chart.AnimationDuration = TimeSpan.FromMilliseconds(duration.SelectedIndex switch { 0 => 250, 2 => 1200, _ => 650 }); };
        actions.Children.Add(duration);
        var delay = new CheckBox { Content = "延迟 250 ms", Margin = new Thickness(8, 0) };
        delay.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.AnimationDelay = TimeSpan.FromMilliseconds(delay.IsChecked == true ? 250 : 0); };
        actions.Children.Add(delay);
        var grid = new Grid { ColumnDefinitions = new(overview ? "*,*,*" : "*,*"), RowDefinitions = new(overview ? "Auto,Auto" : "Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 12 };
        var columns = overview ? 3 : 2;
        for (var i = 0; i < charts.Length; i++) { Grid.SetColumn(charts[i], i % columns); Grid.SetRow(charts[i], i / columns); grid.Children.Add(charts[i]); }
        return new StackPanel { Spacing = 10, Children =
        {
            new TextBlock { Text = "分支由叶节点汇总。矩形面积、扇区角度和同层叶圆面积表示权重；父圆留有打包空隙。双击 / Enter 钻取，Backspace 返回，+ / - 展开折叠，滚轮缩放，中键平移。", TextWrapping = TextWrapping.Wrap },
            actions, selected, path, grid,
        } };
    }

    private static IBrush[] HierarchyPalette() => [Brush("#1598B6"), Brush("#D25573"), Brush("#55B47A"), Brush("#E4AB47"), Brush("#8C73C6"), Brush("#D77F43"), Brush("#477DB3")];
    private static ObservableCollection<HierarchyDemoRecord> HierarchyDemoData() =>
    [
        new("engineering", "研发", 0,
        [
            new("platform", "平台", 0, [new("api", "接口", 18), new("data", "数据", 12)]),
            new("apps", "产品", 0, [new("desktop", "桌面", 14), new("mobile", "移动", 10)]),
        ]),
        new("operations", "运营", 0, [new("support", "支持", 10), new("delivery", "交付", 8), new("community", "社区", 5)]),
        new("research", "研究", 7),
    ];

    private sealed class HierarchyDemoRecord(string id, string label, double value, IEnumerable<HierarchyDemoRecord>? children = null) : INotifyPropertyChanged
    {
        private double _value = value;
        public string Id { get; } = id;
        public string Label { get; } = label;
        public string Group => Id is "api" or "data" or "platform" or "apps" or "desktop" or "mobile" or "engineering" ? "研发" : Id == "research" ? "研究" : "运营";
        public double Value { get => _value; set { if (_value.Equals(value)) return; _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public ObservableCollection<HierarchyDemoRecord> Children { get; } = new(children ?? []);
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
