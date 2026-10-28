using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Daily activity with explicit week start, missing-day decorations and exact original daily records. Dates are calendar dates without implicit local-time conversion.</summary>
public sealed class CalendarHeatmapChart : OperationalChartBase
{
    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty = AvaloniaProperty.Register<CalendarHeatmapChart, DayOfWeek>(nameof(FirstDayOfWeek), DayOfWeek.Monday, validate: Enum.IsDefined);
    public static readonly StyledProperty<DateOnly?> StartDateProperty = AvaloniaProperty.Register<CalendarHeatmapChart, DateOnly?>(nameof(StartDate));
    public static readonly StyledProperty<DateOnly?> EndDateProperty = AvaloniaProperty.Register<CalendarHeatmapChart, DateOnly?>(nameof(EndDate));
    public static readonly StyledProperty<IBrush?> EmptyCellBrushProperty = AvaloniaProperty.Register<CalendarHeatmapChart, IBrush?>(nameof(EmptyCellBrush), Brush.Parse("#70899AA9"));
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }
    public DateOnly? StartDate { get => GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }
    public DateOnly? EndDate { get => GetValue(EndDateProperty); set => SetValue(EndDateProperty, value); }
    public IBrush? EmptyCellBrush { get => GetValue(EmptyCellBrushProperty); set => SetValue(EmptyCellBrushProperty, value); }
    private OperationalCalendar? _calendar;
    public OperationalCalendar? Calendar { get { _ = GetMarks(); return _calendar; } }
    private static DateOnly Date(object? raw) => raw switch { DateOnly date => date, DateTime time => DateOnly.FromDateTime(time), DateTimeOffset offset => DateOnly.FromDateTime(offset.DateTime), _ => throw new ArgumentException("Calendar date must be DateOnly, DateTime or DateTimeOffset; no implicit timezone conversion occurs.") };
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var values = data.Points.Select(p => new CalendarPoint(Identity(p), Date(Read(p.Item, DatePath)), Numeric(p, ValuePath), LabelOf(p))).ToArray(); _calendar = ChartOperational.Calendar(values, FirstDayOfWeek, StartDate, EndDate); var actual = _calendar.Cells.Where(c => c.Source is not null).ToArray(); var minimum = actual.Select(x => x.Source!.Value).DefaultIfEmpty(0).Min(); var maximum = actual.Select(x => x.Source!.Value).DefaultIfEmpty(0).Max(); var marks = new List<EngineeringMarkInfo>();
        foreach (var cell in actual) { var source = data.Points[cell.SourceIndex!.Value]; var x = cell.Week / (double)_calendar.Weeks; var y = cell.Day / 7d; marks.Add(Single(source, Cell(x, y, 1d / _calendar.Weeks, 1d / 7, plot), ColorAt(cell.Source!.Value, minimum, maximum), Normalized(x + .5 / _calendar.Weeks, y + .5 / 7, plot), Identity(source), cell.Source.Value, $"{cell.Date:yyyy-MM-dd}: {ValueText(cell.Source.Value)}")); }
        for (var day = 0; day < 7; day++) AddLabel(CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[((int)FirstDayOfWeek + day) % 7], new(plot.Left - 15, plot.Top + (day + .5) * plot.Height / 7), 28);
        foreach (var cell in _calendar.Cells.Where(x => x.Date.Day == 1 || x.Date == _calendar.Start)) AddLabel(cell.Date.ToString("MMM", CultureInfo.CurrentCulture), new(plot.Left + (cell.Week + .5) * plot.Width / _calendar.Weeks, plot.Top - 11), Math.Max(1, plot.Width / _calendar.Weeks * 4));
        AddLabel($"{ValueText(minimum)} → {ValueText(maximum)}", new(plot.Center.X, plot.Bottom + 12), plot.Width); return marks;
    }
    protected override Rect GetPlotArea(Size size) { var area = base.GetPlotArea(size); var left = Math.Min(14, area.Width); return new(area.Left + left, area.Top, Math.Max(0, area.Width - left), area.Height); }
    protected override void RenderUnderlay(DrawingContext context, Rect plot)
    { if (_calendar is null) return; foreach (var cell in _calendar.Cells.Where(x => x.Source is null)) context.DrawGeometry(EmptyCellBrush, null, Cell(cell.Week / (double)_calendar.Weeks, cell.Day / 7d, 1d / _calendar.Weeks, 1d / 7, plot)); }
    protected override void ClearProjection(Exception error) { _calendar = null; base.ClearProjection(error); }
}

/// <summary>Source-preserving categorical grid. Duplicate pairs are rejected; missing combinations stay decorative.</summary>
public abstract class OperationalMatrixChartBase : OperationalChartBase
{
    protected OperationalMatrix? MatrixProjection;
    public OperationalMatrix? Matrix { get { _ = GetMarks(); return MatrixProjection; } }
    protected abstract bool IsTable { get; }
    private IReadOnlyList<EngineeringMarkInfo>? _textMarks;
    private readonly Dictionary<Point, IBrush> _cellTextBrushes = [];
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    {
        var marks = GetMarks();
        if (!ReferenceEquals(marks, _textMarks))
        {
            _textMarks = marks; _cellTextBrushes.Clear();
            foreach (var mark in marks)
                if (mark.Fill is ISolidColorBrush fill) _cellTextBrushes[mark.Position] = CellTextBrush(fill);
        }
        for (var ordinal = 0; ordinal < Labels.Count; ordinal++)
        {
            if (IsTemplatedDataLabel(ordinal)) continue;
            var label = Labels[ordinal];
            if (!_cellTextBrushes.TryGetValue(label.Position, out var brush)) { DrawLabel(context, label.Text, label.Position, label.Width); continue; }
            if (label.Width <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0) continue;
            var text = new FormattedText(label.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight), FontSize, brush)
            { MaxTextWidth = Math.Max(.01, label.Width), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
            context.DrawText(text, new(Math.Clamp(label.Position.X - text.Width / 2, 0, Math.Max(0, Bounds.Width - text.Width)),
                Math.Clamp(label.Position.Y - text.Height / 2, 0, Math.Max(0, Bounds.Height - text.Height))));
        }
    }
    private IBrush CellTextBrush(ISolidColorBrush fill)
    {
        // Composite solid fills before choosing the greater black/white contrast ratio.
        var lightForeground = Foreground is ISolidColorBrush foreground &&
            .2126 * foreground.Color.R + .7152 * foreground.Color.G + .0722 * foreground.Color.B > 128;
        var canvas = lightForeground ? Color.Parse("#111722") : Colors.White;
        static double Blend(byte front, double back, double alpha) => front / 255d * alpha + back * (1 - alpha);
        var r = canvas.R / 255d; var g = canvas.G / 255d; var b = canvas.B / 255d;
        if (Background is ISolidColorBrush background)
        {
            var a = background.Color.A / 255d * background.Opacity;
            r = Blend(background.Color.R, r, a); g = Blend(background.Color.G, g, a); b = Blend(background.Color.B, b, a);
        }
        var opacity = fill.Color.A / 255d * fill.Opacity;
        r = Blend(fill.Color.R, r, opacity); g = Blend(fill.Color.G, g, opacity); b = Blend(fill.Color.B, b, opacity);
        static double Linear(double value) => value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        var luminance = .2126 * Linear(r) + .7152 * Linear(g) + .0722 * Linear(b);
        return (luminance + .05) / .05 >= 1.05 / (luminance + .05) ? Brushes.Black : Brushes.White;
    }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var values = data.Points.Select(p => new MatrixPoint(Identity(p), Read(p.Item, RowPath)!, Read(p.Item, ColumnPath)!, Read(p.Item, ValuePath) is bool flag ? flag ? 1 : 0 : Numeric(p, ValuePath), ReadText(p.Item, TextPath), LabelOf(p))).ToArray(); MatrixProjection = ChartOperational.Matrix(values); var marks = new List<EngineeringMarkInfo>(); var rows = Math.Max(1, MatrixProjection.Rows.Count); var columns = Math.Max(1, MatrixProjection.Columns.Count); var minimum = values.Select(x => x.Value).DefaultIfEmpty(0).Min(); var maximum = values.Select(x => x.Value).DefaultIfEmpty(0).Max();
        foreach (var cell in MatrixProjection.Cells) { var source = data.Points[cell.SourceIndex]; var x = cell.Column / (double)columns; var y = cell.Row / (double)rows; var position = Normalized(x + .5 / columns, y + .5 / rows, plot); var geometry = IsTable ? Cell(x, y, 1d / columns, 1d / rows, plot) : Disk(position, Math.Max(.1, Math.Min(plot.Width / columns, plot.Height / rows) * .32 - CellGap / 2) * Zoom); marks.Add(Single(source, geometry, ColorAt(cell.Source.Value, minimum, maximum), position, Identity(source), cell.Source.Value, $"{cell.Source.Row} / {cell.Source.Column}: {cell.Source.Text ?? ValueText(cell.Source.Value)}")); if (IsTable) { if (ShowLabels) DataLabel(marks[^1], cell.Source.Text ?? ValueText(cell.Source.Value), position, Math.Max(1, plot.Width / columns - CellGap - 4)); } else if (ShowValues && plot.Width / columns > 36 && plot.Height / rows > 24) DataNumberLabel(marks[^1], cell.Source.Value, position, Math.Max(1, plot.Width / columns - 4)); }
        for (var i = 0; i < MatrixProjection.Rows.Count; i++) AddLabel(MatrixProjection.Rows[i].ToString(), new(plot.Left - 36, plot.Top + (i + .5) * plot.Height / rows), 68); for (var i = 0; i < MatrixProjection.Columns.Count; i++) AddLabel(MatrixProjection.Columns[i].ToString(), new(plot.Left + (i + .5) * plot.Width / columns, plot.Top - 12), Math.Max(1, plot.Width / columns - 4)); AddLabel($"{ValueText(minimum)} → {ValueText(maximum)}", new(plot.Center.X, plot.Bottom + 12), plot.Width); return marks;
    }
    protected override Rect GetPlotArea(Size size) { var area = base.GetPlotArea(size); var left = Math.Min(54, area.Width); return new(area.Left + left, area.Top, Math.Max(0, area.Width - left), area.Height); }
    protected override void ClearProjection(Exception error) { MatrixProjection = null; base.ClearProjection(error); }
}
/// <summary>Numeric or boolean-as-0/1 relationship matrix, with real zero-valued cells preserved.</summary>
public sealed class MatrixChart : OperationalMatrixChartBase { protected override bool IsTable => false; }
/// <summary>Read-only painted conditional table; editing, sorting controls and virtualized grid ownership belong to a separate table component.</summary>
public sealed partial class TableChart : OperationalMatrixChartBase { protected override bool IsTable => true; }
