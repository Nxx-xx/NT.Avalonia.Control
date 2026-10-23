using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<ExportCompletionEvidence> _exportCompletionEvidence = [];

    private async Task RunExportCompletionSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _exportCompletionExamples!.IsExpanded = true;
        await SmokeFlushAsync(); _exportCompletionExamples.BringIntoView(); await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色"; var token = dark ? "dark" : "light";
            using var board = CreateExportCompletionBoard(false);
            var chart = board.Chart;
            var window = new Window { Title = "Charts · 导出完整性", Width = 1160, Height = 800, RequestedThemeVariant = theme,
                Content = new Border { Padding = new Thickness(18), Child = board.View } };
            var outputs = new List<ExportCompletionImageEvidence>(); var outcomes = new List<ExportCompletionOutcomeEvidence>();
            var failureCount = 0; var failureUi = true; string? failureType = null; string? failurePath = null;
            void Failed(object? sender, ChartExportFailedEventArgs args)
            { failureCount++; failureUi &= Dispatcher.UIThread.CheckAccess(); failureType = args.Exception.GetType().Name; failurePath = args.Path; }
            chart.ExportFailed += Failed;
            string Output(string suffix, string extension = "png") => Path.Combine(directory, stem + $"-export-{token}-{suffix}.{extension}");
            void Check(string name, bool passed) => checks[$"导出完整性{label}{name}"] = passed;
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                window.Background = Brush(dark ? "#111722" : "#F8FAFC"); window.Foreground = Brush(dark ? "#EAF0FB" : "#172238");
                ((Border)window.Content!).Background = window.Background;
                chart.Background = window.Background; chart.Foreground = window.Foreground; FlushControlTemplateWindow(window);
                var page = Output("page");
                using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)))
                { bitmap.Render(window); bitmap.Save(page); }
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                Check("原生窗口与多轴模板已布局", native && chart.Bounds.Width > 700 && chart.PlotArea.Height > 150 && chart.Axes.Count == 1 && chart.PlotAreaContent is not null);

                chart.Background = null; chart.Viewport = new ChartViewport(1, 9, 10, 90);
                board.ReferenceY.ViewMinimum = 1100; board.ReferenceY.ViewMaximum = 1900;
                chart.AnimationTriggers = ChartAnimationTriggers.None;
                void SetLevel(double level) => board.Line.ItemsSource = new ChartPoint[] { new(2, level), new(4, level), new(6, level), new(8, level) };
                SetLevel(80); FlushControlTemplateWindow(window);
                chart.AnimationTriggers = ChartAnimationTriggers.All; chart.AnimationDuration = TimeSpan.FromSeconds(2);
                SetLevel(20); FlushControlTemplateWindow(window);
                var animationWait = Stopwatch.StartNew();
                while (chart.AnimationProgress < .25 && animationWait.Elapsed < TimeSpan.FromSeconds(4)) await Task.Delay(20);
                chart.IsAnimationPaused = true; FlushControlTemplateWindow(window);
                var paused = chart.AnimationProgress;
                Check("捕获缩放后的暂停数据动画", chart.AnimationState == ChartAnimationState.Paused && paused is >= .2 and < .9 && chart.VisibleRange is { XMinimum: 1, XMaximum: 9 });
                var size = new PixelSize(1200, 700);
                var transparent = new ChartExportOptions { PixelSize = size, Dpi = 96, JpegQuality = 90 };
                var primary = await Export("png-96-sync", false, false, transparent);
                var primaryRaster = primary.Raster!;
                Check("PNG真实尺寸透明与密度", primary.Exported && primary.PixelWidth == 1200 && primary.PixelHeight == 700 && primary.CornerAlpha == 0 && primary.DensityMatches);
                Check("PNG包含系列标注参考线与模板", primary.BlueInkPixels > 100 && primary.OrangeInkPixels > 50 && primary.TealInkPixels > 100 && primary.PurpleInkPixels > 100);
                Check("PNG图例和多轴投影位置正确", primary.LegendBlueInkPixels > 5 && primary.OrangeReferenceInkPixels > 8 && primary.TealReferenceInkPixels > 8);
                Check("同步导出不推进暂停帧", chart.AnimationState == ChartAnimationState.Paused && chart.AnimationProgress == paused);
                foreach (var dpi in new[] { 144d, 192d })
                {
                    var other = await Export($"png-{dpi:0}-sync", false, false, transparent with { Dpi = dpi });
                    Check($"PNG固定像素{dpi:0}DPI仅改变元数据", other.DensityMatches && EqualPixels(primaryRaster, other.Raster!));
                }
                var asynchronousPng = await Export("png-96-async", false, true, transparent);
                // The old synchronous platform PNG encoder and CopyPixels(Unpremul)
                // round transparent RGB differently. Alpha and geometry must match;
                // the maximum accepted RGB rounding error is exactly one of 255.
                var pngComparison = CompareExportPixels(primary.Image, asynchronousPng.Image, primaryRaster, asynchronousPng.Raster!);
                Check("同步原生与异步PNG跨编码路径量化误差有界", asynchronousPng.Exported && pngComparison.MaximumAlphaError == 0 && pngComparison.MaximumRgbError <= 1 && chart.AnimationProgress == paused);
                foreach (var dpi in new[] { 144d, 192d })
                {
                    var other = await Export($"png-{dpi:0}-async", false, true, transparent with { Dpi = dpi });
                    Check($"异步PNG固定像素{dpi:0}DPI像素完全一致", other.DensityMatches && EqualPixels(asynchronousPng.Raster!, other.Raster!));
                }
                var jpeg = await Export("jpeg-96-sync", true, false, transparent);
                var jpegError = FlattenedDifference(primaryRaster, jpeg.Raster!);
                Check("JPEG真实解码尺寸白底和密度", jpeg.Exported && jpeg.PixelWidth == 1200 && jpeg.PixelHeight == 700 && jpeg.CornerAlpha == 255 && jpeg.CornerR >= 250 && jpeg.CornerG >= 250 && jpeg.CornerB >= 250 && jpeg.DensityMatches);
                Check("JPEG与白底PNG图形一致且有损误差受限", jpeg.BlueInkPixels > 100 && jpeg.OrangeInkPixels > 50 && jpeg.TealInkPixels > 100 && jpeg.PurpleInkPixels > 100 && jpegError is > 0 and < 8);
                var asynchronousJpeg = await Export("jpeg-96-async", true, true, transparent);
                Check("异步JPEG与同步编码像素一致", asynchronousJpeg.Exported && EqualPixels(jpeg.Raster!, asynchronousJpeg.Raster!) && chart.AnimationProgress == paused);
                foreach (var dpi in new[] { 144d, 192d })
                {
                    var other = await Export($"jpeg-{dpi:0}-sync", true, false, transparent with { Dpi = dpi });
                    Check($"JPEG固定像素{dpi:0}DPI仅改变JFIF密度", other.DensityMatches && EqualPixels(jpeg.Raster!, other.Raster!));
                }
                var lowQuality = await Export("jpeg-quality-40", true, true, transparent with { JpegQuality = 40 });
                var highQuality = await Export("jpeg-quality-100", true, true, transparent with { JpegQuality = 100 });
                Check("JPEG质量选项影响真实文件与解码像素", highQuality.FileBytes > lowQuality.FileBytes && !EqualPixels(lowQuality.Raster!, highQuality.Raster!));
                var blueBackground = await Export("png-blue-background", false, true, transparent with { Background = Brush("#E1F1FF") });
                var blueBackgroundSync = await Export("png-blue-background-sync", false, false, transparent with { Background = Brush("#E1F1FF") });
                var blueJpeg = await Export("jpeg-blue-background", true, true, transparent with { Background = Brush("#E1F1FF") });
                Check("不透明背景同步与异步PNG像素完全一致", EqualPixels(blueBackground.Raster!, blueBackgroundSync.Raster!));
                Check("背景选项覆盖PNG透明与JPEG默认白底", blueBackground.CornerAlpha == 255 && blueBackground.CornerR == 225 && blueBackground.CornerG == 241 && blueBackground.CornerB == 255 &&
                    Math.Abs(blueJpeg.CornerR - 225) <= 3 && Math.Abs(blueJpeg.CornerG - 241) <= 3 && Math.Abs(blueJpeg.CornerB - 255) <= 3);
                foreach (var asJpeg in new[] { false, true })
                {
                    var inferred = await Export(asJpeg ? "jpeg-derived-144" : "png-derived-144", asJpeg, true, transparent with { PixelSize = null, Dpi = 144 });
                    Check($"{(asJpeg ? "JPEG" : "PNG")}推导尺寸按布局与DPI计算", inferred.PixelWidth == (int)Math.Ceiling(chart.Bounds.Width * 1.5) && inferred.PixelHeight == (int)Math.Ceiling(chart.Bounds.Height * 1.5) && inferred.DensityMatches);
                }
                var eventStart = failureCount;
                using (var canceled = new CancellationTokenSource())
                using (var destination = new MemoryStream())
                {
                    canceled.Cancel(); var observed = false;
                    try { await chart.TryExportPngAsync(destination, transparent, canceled.Token); } catch (OperationCanceledException) { observed = true; }
                    Check("预取消不写流且不发失败事件", observed && destination.Length == 0 && destination.CanWrite && failureCount == eventStart);
                    outcomes.Add(new("pre-canceled", observed, failureCount - eventStart, destination.Length, destination.CanWrite));
                }
                using (var canceled = new CancellationTokenSource())
                using (var destination = new MemoryStream())
                using (var queued = new ManualResetEventSlim())
                {
                    // Keep this UI turn until the background caller has queued capture.
                    // This distinguishes cancellation of a queued operation from pre-cancel.
                    var work = Task.Run(async () => { var operation = chart.TryExportJpegAsync(destination, transparent, canceled.Token); queued.Set(); return await operation; });
                    var queuedObserved = queued.Wait(TimeSpan.FromSeconds(5)); canceled.Cancel(); var observed = false;
                    try { await work; } catch (OperationCanceledException) { observed = true; }
                    Check("后台排队捕获可取消且不写流", queuedObserved && observed && destination.Length == 0 && destination.CanWrite && failureCount == eventStart);
                    outcomes.Add(new("queued-canceled", observed, failureCount - eventStart, destination.Length, destination.CanWrite, QueuedObserved: queuedObserved));
                }
                foreach (var asJpeg in new[] { false, true })
                {
                    using var gated = new ExportGateStream(); using var cancellation = new CancellationTokenSource();
                    var ticks = 0; var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) }; timer.Tick += (_, _) => ticks++;
                    timer.Start(); var clock = Stopwatch.StartNew();
                    var operation = asJpeg ? chart.TryExportJpegAsync(gated, transparent, cancellation.Token) : chart.TryExportPngAsync(gated, transparent, cancellation.Token);
                    await gated.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    var ticksBefore = ticks; await Task.Delay(140); var heartbeat = ticks - ticksBefore;
                    var busyBefore = failureCount;
                    using var syncBusyOutput = new MemoryStream(); using var asyncBusyOutput = new MemoryStream();
                    var syncBusyRejected = !(asJpeg ? chart.TryExportPng(syncBusyOutput, transparent) : chart.TryExportJpeg(syncBusyOutput, transparent));
                    var syncLeaseHeld = !operation.IsCompleted && gated.Length == 0 && !gated.Release.Task.IsCompleted;
                    Check($"{(asJpeg ? "JPEG" : "PNG")}写出期间同步并发拒绝且租约保持", syncBusyRejected && syncLeaseHeld && syncBusyOutput.Length == 0 && syncBusyOutput.CanWrite &&
                        failureCount == busyBefore + 1 && failureUi && failureType == nameof(InvalidOperationException) && failurePath is null);
                    var asyncBusyRejected = !(asJpeg ? await chart.TryExportPngAsync(asyncBusyOutput, transparent) : await chart.TryExportJpegAsync(asyncBusyOutput, transparent));
                    var asyncLeaseHeld = !operation.IsCompleted && gated.Length == 0 && !gated.Release.Task.IsCompleted;
                    Check($"{(asJpeg ? "JPEG" : "PNG")}写出期间异步并发拒绝且原请求仍等待", asyncBusyRejected && asyncLeaseHeld && asyncBusyOutput.Length == 0 && asyncBusyOutput.CanWrite &&
                        failureCount == busyBefore + 2 && failureUi && failureType == nameof(InvalidOperationException) && failurePath is null);
                    outcomes.Add(new(asJpeg ? "busy-during-write-jpeg" : "busy-during-write-png", false, failureCount - busyBefore, syncBusyOutput.Length + asyncBusyOutput.Length,
                        syncBusyOutput.CanWrite && asyncBusyOutput.CanWrite, failureType, BackgroundWrite: gated.BackgroundWrite,
                        SyncBusyRejected: syncBusyRejected, AsyncBusyRejected: asyncBusyRejected, OriginalWriteStillPending: syncLeaseHeld && asyncLeaseHeld, FailureEventsOnUiThread: failureUi));
                    // These two deliberate busy failures belong to their own outcome.
                    // Cancellation below must still raise no additional failure event.
                    eventStart = failureCount;
                    if (asJpeg) cancellation.Cancel(); else gated.Release.TrySetResult();
                    var wasCanceled = false; var succeeded = false;
                    try { succeeded = await operation; } catch (OperationCanceledException) { wasCanceled = true; }
                    timer.Stop();
                    Check(asJpeg ? "异步JPEG写入可取消且不发失败事件" : "异步PNG等待写入时UI心跳持续", heartbeat >= 3 && gated.BackgroundWrite && gated.CanWrite && failureCount == eventStart && (asJpeg ? wasCanceled && gated.Length == 0 : succeeded && gated.Length > 0));
                    outcomes.Add(new(asJpeg ? "write-canceled-jpeg" : "write-gated-png", wasCanceled, failureCount - eventStart, gated.Length, gated.CanWrite,
                        HeartbeatTicks: heartbeat, WaitMilliseconds: clock.Elapsed.TotalMilliseconds, BackgroundWrite: gated.BackgroundWrite));
                    if (!asJpeg) File.WriteAllBytes(Output("png-gated-write"), gated.ToArray());
                }
                foreach (var asJpeg in new[] { false, true })
                {
                    using var partial = new ExportPartialCancelStream(); using var cancellation = new CancellationTokenSource();
                    var before = failureCount;
                    var operation = asJpeg ? chart.TryExportJpegAsync(partial, transparent, cancellation.Token) : chart.TryExportPngAsync(partial, transparent, cancellation.Token);
                    await partial.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    cancellation.Cancel(); var wasCanceled = false;
                    try { await operation; } catch (OperationCanceledException) { wasCanceled = true; }
                    var prefix = partial.ToArray();
                    var prefixMatches = asJpeg ? prefix.Length >= 2 && prefix[0] == 0xFF && prefix[1] == 0xD8 : prefix.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                    var partialFile = Output(asJpeg ? "jpeg-partial-canceled" : "png-partial-canceled", "bin"); File.WriteAllBytes(partialFile, prefix);
                    Check($"{(asJpeg ? "JPEG" : "PNG")}取消写入保留16字节部分输出且流不关闭", wasCanceled && prefix.Length == 16 && prefixMatches && partial.CanWrite && partial.BackgroundWrite && failureCount == before);
                    outcomes.Add(new(asJpeg ? "partial-write-canceled-jpeg" : "partial-write-canceled-png", wasCanceled, failureCount - before, prefix.Length, partial.CanWrite,
                        BackgroundWrite: partial.BackgroundWrite, PartialOutputFile: partialFile, PartialOutputSha256: HashFile(partialFile)));
                }
                foreach (var asJpeg in new[] { false, true })
                {
                    using var destination = new MemoryStream(); var before = failureCount;
                    var success = asJpeg ? await chart.TryExportJpegAsync(destination, transparent with { JpegQuality = 0 }) : await chart.TryExportPngAsync(destination, transparent with { Dpi = 0 });
                    Check($"{(asJpeg ? "JPEG" : "PNG")}无效选项返回false且UI事件一次", !success && destination.Length == 0 && failureCount == before + 1 && failureUi && failureType == nameof(ArgumentOutOfRangeException) && failurePath is null);
                    outcomes.Add(new(asJpeg ? "invalid-jpeg-quality" : "invalid-png-dpi", false, failureCount - before, destination.Length, destination.CanWrite, failureType));
                    using var throwing = new ExportThrowStream(); before = failureCount;
                    success = asJpeg ? await chart.TryExportJpegAsync(throwing, transparent) : await chart.TryExportPngAsync(throwing, transparent);
                    Check($"{(asJpeg ? "JPEG" : "PNG")}异步写入异常返回false且流保留", !success && failureCount == before + 1 && failureUi && failureType == nameof(IOException) && throwing.CanWrite);
                    outcomes.Add(new(asJpeg ? "throwing-jpeg-stream" : "throwing-png-stream", false, failureCount - before, throwing.Length, throwing.CanWrite, failureType));
                }

                var atomicDirectory = Path.Combine(directory, stem + $"-export-{token}-atomic"); Directory.CreateDirectory(atomicDirectory);
                foreach (var asJpeg in new[] { false, true })
                {
                    var destination = Path.Combine(atomicDirectory, asJpeg ? "preserved.jpg" : "preserved.png");
                    var beforeBytes = System.Text.Encoding.UTF8.GetBytes("existing destination must survive failure"); File.WriteAllBytes(destination, beforeBytes);
                    var beforeFile = destination + ".before.bin"; File.Copy(destination, beforeFile, overwrite: false);
                    var beforeHash = Convert.ToHexString(SHA256.HashData(beforeBytes)); var before = failureCount;
                    var success = asJpeg ? await chart.TryExportJpegAsync(destination, transparent with { JpegQuality = 101 }) : await chart.TryExportPngAsync(destination, transparent with { Dpi = 0 });
                    var invalidHash = HashFile(destination);
                    var invalidFile = destination + ".after-invalid.bin"; File.Copy(destination, invalidFile, overwrite: false);
                    using var cancel = new CancellationTokenSource(); cancel.Cancel(); var wasCanceled = false;
                    try { if (asJpeg) await chart.TryExportJpegAsync(destination, transparent, cancel.Token); else await chart.TryExportPngAsync(destination, transparent, cancel.Token); }
                    catch (OperationCanceledException) { wasCanceled = true; }
                    var canceledHash = HashFile(destination); var temporariesBefore = Directory.GetFiles(atomicDirectory, ".*.tmp");
                    var canceledFile = destination + ".after-canceled.bin"; File.Copy(destination, canceledFile, overwrite: false);
                    Check($"{(asJpeg ? "JPEG" : "PNG")}文件失败取消保留原件且无临时文件", !success && wasCanceled && invalidHash == beforeHash && canceledHash == beforeHash && temporariesBefore.Length == 0 && failureCount == before + 1 && failurePath == destination);
                    success = asJpeg ? await chart.TryExportJpegAsync(destination, transparent) : await chart.TryExportPngAsync(destination, transparent);
                    using var restored = new Bitmap(destination);
                    Check($"{(asJpeg ? "JPEG" : "PNG")}异步文件原子替换成功且清理临时文件", success && HashFile(destination) != beforeHash && restored.PixelSize == size && Directory.GetFiles(atomicDirectory, ".*.tmp").Length == 0);
                    outcomes.Add(new(asJpeg ? "atomic-jpeg" : "atomic-png", wasCanceled, failureCount - before, new FileInfo(destination).Length, true,
                        Destination: destination, BeforeSha256: beforeHash, AfterInvalidSha256: invalidHash, AfterCanceledSha256: canceledHash, AfterSuccessSha256: HashFile(destination), TemporaryFiles: Directory.GetFiles(atomicDirectory, ".*.tmp")));
                    outcomes[^1] = outcomes[^1] with { BeforeFile = beforeFile, AfterInvalidFile = invalidFile, AfterCanceledFile = canceledFile };
                }
                var blocked = Path.Combine(atomicDirectory, "destination-is-directory.png"); Directory.CreateDirectory(blocked);
                var marker = Path.Combine(blocked, "original.txt"); File.WriteAllText(marker, "preserved-directory");
                var blockedBefore = failureCount; var blockedSuccess = await chart.TryExportPngAsync(blocked, transparent);
                Check("文件替换阶段失败保留目标并清理临时文件", !blockedSuccess && File.ReadAllText(marker) == "preserved-directory" && Directory.GetFiles(atomicDirectory, ".*.tmp").Length == 0 && failureCount == blockedBefore + 1 && failureUi && failurePath == blocked);
                outcomes.Add(new("atomic-replace-failure", false, failureCount - blockedBefore, 0, true, failureType, Destination: blocked,
                    TemporaryFiles: Directory.GetFiles(atomicDirectory, ".*.tmp")));
                Check("所有导出均不推进暂停动画", chart.AnimationState == ChartAnimationState.Paused && chart.AnimationProgress == paused);
                // Changing label configuration deliberately settles an animation.
                // Isolate label visibility against its own static-frame baseline.
                chart.CancelAnimations(); chart.IsAnimationPaused = false; FlushControlTemplateWindow(window);
                var labelBaseline = await Export("png-labels-baseline", false, true, transparent);
                board.Line.ShowDataLabels = false; FlushControlTemplateWindow(window);
                var noLabels = await Export("png-labels-off", false, true, transparent);
                board.Line.ShowDataLabels = true; FlushControlTemplateWindow(window);
                var labelsRestored = await Export("png-labels-restored", false, true, transparent);
                var labelChanges = CountControlTemplateExactChanges(labelBaseline.Raster!, noLabels.Raster!, Map(chart.PlotArea, size));
                Check("数据标签实际参与导出并可恢复", labelChanges > 10 && EqualPixels(labelBaseline.Raster!, labelsRestored.Raster!));
                _exportCompletionEvidence.Add(new(token, native, page, chart.Bounds.Size, chart.PlotArea, chart.VisibleRange!.Value, board.ReferenceY.ViewMinimum!.Value,
                    board.ReferenceY.ViewMaximum!.Value, paused, outputs, outcomes, pngComparison, jpegError, labelChanges, failureUi,
                    "Native Skia output decoded through Avalonia Bitmap. JPEG is compared with the same paused-frame PNG flattened on white; alpha is premultiplied. UI responsiveness covers controlled asynchronous writes, not a performance qualification."));

                async Task<ExportCompletionImageEvidence> Export(string scenario, bool asJpeg, bool asynchronous, ChartExportOptions options)
                {
                    using var stream = new MemoryStream();
                    var exported = asynchronous
                        ? asJpeg ? await chart.TryExportJpegAsync(stream, options) : await chart.TryExportPngAsync(stream, options)
                        : asJpeg ? chart.TryExportJpeg(stream, options) : chart.TryExportPng(stream, options);
                    var bytes = stream.ToArray(); var path = Output(scenario, asJpeg ? "jpg" : "png"); File.WriteAllBytes(path, bytes);
                    using var bitmap = new Bitmap(path); var raster = ReadShapePixels(bitmap);
                    var density = asJpeg ? ReadExportJpegDensity(bytes) : ReadExportPngDensity(bytes);
                    var expectedDensity = asJpeg ? (int)Math.Round(options.Dpi, MidpointRounding.AwayFromZero) : (int)Math.Round(options.Dpi / .0254, MidpointRounding.AwayFromZero);
                    var pixelSize = bitmap.PixelSize; var mappedPlot = Map(chart.PlotArea, pixelSize);
                    var legend = Map(new Rect(0, chart.PlotArea.Bottom + 1, chart.Bounds.Width, Math.Max(1, chart.Bounds.Height - chart.PlotArea.Bottom - 1)), pixelSize);
                    var range = chart.VisibleRange!.Value;
                    Point Project(double x, double y) => Fit(new Point(chart.PlotArea.Left + chart.PlotArea.Width * (x - range.XMinimum) / (range.XMaximum - range.XMinimum),
                        chart.PlotArea.Bottom - chart.PlotArea.Height * (y - range.YMinimum) / (range.YMaximum - range.YMinimum)), pixelSize);
                    // Probe the reference between the known data-label anchors at
                    // x=2 and x=4. A label above the paused x=2 marker can cover
                    // the reference line, with font-dependent coverage across OSes.
                    var orangePoint = Project(5, 40); var tealPoint = Project(3, 70);
                    var result = new ExportCompletionImageEvidence(scenario, asJpeg ? "JPEG" : "PNG", asynchronous, path, exported, pixelSize.Width, pixelSize.Height,
                        options.Dpi, options.PixelSize is null, options.JpegQuality, (options.Background as ISolidColorBrush)?.Color.ToString(), bytes.Length, chart.AnimationState.ToString(), chart.AnimationProgress,
                        density.X, density.Y, density.Unit, density.X == expectedDensity && density.Y == expectedDensity && density.Unit == 1,
                        Convert.ToHexString(SHA256.HashData(raster.Pixels)), raster.Pixels[2], raster.Pixels[1], raster.Pixels[0], raster.Pixels[3], mappedPlot,
                        raster.Count(TemplatePixelRect(mappedPlot), Color.Parse("#4385F4"), asJpeg ? 35 : 12),
                        raster.Count(TemplatePixelRect(mappedPlot), Color.Parse("#F2A33A"), asJpeg ? 35 : 12),
                        raster.Count(TemplatePixelRect(mappedPlot), Color.Parse("#19A798"), asJpeg ? 35 : 12),
                        raster.Count(TemplatePixelRect(mappedPlot), Color.Parse("#7B2688"), asJpeg ? 35 : 12),
                        raster.Count(TemplatePixelRect(legend), Color.Parse("#4385F4"), asJpeg ? 35 : 12),
                        orangePoint, raster.Count(Around(orangePoint, 5), Color.Parse("#F2A33A"), asJpeg ? 35 : 12),
                        tealPoint, raster.Count(Around(tealPoint, 5), Color.Parse("#19A798"), asJpeg ? 35 : 12), raster);
                    outputs.Add(result); return result;
                }
                Point Fit(Point point, PixelSize pixels)
                {
                    var scale = Math.Min(pixels.Width / chart.Bounds.Width, pixels.Height / chart.Bounds.Height);
                    return new(point.X * scale + (pixels.Width - chart.Bounds.Width * scale) / 2, point.Y * scale + (pixels.Height - chart.Bounds.Height * scale) / 2);
                }
                Rect Map(Rect rect, PixelSize pixels) => new(Fit(rect.TopLeft, pixels), Fit(rect.BottomRight, pixels));
            }
            finally { chart.ExportFailed -= Failed; window.Close(); }
        }
    }

    private static bool EqualPixels(ShapeRaster first, ShapeRaster second) => first.Width == second.Width && first.Height == second.Height && first.Stride == second.Stride && first.Pixels.AsSpan().SequenceEqual(second.Pixels);
    private static ExportPixelComparisonEvidence CompareExportPixels(string referenceImage, string actualImage, ShapeRaster first, ShapeRaster second)
    {
        if (first.Width != second.Width || first.Height != second.Height) return new(referenceImage, actualImage, -1, 255, 255, 255);
        var changed = 0; var maximumRgb = 0; var maximumAlpha = 0; long totalRgb = 0;
        for (var y = 0; y < first.Height; y++)
            for (var x = 0; x < first.Width; x++)
            {
                var a = y * first.Stride + x * 4; var b = y * second.Stride + x * 4; var different = false;
                for (var c = 0; c < 4; c++)
                {
                    var error = Math.Abs(first.Pixels[a + c] - second.Pixels[b + c]); different |= error != 0;
                    if (c == 3) maximumAlpha = Math.Max(maximumAlpha, error);
                    else { maximumRgb = Math.Max(maximumRgb, error); totalRgb += error; }
                }
                if (different) changed++;
            }
        return new(referenceImage, actualImage, changed, maximumRgb, maximumAlpha, totalRgb / (first.Width * first.Height * 3d));
    }
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static double FlattenedDifference(ShapeRaster png, ShapeRaster jpeg)
    {
        if (png.Width != jpeg.Width || png.Height != jpeg.Height) return double.PositiveInfinity;
        double total = 0;
        for (var y = 0; y < png.Height; y++)
            for (var x = 0; x < png.Width; x++)
            {
                var a = y * png.Stride + x * 4; var b = y * jpeg.Stride + x * 4;
                for (var c = 0; c < 3; c++) total += Math.Abs(Math.Min(255, png.Pixels[a + c] + 255 - png.Pixels[a + 3]) - jpeg.Pixels[b + c]);
            }
        return total / (png.Width * png.Height * 3d);
    }
    private static (int X, int Y, int Unit) ReadExportPngDensity(byte[] bytes) => TryReadCommonPngDensity(bytes, out var x, out var y, out var unit) ? ((int)x, (int)y, unit) : (0, 0, 0);
    private static (int X, int Y, int Unit) ReadExportJpegDensity(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 255 || bytes[1] != 216) return (0, 0, 0);
        var offset = 2;
        while (offset + 4 <= bytes.Length && bytes[offset] == 255)
        {
            var marker = bytes[offset + 1]; if (marker is 0xDA or 0xD9) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 2, 2));
            if (length < 2 || offset + 2 + length > bytes.Length) break;
            if (marker == 0xE0 && length >= 16 && bytes.AsSpan(offset + 4, 5).SequenceEqual("JFIF\0"u8))
                return (BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 12, 2)), BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 14, 2)), bytes[offset + 11]);
            offset += length + 2;
        }
        return (0, 0, 0);
    }

    private sealed class ExportGateStream : MemoryStream
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool BackgroundWrite;
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            BackgroundWrite |= !Dispatcher.UIThread.CheckAccess(); Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken); cancellationToken.ThrowIfCancellationRequested();
            base.Write(buffer.Span);
        }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        { await WriteAsync(buffer.AsMemory(offset, count), cancellationToken); }
    }
    private sealed class ExportThrowStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException(new IOException("Controlled destination write failure."));
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromException(new IOException("Controlled destination write failure."));
        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("Controlled destination write failure.");
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Controlled destination write failure.");
    }
    private sealed class ExportPartialCancelStream : MemoryStream
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool BackgroundWrite;
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            BackgroundWrite |= !Dispatcher.UIThread.CheckAccess();
            base.Write(buffer.Span[..Math.Min(16, buffer.Length)]);
            Entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        { await WriteAsync(buffer.AsMemory(offset, count), cancellationToken); }
    }

    private sealed record ExportCompletionImageEvidence(string Scenario, string Format, bool Asynchronous, string Image, bool Exported, int PixelWidth, int PixelHeight,
        double RequestedDpi, bool DerivedSize, int JpegQuality, string? RequestedBackground, long FileBytes, string AnimationState, double AnimationProgress, int DensityX, int DensityY, int DensityUnit, bool DensityMatches,
        string DecodedPixelSha256, byte CornerR, byte CornerG, byte CornerB, byte CornerAlpha, Rect ExpectedPlotPixels,
        int BlueInkPixels, int OrangeInkPixels, int TealInkPixels, int PurpleInkPixels, int LegendBlueInkPixels,
        Point ExpectedOrangeReferencePixel, int OrangeReferenceInkPixels, Point ExpectedTealReferencePixel, int TealReferenceInkPixels,
        [property: System.Text.Json.Serialization.JsonIgnore] ShapeRaster? Raster);
    private sealed record ExportCompletionOutcomeEvidence(string Scenario, bool CancellationObserved, int FailureEvents, long OutputBytes, bool StreamLeftOpen,
        string? FailureType = null, bool QueuedObserved = false, int HeartbeatTicks = 0, double WaitMilliseconds = 0, bool BackgroundWrite = false,
        string? Destination = null, string? BeforeSha256 = null, string? AfterInvalidSha256 = null, string? AfterCanceledSha256 = null, string? AfterSuccessSha256 = null, string[]? TemporaryFiles = null,
        string? BeforeFile = null, string? AfterInvalidFile = null, string? AfterCanceledFile = null,
        bool SyncBusyRejected = false, bool AsyncBusyRejected = false, bool OriginalWriteStillPending = false, bool FailureEventsOnUiThread = true,
        string? PartialOutputFile = null, string? PartialOutputSha256 = null);
    private sealed record ExportPixelComparisonEvidence(string ReferenceImage, string ActualImage, int DifferentPixels, int MaximumRgbError, int MaximumAlphaError, double MeanRgbError);
    private sealed record ExportCompletionEvidence(string Theme, bool NativeWindowCreated, string PageImage, Size ChartSize, Rect PlotArea, ChartViewport VisibleRange,
        double ReferenceYMinimum, double ReferenceYMaximum, double PausedAnimationProgress, List<ExportCompletionImageEvidence> Images,
        List<ExportCompletionOutcomeEvidence> Outcomes, ExportPixelComparisonEvidence SyncAsyncPngComparison, double JpegMeanAbsoluteErrorToWhitePng, int LabelChangedPixels, bool AllFailureEventsOnUiThread, string Scope);
}
