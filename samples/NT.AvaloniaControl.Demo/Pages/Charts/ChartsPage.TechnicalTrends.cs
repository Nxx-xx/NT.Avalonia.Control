using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Control CreateTechnicalTrendsExamples() => new Expander
    {
        Name = "ChartTechnicalTrendsExamples", Header = "技术指标与趋势：均线、布林带、回归与预测",
        HorizontalAlignment = HorizontalAlignment.Stretch, Content = CreateTechnicalTrendsBoard(),
    };

    private Control CreateTechnicalTrendsBoard()
    {
        var values = new ObservableCollection<ChartPoint>(TechnicalDemoPoints(36));
        var purple = Brush("#8B5CF6");
        var moving = TechnicalDemoChart("四种均线 · SMA / EMA / WMA / TMA");
        var averageSource = TechnicalDemoSource(values); moving.Series.Add(averageSource);
        var averages = Enum.GetValues<ChartMovingAverageType>().Select((type, i) => new MovingAverageSeries
        {
            Title = type.ToString(), SourceSeries = averageSource, Type = type, Period = 5,
            Stroke = new[] { Blue, Orange, Teal, purple }[i], StrokeThickness = 2.2,
        }).ToArray();
        foreach (var average in averages) moving.Series.Add(average);

        var indicatorChart = TechnicalDemoChart("指标对象 · SMA / EMA / WMA / Bollinger");
        var indicatorSource = TechnicalDemoSource(values); indicatorChart.Series.Add(indicatorSource);
        ChartTechnicalIndicator[] indicators =
        [
            new SMAIndicator { Title = "SMA", SourceSeries = indicatorSource, Period = 5, Stroke = Blue },
            new EMAIndicator { Title = "EMA", SourceSeries = indicatorSource, Period = 5, Stroke = Orange },
            new WMAIndicator { Title = "WMA", SourceSeries = indicatorSource, Period = 5, Stroke = purple },
        ];
        foreach (var indicator in indicators) indicatorChart.TechnicalIndicators.Add(indicator);
        var bands = new BollingerBandsIndicator { Title = "Bollinger", SourceSeries = indicatorSource, Period = 5, Stroke = Teal };
        bands.UpperSeries.Stroke = Teal; bands.LowerSeries.Stroke = Teal; bands.BandSeries.Fill = Brush("#181CA58A");
        bands.UpperSeries.ShowInLegend = bands.LowerSeries.ShowInLegend = false; bands.BandSeries.ShowInLegend = true;
        indicatorChart.TechnicalIndicators.Add(bands);

        var attachedChart = TechnicalDemoChart("附加趋势 · 随源数据更新");
        var attachedSource = TechnicalDemoSource(values); attachedChart.Series.Add(attachedSource);
        var attached = new Trendline { Title = "附加拟合", Type = ChartTrendlineType.Polynomial, Period = 5, Stroke = Orange, StrokeThickness = 2.5, ForwardForecast = 3 };
        attachedSource.Trendlines.Add(attached);

        var standaloneChart = TechnicalDemoChart("独立趋势 · 多项式与前后预测");
        var regressionInput = new ScatterSeries
        {
            Title = "观测", ItemsSource = Enumerable.Range(1, 16).Select(i => new ChartPoint(i, 2 + .2 * i * i + Math.Sin(i) * 2)).ToArray(),
            Fill = Teal, MarkerSize = 5,
        };
        standaloneChart.Series.Add(regressionInput);
        var standalone = new ChartTrendlineSeries
        {
            Title = "Polynomial", SourceSeries = regressionInput, Type = ChartTrendlineType.Polynomial,
            PolynomialDegree = 2, Period = 5, BackwardForecast = .5, ForwardForecast = 2, Stroke = purple, StrokeThickness = 2.5,
        };
        standaloneChart.Series.Add(standalone);

        var financial = new FinancialChart { Height = 285 };
        financial.Chart.Title = "金融叠加 · 交易槽、价格与成交量";
        financial.Chart.AnimationTriggers = ChartAnimationTriggers.None; financial.Chart.ShowLegend = true;
        financial.TradingAxis.LabelMode = ChartAxisLabelMode.Stagger;
        financial.VolumeAxis.Maximum = 10000; financial.SetData(FinancialDemoRows(36, 0));
        var financialAverage = new MovingAverageSeries { Title = "EMA (5)", SourceSeries = financial.PriceSeries, Type = ChartMovingAverageType.Exponential, Period = 5, Stroke = Orange };
        financial.Series.Add(financialAverage);
        var financialBands = new BollingerBandsIndicator { Title = "价格带", SourceSeries = financial.PriceSeries, Period = 5, Stroke = Blue };
        financialBands.UpperSeries.Stroke = Blue; financialBands.LowerSeries.Stroke = Blue; financialBands.BandSeries.Fill = Brush("#15155CC1");
        financialBands.UpperSeries.ShowInLegend = financialBands.LowerSeries.ShowInLegend = false; financialBands.BandSeries.ShowInLegend = true;
        financial.Chart.TechnicalIndicators.Add(financialBands);

        var history = TechnicalDemoChart("缺口与历史 · 缩放保留视口外预热数据");
        var historySource = TechnicalDemoSource(values); history.Series.Add(historySource);
        var historyAverage = new MovingAverageSeries { Title = "完整历史 EMA", SourceSeries = historySource, Type = ChartMovingAverageType.Exponential, Period = 5, Stroke = Orange, StrokeThickness = 2.5 };
        history.Series.Add(historyAverage); history.Viewport = new ChartViewport(18, 30, 88, 124);

        var period = new ComboBox { ItemsSource = new[] { 3, 5, 8 }, SelectedIndex = 1, Width = 65 };
        var typePicker = new ComboBox { ItemsSource = Enum.GetValues<ChartTrendlineType>(), SelectedItem = ChartTrendlineType.Polynomial, Width = 140 };
        var status = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var revision = 0;
        void UpdateStatus() => status.Text = $"完整历史 {values.Count} 点 · 第13个观测为缺口 · 周期 {period.SelectedItem}；均线每段预热后开始。橙色历史 EMA 使用视口外数据。";
        period.SelectionChanged += (_, _) =>
        {
            var count = (int)(period.SelectedItem ?? 5);
            foreach (var average in averages) average.Period = count;
            foreach (var indicator in indicators) indicator.Period = count;
            bands.Period = financialBands.Period = financialAverage.Period = historyAverage.Period = attached.Period = standalone.Period = count;
            UpdateStatus();
        };
        typePicker.SelectionChanged += (_, _) =>
        {
            var type = (ChartTrendlineType)(typePicker.SelectedItem ?? ChartTrendlineType.Linear);
            attached.Type = standalone.Type = type;
            attached.ForwardForecast = type == ChartTrendlineType.MovingAverage ? 0 : 3;
            standalone.BackwardForecast = type == ChartTrendlineType.MovingAverage ? 0 : .5;
            standalone.ForwardForecast = type == ChartTrendlineType.MovingAverage ? 0 : 2;
            attached.Title = standalone.Title = type.ToString();
        };
        var actions = new WrapPanel { ItemSpacing = 10, LineSpacing = 6 };
        actions.Children.Add(new TextBlock { Text = "均线周期", VerticalAlignment = VerticalAlignment.Center }); actions.Children.Add(period);
        actions.Children.Add(new TextBlock { Text = "趋势类型", VerticalAlignment = VerticalAlignment.Center }); actions.Children.Add(typePicker);
        AddAction(actions, "追加观测", () =>
        {
            var index = values.Count; values.Add(TechnicalDemoValue(index, revision));
            financial.SetData(FinancialDemoRows(values.Count, revision)); UpdateStatus();
        }, "追加后重新计算完整历史；当前视口保持，价格叠加读取真实交易槽。");
        AddAction(actions, "替换数据", () =>
        {
            revision++;
            using (moving.DeferDataUpdates()) using (indicatorChart.DeferDataUpdates()) using (attachedChart.DeferDataUpdates()) using (history.DeferDataUpdates())
            { values.Clear(); foreach (var point in TechnicalDemoPoints(36, revision)) values.Add(point); }
            financial.SetData(FinancialDemoRows(36, revision)); UpdateStatus();
        }, "替换观测序列，关联指标和附加趋势随源数据通知更新。");
        AddAction(actions, "历史局部视图", () => history.Viewport = new ChartViewport(18, 30, 88, 124), "缩放只改变显示范围，不重新以可见点初始化 EMA。");
        AddAction(actions, "恢复视图", () =>
        {
            foreach (var chart in new[] { moving, indicatorChart, attachedChart, standaloneChart, history, financial.Chart }) chart.ResetViewport();
        }, "恢复六张图的完整数据范围。");
        UpdateStatus();
        var grid = new Grid { ColumnDefinitions = new("*,*"), RowDefinitions = new("Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 12 };
        Control[] panels = [moving, indicatorChart, attachedChart, standaloneChart, financial, history];
        for (var i = 0; i < panels.Length; i++) { Grid.SetColumn(panels[i], i % 2); Grid.SetRow(panels[i], i / 2); grid.Children.Add(panels[i]); }
        return new StackPanel { Spacing = 10, Children =
        {
            new TextBlock { Text = "SMA / EMA / WMA / TMA 使用完整输入历史；缺失值重置窗口。布林带为 SMA ± 2 倍总体标准差。拟合点为合成值，均线保留窗口末端来源；预测只是数学外推。", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            actions, status, grid,
        } };
    }

    private static CartesianChart TechnicalDemoChart(string title) => new()
    {
        Title = title, Height = 285, ShowLegend = true, AnimationTriggers = ChartAnimationTriggers.None,
        IsTooltipEnabled = true, TooltipMode = ChartTooltipMode.SharedX, IsZoomEnabled = true, IsPanEnabled = true,
        SelectionMode = ChartSelectionMode.Single, XAxis = new NumericAxis(), YAxis = new NumericAxis(),
    };
    private static LineSeries TechnicalDemoSource(IEnumerable<ChartPoint> points) => new()
    {
        Title = "观测", ItemsSource = points, Stroke = Brush("#77859A"), StrokeThickness = 1.1, ShowMarkers = false,
    };
    private static IEnumerable<ChartPoint> TechnicalDemoPoints(int count, int revision = 0) => Enumerable.Range(0, count).Select(i => TechnicalDemoValue(i, revision));
    private static ChartPoint TechnicalDemoValue(int index, int revision) => new(index + 1, index == 12 ? double.NaN : 100 + Math.Sin((index + revision) * .7) * 7 + index * .3);
}
