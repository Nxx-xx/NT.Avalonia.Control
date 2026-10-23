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
    private Expander? _gaugeMicroExamples;
    private GaugeMicroBoard? _gaugeMicroBoard;

    private Control CreateGaugeMicroExamples()
    {
        _gaugeMicroBoard = CreateGaugeMicroBoard();
        return _gaugeMicroExamples = new Expander { Name = "ChartGaugeMicroExamples", Header = "仪表与微图：数值、目标、区间和紧凑趋势", HorizontalAlignment = HorizontalAlignment.Stretch, Content = _gaugeMicroBoard.View };
    }

    private GaugeMicroBoard CreateGaugeMicroBoard()
    {
        var ranges = new ObservableCollection<GaugeRange> { new() { Start = 0, End = 40, Brush = Teal }, new() { Start = 40, End = 75, Brush = Orange }, new() { Start = 75, End = 100, Brush = Brush("#D7256A") } };
        var rings = new ObservableCollection<GaugeValue> { new() { Value = 72, Label = "本期", Brush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = new GradientStops { new(Colors.CornflowerBlue, 0), new(Colors.MediumPurple, 1) } } }, new() { Value = 48, Label = "上期", Brush = Teal }, new() { Value = 90, Label = "目标", Brush = Orange } };
        var circular = new CircularGaugeChart { Name = "CircularGaugeExample", Title = "圆形仪表 · 270° 刻度", Value = 68, TargetValue = 80, RangesSource = ranges };
        var semicircle = new GaugeChart { Name = "SemiGaugeExample", Title = "半圆仪表 · 区间与指针", Value = 68, TargetValue = 80, RangesSource = ranges };
        var gradient = new GradientRingChart { Name = "GradientRingExample", Title = "多环仪表 · 独立数值", ValuesSource = rings };
        var linear = new LinearGaugeChart { Name = "LinearGaugeExample", Title = "线性仪表 · 水平 / 垂直", Value = 68, TargetValue = 80, RangesSource = ranges };
        var liquid = new LiquidFillGaugeChart { Name = "LiquidGaugeExample", Title = "液体仪表 · 静态波形", Value = 68, TargetValue = 80, WavePhase = .25, IsWaveAnimationEnabled = false };
        var progress = new ProgressDonutChart { Name = "ProgressDonutExample", Title = "进度环 · 目标与边界", Value = 68, TargetValue = 80 };
        GaugeBase[] gauges = [circular, semicircle, gradient, linear, liquid, progress];
        foreach (var gauge in gauges) { gauge.Height = 250; gauge.Padding = new Thickness(12); gauge.ValueBrush = Blue; gauge.TrackBrush = Brush("#70809040"); }
        var rows = new ObservableCollection<MicroDemoRow> { new("micro-a", 25), new("micro-b", 55), new("micro-c", -15), new("micro-d", 75), new("micro-e", 0), new("micro-f", 45), new("micro-g", 30) };
        var sparklines = Enum.GetValues<SparklineType>().Select(type => new SparklineChart { Name = "Sparkline" + type + "Example", SparklineType = type, Height = 150, Padding = new Thickness(12), ItemsSource = rows, ValuePath = nameof(MicroDemoRow.Value), IdPath = nameof(MicroDemoRow.Id), LineBrush = Blue, AreaFill = Brush("#554385F4"), BarBrush = Blue, WinBrush = Teal, LossBrush = Brush("#D7256A"), ShowMarkers = true, ShowFirstLast = true, ShowMinMax = true, ShowBaseline = true }).ToArray();
        var value = new Slider { Name = "GaugeMicroValue", Minimum = -20, Maximum = 120, Value = 68, Width = 180, Margin = new Thickness(0, 0, 12, 6) };
        value.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) foreach (var gauge in gauges) gauge.Value = value.Value; };
        var target = new CheckBox { Name = "GaugeMicroTarget", Content = "目标值 80", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        target.IsCheckedChanged += (_, _) => { foreach (var gauge in gauges) gauge.TargetValue = target.IsChecked == true ? 80 : null; };
        var orientation = new CheckBox { Name = "GaugeMicroVertical", Content = "垂直线性仪表", Margin = new Thickness(0, 0, 12, 6) };
        orientation.IsCheckedChanged += (_, _) => linear.Orientation = orientation.IsChecked == true ? Orientation.Vertical : Orientation.Horizontal;
        var wave = new CheckBox { Name = "GaugeMicroWave", Content = "液体波动", Margin = new Thickness(0, 0, 12, 6) };
        wave.IsCheckedChanged += (_, _) => { liquid.IsWaveAnimationEnabled = wave.IsChecked == true; liquid.Title = wave.IsChecked == true ? "液体仪表 · 动态波形" : "液体仪表 · 静态波形"; };
        var reduce = new CheckBox { Name = "GaugeMicroReduceMotion", Content = "减少动态效果", Margin = new Thickness(0, 0, 12, 6) };
        reduce.IsCheckedChanged += (_, _) => liquid.ReduceMotion = reduce.IsChecked == true;
        var paused = new CheckBox { Name = "GaugeMicroPaused", Content = "暂停波动", Margin = new Thickness(0, 0, 12, 6) };
        paused.IsCheckedChanged += (_, _) => liquid.IsPaused = paused.IsChecked == true;
        var rectangle = new CheckBox { Name = "GaugeMicroRectangle", Content = "矩形液体", Margin = new Thickness(0, 0, 12, 6) };
        rectangle.IsCheckedChanged += (_, _) => liquid.Shape = rectangle.IsChecked == true ? GaugeLiquidShape.Rectangle : GaugeLiquidShape.Circle;
        var options = new WrapPanel { Children = { new TextBlock { Text = "仪表值", VerticalAlignment = VerticalAlignment.Center }, value, target, orientation, wave, reduce, paused, rectangle } };
        var revision = 0;
        void UpdateData() { revision++; rows[1].Value = revision % 2 == 0 ? 55 : -35; rows.Add(new("micro-added-" + revision, revision % 2 == 0 ? 20 : 65)); if (rows.Count > 8) rows.RemoveAt(0); rings[0].Value = revision % 2 == 0 ? 72 : 88; ranges[0].End = revision % 2 == 0 ? 40 : 30; }
        AddAction(options, "更新业务数据", UpdateData, "INPC 更新微图、区间与多环，集合增删保留源槽位和业务 ID。");
        AddAction(options, "0–100 / 20–120", () => { foreach (var gauge in gauges) { gauge.Minimum = gauge.Minimum == 0 ? 20 : 0; gauge.Maximum = gauge.Minimum + 100; } }, "切换数值域，显示值限制到域端点，原始 Value 不改写。");
        var export = new Button { Content = "导出仪表 PNG", Margin = new Thickness(0, 0, 8, 6) };
        export.Click += async (_, _) => { var file = await _hostWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions { Title = "导出仪表", SuggestedFileName = "circular-gauge.png", DefaultExtension = "png" }); if (file is null) return; await using var stream = await file.OpenWriteAsync(); await circular.TryExportPngAsync(stream, new ChartExportOptions { PixelSize = new PixelSize(900, 600), Background = circular.Background }); };
        options.Children.Add(export);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 12 };
        Control[] cards = [.. gauges, .. sparklines.Select((chart, index) => new StackPanel { Spacing = 4, Children = { new TextBlock { Text = new[] { "折线微图 · 最小 / 最大与首 / 尾", "面积微图 · 基线与缺口", "柱形微图 · 正负值", "盈亏微图 · 符号与零值" }[index], FontWeight = FontWeight.SemiBold }, chart } })];
        for (var index = 0; index < cards.Length; index++) { grid.Children.Add(cards[index]); Grid.SetColumn(cards[index], index % 2); Grid.SetRow(cards[index], index / 2); }
        grid.SizeChanged += (_, _) =>
        {
            var columns = grid.Bounds.Width >= 700 ? 2 : 1;
            if (grid.ColumnDefinitions.Count == columns) return;
            grid.ColumnDefinitions = new ColumnDefinitions(columns == 2 ? "*,*" : "*");
            grid.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", (cards.Length + columns - 1) / columns)));
            for (var index = 0; index < cards.Length; index++) { Grid.SetColumn(cards[index], index % columns); Grid.SetRow(cards[index], index / columns); }
        };
        return new(new StackPanel { Spacing = 10, Children = { new TextBlock { Text = "六种仪表共享数值域与目标；圆形、半圆和线性仪表支持区间，多环使用独立业务项。四类微图保留源顺序、缺口与业务身份，适合表格和概览。液体默认静态，可显式启用波动、暂停或减少动态效果。", TextWrapping = TextWrapping.Wrap, Opacity = .8 }, options, grid } }, gauges, sparklines, rows, ranges, rings, value, target, orientation, wave, reduce, paused, rectangle, UpdateData);
    }

    private sealed class MicroDemoRow(string id, double value) : INotifyPropertyChanged
    {
        private double _value = value;
        public string Id { get; } = id;
        public double Value { get => _value; set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed record GaugeMicroBoard(StackPanel View, GaugeBase[] Gauges, SparklineChart[] Sparklines, ObservableCollection<MicroDemoRow> Rows, ObservableCollection<GaugeRange> Ranges, ObservableCollection<GaugeValue> Rings, Slider Value, CheckBox Target, CheckBox Vertical, CheckBox Wave, CheckBox Reduce, CheckBox Paused, CheckBox Rectangle, Action UpdateData) : IDisposable
    { public void Dispose() { foreach (var chart in Sparklines) chart.ItemsSource = null; foreach (var gauge in Gauges) gauge.RangesSource = null; ((GradientRingChart)Gauges[2]).ValuesSource = null; } }
}
