using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<InputGesturesEvidence> _inputGesturesEvidence = [];

    private async Task RunInputGesturesSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _inputGesturesExamples!.IsExpanded = true; await SmokeFlushAsync();
        _inputGesturesExamples.BringIntoView(); await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色"; var token = dark ? "dark" : "light";
            using var board = CreateInputGesturesBoard(false);
            var window = new Window { Title = "Charts · 触摸、连续滚动与嵌套输入", Width = 1160, Height = 940,
                RequestedThemeVariant = theme, Content = new Border { Padding = new Thickness(18), Child = board.View } };
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                window.Background = Brush(dark ? "#111722" : "#F8FAFC"); window.Foreground = Brush(dark ? "#EAF0FB" : "#172238");
                ((Border)window.Content!).Background = window.Background;
                var chart = board.Chart; chart.Background = window.Background; chart.Foreground = window.Foreground;
                chart.IsTooltipEnabled = false; chart.SelectionMode = ChartSelectionMode.None; chart.SeriesSelectionMode = ChartSelectionMode.None;
                // The synthetic checks deliberately exercise chart-first routing. Keep the
                // visible selector in sync with that setup in the retained page images.
                board.View.GetVisualDescendants().OfType<ComboBox>().Single(item => item.Name == "InputGesturesRouting").SelectedIndex = 0;
                board.Scroll.Offset = new Vector(0, 145); FlushControlTemplateWindow(window);
                var steps = new List<InputGestureStepEvidence>();
                void Check(string name, bool passed) => checks[$"输入完整性{label}{name}"] = passed;
                static bool Near(double a, double b) => Math.Abs(a - b) < 1e-7;
                static bool RangeNear(ChartViewport? actual, ChartViewport expected) => actual is { } value &&
                    Math.Abs(value.XMinimum - expected.XMinimum) < 1e-7 && Math.Abs(value.XMaximum - expected.XMaximum) < 1e-7 &&
                    Math.Abs(value.YMinimum - expected.YMinimum) < 1e-7 && Math.Abs(value.YMaximum - expected.YMaximum) < 1e-7;
                void Baseline()
                {
                    chart.ResetViewport(); chart.Viewport = new ChartViewport(20, 80, 20, 80);
                    chart.IsTouchEnabled = true; chart.IsZoomEnabled = true; chart.IsPanEnabled = true;
                    chart.InteractionAxes = ChartInteractionAxes.Both; chart.InputRoutingMode = ChartInputRoutingMode.Chart;
                    chart.WheelMode = ChartWheelMode.Auto; chart.WheelPanSensitivity = 48;
                    chart.WheelZoomSensitivity = 1.2; chart.WheelZoomModifiers = KeyModifiers.None;
                    FlushControlTemplateWindow(window);
                }
                Baseline();
                var layout = new InputGestureLayoutEvidence(window.TryGetPlatformHandle() is { Handle: not 0 },
                    chart.Bounds, chart.PlotArea, board.Scroll.Extent, board.Scroll.Viewport, board.Scroll.Offset,
                    chart.GetVisualAncestors().OfType<ScrollViewer>().Any(item => ReferenceEquals(item, board.Scroll)));
                Check("原生窗口与真正嵌套容器布局", layout.NativeWindowCreated && layout.ChartBounds.Width > 700 &&
                    layout.PlotArea.Height > 180 && layout.ScrollExtent.Height > layout.ScrollViewport.Height + 250 &&
                    layout.ScrollOffset.Y > 0 && layout.RealScrollViewerAncestor);
                var beforeFile = Path.Combine(directory, stem + $"-input-{token}-before.png");
                var baselinePixels = CaptureControlTemplateRaster(chart, beforeFile);
                var center = chart.PlotArea.Center; var initial = chart.VisibleRange!.Value; var initialPlot = chart.PlotArea;
                using (var touch = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true))
                {
                    var down = InputTouchPress(chart, touch, center, 1);
                    var small = InputTouchMove(chart, touch, center + new Vector(2.5, 1.25), 2);
                    Check("单指阈值内不捕获也不改变视图", !down.Handled && !small.Handled && touch.Captured is null && chart.VisibleRange == initial);
                    var moved = InputTouchMove(chart, touch, center + new Vector(32.25, 17.5), 3);
                    var delta = moved.GetPosition(chart) - down.GetPosition(chart);
                    var expected = InputPanExpected(initial, initialPlot, delta, ChartInteractionAxes.Both);
                    Check("单指真实路由捕获并按位移平移", moved.Handled && ReferenceEquals(touch.Captured, chart) && RangeNear(chart.VisibleRange, expected));
                    steps.Add(new("touch-pan", initial, chart.VisibleRange, expected, [down.GetPosition(chart), moved.GetPosition(chart)], delta, true, moved.Handled, null, null, null));
                    var released = InputTouchRelease(chart, touch, moved.GetPosition(chart), 4);
                    Check("单指释放清理捕获且不误选点", released.Handled && touch.Captured is null && chart.SelectedPoint is null && chart.SelectedSeries is not { Count: > 0 });
                }
                Baseline(); center = chart.PlotArea.Center; initial = chart.VisibleRange!.Value; initialPlot = chart.PlotArea;
                var a = center - new Vector(80, 0); var b = center + new Vector(80, 0);
                var aEnd = center + new Vector(-100.5, 8.25); var bEnd = center + new Vector(104.75, 8.25);
                ChartViewport pinchExpected;
                using (var first = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true))
                using (var second = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, false))
                {
                    InputTouchPress(chart, first, a, 10); InputTouchPress(chart, second, b, 11);
                    Check("二指按下保留初始范围", first.Captured is null && second.Captured is null && chart.VisibleRange == initial);
                    InputTouchMove(chart, first, aEnd, 12); var move = InputTouchMove(chart, second, bEnd, 13);
                    pinchExpected = InputPinchExpected(initial, initialPlot, a, b, aEnd, bEnd, ChartInteractionAxes.Both);
                    var bothCaptured = ReferenceEquals(first.Captured, chart) && ReferenceEquals(second.Captured, chart);
                    Check("二指距离和质心独立公式匹配", bothCaptured && move.Handled && RangeNear(chart.VisibleRange, pinchExpected));
                    steps.Add(new("touch-pinch-and-centroid-pan", initial, chart.VisibleRange, pinchExpected, [a, b, aEnd, bEnd], bEnd - b, bothCaptured, move.Handled, null, null, double.Hypot(a.X - b.X, a.Y - b.Y) / double.Hypot(aEnd.X - bEnd.X, aEnd.Y - bEnd.Y)));
                    InputTouchRelease(chart, second, bEnd, 14); var transition = chart.VisibleRange!.Value; var transitionPlot = chart.PlotArea;
                    var next = aEnd + new Vector(-12.5, 9.5); var nextMove = InputTouchMove(chart, first, next, 15);
                    var expected = InputPanExpected(transition, transitionPlot, next - aEnd, ChartInteractionAxes.Both);
                    Check("二指转单指连续平移无跳变", second.Captured is null && ReferenceEquals(first.Captured, chart) && nextMove.Handled && RangeNear(chart.VisibleRange, expected));
                    InputTouchRelease(chart, first, next, 16);
                    Check("二指序列结束释放全部捕获", first.Captured is null && second.Captured is null && chart.SelectedPoint is null);
                }
                var pinchFile = Path.Combine(directory, stem + $"-input-{token}-pinch.png");
                var pinchPixels = CaptureControlTemplateRaster(chart, pinchFile);
                var pinchChanges = CountControlTemplateExactChanges(baselinePixels, pinchPixels, initialPlot);
                Baseline(); var restoreFile = Path.Combine(directory, stem + $"-input-{token}-restored.png");
                var restorePixels = CaptureControlTemplateRaster(chart, restoreFile);
                var restoreChanges = CountControlTemplateExactChanges(baselinePixels, restorePixels, initialPlot);
                Check("捏合平移改变像素并可精确恢复", pinchChanges >= 100 && restoreChanges == 0);

                initial = chart.VisibleRange!.Value; center = chart.PlotArea.Center;
                using (var lost = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true))
                {
                    InputTouchPress(chart, lost, center, 20); InputTouchMove(chart, lost, center + new Vector(22, 12), 21);
                    var beforeLoss = chart.VisibleRange; lost.Capture(null);
                    InputTouchMove(chart, lost, center + new Vector(52, 32), 22); InputTouchRelease(chart, lost, center + new Vector(52, 32), 23);
                    Check("丢失捕获后旧触点不再更新", lost.Captured is null && chart.VisibleRange == beforeLoss);
                }
                Baseline(); center = chart.PlotArea.Center;
                using (var cancelled = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true))
                {
                    InputTouchPress(chart, cancelled, center, 30); InputTouchMove(chart, cancelled, center + new Vector(23, 12), 31);
                    chart.ResetViewport(); FlushControlTemplateWindow(window); var reset = chart.VisibleRange;
                    InputTouchMove(chart, cancelled, center + new Vector(58, 32), 32); InputTouchRelease(chart, cancelled, center + new Vector(58, 32), 33);
                    Check("恢复视图取消在途触摸", cancelled.Captured is null && chart.VisibleRange == reset && chart.Viewport is null);
                }
                Baseline(); center = chart.PlotArea.Center;
                using (var disabled = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true))
                {
                    InputTouchPress(chart, disabled, center, 40); InputTouchMove(chart, disabled, center + new Vector(23, 12), 41);
                    chart.IsTouchEnabled = false; var stopped = chart.VisibleRange;
                    InputTouchMove(chart, disabled, center + new Vector(58, 32), 42); InputTouchRelease(chart, disabled, center + new Vector(58, 32), 43);
                    Check("关闭触摸立即释放且旧序列失效", disabled.Captured is null && chart.VisibleRange == stopped);
                }
                Baseline(); chart.InteractionAxes = ChartInteractionAxes.Y; initial = chart.VisibleRange!.Value; initialPlot = chart.PlotArea; center = chart.PlotArea.Center;
                using (var vertical = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true))
                {
                    InputTouchPress(chart, vertical, center, 50); var moved = InputTouchMove(chart, vertical, center + new Vector(24.5, 12.75), 51);
                    var expected = InputPanExpected(initial, initialPlot, new Vector(24.5, 12.75), ChartInteractionAxes.Y);
                    Check("单轴触摸保留另一轴", moved.Handled && RangeNear(chart.VisibleRange, expected)); InputTouchRelease(chart, vertical, moved.GetPosition(chart), 52);
                }
                Baseline(); chart.InputRoutingMode = ChartInputRoutingMode.Auto; center = chart.PlotArea.Center;
                using (var delegated = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true))
                {
                    initial = chart.VisibleRange!.Value; InputTouchPress(chart, delegated, center, 60);
                    var moved = InputTouchMove(chart, delegated, center + new Vector(1, -28.5), 61);
                    Check("嵌套自动为父容器保留纵向触摸", !moved.Handled && delegated.Captured is null && chart.VisibleRange == initial);
                    steps.Add(new("nested-touch-routing", initial, chart.VisibleRange, initial, [center, moved.GetPosition(chart)], moved.GetPosition(chart) - center, false, moved.Handled, board.Scroll.Offset, board.Scroll.Offset, null));
                    InputTouchRelease(chart, delegated, moved.GetPosition(chart), 62);
                }
                Baseline(); initial = chart.VisibleRange!.Value; initialPlot = chart.PlotArea; center = initialPlot.Center;
                InputNativePinch(chart, 1, center);
                var nativeCenter = center + new Vector(7.5, 6.25);
                var nativeFirst = InputNativePinch(chart, 1.5, nativeCenter);
                var nativeFirstExpected = InputPinchExpected(initial, initialPlot, center - new Vector(50, 0), center + new Vector(50, 0), nativeCenter - new Vector(75, 0), nativeCenter + new Vector(75, 0), ChartInteractionAxes.Both);
                Check("原生Pinch路由支持缩放与质心移动", nativeFirst.Handled && RangeNear(chart.VisibleRange, nativeFirstExpected));
                var nativeSecond = InputNativePinch(chart, 2, nativeCenter);
                var nativeSecondExpected = InputPinchExpected(initial, initialPlot, center - new Vector(50, 0), center + new Vector(50, 0), nativeCenter - new Vector(100, 0), nativeCenter + new Vector(100, 0), ChartInteractionAxes.Both);
                Check("原生Pinch累计Scale不重复相乘", nativeSecond.Handled && RangeNear(chart.VisibleRange, nativeSecondExpected));
                steps.Add(new("native-pinch-cumulative-scale", initial, chart.VisibleRange, nativeSecondExpected, [center, nativeCenter], nativeCenter - center, false, nativeSecond.Handled, null, null, .5));
                InputNativePinchEnd(chart); initial = chart.VisibleRange!.Value; initialPlot = chart.PlotArea; center = initialPlot.Center;
                InputNativePinch(chart, 1, center); var restarted = InputNativePinch(chart, 2, center);
                var restartExpected = InputZoomExpected(initial, .5, .5, .5, ChartInteractionAxes.Both);
                Check("原生Pinch结束后下一序列重新基准", restarted.Handled && RangeNear(chart.VisibleRange, restartExpected));
                InputNativePinchEnd(chart); Baseline(); center = chart.PlotArea.Center; initial = chart.VisibleRange!.Value;
                using (var directTouch = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true))
                {
                    InputTouchPress(chart, directTouch, center, 65); var duplicate = InputNativePinch(chart, 2, center);
                    Check("原生Pinch不重复处理正在跟踪的触点", !duplicate.Handled && chart.VisibleRange == initial);
                    InputTouchRelease(chart, directTouch, center, 66); InputNativePinchEnd(chart);
                }
                Baseline(); center = chart.PlotArea.Center; initial = chart.VisibleRange!.Value;
                initialPlot = chart.PlotArea; var deltaWheel = new Vector(.375, -.625); var scrollBefore = board.Scroll.Offset;
                var continuous = InputWheel(board, window, center, deltaWheel);
                var continuousExpected = InputPanExpected(initial, initialPlot, deltaWheel * chart.WheelPanSensitivity, ChartInteractionAxes.Both);
                Check("触控板小数XY增量连续平移", continuous.ChartHandled && continuous.FinalHandled && RangeNear(chart.VisibleRange, continuousExpected) && board.Scroll.Offset == scrollBefore && !Near(chart.VisibleRange!.Value.XMinimum, Math.Round(chart.VisibleRange.Value.XMinimum)));
                steps.Add(new("fractional-wheel-pan", initial, chart.VisibleRange, continuousExpected, [continuous.Position], deltaWheel, false, continuous.ChartHandled, scrollBefore, board.Scroll.Offset, null));
                initial = chart.VisibleRange!.Value; var anchor = new Point(chart.PlotArea.Left + chart.PlotArea.Width * .35, chart.PlotArea.Top + chart.PlotArea.Height * .6);
                var ctrl = InputWheel(board, window, anchor, new Vector(0, .5), KeyModifiers.Control);
                var zoomFactor = Math.Pow(1.2, -.5); var zoomExpected = InputZoomExpected(initial, .35, .4, zoomFactor, ChartInteractionAxes.Both);
                Check("Ctrl小数滚动按指针锚定缩放", ctrl.ChartHandled && RangeNear(chart.VisibleRange, zoomExpected) && board.Scroll.Offset == scrollBefore);
                steps.Add(new("ctrl-fractional-wheel-zoom", initial, chart.VisibleRange, zoomExpected, [ctrl.Position], new Vector(0, .5), false, ctrl.ChartHandled, scrollBefore, board.Scroll.Offset, zoomFactor));
                Baseline(); initial = chart.VisibleRange!.Value; initialPlot = chart.PlotArea;
                var shift = InputWheel(board, window, chart.PlotArea.Center, new Vector(0, .4375), KeyModifiers.Shift);
                var shiftExpected = InputPanExpected(initial, initialPlot, new Vector(.4375 * 48, 0), ChartInteractionAxes.Both);
                Check("Shift纵向滚动转为横向平移", shift.ChartHandled && RangeNear(chart.VisibleRange, shiftExpected));
                Baseline(); chart.WheelPanSensitivity = 24; initial = chart.VisibleRange!.Value; initialPlot = chart.PlotArea;
                var sensitive = InputWheel(board, window, chart.PlotArea.Center, new Vector(.25, 0));
                var sensitiveExpected = InputPanExpected(initial, initialPlot, new Vector(6, 0), ChartInteractionAxes.Both);
                Check("灵敏度按DIP比例影响连续平移", sensitive.ChartHandled && RangeNear(chart.VisibleRange, sensitiveExpected));
                Baseline(); chart.InputRoutingMode = ChartInputRoutingMode.Auto; initial = chart.VisibleRange!.Value;
                board.Scroll.Offset = new Vector(0, 145); FlushControlTemplateWindow(window); scrollBefore = board.Scroll.Offset;
                var nested = InputWheel(board, window, chart.PlotArea.Center, new Vector(0, -.375));
                Check("嵌套自动滚动真实ScrollViewer", !nested.ChartHandled && nested.FinalHandled && chart.VisibleRange == initial && board.Scroll.Offset.Y > scrollBefore.Y);
                steps.Add(new("nested-auto-wheel-scroll", initial, chart.VisibleRange, initial, [nested.Position], new Vector(0, -.375), false, nested.ChartHandled, scrollBefore, board.Scroll.Offset, null));
                scrollBefore = board.Scroll.Offset;
                var nestedCtrl = InputWheel(board, window, chart.PlotArea.Center, new Vector(0, .75), KeyModifiers.Control);
                Check("嵌套自动Ctrl仍由图表缩放", nestedCtrl.ChartHandled && chart.VisibleRange!.Value.XMaximum - chart.VisibleRange.Value.XMinimum < 60 && board.Scroll.Offset == scrollBefore);
                Baseline(); chart.ResetViewport(); FlushControlTemplateWindow(window); initial = chart.VisibleRange!.Value;
                board.Scroll.Offset = new Vector(0, 145); FlushControlTemplateWindow(window); scrollBefore = board.Scroll.Offset;
                var boundary = InputWheel(board, window, chart.PlotArea.Center, new Vector(0, -.5));
                Check("图表边界交父容器且不吞滚轮", !boundary.ChartHandled && boundary.FinalHandled && chart.VisibleRange == initial && board.Scroll.Offset.Y > scrollBefore.Y);
                steps.Add(new("chart-boundary-wheel-handoff", initial, chart.VisibleRange, initial, [boundary.Position], new Vector(0, -.5), false, boundary.ChartHandled, scrollBefore, board.Scroll.Offset, null));
                Baseline(); chart.InputRoutingMode = ChartInputRoutingMode.ScrollViewer; initial = chart.VisibleRange!.Value;
                scrollBefore = board.Scroll.Offset; var parentOnly = InputWheel(board, window, chart.PlotArea.Center, new Vector(0, -.5));
                Check("容器优先路由保留图表范围", !parentOnly.ChartHandled && parentOnly.FinalHandled && chart.VisibleRange == initial && board.Scroll.Offset.Y > scrollBefore.Y);
                Baseline(); chart.WheelMode = ChartWheelMode.Disabled; initial = chart.VisibleRange!.Value;
                var disabledWheel = InputWheel(board, window, chart.PlotArea.Center, new Vector(0, -.25));
                Check("关闭滚轮不阻断容器滚动", !disabledWheel.ChartHandled && disabledWheel.FinalHandled && chart.VisibleRange == initial);
                Baseline(); chart.WheelMode = ChartWheelMode.Zoom; chart.WheelZoomModifiers = KeyModifiers.Alt; initial = chart.VisibleRange!.Value;
                var unmodified = InputWheel(board, window, chart.PlotArea.Center, new Vector(0, -.25));
                Check("指定缩放修饰键缺席时交父", !unmodified.ChartHandled && chart.VisibleRange == initial);
                var alternate = InputWheel(board, window, chart.PlotArea.Center, new Vector(0, .25), KeyModifiers.Alt);
                Check("指定缩放修饰键可连续缩放", alternate.ChartHandled && chart.VisibleRange!.Value.XMaximum - chart.VisibleRange.Value.XMinimum < 60);

                // Export a viewport reached by a routed two-touch sequence. Keep its full-range
                // negative control and restored image, so independent readers can recompute pixels.
                Baseline(); center = chart.PlotArea.Center; a = center - new Vector(80, 0); b = center + new Vector(80, 0);
                using (var first = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true))
                using (var second = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, false))
                {
                    InputTouchPress(chart, first, a, 80); InputTouchPress(chart, second, b, 81);
                    InputTouchMove(chart, first, a - new Vector(28, 0), 82); InputTouchMove(chart, second, b + new Vector(28, 0), 83);
                    InputTouchRelease(chart, second, b + new Vector(28, 0), 84); InputTouchRelease(chart, first, a - new Vector(28, 0), 85);
                }
                FlushControlTemplateWindow(window); var exportRange = chart.VisibleRange!.Value;
                board.Scroll.Offset = new Vector(0, 145); FlushControlTemplateWindow(window);
                var pageImage = Path.Combine(directory, stem + $"-input-gestures-{token}.png");
                var page = (Border)window.Content!;
                using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(page.Bounds.Width), (int)Math.Ceiling(page.Bounds.Height)), new Vector(96, 96))) { bitmap.Render(page); bitmap.Save(pageImage); }
                var exports = new List<InputGestureExportEvidence>();
                foreach (var dpi in new[] { 96d, 144d, 192d })
                {
                    byte[] Export()
                    {
                        using var stream = new MemoryStream();
                        if (!chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(960, 540), Dpi = dpi })) throw new InvalidOperationException("输入示例导出失败。");
                        return stream.ToArray();
                    }
                    var positive = Export(); using var onStream = new MemoryStream(positive); using var on = new Bitmap(onStream); var positivePixels = ReadShapePixels(on);
                    chart.ResetViewport(); FlushControlTemplateWindow(window); var negative = Export();
                    using var offStream = new MemoryStream(negative); using var off = new Bitmap(offStream); var negativePixels = ReadShapePixels(off);
                    chart.Viewport = exportRange; FlushControlTemplateWindow(window); var restored = Export();
                    using var restoredStream = new MemoryStream(restored); using var restoredImage = new Bitmap(restoredStream);
                    var changes = CountControlTemplateExactChanges(positivePixels, negativePixels, new Rect(0, 0, 960, 540));
                    var restoredChanges = CountControlTemplateExactChanges(positivePixels, ReadShapePixels(restoredImage), new Rect(0, 0, 960, 540));
                    var density = TryReadCommonPngDensity(positive, out var x, out var y, out var unit) && x == (uint)Math.Round(dpi / .0254) && x == y && unit == 1;
                    Check($"{dpi:0}DPI导出保留触摸视图并精确恢复", on.PixelSize == new PixelSize(960, 540) && changes >= 100 && restoredChanges == 0 && density);
                    var onPath = Path.Combine(directory, stem + $"-input-{token}-{dpi:0}dpi-on.png");
                    var offPath = Path.Combine(directory, stem + $"-input-{token}-{dpi:0}dpi-off.png");
                    var restoredPath = Path.Combine(directory, stem + $"-input-{token}-{dpi:0}dpi-restored.png");
                    File.WriteAllBytes(onPath, positive); File.WriteAllBytes(offPath, negative); File.WriteAllBytes(restoredPath, restored);
                    exports.Add(new(dpi, changes, restoredChanges, density, on.PixelSize.Width, on.PixelSize.Height, onPath, offPath, restoredPath));
                }
                Check("实时状态显示触摸滚动与可见范围", board.Status.Text?.Contains("可见 X", StringComparison.Ordinal) == true && board.Status.Text.Contains("容器偏移", StringComparison.Ordinal));
                _inputGesturesEvidence.Add(new(label, pageImage, beforeFile, pinchFile, restoreFile, pinchChanges, restoreChanges, layout, steps, exports,
                    "synthetic-routed-touch-pointer-native-pinch-event-and-continuous-wheel-in-native-window; no physical hardware or compositor claim"));
            }
            finally { window.Close(); board.Chart.ResetViewport(); board.Chart.Series.Clear(); }
        }
        _inputGesturesExamples.IsExpanded = false;

    }

    private static ChartViewport InputPanExpected(ChartViewport before, Rect plot, Vector deltaDip, ChartInteractionAxes axes)
    {
        var spanX = before.XMaximum - before.XMinimum; var spanY = before.YMaximum - before.YMinimum;
        var x = (axes & ChartInteractionAxes.X) == 0 ? before.XMinimum : Math.Clamp(before.XMinimum - deltaDip.X / plot.Width * spanX, 0, 100 - spanX);
        var y = (axes & ChartInteractionAxes.Y) == 0 ? before.YMinimum : Math.Clamp(before.YMinimum + deltaDip.Y / plot.Height * spanY, 0, 100 - spanY);
        return new(x, x + spanX, y, y + spanY);
    }

    private static ChartViewport InputZoomExpected(ChartViewport before, double anchorX, double anchorY, double factor, ChartInteractionAxes axes)
    {
        var oldX = before.XMaximum - before.XMinimum; var oldY = before.YMaximum - before.YMinimum;
        var spanX = (axes & ChartInteractionAxes.X) == 0 ? oldX : Math.Clamp(oldX * factor, .0001, 100);
        var spanY = (axes & ChartInteractionAxes.Y) == 0 ? oldY : Math.Clamp(oldY * factor, .0001, 100);
        var x = Math.Clamp(before.XMinimum + anchorX * (oldX - spanX), 0, 100 - spanX);
        var y = Math.Clamp(before.YMinimum + anchorY * (oldY - spanY), 0, 100 - spanY);
        return new(x, x + spanX, y, y + spanY);
    }

    private static ChartViewport InputPinchExpected(ChartViewport before, Rect plot, Point first, Point second, Point firstNow, Point secondNow, ChartInteractionAxes axes)
    {
        var initialCenter = new Point((first.X + second.X) / 2, (first.Y + second.Y) / 2);
        var currentCenter = new Point((firstNow.X + secondNow.X) / 2, (firstNow.Y + secondNow.Y) / 2);
        var factor = double.Hypot(first.X - second.X, first.Y - second.Y) / double.Hypot(firstNow.X - secondNow.X, firstNow.Y - secondNow.Y);
        var oldX = before.XMaximum - before.XMinimum; var oldY = before.YMaximum - before.YMinimum;
        var spanX = (axes & ChartInteractionAxes.X) == 0 ? oldX : oldX * factor;
        var spanY = (axes & ChartInteractionAxes.Y) == 0 ? oldY : oldY * factor;
        var x = (axes & ChartInteractionAxes.X) == 0 ? before.XMinimum : Math.Clamp(before.XMinimum + (initialCenter.X - plot.Left) / plot.Width * oldX - (currentCenter.X - plot.Left) / plot.Width * spanX, 0, 100 - spanX);
        var y = (axes & ChartInteractionAxes.Y) == 0 ? before.YMinimum : Math.Clamp(before.YMinimum + (plot.Bottom - initialCenter.Y) / plot.Height * oldY - (plot.Bottom - currentCenter.Y) / plot.Height * spanY, 0, 100 - spanY);
        return new(x, x + spanX, y, y + spanY);
    }

    private static PointerPressedEventArgs InputTouchPress(CartesianChart chart, Pointer pointer, Point local, ulong timestamp)
    {
        var root = chart.GetVisualAncestors().Last(); var position = chart.TranslatePoint(local, root)!.Value;
        var args = new PointerPressedEventArgs(chart, pointer, root, position, timestamp, default, KeyModifiers.None) { RoutedEvent = InputElement.PointerPressedEvent };
        RequireInputPosition(local, args.GetPosition(chart));
        chart.RaiseEvent(args); return args;
    }

    private static PointerEventArgs InputTouchMove(CartesianChart chart, Pointer pointer, Point local, ulong timestamp)
    {
        var root = chart.GetVisualAncestors().Last(); var position = chart.TranslatePoint(local, root)!.Value;
        var args = new PointerEventArgs(InputElement.PointerMovedEvent, chart, pointer, root, position, timestamp, default, KeyModifiers.None);
        RequireInputPosition(local, args.GetPosition(chart));
        chart.RaiseEvent(args); return args;
    }

    private static PointerReleasedEventArgs InputTouchRelease(CartesianChart chart, Pointer pointer, Point local, ulong timestamp)
    {
        var root = chart.GetVisualAncestors().Last(); var position = chart.TranslatePoint(local, root)!.Value;
        var args = new PointerReleasedEventArgs(chart, pointer, root, position, timestamp, default, KeyModifiers.None, MouseButton.Left) { RoutedEvent = InputElement.PointerReleasedEvent };
        RequireInputPosition(local, args.GetPosition(chart));
        chart.RaiseEvent(args); return args;
    }

    private static PinchEventArgs InputNativePinch(CartesianChart chart, double scale, Point origin)
    {
        var args = new PinchEventArgs(scale, origin) { RoutedEvent = InputElement.PinchEvent, Source = chart };
        chart.RaiseEvent(args); return args;
    }

    private static void InputNativePinchEnd(CartesianChart chart) =>
        chart.RaiseEvent(new PinchEndedEventArgs { RoutedEvent = InputElement.PinchEndedEvent, Source = chart });

    private static InputWheelEvidence InputWheel(InputGesturesBoard board, Window window, Point local, Vector delta, KeyModifiers modifiers = KeyModifiers.None)
    {
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var chart = board.Chart; var root = chart.GetVisualAncestors().Last(); var position = chart.TranslatePoint(local, root)!.Value;
        var args = new PointerWheelEventArgs(chart, pointer, root, position, 100, default, modifiers, delta) { RoutedEvent = InputElement.PointerWheelChangedEvent };
        var routedPosition = args.GetPosition(chart);
        RequireInputPosition(local, routedPosition);
        var chartHandled = false;
        void BeforeScroll(object? sender, PointerWheelEventArgs routed) { if (ReferenceEquals(routed, args)) chartHandled = routed.Handled; }
        board.ChartHost.AddHandler(InputElement.PointerWheelChangedEvent, BeforeScroll, RoutingStrategies.Bubble, true);
        try { chart.RaiseEvent(args); FlushControlTemplateWindow(window); }
        finally { board.ChartHost.RemoveHandler(InputElement.PointerWheelChangedEvent, BeforeScroll); }
        return new(routedPosition, chartHandled, args.Handled);
    }

    private static void RequireInputPosition(Point expected, Point actual)
    {
        if (Math.Abs(expected.X - actual.X) > 1e-8 || Math.Abs(expected.Y - actual.Y) > 1e-8)
            throw new InvalidOperationException($"合成路由坐标出现舍入：expected {expected}; actual {actual}。");
    }

    private sealed record InputWheelEvidence(Point Position, bool ChartHandled, bool FinalHandled);
    private sealed record InputGestureStepEvidence(string Name, ChartViewport? Before, ChartViewport? After, ChartViewport Expected,
        Point[] Positions, Vector Delta, bool Captured, bool ChartHandled, Vector? ScrollBefore, Vector? ScrollAfter, double? ScaleFactor);
    private sealed record InputGestureExportEvidence(double Dpi, int ChangedPixels, int RestoredChangedPixels, bool DensityMatches,
        int Width, int Height, string OnImage, string OffImage, string RestoredImage);
    private sealed record InputGestureLayoutEvidence(bool NativeWindowCreated, Rect ChartBounds, Rect PlotArea,
        Size ScrollExtent, Size ScrollViewport, Vector ScrollOffset, bool RealScrollViewerAncestor);
    private sealed record InputGesturesEvidence(string Theme, string PageImage, string BeforeImage, string PinchImage, string RestoredImage,
        int GestureChangedPixels, int RestoredChangedPixels, InputGestureLayoutEvidence Layout, List<InputGestureStepEvidence> Steps, List<InputGestureExportEvidence> Exports, string InputScope);
}
