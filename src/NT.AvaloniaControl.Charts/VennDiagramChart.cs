using System.Collections;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Two- or three-set inclusive summaries rendered as independently selectable exclusive regions.
/// A mark's Key is its exclusive mask. Members are the inclusive summary rows used by inclusion-exclusion,
/// rather than individual set elements. Arbitrary three-set areas cannot always be represented by circles.</summary>
public sealed class VennDiagramChart : ComparisonChartBase
{
    public static readonly StyledProperty<string?> MaskPathProperty = AvaloniaProperty.Register<VennDiagramChart, string?>(nameof(MaskPath));
    public static readonly StyledProperty<string?> SetsPathProperty = AvaloniaProperty.Register<VennDiagramChart, string?>(nameof(SetsPath), "Sets");
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<VennDiagramChart, string?>(nameof(ValuePath), "Value");
    public static readonly StyledProperty<string?> NamePathProperty = LabelPathProperty;
    public static readonly StyledProperty<int> SetCountProperty = AvaloniaProperty.Register<VennDiagramChart, int>(nameof(SetCount), 3, validate: x => x is 2 or 3);
    public static readonly StyledProperty<IReadOnlyList<string>?> SetNamesProperty = AvaloniaProperty.Register<VennDiagramChart, IReadOnlyList<string>?>(nameof(SetNames));
    private VennLayout? _layout;
    private IReadOnlyList<int> _unrepresented = [];
    public VennDiagramChart() => ObservePaths(nameof(VennItem.Mask), nameof(VennItem.Value), nameof(VennItem.Label));
    public string? MaskPath { get => GetValue(MaskPathProperty); set => SetValue(MaskPathProperty, value); }
    public string? SetsPath { get => GetValue(SetsPathProperty); set => SetValue(SetsPathProperty, value); }
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public string? NamePath { get => LabelPath; set => LabelPath = value; }
    public int SetCount { get => GetValue(SetCountProperty); set => SetValue(SetCountProperty, value); }
    public IReadOnlyList<string>? SetNames { get => GetValue(SetNamesProperty); set => SetValue(SetNamesProperty, value); }
    /// <summary>Inclusive observations, exclusive atom values, and the circle-area approximation diagnostics.</summary>
    public VennLayout? Layout { get { _ = GetMarks(); return _layout; } }
    public bool IsApproximate => Layout?.IsApproximate ?? false;
    public double MaximumRelativeAreaError => Layout?.MaximumRelativeAreaError ?? 0;
    /// <summary>Positive atoms for which the approximate circles contain no usable interior.</summary>
    public IReadOnlyList<int> UnrepresentedRegionMasks { get { _ = GetMarks(); return _unrepresented; } }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == MaskPathProperty || change.Property == SetsPathProperty || change.Property == ValuePathProperty) ObservePaths(MaskPath ?? nameof(VennItem.Mask), SetsPath, ValuePath, nameof(VennItem.Label)); }
    private string SetName(int index) => SetNames is { } names && index < names.Count && !string.IsNullOrWhiteSpace(names[index]) ? names[index] : ((char)('A' + index)).ToString();
    private int ReadMask(object? item)
    {
        if (!string.IsNullOrWhiteSpace(MaskPath))
        { var raw = Read(item, MaskPath); if (ChartDataAccessor.TryNumber(raw, out var number) && number == Math.Truncate(number) && number is >= 1 and <= 7) return (int)number; return 0; }
        var sets = Read(item, SetsPath); var result = 0;
        var sequence = sets is string name ? new object?[] { name } : Enumerate(sets);
        foreach (var set in sequence)
        {
            var index = -1;
            if (set is string text) for (var i = 0; i < SetCount; i++) { if (string.Equals(text, SetName(i), StringComparison.OrdinalIgnoreCase)) { index = i; break; } }
            else if (ChartDataAccessor.TryNumber(set, out var numeric) && numeric == Math.Truncate(numeric) && numeric >= 0 && numeric < SetCount) index = (int)numeric;
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "A Venn set name or zero-based set index is unknown.");
            result |= 1 << index;
        }
        return result;
    }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var values = new VennItem[data.Count]; var owners = new List<object?>();
        try { for (var i = 0; i < data.Count; i++)
        {
            var source = data.Points[i]; owners.Add(Read(source.Item, SetsPath)); values[i] = source.Item is VennItem typed && string.IsNullOrWhiteSpace(MaskPath) ? typed : new(ReadMask(source.Item), Number(source.Item, ValuePath, out var value) ? value : double.NaN, ReadText(source.Item, LabelPath) ?? ReadText(source.Item, "Name"));
        } } finally { ObserveNested(owners); }
        _layout = ChartAdvancedComparison.Venn(values, SetCount);
        var square = Square(plot); var circles = _layout.Circles; var geometries = circles.ToDictionary(circle => circle.SetIndex, circle => (Geometry)new EllipseGeometry(new Rect(ApplyViewport(new(square.Left + (circle.Center.X - circle.Radius) * square.Width, square.Top + (circle.Center.Y - circle.Radius) * square.Height), plot), new Size(circle.Radius * 2 * square.Width * Zoom, circle.Radius * 2 * square.Height * Zoom))));
        var marks = new List<EngineeringMarkInfo>(); var missing = new List<int>();
        foreach (var region in _layout.Regions)
        {
            if (region.Value <= 0) continue;
            var center = Interior(region.Mask, circles);
            if (center is null) { missing.Add(region.Mask); continue; }
            Geometry? geometry = null;
            for (var set = 0; set < SetCount; set++)
                if ((region.Mask & (1 << set)) != 0)
                { if (!geometries.TryGetValue(set, out var circle)) { geometry = null; break; } geometry = geometry is null ? circle : new CombinedGeometry(GeometryCombineMode.Intersect, geometry, circle); }
            if (geometry is null) { missing.Add(region.Mask); continue; }
            for (var set = 0; set < SetCount; set++) if ((region.Mask & (1 << set)) == 0 && geometries.TryGetValue(set, out var circle)) geometry = new CombinedGeometry(GeometryCombineMode.Exclude, geometry, circle);
            var position = ApplyViewport(new(square.Left + center.Value.X * square.Width, square.Top + center.Value.Y * square.Height), plot);
            var members = region.SourceIndices.Select(index => data.Points[index] with { Point = new(values[index].Mask, values[index].Value), Value = values[index].Value, Category = values[index].Label, IsEmpty = false }).ToArray();
            var name = string.Join(" ∩ ", Enumerable.Range(0, SetCount).Where(index => (region.Mask & (1 << index)) != 0).Select(SetName));
            marks.Add(new(Array.AsReadOnly(members), geometry, BrushAt(region.Mask - 1), position) { Key = region.Mask, Value = region.Value, TooltipText = $"Exclusive {name}: {ValueText(region.Value)}; {members.Length} inclusive summary dependencies; represented {ValueText(region.EstimatedValue)}" });
            if (ShowValues) AddDataLabel(marks[^1], $"{name}: {ValueText(region.Value)}", position, square.Width / 3);
        }
        _unrepresented = Array.AsReadOnly(missing.ToArray());
        foreach (var circle in circles)
        {
            Decorations.Add((geometries[circle.SetIndex], Foreground, 1));
            Label(SetName(circle.SetIndex), ApplyViewport(new(square.Left + circle.Center.X * square.Width, square.Top + (circle.Center.Y - circle.Radius) * square.Height - 9), plot), square.Width / 3);
        }
        if (_layout.IsApproximate || missing.Count > 0) Labels.Add(($"Circle approximation; max area error {_layout.MaximumRelativeAreaError:P1}{(missing.Count > 0 ? $"; hidden atoms {string.Join(",", missing)}" : "")}", new(plot.Center.X, plot.Bottom + 12), plot.Width));
        return marks;
    }
    private static ChartPoint? Interior(int mask, IReadOnlyList<VennCircle> circles)
    {
        var best = double.NegativeInfinity; ChartPoint? result = null;
        void Consider(ChartPoint point)
        {
            var margin = double.PositiveInfinity;
            foreach (var circle in circles)
            { var dx = point.X - circle.Center.X; var dy = point.Y - circle.Center.Y; var distance = Math.Sqrt(dx * dx + dy * dy); margin = Math.Min(margin, (mask & (1 << circle.SetIndex)) != 0 ? circle.Radius - distance : distance - circle.Radius); }
            if (margin > 1e-10 && margin > best) { best = margin; result = point; }
        }
        // Analytic boundary seeds retain narrow lenses and crescents that a fixed sample grid can miss.
        var seeds = circles.Select(circle => circle.Center).ToList();
        for (var i = 0; i < circles.Count; i++) for (var j = i + 1; j < circles.Count; j++)
        {
            var a = circles[i]; var b = circles[j]; var dx = b.Center.X - a.Center.X; var dy = b.Center.Y - a.Center.Y; var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= 1e-15) continue;
            var along = (a.Radius * a.Radius - b.Radius * b.Radius + distance * distance) / (2 * distance); var heightSquared = a.Radius * a.Radius - along * along;
            if (heightSquared >= -1e-15)
            {
                var height = Math.Sqrt(Math.Max(0, heightSquared)); var x = a.Center.X + along * dx / distance; var y = a.Center.Y + along * dy / distance;
                seeds.Add(new(x - height * dy / distance, y + height * dx / distance)); seeds.Add(new(x + height * dy / distance, y - height * dx / distance)); seeds.Add(new(x, y));
            }
            foreach (var direction in new[] { -1d, 1d })
            { seeds.Add(new(a.Center.X + direction * a.Radius * dx / distance, a.Center.Y + direction * a.Radius * dy / distance)); seeds.Add(new(b.Center.X + direction * b.Radius * dx / distance, b.Center.Y + direction * b.Radius * dy / distance)); }
        }
        foreach (var seed in seeds)
        {
            Consider(seed);
            foreach (var epsilon in new[] { .05, .01, .002, .0004, .00008, .000016, .000001, .00000001 })
                for (var direction = 0; direction < 8; direction++) Consider(new(seed.X + epsilon * Math.Cos(direction * Math.PI / 4), seed.Y + epsilon * Math.Sin(direction * Math.PI / 4)));
        }
        for (var y = 0; y <= 96; y++) for (var x = 0; x <= 96; x++)
            Consider(new(x / 96d, y / 96d));
        return result;
    }
    protected override void ClearProjection(Exception error) { _layout = null; _unrepresented = []; base.ClearProjection(error); }
}
