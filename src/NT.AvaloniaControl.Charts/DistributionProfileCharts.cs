using System.Collections;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Shared source adaptation and observable nested samples for violin and ridgeline charts.</summary>
public abstract class DistributionProfileChartBase : ComparisonChartBase
{
    public static readonly StyledProperty<string?> SamplesPathProperty = AvaloniaProperty.Register<DistributionProfileChartBase, string?>(nameof(SamplesPath), "Samples");
    public static readonly StyledProperty<int> SampleCountProperty = AvaloniaProperty.Register<DistributionProfileChartBase, int>(nameof(SampleCount), 128, validate: value => value is >= 2 and <= 4096);
    public static readonly StyledProperty<double?> BandwidthProperty = AvaloniaProperty.Register<DistributionProfileChartBase, double?>(nameof(Bandwidth), validate: value => value is null || double.IsFinite(value.Value) && value.Value > 0);
    public static readonly StyledProperty<double?> MinimumProperty = AvaloniaProperty.Register<DistributionProfileChartBase, double?>(nameof(Minimum), validate: value => value is null || double.IsFinite(value.Value));
    public static readonly StyledProperty<double?> MaximumProperty = AvaloniaProperty.Register<DistributionProfileChartBase, double?>(nameof(Maximum), validate: value => value is null || double.IsFinite(value.Value));
    public static readonly StyledProperty<bool> ShowQuartilesProperty = AvaloniaProperty.Register<DistributionProfileChartBase, bool>(nameof(ShowQuartiles), true);
    public static readonly StyledProperty<double> ProfileWidthProperty = AvaloniaProperty.Register<DistributionProfileChartBase, double>(nameof(ProfileWidth), .8, validate: value => double.IsFinite(value) && value is > 0 and <= 1);
    private ChartDistributionProfileLayout? _layout;
    protected bool IsProfileAttached { get; private set; }
    protected DistributionProfileChartBase() { IdPath = "Id"; LabelPath = "Label"; ObservePaths(SamplesPath); }
    protected override int SourceItemLimit => ChartDistributionProfiles.MaximumGroups;
    public string? SamplesPath { get => GetValue(SamplesPathProperty); set => SetValue(SamplesPathProperty, value); }
    public int SampleCount { get => GetValue(SampleCountProperty); set => SetValue(SampleCountProperty, value); }
    public double? Bandwidth { get => GetValue(BandwidthProperty); set => SetValue(BandwidthProperty, value); }
    public double? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public bool ShowQuartiles { get => GetValue(ShowQuartilesProperty); set => SetValue(ShowQuartilesProperty, value); }
    public double ProfileWidth { get => GetValue(ProfileWidthProperty); set => SetValue(ProfileWidthProperty, value); }
    public ChartDistributionProfileLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override void OnDataInvalidated() => _layout = null;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SamplesPathProperty || change.Property == SampleCountProperty || change.Property == BandwidthProperty || change.Property == MinimumProperty || change.Property == MaximumProperty) _layout = null;
        base.OnPropertyChanged(change);
        if (change.Property == SamplesPathProperty) ObservePaths(SamplesPath);
    }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { IsProfileAttached = true; OnDataInvalidated(); base.OnAttachedToVisualTree(e); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { IsProfileAttached = false; OnDataInvalidated(); base.OnDetachedFromVisualTree(e); }
    protected ChartDistributionProfileLayout CaptureProfiles(ChartDataSnapshot data)
    {
        if (IsProfileAttached && _layout is not null) return _layout;
        var groups = new List<DistributionSamples>(); var owners = new List<object?>(); var count = 0;
        try
        {
            foreach (var source in data.Points)
            {
                var collection = Read(source.Item, SamplesPath); owners.Add(collection);
                var samples = new List<double>();
                if (collection is IEnumerable enumerable && collection is not string) foreach (var item in enumerable)
                {
                    if (count >= ChartDistributionProfiles.MaximumObservations) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "Nested enumeration exceeds 100000 actual entries, including omitted values.");
                    count++; samples.Add(ChartDataAccessor.TryNumber(item, out var value) ? value : double.NaN);
                }
                groups.Add(new(Read(source.Item, IdPath), ReadText(source.Item, LabelPath) ?? source.Category?.ToString() ?? "", samples.ToArray()) { OriginalItem = source.Item });
            }
        }
        finally { ObserveNested(owners); }
        return _layout = ChartDistributionProfiles.Calculate(groups, SampleCount, Bandwidth, Minimum, Maximum);
    }
    protected ChartDataPoint GroupMember(ChartDataSnapshot data, ChartDistributionProfile profile) => data.Points[profile.SourceIndex] with
    { Point = new(profile.SourceIndex, profile.Summary.Median), Value = profile, Low = profile.Summary.LowerQuartile, High = profile.Summary.UpperQuartile, Category = profile.Label, IsEmpty = false };
    protected string ProfileTooltip(ChartDistributionProfile profile) => $"{profile.Label}: n={profile.Observations.Count}; median {ValueText(profile.Summary.Median)}; Q1 {ValueText(profile.Summary.LowerQuartile)}; Q3 {ValueText(profile.Summary.UpperQuartile)}; h={F(profile.Density.Bandwidth)}";
    protected static double ValueFraction(double value, ChartDistributionProfileLayout layout) => Fraction(value, layout.Minimum, layout.Maximum);
    protected static double[] Quartiles(ChartDistributionProfile profile) => [profile.Summary.LowerQuartile, profile.Summary.Median, profile.Summary.UpperQuartile];
}

/// <summary>Vertical value axis and horizontal categories. All violin half-widths use the same absolute KDE scale.</summary>
public sealed class ViolinPlot : DistributionProfileChartBase
{
    public static readonly StyledProperty<string?> ValuesPathProperty = SamplesPathProperty;
    public static readonly StyledProperty<string?> CategoryPathProperty = LabelPathProperty;
    public static readonly StyledProperty<bool> ShowMedianProperty = AvaloniaProperty.Register<ViolinPlot, bool>(nameof(ShowMedian), true);
    public static readonly StyledProperty<bool> ShowBoxPlotProperty = AvaloniaProperty.Register<ViolinPlot, bool>(nameof(ShowBoxPlot), true);
    public string? ValuesPath { get => SamplesPath; set => SamplesPath = value; }
    public string? CategoryPath { get => LabelPath; set => LabelPath = value; }
    public bool ShowMedian { get => GetValue(ShowMedianProperty); set => SetValue(ShowMedianProperty, value); }
    public bool ShowBoxPlot { get => GetValue(ShowBoxPlotProperty); set => SetValue(ShowBoxPlotProperty, value); }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var layout = CaptureProfiles(data); var marks = new List<EngineeringMarkInfo>();
        if (layout.GroupCount == 0) return marks;
        foreach (var profile in layout.Groups)
        {
            var center = (profile.SourceIndex + .5) / layout.GroupCount;
            var width = ProfileWidth / layout.GroupCount / 2;
            var knots = profile.Density.Points;
            Point At(ChartPoint point, int direction) => Normalized(center + direction * width * point.Y / layout.MaximumDensity, 1 - ValueFraction(point.X, layout), plot);
            var polygon = knots.Select(point => At(point, -1)).Concat(knots.Reverse().Select(point => At(point, 1))).ToArray();
            var median = Normalized(center, 1 - ValueFraction(profile.Summary.Median, layout), plot);
            marks.Add(Mark(GroupMember(data, profile), new PolylineGeometry(polygon, true), BrushAt(profile.SourceIndex), median, "profile", profile.Summary.Median, ProfileTooltip(profile)));
            if (ShowQuartiles && ShowMedian)
            {
                var y = 1 - ValueFraction(profile.Summary.Median, layout);
                if (y is >= 0 and <= 1) Decorations.Add((Line([Normalized(center - width * .35, y, plot), Normalized(center + width * .35, y, plot)]), Foreground, 2));
            }
            // A box is a sample summary, not a reliable visual subdivision for fewer than five samples.
            if (ShowQuartiles && ShowBoxPlot && profile.Observations.Count >= 5)
            {
                var q1 = Math.Clamp(1 - ValueFraction(profile.Summary.LowerQuartile, layout), 0, 1);
                var q3 = Math.Clamp(1 - ValueFraction(profile.Summary.UpperQuartile, layout), 0, 1);
                Decorations.Add((Rectangle(center - width * .2, q3, width * .4, q1 - q3, plot), Foreground, 1));
                var lo = Math.Clamp(1 - ValueFraction(profile.Summary.LowerWhisker, layout), 0, 1);
                var hi = Math.Clamp(1 - ValueFraction(profile.Summary.UpperWhisker, layout), 0, 1);
                Decorations.Add((Line([Normalized(center, hi, plot), Normalized(center, lo, plot)]), Foreground, 1));
            }
            Label(profile.Label, Normalized(center, 1, plot) + new Vector(0, 12), Math.Max(1, plot.Width / layout.GroupCount - 4));
        }
        return marks;
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot)
    {
        var layout = Layout; if (layout is null || layout.Groups.Count == 0) return [];
        return Enumerable.Range(0, 5).Select(index => { var fraction = index / 4d; return new EngineeringGridLine(Line([Normalized(0, 1 - fraction, plot), Normalized(1, 1 - fraction, plot)]), ValueText(Interpolate(layout.Minimum, layout.Maximum, fraction)), Normalized(0, 1 - fraction, plot) + new Vector(12, 0)); }).ToArray();
    }
}

/// <summary>Horizontal values and category baselines. Zero overlap separates profiles; positive values increase their shared height relative to the row spacing.</summary>
public sealed class RidgelineChart : DistributionProfileChartBase
{
    public static readonly StyledProperty<double> OverlapProperty = AvaloniaProperty.Register<RidgelineChart, double>(nameof(Overlap), .5, validate: value => double.IsFinite(value) && value is >= 0 and <= 4);
    public static readonly StyledProperty<string?> ProfilePathProperty = AvaloniaProperty.Register<RidgelineChart, string?>(nameof(ProfilePath));
    private ChartDistributionCurveLayout? _curveLayout;
    public double Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }
    /// <summary>When set, maps each group's precomputed ChartPoint curve instead of its raw samples.</summary>
    public string? ProfilePath { get => GetValue(ProfilePathProperty); set => SetValue(ProfilePathProperty, value); }
    public ChartDistributionCurveLayout? CurveLayout { get { _ = GetMarks(); return _curveLayout; } }
    protected override void OnDataInvalidated() { _curveLayout = null; base.OnDataInvalidated(); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ProfilePathProperty || change.Property == MinimumProperty || change.Property == MaximumProperty) _curveLayout = null;
        base.OnPropertyChanged(change);
        if (change.Property == ProfilePathProperty) { OnDataInvalidated(); ObservePaths(ProfilePath ?? SamplesPath); }
        else if (change.Property == SamplesPathProperty && !string.IsNullOrWhiteSpace(ProfilePath)) ObservePaths(ProfilePath);
    }
    protected override void ClearProjection(Exception error) { _curveLayout = null; base.ClearProjection(error); }
    private ChartDistributionCurveLayout CaptureCurves(ChartDataSnapshot data)
    {
        if (IsProfileAttached && _curveLayout is not null) return _curveLayout;
        var groups = new List<DistributionCurve>(); var owners = new List<object?>(); var count = 0;
        try
        {
            foreach (var source in data.Points)
            {
                var collection = Read(source.Item, ProfilePath); owners.Add(collection); var points = new List<ChartPoint>();
                if (collection is IEnumerable enumerable && collection is not string) foreach (var value in enumerable)
                {
                    if (count >= ChartDistributionProfiles.MaximumObservations) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "Precomputed curve enumeration exceeds 100000 actual points.");
                    count++;
                    if (value is not ChartPoint point) throw new ArgumentException("Precomputed curve entries must be ChartPoint values.", nameof(ItemsSource));
                    points.Add(point);
                }
                groups.Add(new(Read(source.Item, IdPath), ReadText(source.Item, LabelPath) ?? "", points.ToArray()) { OriginalItem = source.Item });
            }
        }
        finally { ObserveNested(owners); }
        return _curveLayout = ChartDistributionProfiles.FromCurves(groups, Minimum, Maximum);
    }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene();
        if (!string.IsNullOrWhiteSpace(ProfilePath)) return BuildCurveMarks(data, plot);
        _curveLayout = null;
        var layout = CaptureProfiles(data); var marks = new List<EngineeringMarkInfo>();
        if (layout.GroupCount == 0) return marks;
        // Reserve enough space above the first baseline for overlapping ridges.
        var extent = (1 + Overlap) * ProfileWidth;
        var rows = layout.GroupCount - 1 + extent;
        foreach (var profile in layout.Groups)
        {
            var baseline = (profile.SourceIndex + extent) / rows;
            var height = extent / rows;
            var knots = profile.Density.Points;
            var polygon = new List<Point> { Normalized(0, baseline, plot) };
            polygon.AddRange(knots.Select(point => Normalized(ValueFraction(point.X, layout), baseline - height * point.Y / layout.MaximumDensity, plot)));
            polygon.Add(Normalized(1, baseline, plot));
            if (height > 0)
            {
                // The tallest sampled knot is guaranteed to lie inside the filled ridge, even for bimodal groups.
                var peak = knots.MaxBy(point => point.Y);
                var position = Normalized(ValueFraction(peak.X, layout), baseline - height * peak.Y / layout.MaximumDensity / 2, plot);
                marks.Add(Mark(GroupMember(data, profile), new PolylineGeometry(polygon, true), BrushAt(profile.SourceIndex), position, "profile", profile.Summary.Median, ProfileTooltip(profile)));
            }
            Decorations.Add((Line([Normalized(0, baseline, plot), Normalized(1, baseline, plot)]), GridLineBrush, 1));
            if (ShowQuartiles && height > 0) foreach (var value in Quartiles(profile))
            { var x = ValueFraction(value, layout); if (x is >= 0 and <= 1) Decorations.Add((Line([Normalized(x, baseline, plot), Normalized(x, baseline - height * .22, plot)]), Foreground, value == profile.Summary.Median ? 2 : 1)); }
            Label(profile.Label, Normalized(.08, baseline, plot) + new Vector(0, 9), Math.Max(1, plot.Width * .16));
        }
        return marks;
    }
    private IReadOnlyList<EngineeringMarkInfo> BuildCurveMarks(ChartDataSnapshot data, Rect plot)
    {
        var layout = CaptureCurves(data); var marks = new List<EngineeringMarkInfo>();
        if (layout.GroupCount == 0) return marks;
        var extent = (1 + Overlap) * ProfileWidth;
        var rows = layout.GroupCount - 1 + extent;
        foreach (var profile in layout.Groups)
        {
            var baseline = (profile.SourceIndex + extent) / rows;
            var height = extent / rows;
            double X(double value) => Fraction(value, layout.Minimum, layout.Maximum);
            var polygon = new List<Point> { Normalized(X(profile.Points[0].X), baseline, plot) };
            polygon.AddRange(profile.Points.Select(point => Normalized(X(point.X), baseline - (layout.MaximumDensity > 0 ? height * point.Y / layout.MaximumDensity : 0), plot)));
            polygon.Add(Normalized(X(profile.Points[^1].X), baseline, plot));
            var peak = profile.Points.MaxBy(point => point.Y);
            if (height > 0 && peak.Y > 0)
            {
                var source = data.Points[profile.SourceIndex] with { Point = peak, Value = profile, Category = profile.Source.Label, IsEmpty = false };
                var position = Normalized(X(peak.X), baseline - height * peak.Y / layout.MaximumDensity / 2, plot);
                marks.Add(Mark(source, new PolylineGeometry(polygon, true), BrushAt(profile.SourceIndex), position, "profile", peak.Y, $"{profile.Source.Label}: precomputed density; {profile.Points.Count} knots; peak {ValueText(peak.Y)}"));
            }
            Decorations.Add((Line([Normalized(0, baseline, plot), Normalized(1, baseline, plot)]), GridLineBrush, 1));
            Label(profile.Source.Label, Normalized(.08, baseline, plot) + new Vector(0, 9), Math.Max(1, plot.Width * .16));
        }
        return marks;
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot)
    {
        if (!string.IsNullOrWhiteSpace(ProfilePath))
        {
            var curves = _curveLayout; if (curves is null || curves.Groups.Count == 0) return [];
            return Enumerable.Range(0, 5).Select(index => { var fraction = index / 4d; return new EngineeringGridLine(Line([Normalized(fraction, 0, plot), Normalized(fraction, 1, plot)]), ValueText(Interpolate(curves.Minimum, curves.Maximum, fraction)), Normalized(fraction, 1, plot) + new Vector(0, 12)); }).ToArray();
        }
        var layout = Layout; if (layout is null || layout.Groups.Count == 0) return [];
        return Enumerable.Range(0, 5).Select(index => { var fraction = index / 4d; return new EngineeringGridLine(Line([Normalized(fraction, 0, plot), Normalized(fraction, 1, plot)]), ValueText(Interpolate(layout.Minimum, layout.Maximum, fraction)), Normalized(fraction, 1, plot) + new Vector(0, 12)); }).ToArray();
    }
}
