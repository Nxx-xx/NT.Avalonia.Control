namespace NT.AvaloniaControl.Charts;

public static partial class ChartAnalytics
{
    /// <summary>
    /// Fits Y=a+bX, a degree-1..10 polynomial, Y=exp(a+bX), Y=a+b*log(X), or Y=exp(a)*X^b.
    /// Regression omits nonfinite X/Y; exponential also omits Y&lt;=0, logarithmic X&lt;=0, and power either.
    /// At least degree+1 independent observations are required. Transformed models minimize transformed-Y
    /// residuals; R² is reported in the original Y domain. Samples span [minimum, maximum] (observed X
    /// extrema by default), with 2..4096 samples. Logarithmic/power prediction and sampling require X&gt;0.
    /// MovingAverage returns the aligned trailing SMA, ignores sampleCount and polynomialDegree, has no
    /// R² or Predict model, and does not accept explicit sampling bounds. No regression bridges are inferred
    /// from missing data: all eligible observations participate in one fit.
    /// </summary>
    public static ChartTrendlineResult FitTrend(IEnumerable<ChartPoint> points, ChartTrendlineType type = ChartTrendlineType.Linear,
        int polynomialDegree = 2, int period = 14, int sampleCount = 128, double? minimum = null, double? maximum = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (type == ChartTrendlineType.Polynomial && polynomialDegree is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(polynomialDegree));
        if (type != ChartTrendlineType.MovingAverage && sampleCount is < 2 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        if (minimum.HasValue && !double.IsFinite(minimum.Value)) throw new ArgumentOutOfRangeException(nameof(minimum));
        if (maximum.HasValue && !double.IsFinite(maximum.Value)) throw new ArgumentOutOfRangeException(nameof(maximum));
        if (type == ChartTrendlineType.MovingAverage)
        {
            ValidatePeriod(period);
            if (minimum.HasValue || maximum.HasValue)
                throw new ArgumentException("Moving average trends retain source alignment and cannot use sampling bounds.");
        }
        var input = ReadAnalytics(points, cancellationToken);
        var eligible = input.Where(point => EligibleTrendPoint(point, type)).ToArray();
        if (type == ChartTrendlineType.MovingAverage)
            return new(type, MovingAverageCore(input, period, ChartMovingAverageType.Simple, cancellationToken), eligible.Length,
                input.Length - eligible.Length, null, null);
        var degree = type == ChartTrendlineType.Polynomial ? polynomialDegree : 1;
        if (eligible.Length < degree + 1)
            throw new ArgumentException("The trend requires at least degree + 1 eligible independent observations.", nameof(points));
        var logarithmicX = type is ChartTrendlineType.Logarithmic or ChartTrendlineType.Power;
        var logarithmicY = type is ChartTrendlineType.Exponential or ChartTrendlineType.Power;
        var observedMinimum = eligible.Min(point => point.X);
        var observedMaximum = eligible.Max(point => point.X);
        var low = minimum ?? observedMinimum;
        var high = maximum ?? observedMaximum;
        if (low >= high) throw new ArgumentException("Trend sampling bounds must be strictly increasing.");
        if (logarithmicX && (low <= 0 || high <= 0))
            throw new ArgumentOutOfRangeException(nameof(minimum), "This model requires a strictly positive X domain.");
        var x = new double[eligible.Length];
        var y = new double[eligible.Length];
        for (var i = 0; i < eligible.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            x[i] = logarithmicX ? Math.Log(eligible[i].X) : eligible[i].X;
            y[i] = logarithmicY ? Math.Log(eligible[i].Y) : eligible[i].Y;
        }
        var xCenter = AnalyticsMidpoint(x.Min(), x.Max());
        var xScale = x.Max(value => Math.Abs(value - xCenter));
        if (xScale == 0) throw new ArgumentException("Trend coordinates are rank deficient.", nameof(points));
        var yCenter = AnalyticsMidpoint(y.Min(), y.Max());
        var yScale = y.Max(value => Math.Abs(value - yCenter));
        if (yScale == 0) yScale = 1;
        var coefficients = FitAnalyticsQr(x, y, degree, xCenter, xScale, yCenter, yScale, cancellationToken);
        double Predict(double coordinate)
        {
            if (logarithmicX && coordinate <= 0)
                throw new ArgumentOutOfRangeException(nameof(coordinate), "This model requires positive X.");
            var transformedX = logarithmicX ? Math.Log(coordinate) : coordinate;
            var normalizedX = AnalyticsDifferenceRatio(transformedX, xCenter, xScale);
            var value = coefficients[^1];
            for (var i = coefficients.Length - 2; i >= 0; i--)
                value = AnalyticsFinite(Math.FusedMultiplyAdd(value, normalizedX, coefficients[i]));
            value = AnalyticsFinite(Math.FusedMultiplyAdd(value, yScale, yCenter));
            if (!logarithmicY) return value;
            value = AnalyticsFinite(Math.Exp(value));
            if (value == 0) throw new OverflowException("The positive trend result is smaller than the finite positive double range.");
            return value;
        }
        var samples = new ChartPoint[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var coordinate = i == sampleCount - 1 ? high : AnalyticsBlend(low, high, (double)i / (sampleCount - 1));
            samples[i] = new(coordinate, Predict(coordinate));
        }
        var rSquared = AnalyticsRSquared(eligible, Predict, cancellationToken);
        return new(type, samples, eligible.Length, input.Length - eligible.Length, rSquared, Predict);
    }

    private static bool EligibleTrendPoint(ChartPoint point, ChartTrendlineType type) => ValidAnalyticsPoint(point)
        && (!(type is ChartTrendlineType.Exponential or ChartTrendlineType.Power) || point.Y > 0)
        && (!(type is ChartTrendlineType.Logarithmic or ChartTrendlineType.Power) || point.X > 0);

    private static double AnalyticsMidpoint(double low, double high) => AnalyticsBlend(low, high, .5);
    private static double AnalyticsDifferenceRatio(double first, double second, double scale)
    {
        var difference = first - second;
        return AnalyticsFinite(double.IsFinite(difference) ? difference / scale : first / scale - second / scale);
    }

    // Householder QR on a centered/scaled Vandermonde matrix. Never forms A^T A, whose conditioning
    // would square the fit's condition number. Normalized powers stay within [-1,1] while fitting.
    private static double[] FitAnalyticsQr(double[] x, double[] y, int degree, double xCenter, double xScale,
        double yCenter, double yScale, CancellationToken cancellationToken)
    {
        var columns = degree + 1;
        var matrix = new double[checked(x.Length * columns)];
        var right = new double[y.Length];
        for (var row = 0; row < x.Length; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var coordinate = AnalyticsDifferenceRatio(x[row], xCenter, xScale);
            var power = 1d;
            for (var column = 0; column < columns; column++)
            { matrix[row * columns + column] = power; power *= coordinate; }
            right[row] = AnalyticsDifferenceRatio(y[row], yCenter, yScale);
        }
        var rankTolerance = Math.Sqrt(x.Length) * columns * 2.5e-13;
        for (var column = 0; column < columns; column++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var norm = 0d;
            for (var row = column; row < x.Length; row++)
            {
                if ((row & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                norm = AnalyticsHypot(norm, matrix[row * columns + column]);
            }
            if (norm <= rankTolerance) throw new ArgumentException("Trend coordinates are rank deficient or numerically unresolved.", nameof(x));
            var diagonal = matrix[column * columns + column];
            var alpha = -Math.CopySign(norm, diagonal);
            var divisor = diagonal - alpha;
            var tau = (alpha - diagonal) / alpha;
            matrix[column * columns + column] = alpha;
            for (var row = column + 1; row < x.Length; row++)
                matrix[row * columns + column] /= divisor;
            for (var target = column + 1; target <= columns; target++)
            {
                var dot = target == columns ? right[column] : matrix[column * columns + target];
                var correction = 0d;
                for (var row = column + 1; row < x.Length; row++)
                {
                    if ((row & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                    var product = matrix[row * columns + column] * (target == columns ? right[row] : matrix[row * columns + target]);
                    var adjusted = product - correction;
                    var next = dot + adjusted;
                    correction = (next - dot) - adjusted;
                    dot = next;
                }
                dot *= tau;
                if (target == columns) right[column] -= dot;
                else matrix[column * columns + target] -= dot;
                for (var row = column + 1; row < x.Length; row++)
                {
                    if ((row & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (target == columns) right[row] -= matrix[row * columns + column] * dot;
                    else matrix[row * columns + target] -= matrix[row * columns + column] * dot;
                }
            }
        }
        var coefficients = new double[columns];
        for (var row = columns - 1; row >= 0; row--)
        {
            var value = right[row];
            for (var column = row + 1; column < columns; column++)
                value -= matrix[row * columns + column] * coefficients[column];
            coefficients[row] = AnalyticsFinite(value / matrix[row * columns + row]);
        }
        return coefficients;
    }

    private static double? AnalyticsRSquared(ChartPoint[] points, Func<double, double> predict, CancellationToken cancellationToken)
    {
        var center = AnalyticsMidpoint(points.Min(point => point.Y), points.Max(point => point.Y));
        var scale = points.Max(point => Math.Abs(point.Y - center));
        if (scale == 0) return null;
        var mean = 0d;
        for (var i = 0; i < points.Length; i++)
        {
            if ((i & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            mean += (AnalyticsDifferenceRatio(points[i].Y, center, scale) - mean) / (i + 1);
        }
        var residualNorm = 0d;
        var totalNorm = 0d;
        foreach (var point in points)
        {
            cancellationToken.ThrowIfCancellationRequested();
            residualNorm = AnalyticsHypot(residualNorm, AnalyticsDifferenceRatio(point.Y, predict(point.X), scale));
            totalNorm = AnalyticsHypot(totalNorm, AnalyticsDifferenceRatio(point.Y, center, scale) - mean);
        }
        var ratio = residualNorm / totalNorm;
        return AnalyticsFinite(1 - ratio * ratio);
    }
}
