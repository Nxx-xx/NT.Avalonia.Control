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
    private readonly List<SelectionRangeEvidence> _selectionRangeEvidence = [];

    private async Task RunSelectionRangeSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _selectionRangeExamples!.IsExpanded = true; await SmokeFlushAsync();
        _selectionRangeExamples.BringIntoView(); await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
            var board = CreateSelectionRangeBoard(false);
            var window = new Window { Title = "Charts · 系列选择与范围联动", Width = 1160, Height = 990,
                RequestedThemeVariant = theme, Content = new Border { Padding = new Thickness(18), Child = board.View } };
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                var chart = board.Primary; var other = board.Secondary; var selector = board.Horizontal;
                window.Background = Brush(dark ? "#111722" : "#F8FAFC");
                window.Foreground = Brush(dark ? "#EAF0FB" : "#172238");
                ((Border)window.Content!).Background = window.Background;
                foreach (var item in new[] { chart, other })
                {
                    item.Background = window.Background; item.Foreground = Brush(dark ? "#EAF0FB" : "#172238");
                    item.IsPanEnabled = false; item.IsTooltipEnabled = false;
                }
                FlushControlTemplateWindow(window);
                void Check(string name, bool value) => checks[$"选择范围{label}{name}"] = value;
                static bool Near(double a, double b) => Math.Abs(a - b) < 1e-7;
                bool Fractions(CartesianChart item, ChartInteractionAxes axis, double lower, double upper) =>
                    item.GetVisibleFraction(axis) is { } range && Near(range.Lower, lower) && Near(range.Upper, upper);
                Point Project(double x, double y) => new(chart.PlotArea.Left + chart.PlotArea.Width * x / 100, chart.PlotArea.Bottom - chart.PlotArea.Height * y / 100);
                Check("原生窗口和横竖范围条布局", window.TryGetPlatformHandle() is { Handle: not 0 } && chart.Bounds.Width > 700 && selector.Bounds.Width > 700 && board.Vertical.Bounds.Height > 100);
                SelectionRangeClick(chart, window, Project(30, 65));
                Check("点击真实折线选择整系列", chart.SelectedSeries is { Count: 1 } && ReferenceEquals(chart.SelectedSeries[0], chart.Series[0]));
                SelectionRangeClick(chart, window, Project(45, 88), KeyModifiers.Control);
                Check("Ctrl散点追加系列", chart.SelectedSeries is { Count: 2 } && ReferenceEquals(chart.SelectedSeries[1], chart.Series[2]));
                SelectionRangeClick(chart, window, Project(30, 65), KeyModifiers.Control);
                Check("Ctrl重复点击切换系列", chart.SelectedSeries is { Count: 1 } && ReferenceEquals(chart.SelectedSeries[0], chart.Series[2]));
                SelectionRangeClick(chart, window, Project(40, 20));
                Check("柱体点击选择整系列", chart.SelectedSeries is { Count: 1 } && ReferenceEquals(chart.SelectedSeries[0], chart.Series[1]));
                chart.SeriesSelectionMode = ChartSelectionMode.SingleDeselect;
                SelectionRangeClick(chart, window, Project(40, 20));
                Check("SingleDeselect再次点击取消", chart.SelectedSeries is { Count: 0 });
                chart.SeriesSelectionMode = ChartSelectionMode.Multiple;
                chart.SelectSeries(chart.Series[0]);
                void Cancel(object? sender, ChartSeriesSelectionChangingEventArgs args) { if (args.CanCancel) args.Cancel = true; }
                chart.SeriesSelectionChanging += Cancel;
                SelectionRangeClick(chart, window, Project(45, 88));
                Check("可取消前置事件保持旧系列", chart.SelectedSeries is { Count: 1 } && ReferenceEquals(chart.SelectedSeries[0], chart.Series[0]));
                chart.SeriesSelectionChanging -= Cancel;
                chart.ClearSelection(); chart.ClearSeriesSelection(); FlushControlTemplateWindow(window);
                var baseline = TemplateChartRaster(chart);
                chart.SelectSeries(chart.Series[0]); FlushControlTemplateWindow(window); var faded = TemplateChartRaster(chart);
                var changedPixels = CountControlTemplateExactChanges(baseline, faded, chart.PlotArea);
                chart.ClearSeriesSelection(); FlushControlTemplateWindow(window); var restored = TemplateChartRaster(chart);
                var restoreChanged = CountControlTemplateExactChanges(baseline, restored, chart.PlotArea);
                Check("系列淡化有正负像素并精确恢复", changedPixels >= 100 && restoreChanged == 0);
                chart.SelectSeries(chart.Series[2]); chart.Series[2].IsVisible = false; FlushControlTemplateWindow(window);
                Check("隐藏系列强制清理选择", chart.SelectedSeries is { Count: 0 });
                chart.Series[2].IsVisible = true;
                selector.Range = new(.2, .65); FlushControlTemplateWindow(window);
                Check("范围条驱动主图与不同范围从图", chart.VisibleRange is { } a && other.VisibleRange is { } b && Near(a.XMinimum, 20) && Near(a.XMaximum, 65) && Near(b.XMinimum, 200) && Near(b.XMaximum, 650));
                var lower = selector.GetVisualDescendants().OfType<Control>().Single(item => item.Name == "PART_LowerHandle");
                var start = lower.TranslatePoint(new Point(lower.Bounds.Width / 2, lower.Bounds.Height / 2), selector)!.Value;
                var drag = SelectionRangeDrag(selector, window, start, start + new Vector((selector.Bounds.Width - selector.HandleSize) * .1, 0));
                var expectedLower = .2 + drag.Delta.X / (selector.Bounds.Width - selector.HandleSize);
                Check("横向拖柄通过真实路由改变端点", drag.Captured && !selector.IsDragging && Near(selector.Range.Lower, expectedLower) && Near(selector.Range.Upper, .65) && Fractions(other, ChartInteractionAxes.X, expectedLower, .65));
                selector.Range = new(.2, .6); FlushControlTemplateWindow(window);
                var center = new Point(selector.HandleSize / 2 + .4 * (selector.Bounds.Width - selector.HandleSize), selector.Bounds.Height / 2);
                var move = SelectionRangeDrag(selector, window, center, center + new Vector((selector.Bounds.Width - selector.HandleSize) * .08, 0));
                var expectedMove = move.Delta.X / (selector.Bounds.Width - selector.HandleSize);
                Check("中段拖动平移并保持跨度", move.Captured && Near(selector.Range.Lower, .2 + expectedMove) && Near(selector.Range.Upper, .6 + expectedMove));
                selector.Range = new(.2, .6); FlushControlTemplateWindow(window); lower.Focus();
                lower.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }); FlushControlTemplateWindow(window);
                Check("聚焦端点方向键调整", Near(selector.Range.Lower, .22) && Near(selector.Range.Upper, .6));
                board.Vertical.Range = new(.2, .8); FlushControlTemplateWindow(window);
                var verticalLower = board.Vertical.GetVisualDescendants().OfType<Control>().Single(item => item.Name == "PART_LowerHandle");
                var verticalStart = verticalLower.TranslatePoint(new Point(verticalLower.Bounds.Width / 2, verticalLower.Bounds.Height / 2), board.Vertical)!.Value;
                var verticalDrag = SelectionRangeDrag(board.Vertical, window, verticalStart, verticalStart - new Vector(0, (board.Vertical.Bounds.Height - board.Vertical.HandleSize) * .1));
                var expectedY = .2 - verticalDrag.Delta.Y / (board.Vertical.Bounds.Height - board.Vertical.HandleSize);
                Check("竖向拖柄增加向上且仅改变Y", verticalDrag.Captured && Fractions(chart, ChartInteractionAxes.Y, expectedY, .8) && Fractions(other, ChartInteractionAxes.Y, 0, 1));
                chart.SetNormalizedViewport(new(.1, .7, .25, .75)); FlushControlTemplateWindow(window);
                board.Group.Axes = ChartInteractionAxes.Y; board.Group.SynchronizeFrom(chart); FlushControlTemplateWindow(window);
                Check("按Y联动保留从图X", Fractions(other, ChartInteractionAxes.X, .1, .7) && Fractions(other, ChartInteractionAxes.Y, .25, .75));
                board.Group.Axes = ChartInteractionAxes.Both; chart.SetNormalizedViewport(new(.3, .8, .1, .6)); FlushControlTemplateWindow(window);
                Check("双轴联动", Fractions(other, ChartInteractionAxes.X, .3, .8) && Fractions(other, ChartInteractionAxes.Y, .1, .6));
                board.Group.CoordinateMode = ChartSynchronizationCoordinateMode.DataCoordinates; board.Group.Axes = ChartInteractionAxes.X; board.Group.SynchronizeFrom(chart); FlushControlTemplateWindow(window);
                Check("数据坐标联动不混用范围比例", other.VisibleRange is { } data && Near(data.XMinimum, 30) && Near(data.XMaximum, 80));
                board.Group.Remove(other); var removedRange = other.VisibleRange; selector.Range = new(.4, .7); FlushControlTemplateWindow(window);
                Check("移出组后不再联动", other.VisibleRange == removedRange);
                board.Group.Add(other); board.Group.CoordinateMode = ChartSynchronizationCoordinateMode.Normalized;
                board.Group.SynchronizeFrom(chart); FlushControlTemplateWindow(window);
                Check("重新加入显式同步", Fractions(other, ChartInteractionAxes.X, .4, .7));
                chart.ResetViewport(); other.ResetViewport(); FlushControlTemplateWindow(window);
                var wheel = RouteControlTemplateWheel(chart, window, chart.PlotArea.Center, 2);
                Check("滚轮缩放回写范围条及从图", wheel.Handled && selector.Range.Span < 1 && chart.GetVisibleFraction(ChartInteractionAxes.X) == selector.Range && other.GetVisibleFraction(ChartInteractionAxes.X) == selector.Range);
                chart.ResetViewport(); FlushControlTemplateWindow(window);
                Check("恢复范围同步到范围条与从图", selector.Range == new ChartRange(0, 1) && Fractions(other, ChartInteractionAxes.X, 0, 1));
                chart.SelectionMode = ChartSelectionMode.None; chart.SelectSeries(chart.Series[0]); chart.Focus();
                chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }); FlushControlTemplateWindow(window);
                Check("关闭点选择仍可键盘选择系列", chart.SelectedSeries is { Count: 1 } && ReferenceEquals(chart.SelectedSeries[0], chart.Series[1]));
                chart.SelectSeries(chart.Series[0]); selector.Range = new(.1, .9); FlushControlTemplateWindow(window);
                var pageImage = Path.Combine(directory, stem + $"-selection-range-{(dark ? "dark" : "light")}.png");
                var page = (Border)window.Content!;
                using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(page.Bounds.Width), (int)Math.Ceiling(page.Bounds.Height)), new Vector(96, 96))) { bitmap.Render(page); bitmap.Save(pageImage); }
                var exportImage = Path.Combine(directory, stem + $"-series-selection-export-{(dark ? "dark" : "light")}.png");
                var exports = new List<SelectionRangeExportEvidence>();
                foreach (var dpi in new[] { 96d, 144d, 192d })
                {
                    using var on = new MemoryStream(); var exported = chart.TryExportPng(on, new ChartExportOptions { PixelSize = new PixelSize(960, 540), Dpi = dpi });
                    on.Position = 0; using var image = new Bitmap(on); var onPixels = ReadShapePixels(image);
                    chart.ClearSeriesSelection(); FlushControlTemplateWindow(window);
                    using var off = new MemoryStream(); var offExported = chart.TryExportPng(off, new ChartExportOptions { PixelSize = new PixelSize(960, 540), Dpi = dpi });
                    off.Position = 0; using var offImage = new Bitmap(off); var offPixels = ReadShapePixels(offImage);
                    var differences = CountControlTemplateExactChanges(onPixels, offPixels, new Rect(0, 0, 960, 540));
                    chart.SelectSeries(chart.Series[0]); FlushControlTemplateWindow(window);
                    var bytes = on.ToArray(); var density = TryReadCommonPngDensity(bytes, out var x, out var y, out var unit) && x == (uint)Math.Round(dpi / .0254) && y == x && unit == 1;
                    Check($"{dpi:0}DPI导出保留系列淡化", exported && offExported && image.PixelSize == new PixelSize(960, 540) && differences >= 100 && density);
                    var onImage = Path.Combine(directory, stem + $"-series-selection-{(dark ? "dark" : "light")}-{dpi:0}dpi-on.png");
                    var offFile = Path.Combine(directory, stem + $"-series-selection-{(dark ? "dark" : "light")}-{dpi:0}dpi-off.png");
                    File.WriteAllBytes(onImage, bytes); File.WriteAllBytes(offFile, off.ToArray());
                    exports.Add(new(dpi, differences, density, image.PixelSize.Width, image.PixelSize.Height, onImage, offFile)); if (dpi == 96) File.WriteAllBytes(exportImage, bytes);
                }
                _selectionRangeEvidence.Add(new(label, pageImage, exportImage, changedPixels, restoreChanged, drag.Delta, verticalDrag.Delta, selector.Range, chart.VisibleRange, other.VisibleRange, exports));
            }
            finally { board.Group.Dispose(); window.Close(); board.Primary.Series.Clear(); board.Secondary.Series.Clear(); }
        }
        _selectionRangeExamples.IsExpanded = false;
    }

    private static void SelectionRangeClick(Control control, Window window, Point local, KeyModifiers modifiers = KeyModifiers.None)
    {
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var point = control.TranslatePoint(local, window)!.Value;
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, point, 1, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), modifiers, 1) { RoutedEvent = InputElement.PointerPressedEvent });
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, point, 2, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), modifiers, MouseButton.Left) { RoutedEvent = InputElement.PointerReleasedEvent });
        FlushControlTemplateWindow(window);
    }

    private static (Vector Delta, bool Captured) SelectionRangeDrag(ChartRangeSelector selector, Window window, Point start, Point end)
    {
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        Point WindowPoint(Point value) => selector.TranslatePoint(value, window)!.Value;
        var down = new PointerPressedEventArgs(selector, pointer, window, WindowPoint(start), 1, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1) { RoutedEvent = InputElement.PointerPressedEvent };
        selector.RaiseEvent(down); var captured = ReferenceEquals(pointer.Captured, selector) && selector.IsDragging;
        var move = new PointerEventArgs(InputElement.PointerMovedEvent, selector, pointer, window, WindowPoint(end), 2, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None);
        var delta = move.GetPosition(selector) - down.GetPosition(selector); selector.RaiseEvent(move);
        selector.RaiseEvent(new PointerReleasedEventArgs(selector, pointer, window, WindowPoint(end), 3, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left) { RoutedEvent = InputElement.PointerReleasedEvent });
        FlushControlTemplateWindow(window); return (delta, captured);
    }

    private sealed record SelectionRangeExportEvidence(double Dpi, int ChangedPixels, bool DensityMatches, int Width, int Height, string OnImage, string OffImage);
    private sealed record SelectionRangeEvidence(string Theme, string PageImage, string ExportImage, int SeriesDimmingChangedPixels, int SeriesRestoreChangedPixels,
        Vector HorizontalDragDelta, Vector VerticalDragDelta, ChartRange Range, ChartViewport? PrimaryRange, ChartViewport? SecondaryRange, List<SelectionRangeExportEvidence> Exports);
}
