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
    private ProfessionalPresentationBoard? _professionalPresentationBoard;

    private Control CreateProfessionalPresentationExamples()
    {
        _professionalPresentationBoard = CreateProfessionalPresentationBoard();
        return new Expander { Name = "ChartProfessionalPresentationExamples", Header = "专业图族共享图例和标签：业务模板、原始成员和导出", HorizontalAlignment = HorizontalAlignment.Stretch, Content = _professionalPresentationBoard.View };
    }

    private ProfessionalPresentationBoard CreateProfessionalPresentationBoard()
    {
        var model = new ProfessionalTooltipModel { Title = "业务呈现 · 同一份原始来源" };
        var funnel = new FunnelChart { Name = "ProfessionalPresentationFunnel", CategoryPath = "Label", ValuePath = "Value", IdPath = "Id" };
        var mosaic = new MosaicChart { Name = "ProfessionalPresentationMosaic", LabelPath = "Label" };
        var strip = new StripPlot { Name = "ProfessionalPresentationStrip", CategoryPath = "Row", ValuePath = "Value", IdPath = "Id", LabelPath = "Label", Jitter = 0 };
        var map = new SeatMap
        {
            Name = "ProfessionalPresentationMap", ShowLabels = true, ShowLegend = true,
            FeaturesSource = Enumerable.Range(0, 4).Select(i => new MapFeature(new[] { "a", "b", "c", "d" }[i],
                [new MapPolygon([new(i % 2, i / 2), new(i % 2 + .9, i / 2), new(i % 2 + .9, i / 2 + .9), new(i % 2, i / 2 + .9), new(i % 2, i / 2)])],
                new[] { "A 区", "B 区", "C 区", "D 区" }[i])).ToArray(),
        };
        var layer = new ShapeLayer { RegionPath = "Id", ValuePath = "Value" }; map.Layers.Add(layer);
        ChartBase[] charts = [funnel, mosaic, strip, map];
        var bindings = new List<IDisposable>();
        var labelBuilds = charts.ToDictionary(chart => chart, _ => 0);
        var legendBuilds = charts.ToDictionary(chart => chart, _ => 0);
        foreach (var chart in charts)
        {
            chart.DataContext = model; chart.Height = 320; SetProfessionalTooltipEnabled(chart, false);
            if (chart is EngineeringChartBase engineering && chart != map) bindings.Add(engineering.Bind(EngineeringChartBase.ItemsSourceProperty, new Binding(nameof(ProfessionalTooltipModel.Rows))));
            else if (chart is AnalysisChartBase analysis) bindings.Add(analysis.Bind(AnalysisChartBase.ItemsSourceProperty, new Binding(nameof(ProfessionalTooltipModel.Rows))));
            SetPresentationLimits(chart, 4, 4);
        }
        bindings.Add(layer.Bind(MapLayer.ItemsSourceProperty, new Binding(nameof(ProfessionalTooltipModel.Rows))));
        var labels = charts.ToDictionary(chart => chart, chart => (IDataTemplate)new FuncDataTemplate<ChartDataLabelItemContext>((_, _) =>
        {
            labelBuilds[chart]++;
            return PresentationTemplateBody(chart, false);
        }));
        var legends = charts.ToDictionary(chart => chart, chart => (IDataTemplate)new FuncDataTemplate<ChartDataLegendItemContext>((_, _) =>
        {
            legendBuilds[chart]++;
            return PresentationTemplateBody(chart, true);
        }));
        foreach (var chart in charts) SetProfessionalPresentationTemplates(chart, legends[chart], labels[chart]);
        var legendOption = new CheckBox { Content = "共享图例模板", IsChecked = true, Margin = new Thickness(0, 0, 12, 4) };
        var labelOption = new CheckBox { Content = "共享数据标签模板", IsChecked = true, Margin = new Thickness(0, 0, 12, 4) };
        void ApplyTemplates() { foreach (var chart in charts) SetProfessionalPresentationTemplates(chart, legendOption.IsChecked == true ? legends[chart] : null, labelOption.IsChecked == true ? labels[chart] : null); }
        legendOption.IsCheckedChanged += (_, _) => ApplyTemplates(); labelOption.IsCheckedChanged += (_, _) => ApplyTemplates();
        var contrast = new ComboBox { ItemsSource = Enum.GetValues<ChartHighContrastMode>(), SelectedItem = ChartHighContrastMode.None, MinWidth = 100, Margin = new Thickness(0, 0, 12, 4) };
        contrast.SelectionChanged += (_, _) => { if (contrast.SelectedItem is ChartHighContrastMode mode) foreach (var chart in charts) chart.HighContrastMode = mode; };
        var capped = new CheckBox { Content = "每图最多 2 项", Margin = new Thickness(0, 0, 12, 4) };
        capped.IsCheckedChanged += (_, _) => { foreach (var chart in charts) SetPresentationLimits(chart, capped.IsChecked == true ? 2 : 4, capped.IsChecked == true ? 2 : 4); };
        var group = new Grid { ColumnDefinitions = new("*,*"), RowDefinitions = new("Auto,Auto"), ColumnSpacing = 20, RowSpacing = 12 };
        string[] titles = ["Funnel · 原始阶段与永久标签", "Mosaic · 聚合保留全部成员 [0, 2]", "Strip · 逐点身份与显示预算", "Map · 区域标签与颜色标尺"];
        for (var i = 0; i < charts.Length; i++)
        {
            var card = new StackPanel { Spacing = 5, Children = { new TextBlock { Text = titles[i], FontWeight = FontWeight.SemiBold }, charts[i] } };
            Grid.SetColumn(card, i % 2); Grid.SetRow(card, i / 2); group.Children.Add(card);
        }
        var options = new WrapPanel { Children = { legendOption, labelOption, capped, contrast } };
        var revision = 0;
        void Mutate() { model.Rows[0].Value += 5; }
        void Replace() { model.Rows = ProfessionalTooltipRows(++revision * 3); }
        void Header() { model.Title = $"业务呈现 · 修订 {++revision}"; }
        void Reset() { foreach (var chart in charts) chart.ResetViewportCommand.Execute(null); }
        AddAction(options, "呈现 更新数值", Mutate, "数据标签及图例上下文跟随原始 DTO 的属性通知。");
        AddAction(options, "呈现 替换来源", Replace, "相同业务 ID 的新对象替换旧来源，释放旧绑定。");
        AddAction(options, "呈现 更新标题", Header, "共享上下文提供真实 BusinessDataContext，标题绑定可实时更新。");
        AddAction(options, "呈现 缩放平移", () => { foreach (var chart in charts.OfType<EngineeringChartBase>()) { chart.Zoom = chart.Zoom == 1 ? 1.2 : 1; chart.PanX = chart.Zoom == 1 ? 0 : .025; } }, "专业投影的标签锚点随缩放和平移重算。");
        AddAction(options, "呈现 恢复视图", Reset, "重置视图后，永久图例及标签保持显示。");
        AddAction(options, "呈现 隐藏图组", () => group.IsVisible = !group.IsVisible, "隐藏祖先时释放模板子树，显示后重建当前来源。");
        var view = new StackPanel { Name = "ProfessionalPresentationBoard", Spacing = 10, DataContext = model, Children =
        {
            new TextBlock { Text = "专业图族 · 共用图例和数据标签模板", FontSize = 19, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "图例保留系列、分类和颜色标尺的语义；标签保留每个图元的原始成员。模板只呈现成员计数和首项身份，永久内容同时进入 PNG / JPEG 导出。", FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            options, group,
        } };
        var singleColumn = false;
        view.SizeChanged += (_, change) =>
        {
            var compact = change.NewSize.Width < 1120;
            if (singleColumn == compact) return;
            singleColumn = compact;
            group.ColumnDefinitions = new(compact ? "*" : "*,*"); group.RowDefinitions = new(compact ? "Auto,Auto,Auto,Auto" : "Auto,Auto");
            for (var index = 0; index < group.Children.Count; index++)
            { Grid.SetColumn(group.Children[index], compact ? 0 : index % 2); Grid.SetRow(group.Children[index], compact ? index : index / 2); }
        };
        return new(view, group, charts, model, funnel, mosaic, strip, map, legends, labels, legendBuilds, labelBuilds, bindings, legendOption, labelOption, capped, contrast, Mutate, Replace, Header, Reset);
    }

    private static Control PresentationTemplateBody(ChartBase chart, bool legend)
    {
        TextBlock Read(string name, string path, string? format = null)
        {
            var text = new TextBlock { Name = name, FontSize = 10, LineHeight = 12, MaxWidth = legend ? 148 : 126, TextTrimming = TextTrimming.CharacterEllipsis };
            var binding = new Binding(path); if (format is not null) binding.StringFormat = format;
            text.Bind(TextBlock.TextProperty, binding); text.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = chart });
            return text;
        }
        var prefix = legend ? "ProfessionalLegend" : "ProfessionalLabel";
        var title = Read(prefix + "Text", "Text"); title.FontWeight = FontWeight.SemiBold;
        var members = Read(prefix + "Members", "Members.Count", "原始成员 {0}");
        var panel = new StackPanel { Spacing = 1, Children = { title, members } };
        if (legend)
        {
            members.Bind(TextBlock.TextProperty, new MultiBinding { StringFormat = "成员 {0} · 首项 {1}", Bindings = { new Binding("Members.Count"), new Binding("Members[0].StableId") } });
            panel.Children.Add(Read(prefix + "BusinessTitle", "BusinessDataContext.Title"));
        }
        else panel.Children.Add(Read(prefix + "Id", "Members[0].StableId", "首项 {0}"));
        Control content = panel;
        if (legend)
        {
            var swatch = new Border { Name = "ProfessionalLegendSwatch", Height = 5, MinWidth = 68, HorizontalAlignment = HorizontalAlignment.Stretch };
            swatch.Bind(Border.BackgroundProperty, new Binding("Brush"));
            content = new StackPanel { Spacing = 2, Children = { swatch, panel } };
        }
        var body = new Border { Name = prefix + "Content", Padding = new Thickness(4, 2), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Child = content };
        body.Bind(Border.BorderBrushProperty, new Binding(nameof(Foreground)) { Source = chart });
        if (chart is EngineeringChartBase) body.Bind(Border.BackgroundProperty, new Binding(nameof(EngineeringChartBase.TooltipBackground)) { Source = chart });
        else body.Bind(Border.BackgroundProperty, new Binding(nameof(AnalysisChartBase.TooltipBackground)) { Source = chart });
        return body;
    }
    private static void SetProfessionalPresentationTemplates(ChartBase chart, IDataTemplate? legend, IDataTemplate? label)
    {
        if (chart is EngineeringChartBase engineering) { engineering.LegendItemTemplate = legend; engineering.DataLabelTemplate = label; }
        else { var analysis = (AnalysisChartBase)chart; analysis.LegendItemTemplate = legend; analysis.DataLabelTemplate = label; }
    }
    private static void SetPresentationLimits(ChartBase chart, int legend, int label)
    {
        if (chart is EngineeringChartBase engineering) { engineering.MaxLegendItems = legend; engineering.MaxDataLabels = label; }
        else { var analysis = (AnalysisChartBase)chart; analysis.MaxLegendItems = legend; analysis.MaxDataLabels = label; }
    }
    private static IReadOnlyList<ChartDataLegendItemContext> PresentationLegendItems(ChartBase chart) => chart is EngineeringChartBase engineering ? engineering.GetLegendItems() : ((AnalysisChartBase)chart).GetLegendItems();
    private static IReadOnlyList<ChartDataLabelItemContext> PresentationLabelItems(ChartBase chart) => chart is EngineeringChartBase engineering ? engineering.GetDataLabels() : ((AnalysisChartBase)chart).GetDataLabels();

    private sealed record ProfessionalPresentationBoard(StackPanel View, Grid Group, ChartBase[] Charts, ProfessionalTooltipModel Model,
        FunnelChart Funnel, MosaicChart Mosaic, StripPlot Strip, SeatMap Map, Dictionary<ChartBase, IDataTemplate> Legends, Dictionary<ChartBase, IDataTemplate> Labels,
        Dictionary<ChartBase, int> LegendBuilds, Dictionary<ChartBase, int> LabelBuilds, List<IDisposable> Bindings,
        CheckBox LegendOption, CheckBox LabelOption, CheckBox CappedOption, ComboBox Contrast, Action Mutate, Action Replace, Action Header, Action Reset) : IDisposable
    {
        public void Dispose()
        {
            foreach (var chart in Charts) SetProfessionalPresentationTemplates(chart, null, null);
            foreach (var binding in Bindings) binding.Dispose();
            Funnel.ItemsSource = null; Mosaic.ItemsSource = null; Strip.ItemsSource = null; Map.Dispose();
        }
    }
}
