using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>One read-only conditional-table column. Stable Id is optional; its object identity is used when omitted.</summary>
public sealed class OperationalTableColumn : INotifyPropertyChanged
{
    private object? _id; private string? _header, _valuePath, _format; private double? _minimum, _maximum; private bool _color = true;
    public OperationalTableColumn() { }
    public OperationalTableColumn(string header, string valuePath) { _header = header; _valuePath = valuePath; }
    public object? Id { get => _id; set => Set(ref _id, value); }
    public string? Header { get => _header; set => Set(ref _header, value); }
    public string? ValuePath { get => _valuePath; set => Set(ref _valuePath, value); }
    public string? Format { get => _format; set => Set(ref _format, value); }
    public double? Minimum { get => _minimum; set { if (value is { } v && !double.IsFinite(v)) throw new ArgumentOutOfRangeException(nameof(value)); Set(ref _minimum, value); } }
    public double? Maximum { get => _maximum; set { if (value is { } v && !double.IsFinite(v)) throw new ArgumentOutOfRangeException(nameof(value)); Set(ref _maximum, value); } }
    public bool UseColorScale { get => _color; set => Set(ref _color, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new(name)); }
}
/// <summary>Dictionary-backed table row; column ValuePath is an exact dictionary key for this DTO. Other business DTOs use ordinary nested property paths.</summary>
public sealed record OperationalTableRow(object Id, IReadOnlyDictionary<string, object?> Values, string? Label = null);
public sealed record OperationalTableCell(int SourceIndex, int ColumnIndex, object ColumnId, string Header, object? Source, object? RawValue, double? NumericValue, string DisplayText, double ColorMinimum, double ColorMaximum);

public sealed partial class TableChart
{
    public static readonly StyledProperty<IEnumerable<OperationalTableColumn>?> ColumnsProperty = AvaloniaProperty.Register<TableChart, IEnumerable<OperationalTableColumn>?>(nameof(Columns));
    /// <summary>Non-null columns enable business-row mode; null retains flat MatrixPoint mode. Editing and virtualization are owned by a separate table component.</summary>
    public IEnumerable<OperationalTableColumn>? Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    private IReadOnlyList<OperationalTableCell> _cells = [];
    public IReadOnlyList<OperationalTableCell> Cells { get { _ = GetMarks(); return _cells; } }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { if (change.Property == ColumnsProperty) ObserveNested([]); base.OnPropertyChanged(change); }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        if (Columns is null) { _cells = []; return base.BuildMarks(data, plot); }
        BeginScene(); MatrixProjection = null; var owners = new List<object?> { Columns }; var columns = new List<OperationalTableColumn>(); var ids = new HashSet<object>(); var rowIds = new HashSet<object>(); var cells = new List<OperationalTableCell>(); var marks = new List<EngineeringMarkInfo>();
        try
        {
            foreach (var column in Columns) { if (column is null || columns.Count == 256) throw new ArgumentOutOfRangeException(nameof(Columns), "Table column budget exceeded."); if (string.IsNullOrWhiteSpace(column.ValuePath) || column.ValuePath.Length > 256 || column.Header?.Length > 512) throw new ArgumentException("Columns require a property path of at most 256 characters and a header of at most 512 characters."); if (!ids.Add(column.Id ?? column)) throw new ArgumentException("Table column IDs must be unique."); owners.Add(column); columns.Add(column); }
            if ((long)columns.Count * data.Count > ChartOperational.MaximumCells) throw new ArgumentOutOfRangeException(nameof(Columns), "Table cell budget exceeded.");
            foreach (var source in data.Points) if (!rowIds.Add(Identity(source))) throw new ArgumentException("Table row IDs must be unique.");
            var raw = new object?[columns.Count, data.Count]; var numbers = new double?[columns.Count, data.Count];
            for (var c = 0; c < columns.Count; c++) for (var r = 0; r < data.Count; r++) { var source = data.Points[r]; var column = columns[c]; if (source.Item is OperationalTableRow typed) { if (typed.Values is null) throw new ArgumentException("Table row Values cannot be null."); typed.Values.TryGetValue(column.ValuePath!, out raw[c, r]); owners.Add(typed.Values); } else { raw[c, r] = Read(source.Item, column.ValuePath); owners.AddRange(ChartDataAccessor.Owners(source.Item, column.ValuePath)); } var value = raw[c, r]; if (value is bool flag) numbers[c, r] = flag ? 1 : 0; else if (ChartDataAccessor.TryNumber(value, out var number)) numbers[c, r] = number; else if (value is double or float or decimal or int or long or short or uint or ulong or ushort or byte or sbyte) throw new ArgumentException("Numeric table values must be finite."); }
            for (var c = 0; c < columns.Count; c++)
            {
                var column = columns[c]; var numeric = Enumerable.Range(0, data.Count).Select(r => numbers[c, r]).Where(x => x.HasValue).Select(x => x!.Value).ToArray(); var min = column.Minimum ?? numeric.DefaultIfEmpty(0).Min(); var max = column.Maximum ?? numeric.DefaultIfEmpty(0).Max(); if (min > max) throw new ArgumentException("Table color minimum cannot exceed maximum.");
                for (var r = 0; r < data.Count; r++)
                {
                    var source = data.Points[r]; var value = raw[c, r]; var number = numbers[c, r]; var text = value?.ToString() ?? "—"; if (value is IFormattable formatted && column.Format is { } format) { try { text = formatted.ToString(format, CultureInfo.CurrentCulture) ?? "—"; } catch (FormatException) { text = value.ToString() ?? "—"; } }
                    var cell = new OperationalTableCell(source.SourceIndex, c, column.Id ?? column, column.Header ?? column.ValuePath!, source.Item, value, number, text, min, max); cells.Add(cell); var x = c / (double)Math.Max(1, columns.Count); var y = r / (double)Math.Max(1, data.Count); var position = Normalized(x + .5 / Math.Max(1, columns.Count), y + .5 / Math.Max(1, data.Count), plot); var member = source with { Value = value, Point = new(source.SourceIndex, number ?? 0), IsEmpty = value is null, WasEmpty = value is null };
                    marks.Add(new(Array.AsReadOnly(new[] { member }), Cell(x, y, 1d / Math.Max(1, columns.Count), 1d / Math.Max(1, data.Count), plot), column.UseColorScale && number is { } n ? ColorAt(n, min, max) : Brush.Parse("#20899AA9"), position) { Key = ("table", Identity(source), cell.ColumnId), Value = number ?? 0, TooltipText = $"{LabelOf(source)} / {cell.Header}: {text}" }); AddLabel(text, position, Math.Max(1, plot.Width / Math.Max(1, columns.Count) - CellGap - 4));
                }
                AddLabel(column.Header ?? column.ValuePath, new(plot.Left + (c + .5) * plot.Width / Math.Max(1, columns.Count), plot.Top - 12), Math.Max(1, plot.Width / Math.Max(1, columns.Count) - 4));
            }
            for (var r = 0; r < data.Count; r++) AddLabel(LabelOf(data.Points[r]), new(plot.Left - 36, plot.Top + (r + .5) * plot.Height / Math.Max(1, data.Count)), 68);
            _cells = cells.AsReadOnly(); return marks;
        }
        finally { ObserveNested(owners); }
    }
    protected override void ClearProjection(Exception error) { _cells = []; base.ClearProjection(error); }
}
