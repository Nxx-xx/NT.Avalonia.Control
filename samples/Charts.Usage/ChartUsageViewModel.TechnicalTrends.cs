using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private ObservableCollection<TechnicalUsageRecord> _technicalRecords = CreateTechnicalRecords();
    private int _technicalPeriod = 3;
    private ChartMovingAverageType _technicalAverageType;
    private ChartTrendlineType _technicalTrendType;
    public ObservableCollection<TechnicalUsageRecord> TechnicalRecords
    { get => _technicalRecords; set { if (ReferenceEquals(_technicalRecords, value)) return; _technicalRecords = value; Notify(); } }
    public int TechnicalPeriod { get => _technicalPeriod; set { if (_technicalPeriod == value) return; _technicalPeriod = value; Notify(); } }
    public ChartMovingAverageType TechnicalAverageType { get => _technicalAverageType; set { if (_technicalAverageType == value) return; _technicalAverageType = value; Notify(); } }
    public ChartTrendlineType TechnicalTrendType { get => _technicalTrendType; set { if (_technicalTrendType == value) return; _technicalTrendType = value; Notify(); } }
    public int[] TechnicalPeriods { get; } = [2, 3, 4, 5];
    public ChartMovingAverageType[] TechnicalAverageTypes { get; } = Enum.GetValues<ChartMovingAverageType>();
    public ChartTrendlineType[] TechnicalTrendTypes { get; } = Enum.GetValues<ChartTrendlineType>();
    public ICommand TechnicalUpdateCommand => new ActionCommand(() => TechnicalRecords[1].Y += 3);
    public ICommand TechnicalAppendCommand => new ActionCommand(() =>
    {
        var next = TechnicalRecords.Count + 1;
        TechnicalRecords.Add(new($"technical-{next}", next, 20 + next));
    });
    public ICommand TechnicalReplaceCommand => new ActionCommand(() => TechnicalRecords = CreateTechnicalRecords(5));
    internal static ObservableCollection<TechnicalUsageRecord> CreateTechnicalRecords(double offset = 0)
    {
        double[] values = [4, 8, 6, 12, 10, 14, double.NaN, 18, 24, 20, 28, 26];
        return new(values.Select((value, index) => new TechnicalUsageRecord($"technical-{index + 1}", index + 1, value + offset)));
    }
}

public sealed class TechnicalUsageRecord(string id, double x, double y) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private double _y = y;
    public string Id { get; } = id;
    public double X { get; } = x;
    public double Y { get => _y; set { if (_y.Equals(value)) return; _y = value; _changed?.Invoke(this, new(nameof(Y))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
