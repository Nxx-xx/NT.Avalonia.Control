using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<ShapeMap> _mapCharts = [];
    private static readonly Lazy<string> OfflineWorldJson = new(() =>
    {
        using var stream = AssetLoader.Open(new Uri("avares://NT.AvaloniaControl.Demo/Assets/Maps/natural-earth-110m.geojson"));
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    });
    private const string MapFixtureJson = """
        {"type":"FeatureCollection","features":[
          {"type":"Feature","id":"a","properties":{"name":"Alpha"},"geometry":{"type":"Polygon","coordinates":[[[-30,-20],[10,-20],[10,20],[-30,20],[-30,-20]],[[-15,-5],[-15,5],[-5,5],[-5,-5],[-15,-5]]]}},
          {"type":"Feature","id":"b","properties":{"name":"Beta"},"geometry":{"type":"MultiPolygon","coordinates":[[[[20,-20],[30,-20],[30,-10],[20,-10],[20,-20]]],[[[20,10],[30,10],[30,20],[20,20],[20,10]]]]}}
        ]}
        """;
    private Control CreateMapExamples() => new Expander
    {
        Name = "ChartMapExamples", Header = "地理图：离线地图、区域着色、气泡、热力、形状和座位",
        HorizontalAlignment = HorizontalAlignment.Stretch, Content = CreateMapBoard(),
    };
    private Control CreateMapBoard(bool overview = false)
    {
        var values = new ObservableCollection<MapDemoValue>
        {
            new("CHN", "中国", 40), new("USA", "美国", 32), new("IND", "印度", 25), new("BRA", "巴西", 18),
            new("ZAF", "南非", 12), new("AUS", "澳大利亚", 22), new("JPN", "日本", 30), new("FRA", "法国", 35),
            new("DEU", "德国", 33), new("CAN", "加拿大", 20), new("ZZZ", "缺少区域示例", 15),
        };
        var cities = new ObservableCollection<MapDemoObservation>
        {
            new("sh", "上海", 121.47, 31.23, 28), new("bj", "北京", 116.4, 39.9, 18), new("tokyo", "东京", 139.69, 35.68, 36),
            new("london", "伦敦", -.12, 51.5, 16), new("paris", "巴黎", 2.35, 48.85, 12), new("ny", "纽约", -74, 40.7, 30),
            new("sf", "旧金山", -122.42, 37.77, 14), new("sydney", "悉尼", 151.2, -33.87, 20),
            new("sao", "圣保罗", -46.63, -23.55, 24), new("cape", "开普敦", 18.42, -33.92, 10),
        };
        ShapeMap World(string title)
        {
            var map = new ShapeMap { GeoJson = OfflineWorldJson.Value, TopologyRule = MapTopologyRule.EvenOdd, Title = title, Height = overview ? 300 : 320, ShowLabels = false };
            map.Layers.Add(new ShapeLayer { NoDataBrush = Brush("#D5DCE2"), Stroke = Brush("#6F8495") }); return map;
        }
        var choropleth = World("Choropleth · 区域着色与缺失关联");
        ((ShapeLayer)choropleth.Layers[0]).ItemsSource = values;
        var bubble = World("Bubble · 地理位置与面积");
        var bubbleLayer = new BubbleLayer { ItemsSource = cities, MaximumRadius = .075, Fill = Brush("#DB5E78"), Stroke = Brushes.White };
        bubble.Layers.Add(bubbleLayer);
        var heat = World("Heatmap · 栅格强度与原始成员");
        var heatLayer = new HeatmapLayer { ItemsSource = cities, Columns = 56, Rows = 28, Radius = 2, Opacity = .8 };
        heat.Layers.Add(heatLayer);
        var custom = new ShapeMap { GeoJson = MapFixtureJson, Title = "Shape · 空洞、离散区域和叠加", Height = overview ? 300 : 320, ShowLabels = false };
        var customValues = new ObservableCollection<MapDemoValue> { new("a", "含空洞区域", 1), new("b", "离散区域", 4) };
        custom.Layers.Add(new ShapeLayer { ItemsSource = customValues });
        custom.Layers.Add(new BubbleLayer { ItemsSource = new[] { new MapDemoObservation("overlay", "叠加点", -25, 0, 1) }, MaximumRadius = .055, Fill = Brush("#E4AB47") });
        var seat = new SeatMap { Title = "Seat · 平面坐标与业务状态选择", Height = overview ? 300 : 320, ShowLabels = false, ShowLegend = false };
        var seatValues = new ObservableCollection<MapDemoValue>(); var features = new List<MapFeature>();
        for (var row = 0; row < 6; row++) for (var column = 0; column < 8; column++)
        {
            var id = $"{row + 1}-{column + 1}"; var x = column + (column >= 4 ? .7 : 0); var y = row * 1.2;
            features.Add(new(id, new[] { new MapPolygon(new MapCoordinate[] { new(x,y), new(x+.8,y), new(x+.8,y+.8), new(x,y+.8), new(x,y) }) }, id));
            seatValues.Add(new(id, id, (row + column) % 5 == 0 ? 0 : 1) { Fill = (row + column) % 5 == 0 ? "#99A3AF" : "#55B47A" });
        }
        seat.FeaturesSource = features;
        seat.Layers.Add(new ShapeLayer { ItemsSource = seatValues, FillPath = "Fill", Stroke = Brushes.White });
        var charts = new[] { choropleth, bubble, heat, custom, seat };
        if (!overview) _mapCharts.AddRange(charts);
        var selected = new TextBlock { Text = "点击区域、气泡或热力格：返回原始业务记录；热力格保留全部贡献成员。", TextWrapping = TextWrapping.Wrap };
        var status = new TextBlock { Text = "离线 Natural Earth 110m · 177 个区域；数值仅为演示指标。ZZZ 保留为未匹配数据。", TextWrapping = TextWrapping.Wrap, Opacity = .8 };
        foreach (var chart in charts) chart.PropertyChanged += (_, change) =>
        {
            if (change.Property != EngineeringChartBase.SelectedPointProperty || chart.SelectedPoint is not { } hit) return;
            selected.Text = $"{chart.Title} · {hit.Mark.TooltipText} · 原始成员 {hit.Members.Count} · 序号 {string.Join(", ", hit.Members.Select(m => m.SourceIndex))}";
            if (ReferenceEquals(chart, seat) && hit.Members[0].Item is MapDemoValue value)
                status.Text = $"座位 {value.RegionId}：{(value.Value == 0 ? "不可用" : "可用")}；可用下方按钮切换状态。";
        };
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        AddAction(actions, "更新区域指标", () => values[0].Value = values[0].Value >= 60 ? 20 : values[0].Value + 10, "业务 INPC 自动刷新区域颜色。");
        AddAction(actions, "更新气泡 / 热力", () => cities[0].Value = cities[0].Value >= 60 ? 10 : cities[0].Value + 10, "同一业务记录驱动不同图层。");
        AddAction(actions, "切换投影", () =>
        {
            foreach (var chart in charts.Where(chart => chart.IsGeographic)) chart.Projection = chart.Projection == MapProjection.WebMercator ? MapProjection.Equirectangular : MapProjection.WebMercator;
            status.Text = "地理图层共享投影；Web Mercator 对极区纬度作显式裁限。座位保持平面坐标。";
        }, "等距经纬 / Web Mercator；原始经纬度不改写。");
        AddAction(actions, "切换气泡层", () => bubbleLayer.IsVisible = !bubbleLayer.IsVisible, "图层可见性和命中一起更新。");
        AddAction(actions, "切换座位状态", () =>
        {
            var value = seat.SelectedPoint?.Members[0].Item as MapDemoValue ?? seatValues[0]; value.Value = value.Value == 0 ? 1 : 0; value.Fill = value.Value == 0 ? "#99A3AF" : "#55B47A";
        }, "逐业务对象画刷和选择身份保持。");
        AddAction(actions, "缩放", () => { foreach (var chart in charts) { chart.Zoom = 1.3; chart.PanX = .02; } }, "所有图层使用同一缩放平移。");
        AddAction(actions, "恢复视图", () => { foreach (var chart in charts) chart.ResetViewport(); }, "恢复所有地图视图。");
        AddAction(actions, "后台读取离线资源", async () =>
        {
            try
            {
                foreach (var chart in charts.Take(3))
                {
                    chart.GeoJson = null; chart.SourceUri = new Uri("avares://NT.AvaloniaControl.Demo/Assets/Maps/natural-earth-110m.geojson");
                    await chart.LoadSourceAsync();
                }
                status.Text = $"离线资源读取完成：{choropleth.Snapshot?.Features.Count} 个区域；保留原始几何，自交填充采用显式 EvenOdd。";
            }
            catch (Exception error) { status.Text = "读取资源：" + error.Message; }
        }, "只读取内嵌资源；失败和取消不会发布旧地图。");
        AddAction(actions, "取消读取", () => { foreach (var chart in charts) chart.CancelSourceLoad(); }, "取消待处理的异步来源。");
        var labels = new CheckBox { Content = "标签", IsChecked = false, Margin = new Thickness(8, 0) };
        labels.IsCheckedChanged += (_, _) => { foreach (var chart in charts) chart.ShowLabels = labels.IsChecked == true; }; actions.Children.Add(labels);
        var grid = new Grid { ColumnDefinitions = new(overview ? "*,*,*" : "*,*"), RowDefinitions = new(overview ? "Auto,Auto" : "Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 12 };
        for (var i = 0; i < charts.Length; i++) { Grid.SetColumn(charts[i], i % (overview ? 3 : 2)); Grid.SetRow(charts[i], i / (overview ? 3 : 2)); grid.Children.Add(charts[i]); }
        return new StackPanel { Spacing = 10, Children = { new TextBlock { Text = "地图与图层 · 滚轮缩放 / 中键平移 / 方向键浏览 / Space 选择。空洞不着色、不命中；缺失关联保留诊断。", TextWrapping = TextWrapping.Wrap }, actions, selected, status, grid } };
    }
    private sealed class MapDemoValue(string regionId, string label, double value) : INotifyPropertyChanged
    {
        private double _value = value; private string? _fill;
        public string Id => RegionId;
        public string RegionId { get; } = regionId;
        public string Label { get; } = label;
        public double Value { get => _value; set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public string? Fill { get => _fill; set { if (_fill == value) return; _fill = value; PropertyChanged?.Invoke(this, new(nameof(Fill))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class MapDemoObservation(string id, string label, double longitude, double latitude, double value) : INotifyPropertyChanged
    {
        private double _value = value;
        public string Id { get; } = id;
        public string Label { get; } = label;
        public double Longitude { get; } = longitude;
        public double Latitude { get; } = latitude;
        public double Value { get => _value; set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
