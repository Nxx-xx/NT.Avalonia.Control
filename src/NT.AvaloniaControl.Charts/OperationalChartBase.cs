using System.Collections;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Common observed DTO paths and original-source interaction for operational charts.</summary>
public abstract class OperationalChartBase : ComparisonChartBase
{
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(ValuePath), "Value");
    public static readonly StyledProperty<string?> SecondaryPathProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(SecondaryPath), "Previous");
    public static readonly StyledProperty<string?> DatePathProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(DatePath), "Date");
    public static readonly StyledProperty<string?> RowPathProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(RowPath), "Row");
    public static readonly StyledProperty<string?> ColumnPathProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(ColumnPath), "Column");
    public static readonly StyledProperty<string?> EntityPathProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(EntityPath), "Entity");
    public static readonly StyledProperty<string?> PeriodPathProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(PeriodPath), "Period");
    public static readonly StyledProperty<string?> TextPathProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(TextPath), "Text");
    public static readonly StyledProperty<string?> RangesPathProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(RangesPath), "Ranges");
    public static readonly StyledProperty<string?> TrendPathProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(TrendPath), "Trend");
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<OperationalChartBase, string?>(nameof(Label));
    public static readonly StyledProperty<double> CellGapProperty = AvaloniaProperty.Register<OperationalChartBase, double>(nameof(CellGap), 2, validate: x => double.IsFinite(x) && x >= 0 && x <= 1024);
    public static readonly StyledProperty<Color> MinimumColorProperty = AvaloniaProperty.Register<OperationalChartBase, Color>(nameof(MinimumColor), Color.Parse("#D7E9F5"));
    public static readonly StyledProperty<Color> MaximumColorProperty = AvaloniaProperty.Register<OperationalChartBase, Color>(nameof(MaximumColor), Color.Parse("#2563EB"));
    protected OperationalChartBase() { IdPath = "Id"; LabelPath = "Label"; ShowGrid = false; ObserveOperationalPaths(); }
    private int _nestedNumericCount;
    protected override int SourceItemLimit => ChartOperational.MaximumCount;
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public string? SecondaryPath { get => GetValue(SecondaryPathProperty); set => SetValue(SecondaryPathProperty, value); }
    public string? DatePath { get => GetValue(DatePathProperty); set => SetValue(DatePathProperty, value); }
    public string? RowPath { get => GetValue(RowPathProperty); set => SetValue(RowPathProperty, value); }
    public string? ColumnPath { get => GetValue(ColumnPathProperty); set => SetValue(ColumnPathProperty, value); }
    public string? EntityPath { get => GetValue(EntityPathProperty); set => SetValue(EntityPathProperty, value); }
    public string? PeriodPath { get => GetValue(PeriodPathProperty); set => SetValue(PeriodPathProperty, value); }
    public string? TextPath { get => GetValue(TextPathProperty); set => SetValue(TextPathProperty, value); }
    public string? RangesPath { get => GetValue(RangesPathProperty); set => SetValue(RangesPathProperty, value); }
    public string? TrendPath { get => GetValue(TrendPathProperty); set => SetValue(TrendPathProperty, value); }
    public new string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double CellGap { get => GetValue(CellGapProperty); set => SetValue(CellGapProperty, value); }
    public Color MinimumColor { get => GetValue(MinimumColorProperty); set => SetValue(MinimumColorProperty, value); }
    public Color MaximumColor { get => GetValue(MaximumColorProperty); set => SetValue(MaximumColorProperty, value); }
    private void ObserveOperationalPaths() => ObservePaths(ValuePath, SecondaryPath, DatePath, RowPath, ColumnPath, EntityPath, PeriodPath, TextPath, RangesPath, TrendPath, "Minimum", "Maximum");
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == ValuePathProperty || change.Property == SecondaryPathProperty || change.Property == DatePathProperty || change.Property == RowPathProperty || change.Property == ColumnPathProperty || change.Property == EntityPathProperty || change.Property == PeriodPathProperty || change.Property == TextPathProperty || change.Property == RangesPathProperty || change.Property == TrendPathProperty) ObserveOperationalPaths(); }
    protected object Identity(ChartDataPoint source) => source.StableId ?? source.Item ?? source.SourceIndex;
    protected string? LabelOf(ChartDataPoint source) => ReadText(source.Item, LabelPath) ?? source.Category?.ToString();
    protected double Numeric(ChartDataPoint source, string? path) => Number(source.Item, path, out var value) ? value : double.NaN;
    protected ChartDataPoint Original(ChartDataPoint source, double value, double weight = 1) => source with { Point = new(source.SourceIndex, value), Value = value, Weight = weight, IsEmpty = false, WasEmpty = false };
    /// <summary>Scalar properties belong to this real control, rather than an invented business item.</summary>
    protected ChartDataSnapshot ScalarSnapshot(double value) => new(0, new[] { new ChartDataPoint(0, this, this, Label ?? Title, new(0, value), false) { Value = value } });
    protected void BeginOperationalScene() { BeginScene(); _nestedNumericCount = 0; }
    protected void AddLabel(string? text, Point position, double width) => base.Label(text, position, width);
    protected IReadOnlyList<double>? Numbers(object? item, string? path, List<object?> owners)
    {
        var raw = Read(item, path); if (raw is null) return null; owners.Add(raw); if (raw is not IEnumerable source || raw is string) throw new ArgumentException("Nested numeric data must be a sequence."); var values = new List<double>(); foreach (var value in source) { if (_nestedNumericCount == ChartOperational.MaximumCount) throw new ArgumentOutOfRangeException(nameof(item), "Combined nested numeric observation budget exceeded."); _nestedNumericCount++; values.Add(ChartDataAccessor.TryNumber(value, out var number) ? number : double.NaN); } return values.AsReadOnly();
    }
    protected IBrush ColorAt(double value, double minimum, double maximum)
    {
        var fraction = Math.Clamp(Fraction(value, minimum, maximum), 0, 1); byte Mix(byte a, byte b) => (byte)Math.Clamp(Math.Round(a * (1 - fraction) + b * fraction), 0, 255); var a = MinimumColor; var b = MaximumColor; return new SolidColorBrush(Color.FromArgb(Mix(a.A, b.A), Mix(a.R, b.R), Mix(a.G, b.G), Mix(a.B, b.B)));
    }
    protected Geometry Cell(double x, double y, double width, double height, Rect plot)
    { var dx = plot.Width <= 0 ? 0 : Math.Min(width, CellGap / plot.Width); var dy = plot.Height <= 0 ? 0 : Math.Min(height, CellGap / plot.Height); return Rectangle(x + dx / 2, y + dy / 2, width - dx, height - dy, plot); }
    protected EngineeringMarkInfo Single(ChartDataPoint source, Geometry geometry, IBrush brush, Point position, object key, double value, string tooltip) => Mark(Original(source, value), geometry, brush, position, key, value, tooltip);
    protected Geometry Disk(Point center, double radius) => new EllipseGeometry(new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius));
}
