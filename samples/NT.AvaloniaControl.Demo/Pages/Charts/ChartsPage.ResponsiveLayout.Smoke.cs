using System.Diagnostics;
using Avalonia;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _responsiveLayoutProbes = [];

    private static object ResponsiveBounds(Rect bounds) => new { x = bounds.X, y = bounds.Y, width = bounds.Width, height = bounds.Height };

    private void CaptureResponsiveLayout(string phase, double requestedWidth, bool configured)
    {
        _responsiveLayoutProbes.Add(new
        {
            phase, requestedWidth, widthProperty = _hostWindow.Width,
            clientSize = new { width = _hostWindow.ClientSize.Width, height = _hostWindow.ClientSize.Height },
            pageBounds = ResponsiveBounds(Bounds), scrollBounds = ResponsiveBounds(_scroll.Bounds), gridBounds = ResponsiveBounds(_chartGrid.Bounds),
            cards = new[] { _lineCard, _barCard, _scatterCard }.Select((card, index) => new { index, bounds = ResponsiveBounds(card.Bounds) }).ToArray(),
            charts = AllCharts.Select(chart => new { chart.Name, bounds = ResponsiveBounds(chart.Bounds) }).ToArray(),
            threeColumns = _threeColumns, columnCount = _chartGrid.ColumnDefinitions.Count, rowCount = _chartGrid.RowDefinitions.Count,
            configured, windowState = _hostWindow.WindowState.ToString(), renderScaling = _hostWindow.RenderScaling,
            nativeHandleCreated = _hostWindow.TryGetPlatformHandle() is { Handle: not 0 },
            syntheticWindowResize = true, physicalInputValidated = false, performanceMeasured = false,
        });
    }

    private async Task<bool> WaitResponsiveLayoutAsync(Func<bool> configured)
    {
        // X11/WSLg native configure notifications arrive asynchronously. Wait for
        // the observed size and a following layout pass, rather than a fixed delay.
        var timeout = Stopwatch.StartNew(); var consecutive = 0;
        do
        {
            FlushControlTemplateWindow(_hostWindow);
            consecutive = configured() ? consecutive + 1 : 0;
            if (consecutive >= 2) return true;
            await Task.Delay(20);
        }
        while (timeout.Elapsed < TimeSpan.FromSeconds(4));
        FlushControlTemplateWindow(_hostWindow);
        return configured();
    }

    private bool ResponsiveVertical() => !_threeColumns && _barCard.Bounds.Top > _lineCard.Bounds.Top &&
        _scatterCard.Bounds.Top > _barCard.Bounds.Top && AllCharts.All(chart => chart.Bounds.Width > 250);

    private bool ResponsiveThreeColumns() => _threeColumns && _barCard.Bounds.Left > _lineCard.Bounds.Left &&
        _scatterCard.Bounds.Left > _barCard.Bounds.Left && AllCharts.All(chart => chart.Bounds.Width > 250);

    private async Task RunResponsiveLayoutSmokeAsync(Dictionary<string, bool> checks)
    {
        var ready = await WaitResponsiveLayoutAsync(() => Bounds.Width > 0 && _hostWindow.ClientSize.Width > 0 && _lineCard.Bounds.Height > 0);
        checks["响应式初始布局已稳定"] = ready;
        var originalWidth = _hostWindow.Width; var originalPageWidth = Bounds.Width; var originalClientWidth = _hostWindow.ClientSize.Width;
        CaptureResponsiveLayout("initial", originalWidth, ready);
        if (originalPageWidth >= 1050) checks["响应式初始宽窗口三列"] = ResponsiveThreeColumns();
        else checks["响应式初始窄窗口纵向"] = ResponsiveVertical();
        try
        {
            // The gallery no longer has a sidebar to subtract from 1080 DIPs.
            // Use a window size unambiguously below the unchanged 1050-DIP page threshold.
            _hostWindow.Width = 960;
            var narrowConfigured = await WaitResponsiveLayoutAsync(() => _hostWindow.ClientSize.Width is > 0 and < 1050 && Bounds.Width is > 0 and < 1050);
            CaptureResponsiveLayout("narrow", 960, narrowConfigured);
            checks["响应式窄窗口原生尺寸已应用"] = narrowConfigured;
            checks["窄窗口纵向排列"] = narrowConfigured && ResponsiveVertical();
        }
        finally
        {
            _hostWindow.Width = originalWidth;
            var restored = await WaitResponsiveLayoutAsync(() => Math.Abs(Bounds.Width - originalPageWidth) <= 1 &&
                Math.Abs(_hostWindow.ClientSize.Width - originalClientWidth) <= 1);
            CaptureResponsiveLayout("restored", originalWidth, restored);
            checks["响应式原窗口尺寸已恢复"] = restored;
            checks[originalPageWidth >= 1050 ? "宽窗口三列排列" : "恢复原窄窗口纵向排列"] = restored &&
                (originalPageWidth >= 1050 ? ResponsiveThreeColumns() : ResponsiveVertical());
        }
    }
}
