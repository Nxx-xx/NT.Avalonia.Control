using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private CartesianChart? _trackballChart;
    private Expander? _trackballExamples;
    private readonly ObservableCollection<TrackballBusinessRecord> _trackballPrimary =
    [new("track-a0", "09:00", 1, 20), new("track-a1", "09:40", 5, 60), new("track-a2", "10:20", 9, 35)];
    private readonly ObservableCollection<TrackballBusinessRecord> _trackballSecondary =
    [new("track-b0", "批次 A", 0, 80), new("track-b1", "批次 B", 4, 40), new("track-b2", "批次 C", 8, 70)];

    private Control CreateTrackballExamples()
    {
        var chart = _trackballChart = CreateAdvancedChart("TrackballChart");
        chart.Height = 520;
        ConfigureTrackballChart(chart, _trackballPrimary, _trackballSecondary);
        var template = CreateTrackballBusinessTemplate();
        chart.TrackballContentTemplate = template;
        chart.TooltipContentTemplate = template;
        var direction = new ComboBox { ItemsSource = new[] { "关闭追踪", "垂直追踪 · 同一 X", "水平追踪 · 同一 Y" }, SelectedIndex = 1, MinWidth = 185, Margin = new Thickness(0, 0, 8, 6) };
        direction.SelectionChanged += (_, _) => { chart.TrackballMode = direction.SelectedIndex switch { 1 => ChartTrackballMode.Vertical, 2 => ChartTrackballMode.Horizontal, _ => ChartTrackballMode.None }; chart.IsTooltipEnabled = direction.SelectedIndex == 0; };
        var snap = new ComboBox { ItemsSource = new[] { "读取最近采样点", "线段插值估算" }, SelectedIndex = 1, MinWidth = 155, Margin = new Thickness(0, 0, 8, 6) };
        snap.SelectionChanged += (_, _) => chart.TrackballSnapMode = snap.SelectedIndex == 0 ? ChartTrackballSnapMode.NearestPoint : ChartTrackballSnapMode.LinearInterpolation;
        var display = new ComboBox { ItemsSource = new[] { "分组显示全部系列", "各系列独立浮动" }, SelectedIndex = 0, MinWidth = 165, Margin = new Thickness(0, 0, 8, 6) };
        display.SelectionChanged += (_, _) => chart.TrackballDisplayMode = display.SelectedIndex == 0 ? ChartTrackballDisplayMode.GroupAllPoints : ChartTrackballDisplayMode.FloatAllPoints;
        var tooltip = new ComboBox { ItemsSource = new[] { "普通提示：最近点", "普通提示：最近线段", "普通提示：按 X 插值", "普通提示：按 Y 插值", "普通提示：共享 X", "普通提示：共享 Y" }, SelectedIndex = 0, MinWidth = 190, Margin = new Thickness(0, 0, 8, 6) };
        tooltip.SelectionChanged += (_, _) => chart.TooltipMode = tooltip.SelectedIndex switch { 1 => ChartTooltipMode.NearestSegment, 2 => ChartTooltipMode.InterpolateX, 3 => ChartTooltipMode.InterpolateY, 4 => ChartTooltipMode.SharedX, 5 => ChartTooltipMode.SharedY, _ => ChartTooltipMode.NearestPoint };
        var rich = new CheckBox { Content = "业务追踪卡", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        rich.IsCheckedChanged += (_, _) => chart.TrackballContentTemplate = rich.IsChecked == true ? template : null;
        var actions = new WrapPanel { Children = { direction, snap, display, tooltip, rich } };
        AddAction(actions, "更新 09:40 记录", () => _trackballPrimary[1].Y = _trackballPrimary[1].Y == 60 ? 80 : 60, "更新右端点后，线段估算会随同一条业务记录变化。");
        AddAction(actions, "恢复全部范围", chart.ResetViewport, "恢复完整观察时段与响应范围。");
        return _trackballExamples = new Expander
        {
            Name = "ChartTrackballExamples", Header = "Trackball：线段插值、端点身份与共享追踪", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 10, Children =
            {
                new TextBlock { Text = "在两条折线之间移动鼠标，比较同一时间或同一响应值。估算值与两端真实记录分开展示；关闭追踪后可比较普通提示模式。", TextWrapping = TextWrapping.Wrap, Opacity = .75 },
                actions, CreateChartCard("业务采样与线段估算", "TRACKBALL", "例如 X = 3 时，响应线在 track-a0 与 track-a1 之间估算为 40；这不是新采样记录。", chart),
            } },
        };
    }

    private static void ConfigureTrackballChart(CartesianChart chart, ObservableCollection<TrackballBusinessRecord> primary, ObservableCollection<TrackballBusinessRecord> secondary)
    {
        chart.Title = "业务采样与线段估算";
        chart.XAxis = new NumericAxis { Title = "观察时间", Minimum = 0, Maximum = 10, Interval = 2, LabelFormat = "0.0" };
        chart.YAxis = new NumericAxis { Title = "响应值", Minimum = 0, Maximum = 100, Interval = 20, LabelFormat = "0.0" };
        chart.TrackballMode = ChartTrackballMode.Vertical;
        chart.TrackballDisplayMode = ChartTrackballDisplayMode.GroupAllPoints;
        chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        chart.IsTooltipEnabled = false;
        chart.Series.Add(new LineSeries { Title = "响应采样", ItemsSource = primary, CategoryPath = nameof(TrackballBusinessRecord.X), ValuePath = nameof(TrackballBusinessRecord.Y), IdPath = nameof(TrackballBusinessRecord.Id), Stroke = Blue, StrokeThickness = 3, MarkerSize = 8 });
        chart.Series.Add(new LineSeries { Title = "任务采样", ItemsSource = secondary, CategoryPath = nameof(TrackballBusinessRecord.X), ValuePath = nameof(TrackballBusinessRecord.Y), IdPath = nameof(TrackballBusinessRecord.Id), Stroke = Teal, StrokeThickness = 3, MarkerSize = 8 });
    }

    private static IDataTemplate CreateTrackballBusinessTemplate(IBrush? textInk = null) => new FuncDataTemplate<ChartTooltipContext>((_, _) =>
    {
        var readings = new ItemsControl { ItemTemplate = new FuncDataTemplate<ChartTrackballReading>((_, _) =>
        {
            var title = TrackballText("Original.Series.Title", "TrackballSeriesTitle", textInk); title.FontWeight = FontWeight.SemiBold;
            var estimated = new TextBlock { Text = "插值估算 · 非原始采样", FontWeight = FontWeight.SemiBold };
            estimated.Bind(IsVisibleProperty, new Binding("IsInterpolated"));
            var original = TrackballText("Original.Item.Id", "TrackballOriginalId", textInk);
            original.Bind(IsVisibleProperty, new Binding("!IsInterpolated"));
            var coordinates = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children =
            { TrackballText("Point.X", "TrackballValueX", textInk, "X {0:0.00}"), TrackballText("Point.Y", "TrackballValueY", textInk, "Y {0:0.00}") } };
            var endpoints = new StackPanel { Spacing = 2, Children =
            {
                new TextBlock { Text = "左 / 起点真实记录", FontSize = 11, Opacity = .75 }, TrackballText("Segment.Start.Item.Id", "TrackballLeftId", textInk),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { TrackballText("Segment.Start.Point.X", "TrackballLeftX", textInk, "X {0:0.00}"), TrackballText("Segment.Start.Point.Y", "TrackballLeftY", textInk, "Y {0:0.00}") } },
                new TextBlock { Text = "右 / 终点真实记录", FontSize = 11, Opacity = .75 }, TrackballText("Segment.End.Item.Id", "TrackballRightId", textInk),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { TrackballText("Segment.End.Point.X", "TrackballRightX", textInk, "X {0:0.00}"), TrackballText("Segment.End.Point.Y", "TrackballRightY", textInk, "Y {0:0.00}") } },
            } };
            endpoints.Bind(IsVisibleProperty, new Binding("IsInterpolated"));
            return new Border { Name = "TrackballReading", Padding = new Thickness(4), Child = new StackPanel { Spacing = 3, Children = { title, estimated, original, coordinates, endpoints } } };
        }) };
        readings.Bind(ItemsControl.ItemsSourceProperty, new Binding("Readings"));
        var fallback = TrackballText("Text", "TrackballFallbackText", textInk); fallback.TextWrapping = TextWrapping.Wrap;
        fallback.Bind(IsVisibleProperty, new Binding("Readings.Count") { Converter = new FuncValueConverter<int, bool>(count => count == 0) });
        return new Border { Name = "TrackballBusinessCard", Padding = new Thickness(5), MaxWidth = 330, Child = new StackPanel { Spacing = 5, Children =
        {
            new Border { Name = "TrackballBadge", Background = Brush("#D7259F"), CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 3), HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock { Text = "追踪读数与端点", Foreground = Brushes.White, FontWeight = FontWeight.SemiBold } }, readings, fallback,
        } } };
    });

    private static TextBlock TrackballText(string path, string name, IBrush? ink, string? format = null)
    {
        var text = new TextBlock { Name = name, TextWrapping = TextWrapping.Wrap, MaxWidth = 310 };
        if (ink is not null) text.Foreground = ink;
        text.Bind(TextBlock.TextProperty, new Binding(path) { StringFormat = format });
        return text;
    }

    private sealed class TrackballBusinessRecord(string id, string caption, double x, double y) : INotifyPropertyChanged
    {
        private double _y = y;
        public string Id { get; } = id;
        public string Caption { get; } = caption;
        public double X { get; } = x;
        public double Y { get => _y; set { if (_y == value) return; _y = value; PropertyChanged?.Invoke(this, new(nameof(Y))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
