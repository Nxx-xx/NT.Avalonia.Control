using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _technicalEvidence = [];
    private async Task RunTechnicalTrendsSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var label = theme == ThemeVariant.Dark ? "dark" : "light";
            var background = Brush(label == "dark" ? "#111722" : "#FFFFFF");
            string Image(string kind, string state) => Path.Combine(Path.GetDirectoryName(baseImage)!, Path.GetFileNameWithoutExtension(baseImage) + $"-technical-{label}-{kind}-{state}.png");
            void Check(string name, bool value) => checks[$"TECHNICAL-{label}-{name}"] = value;
            foreach (var kind in new[] { "sma", "ema", "wma", "tma", "bollinger", "linear", "polynomial", "exponential", "logarithmic", "power", "trendma" })
            {
                var input = TechnicalProbePoints(kind);
                var source = new LineSeries { ItemsSource = input };
                var isWindow = kind is "sma" or "ema" or "wma" or "tma" or "bollinger" or "trendma";
                var xMinimum = isWindow || kind is not ("logarithmic" or "power") ? -.5 : .5;
                var xMaximum = isWindow ? 8.5 : kind is "logarithmic" or "power" ? 7.5 : 6.5;
                var yMinimum = isWindow ? -2 : 0;
                var yMaximum = isWindow ? 16 : kind == "power" ? 12 : 10;
                ChartAnalyticalSeries series;
                if (kind == "bollinger")
                {
                    var bands = new BollingerBandsIndicator { SourceSeries = source, Period = 3 };
                    bands.UpperSeries.Stroke = bands.LowerSeries.Stroke = Brush("#C5394D");
                    bands.UpperSeries.StrokeThickness = bands.LowerSeries.StrokeThickness = 3;
                    bands.BandSeries.IsVisible = false;
                    series = bands;
                }
                else if (kind is "sma" or "ema" or "wma" or "tma") series = new MovingAverageSeries
                {
                    SourceSeries = source, Period = 3, Type = kind switch
                    { "ema" => ChartMovingAverageType.Exponential, "wma" => ChartMovingAverageType.Weighted, "tma" => ChartMovingAverageType.Triangular, _ => ChartMovingAverageType.Simple },
                };
                else series = new ChartTrendlineSeries
                {
                    SourceSeries = source, Type = kind switch
                    {
                        "polynomial" => ChartTrendlineType.Polynomial, "exponential" => ChartTrendlineType.Exponential,
                        "logarithmic" => ChartTrendlineType.Logarithmic, "power" => ChartTrendlineType.Power,
                        "trendma" => ChartTrendlineType.MovingAverage, _ => ChartTrendlineType.Linear,
                    },
                    Period = 3, PolynomialDegree = 2, SampleCount = 129,
                };
                series.Stroke = Brush("#155CC1"); series.StrokeThickness = 3; series.ShowMarkers = false;
                var chart = new CartesianChart
                {
                    Width = 420, Height = 280, Padding = new Thickness(40, 20, 20, 30), ShowLegend = false,
                    Background = background, AnimationTriggers = ChartAnimationTriggers.None, IsTooltipEnabled = false,
                    SelectionMode = ChartSelectionMode.Single, XAxis = FinancialProbeAxis(xMinimum, xMaximum), YAxis = FinancialProbeAxis(yMinimum, yMaximum),
                };
                if (series is BollingerBandsIndicator indicator) chart.TechnicalIndicators.Add(indicator); else chart.Series.Add(series);
                var window = new Window { Title = "Technical probe · " + kind, Width = 420, Height = 280, CanResize = false, RequestedThemeVariant = theme, Background = background, Content = chart };
                try
                {
                    window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window);
                    Check(kind + "-native-window", window.TryGetPlatformHandle() is { Handle: not 0 });
                    Check(kind + "-fixed-independent-plot", chart.PlotArea == new Rect(40, 24, 354, 226));
                    var snapshot = series.CreateSnapshot();
                    Check(kind + "-expected-output-count", snapshot.Points.Count == (isWindow ? 9 : 129));
                    Check(kind + "-provenance", isWindow
                        ? snapshot.Points[2].Analysis is { IsSynthetic: false, SourceIndex: 2 } && snapshot.Points[2].RawPoint == input[2]
                        : snapshot.Points.All(point => point.Analysis is { IsSynthetic: true, SourceIndex: null } && point.Item is null));
                    var path = Image(kind, "native"); CaptureControlTemplateRaster(chart, path);
                    var exports = new List<string>();
                    if (kind == "bollinger")
                    {
                        var exported = Image(kind, "export"); exports.Add(exported);
                        Check(kind + "-export96", chart.TryExportPng(exported, new ChartExportOptions { Dpi = 96, PixelSize = new PixelSize(420, 280), Background = background }));
                    }
                    if (kind == "sma" && label == "light") foreach (var dpi in new[] { 144, 192 })
                    {
                        var exported = Image(kind, "dpi" + dpi); exports.Add(exported);
                        Check(kind + "-export" + dpi, chart.TryExportPng(exported, new ChartExportOptions { Dpi = dpi, PixelSize = new PixelSize(420 * dpi / 96, 280 * dpi / 96), Background = background }));
                    }
                    var computations = series.ComputationCount;
                    chart.Viewport = new ChartViewport(isWindow ? 3 : 2, xMaximum, yMinimum, yMaximum); FlushControlTemplateWindow(window);
                    Check(kind + "-viewport-reuses-full-history", series.ComputationCount == computations);
                    chart.Viewport = null;
                    _technicalEvidence.Add(new
                    {
                        kind, theme = label, native = path, exports, input, observedPlot = chart.PlotArea,
                        xMinimum, xMaximum, yMinimum, yMaximum, sourceCount = input.Length,
                        display = snapshot.Points.Select(TechnicalProbePoint).ToArray(),
                        upper = (series as BollingerBandsIndicator)?.UpperSeries.CreateSnapshot().Points.Select(TechnicalProbePoint).ToArray(),
                        lower = (series as BollingerBandsIndicator)?.LowerSeries.CreateSnapshot().Points.Select(TechnicalProbePoint).ToArray(),
                        fitCount = (series as ChartTrendlineSeries)?.FitResult?.Count,
                        omittedCount = (series as ChartTrendlineSeries)?.FitResult?.OmittedCount,
                        rSquared = (series as ChartTrendlineSeries)?.FitResult?.RSquared,
                        computations, physicalInputValidated = false, performanceMeasured = false,
                    });
                }
                finally { window.Close(); }
            }

            var values = new ObservableCollection<ChartPoint>(Enumerable.Range(0, 9).Select(i => new ChartPoint(i, i + 1)));
            var historySource = new LineSeries { ItemsSource = values };
            var historyAverage = new MovingAverageSeries { SourceSeries = historySource, Type = ChartMovingAverageType.Exponential, Period = 3, Stroke = Orange };
            var history = TechnicalDemoChart("Full-history regression"); history.Series.Add(historySource); history.Series.Add(historyAverage);
            var historyWindow = new Window { Width = 600, Height = 350, Content = history, RequestedThemeVariant = theme };
            try
            {
                historyWindow.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(historyWindow);
                var old = historyAverage.CreateSnapshot(); var count = historyAverage.ComputationCount;
                Check("history-preheat-and-endpoint", old.Points.Take(2).All(point => point.IsEmpty) && Math.Abs(old.Points[8].Point.Y - 8) < 1e-10);
                history.Viewport = new ChartViewport(6, 8, 0, 12); FlushControlTemplateWindow(historyWindow);
                Check("history-zoom-preserves-source-and-calculation", historyAverage.ComputationCount == count && Math.Abs(historyAverage.CreateSnapshot().Points[6].Point.Y - 6) < 1e-10);
                values.Add(new(9, 10)); FlushControlTemplateWindow(historyWindow);
                Check("history-append-updates-and-retains-old-snapshot", historyAverage.CreateSnapshot().Points.Count == 10 && Math.Abs(historyAverage.CreateSnapshot().Points[9].Point.Y - 9) < 1e-10 && old.Points.Count == 9);
                values.Add(new(10, double.NaN)); values.Add(new(11, 20)); values.Add(new(12, 22)); values.Add(new(13, 24)); FlushControlTemplateWindow(historyWindow);
                var gap = historyAverage.CreateSnapshot();
                Check("history-gap-restarts-seed", gap.Points.Skip(10).Take(3).All(point => point.IsEmpty) && Math.Abs(gap.Points[13].Point.Y - 22) < 1e-10);
                history.Series.Remove(historySource); FlushControlTemplateWindow(historyWindow);
                Check("source-reference-outlives-source-membership", historyAverage.CreateSnapshot().Points.Count == 14);
                historyWindow.Content = null; FlushControlTemplateWindow(historyWindow); values.Add(new(14, 26));
                historyWindow.Content = history; FlushControlTemplateWindow(historyWindow);
                Check("reattach-reads-latest-full-source", Math.Abs(historyAverage.CreateSnapshot().Points[14].Point.Y - 24) < 1e-10);
                _technicalEvidence.Add(new { kind = "history", theme = label, input = values.ToArray(), display = historyAverage.CreateSnapshot().Points.Select(TechnicalProbePoint).ToArray(), physicalInputValidated = false, performanceMeasured = false });
            }
            finally { historyWindow.Close(); }

            var board = CreateTechnicalTrendsBoard();
            var overview = new Window { Title = "Charts · 技术指标与趋势", Width = 1440, Height = 1120, RequestedThemeVariant = theme, Background = background, Content = new Border { Padding = new Thickness(16), Child = board } };
            try
            {
                overview.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(overview);
                Check("six-demo-panels", board.GetVisualDescendants().OfType<CartesianChart>().Count() == 6);
                var financial = board.GetVisualDescendants().OfType<FinancialChart>().Single();
                Check("financial-overlay-inherits-price-and-trading-slots", financial.Series.OfType<MovingAverageSeries>().Single().CreateSnapshot().Points.Select(point => point.Point.X).SequenceEqual(financial.Samples.Select(sample => (double)sample.Slot)));
                var path = Image("board", "overview");
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(overview.ClientSize.Width), (int)Math.Ceiling(overview.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(overview); bitmap.Save(path);
                _technicalEvidence.Add(new { kind = "board", theme = label, image = path, physicalInputValidated = false, performanceMeasured = false });
            }
            finally { overview.Close(); }
        }
    }

    private static object TechnicalProbePoint(ChartDataPoint point) => new
    {
        point.Point, point.SourceIndex, point.RawPoint, point.IsEmpty,
        analysis = point.Analysis is { } analysis ? new { analysis.IsSynthetic, analysis.SourceIndex, analysis.CalculatedPoint } : null,
    };
    private static ChartPoint[] TechnicalProbePoints(string kind)
    {
        if (kind is "sma" or "ema" or "wma" or "tma" or "bollinger" or "trendma")
            return new double[] { 2, 4, 1, 7, 3, 9, 5, 11, 6 }.Select((y, x) => new ChartPoint(x, y)).ToArray();
        return Enumerable.Range(kind is "logarithmic" or "power" ? 1 : 0, 7).Select(x => new ChartPoint(x, kind switch
        {
            "polynomial" => 2 + .2 * (x - 3) * (x - 3), "exponential" => 2 * Math.Exp(.2 * x),
            "logarithmic" => 2 + 2 * Math.Log(x), "power" => .5 * Math.Pow(x, 1.5), _ => 2 + x,
        })).ToArray();
    }
}
