using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Two-category contingency chart; duplicate pairs aggregate and retain all original members.</summary>
public sealed class MosaicChart : OperationalChartBase
{
    public static readonly StyledProperty<string?> GroupPathProperty = AvaloniaProperty.Register<MosaicChart,string?>(nameof(GroupPath),"Column");
    public static readonly StyledProperty<string?> SubGroupPathProperty = AvaloniaProperty.Register<MosaicChart,string?>(nameof(SubGroupPath),"Row");
    private bool _syncingPaths;
    private MosaicLayout? _layout;
    public MosaicChart() { RowPath = "Row"; ColumnPath = "Column"; CellGap = 0; }
    public string? GroupPath { get=>GetValue(GroupPathProperty);set=>SetValue(GroupPathProperty,value); }
    public string? SubGroupPath { get=>GetValue(SubGroupPathProperty);set=>SetValue(SubGroupPathProperty,value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);if(_syncingPaths)return;_syncingPaths=true;
        try { if(change.Property==GroupPathProperty)SetCurrentValue(ColumnPathProperty,GroupPath);else if(change.Property==ColumnPathProperty)SetCurrentValue(GroupPathProperty,ColumnPath);else if(change.Property==SubGroupPathProperty)SetCurrentValue(RowPathProperty,SubGroupPath);else if(change.Property==RowPathProperty)SetCurrentValue(SubGroupPathProperty,RowPath); }
        finally{_syncingPaths=false;}
    }
    public MosaicLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var values = data.Points.Select(p => new MosaicObservation(Identity(p), Read(p.Item, ColumnPath)!, Read(p.Item, RowPath)!, Numeric(p, ValuePath), LabelOf(p))).ToArray();
        _layout = ChartMultivariate.Mosaic(values); var marks = new List<EngineeringMarkInfo>();
        foreach (var cell in _layout.Cells)
        {
            if (cell.Width <= 0 || cell.Height <= 0 || cell.Value <= 0) continue;
            var position = Normalized(cell.X + cell.Width / 2, cell.Y + cell.Height / 2, plot); var members = cell.SourceIndices.Select(i => Original(data.Points[i], values[i].Value)).ToArray();
            marks.Add(new(Array.AsReadOnly(members), Cell(cell.X, cell.Y, cell.Width, cell.Height, plot), BrushAt(cell.RowIndex), position) { Key = ("mosaic", cell.Column, cell.Row), Value = cell.Value, TooltipText = $"{cell.Column} / {cell.Row}: {ValueText(cell.Value)} ({cell.Share:P1}); {members.Length} original rows" });
            AddLabel($"{cell.Row}: {ValueText(cell.Value)}", position, Math.Max(1, cell.Width * plot.Width - CellGap - 4));
        }
        foreach (var group in _layout.Cells.GroupBy(c => c.ColumnIndex)) { var cell = group.First(); AddLabel(cell.Column.ToString(), new(plot.Left + (cell.X + cell.Width / 2) * plot.Width, plot.Bottom + 12), Math.Max(1, cell.Width * plot.Width)); }
        return marks;
    }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}

/// <summary>Observed numeric axis definition; null bounds infer a full-source domain, NaN values form gaps.</summary>
public sealed class ParallelAxis : INotifyPropertyChanged
{
    private object? _id; private string? _header, _valuePath; private double? _minimum, _maximum; private bool _reversed;
    public object? Id { get => _id; set => Set(ref _id, value); }
    public string? Header { get => _header; set => Set(ref _header, value); }
    public string? ValuePath { get => _valuePath; set => Set(ref _valuePath, value); }
    public double? Minimum { get => _minimum; set { if (value is { } v && !double.IsFinite(v)) throw new ArgumentOutOfRangeException(nameof(value)); Set(ref _minimum, value); } }
    public double? Maximum { get => _maximum; set { if (value is { } v && !double.IsFinite(v)) throw new ArgumentOutOfRangeException(nameof(value)); Set(ref _maximum, value); } }
    public bool IsReversed { get => _reversed; set => Set(ref _reversed, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new(name)); }
}

/// <summary>One selectable original multivariate row, with disconnected missing dimensions.</summary>
public sealed class ParallelCoordinatesChart : ComparisonChartBase
{
    public static readonly StyledProperty<ObservableCollection<ParallelAxis>> AxesProperty = AvaloniaProperty.Register<ParallelCoordinatesChart, ObservableCollection<ParallelAxis>>(nameof(Axes));
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<ParallelCoordinatesChart, double>(nameof(StrokeThickness), 2, validate: v => double.IsFinite(v) && v > 0 && v <= 64);
    public static readonly StyledProperty<string?> BrushPathProperty = AvaloniaProperty.Register<ParallelCoordinatesChart, string?>(nameof(BrushPath));
    private ParallelCoordinatesLayout? _layout;
    public ParallelCoordinatesChart() { Axes=[]; IdPath = "Id"; LabelPath = "Label"; ShowGrid = false; }
    public ObservableCollection<ParallelAxis> Axes { get => GetValue(AxesProperty); set => SetValue(AxesProperty, value ?? throw new ArgumentNullException(nameof(value))); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public string? BrushPath { get => GetValue(BrushPathProperty); set => SetValue(BrushPathProperty, value); }
    public ParallelCoordinatesLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { if (change.Property==AxesProperty) ObserveNested([]); base.OnPropertyChanged(change); }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var owners = new List<object?> { Axes }; var axes = new List<ParallelAxis>();
        try
        {
            foreach (var axis in Axes) { if (axes.Count == ChartMultivariate.MaximumDimensions || axis is null) throw new ArgumentOutOfRangeException(nameof(Axes)); axes.Add(axis); owners.Add(axis); }
            var dimensions = axes.Select((a,i) => new ParallelDimension(a.Id ?? a, a.Header ?? a.ValuePath ?? $"D{i+1}", a.Minimum, a.Maximum, a.IsReversed)).ToArray();
            if ((long)data.Count * axes.Count > ChartMultivariate.MaximumValues) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "Parallel value budget exceeded.");
            var values = new List<ParallelRecord>();
            // Invalid dimensions must retain every live values observer, so correcting an
            // observed collection can recover without an unrelated redraw or manual Refresh.
            foreach(var point in data.Points) if(point.Item is ParallelRecord typedOwner) owners.Add(typedOwner.Values);
            foreach (var source in data.Points)
            {
                var row = new double[axes.Count];
                for (var i = 0; i < axes.Count; i++)
                {
                    if (source.Item is ParallelRecord typed && string.IsNullOrWhiteSpace(axes[i].ValuePath)) { if(typed.Values is null || typed.Values.Count!=axes.Count) throw new ArgumentException("Typed parallel record dimensions must match the axes."); owners.Add(typed.Values); row[i] = typed.Values[i]; }
                    else { owners.AddRange(ChartDataAccessor.Owners(source.Item, axes[i].ValuePath)); row[i] = Number(source.Item, axes[i].ValuePath, out var v) ? v : double.NaN; }
                }
                owners.AddRange(ChartDataAccessor.Owners(source.Item, BrushPath)); values.Add(new(source.StableId ?? source.Item ?? source.SourceIndex, Array.AsReadOnly(row), ReadText(source.Item, LabelPath)));
            }
            _layout = ChartMultivariate.Parallel(values, dimensions); var marks = new List<EngineeringMarkInfo>();
            foreach (var row in _layout.Records)
            {
                if (row.Coordinates.Count == 0) continue; var source = data.Points[row.SourceIndex]; var projected = row.Coordinates.Select(p => Normalized(p.Position.X, p.Position.Y, plot)).ToArray(); var geometry = new GeometryGroup(); var radius = Math.Max(2, StrokeThickness / 2);
                for (var i = 0; i < projected.Length; i++) { geometry.Children.Add(new EllipseGeometry(new Rect(projected[i].X-radius,projected[i].Y-radius,radius*2,radius*2))); if (i>0 && row.Coordinates[i].DimensionIndex==row.Coordinates[i-1].DimensionIndex+1) geometry.Children.Add(Line([projected[i-1],projected[i]])); }
                var brush = Read(source.Item, BrushPath) switch { IBrush b => b, string text when Color.TryParse(text, out var c) => new SolidColorBrush(c), _ => BrushAt(row.SourceIndex) };
                marks.Add(new(Array.AsReadOnly(new[] { source with { Value = row.Source.Values, IsEmpty=false, WasEmpty=false } }), geometry, Brushes.Transparent, projected[0]) { Key = source.StableId ?? source.Item ?? source.SourceIndex, Stroke=brush, TooltipText = $"{row.Source.Label ?? row.Source.Id?.ToString()}: {string.Join(", ", row.Coordinates.Select(p => $"{dimensions[p.DimensionIndex].Header}={F(p.Value)}"))}" });
            }
            foreach (var (axis,i) in _layout.Dimensions.Select((a,i)=>(a,i))) { var x=i/(double)(_layout.Dimensions.Count-1); Decorations.Add((Line([Normalized(x,0,plot),Normalized(x,1,plot)]), Foreground,1)); Label(axis.Header,new(plot.Left+x*plot.Width,plot.Top-12),Math.Max(1,plot.Width/axes.Count)); NumberLabel(axis.IsReversed?axis.Minimum:axis.Maximum,new(plot.Left+x*plot.Width,plot.Top+10),60); NumberLabel(axis.IsReversed?axis.Maximum:axis.Minimum,new(plot.Left+x*plot.Width,plot.Bottom-10),60); }
            return marks;
        }
        finally { ObserveNested(owners); }
    }
    protected override bool ContainsMark(EngineeringMarkInfo mark, Point point)
    {
        if(mark.Geometry is not GeometryGroup group) return false;
        var tolerance=Math.Max(3,StrokeThickness/2);
        foreach(var child in group.Children)
        {
            if(child is EllipseGeometry ellipse && ellipse.Rect.Contains(point))
            { var r=ellipse.Rect;var dx=(point.X-r.Center.X)/Math.Max(1,r.Width/2);var dy=(point.Y-r.Center.Y)/Math.Max(1,r.Height/2);if(dx*dx+dy*dy<=1)return true; }
            if(child is PolylineGeometry line)
                for(var i=1;i<line.Points.Count;i++)
                {var a=line.Points[i-1];var b=line.Points[i];var dx=b.X-a.X;var dy=b.Y-a.Y;var len=dx*dx+dy*dy;var t=len==0?0:Math.Clamp(((point.X-a.X)*dx+(point.Y-a.Y)*dy)/len,0,1);var x=point.X-(a.X+t*dx);var y=point.Y-(a.Y+t*dy);if(x*x+y*y<=tolerance*tolerance)return true;}
        }
        return false;
    }
    protected override void RenderEngineeringMarks(DrawingContext context, IReadOnlyList<EngineeringMarkInfo> marks, EngineeringPointHit? selected, EngineeringPointHit? active)
    { for (var i=0;i<marks.Count;i++) context.DrawGeometry(null,new Pen(marks[i].Stroke ?? Foreground ?? Brushes.Gray, i==selected?.Index || i==active?.Index ? StrokeThickness+2 : StrokeThickness),marks[i].Geometry); }
    protected override void ClearProjection(Exception error) { _layout=null; base.ClearProjection(error); }
}
