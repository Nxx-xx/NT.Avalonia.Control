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
    private static readonly string[] C5CompletionKinds = ["beeswarm", "strip", "contour", "mosaic", "parallel", "violin", "ridgeline"];
    private readonly List<EngineeringChartBase> _c5CompletionCharts = [];
    private abstract class C5ObservedRow(string id, string label) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public string Id { get; } = id;
        public string Label { get; } = label;
        public int Subscribers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { Subscribers++; _changed += value; } remove { Subscribers--; _changed -= value; } }
        protected void Changed(string property) => _changed?.Invoke(this, new(property));
    }
    private sealed class C5SampleRow(string id, string category, double value) : C5ObservedRow(id, id)
    {
        private double _value = value;
        public string Category { get; } = category;
        public double Value { get => _value; set { _value = value; Changed(nameof(Value)); } }
    }
    private sealed class C5ContourRow(string id, double x, double y, double value) : C5ObservedRow(id, id)
    {
        private double _value = value;
        public double X { get; } = x;
        public double Y { get; } = y;
        public double Value { get => _value; set { _value = value; Changed(nameof(Value)); } }
    }
    private sealed class C5MosaicRow(string id, string column, string row, double value) : C5ObservedRow(id, id)
    {
        private double _value = value;
        public string Column { get; } = column;
        public string Row { get; } = row;
        public double Value { get => _value; set { _value = value; Changed(nameof(Value)); } }
    }
    private sealed class C5ParallelRow(string id, double cost, double quality, double speed) : C5ObservedRow(id, id)
    {
        private double _cost = cost, _quality = quality, _speed = speed;
        public double Cost { get => _cost; set { _cost = value; Changed(nameof(Cost)); } }
        public double Quality { get => _quality; set { _quality = value; Changed(nameof(Quality)); } }
        public double Speed { get => _speed; set { _speed = value; Changed(nameof(Speed)); } }
    }
    private sealed class C5ProfileRow(string id, string label, IEnumerable<double> samples) : C5ObservedRow(id, label)
    { public ObservableCollection<double> Samples { get; } = new(samples); }
    private static EngineeringChartBase CreateC5CompletionChart(string kind)
    {
        var points = new ObservableCollection<C5SampleRow> { new("sample-a0", "A", 0), new("sample-a1", "A", 1), new("sample-a2", "A", 1), new("sample-a3", "A", 2), new("sample-b0", "B", 0), new("sample-b1", "B", 1), new("sample-b2", "B", 1), new("sample-b3", "B", 2) };
        var profiles = new ObservableCollection<C5ProfileRow> { new("profile-a", "A", [-1d, 0, 0, 0, 1]), new("profile-b", "B", [-1d, 0, 0, 0, 1]) };
        EngineeringChartBase chart = kind switch
        {
            "beeswarm" => new BeeswarmChart { ItemsSource = points, MarkerSize = 8, Jitter = .36 },
            "strip" => new StripPlot { ItemsSource = points, MarkerSize = 8, Jitter = 0, JitterSeed = 17 },
            "contour" => new ContourPlot { ItemsSource = new ObservableCollection<C5ContourRow> { new("grid-00", 0, 0, 0), new("grid-10", 1, 0, 1), new("grid-11", 1, 1, 2), new("grid-01", 0, 1, 1) }, Levels = [1d], ShowFill = true, ShowLines = true },
            "mosaic" => new MosaicChart { ItemsSource = new ObservableCollection<C5MosaicRow> { new("m-ax1", "A", "X", 1), new("m-ax2", "A", "X", 1), new("m-ay", "A", "Y", 2), new("m-bx", "B", "X", 1), new("m-by", "B", "Y", 3) }, CellGap = 0 },
            "parallel" => CreateC5Parallel(),
            "violin" => new ViolinPlot { ItemsSource = profiles, SampleCount = 65, Bandwidth = .5, Minimum = -2, Maximum = 2, ProfileWidth = .8 },
            "ridgeline" => new RidgelineChart { ItemsSource = profiles, SampleCount = 65, Bandwidth = .5, Minimum = -2, Maximum = 2, ProfileWidth = 1, Overlap = 0 },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        chart.IdPath = "Id"; chart.LabelPath = "Label"; chart.Width = 420; chart.Height = 280;
        chart.Palette = [Brush("#4385F4"), Brush("#F2A33A"), Brush("#55B47A"), Brush("#D25573")];
        return chart;
    }
    private static ParallelCoordinatesChart CreateC5Parallel()
    {
        var chart = new ParallelCoordinatesChart { ItemsSource = new ObservableCollection<C5ParallelRow> { new("parallel-a", 0, 5, 10), new("parallel-b", 10, 5, 0), new("parallel-gap", 2, double.NaN, 8) } };
        chart.Axes.Add(new() { Id = "cost", Header = "Cost", ValuePath = "Cost", Minimum = 0, Maximum = 10 });
        chart.Axes.Add(new() { Id = "quality", Header = "Quality", ValuePath = "Quality", Minimum = 0, Maximum = 10 });
        chart.Axes.Add(new() { Id = "speed", Header = "Speed", ValuePath = "Speed", Minimum = 0, Maximum = 10 });
        return chart;
    }
    private Control CreateC5CompletionExamples() => new Expander
    {
        Name = "ChartC5CompletionExamples", Header = "C5 补齐：分布、等值线、列联比例与多维比较", HorizontalAlignment = HorizontalAlignment.Stretch,
        Content = CreateC5CompletionBoard()
    };
    private Control CreateC5CompletionBoard(bool overview = false)
    {
        var charts = C5CompletionKinds.Select(CreateC5CompletionChart).ToArray(); if (!overview) _c5CompletionCharts.AddRange(charts);
        string[] titles = ["Beeswarm · 每个原始观测", "Strip · 可重复抖动", "Contour · 共用拓扑的线与填色", "Mosaic · 重复分类聚合", "Parallel · 独立数值轴和缺失断点", "Violin · 密度、四分位与中位数", "Ridgeline · 同尺度密度比较"];
        var first = new Grid { ColumnDefinitions = new("*,*,*"), ColumnSpacing = 12 };
        var second = new Grid { ColumnDefinitions = new("*,*,*,*"), ColumnSpacing = 12 };
        for (var i = 0; i < charts.Length; i++)
        { charts[i].Width = double.NaN; charts[i].Height = 300; charts[i].Title = titles[i]; Grid.SetColumn(charts[i], i < 3 ? i : i - 3); (i < 3 ? first : second).Children.Add(charts[i]); }
        var selected = new TextBlock { Text = "鼠标或方向键浏览，Space 选择；聚合图形与密度保留原始记录。", TextWrapping = TextWrapping.Wrap };
        foreach (var chart in charts) chart.PropertyChanged += (_, change) => { if (change.Property == EngineeringChartBase.SelectedPointProperty && chart.SelectedPoint is { } hit) selected.Text = $"{chart.Title} · {hit.Mark.TooltipText} · 原始序号 {string.Join(", ", hit.Members.Select(point => point.SourceIndex))}"; };
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        AddAction(actions, "C5 缩放 / 平移", () => { foreach (var chart in charts) { chart.Zoom = 1.25; chart.PanX = .03; chart.PanY = -.02; } }, "视图变化保持原始来源。");
        AddAction(actions, "C5 恢复视图", () => { foreach (var chart in charts) chart.ResetViewport(); }, "恢复初始视图。");
        AddAction(actions, "C5 更新数据", () => { foreach (var chart in charts) UpdateC5CompletionSource(chart); }, "可观察 DTO、集合和嵌套 Samples 更新。");
        AddAction(actions, "等值线填色", () => { var chart = (ContourPlot)charts[2]; chart.ShowFill = !chart.ShowFill; }, "线与填色使用同一表面。");
        AddAction(actions, "Strip 抖动", () => { var chart = (StripPlot)charts[1]; chart.Jitter = chart.Jitter == 0 ? .25 : 0; }, "固定种子的抖动不会改变观测值。");
        AddAction(actions, "小提琴摘要", () => { var chart = (ViolinPlot)charts[5]; chart.ShowQuartiles = !chart.ShowQuartiles; }, "切换中位数与四分位摘要。");
        AddAction(actions, "山峦重叠", () => { var chart = (RidgelineChart)charts[6]; chart.Overlap = chart.Overlap == 0 ? .5 : 0; }, "同一绝对密度尺度。");
        return new StackPanel { Spacing = 10, Children = { new TextBlock { Text = "C5 · 统计与多维补齐", FontSize = 18, FontWeight = FontWeight.SemiBold }, actions, selected, first, second } };
    }
    private static void UpdateC5CompletionSource(EngineeringChartBase chart)
    {
        if (chart.ItemsSource is null) return;
        foreach (var item in chart.ItemsSource)
        {
            switch (item)
            {
                case C5SampleRow row: row.Value += row.Category == "A" ? .25 : -.25; break;
                case C5ContourRow row: row.Value += .25; break;
                case C5MosaicRow row: row.Value += 1; break;
                case C5ParallelRow row: row.Cost = (row.Cost + 1) % 10; row.Quality = double.IsNaN(row.Quality) ? double.NaN : (row.Quality + 1) % 10; row.Speed = (row.Speed + 1) % 10; break;
                case C5ProfileRow row: row.Samples.Add(.5); break;
            }
        }
    }
    private void DisposeC5CompletionExamples()
    { foreach (var chart in _c5CompletionCharts) (chart as IDisposable)?.Dispose(); _c5CompletionCharts.Clear(); }
}
