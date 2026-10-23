using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _multiChartEvidence = [];
    private async Task RunMultiChartSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var label = theme == ThemeVariant.Dark ? "dark" : "light";
            void Check(string name, bool value) => checks[$"MULTICHART-{label}-{name}"] = value;
            string Image(string name) => Path.Combine(Path.GetDirectoryName(baseImage)!, Path.GetFileNameWithoutExtension(baseImage) + $"-multichart-{label}-{name}.png");
            using var board = CreateMultiChartBoard(); var chart = board.Charts[0];
            var window = new Window { Title = "Charts · 多图与后台布局", Width = 1280, Height = 1000, RequestedThemeVariant = theme,
                Background = Brush(label == "dark" ? "#111722" : "#F8FAFC"), Foreground = Brush(label == "dark" ? "#EAF0FB" : "#172238"), Content = new Border { Padding = new Thickness(20), Child = board.View } };
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window); await board.RefreshAsync(); FlushControlTemplateWindow(window);
                var initial = board.Charts.Select(item => item.GetBackgroundLayoutInfo()).ToArray(); var schedule = board.Charts.Select(item => item.GetWorkSchedulingInfo()).ToArray();
                Check("six-native-charts-attached-and-active", window.TryGetPlatformHandle() is { Handle: not 0 } && board.Charts.All(item => item.PlotArea.Width > 250 && item.Bounds.Height >= 240) && schedule.All(item => item.IsActive));
                Check("six-numeric-layouts-published-without-failure", initial.All(item => !item.IsBusy && item.CompletedCount > 0 && item.FailedCount == 0 && item.PublishedVertexCount > 0));
                Check("worker-is-distinct-from-ui-thread", initial.All(item => item.LastWorkerThreadId > 0 && item.LastWorkerThreadId != Environment.CurrentManagedThreadId));
                var requestsBefore = board.Charts.Select(item => item.GetWorkSchedulingInfo()).ToArray();
                for (var q = 0; q < 20; q++) foreach (var item in board.Charts) item.RequestRender();
                var queued = board.Charts.Select(item => item.GetWorkSchedulingInfo()).ToArray();
                Check("repeated-refresh-is-coalesced-for-every-chart", queued.Select((item, i) => item.RefreshPending && item.RefreshRequests >= requestsBefore[i].RefreshRequests + 20 && item.CoalescedRefreshes >= requestsBefore[i].CoalescedRefreshes + 19).All(value => value));
                await Task.Delay(30); FlushControlTemplateWindow(window); var dispatched = board.Charts.Select(item => item.GetWorkSchedulingInfo()).ToArray();
                Check("every-chart-receives-fair-refresh-dispatch", dispatched.Select((item, i) => item.RefreshDispatches > requestsBefore[i].RefreshDispatches).All(value => value));
                var backgroundImage = Image("background"); var backgroundRaster = CaptureControlTemplateRaster(chart, backgroundImage);
                chart.UseBackgroundLayout = false; FlushControlTemplateWindow(window); var synchronousImage = Image("synchronous"); var synchronousRaster = CaptureControlTemplateRaster(chart, synchronousImage);
                var syncChanges = CountControlTemplateExactChanges(backgroundRaster, synchronousRaster, new Rect(0, 0, chart.Bounds.Width, chart.Bounds.Height));
                Check("background-and-synchronous-raster-exact", syncChanges == 0);
                chart.UseBackgroundLayout = true; await chart.PrepareBackgroundLayoutAsync(); await board.RetargetAsync(); FlushControlTemplateWindow(window);
                var retargeted = board.Charts.Select(item => item.GetBackgroundLayoutInfo()).ToArray();
                Check("latest-revision-is-three-and-all-layouts-finished", board.Revision == 3 && retargeted.All(item => !item.IsBusy && item.FailedCount == 0 && item.PublishedVertexCount > 0));
                Check("latest-raw-data-keeps-business-identity", board.Charts.Select((item, s) => item.Series[0].CreateSnapshot().Points.Select((point, q) => point.SourceIndex == q && point.Item is MultiChartRow row && row.Id == $"multi-{s}-v3-{q}" && Equals(point.StableId, row.Id) && point.Point == new ChartPoint(q, 15 + s * 3 + q % 37 * .8 + 12)).All(value => value)).All(value => value));
                var latestImage = Image("latest"); var latestRaster = CaptureControlTemplateRaster(chart, latestImage); chart.UseBackgroundLayout = false; FlushControlTemplateWindow(window);
                var latestSyncImage = Image("latest-synchronous"); var latestSyncRaster = CaptureControlTemplateRaster(chart, latestSyncImage);
                var latestChanges = CountControlTemplateExactChanges(latestRaster, latestSyncRaster, new Rect(0, 0, chart.Bounds.Width, chart.Bounds.Height));
                Check("latest-background-and-sync-raster-exact", latestChanges == 0); chart.UseBackgroundLayout = true; await chart.PrepareBackgroundLayoutAsync();
                board.Grid.IsVisible = false; FlushControlTemplateWindow(window); var hidden = board.Charts.Select(item => item.GetWorkSchedulingInfo()).ToArray(); var hiddenCompleted = board.Charts.Select(item => item.GetBackgroundLayoutInfo().CompletedCount).ToArray();
                foreach (var item in board.Charts) item.RequestRender(); var hiddenResults = await Task.WhenAll(board.Charts.Select(item => item.PrepareBackgroundLayoutAsync())); await Task.Delay(20); FlushControlTemplateWindow(window);
                Check("hidden-ancestor-deactivates-all-charts", hidden.All(item => !item.IsActive) && board.Charts.All(item => !item.GetWorkSchedulingInfo().RefreshPending));
                Check("hidden-does-not-publish-new-layout", hiddenResults.All(value => !value) && board.Charts.Select((item, i) => item.GetBackgroundLayoutInfo().CompletedCount == hiddenCompleted[i] && !item.GetBackgroundLayoutInfo().IsBusy).All(value => value));
                var hiddenImage = Image("hidden"); SaveWindow(hiddenImage);
                board.Grid.IsVisible = true; FlushControlTemplateWindow(window); await board.RefreshAsync(); FlushControlTemplateWindow(window);
                Check("show-restores-current-layout-and-scheduling", board.Charts.All(item => item.GetWorkSchedulingInfo().IsActive && !item.GetBackgroundLayoutInfo().IsBusy));
                board.View.Children.Remove(board.Grid); FlushControlTemplateWindow(window);
                Check("unload-deactivates-and-releases-local-cache", board.Charts.All(item => !item.GetWorkSchedulingInfo().IsActive && !item.GetBackgroundLayoutInfo().IsBusy && item.GetResourceCacheInfo().EstimatedBytes == 0));
                board.View.Children.Add(board.Grid); FlushControlTemplateWindow(window); await board.RefreshAsync(); FlushControlTemplateWindow(window);
                Check("reattach-publishes-current-version", board.Charts.All(item => item.GetWorkSchedulingInfo().IsActive && item.GetBackgroundLayoutInfo().PublishedVertexCount > 0));
                board.SetProcessBudget(1024 * 1024, 8 * 1024 * 1024, 8); await board.RefreshAsync(); var limited = ChartWorkScheduler.GetBudgetInfo();
                Check("shared-logical-cache-and-job-budgets-bound", limited.EstimatedResourceCacheBytes <= limited.ResourceCacheBudgetBytes && limited.EstimatedBackgroundPayloadBytes <= limited.BackgroundPayloadBudgetBytes && limited.ReservedBackgroundJobs <= limited.MaximumBackgroundJobs && !limited.NativeMemoryIncluded && !limited.SharedIndexCachesIncluded);
                board.RefreshStatus(); var overviewImage = Image("overview"); SaveWindow(overviewImage);
                var final = board.Charts.Select(item => new { scheduling = item.GetWorkSchedulingInfo(), layout = item.GetBackgroundLayoutInfo(), resources = item.GetResourceCacheInfo() }).ToArray();
                _multiChartEvidence.Add(new { theme = label, initial, schedule, requestsBefore, queued, dispatched, retargeted, hidden, hiddenResults, limited, final, backgroundImage, synchronousImage, latestImage, latestSyncImage, hiddenImage, overviewImage, syncChanges, latestChanges, revision = board.Revision, plot = chart.PlotArea, range = chart.VisibleRange, chartWidth = chart.Bounds.Width, chartHeight = chart.Bounds.Height,
                    originalRows = board.Charts.Select((item, s) => item.Series[0].CreateSnapshot().Points.Select(point => new { series = s, index = point.SourceIndex, id = point.StableId, x = point.Point.X, y = point.Point.Y }).ToArray()).ToArray(), physicalInputValidated = false, performanceMeasured = false, physicalDesktopQualified = false });
            }
            finally { window.Close(); }
            void SaveWindow(string path) { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path); }
        }
    }
}
