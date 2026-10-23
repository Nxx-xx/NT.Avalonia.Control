using System.ComponentModel;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private bool _polarClockwise = true, _polarClosed = true, _polarLabels, _polarMixedReference;
    public bool PolarMixedReference { get => _polarMixedReference; set { if (_polarMixedReference == value) return; _polarMixedReference = value; Notify(); } }
    private double _polarStartAngle = -90, _polarSweep = 360;
    private PolarGridShape _polarGrid = PolarGridShape.Circle;
    private ChartAngleUnit _polarUnit;
    private ChartPointHit? _polarSelectedPoint;
    public ChartPointHit? PolarSelectedPoint { get => _polarSelectedPoint; set { if (Equals(_polarSelectedPoint, value)) return; _polarSelectedPoint = value; Notify(); } }
    public bool PolarClockwise { get => _polarClockwise; set { if (_polarClockwise == value) return; _polarClockwise = value; Notify(); } }
    public bool PolarClosed { get => _polarClosed; set { if (_polarClosed == value) return; _polarClosed = value; Notify(); } }
    public bool PolarLabels { get => _polarLabels; set { if (_polarLabels == value) return; _polarLabels = value; Notify(); } }
    public double PolarStartAngle { get => _polarStartAngle; set { if (_polarStartAngle == value) return; _polarStartAngle = value; Notify(); } }
    public double PolarSweep { get => _polarSweep; set { if (_polarSweep == value) return; _polarSweep = value; Notify(); } }
    public PolarGridShape PolarGrid { get => _polarGrid; set { if (_polarGrid == value) return; _polarGrid = value; Notify(); } }
    public ChartAngleUnit PolarUnit
    {
        get => _polarUnit;
        set
        {
            if (_polarUnit == value) return;
            _polarUnit = value; var factor = value == ChartAngleUnit.Radians ? Math.PI / 180 : 1;
            for (var index = 0; index < PolarRecords.Length; index++) PolarRecords[index].Angle = index * 90 * factor;
            Notify(); Notify(nameof(PolarAngleMaximum)); Notify(nameof(PolarAngleInterval));
        }
    }
    public double PolarAngleMaximum => PolarUnit == ChartAngleUnit.Radians ? 2 * Math.PI : 360;
    public double PolarAngleInterval => PolarAngleMaximum / 4;
    public double[] PolarStarts { get; } = [-90, 0, 45];
    public double[] PolarSweeps { get; } = [360, 270, 180];
    public PolarGridShape[] PolarGrids { get; } = Enum.GetValues<PolarGridShape>();
    public ChartAngleUnit[] PolarUnits { get; } = Enum.GetValues<ChartAngleUnit>();
    public PolarUsageRecord[] PolarRecords { get; } = [new("polar-a", "北", 0, 80, "#4385F4"), new("polar-b", "东", 90, 50, "#19A798"), new("polar-c", "南", 180, 70, "#F2A33A"), new("polar-d", "西", 270, 90, "#D7259F")];
    public PolarUsageRecord[] RadarRecords { get; } = [new("radar-a", "速度", 0, 80, "#4385F4"), new("radar-b", "质量", 90, 50, "#19A798"), new("radar-c", "成本", 180, 70, "#F2A33A"), new("radar-d", "交付", 270, 90, "#D7259F")];
    public PolarUsageRecord[] RadarComparisonRecords { get; } = [new("previous-c", "成本", 180, 45, "#19A798"), new("previous-a", "速度", 0, 65, "#19A798"), new("previous-d", "交付", 270, 85, "#19A798"), new("previous-b", "质量", 90, 40, "#19A798")];
}

public sealed class PolarUsageRecord(string id, string label, double angle, double value, string color) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private double _angle = angle, _value = value;
    private string _color = color;
    public string Id { get; } = id;
    public string Label { get; } = label;
    public double Angle { get => _angle; set { if (_angle == value) return; _angle = value; _changed?.Invoke(this, new(nameof(Angle))); } }
    public double Value { get => _value; set { if (_value == value) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
    public string Color { get => _color; set { if (_color == value) return; _color = value; _changed?.Invoke(this, new(nameof(Color))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
