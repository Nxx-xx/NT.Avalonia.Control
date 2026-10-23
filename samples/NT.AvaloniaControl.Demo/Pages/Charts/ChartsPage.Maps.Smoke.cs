using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private async Task RunMapsSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var themeName = theme == ThemeVariant.Dark ? "dark" : "light"; var background = Brush(themeName == "dark" ? "#111722" : "#FFFFFF");
            string Image(string kind, string phase) => Path.Combine(directory, $"{stem}-maps-{themeName}-{kind}-{phase}.png");
            void Check(string name, bool value) => checks[$"MAPS-{themeName}-{name}"] = value;
            foreach (var kind in MapKinds)
            {
                var values = new[] { new MapDemoValue("a", "Alpha", 1), new MapDemoValue("b", "Beta", 4), new MapDemoValue("missing", "Unmatched", 9) };
                var points = new[] { new MapDemoObservation("p-a", "Point A", -25, 1, 1), new MapDemoObservation("p-b", "Point B", 25, 15, 4) };
                ShapeMap chart = kind == "seat" ? new SeatMap() : new ShapeMap();
                chart.GeoJson = MapFixtureJson; chart.Width = 420; chart.Height = 280; chart.ShowLabels = chart.ShowLegend = chart.IsTooltipEnabled = false; chart.Background = background;
                var shape = new ShapeLayer { MinimumColor = Color.Parse("#2563EB"), MaximumColor = Color.Parse("#F97316"), NoDataBrush = Brush("#D5DCE2"), Stroke = null };
                chart.Layers.Add(shape);
                if (kind is "bubble" or "heatmap")
                {
                    if (kind == "bubble") chart.Layers.Add(new BubbleLayer { ItemsSource = points, MinimumRadius = 0, MaximumRadius = .08, Fill = Brush("#2563EB"), Stroke = null });
                    else chart.Layers.Add(new HeatmapLayer { ItemsSource = points, Columns = 6, Rows = 4, Radius = 0, MinimumColor = Color.Parse("#2563EB"), MaximumColor = Color.Parse("#F97316"), Stroke = null });
                }
                else shape.ItemsSource = values;
                var window = new Window { Width = 420, Height = 280, CanResize = false, RequestedThemeVariant = theme, Background = background, Content = chart, Title = "Maps · " + kind };
                try
                {
                    window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window);
                    Check(kind + "-native-window", window.TryGetPlatformHandle() is { Handle: not 0 });
                    Check(kind + "-fixed-plot", chart.PlotArea == new Rect(24, 24, 372, 232));
                    Check(kind + "-valid-layered-scene", chart.DataError is null && chart.Snapshot?.Features.Count == 2 && chart.GetMarks().Count == 2);
                    Check(kind + "-shared-aspect-fit", chart.MapViewport is { } viewport && Math.Abs(viewport.FittedArea.X - 36) < 1e-8 && Math.Abs(viewport.FittedArea.Width - 348) < 1e-8);
                    Check(kind + "-unknown-region-diagnostic", kind is "bubble" or "heatmap" ? chart.UnknownRegions.Count == 0 : chart.UnknownRegions.Count == 1 && ReferenceEquals(chart.UnknownRegions[0].Source, values[2]));
                    var nativePath = Image(kind, "native");
                    using (var raster = new RenderTargetBitmap(new PixelSize(420, 280), new Vector(96, 96))) { raster.Render(chart); raster.Save(nativePath); }
                    var exportPath = Image(kind, "export");
                    Check(kind + "-export96", chart.TryExportPng(exportPath, new ChartExportOptions { PixelSize = new PixelSize(420, 280), Dpi = 96, Background = background }));
                    var dpiImages = new List<string>();
                    if (themeName == "light" && kind is "shape" or "bubble")
                    {
                        var dpi = kind == "shape" ? 144 : 192; var path = Image(kind, "dpi" + dpi); dpiImages.Add(path);
                        Check(kind + "-dpi-export", chart.TryExportPng(path, new ChartExportOptions { PixelSize = new PixelSize(420 * dpi / 96, 280 * dpi / 96), Dpi = dpi, Background = background }));
                    }
                    var marks = chart.GetMarks(); var selections = new List<object>();
                    for (var i = 0; i < marks.Count; i++)
                    {
                        chart.SelectedIndex = i; FlushControlTemplateWindow(window);
                        Check(kind + "-selection-original-" + i, chart.SelectedPoint?.Index == i && chart.SelectedPoint.Members.Zip(marks[i].Members).All(pair => ReferenceEquals(pair.First.Item, pair.Second.Item)));
                        selections.Add(new { index = i, selected = MapHitProbe(chart, chart.SelectedPoint) });
                    }
                    var targets = kind == "bubble" ? new[] { new Point(65, 134.2), new Point(355, 53) }
                        : kind == "heatmap" ? new[] { new Point(65, 111), new Point(355, 53) } : new[] { new Point(65, 140), new Point(355, 53) };
                    var hits = targets.Select(target => new { target = new { x = target.X, y = target.Y }, hit = MapHitProbe(chart, chart.HitTest(target)) }).ToArray();
                    for (var i = 0; i < targets.Length; i++) Check(kind + "-independent-hit-" + i, chart.HitTest(targets[i])?.Members.Any(member => ReferenceEquals(member.Item, kind is "bubble" or "heatmap" ? points[i] : values[i])) == true);
                    var hole = new Point(152, 140); var blank = new Point(410, 10);
                    Check(kind + "-hole-not-data", chart.HitTest(hole) is null); Check(kind + "-outside-plot-not-data", chart.HitTest(blank) is null);
                    if (kind is not ("bubble" or "heatmap")) Check(kind + "-multipolygon-is-one-business-row", ReferenceEquals(chart.HitTest(new(355, 227))?.Members[0].Item, values[1]));
                    AddMapProbe(kind, themeName, "baseline", chart, values, points, new { selections, hits, hole = new { x = hole.X, y = hole.Y, hit = MapHitProbe(chart, chart.HitTest(hole)) }, blank = new { x = blank.X, y = blank.Y, hit = MapHitProbe(chart, chart.HitTest(blank)) } }, nativePath, exportPath, dpiImages.ToArray());
                    chart.SelectedIndex = -1; chart.Zoom = 1.25; chart.PanX = .03; chart.PanY = -.02; FlushControlTemplateWindow(window);
                    Check(kind + "-viewport-original-identity", chart.GetMarks().SelectMany(mark => mark.Members).All(member => marks.SelectMany(mark => mark.Members).Any(old => ReferenceEquals(old.Item, member.Item))));
                    AddMapProbe(kind, themeName, "viewport", chart, values, points); chart.ResetViewport(); FlushControlTemplateWindow(window);
                    if (kind is "bubble" or "heatmap") points[0].Value = 2; else values[0].Value = 2;
                    FlushControlTemplateWindow(window); Check(kind + "-inpc-update", chart.DataError is null && chart.GetMarks().Count == 2);
                    AddMapProbe(kind, themeName, "updated", chart, values, points);
                    if (kind is "bubble" or "heatmap") points[0].Value = -1; else values[0].Value = double.NaN;
                    FlushControlTemplateWindow(window); Check(kind + "-invalid-data-clears", chart.GetMarks().Count == 0 && chart.DataError is not null);
                    if (kind is "bubble" or "heatmap") points[0].Value = 1; else values[0].Value = 1;
                    FlushControlTemplateWindow(window); Check(kind + "-repaired-source-recovers", chart.GetMarks().Count == 2 && chart.DataError is null);
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Home });
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Space }); FlushControlTemplateWindow(window);
                    Check(kind + "-keyboard-original-selection", chart.SelectedPoint is { Members.Count: > 0 });
                    await RunMapSourceSmokeAsync(chart, window, kind, themeName, directory, Check);
                }
                finally { window.Close(); chart.Dispose(); }
            }
            var board = CreateMapBoard(true);
            var host = new Window { Width = 1440, Height = 900, Title = "Charts · 地理图", Content = new Border { Padding = new Thickness(16), Child = board }, RequestedThemeVariant = theme, Background = background };
            ShapeMap[] charts = [];
            try
            {
                host.Show(_hostWindow); await Task.Delay(60); FlushControlTemplateWindow(host); charts = board.GetVisualDescendants().OfType<ShapeMap>().ToArray();
                Check("five-demo-panels", charts.Length == 5 && charts.All(chart => chart.DataError is null && chart.GetMarks().Count > 0));
                Check("offline-all-world-features", charts.Take(3).All(chart => chart.Snapshot?.Features.Count == 177));
                var path = Image("board", "overview"); using var raster = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(host.ClientSize.Width), (int)Math.Ceiling(host.ClientSize.Height)), new Vector(96, 96)); raster.Render(host); raster.Save(path);
                _mapProbes.Add(new { kind = "board", theme = themeName, phase = "board", image = path, worldFeatureCount = charts.FirstOrDefault()?.Snapshot?.Features.Count, physicalInputValidated = false, performanceMeasured = false });
            }
            finally { host.Close(); foreach (var chart in charts) chart.Dispose(); }
        }
    }
    private async Task RunMapSourceSmokeAsync(ShapeMap chart, Window window, string kind, string theme, string directory, Action<string, bool> check)
    {
        var path = Path.Combine(directory, $"maps-source-{theme}-{kind}.geojson"); File.WriteAllText(path, MapFixtureJson);
        chart.GeoJson = null; chart.SourceUri = new Uri(Path.GetFullPath(path));
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); var preCanceled = false;
        try { await chart.LoadSourceAsync(canceled.Token); } catch (OperationCanceledException error) { preCanceled = error.CancellationToken == canceled.Token; }
        var obsolete = chart.LoadSourceAsync(); var latest = chart.LoadSourceAsync(); var obsoleteAccepted = await obsolete; var latestAccepted = await latest;
        FlushControlTemplateWindow(window);
        check(kind + "-latest-local-load-only", preCanceled && !obsoleteAccepted && latestAccepted && chart.SourceState == MapSourceState.Completed && chart.Snapshot?.Features.Count == 2);
        var pending = chart.LoadSourceAsync(); chart.CancelSourceLoad(); var canceledAccepted = await pending;
        check(kind + "-cancel-source-no-publication", !canceledAccepted && chart.SourceState == MapSourceState.Canceled);
        chart.SourceUri = new Uri(Path.GetFullPath(path + ".missing")); var failedAccepted = await chart.LoadSourceAsync(); FlushControlTemplateWindow(window);
        check(kind + "-missing-file-clears-old-map", !failedAccepted && chart.SourceState == MapSourceState.Failed && chart.SourceError is not null && chart.GetMarks().Count == 0);
        chart.SourceUri = new Uri(Path.GetFullPath(path)); var recovered = await chart.LoadSourceAsync(); FlushControlTemplateWindow(window);
        check(kind + "-local-source-recovery", recovered && chart.Snapshot?.Features.Count == 2 && chart.DataError is null);
        var detached = chart.LoadSourceAsync(); window.Content = null; var detachedAccepted = await detached;
        check(kind + "-detach-cancels-source", !detachedAccepted && chart.SourceState == MapSourceState.Canceled);
        window.Content = chart; var reattached = await chart.LoadSourceAsync(); FlushControlTemplateWindow(window);
        check(kind + "-reattach-original-layers", reattached && chart.GetMarks().Count == 2);
        _mapAsyncEvidence.Add(new { kind, theme, preCanceled, obsoleteAccepted, latestAccepted, canceledAccepted, failedAccepted, recovered, detachedAccepted, reattached,
            finalState = chart.SourceState.ToString(), featureCount = chart.Snapshot?.Features.Count, physicalInputValidated = false, performanceMeasured = false });
    }
}
