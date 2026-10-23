using Avalonia;
using Avalonia.Controls;
using NT.AvaloniaControl.Demo.Navigation;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private sealed record ChartSection(string Group, string Title, string Description, string Preview, Func<Control> Create);

    private IReadOnlyList<ChartSection> ChartSections() =>
    [
        new("坐标轴与数据", "数据绑定与坐标轴", "日期、分类、对数与多轴绑定", "line", CreateDataAndAxesExamples),
        new("坐标轴与数据", "坐标轴配置", "标签、网格与多坐标轴", "line", CreateAxisFeaturesExamples),
        new("交互与呈现", "通用交互", "图例、读数、选择与导出", "line", CreateCommonFeaturesExamples),
        new("交互与呈现", "形状标注", "参考线、箭头与区域标注", "line", CreateShapeAnnotationsExamples),
        new("交互与呈现", "提示模板", "共享读数与自定义提示", "line", CreateTooltipTemplateExamples),
        new("交互与呈现", "控件模板", "图例、标签与业务呈现", "bar", CreateControlTemplateExamples),
        new("交互与呈现", "轨迹球", "数据追踪、插值与读数", "line", CreateTrackballExamples),
        new("交互与呈现", "选择与范围联动", "系列选择与多图同步", "scatter", CreateSelectionRangeExamples),
        new("交互与呈现", "输入手势", "鼠标、触摸、缩放与平移", "line", CreateInputGesturesExamples),
        new("交互与呈现", "标注与内容模板", "参考元素与绘图区内容", "line", CreateAnnotationCompletionExamples),
        new("交互与呈现", "动画", "时长、缓动与动态更新", "line", CreateAnimationExamples),
        new("交互与呈现", "图表导出", "导出格式与图像配置", "doc", CreateExportCompletionExamples),
        new("交互与呈现", "无障碍", "键盘浏览与高对比度", "keys", CreateAccessibilityExamples),
        new("趋势与面积", "折线与面积", "平滑、阶梯、缺失点与面积基线", "line", CreateLineAreaExamples),
        new("柱形与比较", "柱形与堆叠", "分组、堆叠、排序与方向", "bar", CreateBarComparisonExamples),
        new("散点与气泡", "散点与气泡", "大小、颜色与源序连线", "scatter", CreateScatterBubbleExamples),
        new("圆形与仪表", "饼图与环图", "组成、占比与圆形图", "ring", CreateCircularExamples),
        new("圆形与仪表", "极坐标与雷达", "角度、半径与多种系列", "ring", CreatePolarExamples),
        new("圆形与仪表", "仪表与微图", "仪表盘、进度与微型图表", "ring", CreateGaugeMicroExamples),
        new("柱形与比较", "基础分析图", "漏斗、金字塔、热力与主题河流", "bar", CreateAnalysisExamples),
        new("性能与实时数据", "细节层级", "MinMax 抽样与大数据呈现", "line", CreateLevelOfDetailExamples),
        new("性能与实时数据", "高效命中", "密集数据的查询与命中", "scatter", CreateEfficientHitExamples),
        new("性能与实时数据", "增量流式数据", "持续追加、缓冲与调度", "line", CreateIncrementalStreamingExamples),
        new("性能与实时数据", "渲染资源", "资源使用与预算", "bar", CreateRenderResourceExamples),
        new("性能与实时数据", "多图布局", "后台布局与多图调度", "table", CreateMultiChartExamples),
        new("金融与统计", "统计分布", "分布、误差与统计指标", "bar", CreateStatisticsExamples),
        new("金融与统计", "金融价格", "价格、成交量与金融系列", "bar", CreateFinancialExamples),
        new("金融与统计", "技术趋势", "技术指标与趋势分析", "line", CreateTechnicalTrendsExamples),
        new("地图与工程", "工程图表", "工程坐标、投影与专业图表", "scatter", CreateEngineeringExamples),
        new("柱形与比较", "高级比较", "镜像、人口金字塔与差异比较", "bar", CreateAdvancedComparisonExamples),
        new("层级与关系", "层级结构", "树形、矩形与空间布局", "table", CreateHierarchyExamples),
        new("层级与关系", "流向与关系", "网络、桑基与弦图", "line", CreateRelationsExamples),
        new("地图与工程", "地图", "地理数据、图层与叠加", "scatter", CreateMapExamples),
        new("层级与关系", "排程与扩展", "排程、分析排名、层级与地图扩展", "table", CreateC6CompletionExamples),
        new("金融与统计", "多维与分布扩展", "蜂群、等值线、列联比例与密度", "scatter", CreateC5CompletionExamples),
    ];

    public IReadOnlyList<DemoNavigationItem> CreateGallery()
    {
        // Cache each board on first entry so returning to a sample preserves its settings.
        var overview = (Control)_scroll.Content!;
        DemoNavigationItem Leaf(ChartSection section)
        {
            Control? board = null;
            return new DemoNavigationItem(section.Title, section.Description, section.Preview, open: () =>
            {
                if (board is null)
                {
                    var created = section.Create();
                    if (created is Expander expander && expander.Content is Control content)
                    {
                        expander.Content = null;
                        board = content;
                    }
                    else board = created;
                    HostThemeChanged(null, EventArgs.Empty);
                }
                _scroll.Content = board;
                _scroll.Margin = new Thickness(24, 20, 24, 20);
                _scroll.Offset = default;
                return this;
            });
        }
        string[] order = ["趋势与面积", "柱形与比较", "散点与气泡", "圆形与仪表", "金融与统计", "层级与关系", "地图与工程", "坐标轴与数据", "交互与呈现", "性能与实时数据"];
        var groups = ChartSections().GroupBy(section => section.Group).OrderBy(group => Array.IndexOf(order, group.Key)).Select(group =>
            DemoNavigationItem.Group(group.Key, string.Join(" · ", group.Take(3).Select(section => section.Title)), group.First().Preview,
                group.Select(Leaf).ToArray())).ToList();
        groups.Insert(0, DemoNavigationItem.Group("基础示例", "趋势、销售与负载分布的完整交互演示", "line",
            new DemoNavigationItem("基础图表联动", "刷新、追加数据、缩放、平移与共享操作", "line", open: () =>
            {
                _scroll.Content = overview;
                _scroll.Offset = default;
                return this;
            })));
        return groups;
    }
}
