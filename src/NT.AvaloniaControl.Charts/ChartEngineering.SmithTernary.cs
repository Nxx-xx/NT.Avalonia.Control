using System.Globalization;

namespace NT.AvaloniaControl.Charts;

public static partial class ChartEngineering
{
    /// <summary>Returns fractions summing to one. Scaling before summation also accepts very large components.</summary>
    public static TernaryPoint NormalizeTernary(TernaryPoint point)
    {
        if (!double.IsFinite(point.A) || !double.IsFinite(point.B) || !double.IsFinite(point.C) ||
            point.A < 0 || point.B < 0 || point.C < 0)
            throw new ArgumentOutOfRangeException(nameof(point), "Ternary components must be finite and nonnegative.");
        var scale = Math.Max(point.A, Math.Max(point.B, point.C));
        if (scale == 0) throw new ArgumentOutOfRangeException(nameof(point), "At least one component must be positive.");
        var a = point.A / scale; var b = point.B / scale; var c = point.C / scale;
        var total = a + b + c;
        return new(a / total, b / total, c / total);
    }

    /// <summary>Projects to an equilateral triangle: A is the top, B the left base, C the right base.</summary>
    public static ChartPoint ProjectTernary(TernaryPoint point)
    {
        var normalized = NormalizeTernary(point);
        return new(normalized.C + normalized.A / 2, normalized.A * (Math.Sqrt(3) / 2));
    }

    /// <summary>Returns normalized components. Points beyond the triangle (apart from roundoff) are rejected.</summary>
    public static TernaryPoint UnprojectTernary(ChartPoint point)
    {
        ValidatePoint(point, nameof(point));
        var a = point.Y / (Math.Sqrt(3) / 2);
        var c = point.X - a / 2;
        var b = 1 - a - c;
        if (a < -EngineeringEpsilon || b < -EngineeringEpsilon || c < -EngineeringEpsilon ||
            a > 1 + EngineeringEpsilon || b > 1 + EngineeringEpsilon || c > 1 + EngineeringEpsilon)
            throw new ArgumentOutOfRangeException(nameof(point), "Point is outside the ternary triangle.");
        return NormalizeTernary(new(Math.Clamp(a, 0, 1), Math.Clamp(b, 0, 1), Math.Clamp(c, 0, 1)));
    }

    /// <summary>
    /// Converts physical ohms or siemens to the complex reflection coefficient. Impedance uses
    /// (Z-Z0)/(Z+Z0); admittance uses (1-Z0*Y)/(1+Z0*Y). Active negative real components are rejected.
    /// </summary>
    public static ChartPoint ProjectSmith(SmithPoint point, double referenceImpedance = 50,
        SmithQuantity quantity = SmithQuantity.Impedance)
    {
        ValidateSmith(referenceImpedance, quantity);
        if (!double.IsFinite(point.R) || !double.IsFinite(point.X) || point.R < 0)
            throw new ArgumentOutOfRangeException(nameof(point), "Smith values require finite coordinates and a nonnegative real component.");
        double r, x, reference;
        if (quantity == SmithQuantity.Admittance && referenceImpedance < 1)
        {
            // This branch never overflows the products, and avoids computing an overflowing 1/Z0.
            r = point.R * referenceImpedance;
            x = point.X * referenceImpedance;
            reference = 1;
        }
        else
        {
            reference = quantity == SmithQuantity.Impedance ? referenceImpedance : 1 / referenceImpedance;
            var scale = Math.Max(reference, Math.Max(point.R, Math.Abs(point.X)));
            r = point.R / scale; x = point.X / scale; reference /= scale;
        }
        var result = DivideComplex(r - reference, x, r + reference, x);
        if (quantity == SmithQuantity.Admittance) result = new(-result.X, -result.Y);
        ValidateRepresentableSmithProjection(result);
        return result;
    }

    /// <summary>Converts a passive unit-disk reflection coefficient to physical ohms or siemens. Infinite open/short limits are rejected.</summary>
    public static SmithPoint UnprojectSmith(ChartPoint reflection, double referenceImpedance = 50,
        SmithQuantity quantity = SmithQuantity.Impedance)
    {
        ValidateSmith(referenceImpedance, quantity);
        ValidatePoint(reflection, nameof(reflection));
        var magnitudeSquared = reflection.X * reflection.X + reflection.Y * reflection.Y;
        if (!double.IsFinite(magnitudeSquared) || magnitudeSquared > 1 + 8 * 2.2204460492503131e-16 ||
            Math.Abs(reflection.X) > 1 || Math.Abs(reflection.Y) > 1)
            throw new ArgumentOutOfRangeException(nameof(reflection), "A passive Smith reflection must be in the unit disk.");
        if (quantity == SmithQuantity.Admittance) reflection = new(-reflection.X, -reflection.Y);
        var normalized = DivideComplex(1 + reflection.X, reflection.Y, 1 - reflection.X, -reflection.Y);
        // Roundoff on a unit-circle point can produce a tiny negative real component.
        var real = normalized.X;
        if (real < 0 && real >= -EngineeringEpsilon * Math.Max(1, Math.Abs(normalized.Y))) real = 0;
        if (real < 0) throw new ArgumentOutOfRangeException(nameof(reflection), "Reflection corresponds to an active component.");
        var r = quantity == SmithQuantity.Impedance ? real * referenceImpedance : real / referenceImpedance;
        var x = quantity == SmithQuantity.Impedance ? normalized.Y * referenceImpedance : normalized.Y / referenceImpedance;
        if (!double.IsFinite(r) || !double.IsFinite(x))
            throw new OverflowException("The physical Smith value cannot be represented by finite doubles.");
        return new(r, x);
    }

    /// <summary>
    /// Samples constant normalized real/reactive curves for a passive Smith grid. The outer circle is exact;
    /// other curves approach their infinite limit using a tangent parameterization and include that limit.
    /// </summary>
    public static IReadOnlyList<EngineeringPath> SmithGrid(SmithQuantity quantity = SmithQuantity.Impedance,
        int sampleCount = 73, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(quantity)) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (sampleCount < 9 || sampleCount > MaximumCount / 17)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        cancellationToken.ThrowIfCancellationRequested();
        var paths = new List<EngineeringPath>();
        var outer = new ChartPoint[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var angle = 2 * Math.PI * i / (sampleCount - 1);
            outer[i] = new(Math.Cos(angle), Math.Sin(angle));
        }
        paths.Add(new("unit circle", outer));
        var realAxis = new ChartPoint[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var x = -1 + 2d * i / (sampleCount - 1);
            realAxis[i] = new(quantity == SmithQuantity.Impedance ? x : -x, 0);
        }
        paths.Add(new("imaginary=0", realAxis));
        double[] levels = [.2, .5, 1, 2, 5];
        foreach (var real in levels)
        {
            var points = new ChartPoint[sampleCount];
            points[0] = points[^1] = quantity == SmithQuantity.Impedance ? new(1, 0) : new(-1, 0);
            for (var i = 1; i < sampleCount - 1; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var imaginary = Math.Tan(Math.PI * ((double)i / (sampleCount - 1) - .5));
                points[i] = ProjectSmith(new(real, imaginary), 1, quantity);
            }
            paths.Add(new($"real={real.ToString(CultureInfo.InvariantCulture)}", points));
        }
        foreach (var imaginaryMagnitude in levels)
        foreach (var sign in new[] { -1d, 1d })
        {
            var imaginary = sign * imaginaryMagnitude;
            var points = new ChartPoint[sampleCount];
            for (var i = 0; i < sampleCount - 1; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var real = Math.Tan(Math.PI / 2 * i / (sampleCount - 1));
                points[i] = ProjectSmith(new(real, imaginary), 1, quantity);
            }
            points[^1] = quantity == SmithQuantity.Impedance ? new(1, 0) : new(-1, 0);
            paths.Add(new($"imaginary={imaginary.ToString(CultureInfo.InvariantCulture)}", points));
        }
        return Array.AsReadOnly(paths.ToArray());
    }

    private static void ValidateSmith(double referenceImpedance, SmithQuantity quantity)
    {
        if (!double.IsFinite(referenceImpedance) || referenceImpedance <= 0)
            throw new ArgumentOutOfRangeException(nameof(referenceImpedance));
        if (!Enum.IsDefined(quantity)) throw new ArgumentOutOfRangeException(nameof(quantity));
    }

    private static void ValidateRepresentableSmithProjection(ChartPoint point)
    {
        if (!IsFinite(point)) throw new OverflowException("The reflection coefficient is not representable.");
    }

    private static ChartPoint DivideComplex(double real, double imaginary, double denominatorReal, double denominatorImaginary)
    {
        var scale = Math.Max(Math.Abs(denominatorReal), Math.Abs(denominatorImaginary));
        if (scale == 0) throw new ArgumentOutOfRangeException("point", "The Smith transform is singular at this point.");
        var c = denominatorReal / scale; var d = denominatorImaginary / scale;
        var divisor = c * c + d * d;
        // Scale the numerator first: operands here are bounded for forward and inverse Smith maps.
        var a = real / scale; var b = imaginary / scale;
        var result = new ChartPoint((a * c + b * d) / divisor, (b * c - a * d) / divisor);
        if (!IsFinite(result)) throw new OverflowException("The complex quotient cannot be represented by finite doubles.");
        return result;
    }
}
