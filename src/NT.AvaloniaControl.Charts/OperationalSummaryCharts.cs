using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Source-bound bullet rows. Out-of-domain values are clipped visually but remain raw in tooltips and selection.</summary>
public sealed class BulletChart : OperationalChartBase
{
    public static readonly StyledProperty<double?> ValueProperty = AvaloniaProperty.Register<BulletChart, double?>(nameof(Value), validate: x => x is null || double.IsFinite(x.Value));
    public static readonly StyledProperty<double> TargetProperty = AvaloniaProperty.Register<BulletChart, double>(nameof(Target), 85, validate: double.IsFinite);
    public static readonly StyledProperty<double> MinValueProperty = AvaloniaProperty.Register<BulletChart, double>(nameof(MinValue), 0, validate: double.IsFinite);
    public static readonly StyledProperty<double> MaxValueProperty = AvaloniaProperty.Register<BulletChart, double>(nameof(MaxValue), 100, validate: double.IsFinite);
    public static readonly StyledProperty<IReadOnlyList<double>?> RangesProperty = AvaloniaProperty.Register<BulletChart, IReadOnlyList<double>?>(nameof(Ranges));
    public BulletChart() => SecondaryPath = "Target";
    public double? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public double MinValue { get => GetValue(MinValueProperty); set => SetValue(MinValueProperty, value); }
    public double MaxValue { get => GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }
    public IReadOnlyList<double>? Ranges { get => GetValue(RangesProperty); set => SetValue(RangesProperty, value); }
    private bool IsScalar => ItemsSource is null && Value.HasValue;
    protected override ChartDataSnapshot CreateEngineeringSnapshot() => IsScalar ? ScalarSnapshot(Value!.Value) : base.CreateEngineeringSnapshot();
    public IReadOnlyList<OperationalBullet> Layout { get { _ = GetMarks(); return _layout; } }
    private IReadOnlyList<OperationalBullet> _layout = [];
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginOperationalScene(); var owners = new List<object?>(); BulletPoint[] values;
        try { values = data.Points.Select(p => new BulletPoint(Identity(p), IsScalar ? Value!.Value : Numeric(p, ValuePath), IsScalar ? Target : Numeric(p, SecondaryPath), IsScalar ? MinValue : Number(p.Item, "Minimum", out var min) ? min : 0, IsScalar ? MaxValue : Number(p.Item, "Maximum", out var max) ? max : 100, Numbers(p.Item, IsScalar ? nameof(Ranges) : RangesPath, owners), LabelOf(p))).ToArray(); } finally { ObserveNested(owners); }
        _layout = ChartOperational.Bullets(values); var marks = new List<EngineeringMarkInfo>();
        foreach (var row in _layout) { var y = (row.SourceIndex + .5) / Math.Max(1, data.Count); var height = .65 / Math.Max(1, data.Count); var displayWidth = Math.Max(row.Position, 1 / Math.Max(1, plot.Width)); var geometry = Rectangle(0, y - height / 5, displayWidth, height * .4, plot); var position = Normalized(displayWidth / 2, y, plot); marks.Add(Single(data.Points[row.SourceIndex], geometry, BrushAt(row.SourceIndex), position, Identity(data.Points[row.SourceIndex]), row.Source.Value, $"{row.Source.Label}: {ValueText(row.Source.Value)}; target {ValueText(row.Source.Target)}")); var a = Normalized(row.TargetPosition, y - height * .42, plot); var b = Normalized(row.TargetPosition, y + height * .42, plot); Decorations.Add((Line([a, b]), Foreground, 3)); DataLabel(marks[^1], $"{row.Source.Label}  {ValueText(row.Source.Value)} / {ValueText(row.Source.Target)}", Normalized(.5, y - height * .65, plot), plot.Width); }
        return marks;
    }
    protected override void RenderUnderlay(DrawingContext context, Rect plot)
    { foreach (var row in _layout) { var y = (row.SourceIndex + .5) / Math.Max(1, _layout.Count); var height = .65 / Math.Max(1, _layout.Count); var previous = 0d; var boundaries = row.Boundaries.Append(1d).Distinct().ToArray(); for (var j = 0; j < boundaries.Length; j++) { context.DrawGeometry(ColorAt(j, 0, Math.Max(1, boundaries.Length - 1)), null, Rectangle(previous, y - height / 2, boundaries[j] - previous, height, plot)); previous = boundaries[j]; } } }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    { var saved = Decorations.Where(x => x.Thickness == 3).ToArray(); foreach (var line in saved) context.DrawGeometry(null, new Pen(line.Brush ?? Foreground ?? Brushes.Gray, 3), line.Geometry); for (var ordinal = 0; ordinal < Labels.Count; ordinal++) { if (IsTemplatedDataLabel(ordinal)) continue; var label = Labels[ordinal]; DrawLabel(context, label.Text, label.Position, label.Width); } }
    protected override void ClearProjection(Exception error) { _layout = []; base.ClearProjection(error); }
}

/// <summary>KPI cards with signed absolute change, nullable change percentage at zero previous value, and source-owned trend.</summary>
public class KpiCardChart : OperationalChartBase
{
    public static readonly StyledProperty<double?> ValueProperty = AvaloniaProperty.Register<KpiCardChart, double?>(nameof(Value), validate: x => x is null || double.IsFinite(x.Value));
    public static readonly StyledProperty<double> PreviousProperty = AvaloniaProperty.Register<KpiCardChart, double>(nameof(Previous), 0, validate: double.IsFinite);
    public static readonly StyledProperty<double?> DeltaProperty = AvaloniaProperty.Register<KpiCardChart, double?>(nameof(Delta), validate: x => x is null || double.IsFinite(x.Value));
    public static readonly StyledProperty<OperationalDeltaType> DeltaTypeProperty = AvaloniaProperty.Register<KpiCardChart, OperationalDeltaType>(nameof(DeltaType), OperationalDeltaType.Percentage, validate: Enum.IsDefined);
    public static readonly StyledProperty<IReadOnlyList<double>?> SparklineDataProperty = AvaloniaProperty.Register<KpiCardChart, IReadOnlyList<double>?>(nameof(SparklineData));
    public static readonly StyledProperty<IReadOnlyList<double>?> TrendProperty = SparklineDataProperty;
    public static readonly StyledProperty<string?> UnitProperty = AvaloniaProperty.Register<KpiCardChart, string?>(nameof(Unit));
    public static readonly StyledProperty<string?> SubtitleProperty = AvaloniaProperty.Register<KpiCardChart, string?>(nameof(Subtitle));
    public static readonly StyledProperty<bool> ShowSparklineProperty = AvaloniaProperty.Register<KpiCardChart, bool>(nameof(ShowSparkline), true);
    public static readonly StyledProperty<IBrush?> PositiveBrushProperty = AvaloniaProperty.Register<KpiCardChart, IBrush?>(nameof(PositiveBrush));
    public static readonly StyledProperty<IBrush?> NegativeBrushProperty = AvaloniaProperty.Register<KpiCardChart, IBrush?>(nameof(NegativeBrush));
    public static readonly StyledProperty<IBrush?> SparklineBrushProperty = AvaloniaProperty.Register<KpiCardChart, IBrush?>(nameof(SparklineBrush));
    public double? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Previous { get => GetValue(PreviousProperty); set => SetValue(PreviousProperty, value); }
    public double? Delta { get => GetValue(DeltaProperty); set => SetValue(DeltaProperty, value); }
    public OperationalDeltaType DeltaType { get => GetValue(DeltaTypeProperty); set => SetValue(DeltaTypeProperty, value); }
    public IReadOnlyList<double>? SparklineData { get => GetValue(SparklineDataProperty); set => SetValue(SparklineDataProperty, value); }
    public IReadOnlyList<double>? Trend { get => SparklineData; set => SparklineData = value; }
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public bool ShowSparkline { get => GetValue(ShowSparklineProperty); set => SetValue(ShowSparklineProperty, value); }
    public IBrush? PositiveBrush { get => GetValue(PositiveBrushProperty); set => SetValue(PositiveBrushProperty, value); }
    public IBrush? NegativeBrush { get => GetValue(NegativeBrushProperty); set => SetValue(NegativeBrushProperty, value); }
    public IBrush? SparklineBrush { get => GetValue(SparklineBrushProperty); set => SetValue(SparklineBrushProperty, value); }
    private bool IsScalar => ItemsSource is null && Value.HasValue;
    protected override ChartDataSnapshot CreateEngineeringSnapshot() => IsScalar ? ScalarSnapshot(Value!.Value) : base.CreateEngineeringSnapshot();
    private IReadOnlyList<OperationalKpi> _layout = [];
    private readonly List<(string Text, Point Position, double Width, IBrush Brush, EngineeringMarkInfo Mark)> _deltaLabels = [];
    public IReadOnlyList<OperationalKpi> Layout { get { _ = GetMarks(); return _layout; } }
    private bool UseDarkCard
    {
        get { var color = (Foreground as ISolidColorBrush)?.Color ?? Colors.Gray; return 299 * color.R + 587 * color.G + 114 * color.B >= 128000; }
    }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginOperationalScene(); _deltaLabels.Clear(); var owners = new List<object?>(); KpiPoint[] values;
        try { values = data.Points.Select(p => new KpiPoint(Identity(p), IsScalar ? Value!.Value : Numeric(p, ValuePath), IsScalar ? Previous : Numeric(p, SecondaryPath), Numbers(p.Item, IsScalar ? nameof(SparklineData) : TrendPath, owners), LabelOf(p))).ToArray(); } finally { ObserveNested(owners); }
        _layout = Array.AsReadOnly(ChartOperational.Kpis(values).Select(row => row with { DeltaType = DeltaType, IsExplicitDelta = IsScalar && Delta.HasValue, DisplayDelta = IsScalar && Delta is { } explicitValue ? DeltaType == OperationalDeltaType.Percentage ? explicitValue / 100 : explicitValue : DeltaType == OperationalDeltaType.Percentage ? row.RelativeChange : row.Change }).ToArray()); var marks = new List<EngineeringMarkInfo>(); var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(_layout.Count))); var rows = Math.Max(1, (int)Math.Ceiling(_layout.Count / (double)columns));
        var dark = UseDarkCard; var fill = Brush.Parse(dark ? "#1E293B" : "#F5F7FA");
        foreach (var row in _layout) { var x = row.SourceIndex % columns / (double)columns; var y = row.SourceIndex / columns / (double)rows; var w = 1d / columns; var h = 1d / rows; var position = Normalized(x + w / 2, y + h / 2, plot); var delta = row.DisplayDelta is { } change ? row.DeltaType == OperationalDeltaType.Percentage ? change.ToString("+0.0%;-0.0%;0%") : change.ToString("+0.###;-0.###;0") : "—"; var positive = (row.DisplayDelta ?? row.Change) >= 0; var accent = positive ? PositiveBrush ?? Brush.Parse(dark ? "#86EFAC" : "#166534") : NegativeBrush ?? Brush.Parse(dark ? "#FCA5A5" : "#991B1B"); marks.Add(Single(data.Points[row.SourceIndex], Cell(x, y, w, h, plot), fill, position, Identity(data.Points[row.SourceIndex]), row.Source.Value, $"{row.Source.Label}: {ValueText(row.Source.Value)} {Unit}; Δ {delta}{(row.IsExplicitDelta ? " (explicit)" : "; previous " + ValueText(row.Source.Previous))}")); DataLabel(marks[^1], row.Source.Label, Normalized(x + w / 2, y + h * .16, plot), w * plot.Width - 8); if (ShowValues) AddDataLabel(marks[^1], $"{ValueText(row.Source.Value)} {Unit}", Normalized(x + w / 2, y + h * .36, plot), w * plot.Width - 8); if (ShowLabels) _deltaLabels.Add(($"Δ {delta}", Normalized(x + w / 2, y + h * .55, plot), w * plot.Width - 8, accent, marks[^1])); if (IsScalar) DataLabel(marks[^1], Subtitle, Normalized(x + w / 2, y + h * .69, plot), w * plot.Width - 8); if (ShowSparkline && row.Trend.Count > 1) { var min = row.Trend.Min(); var max = row.Trend.Max(); var points = row.Trend.Select((v, i) => Normalized(x + w * (.12 + .76 * i / (row.Trend.Count - 1d)), y + h * (.94 - .17 * Fraction(v, min, max)), plot)).ToArray(); Decorations.Add((Line(points), SparklineBrush ?? Foreground, 2)); } }
        return marks;
    }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    { base.RenderAnnotations(context, plot); if (!UsesDataLabelTemplate) foreach (var label in _deltaLabels) ColoredLabel(context, label.Text, label.Position, label.Width, label.Brush); }
    protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        foreach (var label in base.CreateDataLabels(marks)) yield return label;
        if (!ShowLabels) yield break;
        var indices = new Dictionary<EngineeringMarkInfo, int>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < marks.Count; index++) indices.Add(marks[index], index);
        foreach (var label in _deltaLabels)
            if (indices.TryGetValue(label.Mark, out var index))
                yield return LabelItem(label.Mark, index, label.Text, label.Position, new(label.Position.X - Math.Max(0, label.Width) / 2, label.Position.Y - FontSize, Math.Max(0, label.Width), FontSize * 2)) with { Brush = label.Brush };
    }
    private void ColoredLabel(DrawingContext context, string text, Point center, double width, IBrush brush)
    {
        if (width <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight), FontSize, brush)
        { MaxTextWidth = Math.Max(.01, width), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        context.DrawText(formatted, new(Math.Clamp(center.X - formatted.Width / 2, 0, Math.Max(0, Bounds.Width - formatted.Width)), Math.Clamp(center.Y - formatted.Height / 2, 0, Math.Max(0, Bounds.Height - formatted.Height))));
    }
    protected override void ClearProjection(Exception error) { _layout = []; _deltaLabels.Clear(); base.ClearProjection(error); }
}

/// <summary>Single-card convenience entry point. Delta is expressed in percentage points when DeltaType is Percentage; null Delta derives the change from Previous.</summary>
public sealed class KpiCard : KpiCardChart { public KpiCard() => Value = 0; }

/// <summary>Multi-category waffle using stable largest-remainder apportionment. Units have original category membership.</summary>
public sealed class WaffleChart : OperationalChartBase
{
    public static readonly StyledProperty<double?> ValueProperty = AvaloniaProperty.Register<WaffleChart, double?>(nameof(Value), validate: x => x is null || double.IsFinite(x.Value));
    public static readonly StyledProperty<double> MaxValueProperty = AvaloniaProperty.Register<WaffleChart, double>(nameof(MaxValue), 100, validate: x => double.IsFinite(x) && x > 0);
    public static readonly StyledProperty<IBrush?> FilledBrushProperty = AvaloniaProperty.Register<WaffleChart, IBrush?>(nameof(FilledBrush));
    public static readonly StyledProperty<IBrush?> EmptyBrushProperty = AvaloniaProperty.Register<WaffleChart, IBrush?>(nameof(EmptyBrush), Brush.Parse("#99A3AF"));
    public static readonly StyledProperty<int> RowsProperty = AvaloniaProperty.Register<WaffleChart, int>(nameof(Rows), 10, validate: x => x >= 1 && x <= 256);
    public static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<WaffleChart, int>(nameof(Columns), 10, validate: x => x >= 1 && x <= 256);
    public int Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public double? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double MaxValue { get => GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }
    public IBrush? FilledBrush { get => GetValue(FilledBrushProperty); set => SetValue(FilledBrushProperty, value); }
    public IBrush? EmptyBrush { get => GetValue(EmptyBrushProperty); set => SetValue(EmptyBrushProperty, value); }
    private bool IsScalar => ItemsSource is null && Value.HasValue;
    protected override ChartDataSnapshot CreateEngineeringSnapshot() => IsScalar ? ScalarSnapshot(Value!.Value) : base.CreateEngineeringSnapshot();
    private IReadOnlyList<OperationalAllocation> _allocation = [];
    public IReadOnlyList<OperationalAllocation> Allocation { get { _ = GetMarks(); return _allocation; } }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var values = data.Points.Select(p => new WafflePoint(Identity(p), IsScalar ? Value!.Value : Numeric(p, ValuePath), LabelOf(p))).ToArray(); var total = checked(Rows * Columns); if (IsScalar) { var share = Math.Clamp(Value!.Value, 0, MaxValue) / MaxValue; _allocation = new[] { new OperationalAllocation(0, values[0], (int)Math.Round(share * total, MidpointRounding.AwayFromZero), share) }; } else _allocation = ChartOperational.Waffle(values, total); var marks = new List<EngineeringMarkInfo>(); var ordinal = 0;
        foreach (var item in _allocation) { var firstMark = marks.Count; for (var unit = 0; unit < item.Units; unit++) { var x = ordinal % Columns / (double)Columns; var y = 1 - (ordinal / Columns + 1d) / Rows; var source = data.Points[item.SourceIndex]; marks.Add(Single(source, Cell(x, y, 1d / Columns, 1d / Rows, plot), IsScalar ? FilledBrush ?? BrushAt(0) : BrushAt(item.SourceIndex), Normalized(x + .5 / Columns, y + .5 / Rows, plot), (Identity(source), unit), item.Source.Value, $"{item.Source.Label}: {ValueText(item.Source.Value)} ({item.Share:P1}); {item.Units} cells")); ordinal++; } if (marks.Count > firstMark) DataLabel(marks[firstMark], $"{item.Source.Label} {item.Share:P0}", new(plot.Left + (item.SourceIndex + .5) * plot.Width / Math.Max(1, _allocation.Count), plot.Bottom + 12), plot.Width / Math.Max(1, _allocation.Count)); else AddLabel($"{item.Source.Label} {item.Share:P0}", new(plot.Left + (item.SourceIndex + .5) * plot.Width / Math.Max(1, _allocation.Count), plot.Bottom + 12), plot.Width / Math.Max(1, _allocation.Count)); }
        return marks;
    }
    protected override void RenderUnderlay(DrawingContext context, Rect plot)
    { if (!IsScalar) return; for (var i = 0; i < Rows * Columns; i++) context.DrawGeometry(EmptyBrush, null, Cell(i % Columns / (double)Columns, 1 - (i / Columns + 1d) / Rows, 1d / Columns, 1d / Rows, plot)); }
    protected override void ClearProjection(Exception error) { _allocation = []; base.ClearProjection(error); }
}

/// <summary>Integer legislative seat allocations; the sum of supplied parties defines the total.</summary>
public sealed class ParliamentChart : OperationalChartBase
{
    public static readonly StyledProperty<int> RowsProperty = AvaloniaProperty.Register<ParliamentChart, int>(nameof(Rows), 4, validate: x => x >= 1 && x <= 64);
    public static readonly StyledProperty<double> InnerRadiusFactorProperty = AvaloniaProperty.Register<ParliamentChart, double>(nameof(InnerRadiusFactor), .4, validate: x => double.IsFinite(x) && x > 0 && x <= 1);
    public ParliamentChart() => ValuePath = "Seats";
    public int Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public double InnerRadiusFactor { get => GetValue(InnerRadiusFactorProperty); set => SetValue(InnerRadiusFactorProperty, value); }
    private IReadOnlyList<OperationalSeat> _seats = [];
    public IReadOnlyList<OperationalSeat> Seats { get { _ = GetMarks(); return _seats; } }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var values = data.Points.Select(p => { var seats = Numeric(p, ValuePath); if (!double.IsFinite(seats) || seats < 0 || seats > ChartOperational.MaximumCells || seats != Math.Truncate(seats)) throw new ArgumentException("Seats must be bounded nonnegative integers."); return new ParliamentParty(Identity(p), (int)seats, LabelOf(p)); }).ToArray(); _seats = ChartOperational.Parliament(values, Rows, InnerRadiusFactor); var width = .9 * Math.Min(plot.Width, 2 * plot.Height); var area = new Rect(plot.Center.X - width / 2, plot.Center.Y - width / 4, width, width / 2); var marks = new List<EngineeringMarkInfo>();
        foreach (var seat in _seats) { var position = ApplyViewport(new(area.Left + seat.Position.X * area.Width, area.Top + seat.Position.Y * area.Height), plot); var source = data.Points[seat.SourceIndex]; marks.Add(Single(source, Disk(position, seat.Radius * area.Width * Zoom), BrushAt(seat.SourceIndex), position, (Identity(source), seat.SeatIndex), seat.Source.Seats, $"{seat.Source.Label}: {seat.Source.Seats} seats; #{seat.SeatIndex + 1}")); }
        var firstBySource = marks.GroupBy(mark => mark.Members[0].SourceIndex).ToDictionary(group => group.Key, group => group.First());
        foreach (var (value, i) in values.Select((v, i) => (v, i)))
        {
            var anchor = new Point(plot.Left + (i + .5) * plot.Width / Math.Max(1, values.Length), plot.Bottom + 12);
            if (firstBySource.TryGetValue(i, out var mark)) DataLabel(mark, $"{value.Label}: {value.Seats}", anchor, plot.Width / Math.Max(1, values.Length));
            else AddLabel($"{value.Label}: {value.Seats}", anchor, plot.Width / Math.Max(1, values.Length));
        }
        return marks;
    }
    protected override void ClearProjection(Exception error) { _seats = []; base.ClearProjection(error); }
}
