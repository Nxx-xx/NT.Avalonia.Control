using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _selectionRangeExamples;
    private SelectionRangeBoard? _selectionRangeBoard;

    private Control CreateSelectionRangeExamples()
    {
        _selectionRangeBoard = CreateSelectionRangeBoard(true);
        return _selectionRangeExamples = new Expander
        {
            Name = "ChartSelectionRangeExamples", Header = "选择与范围：系列选择、双向范围条与多图联动",
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = _selectionRangeBoard.View,
        };
    }

    private SelectionRangeBoard CreateSelectionRangeBoard(bool register)
    {
        CartesianChart Chart(string name, double extent)
        {
            var chart = register ? CreateAdvancedChart(name) : new CartesianChart { Name = name };
            chart.Height = 265; chart.Title = extent == 100 ? "业务趋势 · X 0–100" : "负载分布 · X 0–1000";
            chart.XAxis = new NumericAxis { Minimum = 0, Maximum = extent, Title = "观察区间", LabelFormat = "0" };
            chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "数值", LabelFormat = "0" };
            chart.IsZoomEnabled = true; chart.IsPanEnabled = true; chart.InteractionAxes = ChartInteractionAxes.X;
            chart.IsKeyboardNavigationEnabled = true; chart.SeriesSelectionMode = ChartSelectionMode.Multiple;
            chart.SelectionMode = ChartSelectionMode.Multiple; chart.UnselectedSeriesOpacity = .18;
            chart.LegendPosition = ChartLegendPosition.Bottom; chart.Background = Brushes.Transparent;
            chart.Series.Add(new LineSeries { Title = "趋势", Stroke = Blue, StrokeThickness = 3, MarkerSize = 7,
                ItemsSource = new ChartPoint[] { new(extent * .1, 30), new(extent * .3, 65), new(extent * .5, 45), new(extent * .7, 80), new(extent * .9, 60) } });
            chart.Series.Add(new BarSeries { Title = "成交", Fill = Teal, Stroke = Teal,
                ItemsSource = new ChartPoint[] { new(extent * .2, 22), new(extent * .4, 38), new(extent * .6, 28), new(extent * .8, 42) } });
            chart.Series.Add(new ScatterSeries { Title = "事件", Fill = Orange, Stroke = Orange, MarkerSize = 12,
                ItemsSource = new ChartPoint[] { new(extent * .15, 85), new(extent * .45, 88), new(extent * .75, 92) } });
            return chart;
        }
        var first = Chart("SelectionRangePrimary", 100); var second = Chart("SelectionRangeSecondary", 1000);
        var group = new ChartSynchronizationGroup { Axes = ChartInteractionAxes.X };
        group.Add(first); group.Add(second);
        var horizontal = new ChartRangeSelector { Name = "SelectionRangeHorizontal", Chart = first, Axis = ChartInteractionAxes.X, Height = 42, MinimumSpan = .08, SmallChange = .02, LargeChange = .15, Margin = new Thickness(50, 0, 12, 0) };
        var vertical = new ChartRangeSelector { Name = "SelectionRangeVertical", Chart = first, Axis = ChartInteractionAxes.Y, Orientation = Orientation.Vertical, Width = 42, MinimumSpan = .08, SmallChange = .02, Margin = new Thickness(0, 25, 0, 45) };
        var status = new TextBlock { Name = "SelectionRangeStatus", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        void Update()
        {
            var selected = first.SelectedSeries is { Count: > 0 } list ? string.Join("、", list.Select(series => series.Title)) : "无";
            var a = first.VisibleRange; var b = second.VisibleRange;
            status.Text = $"已选系列：{selected}；范围条 {horizontal.Range.Lower:P0}–{horizontal.Range.Upper:P0}\n上图 X {a?.XMinimum:0.#}–{a?.XMaximum:0.#}；下图 X {b?.XMinimum:0.#}–{b?.XMaximum:0.#}；联动轴 {group.Axes}";
        }
        first.SeriesSelectionChanged += (_, _) => Update();
        first.ViewportChanged += (_, _) => Update(); second.ViewportChanged += (_, _) => Update();
        horizontal.RangeChanged += (_, _) => Update();
        var mode = new ComboBox { ItemsSource = new[] { "系列多选（Ctrl 切换）", "系列单选", "单选再点取消", "关闭系列选择" }, SelectedIndex = 0, MinWidth = 190, Margin = new Thickness(0, 0, 8, 5) };
        mode.SelectionChanged += (_, _) => first.SeriesSelectionMode = mode.SelectedIndex switch { 1 => ChartSelectionMode.Single, 2 => ChartSelectionMode.SingleDeselect, 3 => ChartSelectionMode.None, _ => ChartSelectionMode.Multiple };
        var axes = new ComboBox { ItemsSource = new[] { "联动 X", "联动 Y", "联动 X / Y", "暂停联动" }, SelectedIndex = 0, MinWidth = 130, Margin = new Thickness(0, 0, 8, 5) };
        axes.SelectionChanged += (_, _) => { group.Axes = axes.SelectedIndex switch { 1 => ChartInteractionAxes.Y, 2 => ChartInteractionAxes.Both, 3 => ChartInteractionAxes.None, _ => ChartInteractionAxes.X }; group.SynchronizeFrom(first); Update(); };
        var coordinates = new ComboBox { ItemsSource = new[] { "按范围比例联动", "按数据坐标联动" }, SelectedIndex = 0, MinWidth = 170, Margin = new Thickness(0, 0, 8, 5) };
        coordinates.SelectionChanged += (_, _) => { group.CoordinateMode = coordinates.SelectedIndex == 0 ? ChartSynchronizationCoordinateMode.Normalized : ChartSynchronizationCoordinateMode.DataCoordinates; group.SynchronizeFrom(first); Update(); };
        var cancel = new CheckBox { Content = "拦截系列选择", Margin = new Thickness(0, 0, 8, 5) };
        first.SeriesSelectionChanging += (_, args) => { if (cancel.IsChecked == true && args.CanCancel) args.Cancel = true; };
        var actions = new WrapPanel { Children = { mode, axes, coordinates, cancel } };
        AddAction(actions, "选中趋势与事件", () => { first.SelectSeries(first.Series[0]); first.SelectSeries(first.Series[2], true); Update(); }, "选择两个完整系列，成交柱会淡化。");
        AddAction(actions, "清除系列选择", () => first.ClearSeriesSelection(), "清除系列状态，保留独立的点选择。");
        AddAction(actions, "隐藏 / 恢复事件", () => { first.Series[2].IsVisible = !first.Series[2].IsVisible; Update(); }, "隐藏系列会清除该系列的选择，恢复时不自动重新选中。");
        AddAction(actions, "观察 20%–65%", () => { horizontal.Range = new ChartRange(.2, .65); Update(); }, "通过范围条控制上图，并按所选模式同步下图。");
        AddAction(actions, "恢复全部范围", () => { first.ResetViewport(); second.ResetViewport(); Update(); }, "重置两图范围，保留系列选择。");
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { first, vertical } }; Grid.SetColumn(vertical, 1);
        var view = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "点击折线、柱体或散点选择完整系列；Ctrl 切换多选，Shift 拖动仍可框选数据点。拖动范围条两端改变窗口，拖动中段平移；Tab 聚焦拖柄后用方向键、PageUp/Down、Home/End 操作。两图使用不同 X 范围，可比较比例联动与坐标联动。", TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            actions, top, horizontal, second, status,
        } };
        view.AttachedToVisualTree += (_, _) => Update();
        return new(view, first, second, horizontal, vertical, group, status);
    }

    private sealed record SelectionRangeBoard(StackPanel View, CartesianChart Primary, CartesianChart Secondary,
        ChartRangeSelector Horizontal, ChartRangeSelector Vertical, ChartSynchronizationGroup Group, TextBlock Status);
}
