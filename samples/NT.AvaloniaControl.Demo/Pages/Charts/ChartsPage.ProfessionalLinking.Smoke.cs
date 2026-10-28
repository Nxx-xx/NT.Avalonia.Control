using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _professionalLinkingProbes = [];
    private async Task RunProfessionalLinkingSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        var directory = Path.GetDirectoryName(baseImage)!; Directory.CreateDirectory(directory); var stem = Path.GetFileNameWithoutExtension(baseImage);
        string Image(string suffix, string extension = "png") => Path.Combine(directory, $"{stem}-professional-linking-{suffix}.{extension}");
        void Check(string name, bool value) { checks["professional-linking-" + name] = value; Console.WriteLine($"professional-linking: {name}={value}"); Console.Out.Flush(); }
        using var board = CreateProfessionalLinkingBoard(); var owner = board.Owner; var charts = owner.Charts;
        var host = new Border { Padding = new Thickness(18), Child = board.View };
        var window = new Window { Width = 1440, Height = 1000, CanResize = false, Title = "Charts · 专业范围与业务联动", Content = host };
        void Flush() { FlushControlTemplateWindow(window); FlushControlTemplateWindow(window); }
        bool Current(ChartBase chart) => LinkingData(chart).All(point => point.Item is ProfessionalTooltipRow row && point.SourceIndex >= 0 && point.SourceIndex < owner.Model.Rows.Count && ReferenceEquals(row, owner.Model.Rows[point.SourceIndex]) && Equals(row.Id, point.StableId));
        string[] Ids(ChartBase chart) => LinkingData(chart).Select(point => ((ProfessionalTooltipRow)point.Item!).Id).Order().ToArray();
        bool SameIds() => charts.All(chart => Ids(chart).SequenceEqual(Ids(owner.Funnel)));
        object Probe(ChartBase chart) => new { chart.Name, kind = chart.GetType().Name, bounds = C7Bounds(chart.Bounds), originalSelection = Current(chart), keys = LinkingKeys(chart)?.Select(key => key.ToString()).ToArray(), selected = LinkingData(chart).Select(point => new { point.SourceIndex, stableId = point.StableId?.ToString(), originalSourceReference = ReferenceEquals(point.Item, owner.Model.Rows[point.SourceIndex]) }).ToArray(), range = chart is EngineeringChartBase e ? e.DataSelectionRange : ((AnalysisChartBase)chart).DataSelectionRange,
            plot = ProfessionalPlotContext(chart) is { } context ? new { plotArea = C7Bounds(context.PlotArea), businessContext = ReferenceEquals(context.DataContext, chart.DataContext) } : null,
            realTemplateNames = chart.GetVisualDescendants().OfType<Control>().Where(control => control.Name is "LinkLegendBody" or "LinkLabelBody" or "ProfessionalPlotContent" or "ProfessionalTrackballBody" or "LinkTooltipBody").Select(control => control.Name).ToArray() };
        try
        {
            window.Show(_hostWindow); await Task.Delay(70); Flush();
            Check("native-four-family-board", window.TryGetPlatformHandle() is { Handle: not 0 } && charts.All(chart => chart.Bounds.Width > 400 && chart.Bounds.Height == 320));
            var modes = new[] { (Name: "normal-light", Theme: ThemeVariant.Light, Contrast: ChartHighContrastMode.None), (Name: "normal-dark", Theme: ThemeVariant.Dark, Contrast: ChartHighContrastMode.None), (Name: "hc-light", Theme: ThemeVariant.Light, Contrast: ChartHighContrastMode.Light), (Name: "hc-dark", Theme: ThemeVariant.Dark, Contrast: ChartHighContrastMode.Dark) };
            foreach (var mode in modes)
            {
                window.RequestedThemeVariant = mode.Theme; owner.Contrast.SelectedItem = mode.Contrast; Flush();
                owner.Funnel.SelectedDataKeys = ["a"]; Flush();
                Check(mode.Name + "-analysis-to-engineering-selection", SameIds() && Ids(owner.Funnel).SequenceEqual(new[] { "a" }) && charts.All(Current));
                owner.Mosaic.SelectedDataKeys = owner.Mosaic.GetMarks().Single(mark => mark.Members.Count == 2).Members.Select(point => point.StableId!).ToArray(); Flush();
                Check(mode.Name + "-aggregate-retains-all-members", SameIds() && Ids(owner.Mosaic).SequenceEqual(new[] { "a", "c" }) && charts.All(Current));
                owner.Map.SelectedDataKeys = [owner.Map.GetMarks().Single(mark => mark.Members.Any(member => member.SourceIndex == 1)).Key!]; Flush();
                Check(mode.Name + "-map-business-ID-back-to-analysis", SameIds() && Ids(owner.Funnel).SequenceEqual(new[] { "b" }));
                foreach (var chart in charts) { _ = RouteProfessionalReading(chart, window, 0); }
                Flush();
                Check(mode.Name + "-all-permanent-and-transient-template-controls", charts.All(chart => ProfessionalPlotControl(chart) is not null && chart.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "LinkLegendBody") && chart.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "LinkLabelBody") && ProfessionalTrackControls(chart).Length > 0));
                Check(mode.Name + "-theme-foreground-inherited", charts.All(chart => chart.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Name?.StartsWith("Link", StringComparison.Ordinal) == true).All(text => C7Brush(text.Foreground) == C7Brush(chart.Foreground))));
                var path = Image(mode.Name); using (var bitmap = new RenderTargetBitmap(new PixelSize(1440, 1000), new Vector(96, 96))) { bitmap.Render(window); bitmap.Save(path); }
                _professionalLinkingProbes.Add(new { schema = "professional-linking-native/v1", phase = "theme", mode = mode.Name, image = path, charts = charts.Select(Probe).ToArray(), nativeHandleCreated = window.TryGetPlatformHandle() is { Handle: not 0 }, renderScaling = window.RenderScaling, physicalInputValidated = false, desktopPresentCaptured = false, screenshotMethod = "RenderTargetBitmap of a shown native Window", performanceMeasured = false });
            }
            VerifyProfessionalTrackballContrastPixels(owner.Funnel, window, checks, Image);
            window.RequestedThemeVariant = ThemeVariant.Light; owner.Contrast.SelectedItem = ChartHighContrastMode.None; Flush();
            foreach (var chart in charts) ClearLinkingData(chart); Flush();
            SelectionRangeClick(owner.Funnel, window, ProfessionalAnchor(owner.Funnel, 0)); Flush();
            Check("routed-native-click-selects-original-and-links", Ids(owner.Funnel).SequenceEqual(new[] { "a" }) && SameIds());
            owner.Funnel.Focus(); owner.Funnel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }); owner.Funnel.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Flush();
            Check("routed-native-keyboard-selection-links", LinkingData(owner.Funnel).Count > 0 && !Ids(owner.Funnel).SequenceEqual(new[] { "a" }) && SameIds() && charts.All(Current));
            var selector = board.Horizontal.GetVisualDescendants().OfType<ChartRangeSelector>().Single();
            selector.Range = new(.1, .7); Flush(); var oldOtherRange = owner.Strip.DataSelectionRange;
            Check("local-range-selects-complete-real-members", owner.Mosaic.DataSelectionRange is not null && LinkingData(owner.Mosaic).Count > 0 && charts.All(Current) && SameIds());
            Check("different-domains-never-copy-raw-ranges", owner.Strip.DataSelectionRange == oldOtherRange && owner.Funnel.DataSelectionRange is null && owner.Map.DataSelectionRange is null && owner.Strip.Zoom == 1 && owner.Map.Zoom == 1);
            var lower = selector.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "PART_LowerHandle");
            var start = lower.TranslatePoint(new Point(lower.Bounds.Width / 2, lower.Bounds.Height / 2), selector)!.Value;
            var drag = SelectionRangeDrag(selector, window, start, start + new Vector((selector.Bounds.Width - selector.HandleSize) * .08, 0)); Flush();
            var expectedHorizontalLower = .1 + drag.Delta.X / (selector.Bounds.Width - selector.HandleSize);
            var horizontalInput = new { axis = "X", source = "Mosaic", captured = drag.Captured, observedDelta = new { x = drag.Delta.X, y = drag.Delta.Y }, railLength = selector.Bounds.Width - selector.HandleSize, beforeLower = .1, actualLower = selector.Range.Lower, actualUpper = selector.Range.Upper, expectedLower = expectedHorizontalLower };
            Check("routed-range-drag-captures-and-selects", drag.Captured && !selector.IsDragging && Math.Abs(selector.Range.Lower - expectedHorizontalLower) < 1e-7 && selector.Range.Lower > .1 && selector.Range.Upper == .7 && owner.Mosaic.DataSelectionRange is not null && charts.All(Current));
            var rangeBeforeKey = selector.Range; lower.Focus(); lower.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }); Flush();
            Check("routed-range-keyboard-adjusts-endpoint", selector.Range.Lower > rangeBeforeKey.Lower && selector.Range.Upper == rangeBeforeKey.Upper);
            var verticalSelector = board.Vertical.Selector; verticalSelector.Range = new(.2, .8); Flush();
            var verticalLower = verticalSelector.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "PART_LowerHandle"); var verticalStart = verticalLower.TranslatePoint(new Point(verticalLower.Bounds.Width / 2, verticalLower.Bounds.Height / 2), verticalSelector)!.Value;
            var verticalDrag = SelectionRangeDrag(verticalSelector, window, verticalStart, verticalStart + new Vector(0, (verticalSelector.Bounds.Height - verticalSelector.HandleSize) * .08)); Flush();
            Console.WriteLine($"professional-linking-vertical: captured={verticalDrag.Captured}, innerBounds={verticalSelector.Bounds}, wrapperBounds={board.Vertical.Bounds}, delta={verticalDrag.Delta}, range={verticalSelector.Range}, target={owner.Strip.DataSelectionRange}"); Console.Out.Flush();
            var expectedVerticalLower = .2 + verticalDrag.Delta.Y / (verticalSelector.Bounds.Height - verticalSelector.HandleSize);
            var verticalInput = new { axis = "Y", source = "Strip", captured = verticalDrag.Captured, observedDelta = new { x = verticalDrag.Delta.X, y = verticalDrag.Delta.Y }, railLength = verticalSelector.Bounds.Height - verticalSelector.HandleSize, beforeLower = .2, actualLower = verticalSelector.Range.Lower, actualUpper = verticalSelector.Range.Upper, expectedLower = expectedVerticalLower };
            Check("routed-vertical-range-drag-uses-top-left-layout-direction", verticalDrag.Captured && !verticalSelector.IsDragging && Math.Abs(verticalSelector.Range.Lower - expectedVerticalLower) < 1e-7 && verticalSelector.Range.Lower > .2 && verticalSelector.Range.Upper == .8 && owner.Strip.DataSelectionRange is { } stripRange && Math.Abs(stripRange.Y - expectedVerticalLower) < 1e-7 && stripRange.Width == 1 && charts.All(Current));
            Check("vertical-source-links-IDs-without-copying-range-to-other-family", SameIds() && owner.Mosaic.DataSelectionRange is null && owner.Funnel.DataSelectionRange is null);
            owner.Strip.SelectedDataKeys = ["a"]; Flush(); var priorKeys = charts.ToDictionary(chart => chart, Ids); var priorRanges = charts.ToDictionary(chart => chart, chart => chart is EngineeringChartBase e ? e.DataSelectionRange : ((AnalysisChartBase)chart).DataSelectionRange);
            Invoke("联动 缩放 Strip"); Check("local-viewport-does-not-cross-domains", owner.Strip.Zoom == 1.25 && owner.Mosaic.Zoom == 1 && owner.Map.Zoom == 1 && charts.All(chart => Ids(chart).SequenceEqual(priorKeys[chart])));
            Invoke("联动 恢复视图"); Check("reset-clears-ranges-and-linked-data-selection", charts.All(chart => LinkingData(chart).Count == 0) && owner.Mosaic.DataSelectionRange is null && owner.Strip.DataSelectionRange is null);
            owner.Funnel.SelectedDataKeys = ["a"]; Flush(); var retired = owner.Model.Rows.ToArray(); Invoke("联动 更新数值");
            Check("INPC-update-retains-current-identity", SameIds() && charts.All(Current) && charts.All(chart => Ids(chart).SequenceEqual(new[] { "a" })));
            Invoke("联动 替换来源");
            Check("source-replacement-releases-old-observers", retired.All(row => row.Subscribers == 0));
            Check("source-replacement-remaps-explicit-IDs", SameIds() && charts.All(Current) && charts.All(chart => Ids(chart).SequenceEqual(new[] { "a" })) && LinkingData(owner.Funnel).All(point => retired.All(row => !ReferenceEquals(row, point.Item))));
            Invoke("联动 切换 VM"); foreach (var chart in charts) { _ = RouteProfessionalReading(chart, window, 0); } Flush();
            Check("VM-change-updates-all-contexts", charts.All(chart => ReferenceEquals(ProfessionalPlotContext(chart)?.DataContext, chart.DataContext) && ProfessionalTracks(chart).All(reading => ReferenceEquals(reading.BusinessDataContext, chart.DataContext))));
            Invoke("联动 切换 VM");
            board.Enabled.IsChecked = false; owner.Funnel.SelectedDataKeys = ["b"]; Flush(); Check("disabled-link-keeps-other-selections", Ids(owner.Funnel).SequenceEqual(new[] { "b" }) && Ids(owner.Strip).SequenceEqual(new[] { "a" }));
            board.Enabled.IsChecked = true; board.Links.SynchronizeFrom(owner.Funnel); Flush(); Check("explicit-sync-replays-current-business-selection", SameIds() && Ids(owner.Map).SequenceEqual(new[] { "b" }));
            owner.Group.IsVisible = false; Flush(); owner.Funnel.SelectedDataKeys = ["c"]; Flush(); Check("hidden-group-does-not-propagate", LinkingKeys(owner.Strip) is { Count: 1 } && Equals(LinkingKeys(owner.Strip)![0], "b") && charts.All(chart => LinkingData(chart).Count == 0));
            owner.Group.IsVisible = true; Flush(); board.Links.SynchronizeFrom(owner.Funnel); Flush(); Check("visible-explicit-sync-recovers-current-selection", SameIds() && Ids(owner.Funnel).SequenceEqual(new[] { "c" }));
            host.Child = null; Flush(); Check("detach-releases-original-observers", owner.Model.Rows.All(row => row.Subscribers == 0));
            host.Child = board.View; Flush(); board.Links.SynchronizeFrom(owner.Funnel); Flush(); Check("reattach-links-current-originals", charts.All(Current) && SameIds());
            owner.Mosaic.SelectDataRange(new Rect(.1, .1, .6, .8)); Flush();
            await VerifyProfessionalLinkingExportsAsync(owner.Funnel, "analysis", board, window, checks, Image);
            await VerifyProfessionalLinkingExportsAsync(owner.Mosaic, "engineering", board, window, checks, Image);
            Invoke("联动 清除选择"); Check("clear-selection-and-local-ranges", charts.All(chart => LinkingData(chart).Count == 0 && (chart is EngineeringChartBase e ? e.SelectedIndex : ((AnalysisChartBase)chart).SelectedIndex) == -1) && owner.Mosaic.DataSelectionRange is null && owner.Strip.DataSelectionRange is null);
            _professionalLinkingProbes.Add(new { schema = "professional-linking-native/v1", phase = "lifecycle", rangeInputs = new[] { horizontalInput, verticalInput }, charts = charts.Select(Probe).ToArray(), horizontalRange = board.Horizontal.Range, verticalRange = board.Vertical.Range, localRangeCoordinates = "plot layout fractions, independent per family", synchronization = "business IDs only; range and viewport opt in only with matching explicit layout domains", physicalInputValidated = false, performanceMeasured = false });
            await VerifyProfessionalLinkingAnimatedCombinationAsync(window, host, checks, Image);
            void Invoke(string caption) { board.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, caption)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(); }
        }
        finally { window.Close(); }
    }

    private void VerifyProfessionalTrackballContrastPixels(FunnelChart chart, Window window, Dictionary<string, bool> checks, Func<string, string, string> image)
    {
        void Flush() { FlushControlTemplateWindow(window); FlushControlTemplateWindow(window); }
        var palette = chart.Palette; var template = chart.TrackballContentTemplate; var tooltip = chart.IsTooltipEnabled; var labels = chart.MaxDataLabels; var markerFill = chart.TrackballMarkerFill;
        try
        {
            chart.Palette = [Brushes.White]; chart.MaxDataLabels = 0; chart.TrackballMarkerFill = null; chart.ClearDataSelection(); chart.TrackballContentTemplate = new FuncDataTemplate<ChartDataTrackballContext>((_, _) => null); chart.IsTooltipEnabled = false; Flush();
            var mark = chart.GetMarks().First(mark => mark.IsVisible); var anchor = mark.LabelPosition; chart.UpdateTrackball(anchor); Flush();
            var paths = new Dictionary<string, string>(); var hashes = new Dictionary<string, string>();
            foreach (var state in new[] { "bare", "line", "marker", "both" })
            {
                chart.ShowTrackballLine = state is "line" or "both"; chart.ShowTrackballMarkers = state is "marker" or "both"; Flush();
                var target = image("hc-dark-white-glyph-" + state, "png"); using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height)), new Vector(96, 96)); bitmap.Render(chart); bitmap.Save(target);
                paths[state] = target; hashes[state] = Convert.ToHexString(SHA256.HashData(ReadShapePixels(bitmap).Pixels));
            }
            using var bare = new Bitmap(paths["bare"]); using var line = new Bitmap(paths["line"]); using var marker = new Bitmap(paths["marker"]);
            var markerRegion = new Rect(anchor.X - chart.TrackballMarkerSize / 2 - 3, anchor.Y - chart.TrackballMarkerSize / 2 - 3, chart.TrackballMarkerSize + 6, chart.TrackballMarkerSize + 6);
            var lineRegion = new Rect(anchor.X - chart.TrackballLineStrokeThickness / 2 - 2, mark.Bounds.Top + 3, chart.TrackballLineStrokeThickness + 4, Math.Max(1, Math.Min(3, mark.Bounds.Height / 4)));
            var lineChanges = CountControlTemplateExactChanges(ReadShapePixels(bare), ReadShapePixels(line), lineRegion); var markerChanges = CountControlTemplateExactChanges(ReadShapePixels(bare), ReadShapePixels(marker), markerRegion);
            checks["professional-linking-hc-dark-default-line-visible-on-white-glyph"] = chart.HighContrastMode == ChartHighContrastMode.Dark && chart.TrackballLineStroke is null && lineChanges >= 5;
            checks["professional-linking-hc-dark-default-marker-visible-on-white-glyph"] = chart.TrackballMarkerStroke is null && markerChanges >= 3;
            _professionalLinkingProbes.Add(new { schema = "professional-linking-native/v1", phase = "trackball-local-contrast", theme = "hc-dark", nativeRender = true, fill = "#FFFFFFFF", dataLabelsSuppressedToIsolateStroke = true, markerFillClearedToIsolateStroke = true, defaultLineStroke = chart.TrackballLineStroke is null, defaultMarkerStroke = chart.TrackballMarkerStroke is null, chartForeground = C7Brush(chart.Foreground), anchor = C5Point(anchor), glyphBounds = C7Bounds(mark.Bounds), lineRegion = C7Bounds(lineRegion), markerRegion = C7Bounds(markerRegion), lineChangedPixels = lineChanges, markerChangedPixels = markerChanges, paths, decodedBgraSha256 = hashes, physicalInputValidated = false, desktopPresentCaptured = false, performanceMeasured = false });
        }
        finally { chart.Palette = palette; chart.MaxDataLabels = labels; chart.TrackballMarkerFill = markerFill; chart.TrackballContentTemplate = template; chart.IsTooltipEnabled = tooltip; chart.ShowTrackballLine = true; chart.ShowTrackballMarkers = true; Flush(); }
    }

    private async Task VerifyProfessionalLinkingExportsAsync(ChartBase chart, string kind, ProfessionalLinkingBoard board, Window window, Dictionary<string, bool> checks, Func<string, string, string> image)
    {
        void Check(string key, bool value) => checks[$"professional-linking-export-{kind}-{key}"] = value;
        void Flush() { FlushControlTemplateWindow(window); FlushControlTemplateWindow(window); }
        var options = new ChartExportOptions { PixelSize = new PixelSize(640, 320), Background = window.Background, JpegQuality = 90 };
        async Task<bool> Save(string path, string format) => format switch { "sync-png" => chart is EngineeringChartBase e ? e.TryExportPng(path, options) : ((AnalysisChartBase)chart).TryExportPng(path, options), "async-png" => chart is EngineeringChartBase e ? await e.TryExportPngAsync(path, options) : await ((AnalysisChartBase)chart).TryExportPngAsync(path, options), "sync-jpeg" => chart is EngineeringChartBase e ? e.TryExportJpeg(path, options) : ((AnalysisChartBase)chart).TryExportJpeg(path, options), _ => chart is EngineeringChartBase e ? await e.TryExportJpegAsync(path, options) : await ((AnalysisChartBase)chart).TryExportJpegAsync(path, options) };
        var selectedKeys = LinkingKeys(chart)?.ToArray() ?? []; var range = chart is EngineeringChartBase engine ? engine.DataSelectionRange : ((AnalysisChartBase)chart).DataSelectionRange;
        var artifacts = new List<object>(); var hashes = new Dictionary<string, string>();
        var originalMode = chart is EngineeringChartBase root ? root.TrackballMode : ((AnalysisChartBase)chart).TrackballMode;
        void Mode(ChartTrackballMode value) { if (chart is EngineeringChartBase e) e.TrackballMode = value; else ((AnalysisChartBase)chart).TrackballMode = value; }
        foreach (var state in new[] { "selected-active", "selected-tooltip-active", "selected-cleared-transient", "unselected" })
        {
            if (state == "selected-active") { SetProfessionalTooltipEnabled(chart, true); _ = RouteProfessionalReading(chart, window, 0); Check("live-trackball-before-export", ProfessionalTrackControls(chart).Length > 0); }
            else if (state == "selected-tooltip-active") { Mode(ChartTrackballMode.None); SetProfessionalTooltipEnabled(chart, true); Flush(); _ = RouteProfessionalReading(chart, window, 0); Check("live-tooltip-before-export", chart.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "LinkTooltipBody")); }
            else { ClearProfessionalTrackball(chart); SetProfessionalTooltipEnabled(chart, false); }
            if (state == "unselected") { board.Links.SynchronizeSelection = false; ClearLinkingData(chart); }
            Flush(); var syncHashes = new Dictionary<string, string>();
            foreach (var format in new[] { "sync-png", "async-png", "sync-jpeg", "async-jpeg" })
            {
                var path = image($"{kind}-{state}-{format}", format.EndsWith("jpeg", StringComparison.Ordinal) ? "jpg" : "png"); var saved = await Save(path, format);
                using var bitmap = new Bitmap(path); var raster = ReadShapePixels(bitmap); var hash = Convert.ToHexString(SHA256.HashData(raster.Pixels)); var codec = format.EndsWith("jpeg", StringComparison.Ordinal) ? "jpeg" : "png";
                if (format.StartsWith("sync", StringComparison.Ordinal)) syncHashes[codec] = hash;
                if (state == "selected-active") hashes[format] = hash;
                Check(state + "-" + format + "-valid-and-same-codec", saved && bitmap.PixelSize == options.PixelSize && hash == syncHashes[codec]);
                if (state is "selected-tooltip-active" or "selected-cleared-transient") Check(state + "-" + format + "-transient-excluded", hash == hashes[format]);
                if (state == "unselected") Check(format + "-selection-changes-export-pixels", hash != hashes[format]);
                artifacts.Add(new { state, format, path, saved, width = bitmap.PixelSize.Width, height = bitmap.PixelSize.Height, decodedBgraSha256 = hash, selectedBgraSha256 = hashes[format], syncAsyncSameCodecEqual = hash == syncHashes[codec] });
            }
        }
        if (chart is EngineeringChartBase restore) { restore.SelectedDataKeys = selectedKeys; restore.DataSelectionRange = range; } else { var restoreAnalysis = (AnalysisChartBase)chart; restoreAnalysis.SelectedDataKeys = selectedKeys; restoreAnalysis.DataSelectionRange = range; }
        board.Links.SynchronizeSelection = true; SetProfessionalTooltipEnabled(chart, false); ClearProfessionalTrackball(chart); Flush();
        var glyphProofs = new List<object>();
        foreach (var (glyph, targetName) in new[] { ("plot", "ProfessionalPlotCaption"), ("legend", "LinkLegendText"), ("label", "LinkLabelText") })
        {
            var target = chart.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(text => text.Name == targetName && text.Bounds.Width > 0 && text.Bounds.Height > 0);
            var bounds = target is null ? default : PresentationControlBounds(target, chart); var oldOpacity = target?.Opacity ?? 1;
            var scale = Math.Min(640 / chart.Bounds.Width, 320 / chart.Bounds.Height);
            var region = new Rect((640 - chart.Bounds.Width * scale) / 2 + bounds.X * scale, (320 - chart.Bounds.Height * scale) / 2 + bounds.Y * scale, bounds.Width * scale, bounds.Height * scale);
            var hiddenPath = image($"{kind}-{glyph}-glyph-hidden", "png"); var restoredPath = image($"{kind}-{glyph}-glyph-restored", "png"); bool hiddenSaved;
            try { if (target is not null) target.Opacity = 0; Flush(); hiddenSaved = await Save(hiddenPath, "sync-png"); }
            finally { if (target is not null) target.Opacity = oldOpacity; Flush(); }
            var restoredSaved = await Save(restoredPath, "sync-png"); using var baselineBitmap = new Bitmap(image($"{kind}-selected-cleared-transient-sync-png", "png")); using var hiddenBitmap = new Bitmap(hiddenPath); using var restoredBitmap = new Bitmap(restoredPath);
            var baseline = ReadShapePixels(baselineBitmap); var hiddenChanges = CountControlTemplateExactChanges(baseline, ReadShapePixels(hiddenBitmap), region); var restoredChanges = CountControlTemplateExactChanges(baseline, ReadShapePixels(restoredBitmap), region);
            var stableGeometry = target is not null && bounds == PresentationControlBounds(target, chart);
            Check(glyph + "-real-glyph-pixels-enter-permanent-export", target is not null && hiddenSaved && restoredSaved && stableGeometry && hiddenChanges >= 3 && restoredChanges == 0);
            glyphProofs.Add(new { kind = glyph, target = target?.Name, text = target?.Text, liveBounds = C7Bounds(bounds), exportRegion = C7Bounds(region), hiddenPath, restoredPath, hiddenSaved, restoredSaved, hiddenChangedPixels = hiddenChanges, restoredChangedPixels = restoredChanges, geometryUnchanged = stableGeometry });
        }
        Mode(originalMode); SetProfessionalTooltipEnabled(chart, true); Flush();
        _professionalLinkingProbes.Add(new { schema = "professional-linking-native/v1", phase = "export", kind, artifacts, glyphProofs, selectedKeys = selectedKeys.Select(key => key.ToString()).ToArray(), selectionRange = range,
            comparison = "Same codec synchronous/asynchronous decoded pixels compare equally. Clearing transient Tooltip/Trackball preserves permanent selection/template export pixels; clearing selection changes them. No cross codec equality is assumed.", physicalInputValidated = false, desktopPresentCaptured = false, performanceMeasured = false });
    }

    private async Task VerifyProfessionalLinkingAnimatedCombinationAsync(Window window, Border host, Dictionary<string, bool> checks, Func<string, string, string> image)
    {
        var original = host.Child; var rows = HierarchyDemoData(); var chart = new TreemapChart { Name = "ProfessionalLinkingAnimatedHierarchy", Height = 570, ItemsSource = rows, IdPath = "Id", LabelPath = "Label", ChildrenPath = "Children", ValuePath = "Value", AnimationTriggers = ChartAnimationTriggers.Data, AnimationDuration = TimeSpan.FromSeconds(3) };
        chart.PlotAreaContent = "动画中的永久业务内容"; chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => LinkingCompactBody(chart, "AnimationPlot", "Content", "EngineeringMarks.Count", "图元 {0}"));
        chart.LegendItemTemplate = new FuncDataTemplate<ChartDataLegendItemContext>((_, _) => LinkingCompactBody(chart, "AnimationLegend", "Text", "Members.Count", "成员 {0}"));
        chart.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelItemContext>((_, _) => LinkingCompactBody(chart, "AnimationLabel", "Text", "Members[0].StableId", "ID {0}"));
        chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballContentTemplate = new FuncDataTemplate<ChartDataTrackballContext>((_, _) => LinkingCompactBody(chart, "AnimationTrack", "Readings[0].Text", "Readings.Count", "切片 {0}"));
        void Flush() { FlushControlTemplateWindow(window); FlushControlTemplateWindow(window); }
        try
        {
            host.Child = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "专业呈现组合 · 数据动画暂停帧导出", FontSize = 19 }, chart } }; Flush(); _ = chart.GetMarks();
            rows[0].Children[0].Children[0].Value += 15; Flush(); await Task.Delay(100); Flush(); chart.IsAnimationPaused = true; Flush();
            var initialPaused = chart.AnimationProgress; var initialState = chart.AnimationState; var beforeThemeBounds = chart.GetMarks().Select(mark => mark.Bounds).ToArray(); var beforeThemePlot = chart.PlotArea; var beforeThemeReadProgress = chart.AnimationProgress;
            var themePropertyChanges = new List<object>();
            static string? Simple(object? value) => value is null ? null : value is string or bool or int or double or float or Rect or Thickness or Size or ThemeVariant or IBrush ? value.ToString() : value.GetType().FullName;
            void ThemeChanged(object? sender, AvaloniaPropertyChangedEventArgs change) { if (themePropertyChanges.Count < 512) themePropertyChanges.Add(new { property = change.Property.Name, owner = change.Property.OwnerType.FullName, previous = Simple(change.OldValue), current = Simple(change.NewValue), state = chart.AnimationState.ToString(), progress = chart.AnimationProgress }); }
            chart.PropertyChanged += ThemeChanged;
            window.RequestedThemeVariant = ThemeVariant.Dark; chart.HighContrastMode = ChartHighContrastMode.Dark; Flush();
            var themeState = chart.AnimationState; var themeProgress = chart.AnimationProgress; var themeGeometryUnchanged = beforeThemeBounds.SequenceEqual(chart.GetMarks().Select(mark => mark.Bounds)); var afterThemePlot = chart.PlotArea; chart.PropertyChanged -= ThemeChanged;
            checks["professional-linking-animation-theme-keeps-paused-progress-and-geometry"] = themeState == initialState && themeProgress == initialPaused && themeGeometryUnchanged;
            var oldNodes = new HashSet<object>(ReferenceEqualityComparer.Instance);
            void Visit(IEnumerable<HierarchyDemoRecord> values, HashSet<object> destination) { foreach (var row in values) { destination.Add(row); Visit(row.Children, destination); } }
            Visit(rows, oldNodes); var replacement = HierarchyDemoData(); replacement.Move(0, 1); chart.ItemsSource = replacement; Flush();
            var currentNodes = new HashSet<object>(ReferenceEqualityComparer.Instance); Visit(replacement, currentNodes);
            chart.SelectDataRange(new Rect(0, 0, 1, 1)); Flush();
            var mark = chart.GetMarks().First(mark => mark.Members.Any(point => Equals(point.StableId, "api"))); chart.UpdateTrackball(mark.Position); Flush();
            var paused = chart.AnimationProgress; var state = chart.AnimationState;
            checks["professional-linking-animation-source-replace-reorder-retains-new-originals"] = chart.SelectedData.Count > 0 && chart.SelectedData.All(point => point.Item is not null && currentNodes.Contains(point.Item) && !oldNodes.Contains(point.Item)) && chart.TrackballReadings.Count > 0 && chart.TrackballReadings.All(reading => reading.Members.All(point => point.Item is not null && currentNodes.Contains(point.Item)));
            checks["professional-linking-animation-theme-range-and-permanent-templates-coexist"] = chart.HighContrastMode == ChartHighContrastMode.Dark && chart.DataSelectionRange == new Rect(0, 0, 1, 1) && chart.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "AnimationPlotBody") && chart.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "AnimationLabelBody");
            var path = image("animated-paused-frame", "png"); using (var bitmap = new RenderTargetBitmap(new PixelSize(1440, 1000), new Vector(96, 96))) { bitmap.Render(window); bitmap.Save(path); }
            var options = new ChartExportOptions { PixelSize = new PixelSize(800, 440), Background = window.Background }; var artifacts = new List<object>(); var codecHashes = new Dictionary<string, string>();
            foreach (var format in new[] { "sync-png", "async-png", "sync-jpeg", "async-jpeg" })
            {
                var target = image("animated-paused-" + format, format.EndsWith("jpeg", StringComparison.Ordinal) ? "jpg" : "png");
                var beforeState = chart.AnimationState.ToString(); var beforeProgress = chart.AnimationProgress;
                var saved = format switch { "sync-png" => chart.TryExportPng(target, options), "async-png" => await chart.TryExportPngAsync(target, options), "sync-jpeg" => chart.TryExportJpeg(target, options), _ => await chart.TryExportJpegAsync(target, options) };
                using var bitmap = new Bitmap(target); var hash = Convert.ToHexString(SHA256.HashData(ReadShapePixels(bitmap).Pixels)); var codec = format.EndsWith("jpeg", StringComparison.Ordinal) ? "jpeg" : "png"; if (format.StartsWith("sync", StringComparison.Ordinal)) codecHashes[codec] = hash;
                checks["professional-linking-animation-" + format + "-frozen-frame-and-export"] = saved && bitmap.PixelSize == options.PixelSize && hash == codecHashes[codec] && chart.IsAnimationPaused && chart.AnimationState == state && chart.AnimationProgress == paused && chart.SelectedData.Any(point => Equals(point.StableId, "api")) && chart.TrackballReadings.Count > 0;
                artifacts.Add(new { state = "animated-paused", format, path = target, saved, width = bitmap.PixelSize.Width, height = bitmap.PixelSize.Height, decodedBgraSha256 = hash,
                    beforeState, beforeProgress, afterState = chart.AnimationState.ToString(), afterProgress = chart.AnimationProgress, afterIsPaused = chart.IsAnimationPaused,
                    selectedOriginalsCurrent = chart.SelectedData.Count > 0 && chart.SelectedData.All(point => point.Item is not null && currentNodes.Contains(point.Item) && !oldNodes.Contains(point.Item)),
                    trackballOriginalsCurrent = chart.TrackballReadings.Count > 0 && chart.TrackballReadings.All(reading => reading.Members.All(point => point.Item is not null && currentNodes.Contains(point.Item))) });
            }
            checks["professional-linking-animation-native-running-frame-was-paused"] = initialState == ChartAnimationState.Paused && initialPaused > 0 && initialPaused < 1 && state == ChartAnimationState.Paused;
            chart.CancelAnimations(); Flush(); checks["professional-linking-animation-cancel-keeps-permanent-templates-and-selection"] = chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1 && chart.SelectedData.Any(point => Equals(point.StableId, "api")) && chart.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "AnimationPlotBody");
            _professionalLinkingProbes.Add(new { schema = "professional-linking-native/v1", phase = "animation-combination", image = path, initialPausedState = initialState.ToString(), initialPausedProgress = initialPaused, beforeThemeReadProgress, beforeThemePlot = C7Bounds(beforeThemePlot), afterThemePlot = C7Bounds(afterThemePlot), themePropertyChanges, themeChangedState = themeState.ToString(), themeChangedProgress = themeProgress, themeGeometryUnchanged, sourceReorderedWithNewObjects = true, selectionRange = chart.DataSelectionRange, pausedState = state.ToString(), pausedProgress = paused, finalState = chart.AnimationState.ToString(), artifacts, realNativeAnimation = true, deterministicClock = false, physicalInputValidated = false, performanceMeasured = false });
        }
        finally { chart.CancelAnimations(); chart.ClearTrackball(); chart.ItemsSource = null; host.Child = original; Flush(); }
    }
}
