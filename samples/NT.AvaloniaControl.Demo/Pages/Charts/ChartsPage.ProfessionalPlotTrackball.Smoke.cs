using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _professionalPlotTrackballProbes = [];
    private static double PlotTrackDistance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static Control? ProfessionalPlotControl(ChartBase chart) => chart.GetVisualDescendants().OfType<Control>().SingleOrDefault(control => control.Name == "ProfessionalPlotContent");
    private static ChartDataPlotAreaContext? ProfessionalPlotContext(ChartBase chart) => ProfessionalPlotControl(chart)?.DataContext as ChartDataPlotAreaContext;
    private static Control[] ProfessionalTrackControls(ChartBase chart) => chart.GetVisualDescendants().OfType<Control>().Where(control => control.Name == "ProfessionalTrackballBody").ToArray();
    private static bool PlotTrackOriginal(ChartBase chart, ProfessionalPlotTrackballBoard board)
    {
        bool Original(ChartDataPoint point) => point.SourceIndex >= 0 && point.SourceIndex < board.Model.Rows.Count && ReferenceEquals(point.Item, board.Model.Rows[point.SourceIndex]) && Equals(point.StableId, board.Model.Rows[point.SourceIndex].Id);
        return ProfessionalTracks(chart).Count > 0 && ProfessionalTracks(chart).All(reading => reading.Members.Count > 0 && reading.Members.All(Original)) &&
            ProfessionalTrackControls(chart).All(control => control.DataContext is ChartDataTrackballContext context && ReferenceEquals(context.BusinessDataContext, board.Model));
    }
    private static object PlotTrackProbe(ChartBase chart, ProfessionalPlotTrackballBoard board) => new
    {
        chart.Name, kind = chart.GetType().Name, bounds = C7Bounds(chart.Bounds), plotBuilds = board.PlotBuilds[chart], trackballBuilds = board.TrackBuilds[chart], totalReadings = chart is EngineeringChartBase engineering ? engineering.TrackballTotalReadings : ((AnalysisChartBase)chart).TrackballTotalReadings,
        plot = ProfessionalPlotContext(chart) is { } plot ? new { plot.Content, originalBusinessContext = ReferenceEquals(plot.DataContext, board.Model), plotArea = C7Bounds(plot.PlotArea), visibleLayoutBounds = C7Bounds(plot.VisibleLayoutBounds),
            layoutCenter = C5Point(plot.ProjectLayout(.5, .5)), roundTripCenter = C5Point(plot.UnprojectLayout(plot.ProjectLayout(.5, .5))), engineeringMarks = plot.EngineeringMarks.Count, analysisMarks = plot.AnalysisMarks.Count } : null,
        readings = ProfessionalTracks(chart).Select(reading => new { reading.Text, reading.Value, position = C5Point(reading.Position), bounds = C7Bounds(reading.Bounds),
            engineering = reading.EngineeringHit is not null, analysis = reading.PointHit is not null,
            members = reading.Members.Select(member => new { member.SourceIndex, stableId = member.StableId?.ToString(), originalSourceReference = ReferenceEquals(member.Item, board.Model.Rows[member.SourceIndex]) }).ToArray() }).ToArray(),
        controls = ProfessionalTrackControls(chart).Select(control => new { bounds = C7Bounds(PresentationControlBounds(control, chart)), texts = control.GetVisualDescendants().OfType<TextBlock>().Select(text => new { text.Name, text.Text, foreground = C7Brush(text.Foreground) }).ToArray() }).ToArray(),
    };

    private async Task RunProfessionalPlotTrackballSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        var directory = Path.GetDirectoryName(baseImage)!; Directory.CreateDirectory(directory); var stem = Path.GetFileNameWithoutExtension(baseImage);
        string Image(string suffix, string extension = "png") => Path.Combine(directory, $"{stem}-professional-plot-trackball-{suffix}.{extension}");
        void Check(string name, bool value) => checks["professional-plot-trackball-" + name] = value;
        using var board = CreateProfessionalPlotTrackballBoard(); var container = new Border { Padding = new Thickness(18), Child = board.View };
        var window = new Window { Width = 1440, Height = 900, Title = "Charts · 专业绘图区与 Trackball", CanResize = false, Content = container, RequestedThemeVariant = ThemeVariant.Light };
        void Flush() { FlushControlTemplateWindow(window); FlushControlTemplateWindow(window); }
        void ReadAll() { foreach (var chart in board.Charts) { _ = RouteProfessionalReading(chart, window, 0); } Flush(); }
        try
        {
            window.Show(_hostWindow); await Task.Delay(50); Flush(); ReadAll();
            Check("native-four-family-layout", window.TryGetPlatformHandle() is { Handle: not 0 } && board.Charts.All(chart => chart.Bounds.Width > 400 && chart.Bounds.Height == 300));
            var modes = new[] { (Name: "normal-light", Theme: ThemeVariant.Light, Contrast: ChartHighContrastMode.None), (Name: "normal-dark", Theme: ThemeVariant.Dark, Contrast: ChartHighContrastMode.None),
                (Name: "hc-light", Theme: ThemeVariant.Light, Contrast: ChartHighContrastMode.Light), (Name: "hc-dark", Theme: ThemeVariant.Dark, Contrast: ChartHighContrastMode.Dark) };
            foreach (var mode in modes)
            {
                window.RequestedThemeVariant = mode.Theme; board.Contrast.SelectedItem = mode.Contrast; Flush(); ReadAll();
                foreach (var chart in board.Charts)
                {
                    var plot = ProfessionalPlotContext(chart); var body = ProfessionalPlotControl(chart); var tracks = ProfessionalTrackControls(chart);
                    Check(mode.Name + "-" + chart.Name + "-real-template-controls", plot is not null && body is not null && tracks.Length == 1 && tracks[0].DataContext is ChartDataTrackballContext);
                    Check(mode.Name + "-" + chart.Name + "-original-trackball-members", PlotTrackOriginal(chart, board));
                    Check(mode.Name + "-" + chart.Name + "-plot-context-original-business", plot is not null && ReferenceEquals(plot.DataContext, board.Model) && Equals(plot.Content, "永久绘图区 · 归一化中心"));
                    Check(mode.Name + "-" + chart.Name + "-plot-extent-and-layout-roundtrip", plot is not null && plot.PlotArea.Width > 0 && plot.PlotArea.Height > 0 &&
                        PlotTrackDistance(plot.UnprojectLayout(plot.ProjectLayout(.3, .7)), new Point(.3, .7)) < .000001 &&
                        PlotTrackDistance(plot.ToChart(plot.ToLocal(plot.PlotArea.Center)), plot.PlotArea.Center) < .000001);
                    Check(mode.Name + "-" + chart.Name + "-plot-original-family-marks", plot is not null && (chart is EngineeringChartBase ? plot.EngineeringMarks.Count > 0 && plot.AnalysisMarks.Count == 0 : plot.AnalysisMarks.Count > 0 && plot.EngineeringMarks.Count == 0));
                    Check(mode.Name + "-" + chart.Name + "-bounded-business-text", tracks.All(control => control.GetVisualDescendants().OfType<TextBlock>().Count() == 4 && !control.GetVisualDescendants().OfType<ItemsControl>().Any()) &&
                        tracks.SelectMany(control => control.GetVisualDescendants().OfType<TextBlock>()).Where(text => text.Name == "ProfessionalTrackballTitle").All(text => text.Text == board.Model.Title));
                    Check(mode.Name + "-" + chart.Name + "-theme-inherits-chart-foreground", body is not null && body.GetVisualDescendants().OfType<TextBlock>().Concat(tracks.SelectMany(control => control.GetVisualDescendants().OfType<TextBlock>())).All(text => C7Brush(text.Foreground) == C7Brush(chart.Foreground)));
                    Check(mode.Name + "-" + chart.Name + "-passive-plot-and-track-controls", body is not null && !body.IsEffectivelyEnabled == !chart.IsEffectivelyEnabled &&
                        chart.GetVisualDescendants().OfType<Control>().Where(control => control.Name is "PART_ChartDataPlotAreaContentOverlay" or "PART_ChartDataTrackballOverlay").Count(control => !control.IsHitTestVisible && !control.Focusable) == 2);
                    var oldPlot = body; var oldTracks = tracks; var builds = board.TrackBuilds[chart];
                    _ = RouteProfessionalReading(chart, window, 1); _ = RouteProfessionalReading(chart, window, 0); Flush();
                    Check(mode.Name + "-" + chart.Name + "-stable-controls-between-readings", ReferenceEquals(oldPlot, ProfessionalPlotControl(chart)) && oldTracks.SequenceEqual(ProfessionalTrackControls(chart)) && builds == board.TrackBuilds[chart]);
                }
                Check(mode.Name + "-mosaic-aggregate-is-not-truncated", ProfessionalTracks(board.Mosaic).Any(reading => reading.Members.Select(member => member.SourceIndex).SequenceEqual(new[] { 0, 2 }) && reading.EngineeringHit is not null));
                var image = Image(mode.Name); using (var bitmap = new RenderTargetBitmap(new PixelSize(1440, 900), new Vector(96, 96))) { bitmap.Render(window); bitmap.Save(image); }
                _professionalPlotTrackballProbes.Add(new { schema = "professional-plot-trackball-native/v1", phase = "theme", mode = mode.Name, theme = mode.Theme.ToString(), contrast = mode.Contrast.ToString(), image,
                    nativeHandleCreated = window.TryGetPlatformHandle() is { Handle: not 0 }, renderScaling = window.RenderScaling, charts = board.Charts.Select(chart => PlotTrackProbe(chart, board)).ToArray(),
                    physicalInputValidated = false, desktopPresentCaptured = false, screenshotMethod = "RenderTargetBitmap of a shown native Window", performanceMeasured = false });
            }
            var oldPlots = board.Charts.Select(ProfessionalPlotControl).ToArray(); var oldTracksAll = board.Charts.SelectMany(ProfessionalTrackControls).ToArray(); Invoke("绘图区 更新标题");
            Check("live-business-title-keeps-controls", oldPlots.SequenceEqual(board.Charts.Select(ProfessionalPlotControl)) && oldTracksAll.SequenceEqual(board.Charts.SelectMany(ProfessionalTrackControls)) && board.Charts.All(chart =>
                chart.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Name is "ProfessionalPlotTitle" or "ProfessionalTrackballTitle").All(text => text.Text == board.Model.Title)));
            board.FloatOption.IsChecked = true; Flush(); ReadAll();
            Check("float-mode-creates-one-context-per-real-reading", board.Charts.All(chart => ProfessionalTrackControls(chart).Length == ProfessionalTracks(chart).Count && ProfessionalTrackControls(chart).All(control => control.DataContext is ChartDataTrackballContext { Readings.Count: 1 })));
            board.CappedOption.IsChecked = true; Flush(); ReadAll();
            Check("budget-bounds-visible-readings-without-truncating-members", board.Charts.All(chart => ProfessionalTracks(chart).Count is > 0 and <= 2 && PlotTrackOriginal(chart, board)));
            board.CappedOption.IsChecked = false; board.FloatOption.IsChecked = false; board.HorizontalOption.IsChecked = true; Flush(); ReadAll();
            Check("horizontal-slice-uses-original-members", board.Charts.All(chart => PlotTrackOriginal(chart, board)));
            board.HorizontalOption.IsChecked = false; Flush(); ReadAll();
            var previous = board.Charts.SelectMany(ProfessionalTrackControls).ToArray(); Invoke("绘图区 更新数值"); ReadAll();
            Check("INPC-updates-original-values", board.Charts.All(chart => PlotTrackOriginal(chart, board)) && ProfessionalTracks(board.Funnel).Any(reading => reading.Members.Any(member => member.SourceIndex == 0) && reading.Value == board.Model.Rows[0].Value) &&
                ProfessionalTracks(board.Mosaic).Any(reading => reading.Members.Select(member => member.SourceIndex).SequenceEqual(new[] { 0, 2 }) && reading.Value == board.Model.Rows[0].Value + board.Model.Rows[2].Value));
            var retiredRows = board.Model.Rows.ToArray(); var retiredContexts = board.Charts.SelectMany(ProfessionalTrackControls).Select(control => control.DataContext).ToArray(); Invoke("绘图区 替换来源"); ReadAll();
            Check("source-replacement-releases-old-observers", retiredRows.All(row => row.Subscribers == 0) && board.Charts.All(chart => PlotTrackOriginal(chart, board)));
            Check("source-replacement-retires-reading-contexts", board.Charts.SelectMany(ProfessionalTrackControls).All(control => !retiredContexts.Contains(control.DataContext)));
            var replacementVM = new ProfessionalTooltipModel { Rows = board.Model.Rows, Title = "替换后的绘图区 VM" }; foreach (var chart in board.Charts) chart.DataContext = replacementVM; Flush(); ReadAll();
            Check("VM-replacement-reaches-both-contexts", board.Charts.All(chart => ReferenceEquals(ProfessionalPlotContext(chart)?.DataContext, replacementVM) && ProfessionalTrackControls(chart).All(control => control.DataContext is ChartDataTrackballContext context && ReferenceEquals(context.BusinessDataContext, replacementVM))));
            foreach (var chart in board.Charts) chart.DataContext = board.Model; Flush(); ReadAll();
            var center = ProfessionalPlotContext(board.Strip)!.ProjectLayout(.5, .5); Invoke("绘图区 缩放平移"); ReadAll();
            Check("zoom-pan-reprojects-layout-center", PlotTrackDistance(ProfessionalPlotContext(board.Strip)!.ProjectLayout(.5, .5), center) > 1 && PlotTrackOriginal(board.Strip, board));
            Invoke("绘图区 恢复视图"); Check("reset-clears-only-transient-trackball", board.Charts.All(chart => ProfessionalTracks(chart).Count == 0 && ProfessionalTrackControls(chart).Length == 0 && ProfessionalPlotControl(chart) is not null)); ReadAll();
            board.TemplateOption.IsChecked = false; Flush(); ReadAll(); Check("null-trackball-template-keeps-default-readings", board.Charts.All(chart => ProfessionalTracks(chart).Count > 0 && ProfessionalTrackControls(chart).Length == 0));
            board.TemplateOption.IsChecked = true; Flush(); ReadAll(); board.TrackOption.IsChecked = false; Flush(); Check("trackball-off-clears-controls-and-readings", board.Charts.All(chart => ProfessionalTracks(chart).Count == 0 && ProfessionalTrackControls(chart).Length == 0));
            board.TrackOption.IsChecked = true; Flush(); ReadAll(); board.PlotOption.IsChecked = false; Flush(); Check("plot-content-off-keeps-trackball", board.Charts.All(chart => ProfessionalPlotControl(chart) is null && ProfessionalTracks(chart).Count > 0));
            board.PlotOption.IsChecked = true; Flush(); ReadAll();
            previous = board.Charts.SelectMany(ProfessionalTrackControls).ToArray(); board.Group.IsEnabled = false; Flush(); Check("disabled-ancestor-clears-transient", previous.All(ProfessionalReleased) && board.Charts.All(chart => ProfessionalTracks(chart).Count == 0));
            board.Group.IsEnabled = true; Flush(); ReadAll(); oldPlots = board.Charts.Select(ProfessionalPlotControl).ToArray(); previous = board.Charts.SelectMany(ProfessionalTrackControls).ToArray(); board.Group.IsVisible = false; Flush();
            Check("hidden-ancestor-releases-all-overlays", oldPlots.All(ProfessionalReleased) && previous.All(ProfessionalReleased) && board.Charts.All(chart => ProfessionalPlotControl(chart) is null && ProfessionalTrackControls(chart).Length == 0));
            board.Group.IsVisible = true; Flush(); ReadAll(); Check("visible-ancestor-restores-current-contexts", board.Charts.All(chart => ProfessionalPlotContext(chart) is not null && PlotTrackOriginal(chart, board)));
            oldPlots = board.Charts.Select(ProfessionalPlotControl).ToArray(); previous = board.Charts.SelectMany(ProfessionalTrackControls).ToArray(); container.Child = null; Flush();
            Check("detach-releases-overlays-and-observers", oldPlots.All(ProfessionalReleased) && previous.All(ProfessionalReleased) && board.Model.Rows.All(row => row.Subscribers == 0));
            container.Child = board.View; Flush(); ReadAll(); Check("reattach-recovers-current-sources", board.Charts.All(chart => ProfessionalPlotContext(chart) is not null && PlotTrackOriginal(chart, board)));
            await VerifyProfessionalPlotTrackballExportsAsync(board.Funnel, "analysis", board, window, checks, Image);
            await VerifyProfessionalPlotTrackballExportsAsync(board.Mosaic, "engineering", board, window, checks, Image);
            _professionalPlotTrackballProbes.Add(new { schema = "professional-plot-trackball-native/v1", phase = "lifecycle", title = board.Model.Title, charts = board.Charts.Select(chart => PlotTrackProbe(chart, board)).ToArray(),
                sources = board.Model.Rows.Select(row => new { row.Id, row.Value, row.Subscribers }).ToArray(), physicalInputValidated = false, performanceMeasured = false });
            void Invoke(string caption) { board.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, caption)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(); }
        }
        finally { window.Close(); }
    }

    private async Task VerifyProfessionalPlotTrackballExportsAsync(ChartBase chart, string kind, ProfessionalPlotTrackballBoard board, Window window, Dictionary<string, bool> checks, Func<string, string, string> image)
    {
        void Check(string key, bool value) => checks[$"professional-plot-trackball-export-{kind}-{key}"] = value;
        void Flush() { FlushControlTemplateWindow(window); FlushControlTemplateWindow(window); }
        var exportSize = new PixelSize(640, 320); var options = new ChartExportOptions { PixelSize = exportSize, Background = window.Background, JpegQuality = 90 };
        async Task<bool> Save(string path, string format) => format switch { "sync-png" => chart is EngineeringChartBase e ? e.TryExportPng(path, options) : ((AnalysisChartBase)chart).TryExportPng(path, options),
            "async-png" => chart is EngineeringChartBase e ? await e.TryExportPngAsync(path, options) : await ((AnalysisChartBase)chart).TryExportPngAsync(path, options),
            "sync-jpeg" => chart is EngineeringChartBase e ? e.TryExportJpeg(path, options) : ((AnalysisChartBase)chart).TryExportJpeg(path, options),
            _ => chart is EngineeringChartBase e ? await e.TryExportJpegAsync(path, options) : await ((AnalysisChartBase)chart).TryExportJpegAsync(path, options) };
        var artifacts = new List<object>(); var hashes = new Dictionary<string, string>();
        foreach (var state in new[] { "legacy", "plot-active", "plot-cleared" })
        {
            SetProfessionalPlot(chart, state == "legacy" ? null : "永久绘图区 · 归一化中心", state == "legacy" ? null : board.Plots[chart]); Flush();
            if (state == "plot-active") { _ = RouteProfessionalReading(chart, window, 0); Check("live-trackball-exists-before-export", ProfessionalTrackControls(chart).Length > 0 && PlotTrackOriginal(chart, board)); }
            else ClearProfessionalTrackball(chart); Flush();
            var syncHashes = new Dictionary<string, string>();
            foreach (var format in new[] { "sync-png", "async-png", "sync-jpeg", "async-jpeg" })
            {
                var path = image($"{kind}-{state}-{format}", format.EndsWith("jpeg", StringComparison.Ordinal) ? "jpg" : "png"); var saved = await Save(path, format);
                using var bitmap = new Bitmap(path); var raster = ReadShapePixels(bitmap); var hash = Convert.ToHexString(SHA256.HashData(raster.Pixels)); var codec = format.EndsWith("jpeg", StringComparison.Ordinal) ? "jpeg" : "png";
                if (format.StartsWith("sync", StringComparison.Ordinal)) syncHashes[codec] = hash;
                Check(state + "-" + format + "-valid-size-and-sync-async-equality", saved && bitmap.PixelSize == exportSize && syncHashes[codec] == hash);
                if (state == "legacy") hashes[format] = hash;
                else Check(state + "-" + format + "-includes-permanent-content", hashes[format] != hash);
                if (state == "plot-active") hashes["active-" + format] = hash;
                if (state == "plot-cleared") Check(format + "-excludes-transient-trackball", hashes["active-" + format] == hash);
                Check(state + "-" + format + "-preserves-live-state", state == "plot-active" ? ProfessionalTrackControls(chart).Length > 0 && PlotTrackOriginal(chart, board) : ProfessionalTracks(chart).Count == 0);
                artifacts.Add(new { state, format, path, saved, width = bitmap.PixelSize.Width, height = bitmap.PixelSize.Height, decodedBgraSha256 = hash,
                    legacyBgraSha256 = hashes[format], activeBgraSha256 = hashes.GetValueOrDefault("active-" + format), differsFromLegacy = hashes[format] != hash, syncAsyncSameCodecEqual = syncHashes[codec] == hash });
            }
        }
        SetProfessionalPlot(chart, "永久绘图区 · 归一化中心", board.Plots[chart]); Flush();
        using var baselineBitmap = new Bitmap(image($"{kind}-plot-cleared-sync-png", "png")); var baseline = ReadShapePixels(baselineBitmap);
        var target = ProfessionalPlotControl(chart)?.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(text => text.Name == "ProfessionalPlotCaption");
        var oldOpacity = target?.Opacity ?? 1; var bounds = target is null ? default : PresentationControlBounds(target, chart);
        var scale = Math.Min(exportSize.Width / chart.Bounds.Width, exportSize.Height / chart.Bounds.Height);
        var region = new Rect((exportSize.Width - chart.Bounds.Width * scale) / 2 + bounds.X * scale, (exportSize.Height - chart.Bounds.Height * scale) / 2 + bounds.Y * scale, bounds.Width * scale, bounds.Height * scale);
        var hiddenPath = image($"{kind}-plot-glyph-hidden", "png"); var restoredPath = image($"{kind}-plot-glyph-restored", "png"); bool hiddenSaved;
        try { if (target is not null) target.Opacity = 0; Flush(); hiddenSaved = await Save(hiddenPath, "sync-png"); }
        finally { if (target is not null) target.Opacity = oldOpacity; Flush(); }
        var restoredSaved = await Save(restoredPath, "sync-png"); using var hiddenBitmap = new Bitmap(hiddenPath); using var restoredBitmap = new Bitmap(restoredPath);
        var hiddenChanges = CountControlTemplateExactChanges(baseline, ReadShapePixels(hiddenBitmap), region); var restoredChanges = CountControlTemplateExactChanges(baseline, ReadShapePixels(restoredBitmap), region);
        var stableGeometry = target is not null && bounds == PresentationControlBounds(target, chart);
        Check("real-template-glyph-pixels-enter-export", hiddenSaved && restoredSaved && stableGeometry && hiddenChanges >= 3 && restoredChanges == 0);
        _professionalPlotTrackballProbes.Add(new { schema = "professional-plot-trackball-native/v1", phase = "export", kind, artifacts,
            glyphProofs = new[] { new { kind = "plot", target = target?.Name, text = target?.Text, liveBounds = C7Bounds(bounds), exportRegion = C7Bounds(region), hiddenPath, restoredPath, hiddenSaved, restoredSaved, hiddenChangedPixels = hiddenChanges, restoredChangedPixels = restoredChanges, geometryUnchanged = stableGeometry } },
            comparison = "Each codec compares synchronous and asynchronous decoded pixels. Permanent plot content changes legacy pixels; active and cleared Trackball export pixels are equal. Hidden/restored real TextBlock verifies unchanged-geometry PNG glyph pixels. No cross-codec equality is assumed.",
            chart = PlotTrackProbe(chart, board), physicalInputValidated = false, desktopPresentCaptured = false, performanceMeasured = false });
    }
}
