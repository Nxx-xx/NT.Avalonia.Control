using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _c7IntegrationProbes = [];

    private static string? C7Brush(IBrush? brush) => brush is ISolidColorBrush solid ? $"{solid.Color}@{solid.Opacity:R}" : brush?.ToString();
    private static bool C7Color(IBrush? brush, Color color) => brush is ISolidColorBrush solid && solid.Color == color && solid.Opacity == 1;
    private static IEnumerable<IBrush>? C7Palette(ChartBase chart) => chart switch
    {
        CartesianChart value => value.ColorPalette, PolarChart value => value.ColorPalette,
        CircularChart value => value.ColorPalette, AnalysisChartBase value => value.Palette, EngineeringChartBase value => value.Palette, _ => null,
    };
    private static IEnumerable<GlyphRunDrawing> C7Glyphs(Drawing drawing)
    {
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var glyph in C7Glyphs(child)) yield return glyph;
        else if (drawing is GlyphRunDrawing glyph) yield return glyph;
    }
    private static object C7Bounds(Rect rect) => new { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height };
    private static object? C7Selected(ChartBase chart) => chart switch
    {
        CartesianChart value => value.SelectedPoint?.Item, PolarChart value => value.SelectedPoint?.Item,
        CircularChart value => value.SelectedPoint?.Item, AnalysisChartBase value => value.SelectedPoint?.Item,
        EngineeringChartBase value => value.SelectedPoint?.Members.FirstOrDefault().Item, _ => null,
    };
    private static void C7Build(C7IntegrationBoard board)
    {
        _ = board.Cartesian.PlotArea; _ = board.Radar.GetPoints(); _ = board.Circular.GetSlices();
        _ = board.Sparkline.GetPoints(); _ = board.Funnel.GetMarks(); _ = board.Mosaic.GetMarks(); _ = board.Map.GetMarks();
    }
    private static bool C7SourcesValid(C7IntegrationBoard board)
    {
        bool Original(ChartDataPoint point) => point.SourceIndex >= 0 && point.SourceIndex < board.Rows.Count && ReferenceEquals(board.Rows[point.SourceIndex], point.Item);
        return board.Line.CreateSnapshot().Points.Count == 4 && board.Line.CreateSnapshot().Points.All(Original) && board.Radar.GetPoints().Count == 4 && board.Radar.GetPoints().All(point => Original(point.Data)) &&
            board.Circular.GetSlices().Count == 4 && board.Circular.GetSlices().All(slice => Original(slice.Data)) &&
            board.Sparkline.GetPoints().Count == 4 && board.Sparkline.GetPoints().All(point => Original(point.Data)) &&
            board.Funnel.GetMarks().Count == 4 && board.Funnel.GetMarks().All(mark => Original(mark.Data)) &&
            board.Mosaic.DataError is null && board.Mosaic.GetMarks().Count == 4 && board.Mosaic.GetMarks().SelectMany(mark => mark.Members).All(Original) &&
            board.Map.DataError is null && board.Map.GetMarks().Count == 4 && board.Map.GetMarks().SelectMany(mark => mark.Members).All(Original);
    }
    private static void C7SelectFirst(C7IntegrationBoard board)
    {
        _ = board.Cartesian.NavigateToPoint(board.Line, 0); board.Cartesian.SelectedPoint = board.Cartesian.CurrentPoint;
        board.Radar.SelectPoint(board.Radar.GetPoints().Single(point => point.Data.SourceIndex == 0).Hit);
        board.Circular.SelectPoint(board.Circular.GetSlices().Single(slice => slice.Data.SourceIndex == 0).Hit);
        board.Funnel.SelectedIndex = 0;
        board.Mosaic.SelectedIndex = board.Mosaic.GetMarks().Select((mark, index) => (mark, index)).Single(pair => pair.mark.Members.Any(member => ReferenceEquals(member.Item, board.Rows[0]))).index;
        board.Map.SelectItem(board.MapLayer, board.Rows[0].Id);
    }
    private static bool C7HitsValid(C7IntegrationBoard board)
    {
        var row = board.Rows[0]; var cartesian = board.Cartesian.CurrentPoint;
        var radar = board.Radar.GetPoints().Single(point => point.Data.SourceIndex == 0);
        var circular = board.Circular.GetSlices().Single(slice => slice.Data.SourceIndex == 0);
        var spark = board.Sparkline.GetPoints().Single(point => point.Data.SourceIndex == 0);
        var funnel = board.Funnel.GetMarks().Single(mark => mark.Data.SourceIndex == 0);
        var mosaic = board.Mosaic.GetMarks().Single(mark => mark.Members.Any(member => ReferenceEquals(member.Item, row)));
        var map = board.Map.GetMarks().Single(mark => mark.Members.Any(member => ReferenceEquals(member.Item, row)));
        return cartesian is not null && ReferenceEquals(board.Cartesian.HitTestPoint(cartesian.Position)?.Item, row) &&
            ReferenceEquals(board.Radar.HitTest(radar.Position)?.Item, row) && ReferenceEquals(board.Circular.HitTest(circular.Anchor)?.Data.Item, row) &&
            ReferenceEquals(board.Sparkline.HitTest(spark.Position)?.Item, row) && ReferenceEquals(board.Funnel.HitTest(funnel.LabelPosition)?.Item, row) &&
            board.Mosaic.HitTest(mosaic.Position)?.Members.Any(member => ReferenceEquals(member.Item, row)) == true &&
            board.Map.HitTest(map.Position)?.Members.Any(member => ReferenceEquals(member.Item, row)) == true;
    }

    private async Task RunC7IntegrationSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        var directory = Path.GetDirectoryName(baseImage)!; Directory.CreateDirectory(directory); var stem = Path.GetFileNameWithoutExtension(baseImage);
        using var board = CreateC7IntegrationBoard(); var container = new Border { Padding = new Thickness(16), Child = board.View };
        var window = new Window
        {
            Title = "Charts · C7 八图族集成", Width = 1440, Height = 800, CanResize = false, RequestedThemeVariant = ThemeVariant.Light, Content = container,
        };
        var selectedRoots = new ChartBase[] { board.Cartesian, board.Radar, board.Circular, board.Funnel, board.Mosaic, board.Map };
        void Check(string name, bool passed) => checks["C7-integration-" + name] = passed;
        try
        {
            window.Show(_hostWindow); await Task.Delay(60); FlushControlTemplateWindow(window); C7Build(board); FlushControlTemplateWindow(window);
            Check("native-window", window.TryGetPlatformHandle() is { Handle: not 0 });
            Check("eight-roots", board.Charts.Length == 8 && board.Charts.All(chart => chart.Bounds.Width > 200 && chart.Bounds.Height == 250));
            Check("initial-original-sources", C7SourcesValid(board));
            C7SelectFirst(board); FlushControlTemplateWindow(window);
            Check("initial-six-selections", selectedRoots.All(chart => ReferenceEquals(C7Selected(chart), board.Rows[0])));
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
                await Task.Delay(20); FlushControlTemplateWindow(window); C7Build(board); FlushControlTemplateWindow(window);
                var foreground = mode.Contrast == ChartHighContrastMode.Light ? Colors.Black : mode.Contrast == ChartHighContrastMode.Dark ? Colors.White : Color.Parse(mode.Theme == ThemeVariant.Dark ? "#D7E0EE" : "#344054");
                Check(mode.Name + "-shared-preset", board.Charts.All(chart => chart.HighContrastMode == mode.Contrast));
                Check(mode.Name + "-theme-and-foreground", window.ActualThemeVariant == mode.Theme && board.Charts.All(chart => chart.ActualThemeVariant == mode.Theme && C7Color(chart.Foreground, foreground)));
                Check(mode.Name + "-typography", board.Charts.All(chart => chart.FontSize == 12));
                Check(mode.Name + "-original-sources", C7SourcesValid(board));
                Check(mode.Name + "-seven-original-hits", C7HitsValid(board));
                Check(mode.Name + "-six-selections-preserved", selectedRoots.All(chart => ReferenceEquals(C7Selected(chart), board.Rows[0])));
                Check(mode.Name + "-observers-attached", board.Rows.All(row => row.Subscribers > 0));
                if (mode.Contrast != ChartHighContrastMode.None)
                {
                    var palette = mode.Contrast == ChartHighContrastMode.Dark ? ChartContrastPalettes.Dark : ChartContrastPalettes.Light;
                    var canvas = mode.Contrast == ChartHighContrastMode.Dark ? Colors.Black : Colors.White;
                    Check(mode.Name + "-eight-canvases", board.Charts.All(chart => C7Color(chart.Background, canvas)));
                    Check(mode.Name + "-six-family-palettes", board.Charts.Where(chart => chart is not GaugeBase and not SparklineChart).All(chart => C7Palette(chart)?.Select(C7Brush).SequenceEqual(palette.Select(C7Brush)) == true));
                    Check(mode.Name + "-gauge-and-sparkline-colors", C7Brush(board.Gauge.ValueBrush) == C7Brush(palette[1]) && C7Brush(board.Sparkline.LineBrush) == C7Brush(palette[0]));
                }
                else Check(mode.Name + "-normal-transparent-canvases", board.Charts.All(chart => chart.Background is ISolidColorBrush solid && (solid.Color.A == 0 || solid.Opacity == 0)));
                var mapDrawing = new DrawingGroup(); using (var context = mapDrawing.Open()) board.Map.Render(context);
                var mapLabels = C7Glyphs(mapDrawing).Select(glyph => C7Brush(glyph.Foreground)).ToArray();
                Check(mode.Name + "-map-pale-labels-use-dark-text", C7Glyphs(mapDrawing).Any(glyph => C7Color(glyph.Foreground, Colors.Black)));
                var image = Path.Combine(directory, $"{stem}-c7-integration-{mode.Name}.png");
                using (var bitmap = new RenderTargetBitmap(new PixelSize(1440, 800), new Vector(96, 96))) { bitmap.Render(window); bitmap.Save(image); }
                _c7IntegrationProbes.Add(new
                {
                    schema = "c7-integration-native/v1", mode = mode.Name, theme = mode.Theme.ToString(), contrast = mode.Contrast.ToString(),
                    nativeHandleCreated = window.TryGetPlatformHandle() is { Handle: not 0 }, clientWidth = window.ClientSize.Width, clientHeight = window.ClientSize.Height, renderScaling = window.RenderScaling,
                    image, pixelWidth = 1440, pixelHeight = 800, mapLabelBrushes = mapLabels,
                    roots = board.Charts.Select(chart => new
                    {
                        kind = chart.GetType().Name, bounds = C7Bounds(chart.Bounds), foreground = C7Brush(chart.Foreground), background = C7Brush(chart.Background),
                        fontSize = chart.FontSize, palette = C7Palette(chart)?.Select(C7Brush).ToArray(), selectedId = (C7Selected(chart) as C7IntegrationRow)?.Id,
                        originalSelectedReference = ReferenceEquals(C7Selected(chart), board.Rows[0]),
                    }).ToArray(),
                    sources = board.Rows.Select(row => new { row.Id, row.Category, row.X, row.Value, row.Column, row.Row, row.Subscribers }).ToArray(),
                    physicalInputValidated = false, desktopPresentCaptured = false, screenshotMethod = "RenderTargetBitmap of a shown native Window", performanceMeasured = false,
                });
            }
            board.Contrast.SelectedItem = ChartHighContrastMode.None; window.RequestedThemeVariant = ThemeVariant.Light; FlushControlTemplateWindow(window); C7Build(board);
            Check("none-restores-six-default-palettes", board.Charts.Where(chart => chart is not GaugeBase and not SparklineChart).All(chart => C7Palette(chart) is null));
            var updateButton = board.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "C7 更新共享数据"));
            updateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); FlushControlTemplateWindow(window); C7Build(board); FlushControlTemplateWindow(window);
            Check("button-updates-shared-values", board.Rows[0].Value == 45 && board.Gauge.Value == 45 && board.Line.CreateSnapshot().Points[0].Point.Y == 45 &&
                board.Radar.GetPoints().Single(point => point.Data.SourceIndex == 0).Data.Point.Y == 45 && board.Circular.GetSlices().Single(slice => slice.Data.SourceIndex == 0).Data.Point.Y == 45 &&
                board.Sparkline.GetPoints().Single(point => point.Data.SourceIndex == 0).Data.Point.Y == 45 && board.Funnel.GetMarks().Single(mark => mark.Data.SourceIndex == 0).Data.Point.Y == 45 &&
                board.Mosaic.GetMarks().Single(mark => mark.Members.Any(member => ReferenceEquals(member.Item, board.Rows[0]))).Value == 45 && board.Map.GetMarks().Single(mark => mark.Members[0].SourceIndex == 0).Value == 45);
            Check("update-preserves-six-selections", selectedRoots.All(chart => ReferenceEquals(C7Selected(chart), board.Rows[0])));
            var beforeDetach = board.Rows.Select(row => row.Subscribers).ToArray();
            container.Child = null; FlushControlTemplateWindow(window); Check("detach-releases-all-source-observers", board.Rows.All(row => row.Subscribers == 0));
            board.Rows[0].Value = 50; Check("detached-source-remains-unobserved", board.Rows.All(row => row.Subscribers == 0));
            container.Child = board.View; FlushControlTemplateWindow(window); C7Build(board); FlushControlTemplateWindow(window);
            Check("reattach-recovers-sources-and-observers", C7SourcesValid(board) && board.Rows.All(row => row.Subscribers > 0) && board.Line.CreateSnapshot().Points[0].Point.Y == 50 && board.Map.GetMarks().Single(mark => mark.Members[0].SourceIndex == 0).Value == 50);
            board.Map.Zoom = 1.5; board.Map.PanX = .05; board.Mosaic.Zoom = 1.5;
            board.View.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "C7 恢复视图")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            FlushControlTemplateWindow(window); C7Build(board);
            Check("reset-command-contract", board.Map.Zoom == 1 && board.Map.PanX == 0 && board.Mosaic.Zoom == 1 && board.Cartesian.Viewport is null && selectedRoots.All(chart => C7Selected(chart) is null));
            _c7IntegrationProbes.Add(new
            {
                schema = "c7-integration-native/v1", phase = "update-detach-reattach-reset", updateInvokedThroughButtonEvent = true, resetInvokedThroughButtonEvent = true,
                observersBeforeDetach = beforeDetach, observersAfterReattach = board.Rows.Select(row => row.Subscribers).ToArray(),
                detachedUpdateValue = board.Rows[0].Value, originalSourcesValid = C7SourcesValid(board), physicalInputValidated = false, performanceMeasured = false,
            });
        }
        finally { window.Close(); }
    }
}
