using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private double _engineeringCarpetOffset = .5, _engineeringHexRadius = 16, _engineeringReferenceImpedance = 50;
    private HexbinAggregation _engineeringHexAggregation = HexbinAggregation.Count;
    private int _engineeringSectorCount = 16, _engineeringSelectedIndex = -1;
    public double EngineeringCarpetOffset { get => _engineeringCarpetOffset; set { if (_engineeringCarpetOffset == value) return; _engineeringCarpetOffset = value; Notify(); } }
    public double EngineeringHexRadius { get => _engineeringHexRadius; set { if (_engineeringHexRadius == value) return; _engineeringHexRadius = value; Notify(); } }
    public double EngineeringReferenceImpedance { get => _engineeringReferenceImpedance; set { if (_engineeringReferenceImpedance == value) return; _engineeringReferenceImpedance = value; Notify(); } }
    public HexbinAggregation EngineeringHexAggregation { get => _engineeringHexAggregation; set { if (_engineeringHexAggregation == value) return; _engineeringHexAggregation = value; Notify(); } }
    public int EngineeringSectorCount { get => _engineeringSectorCount; set { if (_engineeringSectorCount == value) return; _engineeringSectorCount = value; Notify(); } }
    public int EngineeringSelectedIndex { get => _engineeringSelectedIndex; set { if (_engineeringSelectedIndex == value) return; _engineeringSelectedIndex = value; Notify(); } }
    public double[] EngineeringCarpetOffsets { get; } = [0, .5, 1];
    public double[] EngineeringHexRadii { get; } = [10, 16, 24];
    public double[] EngineeringReferenceImpedances { get; } = [25, 50, 75];
    public int[] EngineeringSectorCounts { get; } = [4, 8, 16];
    public HexbinAggregation[] EngineeringHexAggregations { get; } = Enum.GetValues<HexbinAggregation>();
    public IReadOnlyList<double> EngineeringSpeedBounds { get; } = Array.AsReadOnly(new[] { 3d, 6d });
    public EngineeringObserverCollection<EngineeringCarpetRecord> EngineeringCarpetRecords { get; } = new(
        from b in new[] { 0d, 1d, 2d } from a in new[] { 0d, 1d, 2d }
        select new EngineeringCarpetRecord($"surface-{a}-{b}", a, b, 10 + a + 2 * b + .5 * a * b));
    public EngineeringObserverCollection<EngineeringHexRecord> EngineeringHexRecords { get; } = new(
        Enumerable.Range(0, 80).Select(i => new EngineeringHexRecord($"sensor-{i}", i < 2 ? 0 : (i * 37) % 97, i < 2 ? 0 : (i * 71) % 89, 2 + i % 7)));
    public EngineeringObserverCollection<EngineeringSmithRecord> EngineeringSmithRecords { get; } =
        [new("antenna-a", 25, -25), new("antenna-b", 50, 50), new("antenna-c", 100, 0)];
    public EngineeringObserverCollection<EngineeringMixtureRecord> EngineeringMixtureRecords { get; } =
        [new("soil-a", 60, 25, 15), new("soil-b", 20, 55, 25), new("soil-c", 15, 20, 65)];
    public EngineeringObserverCollection<EngineeringWindRecord> EngineeringWindRecords { get; } =
        [new("wind-a", "N", 2, 3), new("wind-b", "N", 4, 5), new("wind-c", "NE", 7, 2), new("wind-d", "E", 2, 4),
         new("wind-e", "SW", 5, 3), new("wind-f", "W", 7, 5), new("wind-g", "NNW", 1, 2)];
    public ICommand EngineeringUpdateCommand => new ActionCommand(() =>
    {
        EngineeringCarpetRecords[4].Temperature += .25;
        EngineeringHexRecords[0].Load += 2;
        EngineeringSmithRecords[1].Resistance += 10;
        EngineeringMixtureRecords[0].Sand += 5;
        EngineeringWindRecords[0].Frequency += 1;
    });
}

public sealed class EngineeringObserverCollection<T> : ObservableCollection<T>
{
    public EngineeringObserverCollection() { }
    public EngineeringObserverCollection(IEnumerable<T> source) : base(source) { }
    private NotifyCollectionChangedEventHandler? _changed;
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public override event NotifyCollectionChangedEventHandler? CollectionChanged { add => _changed += value; remove => _changed -= value; }
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) { base.OnCollectionChanged(e); _changed?.Invoke(this, e); }
}

public abstract class EngineeringBusinessRecord(string id) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    public string Id { get; } = id;
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    protected void Set(ref double field, double value, [CallerMemberName] string? property = null)
    { if (field.Equals(value)) return; field = value; _changed?.Invoke(this, new(property)); }
}

public sealed class EngineeringCarpetRecord(string id, double parameterA, double parameterB, double temperature) : EngineeringBusinessRecord(id)
{
    private double _temperature = temperature;
    public double ParameterA { get; } = parameterA;
    public double ParameterB { get; } = parameterB;
    public double Temperature { get => _temperature; set => Set(ref _temperature, value); }
}

public sealed class EngineeringHexRecord(string id, double easting, double northing, double load) : EngineeringBusinessRecord(id)
{
    private double _load = load;
    public double Easting { get; } = easting;
    public double Northing { get; } = northing;
    public double Load { get => _load; set => Set(ref _load, value); }
}

public sealed class EngineeringSmithRecord(string id, double resistance, double reactance) : EngineeringBusinessRecord(id)
{
    private double _resistance = resistance;
    public double Resistance { get => _resistance; set => Set(ref _resistance, value); }
    public double Reactance { get; } = reactance;
}

public sealed class EngineeringMixtureRecord(string id, double sand, double clay, double silt) : EngineeringBusinessRecord(id)
{
    private double _sand = sand;
    public double Sand { get => _sand; set => Set(ref _sand, value); }
    public double Clay { get; } = clay;
    public double Silt { get; } = silt;
}

public sealed class EngineeringWindRecord(string id, string direction, double speed, double frequency) : EngineeringBusinessRecord(id)
{
    private double _frequency = frequency;
    public string Direction { get; } = direction;
    public double Speed { get; } = speed;
    public double Frequency { get => _frequency; set => Set(ref _frequency, value); }
}
