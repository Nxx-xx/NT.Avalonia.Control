using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Original categorical samples with shared selection, keyboard, navigation and raster export.</summary>
public abstract class CategoricalDistributionChartBase : ComparisonChartBase
{
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, string?>(nameof(ValuePath), nameof(CategoricalSample.Value));
    public static readonly StyledProperty<string?> CategoryPathProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, string?>(nameof(CategoryPath), nameof(CategoricalSample.Category));
    public static readonly StyledProperty<double> JitterProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, double>(nameof(Jitter), .36, validate: value => double.IsFinite(value) && value >= 0 && value <= .5);
    public static readonly StyledProperty<int> JitterSeedProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, int>(nameof(JitterSeed));
    public static readonly StyledProperty<bool> ShowMeanProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, bool>(nameof(ShowMean), true);
    public static readonly StyledProperty<IBrush?> MeanBrushProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, IBrush?>(nameof(MeanBrush));
    public static readonly StyledProperty<double> PointRadiusProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, double>(nameof(PointRadius), 4, validate: value => double.IsFinite(value) && value >= 0 && value <= 512);
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, IBrush?>(nameof(Fill));
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, IBrush?>(nameof(Stroke));
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, double>(nameof(StrokeThickness), 1, validate: value => double.IsFinite(value) && value >= 0 && value <= 64);
    public static readonly StyledProperty<bool> ShowCategoryLabelsProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, bool>(nameof(ShowCategoryLabels), true);
    public static readonly StyledProperty<bool> ShowAxesProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, bool>(nameof(ShowAxes), true);
    public static readonly StyledProperty<bool> ShowMeanLineProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, bool>(nameof(ShowMeanLine), true);
    public static readonly StyledProperty<double> JitterAmountProperty = AvaloniaProperty.Register<CategoricalDistributionChartBase, double>(nameof(JitterAmount), .36, validate: value => double.IsFinite(value) && value >= 0 && value <= .5);
    private CategoricalDistributionLayout? _layout;
    protected CategoricalDistributionChartBase()
    { IdPath = nameof(CategoricalSample.Id); LabelPath = nameof(CategoricalSample.Label); ObservePaths(ValuePath, CategoryPath, LabelPath); }
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public string? CategoryPath { get => GetValue(CategoryPathProperty); set => SetValue(CategoryPathProperty, value); }
    /// <summary>Maximum displacement on either side of the lane center, as a fraction of the lane width.</summary>
    public double Jitter { get => GetValue(JitterProperty); set => SetValue(JitterProperty, value); }
    public int JitterSeed { get => GetValue(JitterSeedProperty); set => SetValue(JitterSeedProperty, value); }
    public bool ShowMean { get => GetValue(ShowMeanProperty); set => SetValue(ShowMeanProperty, value); }
    public IBrush? MeanBrush { get => GetValue(MeanBrushProperty); set => SetValue(MeanBrushProperty, value); }
    /// <summary>Alias for half MarkerSize, in DIPs; both properties stay synchronized.</summary>
    public double PointRadius { get => GetValue(PointRadiusProperty); set => SetValue(PointRadiusProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public bool ShowCategoryLabels { get => GetValue(ShowCategoryLabelsProperty); set => SetValue(ShowCategoryLabelsProperty, value); }
    public bool ShowAxes { get => GetValue(ShowAxesProperty); set => SetValue(ShowAxesProperty, value); }
    /// <summary>Alias for ShowMean.</summary>
    public bool ShowMeanLine { get => GetValue(ShowMeanLineProperty); set => SetValue(ShowMeanLineProperty, value); }
    /// <summary>Alias for Jitter, expressed as a fraction of the category width (0..0.5).</summary>
    public double JitterAmount { get => GetValue(JitterAmountProperty); set => SetValue(JitterAmountProperty, value); }
    public CategoricalDistributionLayout? Layout { get { _ = GetMarks(); return _layout; } }
    public int CollisionCount => Layout?.CollisionCount ?? 0;
    public int OverflowCount => Layout?.OverflowCount ?? 0;
    public bool WorkBudgetExceeded => Layout?.WorkBudgetExceeded ?? false;
    protected abstract CategoricalDistributionKind DistributionKind { get; }
    protected override int SourceItemLimit => ChartCategoricalDistribution.MaximumCount;

    protected override ChartDataSnapshot CreateEngineeringSnapshot()
    {
        try { return base.CreateEngineeringSnapshot(); }
        catch (InvalidOperationException error) when (error.Message.StartsWith("IdPath '", StringComparison.Ordinal))
        { throw new ArgumentException(error.Message, nameof(ItemsSource), error); }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PointRadiusProperty) SetCurrentValue(MarkerSizeProperty, PointRadius * 2);
        else if (change.Property == MarkerSizeProperty) SetCurrentValue(PointRadiusProperty, MarkerSize / 2);
        else if (change.Property == ShowMeanLineProperty) SetCurrentValue(ShowMeanProperty, ShowMeanLine);
        else if (change.Property == ShowMeanProperty) SetCurrentValue(ShowMeanLineProperty, ShowMean);
        else if (change.Property == JitterAmountProperty) SetCurrentValue(JitterProperty, JitterAmount);
        else if (change.Property == JitterProperty) SetCurrentValue(JitterAmountProperty, Jitter);
        if (change.Property == ValuePathProperty || change.Property == CategoryPathProperty || change.Property == LabelPathProperty)
            ObservePaths(ValuePath, CategoryPath, LabelPath);
    }

    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); _layout = null;
        var raw = new CategoricalSample[data.Count]; var identities = new HashSet<object>();
        for (var index = 0; index < raw.Length; index++)
        {
            var source = data.Points[index]; var category = Read(source.Item, CategoryPath);
            if (category is null) throw new ArgumentException("Every observation requires a mapped category.", nameof(ItemsSource));
            var id = Read(source.Item, IdPath);
            if (id is not null && !identities.Add(id)) throw new ArgumentException("Mapped non-null observation IDs must be unique.", nameof(ItemsSource));
            raw[index] = new(id, category, Number(source.Item, ValuePath, out var value) ? value : double.NaN, ReadText(source.Item, LabelPath));
        }
        if (plot.Width <= 0 || plot.Height <= 0) return [];
        _layout = ChartCategoricalDistribution.Layout(raw, DistributionKind, plot.Width, plot.Height, MarkerSize / 2, Jitter, JitterSeed);
        var marks = new List<EngineeringMarkInfo>(raw.Length);
        IEnumerable<CategoricalDistributionPoint> points = _layout.Points;
        if (Sort == ComparisonSort.ValueAscending) points = points.OrderBy(point => point.Value).ThenBy(point => point.SourceIndex);
        else if (Sort == ComparisonSort.ValueDescending) points = points.OrderByDescending(point => point.Value).ThenBy(point => point.SourceIndex);
        foreach (var point in points)
        {
            var original = data.Points[point.SourceIndex];
            var source = original with { StableId = point.Id ?? original.Item, Category = point.Category,
                Point = new(point.CategoryIndex, point.Value), Value = point.Value, IsEmpty = false };
            if (MarkerSize <= 0) continue;
            var position = ApplyViewport(new(plot.Left + point.X, plot.Top + point.Y), plot);
            var diameter = MarkerSize * Zoom;
            marks.Add(Mark(source, new EllipseGeometry(new Rect(position.X - diameter / 2, position.Y - diameter / 2, diameter, diameter)),
                Fill ?? BrushAt(point.CategoryIndex), position, "sample", point.Value,
                $"{point.Category}: {ValueText(point.Value)}{(string.IsNullOrEmpty(point.Label) ? "" : $" ({point.Label})")}{(point.IsOverflow ? " [dense lane]" : "")}"));
        }
        var half = _layout.Categories.Count == 0 ? 0 : Math.Min(plot.Width / _layout.Categories.Count * .32, 40);
        foreach (var category in _layout.Categories)
        {
            if (ShowCategoryLabels && ShowAxes) Label(category.Category.ToString(), ApplyViewport(new(plot.Left + category.CenterX, plot.Bottom + 12), plot), Math.Max(16, plot.Width / _layout.Categories.Count - 4));
            if (ShowMean)
            {
                var left = ApplyViewport(new(plot.Left + category.CenterX - half, plot.Top + category.MeanY), plot);
                var right = ApplyViewport(new(plot.Left + category.CenterX + half, plot.Top + category.MeanY), plot);
                Decorations.Add((Line([left, right]), MeanBrush ?? Foreground, 2));
                NumberLabel(category.Mean, right + new Vector(0, -10), Math.Max(16, half * 2));
            }
        }
        if (ShowAxes && _layout.Points.Count > 0)
        {
            Decorations.Add((Line([ApplyViewport(plot.TopLeft, plot), ApplyViewport(plot.BottomLeft, plot), ApplyViewport(plot.BottomRight, plot)]), Foreground, 1));
            var inset = Math.Min(MarkerSize / 2, plot.Height / 2);
            for (var index = 0; index < 5; index++)
            {
                var fraction = index / 4d; var y = plot.Bottom - inset - fraction * (plot.Height - 2 * inset);
                Labels.Add((ValueText(Interpolate(_layout.Minimum, _layout.Maximum, fraction)), ApplyViewport(new(plot.Left + 16, y - 8), plot), 64));
            }
        }
        return marks;
    }

    protected override void RenderEngineeringMarks(DrawingContext context, IReadOnlyList<EngineeringMarkInfo> marks,
        EngineeringPointHit? selected, EngineeringPointHit? active)
    {
        for (var index = 0; index < marks.Count; index++)
        {
            var mark = marks[index]; var highlighted = index == selected?.Index || index == active?.Index;
            var thickness = highlighted ? Math.Max(2, StrokeThickness) : StrokeThickness;
            var stroke = highlighted ? Foreground ?? Stroke ?? mark.Fill ?? Brushes.Gray : Stroke ?? mark.Stroke ?? mark.Fill ?? Brushes.Gray;
            context.DrawGeometry(mark.Fill, thickness > 0 ? new Pen(stroke, thickness) : null, mark.Geometry);
        }
    }

    /// <summary>All visible original points under a position, in reverse paint order. Dense overlaps remain independently addressable.</summary>
    public IReadOnlyList<EngineeringPointHit> HitTestAll(Point position)
    {
        var marks = GetMarks();
        if (!new Rect(Bounds.Size).Contains(position) || !PlotArea.Contains(position)) return [];
        var hits = new List<EngineeringPointHit>();
        for (var index = marks.Count - 1; index >= 0; index--)
            if (marks[index].Fill is not null && ContainsMark(marks[index], position)) hits.Add(new(index, marks[index]));
        return Array.AsReadOnly(hits.ToArray());
    }

    protected override EngineeringPointHit? FindFollowedMark(EngineeringPointHit previous, IReadOnlyList<EngineeringMarkInfo> marks)
    {
        var former = previous.Members[0];
        for (var index = 0; index < marks.Count; index++)
        {
            var current = marks[index].Members[0];
            var hasExplicitIdentity = former.StableId is not null && !ReferenceEquals(former.StableId, former.Item);
            if (hasExplicitIdentity ? Equals(former.StableId, current.StableId) : ReferenceEquals(former.Item, current.Item)) return new(index, marks[index]);
        }
        return null;
    }

    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot)
    {
        if (_layout is null || _layout.Points.Count == 0) return [];
        var inset = Math.Min(MarkerSize / 2, plot.Height / 2);
        return Enumerable.Range(0, 5).Select(index =>
        {
            var fraction = index / 4d; var y = plot.Bottom - inset - fraction * (plot.Height - 2 * inset);
            var a = ApplyViewport(new(plot.Left, y), plot); var b = ApplyViewport(new(plot.Right, y), plot);
            return new EngineeringGridLine(Line([a, b]));
        }).ToArray();
    }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}

/// <summary>A categorical distribution with bounded, deterministic collision avoidance and explicit dense-lane diagnostics.</summary>
public class BeeswarmChart : CategoricalDistributionChartBase
{ protected override CategoricalDistributionKind DistributionKind => CategoricalDistributionKind.Beeswarm; }

/// <summary>A categorical strip of every original observation, with deterministic seeded horizontal jitter.</summary>
public class StripPlot : CategoricalDistributionChartBase
{ protected override CategoricalDistributionKind DistributionKind => CategoricalDistributionKind.Strip; }

/// <summary>Convenience name for BeeswarmChart; shares its source-preserving contract.</summary>
public sealed class BeeswarmPlotChart : BeeswarmChart { }
/// <summary>Convenience name for StripPlot; shares its source-preserving contract.</summary>
public sealed class StripPlotChart : StripPlot { }
