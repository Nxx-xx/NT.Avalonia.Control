using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private ProfessionalTooltipBoard? _professionalTooltipBoard;

    private Control CreateProfessionalTooltipExamples()
    {
        _professionalTooltipBoard = CreateProfessionalTooltipBoard();
        return new Expander { Name = "ChartProfessionalTooltipExamples", Header = "专业图族提示模板：业务上下文、聚合来源和生命周期", HorizontalAlignment = HorizontalAlignment.Stretch, Content = _professionalTooltipBoard.View };
    }

    private static ObservableCollection<ProfessionalTooltipRow> ProfessionalTooltipRows(int revision = 0) =>
    [
        new("a", "阶段 A", "产品 A", "线上", 30 + revision), new("b", "阶段 B", "产品 A", "门店", 50 + revision),
        new("c", "阶段 C", "产品 A", "线上", 20 + revision), new("d", "阶段 D", "产品 B", "门店", 40 + revision),
    ];

    private ProfessionalTooltipBoard CreateProfessionalTooltipBoard()
    {
        var model = new ProfessionalTooltipModel();
        var funnel = new FunnelChart { Name = "ProfessionalTooltipFunnel", CategoryPath = "Label", ValuePath = "Value", IdPath = "Id" };
        var mosaic = new MosaicChart { Name = "ProfessionalTooltipMosaic", LabelPath = "Label" };
        var strip = new StripPlot { Name = "ProfessionalTooltipStrip", CategoryPath = "Row", ValuePath = "Value", IdPath = "Id", LabelPath = "Label", Jitter = 0 };
        var map = new SeatMap { Name = "ProfessionalTooltipMap", ShowLegend = false, FeaturesSource = Enumerable.Range(0, 4).Select(i =>
            new MapFeature(new[] { "a", "b", "c", "d" }[i], [new MapPolygon([new(i % 2, i / 2), new(i % 2 + .9, i / 2), new(i % 2 + .9, i / 2 + .9), new(i % 2, i / 2 + .9), new(i % 2, i / 2)])], new[] { "A 区", "B 区", "C 区", "D 区" }[i])).ToArray() };
        var layer = new ShapeLayer { RegionPath = "Id", ValuePath = "Value" }; map.Layers.Add(layer);
        ChartBase[] charts = [funnel, mosaic, strip, map];
        var bindings = new List<IDisposable>();
        foreach (var chart in charts)
        {
            chart.DataContext = model; chart.Height = 270;
            if (chart is EngineeringChartBase engineering && chart != map) bindings.Add(engineering.Bind(EngineeringChartBase.ItemsSourceProperty, new Binding(nameof(ProfessionalTooltipModel.Rows))));
            else if (chart is AnalysisChartBase analysis) bindings.Add(analysis.Bind(AnalysisChartBase.ItemsSourceProperty, new Binding(nameof(ProfessionalTooltipModel.Rows))));
        }
        bindings.Add(layer.Bind(MapLayer.ItemsSourceProperty, new Binding(nameof(ProfessionalTooltipModel.Rows))));
        var builds = charts.ToDictionary(chart => chart, _ => 0);
        var templates = charts.ToDictionary(chart => chart, chart => (IDataTemplate)new FuncDataTemplate<ChartDataTooltipContext>((_, _) =>
        {
            builds[chart]++;
            var badge = new TextBlock { Name = "ProfessionalTooltipBadge", FontWeight = FontWeight.SemiBold };
            badge[!TextBlock.TextProperty] = new DynamicResourceExtension("ProfessionalTooltipBadgeText");
            TextBlock Read(string name, string path, string? format = null)
            {
                var text = new TextBlock { Name = name, TextWrapping = TextWrapping.Wrap };
                var binding = new Binding(path); if (format is not null) binding.StringFormat = format;
                text.Bind(TextBlock.TextProperty, binding); return text;
            }
            return new StackPanel { Name = "ProfessionalTooltipContent", Spacing = 4, MaxWidth = 300, Children =
            {
                badge, Read("ProfessionalTooltipBusinessTitle", "BusinessDataContext.Title"),
                Read("ProfessionalTooltipCount", "Members.Count", "原始成员 {0}"),
                Read("ProfessionalTooltipId", "Members[0].StableId", "首项 ID {0}"),
                Read("ProfessionalTooltipIndex", "Members[0].SourceIndex", "首项源序号 {0}"),
                Read("ProfessionalTooltipText", "Text"),
            } };
        }));
        foreach (var chart in charts) SetProfessionalTemplate(chart, templates[chart]);
        var templateOption = new CheckBox { Content = "业务提示模板", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        templateOption.IsCheckedChanged += (_, _) => { foreach (var chart in charts) SetProfessionalTemplate(chart, templateOption.IsChecked == true ? templates[chart] : null); };
        var tooltipOption = new CheckBox { Content = "启用提示", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        tooltipOption.IsCheckedChanged += (_, _) => { foreach (var chart in charts) SetProfessionalTooltipEnabled(chart, tooltipOption.IsChecked == true); };
        var contrast = new ComboBox { ItemsSource = Enum.GetValues<ChartHighContrastMode>(), SelectedItem = ChartHighContrastMode.None, MinWidth = 100, Margin = new Thickness(0, 0, 12, 6) };
        contrast.SelectionChanged += (_, _) => { if (contrast.SelectedItem is ChartHighContrastMode mode) foreach (var chart in charts) chart.HighContrastMode = mode; };
        var group = new Grid { ColumnDefinitions = new("*,*"), RowDefinitions = new("Auto,Auto"), ColumnSpacing = 20, RowSpacing = 12 };
        string[] titles = ["Funnel · 单个原始观测", "Mosaic · 重复分类合并 [0, 2]", "Strip · 每个原始点", "Map · 真实区域业务项"];
        for (var i = 0; i < charts.Length; i++)
        {
            var card = new StackPanel { Spacing = 5, Children = { new TextBlock { Text = titles[i], FontWeight = FontWeight.SemiBold }, charts[i] } };
            Grid.SetColumn(card, i % 2); Grid.SetRow(card, i / 2); group.Children.Add(card);
        }
        var enabled = new CheckBox { Content = "图组可用", IsChecked = true, Margin = new Thickness(0, 0, 12, 6) };
        enabled.IsCheckedChanged += (_, _) => group.IsEnabled = enabled.IsChecked == true;
        var options = new WrapPanel { Children = { templateOption, tooltipOption, enabled, contrast } };
        var revision = 0;
        void Mutate() { model.Rows[0].Value += 5; }
        void Replace() { revision++; model.Rows = ProfessionalTooltipRows(revision * 3); }
        void Header() { model.Title = $"业务看板 · 修订 {++revision}"; }
        void Reset() { foreach (var chart in charts) chart.ResetViewportCommand.Execute(null); }
        AddAction(options, "提示 更新数值", Mutate, "INPC 更新原始 DTO，旧读数上下文立即释放。");
        AddAction(options, "提示 替换数据源", Replace, "使用相同业务 ID 的新 DTO 和集合，保留来源身份语义。");
        AddAction(options, "提示 更新业务标题", Header, "更新 BusinessDataContext.Title，不创建成员列表控件。");
        AddAction(options, "提示 恢复视图", Reset, "清空活动读数和选择，卸载提示子树。");
        AddAction(options, "提示 隐藏 / 显示图组", () => group.IsVisible = !group.IsVisible, "隐藏祖先容器时释放活动提示，再显示可继续读取。");
        var view = new StackPanel { Name = "ProfessionalTooltipBoard", Spacing = 10, DataContext = model, Children =
        {
            new TextBlock { Text = "专业图族 · 共用业务提示模板", FontSize = 19, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "悬停或方向键读取；模板只显示成员计数和首项身份。聚合保留全部原始记录，提示是被动控件并从栅格导出中排除。", FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = .8 },
            options, group,
        } };
        view.Resources["ProfessionalTooltipBadgeText"] = "业务读数 · 原始来源";
        return new(view, group, charts, model, funnel, mosaic, strip, map, templates, builds, bindings, templateOption, tooltipOption, enabled, contrast, Mutate, Replace, Header, Reset);
    }

    private static void SetProfessionalTemplate(ChartBase chart, IDataTemplate? value) { if (chart is EngineeringChartBase e) e.TooltipContentTemplate = value; else ((AnalysisChartBase)chart).TooltipContentTemplate = value; }
    private static void SetProfessionalTooltipEnabled(ChartBase chart, bool value) { if (chart is EngineeringChartBase e) e.IsTooltipEnabled = value; else ((AnalysisChartBase)chart).IsTooltipEnabled = value; }
    private sealed class ProfessionalTooltipModel : INotifyPropertyChanged
    {
        private string _title = "销售业务看板"; private ObservableCollection<ProfessionalTooltipRow> _rows = ProfessionalTooltipRows();
        public string Title { get => _title; set { _title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); } }
        public ObservableCollection<ProfessionalTooltipRow> Rows { get => _rows; set { _rows = value; PropertyChanged?.Invoke(this, new(nameof(Rows))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class ProfessionalTooltipRow(string id, string label, string column, string row, double value) : INotifyPropertyChanged
    {
        private double _value = value; private PropertyChangedEventHandler? _changed;
        public string Id { get; } = id; public string Label { get; } = label; public string Column { get; } = column; public string Row { get; } = row;
        public int Subscribers { get; private set; }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Subscribers++; _changed += value; } remove { Subscribers--; _changed -= value; } }
    }
    private sealed record ProfessionalTooltipBoard(StackPanel View, Grid Group, ChartBase[] Charts, ProfessionalTooltipModel Model,
        FunnelChart Funnel, MosaicChart Mosaic, StripPlot Strip, SeatMap Map, Dictionary<ChartBase, IDataTemplate> Templates, Dictionary<ChartBase, int> Builds,
        List<IDisposable> Bindings, CheckBox TemplateOption, CheckBox TooltipOption, CheckBox EnabledOption, ComboBox Contrast,
        Action Mutate, Action Replace, Action Header, Action Reset) : IDisposable
    {
        public void Dispose()
        {
            foreach (var chart in Charts) { SetProfessionalTooltipEnabled(chart, false); SetProfessionalTemplate(chart, null); }
            foreach (var binding in Bindings) binding.Dispose();
            Funnel.ItemsSource = null; Mosaic.ItemsSource = null; Strip.ItemsSource = null; Map.Dispose();
        }
    }
}
