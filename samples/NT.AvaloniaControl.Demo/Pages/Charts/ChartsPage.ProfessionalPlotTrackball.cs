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
    private ProfessionalPlotTrackballBoard? _professionalPlotTrackballBoard;
    private Control CreateProfessionalPlotTrackballExamples()
    {
        _professionalPlotTrackballBoard = CreateProfessionalPlotTrackballBoard();
        return new Expander { Name = "ChartProfessionalPlotTrackballExamples", Header = "专业图族绘图区和 Trackball：永久业务内容、临时聚合读数", HorizontalAlignment = HorizontalAlignment.Stretch, Content = _professionalPlotTrackballBoard.View };
    }

    private ProfessionalPlotTrackballBoard CreateProfessionalPlotTrackballBoard()
    {
        var model = new ProfessionalTooltipModel { Title = "专业业务 · 绘图区与原始读数" };
        var funnel = new FunnelChart { Name = "ProfessionalPlotTrackballFunnel", CategoryPath = "Label", ValuePath = "Value", IdPath = "Id" };
        var mosaic = new MosaicChart { Name = "ProfessionalPlotTrackballMosaic", LabelPath = "Label" };
        var strip = new StripPlot { Name = "ProfessionalPlotTrackballStrip", CategoryPath = "Row", ValuePath = "Value", IdPath = "Id", LabelPath = "Label", Jitter = 0 };
        var map = new SeatMap { Name = "ProfessionalPlotTrackballMap", ShowLegend = false, ShowLabels = true,
            FeaturesSource = Enumerable.Range(0, 4).Select(i => new MapFeature(new[] { "a", "b", "c", "d" }[i],
                [new MapPolygon([new(i % 2, i / 2), new(i % 2 + .9, i / 2), new(i % 2 + .9, i / 2 + .9), new(i % 2, i / 2 + .9), new(i % 2, i / 2)])],
                new[] { "A 区", "B 区", "C 区", "D 区" }[i])).ToArray() };
        var layer = new ShapeLayer { RegionPath = "Id", ValuePath = "Value" }; map.Layers.Add(layer);
        ChartBase[] charts = [funnel, mosaic, strip, map]; var bindings = new List<IDisposable>();
        var plotBuilds = charts.ToDictionary(chart => chart, _ => 0); var trackBuilds = charts.ToDictionary(chart => chart, _ => 0);
        foreach (var chart in charts)
        {
            chart.DataContext = model; chart.Height = 300; SetProfessionalTooltipEnabled(chart, false);
            if (chart is EngineeringChartBase engineering && chart != map) bindings.Add(engineering.Bind(EngineeringChartBase.ItemsSourceProperty, new Binding(nameof(ProfessionalTooltipModel.Rows))));
            else if (chart is AnalysisChartBase analysis) bindings.Add(analysis.Bind(AnalysisChartBase.ItemsSourceProperty, new Binding(nameof(ProfessionalTooltipModel.Rows))));
        }
        bindings.Add(layer.Bind(MapLayer.ItemsSourceProperty, new Binding(nameof(ProfessionalTooltipModel.Rows))));
        var plots = charts.ToDictionary(chart => chart, chart => (IDataTemplate)new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) =>
        {
            plotBuilds[chart]++;
            var marker = new ProfessionalPlotProjectionMarker();
            marker.Bind(ProfessionalPlotProjectionMarker.MarkerBrushProperty, new Binding(nameof(Foreground)) { Source = chart });
            var content = new Grid { Name = "ProfessionalPlotContent" };
            content.Children.Add(marker);
            content.Children.Add(ProfessionalPlotTrackBody(chart, false));
            return content;
        }));
        var tracks = charts.ToDictionary(chart => chart, chart => (IDataTemplate)new FuncDataTemplate<ChartDataTrackballContext>((_, _) =>
        { trackBuilds[chart]++; return ProfessionalPlotTrackBody(chart, true); }));
        var plotOption = new CheckBox { Content = "永久绘图区模板", IsChecked = true, Margin = new Thickness(0, 0, 12, 4) };
        var trackOption = new CheckBox { Content = "启用 Trackball", IsChecked = true, Margin = new Thickness(0, 0, 12, 4) };
        var templateOption = new CheckBox { Content = "业务读数模板", IsChecked = true, Margin = new Thickness(0, 0, 12, 4) };
        var floatOption = new CheckBox { Content = "逐读数浮动", Margin = new Thickness(0, 0, 12, 4) };
        var horizontalOption = new CheckBox { Content = "水平切片", Margin = new Thickness(0, 0, 12, 4) };
        var capped = new CheckBox { Content = "最多 2 项读数", Margin = new Thickness(0, 0, 12, 4) };
        void Apply()
        {
            foreach (var chart in charts)
            {
                SetProfessionalPlot(chart, plotOption.IsChecked == true ? "永久绘图区 · 归一化中心" : null, plotOption.IsChecked == true ? plots[chart] : null);
                SetProfessionalTrackball(chart, trackOption.IsChecked == true ? horizontalOption.IsChecked == true ? ChartTrackballMode.Horizontal : ChartTrackballMode.Vertical : ChartTrackballMode.None,
                    floatOption.IsChecked == true ? ChartTrackballDisplayMode.FloatAllPoints : ChartTrackballDisplayMode.GroupAllPoints,
                    templateOption.IsChecked == true ? tracks[chart] : null, capped.IsChecked == true ? 2 : 4);
            }
        }
        foreach (var option in new[] { plotOption, trackOption, templateOption, floatOption, horizontalOption, capped }) option.IsCheckedChanged += (_, _) => Apply();
        Apply();
        var contrast = new ComboBox { ItemsSource = Enum.GetValues<ChartHighContrastMode>(), SelectedItem = ChartHighContrastMode.None, MinWidth = 100, Margin = new Thickness(0, 0, 12, 4) };
        contrast.SelectionChanged += (_, _) => { if (contrast.SelectedItem is ChartHighContrastMode mode) foreach (var chart in charts) chart.HighContrastMode = mode; };
        var group = new Grid { ColumnDefinitions = new("*,*"), RowDefinitions = new("Auto,Auto"), ColumnSpacing = 20, RowSpacing = 12 };
        string[] titles = ["Funnel · 阶段布局与原始观测", "Mosaic · 切片保留聚合成员 [0, 2]", "Strip · 真实点与视图变换", "Map · 区域切片与业务来源"];
        for (var i = 0; i < charts.Length; i++)
        {
            var card = new StackPanel { Spacing = 5, Children = { new TextBlock { Text = titles[i], FontWeight = FontWeight.SemiBold }, charts[i] } };
            Grid.SetColumn(card, i % 2); Grid.SetRow(card, i / 2); group.Children.Add(card);
        }
        var options = new WrapPanel { Children = { plotOption, trackOption, templateOption, floatOption, horizontalOption, capped, contrast } }; var revision = 0;
        void Mutate() { model.Rows[0].Value += 5; }
        void Replace() { model.Rows = ProfessionalTooltipRows(++revision * 3); }
        void Header() { model.Title = $"绘图区业务 · 修订 {++revision}"; }
        void Reset() { foreach (var chart in charts) chart.ResetViewportCommand.Execute(null); }
        AddAction(options, "绘图区 更新数值", Mutate, "保留真实原始成员；数据通知使永久投影和临时读数更新。");
        AddAction(options, "绘图区 替换来源", Replace, "相同业务 ID 的新对象替换来源，并释放旧上下文。");
        AddAction(options, "绘图区 更新标题", Header, "图内内容通过绘图区上下文绑定原始业务 VM。");
        AddAction(options, "绘图区 缩放平移", () => { foreach (var chart in charts.OfType<EngineeringChartBase>()) { chart.Zoom = chart.Zoom == 1 ? 1.25 : 1; chart.PanX = chart.Zoom == 1 ? 0 : .05; } }, "十字标记使用家族布局的归一化投影，跟随真实视口。");
        AddAction(options, "绘图区 恢复视图", Reset, "恢复视口并清除临时 Trackball。");
        AddAction(options, "绘图区 隐藏图组", () => group.IsVisible = !group.IsVisible, "隐藏时释放模板上下文；再次显示恢复永久内容。");
        var view = new StackPanel { Name = "ProfessionalPlotTrackballBoard", DataContext = model, Spacing = 10, Children =
        {
            new TextBlock { Text = "专业图族 · 绘图区内容与 Trackball", FontSize = 19, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "移动指针读取家族布局的垂直或水平切片；聚合读数保留全部原始成员。虚线十字是布局归一化中心，并非伪造的数据坐标。永久绘图区进入 PNG / JPEG，临时 Trackball 从导出中排除。", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = .8 }, options, group,
        } };
        var singleColumn = false;
        view.SizeChanged += (_, change) =>
        {
            var compact = change.NewSize.Width < 1120; if (singleColumn == compact) return; singleColumn = compact;
            group.ColumnDefinitions = new(compact ? "*" : "*,*"); group.RowDefinitions = new(compact ? "Auto,Auto,Auto,Auto" : "Auto,Auto");
            for (var i = 0; i < group.Children.Count; i++) { Grid.SetColumn(group.Children[i], compact ? 0 : i % 2); Grid.SetRow(group.Children[i], compact ? i : i / 2); }
        };
        return new(view, group, charts, model, funnel, mosaic, strip, map, plots, tracks, plotBuilds, trackBuilds, bindings, plotOption, trackOption, templateOption, floatOption, horizontalOption, capped, contrast, Mutate, Replace, Header, Reset);
    }

    private static Border ProfessionalPlotTrackBody(ChartBase chart, bool trackball)
    {
        var prefix = trackball ? "ProfessionalTrackball" : "ProfessionalPlot";
        TextBlock Read(string suffix, string path, string? format = null)
        {
            var text = new TextBlock { Name = prefix + suffix, FontSize = 10, LineHeight = 12, MaxWidth = trackball ? 235 : 270, TextTrimming = TextTrimming.CharacterEllipsis };
            var binding = new Binding(path); if (format is not null) binding.StringFormat = format; text.Bind(TextBlock.TextProperty, binding);
            text.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = chart }); return text;
        }
        var title = Read("Title", trackball ? "BusinessDataContext.Title" : "DataContext.Title"); title.FontWeight = FontWeight.SemiBold;
        var panel = new StackPanel { Spacing = 1, Children = { title } };
        if (trackball)
        {
            var count = Read("Count", "Readings.Count");
            count.Bind(TextBlock.TextProperty, new MultiBinding { StringFormat = "切片读数 {0} / {1}", Bindings = { new Binding("Readings.Count"), new Binding("TotalReadings") } });
            panel.Children.Add(count);
            panel.Children.Add(Read("Members", "Readings[0].Members.Count", "首读数原始成员 {0}"));
            panel.Children.Add(Read("Id", "Readings[0].Members[0].StableId", "首项 ID {0}"));
        }
        else { panel.Children.Add(Read("Caption", "Content")); panel.Children.Add(Read("Extent", "VisibleLayoutBounds", "可见布局 {0}")); }
        var body = new Border { Name = prefix + "Body", Padding = new Thickness(5, 3), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Child = panel,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = trackball ? default : new Thickness(5) };
        body.Bind(Border.BorderBrushProperty, new Binding(nameof(Foreground)) { Source = chart });
        if (chart is EngineeringChartBase) body.Bind(Border.BackgroundProperty, new Binding(nameof(EngineeringChartBase.TooltipBackground)) { Source = chart });
        else body.Bind(Border.BackgroundProperty, new Binding(nameof(AnalysisChartBase.TooltipBackground)) { Source = chart });
        return body;
    }
    private static void SetProfessionalPlot(ChartBase chart, object? value, IDataTemplate? template)
    { if (chart is EngineeringChartBase e) { e.PlotAreaContent = value; e.PlotAreaContentTemplate = template; } else { var a = (AnalysisChartBase)chart; a.PlotAreaContent = value; a.PlotAreaContentTemplate = template; } }
    private static void SetProfessionalTrackball(ChartBase chart, ChartTrackballMode mode, ChartTrackballDisplayMode display, IDataTemplate? template, int max)
    {
        if (chart is EngineeringChartBase e) { e.TrackballMode = mode; e.TrackballDisplayMode = display; e.TrackballContentTemplate = template; e.MaxTrackballReadings = max; }
        else { var a = (AnalysisChartBase)chart; a.TrackballMode = mode; a.TrackballDisplayMode = display; a.TrackballContentTemplate = template; a.MaxTrackballReadings = max; }
    }
    private static IReadOnlyList<ChartDataTrackballReading> ProfessionalTracks(ChartBase chart) => chart is EngineeringChartBase e ? e.TrackballReadings : ((AnalysisChartBase)chart).TrackballReadings;
    private static void UpdateProfessionalTrackball(ChartBase chart, Point point) { if (chart is EngineeringChartBase e) e.UpdateTrackball(point); else ((AnalysisChartBase)chart).UpdateTrackball(point); }
    private static void ClearProfessionalTrackball(ChartBase chart) { if (chart is EngineeringChartBase e) e.ClearTrackball(); else ((AnalysisChartBase)chart).ClearTrackball(); }
    private sealed class ProfessionalPlotProjectionMarker : Control
    {
        public static readonly StyledProperty<IBrush?> MarkerBrushProperty = AvaloniaProperty.Register<ProfessionalPlotProjectionMarker, IBrush?>(nameof(MarkerBrush));
        static ProfessionalPlotProjectionMarker() => AffectsRender<ProfessionalPlotProjectionMarker>(MarkerBrushProperty);
        public IBrush? MarkerBrush { get => GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }
        public override void Render(DrawingContext context)
        {
            base.Render(context); if (DataContext is not ChartDataPlotAreaContext plot) return;
            var center = plot.ProjectLayout(.5, .5); var pen = new Pen(MarkerBrush, 1, DashStyle.Dash);
            context.DrawLine(pen, new Point(center.X - 16, center.Y), new Point(center.X + 16, center.Y));
            context.DrawLine(pen, new Point(center.X, center.Y - 16), new Point(center.X, center.Y + 16));
        }
        protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); InvalidateVisual(); }
    }
    private sealed record ProfessionalPlotTrackballBoard(StackPanel View, Grid Group, ChartBase[] Charts, ProfessionalTooltipModel Model,
        FunnelChart Funnel, MosaicChart Mosaic, StripPlot Strip, SeatMap Map, Dictionary<ChartBase, IDataTemplate> Plots, Dictionary<ChartBase, IDataTemplate> Tracks,
        Dictionary<ChartBase, int> PlotBuilds, Dictionary<ChartBase, int> TrackBuilds, List<IDisposable> Bindings,
        CheckBox PlotOption, CheckBox TrackOption, CheckBox TemplateOption, CheckBox FloatOption, CheckBox HorizontalOption, CheckBox CappedOption, ComboBox Contrast,
        Action Mutate, Action Replace, Action Header, Action Reset) : IDisposable
    {
        public void Dispose()
        {
            foreach (var chart in Charts) { ClearProfessionalTrackball(chart); SetProfessionalTrackball(chart, ChartTrackballMode.None, ChartTrackballDisplayMode.GroupAllPoints, null, 4); SetProfessionalPlot(chart, null, null); }
            foreach (var binding in Bindings) binding.Dispose(); Funnel.ItemsSource = null; Mosaic.ItemsSource = null; Strip.ItemsSource = null; Map.Dispose();
        }
    }
}
