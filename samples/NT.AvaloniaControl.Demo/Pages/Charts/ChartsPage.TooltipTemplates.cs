using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private CartesianChart? _tooltipReadingChart;
    private Expander? _tooltipReadingExamples;
    private IDataTemplate? _businessTooltipTemplate;
    private readonly ObservableCollection<TooltipBusinessRecord> _readingResponses =
    [new("response-a", "09:00 响应", 1, 20), new("response-b", "09:30 响应", 2, 37), new("response-c", "10:00 响应", 5, 62)];
    private readonly ObservableCollection<TooltipBusinessRecord> _readingVolumes =
    [new("volume-a", "批次 A", 3.5, 10), new("volume-b", "批次 B", 7, Math.Pow(10, 1.48)), new("volume-c", "批次 C", 9, 1000)];

    private Control CreateTooltipTemplateExamples()
    {
        var chart = CreateAdvancedChart("TooltipReadingChart");
        _tooltipReadingChart = chart;
        chart.Height = 420;
        chart.XAxis = new NumericAxis { Title = "观察时间 / 小时", Minimum = 0, Maximum = 10, LabelFormat = "0.0" };
        chart.YAxis = new NumericAxis { Title = "响应 / ms", Minimum = 0, Maximum = 100, LabelFormat = "0.0" };
        chart.Axes.Add(new LogarithmicAxis { Key = "reading-volume", Title = "任务量", Minimum = 1, Maximum = 10000, Position = ChartAxisPosition.Far, ShowGridLines = false, LabelFormat = "0.00" });
        chart.Series.Add(new LineSeries { Title = "响应时间", ItemsSource = _readingResponses, CategoryPath = nameof(TooltipBusinessRecord.X), ValuePath = nameof(TooltipBusinessRecord.Y), IdPath = nameof(TooltipBusinessRecord.Id), Stroke = Blue, MarkerSize = 9, StrokeThickness = 3 });
        chart.Series.Add(new ScatterSeries { Title = "任务量", ItemsSource = _readingVolumes, CategoryPath = nameof(TooltipBusinessRecord.X), ValuePath = nameof(TooltipBusinessRecord.Y), IdPath = nameof(TooltipBusinessRecord.Id), YAxisKey = "reading-volume", Fill = Teal, Stroke = Teal, MarkerSize = 10 });
        chart.TooltipMode = ChartTooltipMode.SharedY;
        chart.ShowCrosshair = true;
        chart.ShowCrosshairLabels = true;
        chart.CrosshairBrush = Brush("#C615DA");
        chart.TooltipContentTemplate = _businessTooltipTemplate = CreateBusinessTooltipTemplate();

        var actions = new WrapPanel();
        var rich = new CheckBox { Content = "业务提示卡", IsChecked = true, Margin = new Thickness(0, 0, 14, 6) };
        rich.IsCheckedChanged += (_, _) => chart.TooltipContentTemplate = rich.IsChecked == true ? _businessTooltipTemplate : null;
        var lines = new CheckBox { Content = "参考线", IsChecked = true, Margin = new Thickness(0, 0, 14, 6) };
        lines.IsCheckedChanged += (_, _) => chart.ShowCrosshair = lines.IsChecked == true;
        var labels = new CheckBox { Content = "轴上读数", IsChecked = true, Margin = new Thickness(0, 0, 14, 6) };
        labels.IsCheckedChanged += (_, _) => chart.ShowCrosshairLabels = labels.IsChecked == true;
        var mode = new ComboBox { ItemsSource = new[] { "最近点", "共享 X", "共享 Y" }, SelectedIndex = 2, MinWidth = 110, Margin = new Thickness(0, 0, 8, 6) };
        mode.SelectionChanged += (_, _) => chart.TooltipMode = mode.SelectedIndex switch { 1 => ChartTooltipMode.SharedX, 2 => ChartTooltipMode.SharedY, _ => ChartTooltipMode.NearestPoint };
        var direction = new ComboBox { ItemsSource = new[] { "横纵参考线", "仅水平参考线", "仅垂直参考线" }, SelectedIndex = 0, MinWidth = 140, Margin = new Thickness(0, 0, 8, 6) };
        direction.SelectionChanged += (_, _) => chart.CrosshairDirection = direction.SelectedIndex switch { 1 => ChartCrosshairDirection.Horizontal, 2 => ChartCrosshairDirection.Vertical, _ => ChartCrosshairDirection.Both };
        actions.Children.Add(rich); actions.Children.Add(lines); actions.Children.Add(labels); actions.Children.Add(mode); actions.Children.Add(direction);
        AddAction(actions, "更新响应记录", () => { _readingResponses[1].Y = _readingResponses[1].Y == 37 ? 46 : 37; chart.ResetViewport(); }, "修改 09:30 的响应记录，提示卡显示同一条业务数据的新数值。");
        AddAction(actions, "恢复全部范围", chart.ResetViewport, "恢复观察时间与响应范围。");
        var readings = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<ChartAxisReading>((_, _) =>
            {
                var title = new TextBlock(); title.Bind(TextBlock.TextProperty, new Binding("Axis.Title"));
                var value = new TextBlock { FontWeight = FontWeight.SemiBold }; value.Bind(TextBlock.TextProperty, new Binding("Text"));
                return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { title, value } };
            }),
        };
        readings.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(CartesianChart.AxisReadings)) { Source = chart });
        return _tooltipReadingExamples = new Expander
        {
            Name = "ChartTooltipTemplateExamples", Header = "业务提示卡：共享 Y、方向参考线与多轴读数", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 10, Children =
            {
                new TextBlock { Text = "悬停数据点查看业务记录。共享 Y 比较高度最接近的真实数据；左侧响应与右侧任务量按各自刻度读取。", TextWrapping = TextWrapping.Wrap, Opacity = .7 },
                actions, CreateChartCard("响应与任务量", "TOOLTIP TEMPLATE", "提示卡保留各条记录自己的时间、数值和业务 ID", chart), readings,
            } },
        };
    }

    private static IDataTemplate CreateBusinessTooltipTemplate() => new FuncDataTemplate<ChartTooltipContext>((_, _) =>
    {
        var heading = new TextBlock { Name = "BusinessTooltipHeading", TextWrapping = TextWrapping.Wrap, MaxWidth = 300 };
        heading.Bind(TextBlock.TextProperty, new Binding("Text"));
        var rows = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<ChartPointHit>((_, _) =>
            {
                var series = new TextBlock { FontWeight = FontWeight.SemiBold }; series.Bind(TextBlock.TextProperty, new Binding("Series.Title"));
                var caption = new TextBlock { Name = "BusinessTooltipCaption" }; caption.Bind(TextBlock.TextProperty, new Binding("Item.Caption"));
                var id = new TextBlock { Name = "BusinessTooltipId" }; id.Bind(TextBlock.TextProperty, new Binding("Item.Id"));
                var xy = new TextBlock { TextWrapping = TextWrapping.Wrap }; xy.Bind(TextBlock.TextProperty, new Binding("Point") { StringFormat = "原始坐标：{0}" });
                return new StackPanel { Spacing = 2, Margin = new Thickness(0, 4), Children = { series, caption, id, xy } };
            }),
        };
        rows.Bind(ItemsControl.ItemsSourceProperty, new Binding("Hits"));
        return new Border { Name = "BusinessTooltipCard", Padding = new Thickness(4), MaxWidth = 320, Child = new StackPanel { Spacing = 6, Children =
        {
            new Border { Name = "BusinessTooltipBadge", Background = Brush("#D7259F"), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = "业务数据详情", Foreground = Brushes.White, FontWeight = FontWeight.SemiBold } },
            heading, rows,
        } } };
    });

    private sealed class TooltipBusinessRecord(string id, string caption, double x, double y) : INotifyPropertyChanged
    {
        private double _y = y;
        public string Id { get; } = id;
        public string Caption { get; } = caption;
        public double X { get; } = x;
        public double Y { get => _y; set { if (_y == value) return; _y = value; PropertyChanged?.Invoke(this, new(nameof(Y))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
