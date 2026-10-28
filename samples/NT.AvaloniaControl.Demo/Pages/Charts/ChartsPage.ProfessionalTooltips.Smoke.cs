using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    private readonly List<object> _professionalTooltipProbes = [];
    private static Control? ProfessionalContent(ChartBase chart) => chart.GetVisualDescendants().OfType<Control>().SingleOrDefault(control => control.Name == "ProfessionalTooltipContent");
    private static ChartDataTooltipContext? ProfessionalReading(ChartBase chart) => ProfessionalContent(chart)?.DataContext as ChartDataTooltipContext;
    private static Border? ProfessionalChrome(ChartBase chart) => chart.GetVisualDescendants().OfType<Border>().SingleOrDefault(control => control.Name == "PART_ChartDataTooltip");
    private static TextBlock? ProfessionalText(ChartBase chart, string name) => chart.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(control => control.Name == name);
    private static Point ProfessionalAnchor(ChartBase chart, int sourceIndex) => chart is EngineeringChartBase engineering
        ? engineering.GetMarks().First(mark => mark.Members.Any(member => member.SourceIndex == sourceIndex)).Position
        : ((AnalysisChartBase)chart).GetMarks().Single(mark => mark.Data.SourceIndex == sourceIndex).LabelPosition;
    private static object RouteProfessionalReading(ChartBase chart, Window window, int sourceIndex)
    {
        var anchor = ProfessionalAnchor(chart, sourceIndex);
        var windowAnchor = chart.TranslatePoint(anchor, window) ?? throw new InvalidOperationException("The professional chart is not attached to its native host.");
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var args = new PointerEventArgs(InputElement.PointerMovedEvent, chart, pointer, window, windowAnchor, 1, default, KeyModifiers.None);
        chart.RaiseEvent(args); FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
        return new { sourceIndex, requestedAnchor = C5Point(anchor), windowAnchor = C5Point(windowAnchor), routedAnchor = C5Point(args.GetPosition(chart)), syntheticRoutedEvent = true };
    }
    private static bool ProfessionalSourceIdentity(ChartBase chart, ProfessionalTooltipModel model)
    {
        var reading = ProfessionalReading(chart);
        return reading is not null && ReferenceEquals(reading.BusinessDataContext, model) && reading.Members.Count > 0 && reading.Members.All(member =>
            member.SourceIndex >= 0 && member.SourceIndex < model.Rows.Count && ReferenceEquals(member.Item, model.Rows[member.SourceIndex]) && Equals(member.StableId, model.Rows[member.SourceIndex].Id));
    }
    private static bool ProfessionalValueCurrent(ChartBase chart, ProfessionalTooltipModel model)
    {
        var reading = ProfessionalReading(chart);
        var expected = chart is MosaicChart ? model.Rows[0].Value + model.Rows[2].Value : model.Rows[0].Value;
        return reading?.EngineeringHit is { } engineering ? engineering.Mark.Value == expected : reading?.PointHit?.Point.Y == expected;
    }
    private static bool ProfessionalActiveSourceIdentity(ChartBase chart, ProfessionalTooltipModel model)
    {
        bool Original(int index, object? item, object? id) => index >= 0 && index < model.Rows.Count &&
            ReferenceEquals(item, model.Rows[index]) && Equals(id, model.Rows[index].Id);
        if (chart is EngineeringChartBase engineering) return engineering.ActivePoint is { Members.Count: > 0 } active &&
            active.Members.All(member => Original(member.SourceIndex, member.Item, member.StableId));
        return ((AnalysisChartBase)chart).ActivePoint is { } point && Original(point.Index, point.Item, point.StableId);
    }
    private static bool ProfessionalReleased(Control? content) => content is null || content.DataContext is null && content.GetVisualParent() is null && ((ILogical)content).LogicalParent is null;
    private static object ProfessionalReadingProbe(ChartBase chart, ProfessionalTooltipBoard board) => new
    {
        kind = chart.GetType().Name, bounds = C7Bounds(chart.Bounds), builds = board.Builds[chart],
        context = ProfessionalReading(chart) is { } reading ? new
        {
            reading.Text, anchor = C5Point(reading.Anchor), businessTitle = (reading.BusinessDataContext as ProfessionalTooltipModel)?.Title,
            businessContextIsOriginalVM = ReferenceEquals(reading.BusinessDataContext, board.Model),
            members = reading.Members.Select(member => new { member.SourceIndex, stableId = member.StableId?.ToString(),
                originalSourceReference = member.SourceIndex >= 0 && member.SourceIndex < board.Model.Rows.Count && ReferenceEquals(member.Item, board.Model.Rows[member.SourceIndex]) }).ToArray(),
        } : null,
        chrome = ProfessionalChrome(chart) is { } chrome ? new { bounds = C7Bounds(chrome.Bounds), background = C7Brush(chrome.Background), border = C7Brush(chrome.BorderBrush) } : null,
        texts = chart.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Name?.StartsWith("ProfessionalTooltip", StringComparison.Ordinal) == true)
            .Select(text => new { text.Name, text.Text, foreground = C7Brush(text.Foreground), text.FontSize }).ToArray(),
    };

    private async Task RunProfessionalTooltipSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        var directory = Path.GetDirectoryName(baseImage)!; Directory.CreateDirectory(directory); var stem = Path.GetFileNameWithoutExtension(baseImage);
        string Image(string suffix, string extension = "png") => Path.Combine(directory, $"{stem}-professional-tooltips-{suffix}.{extension}");
        void Check(string name, bool value) => checks["professional-tooltips-" + name] = value;
        using var board = CreateProfessionalTooltipBoard(); var container = new Border { Padding = new Thickness(18), Child = board.View };
        var window = new Window { Width = 1440, Height = 900, Title = "Charts · 专业图族业务提示", CanResize = false, Content = container, RequestedThemeVariant = ThemeVariant.Light };
        try
        {
            window.Show(_hostWindow); await Task.Delay(50); FlushControlTemplateWindow(window);
            Check("native-four-family-layout", window.TryGetPlatformHandle() is { Handle: not 0 } && board.Charts.All(chart => chart.Bounds.Width > 400 && chart.Bounds.Height == 270));
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
                FlushControlTemplateWindow(window); var routes = new List<object>();
                foreach (var chart in board.Charts)
                {
                    routes.Add(RouteProfessionalReading(chart, window, 0)); var content = ProfessionalContent(chart); var builds = board.Builds[chart];
                    Check(mode.Name + "-" + chart.Name + "-original-source", ProfessionalSourceIdentity(chart, board.Model));
                    Check(mode.Name + "-" + chart.Name + "-raw-or-aggregate-members", ProfessionalReading(chart) is { } reading &&
                        reading.Members.Select(member => member.SourceIndex).SequenceEqual(chart is MosaicChart ? new[] { 0, 2 } : new[] { 0 }));
                    routes.Add(RouteProfessionalReading(chart, window, 1));
                    Check(mode.Name + "-" + chart.Name + "-control-reused-between-readings", content is not null && ReferenceEquals(content, ProfessionalContent(chart)) && builds == board.Builds[chart] &&
                        ProfessionalReading(chart)?.Members.Count == 1 && ProfessionalReading(chart)?.Members[0].SourceIndex == 1);
                    routes.Add(RouteProfessionalReading(chart, window, 0));
                    var chrome = ProfessionalChrome(chart); var overlay = chart.GetVisualDescendants().OfType<Control>().SingleOrDefault(item => item.Name == "PART_ChartDataTooltipOverlay");
                    var background = chart is EngineeringChartBase e ? e.TooltipBackground : ((AnalysisChartBase)chart).TooltipBackground;
                    var foreground = chart is EngineeringChartBase e2 ? e2.TooltipForeground : ((AnalysisChartBase)chart).TooltipForeground;
                    Check(mode.Name + "-" + chart.Name + "-bounded-passive-overlay", overlay is { IsHitTestVisible: false, Focusable: false } && chrome is not null &&
                        chrome.Bounds.Left >= 8 && chrome.Bounds.Top >= 8 && chrome.Bounds.Right <= chart.Bounds.Width - 8 && chrome.Bounds.Bottom <= chart.Bounds.Height - 8);
                    Check(mode.Name + "-" + chart.Name + "-theme-resource-and-business-bindings", chrome is not null && C7Brush(chrome.Background) == C7Brush(background) &&
                        ProfessionalText(chart, "ProfessionalTooltipBusinessTitle")?.Text == board.Model.Title && ProfessionalText(chart, "ProfessionalTooltipBadge")?.Text == "业务读数 · 原始来源" &&
                        C7Brush(ProfessionalText(chart, "ProfessionalTooltipBusinessTitle")?.Foreground) == C7Brush(foreground));
                    Check(mode.Name + "-" + chart.Name + "-bounded-template-node-count", ProfessionalContent(chart)?.GetVisualDescendants().OfType<TextBlock>().Count() == 6 &&
                        !ProfessionalContent(chart)!.GetVisualDescendants().OfType<ItemsControl>().Any());
                }
                var image = Image(mode.Name);
                using (var bitmap = new RenderTargetBitmap(new PixelSize(1440, 900), new Vector(96, 96))) { bitmap.Render(window); bitmap.Save(image); }
                _professionalTooltipProbes.Add(new
                {
                    schema = "professional-tooltip-native/v1", phase = "theme", mode = mode.Name, theme = mode.Theme.ToString(), contrast = mode.Contrast.ToString(), image,
                    nativeHandleCreated = window.TryGetPlatformHandle() is { Handle: not 0 }, renderScaling = window.RenderScaling,
                    routes, readings = board.Charts.Select(chart => ProfessionalReadingProbe(chart, board)).ToArray(),
                    physicalInputValidated = false, desktopPresentCaptured = false, screenshotMethod = "RenderTargetBitmap of a shown native Window", performanceMeasured = false,
                });
            }
            var previous = board.Charts.ToDictionary(chart => chart, ProfessionalContent);
            Invoke("提示 更新业务标题");
            Check("live-business-header-without-control-replacement", board.Charts.All(chart => ReferenceEquals(previous[chart], ProfessionalContent(chart)) && ProfessionalText(chart, "ProfessionalTooltipBusinessTitle")?.Text == board.Model.Title));
            board.View.Resources["ProfessionalTooltipBadgeText"] = "资源已更新"; FlushControlTemplateWindow(window);
            Check("logical-resource-update-reaches-live-template", board.Charts.All(chart => ProfessionalText(chart, "ProfessionalTooltipBadge")?.Text == "资源已更新"));
            Invoke("提示 更新数值");
            Check("INPC-releases-old-readings-and-uses-current-values", previous.Values.All(ProfessionalReleased) && board.Charts.All(chart => ProfessionalSourceIdentity(chart, board.Model) && ProfessionalValueCurrent(chart, board.Model)));
            previous = board.Charts.ToDictionary(chart => chart, ProfessionalContent); var retired = board.Model.Rows.ToArray();
            Invoke("提示 替换数据源");
            foreach (var chart in board.Charts) _ = RouteProfessionalReading(chart, window, 0);
            Check("source-replacement-releases-old-contexts-and-observers", previous.Values.All(ProfessionalReleased) && retired.All(row => row.Subscribers == 0) && board.Charts.All(chart => ProfessionalSourceIdentity(chart, board.Model)));
            previous = board.Charts.ToDictionary(chart => chart, ProfessionalContent); board.EnabledOption.IsChecked = false; FlushControlTemplateWindow(window);
            Check("disabled-ancestor-releases-live-readings", previous.Values.All(ProfessionalReleased) && board.Charts.All(chart => ProfessionalContent(chart) is null));
            board.EnabledOption.IsChecked = true; FlushControlTemplateWindow(window); foreach (var chart in board.Charts) _ = RouteProfessionalReading(chart, window, 0);
            previous = board.Charts.ToDictionary(chart => chart, ProfessionalContent); board.Group.IsVisible = false; FlushControlTemplateWindow(window);
            Check("hidden-ancestor-releases-live-readings", previous.Values.All(ProfessionalReleased) && board.Charts.All(chart => ProfessionalContent(chart) is null));
            board.Group.IsVisible = true; FlushControlTemplateWindow(window); foreach (var chart in board.Charts) _ = RouteProfessionalReading(chart, window, 0);
            previous = board.Charts.ToDictionary(chart => chart, ProfessionalContent); board.TooltipOption.IsChecked = false; FlushControlTemplateWindow(window);
            Check("tooltip-option-releases-controls", previous.Values.All(ProfessionalReleased) && board.Charts.All(chart => ProfessionalContent(chart) is null));
            board.TooltipOption.IsChecked = true; FlushControlTemplateWindow(window); foreach (var chart in board.Charts) _ = RouteProfessionalReading(chart, window, 0);
            previous = board.Charts.ToDictionary(chart => chart, ProfessionalContent); board.TemplateOption.IsChecked = false; FlushControlTemplateWindow(window);
            Check("template-off-releases-controls-for-legacy-readout", previous.Values.All(ProfessionalReleased) && board.Charts.All(chart => ProfessionalContent(chart) is null));
            board.TemplateOption.IsChecked = true; FlushControlTemplateWindow(window); foreach (var chart in board.Charts) _ = RouteProfessionalReading(chart, window, 0);
            previous = board.Charts.ToDictionary(chart => chart, ProfessionalContent); container.Child = null; FlushControlTemplateWindow(window);
            Check("detach-releases-all-contexts-and-source-observers", previous.Values.All(ProfessionalReleased) && board.Model.Rows.All(row => row.Subscribers == 0));
            container.Child = board.View; FlushControlTemplateWindow(window); foreach (var chart in board.Charts) _ = RouteProfessionalReading(chart, window, 0);
            Check("reattach-recovers-current-original-readings", board.Charts.All(chart => ProfessionalSourceIdentity(chart, board.Model)) && board.Model.Rows.All(row => row.Subscribers > 0));
            await VerifyProfessionalTooltipExportsAsync(board.Funnel, "analysis", board, window, checks, Image);
            await VerifyProfessionalTooltipExportsAsync(board.Mosaic, "engineering", board, window, checks, Image);
            previous = board.Charts.ToDictionary(chart => chart, ProfessionalContent); Invoke("提示 恢复视图");
            Check("reset-releases-transient-controls", previous.Values.All(ProfessionalReleased) && board.Charts.All(chart => ProfessionalContent(chart) is null));
            _professionalTooltipProbes.Add(new { schema = "professional-tooltip-native/v1", phase = "lifecycle", title = board.Model.Title,
                sources = board.Model.Rows.Select(row => new { row.Id, row.Value, row.Subscribers }).ToArray(), builds = board.Charts.Select(chart => new { chart.Name, count = board.Builds[chart] }).ToArray(),
                physicalInputValidated = false, performanceMeasured = false });

            void Invoke(string caption)
            {
                board.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, caption)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                FlushControlTemplateWindow(window); FlushControlTemplateWindow(window);
            }
        }
        finally { window.Close(); }
    }

    private async Task VerifyProfessionalTooltipExportsAsync(ChartBase chart, string kind, ProfessionalTooltipBoard board, Window window,
        Dictionary<string, bool> checks, Func<string, string, string> image)
    {
        void Check(string key, bool value) => checks[$"professional-tooltips-export-{kind}-{key}"] = value;
        if (chart is AnalysisChartBase a) { a.ShowLabels = false; a.ShowValues = false; }
        if (chart is ComparisonChartBase e) { e.ShowLabels = false; e.ShowValues = false; }
        FlushControlTemplateWindow(window); _ = RouteProfessionalReading(chart, window, 0);
        var options = new ChartExportOptions { PixelSize = new PixelSize(640, 320), Background = window.Background, JpegQuality = 90 };
        var artifacts = new List<object>(); var baselines = new Dictionary<string, string>();
        foreach (var state in new[] { "disabled", "legacy", "template" })
        {
            SetProfessionalTooltipEnabled(chart, state != "disabled");
            SetProfessionalTemplate(chart, state == "template" ? board.Templates[chart] : null);
            FlushControlTemplateWindow(window); _ = RouteProfessionalReading(chart, window, 0);
            var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context);
            var glyphText = string.Concat(C7Glyphs(drawing).Select(glyph => glyph.GlyphRun is { } run ? new string(run.Characters.Span) : ""));
            var legacyToken = chart is AnalysisChartBase ? board.Model.Rows[0].Label : board.Model.Rows[0].Column;
            Check(state + "-live-legacy-or-template-evidence", state == "legacy" ? glyphText.Contains(legacyToken, StringComparison.Ordinal) :
                state == "template" ? ProfessionalSourceIdentity(chart, board.Model) && !glyphText.Contains(legacyToken, StringComparison.Ordinal) : ProfessionalContent(chart) is null);
            foreach (var format in new[] { "sync-png", "async-png", "async-jpeg" })
            {
                var path = image($"{kind}-{state}-{format}", format.EndsWith("jpeg", StringComparison.Ordinal) ? "jpg" : "png");
                var saved = format switch
                {
                    "sync-png" => chart is EngineeringChartBase engineering ? engineering.TryExportPng(path, options) : ((AnalysisChartBase)chart).TryExportPng(path, options),
                    "async-png" => chart is EngineeringChartBase engineering ? await engineering.TryExportPngAsync(path, options) : await ((AnalysisChartBase)chart).TryExportPngAsync(path, options),
                    _ => chart is EngineeringChartBase engineering ? await engineering.TryExportJpegAsync(path, options) : await ((AnalysisChartBase)chart).TryExportJpegAsync(path, options),
                };
                using var bitmap = new Bitmap(path); var raster = ReadShapePixels(bitmap); var hash = Convert.ToHexString(SHA256.HashData(raster.Pixels));
                if (state == "disabled") baselines[format] = hash;
                var same = baselines[format] == hash;
                Check(state + "-" + format + "-excludes-transient-content", saved && bitmap.PixelSize == options.PixelSize && same);
                Check(state + "-" + format + "-retains-source-identity", ProfessionalActiveSourceIdentity(chart, board.Model) && (state != "template" || ProfessionalSourceIdentity(chart, board.Model)));
                artifacts.Add(new { state, format, path, saved, width = bitmap.PixelSize.Width, height = bitmap.PixelSize.Height, decodedBgraSha256 = hash, disabledBaselineBgraSha256 = baselines[format], identicalToDisabledTooltip = same });
            }
        }
        SetProfessionalTooltipEnabled(chart, true); SetProfessionalTemplate(chart, board.Templates[chart]); FlushControlTemplateWindow(window);
        _professionalTooltipProbes.Add(new { schema = "professional-tooltip-native/v1", phase = "export", kind,
            comparison = "Each format compares decoded pixels with the same chart, active data mark and its tooltip disabled; no cross-codec equality is assumed.",
            artifacts, physicalInputValidated = false, performanceMeasured = false });
    }
}
