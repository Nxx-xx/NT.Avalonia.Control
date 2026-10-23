using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal enum PolarAxisPresentationKind { ValueRadius, RadialBar, NormalizedReference }

/// <summary>A description of the actual displayed projection, rather than an assumption that value equals radius.</summary>
internal sealed record PolarAxisPresentation(PolarAxisPresentationKind Kind, Point Center, double Radius,
    double Minimum, double Maximum, double InnerRadius, bool AreaRadius, bool CenterCategoryLabels,
    IReadOnlyList<object?> Categories, double StartAngle, double SweepAngle, bool Clockwise,
    bool AngleInversed, bool RadiusInversed)
{
    internal static PolarAxisPresentation Create(PolarScene scene, double startAngle, double sweepAngle,
        bool clockwise, bool angleInversed, bool radiusInversed)
    {
        var series = scene.Series.Select(group => group.Series).ToArray();
        var first = series.FirstOrDefault();
        var bar = first is RadialBarSeries;
        var area = first is PolarAreaSeries;
        var inner = first?.InnerRadius ?? 0;
        var maximum = EffectiveMaximum(first);
        var centered = first is PolarAreaSeries or NightingaleRoseSeries;
        var categorical = first?.IsCategorical ?? false;
        var sameMapping = maximum > scene.RadiusMinimum && series.All(item =>
            (item is RadialBarSeries) == bar && (item is PolarAreaSeries) == area &&
            item.InnerRadius == inner && EffectiveMaximum(item) == maximum &&
            item.IsCategorical == categorical && (item is PolarAreaSeries or NightingaleRoseSeries) == centered);
        var kind = !sameMapping ? PolarAxisPresentationKind.NormalizedReference :
            bar ? PolarAxisPresentationKind.RadialBar : PolarAxisPresentationKind.ValueRadius;
        return new(kind, scene.Center, scene.Radius, scene.RadiusMinimum, maximum, inner, area, centered,
            scene.Categories, startAngle, sweepAngle, clockwise, angleInversed, radiusInversed);

        double EffectiveMaximum(PolarSeries? item) => item is RadarSeries { MaxValue: { } value } ? value : scene.RadiusMaximum;
    }
    internal double AngleAtFraction(double fraction) => PolarChartGeometry.NormalizeAngle(StartAngle) +
        (Clockwise ? 1 : -1) * SweepAngle * (AngleInversed ? 1 - fraction : fraction);
    internal double ValueAngleAtFraction(double fraction) => AngleAtFraction(RadiusInversed ? 1 - fraction : fraction);
    internal double RadiusAtFraction(double fraction)
    {
        if (Kind == PolarAxisPresentationKind.NormalizedReference) return Radius * fraction;
        var value = RadiusInversed ? 1 - fraction : fraction;
        return AreaRadius ? Radius * Math.Sqrt(InnerRadius * InnerRadius + (1 - InnerRadius * InnerRadius) * value) :
            Radius * (InnerRadius + (1 - InnerRadius) * value);
    }
    internal double ValueAtFraction(double fraction) => Minimum * (1 - fraction) + Maximum * fraction;
    internal double LaneBoundaryRadius(int boundary) => Radius * (InnerRadius + (1 - InnerRadius) * boundary / Math.Max(1, Categories.Count));
    internal Point CategoryLabelPosition(int index)
    {
        if (Kind == PolarAxisPresentationKind.RadialBar)
        {
            var angle = AngleAtFraction(0);
            var unit = PolarChartGeometry.PointAt(default, angle, 1);
            var radius = (LaneBoundaryRadius(index) + LaneBoundaryRadius(index + 1)) / 2;
            return PolarChartGeometry.PointAt(Center, angle, radius) + new Vector(unit.Y * 14, -unit.X * 14);
        }
        var fraction = (index + (CenterCategoryLabels ? .5 : 0)) / Math.Max(1, Categories.Count);
        return PolarChartGeometry.PointAt(Center, AngleAtFraction(fraction), Radius + 16);
    }
}

public partial class PolarChart
{
    internal PolarAxisPresentation GetAxisPresentation() => PolarAxisPresentation.Create(GetScene(), StartAngle,
        SweepAngle, Clockwise, AngularAxis.IsInversed, RadialAxis.IsInversed);

    private void DrawGrid(DrawingContext context, PolarScene scene)
    {
        var profile = PolarAxisPresentation.Create(scene, StartAngle, SweepAngle, Clockwise, AngularAxis.IsInversed, RadialAxis.IsInversed);
        if (profile.Kind == PolarAxisPresentationKind.RadialBar) { DrawBarAxes(context, scene, profile); return; }
        var reference = profile.Kind == PolarAxisPresentationKind.NormalizedReference;
        // Sector values and physical r/R references are constant along a circular arc.
        // Polygon rings are only vertex references for connected point projections.
        var circularRings = reference || profile.CenterCategoryLabels;
        var angularMinimum = AngularAxis.Minimum ?? 0;
        var angularMaximum = AngularAxis.Maximum ?? (AngleUnit == ChartAngleUnit.Degrees ? 360 : Math.PI * 2);
        var spokes = !reference && scene.Categories.Count > 0
            ? Enumerable.Range(0, scene.Categories.Count).Select(i => (double)i / scene.Categories.Count).ToArray()
            : Fractions(reference ? 0 : angularMinimum, reference ? 1 : angularMaximum,
                reference ? null : AngularAxis.Interval, 8, includeMinimum: true, includeMaximum: SweepAngle < 360);
        var levels = Fractions(reference ? 0 : profile.Minimum, reference ? 1 : profile.Maximum,
            reference ? null : RadialAxis.Interval, GridLevels, includeMinimum: !reference && profile.InnerRadius > 0, includeMaximum: true);
        if (ShowGridLines && GridLineBrush is { } gridBrush && GridLineStrokeThickness > 0)
        {
            var pen = new Pen(gridBrush, GridLineStrokeThickness);
            foreach (var fraction in levels)
            {
                var radius = profile.RadiusAtFraction(fraction);
                if (radius > 0) context.DrawGeometry(null, pen, Ring(scene.Center, radius, spokes.Select(profile.AngleAtFraction).ToArray(), circularRings));
            }
            foreach (var fraction in spokes)
                context.DrawLine(pen, scene.Center, PolarChartGeometry.PointAt(scene.Center, profile.AngleAtFraction(fraction), scene.Radius));
        }
        DrawOuterAxis(context, scene, spokes.Select(profile.AngleAtFraction).ToArray(), circularRings);
        if (!ShowAxisLabels) return;
        var labels = AxisLabels ?? AngularAxis.Labels;
        for (var i = 0; i < spokes.Length; i++)
        {
            var angle = profile.AngleAtFraction(spokes[i]);
            var label = reference ? $"θ={PolarChartGeometry.NormalizeAngle(angle).ToString("G4", CultureInfo.CurrentCulture)}°" :
                scene.Categories.Count > 0 ? CategoryLabel(labels, scene.Categories, i) :
                FormatAxis(angularMinimum * (1 - spokes[i]) + angularMaximum * spokes[i], AngularAxis);
            var position = !reference && scene.Categories.Count > 0 ? profile.CategoryLabelPosition(i) :
                PolarChartGeometry.PointAt(scene.Center, angle, scene.Radius + 16);
            DrawAxisLabel(context, label, position, AngularAxis);
        }
        foreach (var fraction in levels)
        {
            var position = PolarChartGeometry.PointAt(scene.Center, profile.AngleAtFraction(0), profile.RadiusAtFraction(fraction)) + new Vector(12, 0);
            var label = reference ? $"r/R={fraction.ToString("0%", CultureInfo.CurrentCulture)}" : FormatAxis(profile.ValueAtFraction(fraction), RadialAxis);
            DrawAxisLabel(context, label, position, RadialAxis);
        }
        if (reference)
        {
            DrawAxisLabel(context, "归一半径参考", new Point(scene.Center.X, _plotArea.Bottom + 36), RadialAxis, 220);
            return;
        }
        DrawAxisTitles(context, scene);
    }

    private void DrawBarAxes(DrawingContext context, PolarScene scene, PolarAxisPresentation profile)
    {
        var values = Fractions(profile.Minimum, profile.Maximum, RadialAxis.Interval, GridLevels,
            includeMinimum: true, includeMaximum: SweepAngle < 360);
        var angles = values.Select(profile.ValueAngleAtFraction).ToArray();
        if (ShowGridLines && GridLineBrush is { } gridBrush && GridLineStrokeThickness > 0)
        {
            var pen = new Pen(gridBrush, GridLineStrokeThickness);
            for (var i = 0; i <= scene.Categories.Count; i++)
            {
                var radius = profile.LaneBoundaryRadius(i);
                if (radius > 0) context.DrawGeometry(null, pen, Ring(scene.Center, radius, Array.Empty<double>(), true));
            }
            foreach (var angle in angles)
                context.DrawLine(pen, PolarChartGeometry.PointAt(scene.Center, angle, profile.Radius * profile.InnerRadius),
                    PolarChartGeometry.PointAt(scene.Center, angle, profile.Radius));
        }
        // Radial bars occupy circular lanes even when another chart family selects polygon rings.
        if (AxisBrush is { } axisBrush && GridLineStrokeThickness > 0)
            context.DrawGeometry(null, new Pen(axisBrush, GridLineStrokeThickness), Ring(scene.Center, scene.Radius, Array.Empty<double>(), true));
        if (!ShowAxisLabels) return;
        var labels = AxisLabels ?? AngularAxis.Labels;
        for (var i = 0; i < scene.Categories.Count; i++)
            DrawAxisLabel(context, CategoryLabel(labels, scene.Categories, i), profile.CategoryLabelPosition(i), AngularAxis);
        foreach (var fraction in values)
            DrawAxisLabel(context, FormatAxis(profile.ValueAtFraction(fraction), RadialAxis),
                PolarChartGeometry.PointAt(scene.Center, profile.ValueAngleAtFraction(fraction), scene.Radius + 16), RadialAxis);
        DrawAxisTitles(context, scene);
    }
    private void DrawOuterAxis(DrawingContext context, PolarScene scene, IReadOnlyList<double> angles, bool circularRings)
    {
        if (AxisBrush is { } axisBrush && GridLineStrokeThickness > 0)
            context.DrawGeometry(null, new Pen(axisBrush, GridLineStrokeThickness), Ring(scene.Center, scene.Radius, angles, circularRings));
    }
    private void DrawAxisTitles(DrawingContext context, PolarScene scene)
    {
        if (AngularAxis.Title is { Length: > 0 } angularTitle) DrawAxisLabel(context, angularTitle, new Point(scene.Center.X, _plotArea.Bottom + 38), AngularAxis);
        if (RadialAxis.Title is { Length: > 0 } radialTitle) DrawAxisLabel(context, radialTitle, new Point(_plotArea.Left - 24, scene.Center.Y), RadialAxis);
    }
    private static string CategoryLabel(IReadOnlyList<string>? labels, IReadOnlyList<object?> categories, int index) =>
        labels is not null && index < labels.Count ? labels[index] : Convert.ToString(categories[index], CultureInfo.CurrentCulture) ?? "";
    private Geometry Ring(Point center, double radius, IReadOnlyList<double> angles, bool forceCircle = false)
    {
        if ((forceCircle || GridShape == PolarGridShape.Circle) && SweepAngle == 360)
            return new EllipseGeometry(new Rect(center - new Vector(radius, radius), new Size(radius * 2, radius * 2)));
        var geometry = new StreamGeometry(); using var path = geometry.Open();
        if (!forceCircle && GridShape == PolarGridShape.Polygon && angles.Count >= 3)
        {
            path.BeginFigure(PolarChartGeometry.PointAt(center, angles[0], radius), false);
            foreach (var angle in angles.Skip(1)) path.LineTo(PolarChartGeometry.PointAt(center, angle, radius));
            path.EndFigure(SweepAngle == 360);
        }
        else
        {
            var sweep = (Clockwise ? 1 : -1) * (AngularAxis.IsInversed ? -1 : 1) * SweepAngle;
            var start = PolarChartGeometry.NormalizeAngle(StartAngle) + (AngularAxis.IsInversed ? (Clockwise ? 1 : -1) * SweepAngle : 0);
            path.BeginFigure(PolarChartGeometry.PointAt(center, start, radius), false);
            var direction = sweep >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
            var count = SweepAngle >= 360 ? 2 : 1;
            for (var i = 1; i <= count; i++) path.ArcTo(PolarChartGeometry.PointAt(center, start + sweep * i / count, radius),
                new Size(radius, radius), 0, SweepAngle / count > 180, direction);
            path.EndFigure(false);
        }
        return geometry;
    }
    private static double[] Fractions(double min, double max, double? interval, int count, bool includeMinimum, bool includeMaximum)
    {
        var result = new List<double>(); if (includeMinimum) result.Add(0);
        if (interval is { } step && double.IsFinite(step) && step > 0 && double.IsFinite(max - min))
        {
            for (var i = 1; i <= 100; i++) { var fraction = i * step / (max - min); if (!double.IsFinite(fraction) || fraction >= 1) break; result.Add(fraction); }
        }
        else for (var i = 1; i < count; i++) result.Add((double)i / count);
        if (includeMaximum) result.Add(1); return result.ToArray();
    }
    private static string FormatAxis(double value, ChartAxis axis)
    {
        try { return value.ToString(axis.LabelFormat ?? "G4", CultureInfo.CurrentCulture); }
        catch (FormatException) { return value.ToString("G4", CultureInfo.CurrentCulture); }
    }
    private void DrawAxisLabel(DrawingContext context, string label, Point position, ChartAxis axis, double maxWidth = 96)
    {
        var text = Text(label); text.MaxTextWidth = Math.Min(maxWidth, axis.LabelMaxWidth); text.MaxLineCount = 1; text.Trimming = TextTrimming.CharacterEllipsis;
        context.DrawText(text, new Point(Math.Clamp(position.X - text.Width / 2, 0, Math.Max(0, Bounds.Width - text.Width)),
            Math.Clamp(position.Y - text.Height / 2, 0, Math.Max(0, Bounds.Height - text.Height))));
    }
}
