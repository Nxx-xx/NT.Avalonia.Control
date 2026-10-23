using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private ComparisonSort _advancedSort;
    private Orientation _advancedOrientation;
    private double _advancedBaseline, _advancedCenterGap = 12, _advancedColumnGap = 4;
    private int _advancedSelectedIndex = -1;
    private bool _advancedUseMasks, _advancedShowLabels = true;
    public ComparisonSort AdvancedSort { get => _advancedSort; set { if (_advancedSort == value) return; _advancedSort = value; Notify(); } }
    public Orientation AdvancedOrientation { get => _advancedOrientation; set { if (_advancedOrientation == value) return; _advancedOrientation = value; Notify(); } }
    public double AdvancedBaseline { get => _advancedBaseline; set { if (_advancedBaseline == value) return; _advancedBaseline = value; Notify(); } }
    public double AdvancedCenterGap { get => _advancedCenterGap; set { if (_advancedCenterGap == value) return; _advancedCenterGap = value; Notify(); } }
    public double AdvancedColumnGap { get => _advancedColumnGap; set { if (_advancedColumnGap == value) return; _advancedColumnGap = value; Notify(); } }
    public int AdvancedSelectedIndex { get => _advancedSelectedIndex; set { if (_advancedSelectedIndex == value) return; _advancedSelectedIndex = value; Notify(); } }
    public bool AdvancedUseMasks { get => _advancedUseMasks; set { if (_advancedUseMasks == value) return; _advancedUseMasks = value; Notify(); Notify(nameof(AdvancedMaskPath)); } }
    public string? AdvancedMaskPath => AdvancedUseMasks ? nameof(AdvancedVennRecord.Mask) : null;
    public bool AdvancedShowLabels { get => _advancedShowLabels; set { if (_advancedShowLabels == value) return; _advancedShowLabels = value; Notify(); } }
    public ComparisonSort[] AdvancedSortOptions { get; } = Enum.GetValues<ComparisonSort>();
    public Orientation[] AdvancedOrientationOptions { get; } = Enum.GetValues<Orientation>();
    public double[] AdvancedBaselineOptions { get; } = [-10, 0, 10];
    public double[] AdvancedCenterGapOptions { get; } = [0, 12, 24];
    public double[] AdvancedColumnGapOptions { get; } = [0, 4, 10];
    public IReadOnlyList<string> AdvancedSetNames { get; } = Array.AsReadOnly(new[] { "Web", "Store" });
    public EngineeringObserverCollection<AdvancedSignedRecord> AdvancedSignedRecords { get; } =
        [new("retail", "零售", -12), new("cloud", "云服务", 35), new("support", "支持", 8)];
    public EngineeringObserverCollection<AdvancedPairRecord> AdvancedPairRecords { get; } =
        [new("north", "北区", 18, 28), new("south", "南区", 35, 20), new("west", "西区", 12, 22)];
    public EngineeringObserverCollection<AdvancedMekkoRecord> AdvancedMekkoRecords { get; } =
        [new("market-a", "市场 A", 2, [new("a-organic", "自然", 20), new("a-paid", "付费", 30)]),
         new("market-b", "市场 B", 3, [new("b-organic", "自然", 15), new("b-paid", "付费", 45)])];
    public EngineeringObserverCollection<AdvancedVennRecord> AdvancedVennRecords { get; } =
        [new("web", "Web 总量", 1, 60, ["Web"]), new("store", "Store 总量", 2, 40, ["Store"]),
         new("both", "两渠道交集", 3, 20, ["Web", "Store"])];
    public ICommand AdvancedUpdateCommand => new ActionCommand(() =>
    { AdvancedSignedRecords[0].Amount -= 1; AdvancedPairRecords[0].After += 2; AdvancedMekkoRecords[0].Parts[1].Amount += 3; AdvancedVennRecords[0].Amount += 5; });
}

public abstract class AdvancedBusinessRecord(string id) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    public string Id { get; } = id;
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; _changed?.Invoke(this, new(name)); }
}
public sealed class AdvancedSignedRecord(string id, string name, double amount) : AdvancedBusinessRecord(id)
{
    private double _amount = amount;
    public string Name { get; } = name;
    public double Amount { get => _amount; set => Set(ref _amount, value); }
}
public sealed class AdvancedPairRecord(string id, string name, double before, double after) : AdvancedBusinessRecord(id)
{
    private double _before = before, _after = after;
    public string Name { get; } = name;
    public double Before { get => _before; set => Set(ref _before, value); }
    public double After { get => _after; set => Set(ref _after, value); }
}
public sealed class AdvancedMekkoSegment(string id, string name, double amount) : AdvancedBusinessRecord(id)
{
    private double _amount = amount;
    public string Name { get; } = name;
    public double Amount { get => _amount; set => Set(ref _amount, value); }
}
public sealed class AdvancedMekkoRecord(string id, string name, double weight, IEnumerable<AdvancedMekkoSegment> parts) : AdvancedBusinessRecord(id)
{
    private double _weight = weight;
    private EngineeringObserverCollection<AdvancedMekkoSegment> _parts = new(parts);
    public string Name { get; } = name;
    public double Weight { get => _weight; set => Set(ref _weight, value); }
    public EngineeringObserverCollection<AdvancedMekkoSegment> Parts { get => _parts; set => Set(ref _parts, value); }
}
public sealed class AdvancedVennRecord(string id, string name, int mask, double amount, IEnumerable<string> sets) : AdvancedBusinessRecord(id)
{
    private double _amount = amount;
    private int _mask = mask;
    public string Name { get; } = name;
    public double Amount { get => _amount; set => Set(ref _amount, value); }
    public int Mask { get => _mask; set => Set(ref _mask, value); }
    public EngineeringObserverCollection<string> Sets { get; } = new(sets);
}
