using System.Globalization;

namespace NT.AvaloniaControl.Charts;

/// <summary>Raw-domain bounds/ticks with axis transformation, direction and finite screen projection.</summary>
internal sealed class ChartScale : IChartAxisTransform
{
    private readonly ChartAxis? _axis;
    private readonly IReadOnlyList<string>? _labels;
    private readonly double _logBase;
    private readonly bool _inversed;
    private readonly double _transformedMinimum;
    private readonly double _transformedMaximum;
    private readonly ChartBreakMap? _breakMap;
    private readonly int _domainKind;
    private readonly bool _linearTransform;
    private readonly double _logDenominator;
    private static long _labelCopyCount;
    internal static long LabelCopyCount => Interlocked.Read(ref _labelCopyCount);
    internal int DomainKind => _domainKind;

    // The base scale takes one defensive copy of caller-owned category labels.
    // Subsequent views receive this private immutable wrapper and share its array.
    private sealed class OwnedLabels : IReadOnlyList<string>
    {
        private readonly string[] _values;
        public OwnedLabels(IReadOnlyList<string> source)
        {
            _values = source.ToArray();
            Interlocked.Increment(ref _labelCopyCount);
        }
        public int Count => _values.Length;
        public string this[int index] => _values[index];
        public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_values).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private ChartScale(double minimum, double maximum, double tickStep, IReadOnlyList<double> ticks,
        ChartAxis? axis, IReadOnlyList<string>? labels = null)
    {
        Minimum = minimum;
        Maximum = maximum;
        TickStep = tickStep;
        _axis = axis;
        _labels = labels is null or OwnedLabels ? labels : new OwnedLabels(labels);
        _logBase = axis is LogarithmicAxis log ? log.LogBase : 0;
        _domainKind = axis switch
        {
            null => 1,
            NumericAxis or CategoryAxis => 1,
            DateTimeAxis => 2,
            LogarithmicAxis => 3,
            _ when axis.GetType() == typeof(ChartAxis) => 1,
            _ => 0,
        };
        _linearTransform = _domainKind is 1 or 2;
        _logDenominator = _logBase > 0 ? Math.Log(_logBase) : 0;
        _inversed = axis?.IsInversed ?? false;
        _transformedMinimum = Transform(minimum);
        _transformedMaximum = Transform(maximum);
        MergedBreaks = axis?.GetMergedScaleBreaks().ToArray() ?? [];
        if (MergedBreaks.Count > 0)
            _breakMap = ChartBreakMap.Create(MergedBreaks, minimum, maximum, NormalizeUnbroken);
        Ticks = ticks.Where(value => !IsInScaleBreak(value)).ToArray();
        MinorTicks = BuildMinorTicks(axis, ticks);
    }

    public double Minimum { get; }
    public double Maximum { get; }
    /// <summary>Major interval; logarithmic scales express it in exponent units.</summary>
    public double TickStep { get; }
    public IReadOnlyList<double> Ticks { get; }
    public IReadOnlyList<double> MinorTicks { get; }
    public IReadOnlyList<ChartScaleBreakRange> MergedBreaks { get; }
    public bool IsInversed => _inversed;
    public IReadOnlyList<string>? CategoryLabels => _labels;
    internal bool HasStandardTransform => _axis is null || _axis.GetType() == typeof(ChartAxis) ||
        _axis is NumericAxis or CategoryAxis or DateTimeAxis or LogarithmicAxis;
    public bool IsValidValue(double value) => IsDomainValue(value) && !IsInScaleBreak(value);
    private bool IsDomainValue(double value) => double.IsFinite(value) && (_domainKind switch
    {
        1 => true,
        2 => value >= DateTimeAxis.MinimumUnixMilliseconds && value <= DateTimeAxis.MaximumUnixMilliseconds,
        3 => value > 0,
        _ => _axis?.IsValidValue(value) ?? true,
    });
    private bool IsInScaleBreak(double value) => ChartAxis.ContainsScaleBreak(MergedBreaks, value);
    public double Transform(double value) => _linearTransform ? value : _logBase > 0
        ? value > 0 && double.IsFinite(value) ? Math.Log(value) / _logDenominator : double.NaN
        : _axis?.Transform(value) ?? value;
    public double Inverse(double value) => _linearTransform ? value : _logBase > 0 ? Math.Pow(_logBase, value) : _axis?.Inverse(value) ?? value;

    public static ChartScale FromValues(IEnumerable<double> values, ChartAxis? axis = null,
        bool includeZero = false, double paddingFraction = 0.05, IReadOnlyList<string>? categoryLabels = null)
    {
        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;
        var breaks = axis?.GetMergedScaleBreaks() ?? [];
        foreach (var value in values)
        {
            if (!Valid(axis, value) || ChartAxis.ContainsScaleBreak(breaks, value)) continue;
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }
        return Create(minimum, maximum, axis, includeZero, paddingFraction, categoryLabels);
    }

    public static ChartScale Create(double minimum, double maximum, ChartAxis? axis = null,
        bool includeZero = false, double paddingFraction = 0.05, IReadOnlyList<string>? categoryLabels = null) =>
        CreateCore(minimum, maximum, axis, includeZero, paddingFraction, categoryLabels, false);

    /// <summary>Applies explicit view bounds while retaining the original axis transform and formatting.</summary>
    public static ChartScale CreateForView(ChartAxis axis, double minimum, double maximum,
        IReadOnlyList<string>? categoryLabels = null) =>
        CreateCore(minimum, maximum, axis, false, 0, categoryLabels, true);

    private static ChartScale CreateCore(double minimum, double maximum, ChartAxis? axis,
        bool includeZero, double paddingFraction, IReadOnlyList<string>? categoryLabels, bool view)
    {
        var labels = categoryLabels ?? axis?.Labels;
        if (axis is LogarithmicAxis logarithmic)
            return CreateLog(minimum, maximum, logarithmic, paddingFraction, view);
        var date = axis as DateTimeAxis;
        if (!Valid(axis, minimum) || !Valid(axis, maximum))
        {
            minimum = 0;
            maximum = date is null ? 1 : 86_400_000;
        }
        if (minimum > maximum) (minimum, maximum) = (maximum, minimum);
        if (includeZero && Valid(axis, 0))
        {
            minimum = Math.Min(minimum, 0);
            maximum = Math.Max(maximum, 0);
        }
        var fixedMinimum = view || axis?.Minimum is { } lower && Valid(axis, lower);
        var fixedMaximum = view || axis?.Maximum is { } upper && Valid(axis, upper);
        if (!view && fixedMinimum) minimum = axis!.Minimum!.Value;
        if (!view && fixedMaximum) maximum = axis!.Maximum!.Value;
        RepairOrder(ref minimum, ref maximum, fixedMinimum, fixedMaximum);
        if (minimum == maximum)
        {
            if (axis is CategoryAxis constantCategory)
            {
                var categoryPadding = constantCategory.Gap + (constantCategory.PlotMode == CategoryPlotMode.BetweenTicks ? .5 : 0);
                if (categoryPadding == 0 || fixedMinimum && fixedMaximum)
                {
                    if (!fixedMinimum || fixedMaximum) minimum = FiniteOrLimit(minimum - .5);
                    if (!fixedMaximum || fixedMinimum) maximum = FiniteOrLimit(maximum + .5);
                }
            }
            else if (date is not null)
            {
                var value = minimum;
                if (!fixedMinimum || fixedMaximum) minimum = Math.Max(DateTimeAxis.MinimumUnixMilliseconds, value - 86_400_000);
                if (!fixedMaximum || fixedMinimum) maximum = Math.Min(DateTimeAxis.MaximumUnixMilliseconds, value + 86_400_000);
            }
            else ExpandConstant(ref minimum, ref maximum, fixedMinimum, fixedMaximum);
        }
        if (!view && axis is CategoryAxis category)
        {
            var categoryPadding = category.Gap + (category.PlotMode == CategoryPlotMode.BetweenTicks ? .5 : 0);
            if (!fixedMinimum) minimum = FiniteOrLimit(minimum - categoryPadding);
            if (!fixedMaximum) maximum = FiniteOrLimit(maximum + categoryPadding);
            paddingFraction = 0;
        }
        var padding = double.IsFinite(paddingFraction) ? Math.Clamp(paddingFraction, 0, 0.5) : 0;
        var span = maximum - minimum;
        var margin = double.IsFinite(span) ? span * padding : (maximum * 0.5 - minimum * 0.5) * padding * 2;
        if (!fixedMinimum && !(includeZero && minimum == 0)) minimum = FiniteOrLimit(minimum - margin);
        if (!fixedMaximum && !(includeZero && maximum == 0)) maximum = FiniteOrLimit(maximum + margin);
        var tickCount = Math.Clamp(axis?.DesiredTickCount ?? 6, 2, 30);
        span = maximum - minimum;
        var roughStep = double.IsFinite(span) ? span / (tickCount - 1) :
            (maximum * 0.5 - minimum * 0.5) / (tickCount - 1) * 2;
        var step = ValidInterval(axis?.Interval) ? axis!.Interval!.Value : axis is CategoryAxis ? 1 : NiceStep(roughStep);
        if (labels is not null || axis is CategoryAxis) step = Math.Max(1, Math.Ceiling(step));
        if (!fixedMinimum && labels is null && axis is not CategoryAxis) minimum = RoundBound(minimum, step, true);
        if (!fixedMaximum && labels is null && axis is not CategoryAxis) maximum = RoundBound(maximum, step, false);
        if (date is not null)
        {
            minimum = Math.Clamp(minimum, DateTimeAxis.MinimumUnixMilliseconds, DateTimeAxis.MaximumUnixMilliseconds);
            maximum = Math.Clamp(maximum, DateTimeAxis.MinimumUnixMilliseconds, DateTimeAxis.MaximumUnixMilliseconds);
        }
        if (minimum >= maximum) ExpandConstant(ref minimum, ref maximum, false, false);
        if (date is not null)
        {
            minimum = Math.Clamp(minimum, DateTimeAxis.MinimumUnixMilliseconds, DateTimeAxis.MaximumUnixMilliseconds);
            maximum = Math.Clamp(maximum, DateTimeAxis.MinimumUnixMilliseconds, DateTimeAxis.MaximumUnixMilliseconds);
            var dateTicks = BuildDateTicks(minimum, maximum, date, out step);
            return new ChartScale(minimum, maximum, step, dateTicks, axis);
        }
        return new ChartScale(minimum, maximum, step, BuildTicks(minimum, maximum, step, labels), axis, labels);
    }

    private static ChartScale CreateLog(double minimum, double maximum, LogarithmicAxis axis, double paddingFraction, bool view)
    {
        if (!axis.IsValidValue(minimum) || !axis.IsValidValue(maximum)) (minimum, maximum) = (1, axis.LogBase);
        if (minimum > maximum) (minimum, maximum) = (maximum, minimum);
        var fixedMinimum = view || axis.Minimum is { } lower && axis.IsValidValue(lower);
        var fixedMaximum = view || axis.Maximum is { } upper && axis.IsValidValue(upper);
        if (!view && fixedMinimum) minimum = axis.Minimum!.Value;
        if (!view && fixedMaximum) maximum = axis.Maximum!.Value;
        RepairOrder(ref minimum, ref maximum, fixedMinimum, fixedMaximum);
        if (minimum == maximum)
        {
            var multiplier = Math.Sqrt(axis.LogBase);
            if (!fixedMinimum || fixedMaximum) minimum = Math.Max(double.Epsilon, minimum / multiplier);
            if (!fixedMaximum || fixedMinimum) maximum = Math.Min(double.MaxValue, maximum * multiplier);
            if (minimum == maximum)
            {
                minimum = Math.Max(double.Epsilon, Math.BitDecrement(minimum));
                maximum = Math.Min(double.MaxValue, Math.BitIncrement(maximum));
            }
        }
        var transformedMinimum = axis.Transform(minimum);
        var transformedMaximum = axis.Transform(maximum);
        var padding = double.IsFinite(paddingFraction) ? Math.Clamp(paddingFraction, 0, 0.5) : 0;
        var margin = (transformedMaximum - transformedMinimum) * padding;
        if (!fixedMinimum) transformedMinimum -= margin;
        if (!fixedMaximum) transformedMaximum += margin;
        var step = ValidInterval(axis.Interval) ? axis.Interval!.Value :
            Math.Max(1, NiceStep((transformedMaximum - transformedMinimum) / (Math.Clamp(axis.DesiredTickCount, 2, 30) - 1)));
        if (!fixedMinimum) transformedMinimum = RoundBound(transformedMinimum, step, true);
        if (!fixedMaximum) transformedMaximum = RoundBound(transformedMaximum, step, false);
        if (!fixedMinimum) minimum = Math.Max(double.Epsilon, Math.Min(double.MaxValue, axis.Inverse(transformedMinimum)));
        if (!fixedMaximum) maximum = Math.Max(double.Epsilon, Math.Min(double.MaxValue, axis.Inverse(transformedMaximum)));
        if (minimum >= maximum)
        {
            minimum = Math.Max(double.Epsilon, Math.BitDecrement(minimum));
            maximum = Math.Min(double.MaxValue, Math.BitIncrement(maximum));
        }
        transformedMinimum = axis.Transform(minimum);
        transformedMaximum = axis.Transform(maximum);
        var ticks = BuildTicks(transformedMinimum - step * 1e-10, transformedMaximum + step * 1e-10, step, null)
            .Select(axis.Inverse).Where(value => double.IsFinite(value) && value > 0 && value >= minimum && value <= maximum)
            .Distinct().Order().ToList();
        if (ticks.Count < 2) ticks = ticks.Append(minimum).Append(maximum).Distinct().Order().ToList();
        return new ChartScale(minimum, maximum, step, ticks, axis);
    }

    /// <summary>Bounds map to 0/1 (or 1/0 when inversed); outside values retain slope for clipping.</summary>
    public double Normalize(double value)
    {
        var fraction = NormalizeUnbroken(value);
        if (_breakMap is not null) fraction = _breakMap.Compress(fraction);
        fraction = double.IsNaN(fraction) ? 0 : Math.Clamp(fraction, -1_000_000, 1_000_000);
        return _inversed ? 1 - fraction : fraction;
    }

    private double NormalizeUnbroken(double value)
    {
        if (!IsDomainValue(value) || value == Minimum) return 0;
        if (value == Maximum) return 1;
        return _logBase > 0 ? LogRatio(value, Minimum) / LogRatio(Maximum, Minimum) :
            LinearFraction(Transform(value), _transformedMinimum, _transformedMaximum);
    }

    /// <summary>Converts a screen-oriented normalized position back into the raw data domain.</summary>
    public double Denormalize(double fraction)
    {
        if (double.IsNaN(fraction)) fraction = 0;
        fraction = Math.Clamp(fraction, -1_000_000, 1_000_000);
        if (_inversed) fraction = 1 - fraction;
        if (_breakMap is not null) fraction = _breakMap.Expand(fraction);
        if (fraction == 0) return Minimum;
        if (fraction == 1) return Maximum;
        var value = RawValueAtFraction(Minimum, Maximum, fraction);
        if (_axis is DateTimeAxis) value = Math.Clamp(value, DateTimeAxis.MinimumUnixMilliseconds, DateTimeAxis.MaximumUnixMilliseconds);
        return fraction is >= 0 and <= 1 ? Math.Clamp(value, Minimum, Maximum) : value;
    }

    private double RawValueAtFraction(double minimum, double maximum, double fraction)
    {
        if (fraction == 0) return minimum;
        if (fraction == 1) return maximum;
        double value;
        if (_logBase > 0)
        {
            var exponent = LogRatio(maximum, minimum) * fraction;
            value = Math.Abs(exponent) < 600 ? minimum * Math.Exp(exponent) :
                Math.Exp(Math.Log(minimum) * (1 - fraction) + Math.Log(maximum) * fraction);
            value = Math.Max(double.Epsilon, FiniteOrLimit(value));
        }
        else value = FiniteOrLimit(Inverse(Interpolate(Transform(minimum), Transform(maximum), fraction)));
        return value;
    }

    private IReadOnlyList<double> BuildMinorTicks(ChartAxis? axis, IReadOnlyList<double> majors)
    {
        if (axis is null || axis.MinorInterval is null && axis.MinorTickCount == 0) return [];
        IEnumerable<double> candidates;
        if (axis.MinorInterval is { } interval)
        {
            candidates = _logBase > 0 ?
                BuildTicks(Transform(Minimum), Transform(Maximum), interval, null, 1000).Select(Inverse) :
                BuildTicks(Minimum, Maximum, interval, null, 1000);
        }
        else
        {
            var points = new List<double>();
            var count = axis.MinorTickCount;
            var stride = Math.Max(1, (int)Math.Ceiling(Math.Max(0, majors.Count - 1) * count / 1000d));
            var ordinal = 0;
            for (var index = 1; index < majors.Count; index++)
                for (var minor = 1; minor <= count; minor++)
                    if (ordinal++ % stride == 0) points.Add(RawValueAtFraction(majors[index - 1], majors[index], minor / (count + 1d)));
            candidates = points;
        }
        var positions = majors.Select(NormalizeUnbroken).ToArray();
        return candidates.Where(value => IsValidValue(value) && value >= Minimum && value <= Maximum)
            .Where(value => !positions.Any(major => Math.Abs(major - NormalizeUnbroken(value)) <= 1e-10))
            .Distinct().Order().Take(1000).ToArray();
    }

    public double Map(double value, double start, double end) => Interpolate(start, end, Normalize(value));

    internal bool TryCreateInRangeLinearMapping(double dataMinimum, double dataMaximum,
        double start, double end, out ChartLinearMapping mapping)
    {
        mapping = default;
        var span = Maximum - Minimum;
        var distance = end - start;
        if (_axis is not null && _axis.GetType() != typeof(ChartAxis) && _axis is not NumericAxis ||
            _breakMap is not null || !double.IsFinite(span) || span <= 0 ||
            !double.IsFinite(dataMinimum) || !double.IsFinite(dataMaximum) || dataMinimum > dataMaximum ||
            dataMinimum < Minimum || dataMaximum > Maximum ||
            !double.IsFinite(start) || !double.IsFinite(end) || !double.IsFinite(distance) ||
            Math.Abs(start) >= 1_000_000 || Math.Abs(end) >= 1_000_000)
            return false;
        mapping = new(Minimum, Maximum, span, _inversed, start, end, distance);
        return true;
    }

    internal bool TryCreateBoundedLinearMapping(double dataMinimum, double dataMaximum,
        double start, double end, out ChartLinearMapping mapping)
    {
        mapping = default;
        var span = Maximum - Minimum;
        var distance = end - start;
        if (_axis is not null && _axis.GetType() != typeof(ChartAxis) && _axis is not NumericAxis ||
            _breakMap is not null || !double.IsFinite(span) || span <= 0 ||
            !double.IsFinite(dataMinimum) || !double.IsFinite(dataMaximum) || dataMinimum > dataMaximum ||
            !double.IsFinite(start) || !double.IsFinite(end) || !double.IsFinite(distance) ||
            Math.Abs(start) >= 1_000_000 || Math.Abs(end) >= 1_000_000)
            return false;

        // Prove both original clamps inert without using their clamped output.
        // Positive-span subtraction/division and FMA are monotone, so finite
        // endpoint envelopes also bound every original point between them.
        var firstOffset = dataMinimum - Minimum;
        var lastOffset = dataMaximum - Minimum;
        if (!double.IsFinite(firstOffset) || !double.IsFinite(lastOffset)) return false;
        var first = firstOffset / span;
        var last = lastOffset / span;
        if (!double.IsFinite(first) || !double.IsFinite(last) || first < -1_000_000 || last > 1_000_000)
            return false;
        if (_inversed) { first = 1 - first; last = 1 - last; }
        // Check the actual FMA even at fraction 0/1. Map's exact endpoint
        // shortcuts are separately covered by the start/end bounds above.
        var firstScreen = Math.FusedMultiplyAdd(distance, first, start);
        var lastScreen = Math.FusedMultiplyAdd(distance, last, start);
        if (!double.IsFinite(firstScreen) || !double.IsFinite(lastScreen) ||
            Math.Abs(firstScreen) > 1_000_000 || Math.Abs(lastScreen) > 1_000_000)
            return false;
        mapping = new(Minimum, Maximum, span, _inversed, start, end, distance);
        return true;
    }

    public string FormatLabel(double value, ChartAxis? axis = null)
    {
        axis ??= _axis;
        var labels = _labels ?? axis?.Labels;
        if (labels is not null)
        {
            var rounded = Math.Round(value);
            return rounded >= 0 && rounded < labels.Count && Math.Abs(value - rounded) < 1e-7
                ? labels[(int)rounded] ?? string.Empty : string.Empty;
        }
        if (axis is DateTimeAxis date)
        {
            if (!date.IsValidValue(value)) return string.Empty;
            var labelInterval = Math.Min(TickStep, Maximum - Minimum);
            var format = date.DateFormat ?? date.LabelFormat ?? (labelInterval >= 300 * 86_400_000d ? "yyyy" :
                labelInterval >= 28 * 86_400_000d ? "yyyy-MM" : labelInterval >= 86_400_000 ? "yyyy-MM-dd" :
                labelInterval >= 1000 ? "HH:mm:ss" : "HH:mm:ss.fff");
            try { return DateTimeAxis.FromUnixMilliseconds(value).ToString(format, CultureInfo.CurrentCulture); }
            catch (FormatException) { return DateTimeAxis.FromUnixMilliseconds(value).ToString("u", CultureInfo.CurrentCulture); }
        }
        if (!string.IsNullOrEmpty(axis?.LabelFormat))
        {
            try { return value.ToString(axis.LabelFormat, CultureInfo.CurrentCulture); }
            catch (FormatException) { }
        }
        var labelStep = _logBase > 0 ? Ticks.Zip(Ticks.Skip(1), (a, b) => b - a)
            .Where(step => step > 0 && double.IsFinite(step)).DefaultIfEmpty(Maximum - Minimum).Min() : TickStep;
        if (!double.IsFinite(labelStep) || labelStep <= 0) labelStep = double.Epsilon;
        if (Math.Abs(value) < labelStep * 1e-9) value = 0;
        var absolute = Math.Abs(value);
        if (absolute >= 1e9 || absolute > 0 && (absolute < 1e-4 || labelStep < 1e-8))
        {
            var decimals = Math.Clamp((int)Math.Ceiling(Math.Log10(absolute) - Math.Log10(labelStep)) + 1, 3, 16);
            return value.ToString("0." + new string('#', decimals) + "E+0", CultureInfo.CurrentCulture);
        }
        var digits = labelStep >= 1 ? 0 : Math.Clamp((int)Math.Ceiling(-Math.Log10(labelStep)), 0, 8);
        if (labelStep >= 1 && labelStep % 1 != 0) digits = 1;
        else if (digits < 8 && Math.Round(labelStep, digits) != labelStep) digits++;
        return value.ToString(digits == 0 ? "0" : "0." + new string('#', digits), CultureInfo.CurrentCulture);
    }

    private static IReadOnlyList<double> BuildDateTicks(double minimum, double maximum, DateTimeAxis axis, out double step)
    {
        const double day = 86_400_000;
        var rough = (maximum - minimum) / (Math.Clamp(axis.DesiredTickCount, 2, 30) - 1);
        var unit = axis.IntervalUnit;
        var count = ValidInterval(axis.Interval) ? axis.Interval!.Value : 1;
        if (unit == DateTimeIntervalUnit.Auto && !ValidInterval(axis.Interval))
        {
            if (rough >= 365 * day) { unit = DateTimeIntervalUnit.Year; count = Math.Max(1, NiceStep(rough / (365.2425 * day))); }
            else if (rough >= 28 * day) { unit = DateTimeIntervalUnit.Month; count = rough >= 90 * day ? 3 : 1; }
        }
        if (unit is DateTimeIntervalUnit.Month or DateTimeIntervalUnit.Year)
        {
            var calendarCount = (int)Math.Clamp(Math.Round(count), 1, unit == DateTimeIntervalUnit.Year ? 9999 : 119_988);
            step = calendarCount * day * (unit == DateTimeIntervalUnit.Year ? 365.2425 : 30.436875);
            var first = DateTimeAxis.FromUnixMilliseconds(minimum);
            DateTimeOffset cursor;
            if (unit == DateTimeIntervalUnit.Year)
            {
                var year = Math.Max(1, first.Year / calendarCount * calendarCount);
                cursor = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
            }
            else
            {
                var monthIndex = ((first.Year - 1) * 12 + first.Month - 1) / calendarCount * calendarCount;
                cursor = new DateTimeOffset(monthIndex / 12 + 1, monthIndex % 12 + 1, 1, 0, 0, 0, TimeSpan.Zero);
            }
            var ticks = new List<double>();
            for (var index = 0; index < 100; index++)
            {
                var raw = DateTimeAxis.ToUnixMilliseconds(cursor);
                if (raw > maximum) break;
                if (raw >= minimum) ticks.Add(raw);
                try { cursor = unit == DateTimeIntervalUnit.Year ? cursor.AddYears(calendarCount) : cursor.AddMonths(calendarCount); }
                catch (ArgumentOutOfRangeException) { break; }
            }
            return ticks.Count == 0 ? [minimum, maximum] : ticks;
        }
        var multiplier = unit switch
        {
            DateTimeIntervalUnit.Second => 1000,
            DateTimeIntervalUnit.Minute => 60_000,
            DateTimeIntervalUnit.Hour => 3_600_000,
            DateTimeIntervalUnit.Day => day,
            DateTimeIntervalUnit.Week => day * 7,
            _ => 1,
        };
        if (unit != DateTimeIntervalUnit.Auto || ValidInterval(axis.Interval)) step = Math.Min(double.MaxValue, multiplier * count);
        else
        {
            double[] intervals = [1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10_000, 15_000, 30_000,
                60_000, 120_000, 300_000, 600_000, 900_000, 1_800_000, 3_600_000, 7_200_000, 10_800_000,
                21_600_000, 43_200_000, day, day * 2, day * 7, day * 14];
            step = intervals.FirstOrDefault(candidate => candidate >= rough, day * 14);
        }
        var offset = unit == DateTimeIntervalUnit.Week ? day * 4 : 0;
        return BuildTicks(minimum - offset, maximum - offset, step, null).Select(value => value + offset).ToArray();
    }

    private static bool Valid(ChartAxis? axis, double value) => double.IsFinite(value) && (axis?.IsValidValue(value) ?? true);
    private static bool ValidInterval(double? value) => value is { } interval && double.IsFinite(interval) && interval > 0;
    private static double LogRatio(double value, double origin)
    {
        var ratio = value / origin;
        return ratio > 0 && double.IsFinite(ratio) ? Math.Log(ratio) : Math.Log(value) - Math.Log(origin);
    }
    private static double LinearFraction(double value, double minimum, double maximum)
    {
        var span = maximum - minimum;
        if (!double.IsFinite(span)) return (value * 0.5 - minimum * 0.5) / (maximum * 0.5 - minimum * 0.5);
        var offset = value - minimum;
        return double.IsFinite(offset) ? offset / span : (value * 0.5 - minimum * 0.5) / (span * 0.5);
    }
    private static double Interpolate(double start, double end, double fraction)
    {
        if (fraction == 0) return start;
        if (fraction == 1) return end;
        var distance = end - start;
        var result = double.IsFinite(distance) ? Math.FusedMultiplyAdd(distance, fraction, start) : start * (1 - fraction) + end * fraction;
        return double.IsNaN(result) ? 0 : FiniteOrLimit(result);
    }
    private static void RepairOrder(ref double minimum, ref double maximum, bool fixedMinimum, bool fixedMaximum)
    {
        if (minimum <= maximum) return;
        if (fixedMinimum && !fixedMaximum) maximum = minimum;
        else if (fixedMaximum && !fixedMinimum) minimum = maximum;
        else (minimum, maximum) = (maximum, minimum);
    }
    private static void ExpandConstant(ref double minimum, ref double maximum, bool fixedMinimum, bool fixedMaximum)
    {
        var value = minimum;
        var margin = value == 0 ? 1 : Math.Max(Math.Abs(value) * 0.05, double.Epsilon * 16);
        if (!fixedMinimum || fixedMaximum) minimum = FiniteOrLimit(value - margin);
        if (!fixedMaximum || fixedMinimum) maximum = FiniteOrLimit(value + margin);
        if (minimum == maximum)
        {
            minimum = FiniteOrLimit(Math.BitDecrement(value));
            maximum = FiniteOrLimit(Math.BitIncrement(value));
        }
    }
    private static double FiniteOrLimit(double value) => double.IsPositiveInfinity(value) ? double.MaxValue :
        double.IsNegativeInfinity(value) ? -double.MaxValue : value;
    private static double NiceStep(double roughStep)
    {
        if (roughStep <= 0 || double.IsNaN(roughStep)) return double.Epsilon;
        if (double.IsPositiveInfinity(roughStep)) return double.MaxValue;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
        if (magnitude == 0) magnitude = double.Epsilon;
        var fraction = roughStep / magnitude;
        return FiniteOrLimit(magnitude * (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 2.5 ? 2.5 : fraction <= 5 ? 5 : 10));
    }
    private static double RoundBound(double value, double step, bool down)
    {
        var quotient = value / step;
        return double.IsFinite(quotient) ? FiniteOrLimit((down ? Math.Floor(quotient) : Math.Ceiling(quotient)) * step) : value;
    }
    private static IReadOnlyList<double> BuildTicks(double minimum, double maximum, double step, IReadOnlyList<string>? labels, int maximumCount = 100)
    {
        var ticks = new List<double>();
        var countEstimate = (maximum - minimum) / step;
        if (!double.IsFinite(countEstimate) || countEstimate > maximumCount - 1)
        {
            var multiplier = Math.Ceiling(countEstimate / (maximumCount - 1));
            var thinned = step * multiplier;
            step = double.IsFinite(thinned) && thinned > 0 ? thinned :
                NiceStep((maximum * 0.5 - minimum * 0.5) / (maximumCount - 1) * 2);
        }
        var first = Math.Ceiling(minimum / step) * step;
        if (!double.IsFinite(first)) first = minimum;
        for (var index = 0; index < maximumCount; index++)
        {
            var value = first + step * index;
            if (!double.IsFinite(value)) value = (first * 0.5 + step * 0.5 * index) * 2;
            if (!double.IsFinite(value) || value > maximum) break;
            if (value < minimum) continue;
            if (Math.Abs(value) < step * 1e-9) value = 0;
            if (ticks.Count > 0 && value <= ticks[^1]) break;
            if (labels is null || value >= 0 && value < labels.Count) ticks.Add(value);
        }
        if (ticks.Count == 0 && labels is null) { ticks.Add(minimum); ticks.Add(maximum); }
        return ticks;
    }
}
