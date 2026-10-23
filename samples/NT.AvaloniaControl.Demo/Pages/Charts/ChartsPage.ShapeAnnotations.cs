using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private CartesianChart? _shapeChart;
    private RectangleAnnotation? _shapeRectangle;
    private EllipseAnnotation? _shapeEllipse;
    private ArrowAnnotation? _shapeArrow;
    private Expander? _shapeExamples;

    private Control CreateShapeAnnotationsExamples()
    {
        var chart = CreateAdvancedChart("ShapeAnnotationsChart");
        _shapeChart = chart;
        chart.XAxis = new NumericAxis { Title = "观察时间 / 小时", Minimum = 0, Maximum = 10 };
        chart.YAxis = new NumericAxis { Title = "响应时间 / ms", Minimum = 0, Maximum = 100 };
        chart.Series.Add(new LineSeries
        {
            Title = "监测数据", Stroke = Brush("#8792A2"), StrokeThickness = 2, MarkerSize = 5,
            ItemsSource = new ChartPoint[] { new(0, 22), new(1, 32), new(2, 28), new(3, 45), new(4, 39), new(5, 56), new(6, 48), new(7, 68), new(8, 60), new(9, 73), new(10, 70) },
        });
        var rectangle = new RectangleAnnotation
        {
            X1 = 1, Y1 = 25, X2 = 3.4, Y2 = 55, Fill = Brush("#284385F4"), Stroke = Blue, StrokeThickness = 2,
            Layer = ChartAnnotationLayer.BehindSeries,
        };
        var ellipse = new EllipseAnnotation
        {
            X1 = 5, Y1 = 40, X2 = 8.2, Y2 = 80, Fill = Brush("#2819A798"), Stroke = Teal, StrokeThickness = 2,
            Layer = ChartAnnotationLayer.BehindSeries,
        };
        var arrow = new ArrowAnnotation
        {
            X1 = 2, Y1 = 30, X2 = 7.5, Y2 = 72, Stroke = Orange, Fill = Orange, StrokeThickness = 3,
            ArrowHeads = ChartArrowHeads.End, HeadLength = 14, HeadWidth = 11, DashStyle = new DashStyle(new double[] { 4, 3 }, 0),
        };
        _shapeRectangle = rectangle; _shapeEllipse = ellipse; _shapeArrow = arrow;
        chart.Annotations.Add(rectangle); chart.Annotations.Add(ellipse); chart.Annotations.Add(arrow);
        chart.Annotations.Add(new TextAnnotation { CoordinateUnit = ChartAnnotationCoordinateUnit.Pixel, X = 8, Y = 8, Text = "蓝框：对比区间  ·  绿椭圆：关注区域  ·  橙箭头：变化方向", Foreground = Brush("#8792A2"), FontSize = 12 });

        var status = new TextBlock { Text = "缩放或平移，观察标注跟随数据移动；也可把蓝框固定在绘图区。", Opacity = .7, TextWrapping = TextWrapping.Wrap };
        var actions = new WrapPanel();
        var visible = new CheckBox { Content = "显示形状标注", IsChecked = true, Margin = new Thickness(0, 0, 14, 6) };
        visible.IsCheckedChanged += (_, _) => { rectangle.IsVisible = ellipse.IsVisible = arrow.IsVisible = visible.IsChecked == true; };
        var pixel = new CheckBox { Content = "固定蓝色提示框", Margin = new Thickness(0, 0, 14, 6) };
        pixel.IsCheckedChanged += (_, _) =>
        {
            var fixedPosition = pixel.IsChecked == true;
            rectangle.CoordinateUnit = fixedPosition ? ChartAnnotationCoordinateUnit.Pixel : ChartAnnotationCoordinateUnit.Data;
            rectangle.X1 = fixedPosition ? 20 : 1; rectangle.Y1 = 25;
            rectangle.X2 = fixedPosition ? 180 : 3.4; rectangle.Y2 = fixedPosition ? 100 : 55;
            status.Text = fixedPosition ? "蓝框固定在绘图区左上方；绿椭圆与橙箭头继续跟随数据。" : "三个形状均跟随数据；可缩放查看局部区域。";
        };
        var reversed = new CheckBox { Content = "横轴反向", Margin = new Thickness(0, 0, 14, 6) };
        reversed.IsCheckedChanged += (_, _) => chart.XAxis.IsInversed = reversed.IsChecked == true;
        var dashed = new CheckBox { Content = "虚线箭杆", IsChecked = true, Margin = new Thickness(0, 0, 14, 6) };
        dashed.IsCheckedChanged += (_, _) => arrow.DashStyle = dashed.IsChecked == true ? new DashStyle(new double[] { 4, 3 }, 0) : null;
        var heads = new ComboBox { ItemsSource = new[] { "无箭头", "起点箭头", "终点箭头", "双端箭头" }, SelectedIndex = 2, MinWidth = 120, Margin = new Thickness(0, 0, 8, 6) };
        heads.SelectionChanged += (_, _) => arrow.ArrowHeads = (ChartArrowHeads)Math.Max(0, heads.SelectedIndex);
        actions.Children.Add(visible); actions.Children.Add(pixel); actions.Children.Add(reversed); actions.Children.Add(dashed); actions.Children.Add(heads);
        AddAction(actions, "移动关注区域", () =>
        {
            var early = ellipse.X1 >= 5; ellipse.X1 = early ? 2.5 : 5; ellipse.X2 = early ? 5.7 : 8.2;
            status.Text = early ? "关注区域已移动到前半程，可比较这段时间的数据。" : "关注区域已恢复到后半程。";
        }, "移动绿椭圆，突出另一个时间区间。");
        AddAction(actions, "恢复全部范围", chart.ResetViewport, "恢复这张图的缩放和平移。");
        return _shapeExamples = new Expander
        {
            Name = "ChartShapeAnnotationsExamples", Header = "形状标注：范围、关注区域与变化方向", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 8, 0, 0), Children =
            {
                new TextBlock { Text = "用矩形对比一段数据，用椭圆强调关注区域，用箭头说明变化方向。箭头大小随屏幕保持清晰；椭圆由两个角点投影后的边界确定。", TextWrapping = TextWrapping.Wrap, Opacity = .7 },
                actions, CreateChartCard("响应变化与关注区域", "SHAPE ANNOTATIONS", "蓝框、绿椭圆和橙箭头可独立设置描边、填充与前后层次", chart), status,
            } },
        };
    }
}
