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
    private CartesianChart? _controlTemplateChart;
    private Expander? _controlTemplateExamples;
    private readonly ObservableCollection<ControlTemplateRecord> _controlTemplateRecords =
    [new("template-primary", "直营订单", 1, 37), new("template-second", "会员订单", 3, 62), new("template-third", "到店订单", 5, 45)];

    private Control CreateControlTemplateExamples()
    {
        var chart = _controlTemplateChart = CreateAdvancedChart("ControlTemplateChart");
        chart.Height = 420;
        ConfigureControlTemplateChart(chart, _controlTemplateRecords, null);
        var legendTemplate = chart.LegendItemTemplate;
        var labelTemplate = chart.Series[0].DataLabelTemplate;
        var richLegend = new CheckBox { Content = "渠道图例卡", IsChecked = true, Margin = new Thickness(0, 0, 16, 0) };
        richLegend.IsCheckedChanged += (_, _) => chart.LegendItemTemplate = richLegend.IsChecked == true ? legendTemplate : null;
        var richLabels = new CheckBox { Content = "业务数据标签", IsChecked = true, Margin = new Thickness(0, 0, 16, 0) };
        richLabels.IsCheckedChanged += (_, _) => chart.Series[0].DataLabelTemplate = richLabels.IsChecked == true ? labelTemplate : null;
        var scroll = new CheckBox { Content = "滚动图例", IsChecked = true, Margin = new Thickness(0, 0, 16, 0) };
        scroll.IsCheckedChanged += (_, _) => chart.IsLegendScrollable = scroll.IsChecked == true;
        var actions = new WrapPanel { Children = { richLegend, richLabels, scroll } };
        AddAction(actions, "更新直营订单", () => _controlTemplateRecords[0].Value = _controlTemplateRecords[0].Value == 37 ? 54 : 37, "修改订单数据，查看数据标签与柱高同步变化。");
        AddAction(actions, "重置视图", chart.ResetViewport, "恢复完整月份与订单数范围。");
        _controlTemplateExamples = new Expander
        {
            Name = "ControlTemplateExamples", Header = "图例与数据标签：渠道卡、订单明细和长列表滚动", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 10, Children =
            {
                new TextBlock { Text = "滚动右侧渠道列表并点击整行显示或隐藏系列；订单标签直接展示业务名称、编号和数量。", TextWrapping = TextWrapping.Wrap, Opacity = .75 },
                actions, CreateChartCard("渠道订单", "TEMPLATES", "同一份订单数据同时驱动柱状图和业务标签", chart),
            } },
        };
        return _controlTemplateExamples;
    }

    private static void ConfigureControlTemplateChart(CartesianChart chart, ObservableCollection<ControlTemplateRecord> records, IBrush? textInk)
    {
        chart.Title = "渠道订单与计划";
        chart.XAxis = new NumericAxis { Title = "观察时段", Minimum = 0, Maximum = 6, Interval = 1 };
        chart.YAxis = new NumericAxis { Title = "订单数", Minimum = 0, Maximum = 100, Interval = 20 };
        chart.LegendPosition = ChartLegendPosition.Right;
        chart.LegendOrientation = Orientation.Vertical;
        chart.IsLegendInteractive = true;
        chart.IsLegendScrollable = true;
        chart.LegendMaxWidth = 230;
        chart.LegendMaxHeight = 205;
        chart.LegendItemTemplate = CreateControlLegendTemplate(chart, textInk);
        chart.Series.Add(new BarSeries
        {
            Title = "直营订单", ItemsSource = records, CategoryPath = nameof(ControlTemplateRecord.Position), ValuePath = nameof(ControlTemplateRecord.Value), IdPath = nameof(ControlTemplateRecord.Id),
            Fill = Blue, Stroke = Blue, ShowDataLabels = true, DataLabelFormat = "0", DataLabelPosition = ChartDataLabelPosition.Above,
            DataLabelTemplate = CreateControlDataLabelTemplate(chart, textInk),
        });
        chart.Series.Add(new LineSeries { Title = "履约目标", Stroke = Teal, StrokeThickness = 2, ItemsSource = new ChartPoint[] { new(1, 70), new(3, 76), new(5, 81) } });
        chart.Series.Add(new ScatterSeries { Title = "渠道观察", Stroke = Orange, Fill = Orange, MarkerSize = 9, ItemsSource = new ChartPoint[] { new(1.5, 21), new(3.5, 26), new(4.7, 32) } });
        for (var i = 4; i <= 16; i++)
            chart.Series.Add(new LineSeries { Title = $"候选渠道 {i}", Stroke = i % 2 == 0 ? Teal : Orange, IsVisible = false,
                ItemsSource = new ChartPoint[] { new(1, 10 + i), new(3, 15 + i), new(5, 12 + i) } });
    }

    private static IDataTemplate CreateControlLegendTemplate(CartesianChart chart, IBrush? textInk) => new FuncDataTemplate<ChartLegendItem>((_, _) =>
    {
        var swatch = new Border { Width = 9, Height = 28, CornerRadius = new CornerRadius(3) };
        swatch.Bind(Border.BackgroundProperty, new Binding("Brush"));
        var title = new TextBlock { Name = "ControlLegendTitle", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxWidth = 164 };
        if (textInk is null) title.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = chart }); else title.Foreground = textInk;
        title.Bind(TextBlock.TextProperty, new Binding("Series.Title"));
        var shown = new TextBlock { Text = "已显示 · 点击隐藏", FontSize = 11, Opacity = .72 };
        shown.Bind(IsVisibleProperty, new Binding("Series.IsVisible"));
        var hidden = new TextBlock { Text = "已隐藏 · 点击显示", FontSize = 11, Opacity = .72 };
        hidden.Bind(IsVisibleProperty, new Binding("!Series.IsVisible"));
        var body = new StackPanel { Spacing = 2, Children = { title, shown, hidden } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 9, Children = { swatch, body } };
        Grid.SetColumn(body, 1);
        var border = new Border { Name = "ControlLegendCard", Padding = new Thickness(7, 5), CornerRadius = new CornerRadius(5), Child = grid };
        border.Bind(Border.BackgroundProperty, new Binding(nameof(Background)) { Source = chart });
        return border;
    });

    private static IDataTemplate CreateControlDataLabelTemplate(CartesianChart chart, IBrush? textInk) => new FuncDataTemplate<ChartDataLabelContext>((_, _) =>
    {
        var name = new TextBlock { FontWeight = FontWeight.SemiBold };
        name.Bind(TextBlock.TextProperty, new Binding("Data.Item.Name"));
        var id = new TextBlock { Name = "ControlDataLabelId", FontSize = 11 };
        id.Bind(TextBlock.TextProperty, new Binding("Data.Item.Id"));
        var value = new TextBlock { Name = "ControlDataLabelValue" };
        value.Bind(TextBlock.TextProperty, new Binding("Data.Item.Value") { StringFormat = "{0:0} 单" });
        var border = new Border { Name = "ControlDataLabelCard", Padding = new Thickness(6, 4), CornerRadius = new CornerRadius(5), BorderBrush = textInk, BorderThickness = new Thickness(1),
            Child = new StackPanel { Spacing = 1, Children = { name, id, value } } };
        foreach (var text in new[] { name, id, value })
            if (textInk is null) text.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = chart }); else text.Foreground = textInk;
        if (textInk is null) border.Bind(Border.BorderBrushProperty, new Binding(nameof(Foreground)) { Source = chart });
        border.Bind(Border.BackgroundProperty, new Binding(nameof(CartesianChart.TooltipBackground)) { Source = chart });
        return border;
    });

    private sealed class ControlTemplateRecord(string id, string name, double position, double value) : INotifyPropertyChanged
    {
        private double _value = value;
        public string Id { get; } = id;
        public string Name { get; } = name;
        public double Position { get; } = position;
        public double Value { get => _value; set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
