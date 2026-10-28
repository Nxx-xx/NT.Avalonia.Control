using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class C7SharedThemeTests
{
    [AvaloniaFact]
    public void Shared_mode_preserves_the_Cartesian_property_field_and_CLR_entry_points()
    {
        Assert.Same(ChartBase.HighContrastModeProperty, CartesianChart.HighContrastModeProperty);
        var cartesian = new CartesianChart(); ChartBase chart = cartesian;
        chart.HighContrastMode = ChartHighContrastMode.Dark;
        Assert.Equal(ChartHighContrastMode.Dark, cartesian.HighContrastMode);
        cartesian.HighContrastMode = ChartHighContrastMode.Light;
        Assert.Equal(ChartHighContrastMode.Light, chart.HighContrastMode);
        Assert.Throws<ArgumentException>(() => chart.HighContrastMode = (ChartHighContrastMode)99);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Every_public_chart_inherits_the_preset_and_restores_its_normal_theme(bool darkNormalTheme)
    {
        var types = typeof(ChartBase).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && typeof(ChartBase).IsAssignableFrom(type)).OrderBy(type => type.Name).ToArray();
        Assert.NotEmpty(types);
        var window = new Window { Width = 420, Height = 280, RequestedThemeVariant = darkNormalTheme ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            foreach (var type in types)
            {
                var chart = Assert.IsAssignableFrom<ChartBase>(Activator.CreateInstance(type));
                window.Content = chart; Drain(window);
                var normalForeground = ColorOf(chart.Foreground); var normalBackground = ColorOf(chart.Background);
                foreach (var mode in new[] { ChartHighContrastMode.Dark, ChartHighContrastMode.Light })
                {
                    chart.HighContrastMode = mode; Drain(window);
                    var canvas = mode == ChartHighContrastMode.Dark ? Colors.Black : Colors.White;
                    var foreground = mode == ChartHighContrastMode.Dark ? Colors.White : Colors.Black;
                    Assert.True(ColorOf(chart.Background) == canvas, $"{type.Name} did not receive {mode} canvas");
                    Assert.True(ColorOf(chart.Foreground) == foreground, $"{type.Name} did not receive {mode} foreground");
                    AssertFamilyBrushes(chart, mode, canvas, foreground);
                }
                chart.HighContrastMode = ChartHighContrastMode.None; Drain(window);
                Assert.Equal(normalForeground, ColorOf(chart.Foreground)); Assert.Equal(normalBackground, ColorOf(chart.Background));
                window.Content = null; Drain(window);
                (chart as IDisposable)?.Dispose();
            }
        }
        finally { window.Close(); Drain(window); }
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Theme_switches_preserve_selection_source_identity_and_reopen_subscriptions(int family)
    {
        var rows = new ObservableCollection<Row> { new("a", 0, "A", 15), new("b", 1, "B", 25), new("c", 2, "C", 35) };
        var fixture = Make(family, rows); var chart = fixture.Chart;
        var window = new Window { Content = chart, Width = 540, Height = 360 }; window.Show(); Drain(window);
        try
        {
            Assert.All(rows, row => Assert.True(row.Observers > 0));
            fixture.Select(1); Drain(window); Assert.Same(rows[1], fixture.Selected()); var selected = rows[1];
            foreach (var mode in new[] { ChartHighContrastMode.Dark, ChartHighContrastMode.Light, ChartHighContrastMode.None })
            {
                chart.HighContrastMode = mode; Drain(window);
                Assert.Same(selected, fixture.Selected()); Assert.Equal(3, fixture.Data().Count);
                Assert.Same(selected, fixture.Data().Single(point => Equals(point.StableId, "b")).Item);
            }
            rows.Move(1, 0); selected.Value = 41; Drain(window);
            Assert.Same(selected, fixture.Selected()); Assert.Equal(41, fixture.Data().Single(point => Equals(point.StableId, "b")).Point.Y);
            window.Content = null; Drain(window); Assert.All(rows, row => Assert.Equal(0, row.Observers));
            chart.HighContrastMode = ChartHighContrastMode.Dark; selected.Value = 73;
            window.Content = chart; Drain(window);
            Assert.Same(selected, fixture.Selected()); Assert.All(rows, row => Assert.True(row.Observers > 0));
            Assert.Equal(73, fixture.Data().Single(point => Equals(point.StableId, "b")).Point.Y);
            Assert.Equal(Colors.Black, ColorOf(chart.Background));
            var replacement = new ObservableCollection<Row> { new("d", 3, "D", 19) };
            fixture.Replace(replacement); Drain(window);
            Assert.All(rows, row => Assert.Equal(0, row.Observers)); Assert.True(replacement[0].Observers > 0);
            Assert.Null(fixture.Selected()); Assert.Same(replacement[0], Assert.Single(fixture.Data()).Item);
            rows[0].Value = 999; Drain(window); Assert.Equal(19, Assert.Single(fixture.Data()).Point.Y);
            window.Content = null; Drain(window); Assert.Equal(0, replacement[0].Observers);
        }
        finally { window.Close(); Drain(window); }
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Local_brushes_and_live_bindings_keep_precedence_over_all_mode_changes(int family)
    {
        ChartBase chart = family switch
        {
            0 => new CartesianChart(), 1 => new FunnelChart(), 2 => new StripPlot(),
            3 => new RadarChart(), 4 => new CircularChart(), 5 => new LinearGaugeChart(), _ => new SparklineChart(),
        };
        var brushProperty = DataBrushProperty(chart); var model = new BrushModel();
        using var binding = chart.Bind(brushProperty, new Binding(nameof(BrushModel.Brush)) { Source = model });
        chart.Foreground = Brushes.Pink;
        var window = new Window { Content = chart, Width = 420, Height = 280 }; window.Show(); Drain(window);
        try
        {
            foreach (var mode in new[] { ChartHighContrastMode.Dark, ChartHighContrastMode.Light, ChartHighContrastMode.None })
            {
                chart.HighContrastMode = mode; Drain(window);
                Assert.Same(Brushes.Pink, chart.Foreground); Assert.Same(model.Brush, chart.GetValue(brushProperty));
                model.Brush = ReferenceEquals(model.Brush, Brushes.Red) ? Brushes.Lime : Brushes.Red; Drain(window);
                Assert.Same(model.Brush, chart.GetValue(brushProperty));
            }
            chart.ClearValue(TemplatedControl.ForegroundProperty); chart.HighContrastMode = ChartHighContrastMode.Dark; Drain(window);
            Assert.Equal(Colors.White, ColorOf(chart.Foreground));
        }
        finally { window.Close(); Drain(window); }
    }

    [AvaloniaFact]
    public void Sparkline_and_gauge_special_pens_follow_modes_and_restore_after_detach()
    {
        var rows = new ObservableCollection<Row> { new("a", 0, "A", 1), new("b", 1, "B", -1) };
        var sparkline = new SparklineChart { ItemsSource = rows, ValuePath = nameof(Row.Value), CategoryPath = nameof(Row.Label), IdPath = nameof(Row.Id), SparklineType = SparklineType.WinLoss };
        var gauge = new GradientRingChart { Value = 35, TargetValue = 70 }; gauge.Values.Add(new GaugeValue { Value = 35 });
        var panel = new StackPanel { Children = { sparkline, gauge } };
        var window = new Window { Content = panel, Width = 420, Height = 400 }; window.Show(); Drain(window);
        try
        {
            var normalLine = sparkline.LineBrush; var normalValue = gauge.ValueBrush;
            sparkline.HighContrastMode = gauge.HighContrastMode = ChartHighContrastMode.Dark; Drain(window);
            Assert.Same(ChartContrastPalettes.Dark[1], sparkline.WinBrush); Assert.Same(ChartContrastPalettes.Dark[3], sparkline.LossBrush);
            Assert.Equal(Colors.White, ColorOf(sparkline.BaselineBrush)); Assert.Equal(Colors.White, ColorOf(gauge.NeedleBrush));
            Assert.Same(ChartContrastPalettes.Dark[1], gauge.ValueBrush);
            window.Content = null; Drain(window); Assert.All(rows, row => Assert.Equal(0, row.Observers));
            rows[0].Value = 17; sparkline.HighContrastMode = gauge.HighContrastMode = ChartHighContrastMode.None;
            window.Content = panel; Drain(window);
            Assert.Same(normalLine, sparkline.LineBrush); Assert.Same(normalValue, gauge.ValueBrush); Assert.Equal(17, sparkline.GetPoints()[0].Data.Point.Y);
        }
        finally { window.Close(); Drain(window); }
    }

    private static void AssertFamilyBrushes(ChartBase chart, ChartHighContrastMode mode, Color canvas, Color foreground)
    {
        var palette = mode == ChartHighContrastMode.Dark ? ChartContrastPalettes.Dark : ChartContrastPalettes.Light;
        switch (chart)
        {
            case CartesianChart c: Assert.Same(palette, c.ColorPalette); Assert.Equal(foreground, ColorOf(c.AxisBrush)); Assert.Equal(canvas, ColorOf(c.TooltipBackground)); break;
            case EngineeringChartBase c:
                Assert.Same(palette, c.Palette); Assert.Equal(canvas, ColorOf(c.TooltipBackground));
                Assert.Equal(foreground, ColorOf(c.TooltipForeground)); Assert.Equal(foreground, ColorOf(c.TooltipBorderBrush)); break;
            case AnalysisChartBase c:
                Assert.Same(palette, c.Palette); Assert.Equal(canvas, ColorOf(c.TooltipBackground));
                Assert.Equal(foreground, ColorOf(c.TooltipForeground)); Assert.Equal(foreground, ColorOf(c.TooltipBorderBrush)); break;
            case PolarChart c: Assert.Same(palette, c.ColorPalette); Assert.Equal(foreground, ColorOf(c.AxisBrush)); break;
            case CircularChart c: Assert.Same(palette, c.ColorPalette); Assert.Equal(foreground, ColorOf(c.LabelConnectorBrush)); break;
            case GaugeBase c: Assert.Same(palette[1], c.ValueBrush); Assert.Equal(foreground, ColorOf(c.NeedleBrush)); Assert.Equal(foreground, ColorOf(c.AxisBrush)); break;
            case SparklineChart c: Assert.Same(palette[0], c.LineBrush); Assert.Same(palette[1], c.WinBrush); Assert.Same(palette[3], c.LossBrush); break;
            default: Assert.Fail($"Missing theme root for {chart.GetType().Name}"); break;
        }
    }

    private static AvaloniaProperty DataBrushProperty(ChartBase chart) => chart switch
    {
        CartesianChart => CartesianChart.AxisBrushProperty, AnalysisChartBase => TemplatedControl.BackgroundProperty,
        EngineeringChartBase => EngineeringChartBase.GridLineBrushProperty, PolarChart => PolarChart.AxisBrushProperty,
        CircularChart => CircularChart.LabelConnectorBrushProperty, GaugeBase => GaugeBase.ValueBrushProperty, SparklineChart => SparklineChart.LineBrushProperty,
        _ => throw new ArgumentOutOfRangeException(nameof(chart)),
    };

    private static Fixture Make(int family, ObservableCollection<Row> rows)
    {
        if (family == 1)
        {
            var chart = new FunnelChart { ItemsSource = rows, CategoryPath = nameof(Row.Label), ValuePath = nameof(Row.Value), IdPath = nameof(Row.Id) };
            return new(chart, value => chart.ItemsSource = value, () => chart.GetMarks().Select(mark => mark.Data).ToArray(), index => chart.SelectedIndex = index, () => chart.SelectedPoint?.Item);
        }
        if (family == 2)
        {
            var chart = new StripPlot { ItemsSource = rows, CategoryPath = nameof(Row.Label), ValuePath = nameof(Row.Value), IdPath = nameof(Row.Id) };
            return new(chart, value => chart.ItemsSource = value, () => chart.GetMarks().SelectMany(mark => mark.Members).ToArray(), index => chart.SelectedIndex = index, () => chart.SelectedPoint?.Members[0].Item);
        }
        ChartSeries series; ChartBase owner;
        if (family == 3) { var chart = new RadarChart(); var radar = new RadarSeries(); chart.Series.Add(radar); owner = chart; series = radar; }
        else if (family == 4) { var chart = new CircularChart(); var pie = new PieSeries(); chart.Series.Add(pie); owner = chart; series = pie; }
        else { var chart = new CartesianChart(); var line = new LineSeries(); chart.Series.Add(line); owner = chart; series = line; }
        series.IdPath = nameof(Row.Id); series.CategoryPath = family == 0 ? nameof(Row.X) : nameof(Row.Label); series.ValuePath = nameof(Row.Value); series.ItemsSource = rows;
        ChartPointHit Hit(int index) { var data = series.CreateSnapshot().Points[index]; return new(series, data.Point, data.SourceIndex, default) { Item = data.Item, StableId = data.StableId }; }
        return new(owner, value => series.ItemsSource = value, () => series.CreateSnapshot().Points,
            index => { var hit = Hit(index); switch (owner) { case CartesianChart c: c.SelectedPoint = hit; break; case PolarChart c: c.SelectedPoint = hit; break; case CircularChart c: c.SelectedPoint = hit; break; } },
            () => owner switch { CartesianChart c => c.SelectedPoint?.Item, PolarChart c => c.SelectedPoint?.Item, CircularChart c => c.SelectedPoint?.Item, _ => null });
    }

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
    private static void Drain(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private sealed record Fixture(ChartBase Chart, Action<ObservableCollection<Row>> Replace, Func<IReadOnlyList<ChartDataPoint>> Data, Action<int> Select, Func<object?> Selected);
    private sealed class Row(string id, double x, string label, double value) : INotifyPropertyChanged
    {
        private double _value = value; private PropertyChangedEventHandler? _changed;
        public string Id { get; } = id; public double X { get; } = x; public string Label { get; } = label; public int Observers { get; private set; }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
    private sealed class BrushModel : INotifyPropertyChanged
    {
        private IBrush _brush = Brushes.Red; public IBrush Brush { get => _brush; set { _brush = value; PropertyChanged?.Invoke(this, new(nameof(Brush))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
