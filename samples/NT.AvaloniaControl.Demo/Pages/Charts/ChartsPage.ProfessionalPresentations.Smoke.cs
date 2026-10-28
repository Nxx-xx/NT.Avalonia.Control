using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _professionalPresentationProbes = [];
    private static Control[] PresentationControls(ChartBase chart, bool legend) => chart.GetVisualDescendants().OfType<Control>()
        .Where(control => control.Name == (legend ? "ProfessionalLegendContent" : "ProfessionalLabelContent")).ToArray();
    private static Rect PresentationControlBounds(Control control, ChartBase chart) => control.TranslatePoint(default, chart) is { } point ? new Rect(point, control.Bounds.Size) : default;
    private static bool PresentationCurrent(ChartBase chart, ProfessionalPresentationBoard board)
    {
        bool Original(IReadOnlyList<ChartDataPoint> members, object? model) => ReferenceEquals(model, board.Model) && members.All(member =>
            member.SourceIndex >= 0 && member.SourceIndex < board.Model.Rows.Count && ReferenceEquals(member.Item, board.Model.Rows[member.SourceIndex]) && Equals(member.StableId, board.Model.Rows[member.SourceIndex].Id));
        return PresentationLabelItems(chart) is { Count: > 0 } labels && labels.All(item => Original(item.Members, item.BusinessDataContext)) &&
            PresentationLegendItems(chart) is { Count: > 0 } legends && legends.All(item => Original(item.Members, item.BusinessDataContext));
    }
    private static bool PresentationRetired(IEnumerable<(Control Control, object? Context)> previous) => previous.All(pair =>
        !ReferenceEquals(pair.Control.DataContext, pair.Context) || pair.Control.DataContext is null && pair.Control.GetVisualParent() is null && ((ILogical)pair.Control).LogicalParent is null);
    private static (Control Control, object? Context)[] PresentationContexts(ProfessionalPresentationBoard board) => board.Charts
        .SelectMany(chart => PresentationControls(chart, false).Concat(PresentationControls(chart, true))).Select(control => (control, control.DataContext)).ToArray();
    private static object PresentationProbe(ChartBase chart, ProfessionalPresentationBoard board) => new
    {
        chart.Name, kind = chart.GetType().Name, bounds = C7Bounds(chart.Bounds), legendBuilds = board.LegendBuilds[chart], labelBuilds = board.LabelBuilds[chart],
        legendItems = PresentationLegendItems(chart).Select(item => new
        {
            item.Text, kind = item.Kind.ToString(), key = item.Key?.ToString(), item.Value, item.Minimum, item.Maximum, anchor = C5Point(item.Anchor), bounds = C7Bounds(item.Bounds),
            members = item.Members.Select(member => new { member.SourceIndex, stableId = member.StableId?.ToString(), originalSourceReference = ReferenceEquals(member.Item, board.Model.Rows[member.SourceIndex]) }).ToArray(),
            originalBusinessContext = ReferenceEquals(item.BusinessDataContext, board.Model),
        }).ToArray(),
        labels = PresentationLabelItems(chart).Select(item => new
        {
            item.Text, item.Value, anchor = C5Point(item.Anchor), bounds = C7Bounds(item.Bounds),
            members = item.Members.Select(member => new { member.SourceIndex, stableId = member.StableId?.ToString(), originalSourceReference = ReferenceEquals(member.Item, board.Model.Rows[member.SourceIndex]) }).ToArray(),
            originalBusinessContext = ReferenceEquals(item.BusinessDataContext, board.Model),
        }).ToArray(),
        controls = PresentationControls(chart, false).Concat(PresentationControls(chart, true)).Select(control => new
        {
            control.Name, bounds = C7Bounds(control.Bounds), chartBounds = C7Bounds(PresentationControlBounds(control, chart)),
            texts = control.GetVisualDescendants().OfType<TextBlock>().Select(text => new { text.Name, text.Text, bounds = C7Bounds(PresentationControlBounds(text, chart)), foreground = C7Brush(text.Foreground), text.LineHeight }).ToArray(),
        }).ToArray(),
    };

    private async Task RunProfessionalPresentationSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        var directory = Path.GetDirectoryName(baseImage)!; Directory.CreateDirectory(directory); var stem = Path.GetFileNameWithoutExtension(baseImage);
        string Image(string suffix, string extension = "png") => Path.Combine(directory, $"{stem}-professional-presentations-{suffix}.{extension}");
        void Check(string name, bool value) => checks["professional-presentations-" + name] = value;
        using var board = CreateProfessionalPresentationBoard(); var container = new Border { Padding = new Thickness(18), Child = board.View };
        var window = new Window { Width = 1440, Height = 900, Title = "Charts · 专业图族共享图例和数据标签", CanResize = false, Content = container, RequestedThemeVariant = ThemeVariant.Light };
        try
        {
            window.Show(_hostWindow); await Task.Delay(50); FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            Check("native-four-family-layout", window.TryGetPlatformHandle() is { Handle: not 0 } && board.Charts.All(chart => chart.Bounds.Width > 400 && chart.Bounds.Height == 320));
            var modes = new[]
            {
                (Name: "normal-light", Theme: ThemeVariant.Light, Contrast: ChartHighContrastMode.None),
                (Name: "normal-dark", Theme: ThemeVariant.Dark, Contrast: ChartHighContrastMode.None),
                (Name: "hc-light", Theme: ThemeVariant.Light, Contrast: ChartHighContrastMode.Light),
                (Name: "hc-dark", Theme: ThemeVariant.Dark, Contrast: ChartHighContrastMode.Dark),
            };
            foreach (var mode in modes)
            {
                window.RequestedThemeVariant = mode.Theme; board.Contrast.SelectedItem = mode.Contrast;
                FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
                foreach (var chart in board.Charts)
                {
                    var labels = PresentationControls(chart, false); var legends = PresentationControls(chart, true);
                    Check(mode.Name + "-" + chart.Name + "-original-sources", PresentationCurrent(chart, board));
                    Check(mode.Name + "-" + chart.Name + "-actual-template-controls", labels.Length > 0 && labels.Length <= 4 && legends.Length > 0 && legends.Length <= 4 &&
                        labels.All(control => control.DataContext is ChartDataLabelItemContext) && legends.All(control => control.DataContext is ChartDataLegendItemContext));
                    Check(mode.Name + "-" + chart.Name + "-bounded-member-rendering", labels.All(control => control.GetVisualDescendants().OfType<TextBlock>().Count() == 3) &&
                        legends.All(control => control.GetVisualDescendants().OfType<TextBlock>().Count() == 3) && labels.Concat(legends).All(control => !control.GetVisualDescendants().OfType<ItemsControl>().Any()));
                    Check(mode.Name + "-" + chart.Name + "-theme-and-business-bindings", labels.Concat(legends).SelectMany(control => control.GetVisualDescendants().OfType<TextBlock>()).All(text => C7Brush(text.Foreground) == C7Brush(chart.Foreground)) &&
                        legends.SelectMany(control => control.GetVisualDescendants().OfType<TextBlock>()).Where(text => text.Name == "ProfessionalLegendBusinessTitle").All(text => text.Text == board.Model.Title));
                    Check(mode.Name + "-" + chart.Name + "-business-heading-fits-visible-legend", legends.All(control => control.GetVisualDescendants().OfType<TextBlock>()
                        .Where(text => text.Name == "ProfessionalLegendBusinessTitle").All(text => text.Bounds.Width > 0 && text.Bounds.Height > 0 &&
                            PresentationControlBounds(control, chart).Inflate(.01).Contains(PresentationControlBounds(text, chart)) && new Rect(chart.Bounds.Size).Inflate(.01).Contains(PresentationControlBounds(text, chart)))));
                    Check(mode.Name + "-" + chart.Name + "-labels-stay-clear-of-permanent-legend", labels.All(label => legends.All(legend =>
                        !PresentationControlBounds(label, chart).Intersects(PresentationControlBounds(legend, chart)))));
                    Check(mode.Name + "-" + chart.Name + "-passive-overlays", chart.GetVisualDescendants().OfType<Control>().Where(control => control.Name is "PART_ChartDataLegendOverlay" or "PART_ChartDataLabelsOverlay")
                        .Count(control => !control.IsHitTestVisible && !control.Focusable) == 2);
                    var labelBuilds = board.LabelBuilds[chart]; var legendBuilds = board.LegendBuilds[chart];
                    _ = RouteProfessionalReading(chart, window, 0); _ = RouteProfessionalReading(chart, window, 1);
                    _ = PresentationLabelItems(chart); _ = PresentationLegendItems(chart); FlushControlTemplateWindow(window);
                    Check(mode.Name + "-" + chart.Name + "-stable-controls-without-source-change", labels.SequenceEqual(PresentationControls(chart, false)) && legends.SequenceEqual(PresentationControls(chart, true)) &&
                        labelBuilds == board.LabelBuilds[chart] && legendBuilds == board.LegendBuilds[chart]);
                }
                Check(mode.Name + "-mosaic-full-aggregate-members", PresentationLabelItems(board.Mosaic).Any(item => item.Members.Select(member => member.SourceIndex).SequenceEqual(new[] { 0, 2 }) &&
                    item.EngineeringHit is { } hit && ReferenceEquals(item.Members, hit.Members)) && PresentationLegendItems(board.Mosaic).Any(item => item.Members.Select(member => member.SourceIndex).SequenceEqual(new[] { 0, 2 })));
                var image = Image(mode.Name);
                using (var bitmap = new RenderTargetBitmap(new PixelSize(1440, 900), new Vector(96, 96))) { bitmap.Render(window); bitmap.Save(image); }
                _professionalPresentationProbes.Add(new
                {
                    schema = "professional-presentations-native/v1", phase = "theme", mode = mode.Name, theme = mode.Theme.ToString(), contrast = mode.Contrast.ToString(), image,
                    nativeHandleCreated = window.TryGetPlatformHandle() is { Handle: not 0 }, renderScaling = window.RenderScaling,
                    charts = board.Charts.Select(chart => PresentationProbe(chart, board)).ToArray(),
                    physicalInputValidated = false, desktopPresentCaptured = false, screenshotMethod = "RenderTargetBitmap of a shown native Window", performanceMeasured = false,
                });
            }
            var previous = PresentationContexts(board); var stableControls = board.Charts.ToDictionary(chart => chart, chart => PresentationControls(chart, true));
            Invoke("呈现 更新标题");
            Check("live-business-header-keeps-template-controls", board.Charts.All(chart => stableControls[chart].SequenceEqual(PresentationControls(chart, true)) && PresentationControls(chart, true)
                .SelectMany(control => control.GetVisualDescendants().OfType<TextBlock>()).Where(text => text.Name == "ProfessionalLegendBusinessTitle").All(text => text.Text == board.Model.Title)));
            var replacementModel = new ProfessionalTooltipModel { Rows = board.Model.Rows, Title = "替换后的业务上下文" };
            previous = PresentationContexts(board); foreach (var chart in board.Charts) chart.DataContext = replacementModel;
            FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            Check("business-VM-replacement-refreshes-contexts", PresentationRetired(previous) && board.Charts.All(chart =>
                PresentationLabelItems(chart).All(item => ReferenceEquals(item.BusinessDataContext, replacementModel)) &&
                PresentationLegendItems(chart).All(item => ReferenceEquals(item.BusinessDataContext, replacementModel)) &&
                PresentationControls(chart, true).SelectMany(control => control.GetVisualDescendants().OfType<TextBlock>())
                    .Where(text => text.Name == "ProfessionalLegendBusinessTitle").All(text => text.Text == replacementModel.Title)));
            foreach (var chart in board.Charts) chart.DataContext = board.Model; FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            Invoke("呈现 更新数值");
            Check("INPC-updates-aggregate-and-single-label-values", board.Charts.All(chart => PresentationCurrent(chart, board)) &&
                PresentationLabelItems(board.Funnel).Any(item => item.Members[0].SourceIndex == 0 && item.Value == board.Model.Rows[0].Value) &&
                PresentationLabelItems(board.Mosaic).Any(item => item.Members.Count == 2 && item.Value == board.Model.Rows[0].Value + board.Model.Rows[2].Value));
            previous = PresentationContexts(board); var retired = board.Model.Rows.ToArray(); Invoke("呈现 替换来源");
            Check("source-replacement-releases-contexts-and-observers", PresentationRetired(previous) && retired.All(row => row.Subscribers == 0) && board.Charts.All(chart => PresentationCurrent(chart, board)));
            board.CappedOption.IsChecked = true; FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            Check("visible-control-budgets-are-enforced", board.Charts.All(chart => PresentationControls(chart, false).Length is > 0 and <= 2 && PresentationControls(chart, true).Length is > 0 and <= 2));
            board.CappedOption.IsChecked = false; FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            previous = PresentationContexts(board); board.LabelOption.IsChecked = false; FlushControlTemplateWindow(window);
            Check("label-template-off-keeps-independent-legend", board.Charts.All(chart => PresentationControls(chart, false).Length == 0 && PresentationControls(chart, true).Length > 0));
            board.LabelOption.IsChecked = true; board.LegendOption.IsChecked = false; FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            Check("legend-template-off-keeps-independent-labels", board.Charts.All(chart => PresentationControls(chart, false).Length > 0 && PresentationControls(chart, true).Length == 0));
            board.LegendOption.IsChecked = true; FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            var disabledControls = PresentationContexts(board); board.Group.IsEnabled = false; FlushControlTemplateWindow(window);
            Check("disabled-ancestor-keeps-passive-permanent-content", disabledControls.All(pair => ReferenceEquals(pair.Control.DataContext, pair.Context) && !pair.Control.IsEffectivelyEnabled) &&
                board.Charts.All(chart => PresentationControls(chart, false).Length > 0 && PresentationControls(chart, true).Length > 0));
            board.Group.IsEnabled = true; FlushControlTemplateWindow(window);
            var oldAnchors = PresentationLabelItems(board.Strip).Select(item => item.Anchor).ToArray(); Invoke("呈现 缩放平移");
            Check("zoom-pan-recomputes-label-anchors-with-original-identity", !oldAnchors.SequenceEqual(PresentationLabelItems(board.Strip).Select(item => item.Anchor)) && board.Charts.All(chart => PresentationCurrent(chart, board)));
            Invoke("呈现 恢复视图");
            Check("reset-keeps-permanent-presentations", board.Charts.All(chart => PresentationCurrent(chart, board) && PresentationControls(chart, false).Length > 0 && PresentationControls(chart, true).Length > 0));
            previous = PresentationContexts(board); board.Group.IsVisible = false; FlushControlTemplateWindow(window);
            Check("hidden-ancestor-releases-template-contexts", PresentationRetired(previous) && board.Charts.All(chart => PresentationControls(chart, false).Length == 0 && PresentationControls(chart, true).Length == 0));
            board.Group.IsVisible = true; FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            Check("visible-ancestor-restores-current-sources", board.Charts.All(chart => PresentationCurrent(chart, board) && PresentationControls(chart, false).Length > 0 && PresentationControls(chart, true).Length > 0));
            previous = PresentationContexts(board); container.Child = null; FlushControlTemplateWindow(window);
            Check("detach-releases-controls-contexts-and-observers", PresentationRetired(previous) && board.Model.Rows.All(row => row.Subscribers == 0));
            container.Child = board.View; FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            Check("reattach-restores-original-member-contexts", board.Charts.All(chart => PresentationCurrent(chart, board)) && board.Model.Rows.All(row => row.Subscribers > 0));
            await VerifyProfessionalPresentationExportsAsync(board.Funnel, "analysis", board, window, checks, Image);
            await VerifyProfessionalPresentationExportsAsync(board.Mosaic, "engineering", board, window, checks, Image);
            _professionalPresentationProbes.Add(new { schema = "professional-presentations-native/v1", phase = "lifecycle", title = board.Model.Title,
                sources = board.Model.Rows.Select(row => new { row.Id, row.Value, row.Subscribers }).ToArray(),
                charts = board.Charts.Select(chart => PresentationProbe(chart, board)).ToArray(), physicalInputValidated = false, performanceMeasured = false });

            void Invoke(string caption)
            {
                board.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, caption)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            }
        }
        finally { window.Close(); }
    }

    private async Task VerifyProfessionalPresentationExportsAsync(ChartBase chart, string kind, ProfessionalPresentationBoard board, Window window,
        Dictionary<string, bool> checks, Func<string, string, string> image)
    {
        void Check(string key, bool value) => checks[$"professional-presentations-export-{kind}-{key}"] = value;
        var exportSize = new PixelSize(640, 320);
        var options = new ChartExportOptions { PixelSize = exportSize, Background = window.Background, JpegQuality = 90 };
        var artifacts = new List<object>(); var baselines = new Dictionary<string, string>();
        foreach (var state in new[] { "legacy", "label", "legend", "both" })
        {
            SetProfessionalPresentationTemplates(chart, state is "legend" or "both" ? board.Legends[chart] : null, state is "label" or "both" ? board.Labels[chart] : null);
            FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            var frame = new Dictionary<string, string>();
            foreach (var format in new[] { "sync-png", "async-png", "sync-jpeg", "async-jpeg" })
            {
                var path = image($"{kind}-{state}-{format}", format.EndsWith("jpeg", StringComparison.Ordinal) ? "jpg" : "png");
                var saved = format switch
                {
                    "sync-png" => chart is EngineeringChartBase engineering ? engineering.TryExportPng(path, options) : ((AnalysisChartBase)chart).TryExportPng(path, options),
                    "async-png" => chart is EngineeringChartBase engineering ? await engineering.TryExportPngAsync(path, options) : await ((AnalysisChartBase)chart).TryExportPngAsync(path, options),
                    "sync-jpeg" => chart is EngineeringChartBase engineering ? engineering.TryExportJpeg(path, options) : ((AnalysisChartBase)chart).TryExportJpeg(path, options),
                    _ => chart is EngineeringChartBase engineering ? await engineering.TryExportJpegAsync(path, options) : await ((AnalysisChartBase)chart).TryExportJpegAsync(path, options),
                };
                using var bitmap = new Bitmap(path); var raster = ReadShapePixels(bitmap); var hash = Convert.ToHexString(SHA256.HashData(raster.Pixels));
                var codec = format.EndsWith("jpeg", StringComparison.Ordinal) ? "jpeg" : "png";
                if (format.StartsWith("sync", StringComparison.Ordinal)) frame[codec] = hash;
                Check(state + "-" + format + "-valid-size-and-sync-async-equality", saved && bitmap.PixelSize == options.PixelSize && frame[codec] == hash);
                if (state == "legacy") baselines[format] = hash;
                else Check(state + "-" + format + "-includes-permanent-template-pixels", baselines[format] != hash);
                Check(state + "-" + format + "-preserves-live-original-identity", PresentationCurrent(chart, board) &&
                    (state is "label" or "both" ? PresentationControls(chart, false).Length > 0 : PresentationControls(chart, false).Length == 0) &&
                    (state is "legend" or "both" ? PresentationControls(chart, true).Length > 0 : PresentationControls(chart, true).Length == 0));
                artifacts.Add(new { state, format, path, saved, width = bitmap.PixelSize.Width, height = bitmap.PixelSize.Height, decodedBgraSha256 = hash,
                    legacyBgraSha256 = baselines[format], differsFromLegacy = baselines[format] != hash, syncAsyncSameCodecEqual = frame[codec] == hash });
            }
        }
        SetProfessionalPresentationTemplates(chart, board.Legends[chart], board.Labels[chart]); FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
        using var baselineBitmap = new Bitmap(image($"{kind}-both-sync-png", "png")); var baseline = ReadShapePixels(baselineBitmap);
        var glyphProofs = new List<object>();
        foreach (var legend in new[] { false, true })
        {
            var target = PresentationControls(chart, legend).SelectMany(control => control.GetVisualDescendants().OfType<TextBlock>())
                .FirstOrDefault(text => text.Name == (legend ? "ProfessionalLegendText" : "ProfessionalLabelId") && !string.IsNullOrEmpty(text.Text));
            var oldOpacity = target?.Opacity ?? 1; var bounds = target is null ? default : PresentationControlBounds(target, chart);
            var scale = Math.Min(exportSize.Width / chart.Bounds.Width, exportSize.Height / chart.Bounds.Height);
            var region = new Rect((exportSize.Width - chart.Bounds.Width * scale) / 2 + bounds.X * scale,
                (exportSize.Height - chart.Bounds.Height * scale) / 2 + bounds.Y * scale, bounds.Width * scale, bounds.Height * scale);
            var hiddenPath = image($"{kind}-{(legend ? "legend" : "label")}-glyph-hidden", "png");
            var restoredPath = image($"{kind}-{(legend ? "legend" : "label")}-glyph-restored", "png");
            bool hiddenSaved;
            try
            {
                if (target is not null) target.Opacity = 0; FlushControlTemplateWindow(window);
                hiddenSaved = chart is EngineeringChartBase engineering ? engineering.TryExportPng(hiddenPath, options) : ((AnalysisChartBase)chart).TryExportPng(hiddenPath, options);
            }
            finally { if (target is not null) target.Opacity = oldOpacity; FlushControlTemplateWindow(window); }
            var restoredSaved = chart is EngineeringChartBase e ? e.TryExportPng(restoredPath, options) : ((AnalysisChartBase)chart).TryExportPng(restoredPath, options);
            using var hiddenBitmap = new Bitmap(hiddenPath); using var restoredBitmap = new Bitmap(restoredPath);
            var hidden = ReadShapePixels(hiddenBitmap); var restored = ReadShapePixels(restoredBitmap);
            var hiddenChanges = CountControlTemplateExactChanges(baseline, hidden, region); var restoredChanges = CountControlTemplateExactChanges(baseline, restored, region);
            var stableGeometry = target is not null && bounds == PresentationControlBounds(target, chart);
            Check((legend ? "legend" : "label") + "-real-template-glyph-pixels-in-export", hiddenSaved && restoredSaved && stableGeometry && hiddenChanges >= 3 && restoredChanges == 0);
            glyphProofs.Add(new { kind = legend ? "legend" : "label", target = target?.Name, text = target?.Text, liveBounds = C7Bounds(bounds), exportRegion = C7Bounds(region),
                hiddenPath, restoredPath, hiddenSaved, restoredSaved, hiddenChangedPixels = hiddenChanges, restoredChangedPixels = restoredChanges, geometryUnchanged = stableGeometry });
        }
        _professionalPresentationProbes.Add(new { schema = "professional-presentations-native/v1", phase = "export", kind,
            comparison = "Each PNG/JPEG synchronous export is compared with its asynchronous counterpart using decoded pixels; labels and legends separately change the same chart's legacy raster. Hiding and restoring one real template TextBlock proves its glyph pixels reach PNG at the unchanged chart geometry. No cross-codec equality is assumed.",
            artifacts, glyphProofs, chart = PresentationProbe(chart, board), physicalInputValidated = false, desktopPresentCaptured = false, performanceMeasured = false });
    }
}
