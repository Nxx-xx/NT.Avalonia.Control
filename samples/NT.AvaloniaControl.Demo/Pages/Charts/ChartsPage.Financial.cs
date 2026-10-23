using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Control CreateFinancialExamples() => new Expander
    {
        Name = "ChartFinancialExamples", Header = "金融价格：蜡烛、OHLC、Heikin-Ashi 与价格变换",
        HorizontalAlignment = HorizontalAlignment.Stretch, Content = CreateFinancialBoard(),
    };

    private Control CreateFinancialBoard()
    {
        (string title, ChartFinancialTransformKind transform, ChartFinancialStyle style)[] definitions =
        [
            ("蜡烛 · 价格 / 成交量", ChartFinancialTransformKind.Raw, ChartFinancialStyle.Candlestick),
            ("OHLC · 开高低收", ChartFinancialTransformKind.Raw, ChartFinancialStyle.Ohlc),
            ("Hilo · 高低区间", ChartFinancialTransformKind.Raw, ChartFinancialStyle.HighLow),
            ("Heikin-Ashi · 平滑价格", ChartFinancialTransformKind.HeikinAshi, ChartFinancialStyle.Candlestick),
            ("Renko · 固定砖", ChartFinancialTransformKind.Renko, ChartFinancialStyle.Renko),
            ("Kagi · 反转与粗细", ChartFinancialTransformKind.Kagi, ChartFinancialStyle.Kagi),
            ("Point & Figure · X / O 列", ChartFinancialTransformKind.PointAndFigure, ChartFinancialStyle.PointAndFigure),
        ];
        var charts = definitions.Select((definition, index) =>
        {
            var host = new FinancialChart { Name = "FinancialExample" + index, Height = 260 };
            host.Chart.Title = definition.title; host.Chart.ShowLegend = false; host.Chart.AnimationTriggers = ChartAnimationTriggers.None;
            host.Chart.IsZoomEnabled = true; host.Chart.IsPanEnabled = true; host.Chart.SelectionMode = ChartSelectionMode.Single;
            host.PriceSeries.Style = definition.style; host.PriceSeries.Transform = definition.transform;
            host.PriceSeries.StrokeThickness = 1.7; host.PriceSeries.ReversalBoxes = definition.transform == ChartFinancialTransformKind.PointAndFigure ? 3 : 2;
            host.TradingAxis.LabelMode = ChartAxisLabelMode.Stagger;
            if (definition.transform == ChartFinancialTransformKind.PointAndFigure) host.PriceSeries.WidthRatio = .2;
            host.PriceSeries.BoxSize = 2; host.PriceSeries.ReversalAmount = 3;
            host.PriceAxis.Minimum = 85; host.PriceAxis.Maximum = 120;
            host.VolumeAxis.Maximum = 10000; host.VolumeAxis.IsVisible = index == 0;
            return host;
        }).ToArray();
        var status = new TextBlock { Name = "FinancialSummary", TextWrapping = TextWrapping.Wrap };
        var boxSize = new ComboBox { ItemsSource = new[] { 1d, 2d, 4d }, SelectedIndex = 1, Width = 70 };
        var reversal = new ComboBox { ItemsSource = new[] { 2d, 3d, 5d }, SelectedIndex = 1, Width = 70 };
        var overlayOption = new CheckBox { Content = "显示日期对齐叠加线", IsChecked = true };
        var overlay = new LineSeries { Title = "收盘 + 1", Stroke = Orange, StrokeThickness = 1.7, ShowMarkers = false };
        charts[0].Series.Add(overlay);
        var revision = 0; var count = 36;
        void Refresh()
        {
            var rows = FinancialDemoRows(count, revision);
            foreach (var (host, index) in charts.Select((host,index) => (host,index)))
            {
                host.PriceSeries.BoxSize = (double)(boxSize.SelectedItem ?? 2d);
                host.PriceSeries.ReversalAmount = (double)(reversal.SelectedItem ?? 3d);
                host.SetData(rows);
                if (index != 0) host.VolumeSeries.IsVisible = false;
            }
            overlay.ItemsSource = charts[0].ProjectOverlay(rows.Select(row => new ChartPoint(row.Time.ToUnixTimeMilliseconds(), row.Close + 1)));
            overlay.IsVisible = overlayOption.IsChecked == true;
            status.Text = $"{rows.Length} 个交易周期 · {rows[0].Time:yyyy-MM-dd} 至 {rows[^1].Time:yyyy-MM-dd} · 周末不占横槽\n" +
                $"原始 / HA / Renko / Kagi / P&F 图元：{charts[0].Samples.Count} / {charts[3].Samples.Count} / {charts[4].Samples.Count} / {charts[5].Samples.Count} / {charts[6].Samples.Count}";
        }
        boxSize.SelectionChanged += (_, _) => Refresh(); reversal.SelectionChanged += (_, _) => Refresh();
        overlayOption.IsCheckedChanged += (_, _) => overlay.IsVisible = overlayOption.IsChecked == true;
        var actions = new WrapPanel { ItemSpacing = 10, LineSpacing = 6 };
        AddAction(actions, "切换示例价格", () => { revision++; Refresh(); }, "替换价格快照并重新计算变换；日期槽与成交量保持对齐。");
        AddAction(actions, "追加交易日", () => { count++; Refresh(); }, "跳过周末，追加一个示例交易周期。");
        actions.Children.Add(new TextBlock { Text = "砖 / 箱宽", VerticalAlignment = VerticalAlignment.Center }); actions.Children.Add(boxSize);
        actions.Children.Add(new TextBlock { Text = "Kagi 反转价差", VerticalAlignment = VerticalAlignment.Center }); actions.Children.Add(reversal);
        actions.Children.Add(overlayOption);
        AddAction(actions, "恢复视图", () => { foreach (var host in charts) host.Chart.ResetViewport(); }, "恢复七张图的完整交易槽范围。");
        var grid = new Grid { ColumnDefinitions = new("*,*,*"), RowDefinitions = new("Auto,Auto,Auto"), ColumnSpacing = 12, RowSpacing = 12 };
        for (var i = 0; i < charts.Length; i++) { Grid.SetColumn(charts[i], i % 3); Grid.SetRow(charts[i], i / 3); grid.Children.Add(charts[i]); }
        Refresh();
        return new StackPanel { Spacing = 10, Children =
        {
            new TextBlock { Text = "交易日期等距排列，蜡烛主图以独立右轴显示成交量。Heikin-Ashi 使用递推 OHLC；Renko / Kagi / X-O 列按收盘价和固定阈值生成。悬停或选择可查看显示值与真实来源区间。价格变换不重复绘制来源成交量。", TextWrapping = TextWrapping.Wrap },
            actions, status, grid,
        } };
    }

    private static ChartFinancialPoint[] FinancialDemoRows(int count, int revision)
    {
        var rows = new ChartFinancialPoint[count]; var date = new DateTimeOffset(2026,9,1,9,30,0,TimeSpan.Zero); var previous = 100d;
        for (var i = 0; i < count; i++)
        {
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
            var close = 100 + Math.Sin((i + revision) * .65) * 8 + i * .15;
            rows[i] = new(date, previous, Math.Max(previous,close)+2, Math.Min(previous,close)-2, close, 1000+i*71%500);
            previous = close; date = date.AddDays(1);
        }
        return rows;
    }
}
