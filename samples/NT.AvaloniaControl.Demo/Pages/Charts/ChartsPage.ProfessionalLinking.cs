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
    private ProfessionalLinkingBoard? _professionalLinkingBoard;
    private Control CreateProfessionalLinkingExamples()
    {
        _professionalLinkingBoard = CreateProfessionalLinkingBoard();
        return new Expander { Name = "ChartProfessionalLinkingExamples", Header = "专业图族 · 范围选择、业务 ID 联动与完整呈现", HorizontalAlignment = HorizontalAlignment.Stretch, Content = _professionalLinkingBoard.View };
    }
    private ProfessionalLinkingBoard CreateProfessionalLinkingBoard()
    {
        var owner = CreateProfessionalPlotTrackballBoard(); owner.View.Children.Remove(owner.Group);
        owner.Map.ShowLegend = true;
        var charts = owner.Charts; var links = new ChartDataSynchronizationGroup();
        object? BusinessKey(ChartDataPoint point) => (point.Item as ProfessionalTooltipRow)?.Id;
        foreach (var chart in charts)
        {
            chart.Height = 320;
            if (chart is EngineeringChartBase e) links.Add(e, BusinessKey); else links.Add((AnalysisChartBase)chart, BusinessKey);
            SetProfessionalPresentationTemplates(chart, new FuncDataTemplate<ChartDataLegendItemContext>((_, _) => LinkingCompactBody(chart, "LinkLegend", "Text", "Members.Count", "成员 {0}")),
                new FuncDataTemplate<ChartDataLabelItemContext>((_, _) => LinkingCompactBody(chart, "LinkLabel", "Text", "Members[0].StableId", "ID {0}")));
            SetPresentationLimits(chart, 4, 4);
            SetProfessionalTemplate(chart, new FuncDataTemplate<ChartDataTooltipContext>((_, _) => LinkingCompactBody(chart, "LinkTooltip", "Text", "Members.Count", "原始成员 {0}")));
            SetProfessionalTooltipEnabled(chart, true);
        }
        var horizontal = new ChartDataRangeSelector { Name = "ProfessionalLinkingHorizontal", EngineeringChart = owner.Mosaic, Axis = ChartInteractionAxes.X, Height = 36 };
        var vertical = new ChartDataRangeSelector { Name = "ProfessionalLinkingVertical", EngineeringChart = owner.Strip, Axis = ChartInteractionAxes.Y, Height = 60, Width = 36, HorizontalAlignment = HorizontalAlignment.Left };
        var status = new TextBlock { Name = "ProfessionalLinkingSelectionStatus", FontSize = 12, TextWrapping = TextWrapping.Wrap };
        void Status() => status.Text = string.Join(" · ", charts.Select(chart => $"{chart.GetType().Name}: [{string.Join(", ", LinkingData(chart).Select(point => (point.Item as ProfessionalTooltipRow)?.Id))}]"));
        foreach (var chart in charts) { if (chart is EngineeringChartBase e) e.DataSelectionChanged += (_, _) => Status(); else ((AnalysisChartBase)chart).DataSelectionChanged += (_, _) => Status(); }
        var options = new WrapPanel();
        AddAction(options, "联动 选择 A", () => { owner.Funnel.SelectedDataKeys = ["a"]; }, "使用原始业务 ID 在四种不同布局间映射，不同步不相容的坐标。");
        AddAction(options, "联动 选择聚合", () => { var mark = owner.Mosaic.GetMarks().First(mark => mark.Members.Count == 2); owner.Mosaic.SelectedDataKeys = mark.Members.Select(point => point.StableId!).ToArray(); }, "Mosaic 聚合选择保留全部原始成员，其他图只按业务 ID 高亮对应项。");
        AddAction(options, "联动 清除选择", () => { foreach (var chart in charts) { ClearLinkingData(chart); if (chart is EngineeringChartBase e) e.SelectedIndex = -1; else ((AnalysisChartBase)chart).SelectedIndex = -1; } }, "双向清除数据选择和本地范围；不同家族的范围不会相互覆盖。");
        AddAction(options, "联动 更新数值", owner.Mutate, "保持来源对象与业务 ID，永久模板和临时读数一起更新。");
        AddAction(options, "联动 替换来源", () => { owner.Replace(); if (charts[0].DataContext is ProfessionalTooltipModel current && !ReferenceEquals(current, owner.Model)) current.Rows = owner.Model.Rows; }, "相同显式 ID 的新 DTO 替换旧来源；选中项应指向新对象。");
        AddAction(options, "联动 切换 VM", () => { var changed = !ReferenceEquals(owner.Charts[0].DataContext, owner.Model); var next = changed ? owner.Model : new ProfessionalTooltipModel { Rows = owner.Model.Rows, Title = "替换后的业务 VM" }; foreach (var chart in charts) chart.DataContext = next; }, "替换业务 VM，模板使用当前真实上下文。");
        AddAction(options, "联动 缩放 Strip", () => { owner.Strip.Zoom = owner.Strip.Zoom == 1 ? 1.25 : 1; owner.Strip.PanX = owner.Strip.Zoom == 1 ? 0 : .05; }, "只改变 Strip 本地视图；其他布局不接收原始坐标。");
        AddAction(options, "联动 恢复视图", owner.Reset, "重置视口、临时读数、本地范围和业务数据选择。");
        AddAction(options, "联动 隐藏图组", () => owner.Group.IsVisible = !owner.Group.IsVisible, "隐藏时组成员暂停监听；显示后等待新操作或显式同步。");
        var enabled = new CheckBox { Content = "业务选择联动", IsChecked = true, Margin = new Thickness(8, 0) };
        enabled.IsCheckedChanged += (_, _) => links.SynchronizeSelection = enabled.IsChecked == true; foreach (var previousOptions in owner.View.Children.OfType<WrapPanel>()) { previousOptions.Children.Remove(owner.TrackOption); previousOptions.Children.Remove(owner.Contrast); } options.Children.Add(enabled); options.Children.Add(owner.TrackOption); options.Children.Add(owner.Contrast);
        var view = new StackPanel { Name = "ProfessionalLinkingBoard", DataContext = owner.Model, Spacing = 8, Children = {
            new TextBlock { Text = "专业图族 · 范围选择与多图联动", FontSize = 19, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "点击或键盘选择原始业务项；拖动 Mosaic 横向 / Strip 纵向范围条选中可见图元的完整来源。不同布局仅按业务 ID 联动，不混用分类、数值和地图坐标。关闭 Trackball 可查看业务 Tooltip。永久模板与数据选择进入导出，临时读数排除。", FontSize = 12, TextWrapping = TextWrapping.Wrap }, options, status,
            new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 20, Children = { new StackPanel { Spacing = 2, Children = { new TextBlock { Text = "Mosaic · 本地横向范围" }, horizontal } }, new StackPanel { [Grid.ColumnProperty] = 1, Spacing = 2, Children = { new TextBlock { Text = "Strip · 本地纵向范围" }, vertical } } } }, owner.Group } };
        var compactLayout = false; view.SizeChanged += (_, change) => { var compact = change.NewSize.Width < 1120; if (compact == compactLayout) return; compactLayout = compact; owner.Group.ColumnDefinitions = new(compact ? "*" : "*,*"); owner.Group.RowDefinitions = new(compact ? "Auto,Auto,Auto,Auto" : "Auto,Auto"); for (var i = 0; i < owner.Group.Children.Count; i++) { Grid.SetColumn(owner.Group.Children[i], compact ? 0 : i % 2); Grid.SetRow(owner.Group.Children[i], compact ? i : i / 2); } };
        Status(); return new(view, owner, links, horizontal, vertical, enabled);
    }
    private static Border LinkingCompactBody(ChartBase chart, string prefix, string first, string second, string format)
    {
        TextBlock Read(string name, string path, string? stringFormat = null) { var text = new TextBlock { Name = name, FontSize = 10, LineHeight = 12, MaxWidth = 145, TextTrimming = TextTrimming.CharacterEllipsis }; var binding = new Binding(path) { StringFormat = stringFormat }; text.Bind(TextBlock.TextProperty, binding); text.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = chart }); return text; }
        var lines = new StackPanel(); lines.Children.Add(Read(prefix + "Text", first)); if (!prefix.EndsWith("Legend", StringComparison.Ordinal)) lines.Children.Add(Read(prefix + "Detail", second, format));
        var border = new Border { Name = prefix + "Body", Padding = new Thickness(3, 2), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), Child = lines };
        border.Bind(Border.BorderBrushProperty, new Binding(nameof(Foreground)) { Source = chart }); border.Bind(Border.BackgroundProperty, new Binding(chart is EngineeringChartBase ? nameof(EngineeringChartBase.TooltipBackground) : nameof(AnalysisChartBase.TooltipBackground)) { Source = chart }); return border;
    }
    private static IReadOnlyList<ChartDataPoint> LinkingData(ChartBase chart) => chart is EngineeringChartBase e ? e.SelectedData : ((AnalysisChartBase)chart).SelectedData;
    private static IReadOnlyList<object>? LinkingKeys(ChartBase chart) => chart is EngineeringChartBase e ? e.SelectedDataKeys : ((AnalysisChartBase)chart).SelectedDataKeys;
    private static void ClearLinkingData(ChartBase chart) { if (chart is EngineeringChartBase e) e.ClearDataSelection(); else ((AnalysisChartBase)chart).ClearDataSelection(); }
    private sealed record ProfessionalLinkingBoard(StackPanel View, ProfessionalPlotTrackballBoard Owner, ChartDataSynchronizationGroup Links, ChartDataRangeSelector Horizontal, ChartDataRangeSelector Vertical, CheckBox Enabled) : IDisposable
    {
        public void Dispose() { Links.Dispose(); Horizontal.EngineeringChart = null; Vertical.EngineeringChart = null; foreach (var chart in Owner.Charts) { ClearLinkingData(chart); SetProfessionalPresentationTemplates(chart, null, null); SetProfessionalTemplate(chart, null); } Owner.Dispose(); }
    }
}
