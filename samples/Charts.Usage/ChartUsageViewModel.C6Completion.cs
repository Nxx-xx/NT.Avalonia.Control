using System.Windows.Input;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private int _c6Selected = -1;
    private IReadOnlyList<object> _c6Seats = [];
    private EngineeringObserverCollection<C6TaskRecord> _c6Tasks = CreateC6Tasks();
    private EngineeringObserverCollection<C6MetricRecord> _c6Shares = [new("a", "Alpha", 1), new("b", "Beta", 3)];
    public int C6SelectedIndex { get => _c6Selected; set { if (_c6Selected == value) return; _c6Selected = value; Notify(); } }
    public IReadOnlyList<object> C6SelectedSeats { get => _c6Seats; set { if (ReferenceEquals(_c6Seats, value)) return; _c6Seats = value; Notify(); } }
    public EngineeringObserverCollection<C6TaskRecord> C6Tasks { get => _c6Tasks; set { if (ReferenceEquals(value, _c6Tasks)) return; _c6Tasks = value; Notify(); } }
    public EngineeringObserverCollection<C6MetricRecord> C6Shares { get => _c6Shares; set { if (ReferenceEquals(value, _c6Shares)) return; _c6Shares = value; Notify(); } }
    public C6MetricRecord C6Headline { get; } = new("headline", "Revenue", 72) { Prior = 60, Goal = 85 };
    public EngineeringObserverCollection<double> C6Trend { get; } = [40, 55, 48, 65, 72];
    public EngineeringObserverCollection<double> C6BulletRanges { get; } = [30, 60, 100];
    public EngineeringObserverCollection<C6SpanRecord> C6Spans { get; } = [new("cpu-a", "Compile", 0, 6, "CPU"), new("cpu-b", "Tests", 4, 8, "CPU"), new("io", "Publish", 2, 7, "IO")];
    public EngineeringObserverCollection<C6EventRecord> C6Events { get; } = [new("e0", "Plan", Epoch.AddHours(1)), new("e1", "Build", Epoch.AddHours(4)), new("e2", "Ship", Epoch.AddHours(8))];
    public EngineeringObserverCollection<C6MetricRecord> C6Calendar { get; } = [new("day-a", "Monday", 1) { Day = new(2026, 10, 19) }, new("day-b", "Wednesday", 4) { Day = new(2026, 10, 21) }, new("day-c", "Friday", 2) { Day = new(2026, 10, 23) }];
    public EngineeringObserverCollection<C6MetricRecord> C6Matrix { get; } = [new("ax", "A/X", 1) { Row = "A", Column = "X" }, new("ay", "A/Y", 2) { Row = "A", Column = "Y" }, new("bx", "B/X", 3) { Row = "B", Column = "X" }, new("by", "B/Y", 4) { Row = "B", Column = "Y" }];
    public EngineeringObserverCollection<C6MetricRecord> C6TableRows { get; } = [new("product-a", "Apple", 20) { Prior = 18 }, new("product-b", "Orange", 12) { Prior = 15 }];
    public EngineeringObserverCollection<OperationalTableColumn> C6Columns { get; } = [new("Sales", "Amount") { Id = "sales", Format = "0.0", Minimum = 0, Maximum = 30 }, new("Before", "Prior") { Id = "prior", Format = "0.0" }, new("Product", "Name") { Id = "product", UseColorScale = false }];
    public EngineeringObserverCollection<C6MetricRecord> C6Words { get; } = [new("av", "Avalonia", 8), new("linux", "Linux", 5), new("data", "数据", 4), new("ui", "UI", 3)];
    public EngineeringObserverCollection<C6MetricRecord> C6Ranks { get; } = [new("a0", "Alpha", 8) { Entity = "Alpha", Period = "2024" }, new("b0", "Beta", 6) { Entity = "Beta", Period = "2024" }, new("a1", "Alpha", 5) { Entity = "Alpha", Period = "2025" }, new("b1", "Beta", 9) { Entity = "Beta", Period = "2025" }, new("a2", "Alpha", 10) { Entity = "Alpha", Period = "2026" }, new("b2", "Beta", 7) { Entity = "Beta", Period = "2026" }];
    public EngineeringObserverCollection<C6MetricRecord> C6Slopes { get; } = [new("a0", "Alpha", 10) { Entity = "Alpha", Period = "Before" }, new("a1", "Alpha", 16) { Entity = "Alpha", Period = "After" }, new("b0", "Beta", 15) { Entity = "Beta", Period = "Before" }, new("b1", "Beta", 8) { Entity = "Beta", Period = "After" }];
    public EngineeringObserverCollection<C6MetricRecord> C6Parties { get; } = [new("party-a", "Alliance", 12), new("party-b", "Coalition", 8), new("party-c", "Green", 4)];
    public EngineeringObserverCollection<C6MetricRecord> C6River { get; } = [new("a0", "A", 1) { Row = "A", Column = "Jan" }, new("a1", "A", 2) { Row = "A", Column = "Feb" }, new("a2", "A", 3) { Row = "A", Column = "Mar" }, new("b0", "B", 3) { Row = "B", Column = "Jan" }, new("b1", "B", 2) { Row = "B", Column = "Feb" }, new("b2", "B", 1) { Row = "B", Column = "Mar" }];
    public EngineeringObserverCollection<C6TreeRecord> C6Roots { get; } = [new("root", "Service", 10, 4, [new("leaf-a", "Parse", 4, 0), new("leaf-b", "Draw", 3, 0)])];
    public EngineeringObserverCollection<C6MapRecord> C6MapPoints { get; } = [new("city-a", "Station A", -25, 1, 2), new("city-b", "Station B", 25, 15, 4)];
    public EngineeringObserverCollection<C6MapRecord> C6MapRegions { get; } = [new("a", "Zone A", 0, 0, 1) { Status = "Open" }, new("b", "Zone B", 0, 0, 2) { Status = "Closed" }];
    public IReadOnlyDictionary<object, IBrush> C6CategoryColors { get; } = new Dictionary<object, IBrush> { ["Open"] = Brushes.SeaGreen, ["Closed"] = Brushes.OrangeRed };
    public IReadOnlyList<MapVector> C6Vectors { get; } = [new MapLineVector("vector-line", new(-25, 0), new(25, 15)), new MapCircleVector("vector-circle", new(0, 10), 12)];
    public ICommand C6UpdateCommand => new ActionCommand(() => { C6Headline.Amount += 1; C6TableRows[0].Amount += 1; C6MapPoints[0].Metric += 1; C6Tasks[0].Complete = (C6Tasks[0].Complete + 10) % 100; });
    private static DateTimeOffset Epoch => new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
    private static EngineeringObserverCollection<C6TaskRecord> CreateC6Tasks() => [new("design", "Design", Epoch, Epoch.AddHours(6), 60), new("build", "Build", Epoch.AddHours(8), Epoch.AddHours(14), 30) { Links = [new("design")] }, new("ship", "Release", Epoch.AddHours(16), Epoch.AddHours(16), 0)];
}

public sealed class C6MetricRecord(string id, string name, double amount) : AdvancedBusinessRecord(id)
{
    private double _amount = amount, _prior, _goal = 100;
    public string Name { get; } = name;
    public double Amount { get => _amount; set => Set(ref _amount, value); }
    public double Prior { get => _prior; set => Set(ref _prior, value); }
    public double Goal { get => _goal; set => Set(ref _goal, value); }
    public DateOnly Day { get; init; } = new(2026, 10, 19);
    public object Row { get; init; } = "A";
    public object Column { get; init; } = "X";
    public object Entity { get; init; } = name;
    public object Period { get; init; } = "2026";
}
public sealed class C6TaskRecord(string id, string name, DateTimeOffset open, DateTimeOffset close, double complete) : AdvancedBusinessRecord(id)
{
    private DateTimeOffset _open = open, _close = close; private double _complete = complete;
    public string Name { get; } = name;
    public DateTimeOffset Open { get => _open; set => Set(ref _open, value); }
    public DateTimeOffset Close { get => _close; set => Set(ref _close, value); }
    public double Complete { get => _complete; set => Set(ref _complete, value); }
    public string Team { get; } = "Product";
    public EngineeringObserverCollection<ScheduleDependency> Links { get; init; } = [];
}
public sealed class C6SpanRecord(string id, string name, double open, double close, string resource) : AdvancedBusinessRecord(id)
{ public string Name { get; } = name; public double Open { get; } = open; public double Close { get; } = close; public string Resource { get; } = resource; }
public sealed class C6EventRecord(string id, string name, DateTimeOffset when) : AdvancedBusinessRecord(id)
{ public string Name { get; } = name; public DateTimeOffset When { get; } = when; public string Details => "Event " + Name; public double Intensity => 1; }
public sealed class C6TreeRecord(string id, string name, double cost, double distance, IEnumerable<C6TreeRecord>? children = null) : AdvancedBusinessRecord(id)
{
    private double _cost = cost, _distance = distance; private EngineeringObserverCollection<C6TreeRecord> _branches = new(children ?? []);
    public string Name { get; } = name;
    public double Cost { get => _cost; set => Set(ref _cost, value); }
    public double Distance { get => _distance; set => Set(ref _distance, value); }
    public EngineeringObserverCollection<C6TreeRecord> Branches { get => _branches; set => Set(ref _branches, value); }
}
public sealed class C6MapRecord(string id, string name, double east, double north, double metric) : AdvancedBusinessRecord(id)
{
    private double _metric = metric; private string _status = "Open";
    public string Name { get; } = name;
    public double East { get; } = east; public double North { get; } = north;
    public double EndEast => East < 0 ? 25 : -25; public double EndNorth => North < 10 ? 15 : 1;
    public double Metric { get => _metric; set => Set(ref _metric, value); }
    public string Region => Id;
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Tint => Status == "Open" ? "#55B47A" : "#D25573";
    public EngineeringObserverCollection<C6MetricRecord> Parts { get; } = [new("part-a", "First", 1), new("part-b", "Second", 3)];
}
