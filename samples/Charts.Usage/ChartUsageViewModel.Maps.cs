using System.Windows.Input;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private MapProjection _mapProjection = MapProjection.Equirectangular;
    private int _mapSelectedIndex = -1;
    private bool _mapLabels;
    public MapProjection MapProjection { get => _mapProjection; set { if (_mapProjection == value) return; _mapProjection = value; Notify(); } }
    public IReadOnlyList<MapProjection> MapProjections { get; } = [MapProjection.Equirectangular, MapProjection.WebMercator];
    public int MapSelectedIndex { get => _mapSelectedIndex; set { if (_mapSelectedIndex == value) return; _mapSelectedIndex = value; Notify(); } }
    public bool MapLabels { get => _mapLabels; set { if (_mapLabels == value) return; _mapLabels = value; Notify(); } }
    public IReadOnlyList<MapFeature> MapFeatures { get; } = ChartMaps.ReadGeoJson("""
        {"type":"FeatureCollection","features":[
          {"type":"Feature","id":"a","geometry":{"type":"Polygon","coordinates":[[[-30,-20],[10,-20],[10,20],[-30,20],[-30,-20]],[[-15,-5],[-15,5],[-5,5],[-5,-5],[-15,-5]]]}},
          {"type":"Feature","id":"b","geometry":{"type":"MultiPolygon","coordinates":[[[[20,-20],[30,-20],[30,-10],[20,-10],[20,-20]]],[[[20,10],[30,10],[30,20],[20,20],[20,10]]]]}}
        ]}
        """).Features.Select(entry => entry.Feature).ToArray();
    public EngineeringObserverCollection<MapBusinessRegion> MapRegions { get; } = [new("a", "Alpha", 1), new("b", "Beta", 4), new("missing", "未匹配业务区域", 9)];
    public EngineeringObserverCollection<MapBusinessPoint> MapPoints { get; } = [new("p-a", "观测 A", -25, 1, 1), new("p-b", "观测 B", 25, 15, 4)];
    public ICommand MapUpdateCommand => new ActionCommand(() => { MapRegions[0].Amount += 1; MapPoints[0].Score += 1; });
    public ICommand MapSeatCommand => new ActionCommand(() => { var region = MapRegions[0]; region.Color = region.Color == "#55B47A" ? "#99A3AF" : "#55B47A"; });
}

public sealed class MapBusinessRegion(string id, string name, double amount) : AdvancedBusinessRecord(id)
{
    private double _amount = amount; private string _region = id; private string _color = "#55B47A";
    public string Name { get; } = name;
    public string Region { get => _region; set => Set(ref _region, value); }
    public double Amount { get => _amount; set => Set(ref _amount, value); }
    public string Color { get => _color; set => Set(ref _color, value); }
}
public sealed class MapBusinessPoint(string id, string name, double east, double north, double score) : AdvancedBusinessRecord(id)
{
    private double _score = score, _east = east, _north = north;
    public string Name { get; } = name;
    public double East { get => _east; set => Set(ref _east, value); }
    public double North { get => _north; set => Set(ref _north, value); }
    public double Score { get => _score; set => Set(ref _score, value); }
}
