using System.Collections.ObjectModel;
using System.Windows.Input;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private EngineeringObserverCollection<C5CategoryRecord> _c5Samples = [new("cat-a", "A", 2, "First"), new("cat-b", "A", 4, "Second"), new("cat-c", "A", 4, "Repeated"), new("cat-d", "B", 3, "Fourth"), new("cat-e", "B", 7, "Fifth")];
    private EngineeringObserverCollection<C5ProfileRecord> _c5Profiles = [new("dist-a", "Alpha", [1, 2, 2, 3, 5]), new("dist-b", "Beta", [3, 4, 5, 5, 6])];
    private EngineeringObserverCollection<DistributionSamples> _c5Ridges = [new("ridge-a", "Alpha", new EngineeringObserverCollection<double>([1, 2, 3, 3, 5])), new("ridge-b", "Beta", new EngineeringObserverCollection<double>([3, 4, 4, 5, 7]))];
    private EngineeringObserverCollection<C5ScalarRecord> _c5Grid = CreateC5Grid();
    private EngineeringObserverCollection<double> _c5Levels = [1, 2, 3];
    private EngineeringObserverCollection<MosaicObservation> _c5Mosaic = [new("m-a", "Online", "New", 2, "First"), new("m-b", "Online", "New", 1, "Duplicate pair"), new("m-c", "Online", "Returning", 3), new("m-d", "Store", "New", 4), new("m-e", "Store", "Returning", 2)];
    private EngineeringObserverCollection<ParallelRecord> _c5Parallel = [new("p-a", new EngineeringObserverCollection<double>([2, 5, 8]), "Alpha"), new("p-b", new EngineeringObserverCollection<double>([6, 3, 4]), "Beta"), new("p-c", new EngineeringObserverCollection<double>([9, 7, 2]), "Gamma")];
    private double _c5Jitter = .3, _c5PointRadius = 4, _c5Bandwidth = .6;
    private bool _c5Labels = true, _c5Mean = true;
    private int _c5SwarmSelected = -1, _c5StripSelected = -1, _c5ViolinSelected = -1, _c5RidgeSelected = -1, _c5ContourSelected = -1, _c5MosaicSelected = -1, _c5ParallelSelected = -1;
    public EngineeringObserverCollection<C5CategoryRecord> C5Samples { get => _c5Samples; set { if (ReferenceEquals(value, _c5Samples)) return; _c5Samples = value; Notify(); } }
    public EngineeringObserverCollection<C5ProfileRecord> C5Profiles { get => _c5Profiles; set { if (ReferenceEquals(value, _c5Profiles)) return; _c5Profiles = value; Notify(); } }
    public EngineeringObserverCollection<DistributionSamples> C5Ridges { get => _c5Ridges; set { if (ReferenceEquals(value, _c5Ridges)) return; _c5Ridges = value; Notify(); } }
    public EngineeringObserverCollection<C5ScalarRecord> C5Grid { get => _c5Grid; set { if (ReferenceEquals(value, _c5Grid)) return; _c5Grid = value; Notify(); } }
    public EngineeringObserverCollection<double> C5Levels { get => _c5Levels; set { if (ReferenceEquals(value, _c5Levels)) return; _c5Levels = value; Notify(); } }
    public EngineeringObserverCollection<MosaicObservation> C5Mosaic { get => _c5Mosaic; set { if (ReferenceEquals(value, _c5Mosaic)) return; _c5Mosaic = value; Notify(); } }
    public EngineeringObserverCollection<ParallelRecord> C5Parallel { get => _c5Parallel; set { if (ReferenceEquals(value, _c5Parallel)) return; _c5Parallel = value; Notify(); } }
    public ObservableCollection<ParallelAxis> C5ParallelAxes { get; } = new EngineeringObserverCollection<ParallelAxis>([new() { Id = "speed", Header = "Speed" }, new() { Id = "cost", Header = "Cost" }, new() { Id = "quality", Header = "Quality" }]);
    public double C5Jitter { get => _c5Jitter; set { if (_c5Jitter == value) return; _c5Jitter = value; Notify(); } }
    public double C5PointRadius { get => _c5PointRadius; set { if (_c5PointRadius == value) return; _c5PointRadius = value; Notify(); } }
    public double C5Bandwidth { get => _c5Bandwidth; set { if (_c5Bandwidth == value) return; _c5Bandwidth = value; Notify(); } }
    public bool C5Labels { get => _c5Labels; set { if (_c5Labels == value) return; _c5Labels = value; Notify(); } }
    public bool C5Mean { get => _c5Mean; set { if (_c5Mean == value) return; _c5Mean = value; Notify(); } }
    public int C5SwarmSelected { get => _c5SwarmSelected; set { if (_c5SwarmSelected == value) return; _c5SwarmSelected = value; Notify(); } }
    public int C5StripSelected { get => _c5StripSelected; set { if (_c5StripSelected == value) return; _c5StripSelected = value; Notify(); } }
    public int C5ViolinSelected { get => _c5ViolinSelected; set { if (_c5ViolinSelected == value) return; _c5ViolinSelected = value; Notify(); } }
    public int C5RidgeSelected { get => _c5RidgeSelected; set { if (_c5RidgeSelected == value) return; _c5RidgeSelected = value; Notify(); } }
    public int C5ContourSelected { get => _c5ContourSelected; set { if (_c5ContourSelected == value) return; _c5ContourSelected = value; Notify(); } }
    public int C5MosaicSelected { get => _c5MosaicSelected; set { if (_c5MosaicSelected == value) return; _c5MosaicSelected = value; Notify(); } }
    public int C5ParallelSelected { get => _c5ParallelSelected; set { if (_c5ParallelSelected == value) return; _c5ParallelSelected = value; Notify(); } }
    public ICommand C5CompletionUpdateCommand => new ActionCommand(() => { C5Samples[0].Amount += 1; C5Profiles[0].Observations.Add(6); C5Grid[0].Reading += .25; ((EngineeringObserverCollection<double>)C5Parallel[0].Values)[0] += 1; });
    private static EngineeringObserverCollection<C5ScalarRecord> CreateC5Grid() => new(Enumerable.Range(0, 9).Select(index => new C5ScalarRecord("grid-" + index, index % 3, index / 3, index % 3 + index / 3)));
}

public sealed class C5CategoryRecord(string id, string group, double amount, string name) : AdvancedBusinessRecord(id)
{
    private string _group = group, _name = name; private double _amount = amount;
    public string Group { get => _group; set => Set(ref _group, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public double Amount { get => _amount; set => Set(ref _amount, value); }
}
public sealed class C5ProfileRecord(string id, string name, IEnumerable<double> observations) : AdvancedBusinessRecord(id)
{
    private EngineeringObserverCollection<double> _observations = new(observations); private string _name = name;
    public string Name { get => _name; set => Set(ref _name, value); }
    public EngineeringObserverCollection<double> Observations { get => _observations; set => Set(ref _observations, value); }
}
public sealed class C5ScalarRecord(string id, double east, double north, double reading) : AdvancedBusinessRecord(id)
{
    private double _reading = reading;
    public double East { get; } = east; public double North { get; } = north;
    public double Reading { get => _reading; set => Set(ref _reading, value); }
}
