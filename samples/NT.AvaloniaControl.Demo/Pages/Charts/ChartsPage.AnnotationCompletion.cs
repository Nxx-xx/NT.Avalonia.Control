using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private Expander? _annotationCompletionExamples;
    private AnnotationCompletionBoard? _annotationCompletionBoard;

    private Control CreateAnnotationCompletionExamples()
    {
        _annotationCompletionBoard = CreateAnnotationCompletionBoard(true);
        return _annotationCompletionExamples = new Expander
        {
            Name = "ChartAnnotationCompletionExamples", Header = "标注与参考元素：独立坐标轴、圆角、标签与绘图区模板",
            HorizontalAlignment = HorizontalAlignment.Stretch, Content = _annotationCompletionBoard.View,
        };
    }

    private AnnotationCompletionBoard CreateAnnotationCompletionBoard(bool register)
    {
        var chart = register ? CreateAdvancedChart("AnnotationCompletionChart") : new CartesianChart { Name = "AnnotationCompletionChart" };
        chart.Height = 410; chart.Title = "参考元素使用自己的坐标轴，无需占位系列";
        chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 10, Title = "主轴：时间 / 小时" };
        chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "主轴：负载 / %" };
        chart.IsPanEnabled = true; chart.IsZoomEnabled = true; chart.IsTooltipEnabled = false;
        chart.WheelMode = ChartWheelMode.Zoom; chart.InteractionAxes = ChartInteractionAxes.Both;
        var referenceX = new NumericAxis { Key = "reference-x", Minimum = 0, Maximum = 100, Title = "参考横轴 / 含 40–60 断轴", Position = ChartAxisPosition.Far, LabelBrush = Orange, TitleBrush = Orange };
        referenceX.ScaleBreaks.Add(new ChartScaleBreak { Start = 40, End = 60, CompressionRatio = .2 });
        var referenceY = new LogarithmicAxis { Key = "reference-y", Minimum = 1, Maximum = 1000, Title = "参考纵轴 / 对数", Position = ChartAxisPosition.Far, LabelBrush = Teal, TitleBrush = Teal, IsInversed = true };
        chart.Axes.Add(referenceX); chart.Axes.Add(referenceY);
        chart.Series.Add(new LineSeries { Title = "主轴监测值", Stroke = Brush("#8792A2"), StrokeThickness = 2, MarkerSize = 5,
            ItemsSource = new ChartPoint[] { new(0, 18), new(1, 26), new(2, 19), new(3, 38), new(4, 32), new(5, 62), new(6, 54), new(7, 76), new(8, 64), new(9, 83), new(10, 72) } });
        var rectangle = new RectangleAnnotation { XAxisKey = referenceX.Key, YAxisKey = referenceY.Key,
            X1 = 8, X2 = 32, Y1 = 6, Y2 = 35, CornerRadius = 16, Fill = Brush("#304385F4"), Stroke = Blue, StrokeThickness = 3,
            Label = "圆角比较区", LabelPosition = ChartShapeLabelPosition.TopLeft, LabelOffset = new Vector(8, 6), Foreground = Blue, FontSize = 12 };
        var ellipse = new EllipseAnnotation { XAxisKey = referenceX.Key, YAxisKey = referenceY.Key,
            GeometryMode = ChartEllipseGeometryMode.CenterRadius, X = 73, Y = 220, RadiusX = 10, RadiusY = 150,
            Fill = Brush("#3019A798"), Stroke = Teal, StrokeThickness = 3, Label = "中心 / 数据半径", LabelPosition = ChartShapeLabelPosition.Center,
            Foreground = Teal, FontSize = 12 };
        var arrow = new ArrowAnnotation { XAxisKey = referenceX.Key, YAxisKey = referenceY.Key,
            X1 = 26, Y1 = 200, X2 = 83, Y2 = 700, Stroke = Orange, Fill = Orange, StrokeThickness = 3,
            ArrowHeads = ChartArrowHeads.Both, HeadLength = 14, HeadWidth = 12, Label = "参考趋势", LabelPosition = ChartShapeLabelPosition.End,
            LabelOffset = new Vector(-36, -18), Foreground = Orange, FontSize = 12 };
        var fixedRectangle = new RectangleAnnotation { CoordinateUnit = ChartAnnotationCoordinateUnit.Pixel, X1 = 18, Y1 = 8, X2 = 172, Y2 = 44,
            CornerRadius = 9, Fill = Brush("#CB11A5"), Label = "固定 DIP 提示", Foreground = Brushes.White, FontSize = 12 };
        var referenceLine = new LineAnnotation { YAxisKey = referenceY.Key, Value = 100, Stroke = Teal, StrokeThickness = 2,
            DashStyle = new DashStyle(new double[] { 5, 3 }, 0), Label = "参考阈值 100" };
        var referenceBand = new BandAnnotation { XAxisKey = referenceX.Key, Orientation = Orientation.Vertical, FromValue = 35, ToValue = 65,
            Fill = Brush("#18F2A33A"), Layer = ChartAnnotationLayer.BehindSeries };
        var dataText = new TextAnnotation { XAxisKey = referenceX.Key, YAxisKey = referenceY.Key, X = 6, Y = 420,
            Text = "参考坐标上的文字", Foreground = Orange, FontSize = 12 };
        chart.Annotations.Add(referenceBand); chart.Annotations.Add(rectangle); chart.Annotations.Add(ellipse);
        chart.Annotations.Add(arrow); chart.Annotations.Add(referenceLine); chart.Annotations.Add(dataText); chart.Annotations.Add(fixedRectangle);
        ChartAnnotationRenderContext? lastContext = null;
        var probe = new CustomAnnotation { XAxisKey = referenceX.Key, YAxisKey = referenceY.Key, Renderer = (_, context) => lastContext = context };
        chart.Annotations.Add(probe);
        var note = new AnnotationCompletionNote();
        var contentTemplate = CreateAnnotationCompletionTemplate();
        chart.PlotAreaContent = note; chart.PlotAreaContentTemplate = contentTemplate;
        var status = new TextBlock { Name = "AnnotationCompletionStatus", FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = .8 };
        void Update()
        {
            status.Text = $"仅有 {chart.Series.Count} 个主轴系列；参考轴由标注使用。主轴 X {chart.VisibleRange?.XMinimum:0.#}–{chart.VisibleRange?.XMaximum:0.#}；参考轴可独立设置视图。中心半径椭圆投影的是中心 ± 数据半径，对数轴上屏幕中心不等于数据中心投影。";
        }
        void OnViewport(object? sender, EventArgs args) => Update();
        chart.ViewportChanged += OnViewport;
        var visible = new CheckBox { Name = "AnnotationCompletionVisible", Content = "显示参考元素", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        visible.IsCheckedChanged += (_, _) => { foreach (var annotation in chart.Annotations.Where(item => !ReferenceEquals(item, probe))) annotation.IsVisible = visible.IsChecked == true; };
        var inversed = new CheckBox { Name = "AnnotationCompletionInverse", Content = "参考纵轴反向", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        inversed.IsCheckedChanged += (_, _) => referenceY.IsInversed = inversed.IsChecked == true;
        var labels = new CheckBox { Name = "AnnotationCompletionLabels", Content = "形状标签", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        labels.IsCheckedChanged += (_, _) => { rectangle.Label = labels.IsChecked == true ? "圆角比较区" : null; ellipse.Label = labels.IsChecked == true ? "中心 / 数据半径" : null; arrow.Label = labels.IsChecked == true ? "参考趋势" : null; };
        var content = new CheckBox { Name = "AnnotationCompletionContent", Content = "绘图区内容模板", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        content.IsCheckedChanged += (_, _) => chart.PlotAreaContent = content.IsChecked == true ? note : null;
        var radius = new ComboBox { Name = "AnnotationCompletionRadius", ItemsSource = new[] { "圆角 16 DIP", "直角", "圆角 28 DIP" }, SelectedIndex = 0, MinWidth = 130, Margin = new Thickness(0, 0, 8, 6) };
        radius.SelectionChanged += (_, _) => rectangle.CornerRadius = radius.SelectedIndex switch { 1 => 0, 2 => 28, _ => 16 };
        var actions = new WrapPanel { Children = { visible, inversed, labels, content, radius } };
        AddAction(actions, "放大多轴视图", () => { chart.Viewport = new ChartViewport(2, 8, 20, 80); referenceX.ViewMinimum = 10; referenceX.ViewMaximum = 90; referenceY.ViewMinimum = 3; referenceY.ViewMaximum = 900; Update(); }, "主轴和独立参考轴采用各自的数据域，像素提示仍保持 DIP 尺寸。");
        AddAction(actions, "更新模板消息", () => note.Message = note.Message == "参考元素已就绪" ? "消息来自同一个业务对象" : "参考元素已就绪", "更新同一个业务对象的属性，观察绘图区模板刷新。");
        AddAction(actions, "恢复完整视图", () => { chart.ResetViewport(); Update(); }, "恢复主轴及额外参考轴的完整范围。");
        var export = new Button { Name = "AnnotationCompletionExport", Content = "导出 PNG", Margin = new Thickness(0, 0, 8, 6) };
        export.Click += async (_, _) =>
        {
            var file = await _hostWindow.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions { Title = "导出参考元素示例", SuggestedFileName = "chart-annotations.png", DefaultExtension = "png" });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            status.Text = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(1200, 700), Background = chart.Background }) ? "PNG 已导出，包含参考元素与绘图区模板。" : "当前图表尚未布局，未生成 PNG。";
        };
        actions.Children.Add(export);
        var view = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = "蓝色圆角框、绿色中心半径椭圆、双端箭头及阈值线共用独立参考轴：横轴含断轴，纵轴为反向对数轴。紫色框使用绘图区 DIP；右上角为可绑定的绘图区内容模板。滚轮、拖动或按钮缩放后，导出与屏幕沿用相同投影。", TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            actions, chart, status,
        } };
        view.AttachedToVisualTree += (_, _) => Update();
        return new(view, chart, referenceX, referenceY, rectangle, ellipse, arrow, fixedRectangle, referenceLine, referenceBand, dataText, probe, note, contentTemplate,
            () => lastContext, () => { chart.ViewportChanged -= OnViewport; chart.PlotAreaContent = null; chart.Series.Clear(); });
    }

    private static IDataTemplate CreateAnnotationCompletionTemplate() => new FuncDataTemplate<ChartPlotAreaContext>((_, _) =>
    {
        var text = new TextBlock { Name = "AnnotationCompletionMessage", FontSize = 12, Foreground = Brushes.White };
        text.Bind(TextBlock.TextProperty, new Binding("Content.Message"));
        var badge = new Border { Name = "AnnotationCompletionBadge", Background = Brush("#7B2688"), Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(7), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(8), Child = text };
        return new Grid { Name = "AnnotationCompletionTemplateRoot", Children = { badge } };
    });

    private sealed class AnnotationCompletionNote : INotifyPropertyChanged
    {
        private string _message = "参考元素已就绪";
        public string Message { get => _message; set { if (_message == value) return; _message = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed record AnnotationCompletionBoard(StackPanel View, CartesianChart Chart, NumericAxis ReferenceX, LogarithmicAxis ReferenceY,
        RectangleAnnotation Rectangle, EllipseAnnotation Ellipse, ArrowAnnotation Arrow, RectangleAnnotation FixedRectangle,
        LineAnnotation ReferenceLine, BandAnnotation ReferenceBand, TextAnnotation DataText, CustomAnnotation Probe,
        AnnotationCompletionNote Note, IDataTemplate ContentTemplate, Func<ChartAnnotationRenderContext?> LastContext, Action Release) : IDisposable
    { public void Dispose() => Release(); }
}
