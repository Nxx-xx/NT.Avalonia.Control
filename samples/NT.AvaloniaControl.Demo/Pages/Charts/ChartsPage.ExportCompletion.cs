using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _exportCompletionExamples;
    private ExportCompletionBoard? _exportCompletionBoard;

    private Control CreateExportCompletionExamples()
    {
        _exportCompletionBoard = CreateExportCompletionBoard(true);
        return _exportCompletionExamples = new Expander
        {
            Name = "ChartExportCompletionExamples", Header = "导出：PNG、JPEG 与异步取消",
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = _exportCompletionBoard.View,
        };
    }

    private ExportCompletionBoard CreateExportCompletionBoard(bool register)
    {
        var chart = register ? CreateAdvancedChart("ExportCompletionChart") : new CartesianChart { Name = "ExportCompletionChart" };
        chart.Height = 350; chart.Title = "导出完整图表、当前视图与动画帧";
        chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 10, Title = "时间 / 小时" };
        chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "负载 / %" };
        chart.IsZoomEnabled = true; chart.IsPanEnabled = true; chart.IsTooltipEnabled = true;
        chart.LegendPosition = ChartLegendPosition.Bottom;
        var referenceY = new NumericAxis { Key = "export-reference-y", Minimum = 1000, Maximum = 2000,
            Position = ChartAxisPosition.Far, Title = "参考 / 次", LabelBrush = Teal, TitleBrush = Teal };
        chart.Axes.Add(referenceY);
        var line = new LineSeries { Title = "监测趋势", Stroke = Blue, StrokeThickness = 3, MarkerSize = 7,
            ShowDataLabels = true, DataLabelFormat = "0", DataLabelForeground = Blue,
            ItemsSource = new ChartPoint[] { new(1, 22), new(3, 48), new(5, 35), new(7, 78), new(9, 64) } };
        chart.Series.Add(line);
        chart.Annotations.Add(new RectangleAnnotation { X1 = 3.5, X2 = 6.5, Y1 = 40, Y2 = 65, CornerRadius = 8,
            Fill = Brush("#30F2A33A"), Stroke = Orange, StrokeThickness = 2, Label = "导出观察区", Foreground = Orange });
        chart.Annotations.Add(new LineAnnotation { YAxisKey = referenceY.Key, Value = 1700, Stroke = Teal,
            StrokeThickness = 3, Label = "参考阈值 1700" });
        chart.PlotAreaContent = "业务备注也会导出";
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((context, _) => new Border
        {
            Name = "ExportCompletionBadge", Background = Brush("#7B2688"), Padding = new Thickness(8, 4), CornerRadius = new CornerRadius(5),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8),
            Child = new TextBlock { Text = context?.Content?.ToString(), Foreground = Brushes.White, FontSize = 12 },
        });
        var format = new ComboBox { Name = "ExportCompletionFormat", ItemsSource = new[] { "PNG", "JPEG" }, SelectedIndex = 0, MinWidth = 90, Margin = new Thickness(0, 0, 8, 6) };
        var size = new ComboBox { Name = "ExportCompletionSize", ItemsSource = new[] { "1200 × 700 像素", "800 × 800 像素", "按布局与 DPI 推导" }, SelectedIndex = 0, MinWidth = 165, Margin = new Thickness(0, 0, 8, 6) };
        var dpi = new ComboBox { Name = "ExportCompletionDpi", ItemsSource = new[] { "96 DPI", "144 DPI", "192 DPI" }, SelectedIndex = 0, MinWidth = 100, Margin = new Thickness(0, 0, 8, 6) };
        var background = new ComboBox { Name = "ExportCompletionBackground", ItemsSource = new[] { "透明 / JPEG 白底", "主题背景", "浅蓝背景" }, SelectedIndex = 1, MinWidth = 165, Margin = new Thickness(0, 0, 8, 6) };
        var quality = new ComboBox { Name = "ExportCompletionQuality", ItemsSource = new[] { "JPEG 质量 90", "JPEG 质量 60", "JPEG 质量 100" }, SelectedIndex = 0, MinWidth = 145, Margin = new Thickness(0, 0, 8, 6) };
        var asynchronous = new CheckBox { Name = "ExportCompletionAsync", Content = "异步编码与写入", IsChecked = true, Margin = new Thickness(0, 0, 8, 6) };
        var transparentChart = new CheckBox { Name = "ExportCompletionTransparentChart", Content = "图表本身透明", IsChecked = false, Margin = new Thickness(0, 0, 8, 6) };
        transparentChart.IsCheckedChanged += (_, _) => chart.Background = transparentChart.IsChecked == true ? null : _hostWindow.Background;
        var status = new TextBlock { Name = "ExportCompletionStatus", Text = "选择格式和选项后保存；异步任务可取消。", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        CancellationTokenSource? pending = null;
        var pauseGeneration = 0;
        var released = false; var pickerActive = false;
        EventHandler<ChartExportFailedEventArgs> failure = (_, args) => status.Text = $"导出失败：{args.Exception.GetType().Name} · {args.Exception.Message}";
        chart.ExportFailed += failure;
        var save = new Button { Name = "ExportCompletionSave", Content = "保存图表…", Margin = new Thickness(0, 0, 8, 6) };
        save.Click += async (_, _) =>
        {
            if (released || pickerActive || pending is not null) return;
            pickerActive = true;
            try
            {
                var jpeg = format.SelectedIndex == 1;
                using var file = await _hostWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                { Title = "导出图表", SuggestedFileName = jpeg ? "chart-export.jpg" : "chart-export.png", DefaultExtension = jpeg ? "jpg" : "png" });
                if (file is null || released) return;
                var options = new ChartExportOptions
                {
                    PixelSize = size.SelectedIndex switch { 0 => new PixelSize(1200, 700), 1 => new PixelSize(800, 800), _ => null },
                    Dpi = dpi.SelectedIndex switch { 1 => 144, 2 => 192, _ => 96 },
                    Background = background.SelectedIndex switch { 1 => chart.Background, 2 => Brush("#E1F1FF"), _ => null },
                    JpegQuality = quality.SelectedIndex switch { 1 => 60, 2 => 100, _ => 90 },
                };
                pending = new CancellationTokenSource(); save.IsEnabled = false;
                // Local files use atomic replacement; other picker providers retain
                // their own stream semantics and may contain partial output on failure.
                bool success;
                if (file.TryGetLocalPath() is { } path)
                    success = asynchronous.IsChecked == true
                        ? jpeg ? await chart.TryExportJpegAsync(path, options, pending.Token) : await chart.TryExportPngAsync(path, options, pending.Token)
                        : jpeg ? chart.TryExportJpeg(path, options) : chart.TryExportPng(path, options);
                else
                {
                    await using var stream = await file.OpenWriteAsync();
                    if (released) return;
                    success = asynchronous.IsChecked == true
                        ? jpeg ? await chart.TryExportJpegAsync(stream, options, pending.Token) : await chart.TryExportPngAsync(stream, options, pending.Token)
                        : jpeg ? chart.TryExportJpeg(stream, options) : chart.TryExportPng(stream, options);
                }
                if (success) status.Text = $"{(jpeg ? "JPEG" : "PNG")} 已保存，包含图例、标签、标注和绘图区模板。";
            }
            catch (OperationCanceledException) { status.Text = "导出已取消。调用方流可能含有已写入的部分内容。"; }
            catch (Exception exception) { status.Text = $"无法保存：{exception.Message}"; }
            finally { pickerActive = false; pending?.Dispose(); pending = null; save.IsEnabled = !released; }
        };
        var actions = new WrapPanel { Children = { save } };
        AddAction(actions, "取消导出", () => pending?.Cancel(), "取消排队、编码或写入中的异步导出。");
        AddAction(actions, "切换缩放", () => chart.Viewport = chart.Viewport is null ? new ChartViewport(2, 8, 10, 90) : null, "输出沿用当前可见范围。");
        AddAction(actions, "暂停动画帧", () =>
        {
            chart.AnimationTriggers = ChartAnimationTriggers.All; chart.AnimationDuration = TimeSpan.FromSeconds(4);
            chart.IsAnimationPaused = false; chart.ReplayEntranceAnimation();
            var generation = ++pauseGeneration;
            _ = PauseLater();
            async Task PauseLater() { await Task.Delay(1000); if (generation != pauseGeneration) return; chart.IsAnimationPaused = true; status.Text = "已暂停在当前动画帧，可选择 PNG 或 JPEG 导出。"; }
        }, "动画约一秒后暂停，导出捕获暂停帧。");
        AddAction(actions, "完成动画", () => { pauseGeneration++; chart.CancelAnimations(); chart.IsAnimationPaused = false; }, "直接显示当前目标数据。");
        AddAction(actions, "演示选项错误", () => { using var stream = new MemoryStream(); chart.TryExportPng(stream, new ChartExportOptions { Dpi = 0 }); }, "无效 DPI 通过 ExportFailed 提供明确错误。");
        var view = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "显式像素尺寸不受 DPI 改变；推导尺寸按布局 × DPI / 96 计算。PNG 可保留透明背景，JPEG 将透明区域合成白色；有损质量只影响 JPEG。异步调用在 UI 线程捕获当前画面，随后编码和写入可取消。保存到本地路径时采用临时文件替换；通用存储流保留提供方语义。", TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            new WrapPanel { Children = { format, size, dpi, background, quality, asynchronous, transparentChart } }, actions, chart, status,
        } };
        return new(view, chart, line, referenceY, status, () => { released = true; pauseGeneration++; pending?.Cancel(); chart.ExportFailed -= failure; chart.PlotAreaContent = null; chart.CancelAnimations(); chart.Series.Clear(); });
    }

    private sealed record ExportCompletionBoard(StackPanel View, CartesianChart Chart, LineSeries Line, NumericAxis ReferenceY, TextBlock Status, Action Release) : IDisposable
    { public void Dispose() => Release(); }
}
