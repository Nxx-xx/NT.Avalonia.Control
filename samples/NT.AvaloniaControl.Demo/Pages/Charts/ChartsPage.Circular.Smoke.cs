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
    private readonly List<object> _circularEvidence = [];

    private async Task RunCircularSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        _circularExamples!.IsExpanded = true; await SmokeFlushAsync();
        var directory = Path.GetDirectoryName(baseImage)!; var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var label = dark ? "暗色" : "亮色";
            string Image(string scenario) => Path.Combine(directory, stem + $"-circular-{(dark ? "dark" : "light")}-{scenario}.png");
            void Check(string name, bool value) => checks[$"圆形图{label}{name}"] = value;
            using var board = CreateCircularBoard();
            foreach (var item in board.Charts) { item.AnimationTriggers = ChartAnimationTriggers.None; item.CancelAnimations(); }
            var window = new Window { Title = "Charts · 圆形图", Width = 1160, Height = 1160, RequestedThemeVariant = theme,
                Background = Brush(dark ? "#111722" : "#F8FAFC"), Foreground = Brush(dark ? "#EAF0FB" : "#172238"), Content = new Border { Padding = new Thickness(16), Child = board.View } };
            CircularChart? chart = null; var probes = new List<object>(); var centerContentProbes = new List<object>(); var largeCenterContentProbes = new List<object>(); var exports = new List<object>(); var phases = new List<object>();
            try
            {
                window.Show(_hostWindow); await Task.Delay(80); FlushControlTemplateWindow(window);
                var native = window.TryGetPlatformHandle() is { Handle: not 0 };
                var boardLayouts = board.Charts.Select(value => new { value.Name, value.Bounds, value.PlotArea }).ToArray();
                Check("六图原生布局和真实业务源", native && board.Charts.All(value => value.Bounds.Width > 400 && value.Bounds.Height >= 280 && value.GetSlices().Count > 0) && ReferenceEquals(board.Pie.ItemsSource, board.Records));
                var pageImage = Image("page"); SaveWindow(pageImage);
                board.NegativePolicy.SelectedItem = ChartCircularNegativeValueMode.Absolute; board.Labels.SelectedItem = CircularLabelPosition.Inside; board.HideTiny.IsChecked = true; board.ReducedMotion.IsChecked = true; board.Pause.IsChecked = true; FlushControlTemplateWindow(window);
                Check("用户负值小扇区标签和动画控制生效", board.Charts.SelectMany(value => value.Series).All(value => value.NegativeValueMode == ChartCircularNegativeValueMode.Absolute) && board.Charts.All(value => value.LabelPosition == CircularLabelPosition.Inside && value.ReduceMotion && value.IsAnimationPaused) && board.Small.SmallSliceMode == ChartCircularSmallSliceMode.Hide);
                var changedPageImage = Image("page-absolute-inside-tiny"); SaveWindow(changedPageImage);
                chart = new CircularChart { Background = window.Background, Foreground = window.Foreground, Padding = default, ShowLegend = false, ShowDataLabels = false, LabelPosition = CircularLabelPosition.None, SelectionMode = ChartSelectionMode.None, SelectedExplodeOffset = 0, AnimationTriggers = ChartAnimationTriggers.None };
                window.Content = chart; window.Width = 700; window.Height = 520; FlushControlTemplateWindow(window);
                var records = CircularRecords().Take(3).ToArray();
                var pie = Source(new PieSeries(), records); chart.Series.Add(pie); FlushControlTemplateWindow(window);
                var layout = IndependentLayout(false);
                Check("独立布局公式与公开绘图区一致", Near(chart.PlotArea.X, 8) && Near(chart.PlotArea.Y, 8) && Near(chart.PlotArea.Width, chart.Bounds.Width - 16) && Near(chart.PlotArea.Height, chart.Bounds.Height - 16));
                Probe("pie-share-blue", layout, 0, .75, ColorsFor(0), 180, .75);
                Probe("pie-share-teal", layout, 135, .75, ColorsFor(1), 0, .75);
                Probe("pie-share-orange", layout, 225, .75, ColorsFor(2), 90, .75);
                Check("饼图份额和源身份未改写", chart.GetSlices() is { Count: 3 } slices && Near(slices[0].Fraction, .5) && Near(slices[1].Fraction, .25) && Near(slices[2].Fraction, .25) && slices[1].Data is { SourceIndex: 1, Point.Y: 25 } data && ReferenceEquals(data.Item, records[1]) && Equals(data.StableId, "circular-b") && Equals(data.Category, "门店"));
                chart.Series.Clear(); var donut = Source(new DonutSeries(), records); chart.Series.Add(donut); FlushControlTemplateWindow(window);
                layout = IndependentLayout(false); Probe("donut-blue-hole", layout, 0, .8, ColorsFor(0), 0, 0);
                Check("圆环中心无扇区命中", chart.HitTest(layout.Center) is null && chart.HitTest(Polar(layout, 0, .8)) is { Data.SourceIndex: 0 });
                chart.Series.Clear(); donut = Source(new DonutSeries(), [records[0] with { Value = 100 }]); chart.Series.Add(donut); FlushControlTemplateWindow(window);
                Probe("donut-single-full-ring", IndependentLayout(false), 180, .8, ColorsFor(0), 0, 0);
                CenterContentProbe("center-content-donut", false);
                chart.Series.Clear(); var semi = Source(new SemiDonutSeries(), records); chart.Series.Add(semi); FlushControlTemplateWindow(window);
                layout = IndependentLayout(true); Probe("semi-blue-lower-gap", layout, 225, .8, ColorsFor(0), 90, .2, requireBackground: true);
                Check("半环默认角度与占比", chart.GetSlices().Sum(value => value.RawSweepAngle) == 180 && semi.StartAngle == 180 && semi.InnerRadius == .6);
                semi.ItemsSource = new[] { records[0] with { Value = 100 } }; FlushControlTemplateWindow(window);
                CenterContentProbe("center-content-semi", true);
                chart.Width = 1160; chart.Height = 1160; window.Width = 1160; window.Height = 1160; FlushControlTemplateWindow(window);
                chart.Series.Clear(); var largeDonut = Source(new DonutSeries(), [records[0] with { Value = 100 }]); chart.Series.Add(largeDonut); FlushControlTemplateWindow(window);
                CenterContentProbe("center-content-large-donut", false, largeCenterContentProbes, true);
                chart.Series.Clear(); var largeSemi = Source(new SemiDonutSeries(), [records[0] with { Value = 100 }]); chart.Series.Add(largeSemi); FlushControlTemplateWindow(window);
                CenterContentProbe("center-content-large-semi", true, largeCenterContentProbes, true);
                chart.Width = double.NaN; chart.Height = double.NaN; window.Width = 700; window.Height = 520; FlushControlTemplateWindow(window);
                chart.Series.Clear(); donut = Source(new DonutSeries { SweepAngle = 270 }, records); chart.Series.Add(donut); FlushControlTemplateWindow(window);
                Probe("partial-arc-missing-quadrant", IndependentLayout(false), -22.5, .8, ColorsFor(0), 225, .8, requireBackground: true);
                chart.Series.Clear(); pie = Source(new PieSeries { Clockwise = false }, records); chart.Series.Add(pie); FlushControlTemplateWindow(window);
                Probe("counterclockwise-blue", IndependentLayout(false), 180, .75, ColorsFor(0), 0, .75);
                Check("逆时针保留原份额与方向", chart.GetSlices().All(value => value.SweepAngle < 0) && Near(chart.GetSlices()[0].Fraction, .5));
                pie.Clockwise = true; pie.SliceGap = 12; FlushControlTemplateWindow(window);
                Probe("slice-gap", IndependentLayout(false), 0, .8, ColorsFor(0), 90, .8, requireBackground: true);
                Check("扇区间隔不重算原始份额", Near(chart.GetSlices()[0].Fraction, .5) && Near(chart.GetSlices()[0].RawSweepAngle, 180) && Near(chart.GetSlices()[0].SweepAngle, 168));
                pie.SliceGap = 0; pie.ItemsSource = CircularRecords(); pie.NegativeValueMode = ChartCircularNegativeValueMode.Ignore; FlushControlTemplateWindow(window);
                Check("零值和默认负值不分配角度但保留源记录", chart.GetSlices().Count == 3 && pie.CreateSnapshot().Points.Count == 5);
                pie.NegativeValueMode = ChartCircularNegativeValueMode.Absolute; FlushControlTemplateWindow(window);
                Check("绝对值模式保留负数原值及身份", chart.GetSlices().Single(value => value.Data.SourceIndex == 4) is { Data.Point.Y: -10 } negative && Near(negative.Fraction, 10d / 110));
                chart.Series.Clear(); var rejected = false;
                var invalid = Source(new PieSeries { NegativeValueMode = ChartCircularNegativeValueMode.Throw }, CircularRecords());
                try { chart.Series.Add(invalid); _ = chart.GetSlices(); } catch (InvalidOperationException) { rejected = true; } finally { chart.Series.Clear(); }
                Check("Throw策略明确拒绝负值", rejected);
                var tinyRows = new[] { new CircularRecord("large", "主要", 99, "#4385F4"), new CircularRecord("tiny", "小项", 1, "#F2A33A") };
                pie = Source(new PieSeries { MinimumSliceAngle = 5 }, tinyRows); chart.Series.Add(pie); FlushControlTemplateWindow(window);
                Probe("tiny-sector-keep", IndependentLayout(false), 268.2, .8, ColorsFor(2), 0, .8, minimumInk: 6);
                pie.SmallSliceMode = ChartCircularSmallSliceMode.Hide; FlushControlTemplateWindow(window);
                var tinyImage = Image("tiny-sector-hide"); var tinyRaster = CircularCapture(chart, tinyImage); var tinyPoint = Polar(IndependentLayout(false), 268.2, .8); var tinyInk = Ink(tinyRaster, tinyPoint, ColorsFor(2));
                Check("隐藏小扇区不放大其份额且移除像素和命中", tinyInk == 0 && chart.HitTest(tinyPoint) is null && chart.GetSlices().Single(value => value.Data.SourceIndex == 1) is { IsVisible: false } hidden && Near(hidden.Fraction, .01));
                var tinyLayout = IndependentLayout(false);
                probes.Add(new { scenario = "tiny-sector-hide", image = tinyImage, width = tinyRaster.Width, height = tinyRaster.Height, controlBounds = chart.Bounds, layout = new { center = tinyLayout.Center, radius = tinyLayout.Radius }, negativeAngle = 268.2, negativeRadius = .8, negativePixel = tinyPoint, color = "#F2A33A", negativeInkPixels = tinyInk, pixelSquareDip = 6, colorChannelTolerance = 24 });
                chart.Series.Clear(); pie = Source(new PieSeries(), records); chart.Series.Add(pie); chart.LabelPosition = CircularLabelPosition.Outside; chart.ShowDataLabels = true; chart.LabelConnectorBrush = Brush("#D7259F"); chart.LabelConnectorThickness = 2; FlushControlTemplateWindow(window);
                var labelsImage = Image("outside-labels-connectors"); var labelsRaster = CircularCapture(chart, labelsImage); var connectorInk = labelsRaster.Count(new PixelRect(0, 0, labelsRaster.Width, labelsRaster.Height), Color.Parse("#D7259F"), 24);
                var outsideLabels = chart.GetLabels();
                Check("外标签在控制范围内且不重叠", outsideLabels.Count == 3 && outsideLabels.All(value => new Rect(chart.Bounds.Size).Contains(value.Bounds) && value.Connector.Count >= 2) && outsideLabels.SelectMany((left, index) => outsideLabels.Skip(index + 1).Select(right => !left.Bounds.Intersects(right.Bounds))).All(value => value));
                chart.ShowDataLabels = false; FlushControlTemplateWindow(window); var noLabelsImage = Image("outside-labels-off"); var noLabels = CircularCapture(chart, noLabelsImage);
                Check("外标签连线真实绘制且关闭后移除", connectorInk > 20 && noLabels.Count(new PixelRect(0, 0, noLabels.Width, noLabels.Height), Color.Parse("#D7259F"), 24) == 0 && CountControlTemplateExactChanges(labelsRaster, noLabels, new Rect(chart.Bounds.Size)) > 100);
                probes.Add(new { scenario = "outside-labels-connectors", image = labelsImage, withoutImage = noLabelsImage, width = labelsRaster.Width, height = labelsRaster.Height, controlBounds = chart.Bounds, connectorColor = "#D7259F", connectorInkPixels = connectorInk, minimumInk = 20, colorChannelTolerance = 24 });
                chart.LabelPosition = CircularLabelPosition.None; chart.SelectedExplodeOffset = 8; chart.SelectionMode = ChartSelectionMode.Multiple; FlushControlTemplateWindow(window);
                var beforeSelectionImage = Image("selection-before"); var beforeSelection = CircularCapture(chart, beforeSelectionImage); var candidate = chart.GetSlices().Single(value => value.Data.SourceIndex == 1);
                void Cancel(object? sender, ChartSelectionChangingEventArgs args) { if (args.CanCancel) args.Cancel = true; }
                chart.SelectionChanging += Cancel; SelectionRangeClick(chart, window, candidate.Hit.Position); chart.SelectionChanging -= Cancel;
                Check("选择前置取消拦截程序化指针", chart.SelectedPoints.Count == 0);
                SelectionRangeClick(chart, window, candidate.Hit.Position); FlushControlTemplateWindow(window);
                var selectionImage = Image("selection-exploded"); var selectedRaster = CircularCapture(chart, selectionImage);
                Check("选择经指针路由保留业务身份并改变显示", chart.SelectedPoint is { Index: 1, Point.Y: 25 } selected && ReferenceEquals(selected.Item, records[1]) && Equals(selected.StableId, "circular-b") && CountControlTemplateExactChanges(beforeSelection, selectedRaster, new Rect(chart.Bounds.Size)) > 30);
                var selectedSources = records.Select((item, index) => index == 1 ? item with { Value = 50 } : item).ToArray(); pie.ItemsSource = selectedSources; _ = chart.GetSlices(); FlushControlTemplateWindow(window);
                Check("数据替换按业务ID更新选择项", chart.SelectedPoint is { Index: 1, Point.Y: 50 } newSelection && ReferenceEquals(newSelection.Item, selectedSources[1]) && Equals(newSelection.StableId, "circular-b"));
                var selectionControlBounds = chart.Bounds;
                chart.ClearSelection(); chart.SelectionMode = ChartSelectionMode.None; chart.SelectedExplodeOffset = 0; pie.ItemsSource = records; _ = chart.GetSlices(); FlushControlTemplateWindow(window);
                chart.AnimationTriggers = ChartAnimationTriggers.All; chart.AnimationDuration = TimeSpan.FromMilliseconds(1500); chart.AnimationEasing = new LinearEasing(); chart.ReduceMotion = false;
                var changedRows = records.Select((item, index) => item with { Value = index == 0 ? 20 : index == 1 ? 55 : 25 }).ToArray(); pie.ItemsSource = changedRows; _ = chart.GetSlices(); FlushControlTemplateWindow(window);
                await WaitProgress(); chart.IsAnimationPaused = true; FlushControlTemplateWindow(window);
                var progress = chart.AnimationProgress; var middle = chart.GetSlices().Select(value => new { value.Data.SourceIndex, value.StartAngle, value.SweepAngle }).ToArray();
                var animationImage = Image("data-animation-paused"); var animationRaster = CircularCapture(chart, animationImage); await Task.Delay(100); FlushControlTemplateWindow(window);
                var pausedImage = Image("data-animation-still-paused"); var pausedRaster = CircularCapture(chart, pausedImage);
                Check("数据过渡中间帧可暂停且像素稳定", progress > 0 && progress < 1 && Near(chart.AnimationProgress, progress) && middle.SequenceEqual(chart.GetSlices().Select(value => new { value.Data.SourceIndex, value.StartAngle, value.SweepAngle })) && CountControlTemplateExactChanges(animationRaster, pausedRaster, new Rect(chart.Bounds.Size)) == 0);
                phases.Add(new { scenario = "data-animation-paused", animationImage, pausedImage, progress, state = chart.AnimationState.ToString(), middle });
                foreach (var dpi in new[] { 96d, 144d, 192d })
                {
                    using var stream = new MemoryStream(); var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(animationRaster.Width, animationRaster.Height), Dpi = dpi, Background = chart.Background });
                    var exportImage = Image($"paused-export-{dpi:0}"); File.WriteAllBytes(exportImage, stream.ToArray()); stream.Position = 0; using var bitmap = new Bitmap(stream); var raster = ReadShapePixels(bitmap);
                    var changed = CountControlTemplateExactChanges(animationRaster, raster, new Rect(chart.Bounds.Size)); var density = TryReadCommonPngDensity(stream.ToArray(), out var dx, out var dy, out var unit) && dx == (uint)Math.Round(dpi / .0254) && dx == dy && unit == 1;
                    Check($"暂停帧{dpi:0}DPI导出尺寸密度和像素一致", exported && density && changed == 0 && raster.Width == animationRaster.Width && raster.Height == animationRaster.Height);
                    exports.Add(new { dpi, baselineImage = animationImage, image = exportImage, exported, width = raster.Width, height = raster.Height, controlBounds = chart.Bounds, changedPixels = changed, densityMatches = density });
                }
                chart.CancelAnimations(); chart.IsAnimationPaused = false; FlushControlTemplateWindow(window);
                Check("取消显示最新业务目标", Near(chart.GetSlices().Single(value => value.Data.SourceIndex == 0).Fraction, .2) && chart.AnimationProgress == 1);
                chart.ReduceMotion = true; pie.ItemsSource = records; _ = chart.GetSlices(); chart.ReplayEntranceAnimation(); FlushControlTemplateWindow(window);
                Check("减少动态效果直接显示目标并跳过重播", Near(chart.GetSlices()[0].Fraction, .5) && chart.AnimationProgress == 1);
                chart.ReduceMotion = false; chart.IsAnimationPaused = true; chart.ReplayEntranceAnimation(); _ = chart.GetSlices(); FlushControlTemplateWindow(window);
                Check("暂停入场保留起始帧", chart.IsAnimationPaused && chart.AnimationProgress == 0);
                chart.IsAnimationPaused = false; await WaitProgress(); chart.CancelAnimations(); FlushControlTemplateWindow(window);
                Check("恢复入场并可取消完成", chart.AnimationProgress == 1 && chart.GetSlices().Count(value => value.IsVisible) == 3);
                _circularEvidence.Add(new { theme = label, nativeWindowCreated = native, pageImage, changedPageImage, boardLayouts, probes, centerContentProbes, largeCenterContentProbes, exports, phases, labelsImage, noLabelsImage, connectorInkPixels = connectorInk, outsideLabels = outsideLabels.Select(value => new { sourceIndex = value.Slice.Data.SourceIndex, value.Text, value.Bounds, value.Connector }), beforeSelectionImage, selectionImage, selectionControlBounds,
                    oracle = new { values = new[] { 50d, 25, 25 }, colors = new[] { "#4385F4", "#19A798", "#F2A33A" }, background = dark ? "#111722" : "#F8FAFC", startAngle = -90, sweepAngle = 360, clockwise = true, insetDip = 8, selectionMode = "None", noTitleLegendOrLabels = true, fullRadiusFormula = "min(width-16,height-16)/2", semiRadiusFormula = "min((width-16)/2,height-16)", semiCenterFormula = "(width/2,height/2+radius/2)", pixelSquareDip = 6, colorChannelTolerance = 24 }, physicalInputValidated = false, performanceMeasured = false,
                    evidenceKind = "native-window-rendertargetbitmap-preregistered-independent-polar-business-oracles-and-public-contracts" });
            }
            finally { window.Close(); chart?.CancelAnimations(); chart?.Series.Clear(); }

            void SaveWindow(string path) { using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path); }
            (Point Center, double Radius) IndependentLayout(bool semi)
            {
                var radius = semi ? Math.Min((chart!.Bounds.Width - 16) / 2, chart.Bounds.Height - 16) : Math.Min(chart!.Bounds.Width - 16, chart.Bounds.Height - 16) / 2;
                return (new(chart.Bounds.Width / 2, chart.Bounds.Height / 2 + (semi ? radius / 2 : 0)), radius);
            }
            void Probe(string scenario, (Point Center, double Radius) expectedLayout, double positiveAngle, double positiveRadius, Color color, double negativeAngle, double negativeRadius, bool requireBackground = false, int minimumInk = 10)
            {
                var image = Image(scenario); var raster = CircularCapture(chart!, image); var positive = Polar(expectedLayout, positiveAngle, positiveRadius); var negative = Polar(expectedLayout, negativeAngle, negativeRadius);
                var positiveInk = Ink(raster, positive, color); var negativeInk = Ink(raster, negative, color); var backgroundInk = Ink(raster, negative, ((ISolidColorBrush)chart!.Background!).Color);
                Check(scenario + "独立极坐标正负像素", positiveInk >= minimumInk && negativeInk == 0 && (!requireBackground || backgroundInk >= 10));
                probes.Add(new { scenario, image, width = raster.Width, height = raster.Height, controlBounds = chart.Bounds, layout = new { center = expectedLayout.Center, radius = expectedLayout.Radius }, positiveAngle, positiveRadius, negativeAngle, negativeRadius, positivePixel = positive, negativePixel = negative, color = color.ToString(), positiveInkPixels = positiveInk, negativeInkPixels = negativeInk, backgroundInkPixels = backgroundInk, requireBackground, minimumInk, pixelSquareDip = 6, colorChannelTolerance = 24 });
            }
            void CenterContentProbe(string scenario, bool semi, List<object>? evidenceTarget = null, bool requireLargeBounds = false)
            {
                var color = Color.Parse("#D7259F"); var content = new Border { Width = 600, Height = 300, Background = new SolidColorBrush(color) };
                chart!.CenterContent = content; FlushControlTemplateWindow(window);
                var expected = IndependentLayout(semi); var positive = new Point(expected.Center.X, expected.Center.Y - .4 * .6 * expected.Radius); var negative = new Point(expected.Center.X + .8 * .6 * expected.Radius, expected.Center.Y);
                var image = Image(scenario); var raster = CircularCapture(chart, image); var positiveInk = Ink(raster, positive, color); var negativeInk = Ink(raster, negative, color);
                Check(scenario + "中心内容限制在洞内的宿主范围", positiveInk >= 10 && negativeInk == 0 && chart.CenterContentBounds.Contains(positive) && !chart.CenterContentBounds.Contains(negative) && (!requireLargeBounds || Near(chart.Bounds.Width, 1160) && Near(chart.Bounds.Height, 1160)));
                using var stream = new MemoryStream(); var exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(raster.Width, raster.Height), Dpi = 96, Background = chart.Background });
                var exportImage = Image(scenario + "-export-96"); File.WriteAllBytes(exportImage, stream.ToArray()); stream.Position = 0; using var bitmap = new Bitmap(stream); var exportRaster = ReadShapePixels(bitmap); var changes = CountControlTemplateExactChanges(raster, exportRaster, new Rect(chart.Bounds.Size));
                Check(scenario + "中心内容导出像素和裁剪一致", exported && changes == 0 && Ink(exportRaster, positive, color) >= 10 && Ink(exportRaster, negative, color) == 0);
                (evidenceTarget ?? centerContentProbes).Add(new { scenario, image, exportImage, controlBounds = chart.Bounds, centerContentBounds = chart.CenterContentBounds, requestedWindowWidth = window.Width, requestedWindowHeight = window.Height, requestedControlWidth = double.IsFinite(chart.Width) ? (double?)chart.Width : null, requestedControlHeight = double.IsFinite(chart.Height) ? (double?)chart.Height : null, requireLargeBounds, width = raster.Width, height = raster.Height, childWidth = 600, childHeight = 300, color = "#D7259F", innerRadius = .6, sourceValues = new[] { 100d }, positivePixel = positive, negativePixel = negative, positiveInkPixels = positiveInk, negativeInkPixels = negativeInk, exported, changedPixels = changes, minimumInk = 10, pixelSquareDip = 6, colorChannelTolerance = 24 });
                chart.CenterContent = null; FlushControlTemplateWindow(window);
            }
            async Task WaitProgress()
            {
                var clock = Stopwatch.StartNew(); while (chart!.AnimationProgress < .12 && clock.Elapsed < TimeSpan.FromSeconds(4)) { await Task.Delay(20); _ = chart.GetSlices(); FlushControlTemplateWindow(window); }
            }
        }
        _circularExamples.IsExpanded = false;
        static T Source<T>(T series, CircularRecord[] records) where T : CircularSeries { series.ItemsSource = records; series.ValuePath = nameof(CircularRecord.Value); series.CategoryPath = nameof(CircularRecord.Label); series.IdPath = nameof(CircularRecord.Id); series.ColorPath = nameof(CircularRecord.Color); return series; }
        static Point Polar((Point Center, double Radius) layout, double angle, double radius) { var radians = angle * Math.PI / 180; return new(layout.Center.X + Math.Cos(radians) * layout.Radius * radius, layout.Center.Y + Math.Sin(radians) * layout.Radius * radius); }
        static Color ColorsFor(int index) => Color.Parse(index == 0 ? "#4385F4" : index == 1 ? "#19A798" : "#F2A33A");
        static int Ink(ShapeRaster raster, Point position, Color color) => raster.Count(TemplatePixelRect(new Rect(position.X - 3, position.Y - 3, 6, 6)), color, 24);
        static bool Near(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) <= 1e-9;
    }

    private static ShapeRaster CircularCapture(Control control, string image)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)), new Vector(96, 96)); bitmap.Render(control); bitmap.Save(image); return ReadShapePixels(bitmap);
    }
}
