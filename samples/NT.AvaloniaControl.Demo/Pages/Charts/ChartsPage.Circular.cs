using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _circularExamples;
    private CircularBoard? _circularBoard;

    private Control CreateCircularExamples()
    {
        _circularBoard = CreateCircularBoard();
        return _circularExamples = new Expander { Name = "ChartCircularExamples", Header = "圆形图：饼、环、半环与小扇区", HorizontalAlignment = HorizontalAlignment.Stretch, Content = _circularBoard.View };
    }

    private CircularBoard CreateCircularBoard()
    {
        var records = CircularRecords();
        CircularChart Chart(string name, string title) => new()
        {
            Name = name, Title = title, Height = 290, ShowLegend = true, ShowDataLabels = true,
            LabelPosition = CircularLabelPosition.Outside, SelectionMode = ChartSelectionMode.Multiple,
            AnimationTriggers = ChartAnimationTriggers.All, AnimationDuration = TimeSpan.FromMilliseconds(900), AnimationEasing = new CubicEaseOut(),
        };
        T Source<T>(T series) where T : CircularSeries
        {
            series.ItemsSource = records; series.ValuePath = nameof(CircularRecord.Value); series.CategoryPath = nameof(CircularRecord.Label);
            series.IdPath = nameof(CircularRecord.Id); series.ColorPath = nameof(CircularRecord.Color); return series;
        }
        var pie = Chart("CircularPieChart", "饼图 · 原始业务份额"); var pieSeries = Source(new PieSeries()); pie.Series.Add(pieSeries);
        var donut = Chart("CircularDonutChart", "环图 · 中心文字与选择"); var donutSeries = Source(new DonutSeries()); donut.Series.Add(donutSeries); donut.CenterText = "渠道\n100";
        var semi = Chart("CircularSemiDonutChart", "半环 · 180°"); semi.Series.Add(Source(new SemiDonutSeries()));
        var arc = Chart("CircularArcChart", "部分环 · 270° / 逆时针"); arc.Series.Add(Source(new DonutSeries { StartAngle = 45, SweepAngle = 270, Clockwise = false, InnerRadius = .55 }));
        var nested = Chart("CircularNestedChart", "多环 · 独立业务系列");
        nested.Series.Add(Source(new DonutSeries { Title = "外环", Radius = 1, InnerRadius = .7 }));
        nested.Series.Add(Source(new DonutSeries { Title = "内环", Radius = .6, InnerRadius = .45 }));
        nested.ShowDataLabels = false; nested.CenterText = "多环";
        var small = Chart("CircularSmallSectorsChart", "小扇区 · 外标签避让与连线");
        var smallSeries = new PieSeries { ItemsSource = new[] { new CircularRecord("large", "主要渠道", 94, "#4385F4"), new("tiny-a", "小项 A", 1, "#19A798"), new("tiny-b", "小项 B", 1, "#F2A33A"), new("tiny-c", "小项 C", 1, "#D7256A"), new("tiny-d", "小项 D", 1, "#9C247A"), new("tiny-e", "小项 E", 2, "#689C29") }, ValuePath = nameof(CircularRecord.Value), CategoryPath = nameof(CircularRecord.Label), IdPath = nameof(CircularRecord.Id), ColorPath = nameof(CircularRecord.Color), MinimumSliceAngle = 5 };
        small.Series.Add(smallSeries);
        var charts = new[] { pie, donut, semi, arc, nested, small };
        var policy = new ComboBox { Name = "CircularNegativePolicy", ItemsSource = new[] { ChartCircularNegativeValueMode.Ignore, ChartCircularNegativeValueMode.Absolute }, SelectedItem = ChartCircularNegativeValueMode.Ignore, MinWidth = 120, Margin = new Thickness(0, 0, 12, 6) };
        policy.SelectionChanged += (_, _) => { if (policy.SelectedItem is ChartCircularNegativeValueMode value) { foreach (var chart in charts) foreach (var series in chart.Series) series.NegativeValueMode = value; UpdateCenterText(); } };
        var labels = new ComboBox { Name = "CircularLabelPosition", ItemsSource = Enum.GetValues<CircularLabelPosition>(), SelectedItem = CircularLabelPosition.Outside, MinWidth = 110, Margin = new Thickness(0, 0, 12, 6) };
        labels.SelectionChanged += (_, _) => { if (labels.SelectedItem is CircularLabelPosition value) foreach (var chart in charts) chart.LabelPosition = value; };
        var tiny = new CheckBox { Name = "CircularHideTiny", Content = "隐藏小于 5° 的扇区", Margin = new Thickness(0, 0, 12, 6) };
        tiny.IsCheckedChanged += (_, _) => smallSeries.SmallSliceMode = tiny.IsChecked == true ? ChartCircularSmallSliceMode.Hide : ChartCircularSmallSliceMode.Keep;
        var reduced = new CheckBox { Name = "CircularReducedMotion", Content = "减少动态效果", Margin = new Thickness(0, 0, 12, 6) };
        reduced.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.ReduceMotion = reduced.IsChecked == true; };
        var pause = new CheckBox { Name = "CircularPause", Content = "暂停动画", Margin = new Thickness(0, 0, 12, 6) };
        pause.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.IsAnimationPaused = pause.IsChecked == true; };
        var status = new TextBlock { Name = "CircularSelectionStatus", Text = "点击扇区选择；零值保留业务项但不分配角度，默认忽略负值。", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = .8 };
        foreach (var chart in charts) chart.SelectionChanged += (_, _) => status.Text = chart.SelectedPoint is { } hit ? $"{chart.Title} · {hit.StableId} · 原始值 {hit.Point.Y:0.##} · 源索引 {hit.Index}" : "选择已清除。";
        var options = new WrapPanel { Children = { new TextBlock { Text = "负值策略", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }, policy, new TextBlock { Text = "标签位置", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 6) }, labels, tiny, reduced, pause } };
        var revision = 0;
        void UpdateData()
        {
            revision++; var values = revision % 2 == 0 ? new[] { 50d, 25, 25 } : new[] { 20d, 55, 25 };
            var next = records.Select((item, index) => index < 3 ? item with { Value = values[index] } : item).ToArray();
            foreach (var chart in charts.Take(5)) foreach (var series in chart.Series) series.ItemsSource = next;
            UpdateCenterText();
        }
        void UpdateCenterText()
        {
            var current = (IEnumerable<CircularRecord>)donutSeries.ItemsSource!;
            var total = current.Sum(item => donutSeries.NegativeValueMode == ChartCircularNegativeValueMode.Absolute ? Math.Abs(item.Value) : Math.Max(0, item.Value));
            donut.CenterText = $"渠道\n{total:0.##}";
        }
        AddAction(options, "更新业务数据", UpdateData, "相同业务 ID 在新的份额间过渡，选择保留身份。");
        AddAction(options, "重播入场", () => { foreach (var chart in charts) chart.ReplayEntranceAnimation(); }, "重播当前饼、环及部分环的入场动画。");
        AddAction(options, "选中并分离首项", () => { var slice = donut.GetSlices().FirstOrDefault(value => value.Data.SourceIndex == 0); if (slice is not null) donut.SelectSlice(slice); donutSeries.ExplodedIndex = 0; donutSeries.ExplodeOffset = 16; }, "把第一个业务扇区沿角平分线平移 16 DIP。");
        AddAction(options, "清除选择和分离", () => { foreach (var chart in charts) chart.ClearSelection(); donutSeries.ExplodedIndex = -1; }, "恢复完整圆环布局并清除选择。");
        AddAction(options, "取消并完成", () => { foreach (var chart in charts) chart.CancelAnimations(); pause.IsChecked = false; }, "结束时间线并显示最新目标。");
        AddAction(options, "验证拒绝负值", () =>
        {
            using var temporary = new CircularBoardScope(new CircularChart()); temporary.Chart.Series.Add(Source(new PieSeries { NegativeValueMode = ChartCircularNegativeValueMode.Throw }));
            try { _ = temporary.Chart.GetSlices(); status.Text = "负值拒绝检查未触发，请检查异常报告。"; } catch (InvalidOperationException) { status.Text = "Throw 策略已拒绝负值；页面继续展示当前数据。"; }
        }, "显式 Throw 策略在构建负值份额时拒绝输入。");
        var export = new Button { Content = "导出当前环图 PNG", Margin = new Thickness(0, 0, 8, 6) };
        export.Click += async (_, _) =>
        {
            var file = await _hostWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions { Title = "导出圆形图当前画面", SuggestedFileName = "circular-chart.png", DefaultExtension = "png" });
            if (file is null) return; await using var stream = await file.OpenWriteAsync(); donut.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(1000, 700), Background = donut.Background });
        };
        options.Children.Add(export);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 10 };
        for (var index = 0; index < charts.Length; index++) { grid.Children.Add(charts[index]); Grid.SetColumn(charts[index], index % 2); Grid.SetRow(charts[index], index / 2); }
        return new(new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "饼、环、半环、部分环和多环共用源数据投影。零/负值有显式策略，扇区间隔不改写原始份额，小扇区可保留或隐藏；外标签带连线并避让。点击选择保留业务 ID，选择/分离、入场和数据变化参与同一时间线，可暂停、取消、减少动态效果及导出。", TextWrapping = TextWrapping.Wrap, Opacity = .8 }, options, grid, status } }, charts, records, pieSeries, donutSeries, smallSeries, policy, labels, tiny, reduced, pause, UpdateData);
    }

    private static CircularRecord[] CircularRecords() => [new("circular-a", "线上", 50, "#4385F4"), new("circular-b", "门店", 25, "#19A798"), new("circular-c", "合作", 25, "#F2A33A"), new("circular-zero", "零值", 0, "#9C247A"), new("circular-negative", "负值", -10, "#D7256A")];
    private sealed record CircularRecord(string Id, string Label, double Value, string Color);
    private sealed record CircularBoard(StackPanel View, CircularChart[] Charts, CircularRecord[] Records, PieSeries Pie, DonutSeries Donut, PieSeries Small, ComboBox NegativePolicy, ComboBox Labels, CheckBox HideTiny, CheckBox ReducedMotion, CheckBox Pause, Action UpdateData) : IDisposable
    { public void Dispose() { foreach (var chart in Charts) { chart.CancelAnimations(); chart.Series.Clear(); } } }
    private sealed record CircularBoardScope(CircularChart Chart) : IDisposable { public void Dispose() => Chart.Series.Clear(); }
}
