using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>A discrete matrix with typed category keys and a shared linear color domain.</summary>
/// <remarks>Rows and columns keep first-seen source order. The last source item in a cell wins,
/// including an invalid last value. Missing matrix combinations do not create source points.</remarks>
public sealed class HeatmapChart : AnalysisChartBase
{
    public static readonly StyledProperty<string?> RowPathProperty = AvaloniaProperty.Register<HeatmapChart, string?>(nameof(RowPath));
    public static readonly StyledProperty<string?> ColumnPathProperty = AvaloniaProperty.Register<HeatmapChart, string?>(nameof(ColumnPath));
    public static readonly StyledProperty<Color> LowColorProperty = AvaloniaProperty.Register<HeatmapChart, Color>(nameof(LowColor), Color.Parse("#EFF6FF"));
    public static readonly StyledProperty<Color> HighColorProperty = AvaloniaProperty.Register<HeatmapChart, Color>(nameof(HighColor), Color.Parse("#1D4ED8"));
    public static readonly StyledProperty<double?> MinimumProperty = AvaloniaProperty.Register<HeatmapChart, double?>(nameof(Minimum), validate: v => v is null || double.IsFinite(v.Value));
    public static readonly StyledProperty<double?> MaximumProperty = AvaloniaProperty.Register<HeatmapChart, double?>(nameof(Maximum), validate: v => v is null || double.IsFinite(v.Value));
    public static readonly StyledProperty<double> CellGapProperty = AvaloniaProperty.Register<HeatmapChart, double>(nameof(CellGap), 2, validate: v => double.IsFinite(v) && v >= 0);
    public static readonly StyledProperty<bool> ShowAxesProperty = AvaloniaProperty.Register<HeatmapChart, bool>(nameof(ShowAxes), true);
    public static readonly StyledProperty<bool> ShowColorScaleProperty = AvaloniaProperty.Register<HeatmapChart, bool>(nameof(ShowColorScale), true);
    public static readonly StyledProperty<IBrush?> MissingBrushProperty = AvaloniaProperty.Register<HeatmapChart, IBrush?>(nameof(MissingBrush));
    private IReadOnlyList<object> _rowKeys = Array.Empty<object>(), _columnKeys = Array.Empty<object>();
    private double? _effectiveMinimum, _effectiveMaximum;
    private bool _isColorDomainValid;
    private Rect _matrixPlot;

    public string? RowPath { get => GetValue(RowPathProperty); set => SetValue(RowPathProperty, value); }
    public string? ColumnPath { get => GetValue(ColumnPathProperty); set => SetValue(ColumnPathProperty, value); }
    public Color LowColor { get => GetValue(LowColorProperty); set => SetValue(LowColorProperty, value); }
    public Color HighColor { get => GetValue(HighColorProperty); set => SetValue(HighColorProperty, value); }
    /// <summary>Optional finite lower color bound. Reversed or equal explicit bounds hide colored cells.</summary>
    public double? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double CellGap { get => GetValue(CellGapProperty); set => SetValue(CellGapProperty, value); }
    public bool ShowAxes { get => GetValue(ShowAxesProperty); set => SetValue(ShowAxesProperty, value); }
    public bool ShowColorScale { get => GetValue(ShowColorScaleProperty); set => SetValue(ShowColorScaleProperty, value); }
    /// <summary>Optional fill for an invalid, explicitly supplied cell. Absent combinations stay empty.</summary>
    public IBrush? MissingBrush { get => GetValue(MissingBrushProperty); set => SetValue(MissingBrushProperty, value); }
    public IReadOnlyList<object> RowKeys { get { _ = GetMarks(); return _rowKeys; } }
    public IReadOnlyList<object> ColumnKeys { get { _ = GetMarks(); return _columnKeys; } }
    public double? EffectiveMinimum { get { _ = GetMarks(); return _effectiveMinimum; } }
    public double? EffectiveMaximum { get { _ = GetMarks(); return _effectiveMaximum; } }
    public bool IsColorDomainValid { get { _ = GetMarks(); return _isColorDomainValid; } }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowPathProperty || change.Property == ColumnPathProperty)
        {
            Series.ExtraPaths = new[] { RowPath, ColumnPath };
            Series.Refresh();
        }
    }

    protected override Rect GetPlotArea(Size size)
    {
        var available = base.GetPlotArea(size);
        var left = ShowAxes ? Math.Min(72, available.Width * .2) : 0;
        var bottom = ShowAxes ? Math.Min(32, available.Height * .15) : 0;
        var right = ShowColorScale ? Math.Min(70, available.Width * .2) : 0;
        return new Rect(available.Left + left, available.Top, Math.Max(0, available.Width - left - right), Math.Max(0, available.Height - bottom));
    }

    protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        _matrixPlot = plot;
        var rows = new List<object>(); var columns = new List<object>();
        var rowOrdinals = new Dictionary<TypedKey, int>(); var columnOrdinals = new Dictionary<TypedKey, int>();
        var cells = new Dictionary<(int Row, int Column), ChartDataPoint>();
        foreach (var point in data.Points)
        {
            if (!TryKey(point.Item, RowPath, out var row) || !TryKey(point.Item, ColumnPath, out var column)) continue;
            var r = Ordinal(row, rowOrdinals, rows); var c = Ordinal(column, columnOrdinals, columns);
            cells[(r, c)] = point;
        }
        _rowKeys = Array.AsReadOnly(rows.ToArray()); _columnKeys = Array.AsReadOnly(columns.ToArray());
        var finite = cells.Values.Where(p => !p.IsEmpty && double.IsFinite(p.Point.Y)).Select(p => p.Point.Y).ToArray();
        _effectiveMinimum = Minimum ?? (finite.Length > 0 ? finite.Min() : (double?)null);
        _effectiveMaximum = Maximum ?? (finite.Length > 0 ? finite.Max() : (double?)null);
        _isColorDomainValid = _effectiveMinimum is { } min && _effectiveMaximum is { } max
            && (max > min || max == min && Minimum is null && Maximum is null);
        if (rows.Count == 0 || columns.Count == 0 || plot.Width <= 0 || plot.Height <= 0) return Array.Empty<AnalysisMarkInfo>();
        var cellWidth = plot.Width / columns.Count; var cellHeight = plot.Height / rows.Count;
        var gap = Math.Min(CellGap, Math.Min(cellWidth, cellHeight));
        var marks = new List<AnalysisMarkInfo>(cells.Count);
        // Source order, rather than dictionary traversal order, also determines keyboard traversal.
        foreach (var cell in cells.OrderBy(pair => pair.Value.SourceIndex))
        {
            var point = cell.Value; var isNumeric = !point.IsEmpty && double.IsFinite(point.Point.Y);
            IBrush? fill = isNumeric ? _isColorDomainValid ? new SolidColorBrush(ColorAt(point.Point.Y)) : null : MissingBrush;
            if (fill is null || cellWidth <= gap || cellHeight <= gap) continue;
            var bounds = new Rect(plot.Left + cell.Key.Column * cellWidth + gap / 2, plot.Top + cell.Key.Row * cellHeight + gap / 2, cellWidth - gap, cellHeight - gap);
            marks.Add(new AnalysisMarkInfo(point, new RectangleGeometry(bounds), fill, bounds.Center)
            {
                Label = isNumeric && ShowValues ? FormatNumber(point.Point.Y) : string.Empty,
                TooltipText = $"{KeyLabel(rows[cell.Key.Row])} / {KeyLabel(columns[cell.Key.Column])}: {(isNumeric ? FormatNumber(point.Point.Y) : "—")}",
                LabelBrush = fill is ISolidColorBrush solid && .2126 * solid.Color.R + .7152 * solid.Color.G + .0722 * solid.Color.B < 140 ? Brushes.White : Brushes.Black
            });
        }
        return marks.AsReadOnly();
    }

    protected override void DrawDecorations(DrawingContext context, IReadOnlyList<AnalysisMarkInfo> marks)
    {
        var plot = _matrixPlot;
        if (plot.Width <= 0 || plot.Height <= 0) return;
        if (ShowAxes)
        {
            for (var r = 0; r < _rowKeys.Count; r++) DrawLabel(context, KeyLabel(_rowKeys[r]), new Point(plot.Left - 37, plot.Top + (r + .5) * plot.Height / _rowKeys.Count), 68);
            for (var c = 0; c < _columnKeys.Count; c++) DrawLabel(context, KeyLabel(_columnKeys[c]), new Point(plot.Left + (c + .5) * plot.Width / _columnKeys.Count, plot.Bottom + 15), Math.Max(0, plot.Width / _columnKeys.Count - 2));
        }
        if (!ShowColorScale || !_isColorDomainValid) return;
        var scale = new Rect(plot.Right + 12, plot.Top, 12, plot.Height);
        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops { new(HighColor, 0), new(LowColor, 1) }
        };
        context.DrawRectangle(_effectiveMaximum == _effectiveMinimum ? new SolidColorBrush(ColorAt(_effectiveMinimum!.Value)) : gradient, null, scale);
        DrawLabel(context, FormatNumber(_effectiveMaximum!.Value), new Point(scale.Right + 22, plot.Top + FontSize / 2), 40);
        DrawLabel(context, FormatNumber(_effectiveMinimum!.Value), new Point(scale.Right + 22, plot.Bottom - FontSize / 2), 40);
    }

    private Color ColorAt(double value)
    {
        var minimum = _effectiveMinimum!.Value; var maximum = _effectiveMaximum!.Value;
        var fraction = .5;
        if (maximum > minimum)
        {
            if (value <= minimum) fraction = 0;
            else if (value >= maximum) fraction = 1;
            else
            {
                var range = maximum - minimum;
                var magnitude = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
                fraction = double.IsFinite(range) ? (value - minimum) / range : (value / magnitude - minimum / magnitude) / (maximum / magnitude - minimum / magnitude);
            }
        }
        static byte Blend(byte from, byte to, double amount) => (byte)Math.Clamp(Math.Round(from + (to - from) * amount, MidpointRounding.AwayFromZero), 0, 255);
        return Color.FromArgb(Blend(LowColor.A, HighColor.A, fraction), Blend(LowColor.R, HighColor.R, fraction), Blend(LowColor.G, HighColor.G, fraction), Blend(LowColor.B, HighColor.B, fraction));
    }

    private string FormatNumber(double number)
    {
        try { return number.ToString(ValueFormat, CultureInfo.InvariantCulture); }
        catch (FormatException) { return number.ToString("G4", CultureInfo.InvariantCulture); }
    }
    private static string KeyLabel(object key) => Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
    private static bool TryKey(object? item, string? path, out object key)
    {
        key = null!;
        if (string.IsNullOrWhiteSpace(path) || !ChartDataAccessor.TryRead(item, path, out var value) || value is null) return false;
        key = value; return true;
    }
    private static int Ordinal(object value, Dictionary<TypedKey, int> lookup, List<object> keys)
    {
        var key = new TypedKey(value.GetType(), value);
        if (lookup.TryGetValue(key, out var ordinal)) return ordinal;
        lookup.Add(key, ordinal = keys.Count); keys.Add(value); return ordinal;
    }
    private readonly record struct TypedKey(Type Type, object Value);
}
