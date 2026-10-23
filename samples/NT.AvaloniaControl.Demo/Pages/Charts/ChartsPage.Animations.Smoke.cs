using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<AnimationSmokeEvidence> _animationEvidence = [];
    private bool _animationDiagnosticEnabled;
    private readonly List<AnimationDiagnosticEvidence> _animationDiagnosticEvidence = [];

    private async Task RunAnimationSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _animationExamples!.IsExpanded = true;
        await SmokeFlushAsync(); _animationExamples.BringIntoView(); await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色"; var token = dark ? "dark" : "light";
            using var board = CreateAnimationBoard(false);
            var window = new Window { Title = "Charts · 动画完整性", Width = 1160, Height = 730, RequestedThemeVariant = theme,
                Content = new Border { Padding = new Thickness(18), Child = board.View } };
            var frames = new List<AnimationFrameEvidence>(); var phases = new List<AnimationPhaseEvidence>();
            var exports = new List<AnimationFrameExportEvidence>(); var timings = new List<AnimationTimingEvidence>();
            var chart = board.Chart;
            var diagnosticClock = Stopwatch.StartNew(); var diagnosticEvents = new List<AnimationDiagnosticEvent>(); var diagnosticPhase = "setup";
            if (_animationDiagnosticEnabled) _animationDiagnosticEvidence.Add(new(label, diagnosticEvents, "diagnostic-only; unchanged-animation-assertions; in-memory-property-timeline; no-performance-claim"));
            void Trace(string source, string property, object? before = null, object? after = null, string? stack = null)
            {
                if (!_animationDiagnosticEnabled || diagnosticEvents.Count >= 20_000) return;
                diagnosticEvents.Add(new(diagnosticClock.Elapsed.TotalMilliseconds, diagnosticPhase, source, property, before?.ToString(), after?.ToString(), chart.AnimationState.ToString(), chart.AnimationProgress,
                    chart.IsAnimationPaused, chart.Bounds, chart.IsVisible, chart.IsEffectivelyVisible, chart.IsEffectivelyEnabled, window.ClientSize, chart.AnimationDuration.TotalMilliseconds, chart.AnimationDelay.TotalMilliseconds, stack));
            }
            void ChartChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
            {
                var idleStack = change.Property == CartesianChart.AnimationStateProperty && chart.AnimationState == ChartAnimationState.Idle ? Environment.StackTrace : null;
                Trace("chart", change.Property.Name, change.OldValue, change.NewValue, idleStack);
            }
            void WindowChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
            {
                if (change.Property == Window.ClientSizeProperty || change.Property == Window.BoundsProperty || change.Property == Window.IsVisibleProperty)
                    Trace("window", change.Property.Name, change.OldValue, change.NewValue);
            }
            if (_animationDiagnosticEnabled) { chart.PropertyChanged += ChartChanged; window.PropertyChanged += WindowChanged; Trace("driver", "begin"); }
            string Image(string suffix) => Path.Combine(directory, stem + $"-animation-{token}-{suffix}.png");
            void Check(string name, bool passed) { checks[$"动画完整性{label}{name}"] = passed; Trace("driver", "check:" + name, null, passed); }
            try
            {
                chart.AnimationTriggers = ChartAnimationTriggers.None;
                chart.AnimationDuration = TimeSpan.FromMilliseconds(1200); chart.AnimationDelay = TimeSpan.Zero;
                chart.AnimationEasing = new LinearEasing();
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                window.Background = Brush(dark ? "#111722" : "#F8FAFC"); window.Foreground = Brush(dark ? "#EAF0FB" : "#172238");
                ((Border)window.Content!).Background = window.Background;
                chart.Background = window.Background; chart.Foreground = window.Foreground;
                FlushControlTemplateWindow(window);
                var page = Image("page");
                using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)))
                { bitmap.Render(window); bitmap.Save(page); }
                // The user-facing page above keeps its full sample. Fixed axes and opaque
                // independent colors below make the native animation pixel oracle unambiguous.
                chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "固定横轴" };
                chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "固定纵轴" };
                board.Bars.Fill = Teal; SetPoints(20); FlushControlTemplateWindow(window);
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                Check("原生窗口与三类系列有效布局", native && chart.Bounds.Width > 700 && chart.PlotArea.Height > 160 && chart.Series.Count == 3 &&
                    chart.Series.OfType<LineSeries>().Count() == 1 && chart.Series.OfType<BarSeries>().Count() == 1 && chart.Series.OfType<ScatterSeries>().Count() == 1);
                var initial = Frame("initial", 20);
                Check("关闭动画直接绘制目标", chart.AnimationState == ChartAnimationState.Idle && initial.ScatterOraclePassed);
                Check("三类系列均有真实墨迹", initial.BlueInkPixels > 20 && initial.TealInkPixels > 20 && initial.OrangeInkPixels > 20);

                chart.AnimationTriggers = ChartAnimationTriggers.All;
                chart.IsAnimationPaused = true; chart.ReplayEntranceAnimation(); FlushControlTemplateWindow(window);
                var entranceBefore = Frame("entrance-before", 0);
                Check("入场暂停起点位于零基线", chart.AnimationState == ChartAnimationState.Paused && chart.AnimationProgress == 0 && entranceBefore.ScatterOraclePassed);
                var entranceClock = Stopwatch.StartNew(); chart.IsAnimationPaused = false;
                await WaitProgress(); chart.IsAnimationPaused = true; FlushControlTemplateWindow(window);
                var entranceProgress = chart.AnimationProgress;
                timings.Add(new("entrance", entranceClock.Elapsed.TotalMilliseconds, 0, entranceProgress, chart.AnimationDuration.TotalMilliseconds));
                var entranceMid = Frame("entrance-middle", 20 * entranceProgress);
                var entranceChanges = CountControlTemplateExactChanges(entranceBefore.Raster!, entranceMid.Raster!, chart.PlotArea);
                Check("入场中间帧符合线性基线插值", entranceProgress is > .1 and < .95 && entranceMid.ScatterOraclePassed && entranceChanges > 30);
                phases.Add(new("entrance", entranceBefore.Image, entranceMid.Image, entranceProgress, entranceChanges));
                chart.CancelAnimations(); FlushControlTemplateWindow(window);
                var entranceEnd = Frame("entrance-final", 20);
                Check("入场取消直接到最终目标", chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1 && entranceEnd.ScatterOraclePassed);

                chart.IsAnimationPaused = false; var dataClock = Stopwatch.StartNew(); SetPoints(80); FlushControlTemplateWindow(window);
                await WaitProgress(); chart.IsAnimationPaused = true; FlushControlTemplateWindow(window);
                var dataProgress = chart.AnimationProgress; var dataMid = Frame("data-middle", 20 + 60 * dataProgress);
                timings.Add(new("data", dataClock.Elapsed.TotalMilliseconds, 0, dataProgress, chart.AnimationDuration.TotalMilliseconds));
                Check("数据更新中间帧符合独立数学公式", dataProgress is > .1 and < .95 && dataMid.ScatterOraclePassed);
                var pauseTime = Stopwatch.StartNew(); await Task.Delay(150); FlushControlTemplateWindow(window);
                var dataPause = Frame("data-paused", 20 + 60 * dataProgress);
                var pauseChanges = CountControlTemplateExactChanges(dataMid.Raster!, dataPause.Raster!, chart.PlotArea);
                Check("真实等待后暂停进度和像素不变", pauseTime.ElapsedMilliseconds >= 120 && chart.AnimationState == ChartAnimationState.Paused && chart.AnimationProgress == dataProgress && pauseChanges == 0);
                phases.Add(new("pause", dataMid.Image, dataPause.Image, dataProgress, pauseChanges, pauseTime.Elapsed.TotalMilliseconds));
                foreach (var dpi in new[] { 96d, 144d, 192d })
                {
                    var beforeProgress = chart.AnimationProgress;
                    var path = Image($"paused-export-{dpi:0}");
                    using var stream = new MemoryStream();
                    var exported = chart.TryExportPng(stream, new ChartExportOptions
                    { PixelSize = new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height)), Dpi = dpi, Background = chart.Background });
                    File.WriteAllBytes(path, stream.ToArray());
                    using var bitmap = new Bitmap(path); var raster = ReadShapePixels(bitmap);
                    var changes = CountControlTemplateExactChanges(dataPause.Raster!, raster, chart.PlotArea);
                    var density = TryReadCommonPngDensity(stream.ToArray(), out var x, out var y, out var unit) && x == (uint)Math.Round(dpi / .0254) && y == x && unit == 1;
                    Check($"{dpi:0}DPI导出当前帧且不推进时间线", exported && changes == 0 && chart.AnimationProgress == beforeProgress && chart.AnimationState == ChartAnimationState.Paused && density);
                    exports.Add(new(dpi, path, exported, raster.Width, raster.Height, beforeProgress, chart.AnimationProgress, changes, density, chart.AnimationState.ToString(), chart.PlotArea, dataPause.Image));
                }
                var resumeClock = Stopwatch.StartNew(); chart.IsAnimationPaused = false;
                await WaitProgress(dataProgress + .15); chart.IsAnimationPaused = true; FlushControlTemplateWindow(window);
                var resumed = Frame("data-resumed", 20 + 60 * chart.AnimationProgress);
                timings.Add(new("resume", resumeClock.Elapsed.TotalMilliseconds, dataProgress, chart.AnimationProgress, chart.AnimationDuration.TotalMilliseconds));
                Check("恢复后从暂停位置继续", chart.AnimationProgress > dataProgress + .1 && resumed.ScatterOraclePassed && CountControlTemplateExactChanges(dataPause.Raster!, resumed.Raster!, chart.PlotArea) > 30);
                chart.IsAnimationPaused = false; FlushControlTemplateWindow(window);
                var activeProgress = chart.AnimationProgress; var activePath = Image("data-active");
                var activeRaster = CaptureControlTemplateRaster(chart, activePath);
                var activeExportPath = Image("data-active-export");
                using (var stream = new MemoryStream())
                {
                    var exported = chart.TryExportPng(stream, new ChartExportOptions
                    { PixelSize = new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height)), Background = chart.Background });
                    File.WriteAllBytes(activeExportPath, stream.ToArray());
                    using var bitmap = new Bitmap(activeExportPath); var raster = ReadShapePixels(bitmap);
                    var changes = CountControlTemplateExactChanges(activeRaster, raster, chart.PlotArea);
                    var density = TryReadCommonPngDensity(stream.ToArray(), out var x, out var y, out var unit) && x == (uint)Math.Round(96 / .0254) && y == x && unit == 1;
                    Check("运行中同步导出当前帧不推进进度", exported && chart.AnimationState == ChartAnimationState.Running && chart.AnimationProgress == activeProgress && changes == 0 && density);
                    exports.Add(new(96, activeExportPath, exported, raster.Width, raster.Height, activeProgress, chart.AnimationProgress, changes, density, chart.AnimationState.ToString(), chart.PlotArea, activePath));
                }
                await WaitIdle(); FlushControlTemplateWindow(window);
                var dataFinal = Frame("data-final", 80);
                Check("数据动画按真实帧自然完成", chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1 && dataFinal.ScatterOraclePassed);

                chart.AnimationDelay = TimeSpan.FromMilliseconds(450); SetPoints(20); FlushControlTemplateWindow(window);
                var delayBefore = Frame("delay-before", 80); var delayProgress = chart.AnimationProgress;
                var delayTimer = Stopwatch.StartNew(); await Task.Delay(120); FlushControlTemplateWindow(window);
                var delayAfter = Frame("delay-after", 80);
                var delayChanges = CountControlTemplateExactChanges(delayBefore.Raster!, delayAfter.Raster!, chart.PlotArea);
                Check("延迟期间真实等待保持旧画面", chart.AnimationState == ChartAnimationState.Delayed && delayProgress == 0 && chart.AnimationProgress == 0 && delayBefore.ScatterOraclePassed && delayAfter.ScatterOraclePassed && delayChanges == 0);
                phases.Add(new("delay", delayBefore.Image, delayAfter.Image, delayProgress, delayChanges, delayTimer.Elapsed.TotalMilliseconds));
                await WaitProgress(); chart.IsAnimationPaused = true; FlushControlTemplateWindow(window);
                var delayedRunning = Frame("delay-running", 80 - 60 * chart.AnimationProgress);
                Check("延迟结束后才开始数据插值", chart.AnimationProgress is > .1 and < .95 && delayedRunning.ScatterOraclePassed);
                chart.CancelAnimations(); chart.IsAnimationPaused = false; chart.AnimationDelay = TimeSpan.Zero;
                FlushControlTemplateWindow(window);

                chart.Viewport = new ChartViewport(40, 100, 10, 90); FlushControlTemplateWindow(window);
                await WaitProgress(); chart.IsAnimationPaused = true; FlushControlTemplateWindow(window);
                var viewProgress = chart.AnimationProgress; var view = chart.VisibleRange;
                var viewMid = Frame("viewport-middle", 20);
                Check("视图中间范围按独立公式变化", view is { } range && Math.Abs(range.XMinimum - 40 * viewProgress) < .0001 && Math.Abs(range.XMaximum - 100) < .0001 &&
                    Math.Abs(range.YMinimum - 10 * viewProgress) < .0001 && Math.Abs(range.YMaximum - (100 - 10 * viewProgress)) < .0001 && chart.Viewport == new ChartViewport(40, 100, 10, 90));
                Check("视图系列墨迹采用当前显示范围", viewMid.ScatterOraclePassed && CountControlTemplateExactChanges(delayAfter.Raster!, viewMid.Raster!, chart.PlotArea) > 30);
                chart.CancelAnimations(); chart.IsAnimationPaused = false; FlushControlTemplateWindow(window);
                var viewFinal = Frame("viewport-final", 20);
                Check("视图取消保留业务目标并完成坐标范围", HasVisibleRange(chart, new ChartViewport(40, 100, 10, 90)) && viewFinal.ScatterOraclePassed);

                chart.ReduceMotion = true; chart.ResetViewport(); SetPoints(80); FlushControlTemplateWindow(window);
                var reduced = Frame("reduced-motion", 80);
                Check("减少动态效果立即完成数据和视图", chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1 &&
                    HasVisibleRange(chart, new ChartViewport(0, 100, 0, 100)) && reduced.ScatterOraclePassed);
                chart.ReplayEntranceAnimation(); FlushControlTemplateWindow(window);
                var reducedEntrance = Frame("reduced-entrance", 80);
                Check("减少动态效果跳过重播入场", chart.AnimationState == ChartAnimationState.Idle && reducedEntrance.ScatterOraclePassed &&
                    CountControlTemplateExactChanges(reduced.Raster!, reducedEntrance.Raster!, chart.PlotArea) == 0);

                chart.ReduceMotion = false; SetPoints(20); FlushControlTemplateWindow(window);
                await WaitProgress(); chart.IsAnimationPaused = true; FlushControlTemplateWindow(window);
                var interrupted = Frame("latest-interrupted", 80 - 60 * chart.AnimationProgress);
                var oldProgress = chart.AnimationProgress;
                SetPoints(60); SetPoints(35); SetPoints(65); FlushControlTemplateWindow(window);
                chart.CancelAnimations(); chart.IsAnimationPaused = false; FlushControlTemplateWindow(window);
                var latest = Frame("latest-final", 65);
                Check("连续数据更新以最新目标为准", interrupted.ScatterOraclePassed && oldProgress is > .1 and < .95 && latest.ScatterOraclePassed && chart.AnimationState == ChartAnimationState.Idle);
                chart.AnimationEasing = new CubicEaseOut();
                var easingClock = Stopwatch.StartNew(); SetPoints(25); FlushControlTemplateWindow(window);
                await WaitProgress(.4); chart.IsAnimationPaused = true; FlushControlTemplateWindow(window);
                var easingElapsed = easingClock.Elapsed.TotalMilliseconds; var easedProgress = chart.AnimationProgress;
                var cubic = Frame("cubic-easing", 65 - 40 * easedProgress);
                var linearFraction = Math.Clamp(easingElapsed / chart.AnimationDuration.TotalMilliseconds, 0, 1);
                var expectedEased = 1 - Math.Pow(1 - linearFraction, 3);
                Check("缓动改变真实进度并保持几何一致", easedProgress > linearFraction + .1 && Math.Abs(easedProgress - expectedEased) < .1 && cubic.ScatterOraclePassed);
                timings.Add(new("cubic-easing", easingElapsed, 0, easedProgress, chart.AnimationDuration.TotalMilliseconds, "CubicEaseOut"));
                chart.CancelAnimations(); chart.IsAnimationPaused = false; chart.AnimationEasing = new LinearEasing();
                chart.AnimationPointLimit = 2; SetPoints(85); FlushControlTemplateWindow(window);
                var budget = Frame("over-point-limit", 85);
                Check("超过点数上限立即显示目标", chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1 && budget.ScatterOraclePassed);
                chart.AnimationPointLimit = 10000;
                SetPoints(25); FlushControlTemplateWindow(window); await WaitProgress();
                chart.IsVisible = false; await Task.Delay(40); chart.IsVisible = true; FlushControlTemplateWindow(window);
                var visible = Frame("after-hide", 25);
                Check("隐藏终止时间线并在显示后保持目标", chart.AnimationState == ChartAnimationState.Idle && visible.ScatterOraclePassed);
                SetPoints(75); FlushControlTemplateWindow(window); await WaitProgress();
                board.View.Children.Remove(chart); await Task.Delay(40);
                Check("脱离可视树终止时间线", chart.AnimationState == ChartAnimationState.Idle);
                board.View.Children.Insert(board.View.Children.Count - 1, chart); FlushControlTemplateWindow(window);
                chart.CancelAnimations(); FlushControlTemplateWindow(window);
                var reattached = Frame("after-reattach", 75);
                Check("重新附加后保留最新数据", reattached.ScatterOraclePassed && chart.AnimationState == ChartAnimationState.Idle);
                _animationEvidence.Add(new(label, native, page, chart.AnimationDuration.TotalMilliseconds, "LinearEasing", frames.Select(frame => frame with { Raster = null }).ToList(), phases, exports, timings,
                    "native-top-level-real-animation-frames; programmatic-property-changes; no-physical-input-or-performance-claim"));
            }
            finally { Trace("driver", "finally"); window.Close(); if (_animationDiagnosticEnabled) { chart.PropertyChanged -= ChartChanged; window.PropertyChanged -= WindowChanged; } }

            void SetPoints(double scatterY)
            {
                board.Scatter.ItemsSource = new ChartPoint[] { new(80, scatterY) };
                board.Line.ItemsSource = new ChartPoint[] { new(40, scatterY * .5 + 30), new(50, scatterY * .5 + 35) };
                board.Bars.ItemsSource = new ChartPoint[] { new(20, scatterY * .5 + 20) };
            }

            async Task WaitProgress(double threshold = .2)
            {
                diagnosticPhase = "wait-progress"; Trace("driver", "wait-enter", null, threshold);
                var timer = Stopwatch.StartNew();
                while ((chart.AnimationState is ChartAnimationState.Idle or ChartAnimationState.Delayed || chart.AnimationProgress < threshold) && timer.ElapsedMilliseconds < 5000)
                { await Task.Delay(16); FlushControlTemplateWindow(window); }
                if (chart.AnimationProgress < threshold || chart.AnimationState == ChartAnimationState.Idle)
                {
                    Trace("driver", "wait-failed", null, threshold);
                    throw new InvalidOperationException($"真实原生动画未到达中间帧：state={chart.AnimationState}, progress={chart.AnimationProgress}, threshold={threshold}。");
                }
                Trace("driver", "wait-complete", null, threshold);
            }

            async Task WaitIdle()
            {
                var timer = Stopwatch.StartNew();
                while (chart.AnimationState != ChartAnimationState.Idle && timer.ElapsedMilliseconds < 5000)
                { await Task.Delay(16); FlushControlTemplateWindow(window); }
            }

            AnimationFrameEvidence Frame(string name, double expectedY)
            {
                diagnosticPhase = "frame:" + name; Trace("driver", "frame-start", null, expectedY);
                FlushControlTemplateWindow(window);
                var path = Image(name); var raster = CaptureControlTemplateRaster(chart, path);
                var range = chart.VisibleRange ?? throw new InvalidOperationException("动画图表尚未生成坐标范围。");
                var expected = new Point(chart.PlotArea.Left + (80 - range.XMinimum) / (range.XMaximum - range.XMinimum) * chart.PlotArea.Width,
                    chart.PlotArea.Bottom - (expectedY - range.YMinimum) / (range.YMaximum - range.YMinimum) * chart.PlotArea.Height);
                var centroid = AnimationColorCentroid(raster, chart.PlotArea, Color.Parse("#F2A33A"));
                // At the entrance baseline, the plot clip removes the lower half of the marker.
                // A full circle's color centroid uses the tighter 1.5 DIP tolerance elsewhere.
                var tolerance = expectedY == range.YMinimum ? 7d : 1.5;
                var oracle = centroid.Count >= 10 && Math.Abs(centroid.Center.X - expected.X) <= 1.5 && Math.Abs(centroid.Center.Y - expected.Y) <= tolerance;
                var frame = new AnimationFrameEvidence(name, path, chart.PlotArea, range, chart.AnimationState.ToString(), chart.AnimationProgress,
                    expectedY, expected, centroid.Center, tolerance, centroid.Count, oracle,
                    raster.Count(TemplatePixelRect(chart.PlotArea), Color.Parse("#4385F4"), 12),
                    raster.Count(TemplatePixelRect(chart.PlotArea), Color.Parse("#19A798"), 12), centroid.Count, raster);
                frames.Add(frame); Trace("driver", "frame-end:" + name, expected, centroid.Center); return frame;
            }
        }
    }

    private static (Point Center, int Count) AnimationColorCentroid(ShapeRaster raster, Rect area, Color color)
    {
        var bounds = TemplatePixelRect(area); double xSum = 0, ySum = 0; var count = 0;
        for (var y = Math.Max(0, bounds.Y); y < Math.Min(raster.Height, bounds.Bottom); y++)
            for (var x = Math.Max(0, bounds.X); x < Math.Min(raster.Width, bounds.Right); x++)
            {
                var offset = y * raster.Stride + x * 4;
                if (raster.Pixels[offset + 3] < 250 || Math.Abs(raster.Pixels[offset] - color.B) > 12 || Math.Abs(raster.Pixels[offset + 1] - color.G) > 12 || Math.Abs(raster.Pixels[offset + 2] - color.R) > 12) continue;
                xSum += x + .5; ySum += y + .5; count++;
            }
        return (count == 0 ? new Point(-999, -999) : new Point(xSum / count, ySum / count), count);
    }

    private sealed record AnimationFrameEvidence(string Scenario, string Image, Rect PlotArea, ChartViewport VisibleRange, string State, double Progress,
        double ExpectedScatterY, Point ExpectedScatterCenter, Point ActualScatterCenter, double VerticalCentroidToleranceDip, int ScatterInkPixels, bool ScatterOraclePassed,
        int BlueInkPixels, int TealInkPixels, int OrangeInkPixels, [property: System.Text.Json.Serialization.JsonIgnore] ShapeRaster? Raster);
    private sealed record AnimationPhaseEvidence(string Scenario, string BeforeImage, string AfterImage, double Progress, int ChangedPixels, double? ElapsedMilliseconds = null);
    private sealed record AnimationFrameExportEvidence(double Dpi, string Image, bool Exported, int Width, int Height, double BeforeProgress, double AfterProgress, int ChangedPixels, bool DensityMatches, string State, Rect PlotArea, string ScreenImage);
    private sealed record AnimationTimingEvidence(string Scenario, double ElapsedMilliseconds, double StartProgress, double EndProgress, double DurationMilliseconds, string Easing = "LinearEasing");
    private sealed record AnimationSmokeEvidence(string Theme, bool NativeWindowCreated, string PageImage, double DurationMilliseconds, string Easing,
        List<AnimationFrameEvidence> Frames, List<AnimationPhaseEvidence> Phases, List<AnimationFrameExportEvidence> Exports, List<AnimationTimingEvidence> Timings, string Scope);
    private sealed record AnimationDiagnosticEvidence(string Theme, List<AnimationDiagnosticEvent> Events, string Scope);
    private sealed record AnimationDiagnosticEvent(double ElapsedMilliseconds, string Phase, string Source, string Property, string? Before, string? After,
        string State, double Progress, bool Paused, Rect Bounds, bool IsVisible, bool IsEffectivelyVisible, bool IsEffectivelyEnabled, Size WindowClientSize,
        double DurationMilliseconds, double DelayMilliseconds, string? IdleStack);
}
