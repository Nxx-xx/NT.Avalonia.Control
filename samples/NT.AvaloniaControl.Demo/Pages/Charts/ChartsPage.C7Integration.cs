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
    private C7IntegrationBoard? _c7IntegrationBoard;

    private Control CreateC7IntegrationExamples()
    {
        _c7IntegrationBoard = CreateC7IntegrationBoard();
        return new Expander
        {
            Name = "ChartC7IntegrationExamples", Header = "C7 · 跨图族主题、业务来源和生命周期集成",
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = _c7IntegrationBoard.View,
        };
    }

    private C7IntegrationBoard CreateC7IntegrationBoard()
    {
        var rows = new ObservableCollection<C7IntegrationRow>
        {
            new("c7-a", "速度", 0, "产品 A", "线上", 35), new("c7-b", "质量", 1, "产品 A", "门店", 55),
            new("c7-c", "交付", 2, "产品 B", "线上", 80), new("c7-d", "成本", 3, "产品 B", "门店", 65),
        };
        var cartesian = new CartesianChart
        {
            Name = "C7IntegrationCartesian", ShowLegend = false, SelectionMode = ChartSelectionMode.Single,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 3, Interval = 1 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
        };
        var line = new LineSeries
        {
            Title = "共享业务值", ItemsSource = rows, CategoryPath = nameof(C7IntegrationRow.X),
            ValuePath = nameof(C7IntegrationRow.Value), IdPath = nameof(C7IntegrationRow.Id), ShowMarkers = true, MarkerSize = 7,
        };
        cartesian.Series.Add(line);
        var radar = new RadarChart
        {
            Name = "C7IntegrationRadar", ShowLegend = false, RadialAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            SelectionMode = ChartSelectionMode.Single,
        };
        radar.Series.Add(new RadarSeries
        {
            Title = "能力", ItemsSource = rows, CategoryPath = nameof(C7IntegrationRow.Category),
            RadiusPath = nameof(C7IntegrationRow.Value), IdPath = nameof(C7IntegrationRow.Id), MarkerSize = 7,
        });
        var circular = new CircularChart
        {
            Name = "C7IntegrationCircular", ShowLegend = false, LabelPosition = CircularLabelPosition.Inside,
            SelectionMode = ChartSelectionMode.Single, ReduceMotion = true,
        };
        circular.Series.Add(new DonutSeries
        {
            ItemsSource = rows, CategoryPath = nameof(C7IntegrationRow.Category), ValuePath = nameof(C7IntegrationRow.Value), IdPath = nameof(C7IntegrationRow.Id),
        });
        var gauge = new LinearGaugeChart { Name = "C7IntegrationGauge", Value = rows[0].Value, TargetValue = 80, Minimum = 0, Maximum = 100 };
        var sparkline = new SparklineChart
        {
            Name = "C7IntegrationSparkline", ItemsSource = rows, ValuePath = nameof(C7IntegrationRow.Value), IdPath = nameof(C7IntegrationRow.Id),
            ShowMarkers = true, ShowFirstLast = true, ShowMinMax = true, ShowBaseline = true,
        };
        var funnel = new FunnelChart
        {
            Name = "C7IntegrationFunnel", ItemsSource = rows, CategoryPath = nameof(C7IntegrationRow.Category),
            ValuePath = nameof(C7IntegrationRow.Value), IdPath = nameof(C7IntegrationRow.Id),
        };
        var mosaic = new MosaicChart
        {
            Name = "C7IntegrationMosaic", ItemsSource = rows, ColumnPath = nameof(C7IntegrationRow.Column), RowPath = nameof(C7IntegrationRow.Row),
            ValuePath = nameof(C7IntegrationRow.Value), IdPath = nameof(C7IntegrationRow.Id), LabelPath = nameof(C7IntegrationRow.Category),
        };
        var map = new SeatMap
        {
            Name = "C7IntegrationMap", ShowLabels = true, ShowLegend = false,
            FeaturesSource = Enumerable.Range(0, 5).Select(i => new MapFeature(i < rows.Count ? rows[i].Id : "no-data",
                [new MapPolygon([new(i, 0), new(i + .9, 0), new(i + .9, 1), new(i, 1), new(i, 0)])],
                i < rows.Count ? rows[i].Category : "无数据")).ToArray(),
        };
        // The normal default scale includes a pale minimum and a pale no-data fill.
        // Neither the chart brushes nor its shared palette have local overrides.
        var mapLayer = new ShapeLayer { ItemsSource = rows, RegionPath = nameof(C7IntegrationRow.Id), ValuePath = nameof(C7IntegrationRow.Value) };
        map.Layers.Add(mapLayer);
        ChartBase[] charts = [cartesian, radar, circular, gauge, sparkline, funnel, mosaic, map];
        string[] captions = ["Cartesian · 折线", "Radar · 分类能力", "Circular · 份额", "Gauge · 当前值 / 目标", "Sparkline · 紧凑趋势", "Funnel · 业务阶段", "Mosaic · 分类比例", "Map · 浅色与无数据标签"];
        var grid = new Grid { ColumnDefinitions = new("*,*,*,*"), RowDefinitions = new("Auto,Auto"), ColumnSpacing = 12, RowSpacing = 12 };
        var cards = new List<Control>();
        for (var i = 0; i < charts.Length; i++)
        {
            charts[i].Height = 250;
            var card = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = captions[i], FontSize = 14, FontWeight = FontWeight.SemiBold }, charts[i] } };
            cards.Add(card); grid.Children.Add(card); Grid.SetColumn(card, i % 4); Grid.SetRow(card, i / 4);
        }
        void Layout(int columns)
        {
            grid.ColumnDefinitions = new(string.Join(",", Enumerable.Repeat("*", columns)));
            grid.RowDefinitions = new(string.Join(",", Enumerable.Repeat("Auto", (charts.Length + columns - 1) / columns)));
            for (var i = 0; i < cards.Count; i++) { Grid.SetColumn(cards[i], i % columns); Grid.SetRow(cards[i], i / columns); }
        }
        grid.SizeChanged += (_, _) => { var columns = grid.Bounds.Width >= 1100 ? 4 : grid.Bounds.Width >= 650 ? 2 : 1; if (grid.ColumnDefinitions.Count != columns) Layout(columns); };
        var contrast = new ComboBox { Name = "C7IntegrationContrast", ItemsSource = Enum.GetValues<ChartHighContrastMode>(), SelectedItem = ChartHighContrastMode.None, MinWidth = 100, Margin = new Thickness(0, 0, 12, 6) };
        contrast.SelectionChanged += (_, _) => { if (contrast.SelectedItem is ChartHighContrastMode mode) foreach (var chart in charts) chart.HighContrastMode = mode; };
        var status = new TextBlock { Name = "C7IntegrationStatus", Text = "同一组可观察业务项；切换窗口主题与预设后，选择保留原始来源。", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = .8 };
        var options = new WrapPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = "共用高对比预设", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) }, contrast } };
        var revision = 0;
        void UpdateData()
        {
            revision++; rows[0].Value = revision % 2 == 0 ? 35 : 45; rows[1].Value = revision % 2 == 0 ? 55 : 70; gauge.Value = rows[0].Value;
            status.Text = $"数据更新 {revision} · 首项 {rows[0].Id} = {rows[0].Value:0}；各图继续使用相同 DTO 和业务 ID。";
        }
        void Reset()
        {
            foreach (var chart in charts) chart.ResetViewportCommand.Execute(null);
            cartesian.ClearSelection(); radar.ClearSelection(); circular.ClearSelection();
            status.Text = "八种根控件已复位；业务数据与当前主题预设保留。";
        }
        AddAction(options, "C7 更新共享数据", UpdateData, "通过 INPC 更新共用业务项；仪表同步首项标量值。");
        AddAction(options, "C7 恢复视图", Reset, "调用所有图族的公共 ResetViewportCommand 合约并清除当前选择。");
        var view = new StackPanel
        {
            Name = "C7IntegrationBoard", Spacing = 8, Children =
            {
                new TextBlock { Text = "C7 · 八图族集成", FontSize = 19, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "使用主窗口按钮切换亮 / 暗主题；一个预设同步八种根控件，主题决定画布、文字和系列配色。", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = .8 },
                options, status, grid,
            },
        };
        return new(view, charts, rows, cartesian, line, radar, circular, gauge, sparkline, funnel, mosaic, map, mapLayer, contrast, UpdateData, Reset);
    }

    private sealed class C7IntegrationRow(string id, string category, double x, string column, string row, double value) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed; private double _value = value;
        public string Id { get; } = id; public string Category { get; } = category; public double X { get; } = x;
        public string Column { get; } = column; public string Row { get; } = row;
        public int Subscribers { get; private set; }
        public double Value { get => _value; set { if (_value.Equals(value)) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Subscribers++; _changed += value; } remove { Subscribers--; _changed -= value; } }
    }

    private sealed record C7IntegrationBoard(StackPanel View, ChartBase[] Charts, ObservableCollection<C7IntegrationRow> Rows,
        CartesianChart Cartesian, LineSeries Line, RadarChart Radar, CircularChart Circular, LinearGaugeChart Gauge,
        SparklineChart Sparkline, FunnelChart Funnel, MosaicChart Mosaic, SeatMap Map, ShapeLayer MapLayer, ComboBox Contrast,
        Action UpdateData, Action Reset) : IDisposable
    {
        public void Dispose()
        {
            Cartesian.Series.Clear(); Radar.Series.Clear(); Circular.Series.Clear();
            Sparkline.ItemsSource = null; Funnel.ItemsSource = null; Mosaic.ItemsSource = null; Map.Dispose();
        }
    }
}
