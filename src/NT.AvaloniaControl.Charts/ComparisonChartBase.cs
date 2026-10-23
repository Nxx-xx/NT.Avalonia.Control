using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Advanced comparison glyphs with separate decorations and original parent/child membership.</summary>
public abstract class ComparisonChartBase : EngineeringChartBase
{
    public static readonly StyledProperty<bool> ShowLabelsProperty = AvaloniaProperty.Register<ComparisonChartBase, bool>(nameof(ShowLabels), true);
    public static readonly StyledProperty<bool> ShowValuesProperty = AvaloniaProperty.Register<ComparisonChartBase, bool>(nameof(ShowValues), true);
    public static readonly StyledProperty<string?> ValueFormatProperty = AvaloniaProperty.Register<ComparisonChartBase, string?>(nameof(ValueFormat), "0.#");
    public static readonly StyledProperty<ComparisonSort> SortProperty = AvaloniaProperty.Register<ComparisonChartBase, ComparisonSort>(nameof(Sort), ComparisonSort.Source, validate: Enum.IsDefined);
    private readonly HashSet<object> _nested = new(ReferenceEqualityComparer.Instance);
    private bool _observing;
    protected readonly List<(string Text, Point Position, double Width)> Labels = [];
    protected readonly List<(Geometry Geometry, IBrush? Brush, double Thickness)> Decorations = [];
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    public bool ShowValues { get => GetValue(ShowValuesProperty); set => SetValue(ShowValuesProperty, value); }
    public string? ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    public ComparisonSort Sort { get => GetValue(SortProperty); set => SetValue(SortProperty, value); }
    protected string ValueText(double value) { try { return value.ToString(ValueFormat, CultureInfo.CurrentCulture); } catch (FormatException) { return F(value); } }
    protected string? ReadText(object? item, string? path) => !string.IsNullOrWhiteSpace(path) && ChartDataAccessor.TryRead(item, path, out var raw) ? raw?.ToString() : null;
    protected static object? Read(object? item, string? path) => !string.IsNullOrWhiteSpace(path) && ChartDataAccessor.TryRead(item, path, out var raw) ? raw : null;
    protected void BeginScene() { Labels.Clear(); Decorations.Clear(); }
    protected Point Normalized(double x, double y, Rect plot) => ApplyViewport(new(plot.Left + plot.Width * x, plot.Top + plot.Height * y), plot);
    protected Geometry Rectangle(double x, double y, double width, double height, Rect plot)
    {
        var a = Normalized(x, y, plot); var b = Normalized(x + width, y + height, plot);
        return new RectangleGeometry(new Rect(a, b));
    }
    protected EngineeringMarkInfo Mark(ChartDataPoint source, Geometry geometry, IBrush? fill, Point position, object key, double value, string tooltip) =>
        new(Array.AsReadOnly(new[] { source }), geometry, fill, position) { Key = key, Value = value, TooltipText = tooltip };
    protected void Label(string? text, Point position, double width) { if (ShowLabels && !string.IsNullOrEmpty(text)) Labels.Add((text, position, width)); }
    protected void NumberLabel(double value, Point position, double width) { if (ShowValues) Labels.Add((ValueText(value), position, width)); }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    {
        using (context.PushClip(plot)) foreach (var decoration in Decorations) context.DrawGeometry(null, new Pen(decoration.Brush ?? Foreground ?? Brushes.Gray, decoration.Thickness), decoration.Geometry);
        foreach (var label in Labels) DrawLabel(context, label.Text, label.Position, label.Width);
    }
    protected override EngineeringPointHit? ResolveFollowedMark(EngineeringPointHit previous, IReadOnlyList<EngineeringMarkInfo> marks, EngineeringPointHit? proposed)
    {
        // Stable frames already carry the current glyph. Avoid searching every glyph when
        // an earlier sibling of the same source is the engineering base's default proposal.
        if (previous.Index >= 0 && previous.Index < marks.Count && ReferenceEquals(previous.Mark, marks[previous.Index])) return previous;
        if (proposed is not null && Equals(previous.Mark.Key, proposed.Mark.Key)) return proposed;
        // A compound source can own several selectable glyphs. Only the former side/segment/region
        // is eligible; falling back to its first glyph silently changes the user's selection.
        for (var i = 0; i < marks.Count; i++)
            if (Equals(previous.Mark.Key, marks[i].Key) && previous.Members.Any(old => marks[i].Members.Any(current =>
                old.StableId is not null && (string.IsNullOrWhiteSpace(IdPath) ? ReferenceEquals(old.StableId, current.StableId) : Equals(old.StableId, current.StableId))))) return new(i, marks[i]);
        return null;
    }
    protected void ObserveNested(IEnumerable<object?> owners)
    {
        if (!_observing) return;
        var desired = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var owner in owners) if (owner is INotifyPropertyChanged or INotifyCollectionChanged) desired.Add(owner);
        foreach (var old in _nested.Where(x => !desired.Contains(x)).ToArray()) { Unsubscribe(old); _nested.Remove(old); }
        foreach (var owner in desired) if (_nested.Add(owner)) { if (owner is INotifyPropertyChanged property) property.PropertyChanged += NestedChanged; if (owner is INotifyCollectionChanged collection) collection.CollectionChanged += NestedCollectionChanged; }
    }
    private void NestedChanged(object? sender, PropertyChangedEventArgs args) => Refresh();
    private void NestedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => Refresh();
    private void Unsubscribe(object owner) { if (owner is INotifyPropertyChanged property) property.PropertyChanged -= NestedChanged; if (owner is INotifyCollectionChanged collection) collection.CollectionChanged -= NestedCollectionChanged; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { if (change.Property == ItemsSourceProperty) { foreach (var owner in _nested) Unsubscribe(owner); _nested.Clear(); } base.OnPropertyChanged(change); }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { _observing = true; base.OnAttachedToVisualTree(e); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _observing = false; foreach (var owner in _nested) Unsubscribe(owner); _nested.Clear(); base.OnDetachedFromVisualTree(e); }
    protected override void ClearProjection(Exception error) => BeginScene();
    protected static IReadOnlyList<object?> Enumerate(object? value)
    {
        if (value is not IEnumerable source || value is string) return [];
        var result = new List<object?>(); foreach (var item in source) { if (result.Count >= ChartEngineering.MaximumCount) throw new ArgumentOutOfRangeException(nameof(value), "Nested data exceeds the comparison observation budget."); result.Add(item); }
        return result;
    }
}
