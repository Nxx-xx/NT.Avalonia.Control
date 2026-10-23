namespace NT.AvaloniaControl.Charts;

public sealed record BulletPoint(object Id, double Value, double Target, double Minimum = 0, double Maximum = 100, IReadOnlyList<double>? Ranges = null, string? Label = null);
public sealed record KpiPoint(object Id, double Value, double Previous, IReadOnlyList<double>? Trend = null, string? Label = null);
public sealed record CalendarPoint(object Id, DateOnly Date, double Value, string? Label = null);
public sealed record MatrixPoint(object Id, object Row, object Column, double Value, string? Text = null, string? Label = null);
public sealed record WafflePoint(object Id, double Value, string? Label = null);
public sealed record WordPoint(object Id, string Text, double Weight);
public sealed record RankingPoint(object Id, object Entity, object Period, double Value, string? Label = null);
public sealed record ParliamentParty(object Id, int Seats, string? Label = null);
public sealed record ThemeRiverPoint(object Id, object Stream, object Slot, double Value, string? Label = null);

public sealed record OperationalBullet(int SourceIndex, BulletPoint Source, double Position, double TargetPosition, IReadOnlyList<double> Boundaries);
public enum OperationalDeltaType { Percentage, Absolute }
public sealed record OperationalKpi(int SourceIndex, KpiPoint Source, double Change, double? RelativeChange, IReadOnlyList<double> Trend)
{
    /// <summary>Percentage deltas use fractions (0.125 means 12.5%). Explicit scalar Delta overrides this display field; Change and RelativeChange remain the separate Previous calculation.</summary>
    public double? DisplayDelta { get; init; }
    public OperationalDeltaType DeltaType { get; init; } = OperationalDeltaType.Percentage;
    public bool IsExplicitDelta { get; init; }
}
public sealed record OperationalCalendarCell(DateOnly Date, int Week, int Day, int? SourceIndex, CalendarPoint? Source);
public sealed record OperationalCalendar(IReadOnlyList<OperationalCalendarCell> Cells, DateOnly Start, DateOnly End, int Weeks, DayOfWeek FirstDayOfWeek);
public sealed record OperationalMatrixCell(int SourceIndex, MatrixPoint Source, int Row, int Column);
public sealed record OperationalMatrix(IReadOnlyList<object> Rows, IReadOnlyList<object> Columns, IReadOnlyList<OperationalMatrixCell> Cells);
public sealed record OperationalAllocation(int SourceIndex, WafflePoint Source, int Units, double Share);
public sealed record OperationalSeat(int SourceIndex, ParliamentParty Source, int SeatIndex, int Row, ChartPoint Position, double Radius);
public sealed record OperationalRank(int SourceIndex, RankingPoint Source, int EntityIndex, int PeriodIndex, double Rank, ChartPoint Position);
public sealed record OperationalRanking(IReadOnlyList<object> Entities, IReadOnlyList<object> Periods, IReadOnlyList<OperationalRank> Points);
public sealed record OperationalWord(int SourceIndex, WordPoint Source, double FontSize, double X, double Y, double Width, double Height);
public sealed record OperationalWords(IReadOnlyList<OperationalWord> Words, IReadOnlyList<int> OmittedIndices);
public enum OperationalRiverBaseline { Centered, Zero }
public sealed record OperationalRiverBand(int StreamIndex, object Stream, IReadOnlyList<int> SourceIndices, IReadOnlyList<ChartPoint> Lower, IReadOnlyList<ChartPoint> Upper);
public sealed record OperationalRiver(IReadOnlyList<object> Slots, IReadOnlyList<OperationalRiverBand> Bands, double MaximumTotal, OperationalRiverBaseline Baseline);

/// <summary>Owned source-preserving operational layouts. Normalized Y increases downward, except river values which retain their original numeric units.</summary>
public static class ChartOperational
{
    public const int MaximumCount = 20000;
    public const int MaximumCells = 65536;
    public const int MaximumWords = 256;
    public const int MaximumWordCharacters = 128;
    public const int MaximumWordWork = 2000000;
    private static T[] Snapshot<T>(IEnumerable<T> source, Func<T, object> id, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(source); token.ThrowIfCancellationRequested();
        var result = new List<T>(); var ids = new HashSet<object>();
        foreach (var value in source)
        { token.ThrowIfCancellationRequested(); if (result.Count == MaximumCount) throw new ArgumentOutOfRangeException(nameof(source), "Operational observation budget exceeded."); if (value is null || id(value) is not { } identity || !ids.Add(identity)) throw new ArgumentException("Every observation requires a unique non-null ID.", nameof(source)); result.Add(value); }
        return result.ToArray();
    }
    private static void Finite(double value, bool positive = false) { if (!double.IsFinite(value) || positive && value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Values must be finite; weights must be nonnegative."); }
    private static double Fraction(double value, double minimum, double maximum)
    { if (minimum == maximum) return .5; var scale = Math.Max(Math.Abs(minimum), Math.Abs(maximum)); return (value / scale - minimum / scale) / (maximum / scale - minimum / scale); }
    private static double Sum(IEnumerable<double> source)
    { var values = source.ToArray(); var scale = values.DefaultIfEmpty(0).Max(); if (scale == 0) return 0; var sum = 0d; var correction = 0d; foreach (var raw in values) { var next = raw / scale - correction; var total = sum + next; correction = total - sum - next; sum = total; } var value = sum * scale; if (!double.IsFinite(value)) throw new OverflowException("The total cannot be represented as a finite double."); return value; }
    public static IReadOnlyList<OperationalBullet> Bullets(IEnumerable<BulletPoint> source, CancellationToken cancellationToken = default)
    {
        var values = Snapshot(source, x => x.Id, cancellationToken); var output = new OperationalBullet[values.Length]; var total = 0;
        for (var i = 0; i < values.Length; i++)
        { cancellationToken.ThrowIfCancellationRequested(); var value = values[i]; Finite(value.Value); Finite(value.Target); Finite(value.Minimum); Finite(value.Maximum); if (value.Minimum >= value.Maximum) throw new ArgumentException("Bullet minimum must be less than maximum."); var boundaries = new List<double>(); var previous = value.Minimum;
          if (value.Ranges is { Count: > 256 }) throw new ArgumentOutOfRangeException(nameof(source), "Bullet ranges exceed 256.");
          var owned = new List<double>(); foreach (var boundary in value.Ranges ?? []) { cancellationToken.ThrowIfCancellationRequested(); if (owned.Count == 256 || total == MaximumCount) throw new ArgumentOutOfRangeException(nameof(source), "Combined bullet range budget exceeded."); total++; Finite(boundary); if (boundary <= previous || boundary > value.Maximum) throw new ArgumentException("Bullet boundaries must increase inside the scale."); owned.Add(boundary); boundaries.Add(Fraction(boundary, value.Minimum, value.Maximum)); previous = boundary; }
          output[i] = new(i, value with { Ranges = owned.AsReadOnly() }, Math.Clamp(Fraction(value.Value, value.Minimum, value.Maximum), 0, 1), Math.Clamp(Fraction(value.Target, value.Minimum, value.Maximum), 0, 1), boundaries.AsReadOnly()); }
        return Array.AsReadOnly(output);
    }
    public static IReadOnlyList<OperationalKpi> Kpis(IEnumerable<KpiPoint> source, CancellationToken cancellationToken = default)
    {
        var values = Snapshot(source, x => x.Id, cancellationToken); var output = new OperationalKpi[values.Length]; var total = 0;
        for (var i = 0; i < values.Length; i++) { cancellationToken.ThrowIfCancellationRequested(); var value = values[i]; Finite(value.Value); Finite(value.Previous); var change = value.Value - value.Previous; Finite(change); if ((value.Trend?.Count ?? 0) > MaximumCount - total) throw new ArgumentOutOfRangeException(nameof(source), "KPI trend budget exceeded."); var trend = new List<double>(); foreach (var point in value.Trend ?? []) { cancellationToken.ThrowIfCancellationRequested(); if (total == MaximumCount) throw new ArgumentOutOfRangeException(nameof(source), "Combined KPI trend budget exceeded."); total++; Finite(point); trend.Add(point); } double? relative = value.Previous == 0 ? null : change / Math.Abs(value.Previous); if (relative is { } number) Finite(number); var owned = trend.AsReadOnly(); output[i] = new(i, value with { Trend = owned }, change, relative, owned) { DisplayDelta = relative }; }
        return Array.AsReadOnly(output);
    }
    public static OperationalCalendar Calendar(IEnumerable<CalendarPoint> source, DayOfWeek firstDayOfWeek = DayOfWeek.Monday, DateOnly? start = null, DateOnly? end = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(firstDayOfWeek)) throw new ArgumentOutOfRangeException(nameof(firstDayOfWeek)); var values = Snapshot(source, x => x.Id, cancellationToken); var dates = new Dictionary<DateOnly, int>();
        for (var i = 0; i < values.Length; i++) { Finite(values[i].Value); if (!dates.TryAdd(values[i].Date, i)) throw new ArgumentException("Duplicate calendar dates require explicit caller aggregation."); }
        var first = start ?? (values.Length > 0 ? values.Min(x => x.Date) : new DateOnly(2000, 1, 1)); var last = end ?? (values.Length > 0 ? values.Max(x => x.Date) : first); if (last < first || last.DayNumber - first.DayNumber > 3660) throw new ArgumentOutOfRangeException(nameof(end), "Calendar range is limited to 3661 inclusive days.");
        var offset = ((int)first.DayOfWeek - (int)firstDayOfWeek + 7) % 7; var weeks = (last.DayNumber - first.DayNumber + offset) / 7 + 1; var cells = new List<OperationalCalendarCell>();
        for (var number = first.DayNumber; number <= last.DayNumber; number++) { cancellationToken.ThrowIfCancellationRequested(); var date = DateOnly.FromDayNumber(number); var slot = number - first.DayNumber + offset; var found = dates.TryGetValue(date, out var index); cells.Add(new(date, slot / 7, slot % 7, found ? index : null, found ? values[index] : null)); }
        return new(cells.AsReadOnly(), first, last, weeks, firstDayOfWeek);
    }
    public static OperationalMatrix Matrix(IEnumerable<MatrixPoint> source, CancellationToken cancellationToken = default)
    {
        var values = Snapshot(source, x => x.Id, cancellationToken); var rows = new List<object>(); var columns = new List<object>(); var ri = new Dictionary<object, int>(); var ci = new Dictionary<object, int>(); var seen = new HashSet<(int, int)>(); var cells = new List<OperationalMatrixCell>();
        for (var i = 0; i < values.Length; i++) { cancellationToken.ThrowIfCancellationRequested(); var value = values[i]; Finite(value.Value); if (value.Row is null || value.Column is null) throw new ArgumentException("Matrix row and column keys must be non-null."); if (!ri.TryGetValue(value.Row, out var row)) { ri.Add(value.Row, row = rows.Count); rows.Add(value.Row); } if (!ci.TryGetValue(value.Column, out var column)) { ci.Add(value.Column, column = columns.Count); columns.Add(value.Column); } if (!seen.Add((row, column))) throw new ArgumentException("Duplicate matrix cells require explicit caller aggregation."); if ((long)rows.Count * columns.Count > MaximumCells) throw new ArgumentOutOfRangeException(nameof(source), "Matrix grid budget exceeded."); cells.Add(new(i, value, row, column)); }
        return new(rows.AsReadOnly(), columns.AsReadOnly(), cells.AsReadOnly());
    }
    /// <summary>Largest remainder apportionment; exact ties favor source order. All-zero inputs allocate no units.</summary>
    public static IReadOnlyList<OperationalAllocation> Waffle(IEnumerable<WafflePoint> source, int units = 100, CancellationToken cancellationToken = default)
    {
        if (units < 1 || units > MaximumCells) throw new ArgumentOutOfRangeException(nameof(units)); var values = Snapshot(source, x => x.Id, cancellationToken); foreach (var value in values) Finite(value.Value, true); var maximum = values.Select(x => x.Value).DefaultIfEmpty(0).Max(); var sum = maximum == 0 ? 0 : values.Sum(x => x.Value / maximum); var counts = new int[values.Length]; var shares = new double[values.Length];
        for (var i = 0; i < values.Length; i++) { cancellationToken.ThrowIfCancellationRequested(); shares[i] = maximum == 0 ? 0 : values[i].Value / maximum / sum; counts[i] = (int)Math.Floor(shares[i] * units); }
        if (maximum > 0) { var remaining = units - counts.Sum(); foreach (var index in Enumerable.Range(0, values.Length).OrderByDescending(i => shares[i] * units - counts[i]).ThenBy(i => i).Take(remaining)) counts[index]++; }
        return Array.AsReadOnly(values.Select((x, i) => new OperationalAllocation(i, x, counts[i], shares[i])).ToArray());
    }
    /// <summary>Exact integer seat counts, distributed across concentric rows in proportion to arc length. Sum determines total; zero-seat parties remain in the caller's source.</summary>
    public static IReadOnlyList<OperationalSeat> Parliament(IEnumerable<ParliamentParty> source, int rows = 4, double innerRadiusFactor = .4, CancellationToken cancellationToken = default)
    {
        if (rows < 1 || rows > 64 || !double.IsFinite(innerRadiusFactor) || innerRadiusFactor <= 0 || innerRadiusFactor > 1 || rows > 1 && innerRadiusFactor == 1) throw new ArgumentOutOfRangeException(nameof(rows)); var values = Snapshot(source, x => x.Id, cancellationToken); if (values.Any(x => x.Seats < 0)) throw new ArgumentOutOfRangeException(nameof(source)); var total = values.Sum(x => (long)x.Seats); if (total > MaximumCells) throw new ArgumentOutOfRangeException(nameof(source), "Seat budget exceeded."); if (total == 0) return [];
        var radii = Enumerable.Range(0, rows).Select(i => rows == 1 ? 1 : innerRadiusFactor + (1 - innerRadiusFactor) * i / (rows - 1d)).ToArray(); var rowAllocation = Waffle(radii.Select((x, i) => new WafflePoint(i, x)), (int)total, cancellationToken); var locations = new List<(int Row, double Angle, ChartPoint Point, double Radius)>();
        var radialSpacing = rows == 1 ? .1 : (1 - innerRadiusFactor) / (rows - 1d); for (var row = 0; row < rows; row++) { var count = rowAllocation[row].Units; var radius = radii[row]; var gap = count < 2 ? .15 : radius * Math.Sin(Math.PI / (2 * count)); var seatRadius = Math.Min(.04, Math.Min(radialSpacing / 2.4, gap / 1.2)) * .5; for (var seat = 0; seat < count; seat++) { var angle = Math.PI * (1 - (seat + .5) / count); locations.Add((row, angle, new(.5 + .5 * radius * Math.Cos(angle), 1 - radius * Math.Sin(angle)), seatRadius)); } }
        var ordered = locations.OrderByDescending(x => x.Angle).ThenBy(x => x.Row).ToArray(); var output = new List<OperationalSeat>(); var ordinal = 0; for (var i = 0; i < values.Length; i++) for (var seat = 0; seat < values[i].Seats; seat++) { cancellationToken.ThrowIfCancellationRequested(); var position = ordered[ordinal++]; output.Add(new(i, values[i], seat, position.Row, position.Point, position.Radius)); } return output.AsReadOnly();
    }
    /// <summary>First-seen periods and entities. Value ranking uses competition ranks (1,1,3); missing periods remain gaps. Rank input must be positive.</summary>
    public static OperationalRanking Ranking(IEnumerable<RankingPoint> source, bool rankFromValues = true, bool slope = false, CancellationToken cancellationToken = default)
    {
        var values = Snapshot(source, x => x.Id, cancellationToken); var entities = new List<object>(); var periods = new List<object>(); var ei = new Dictionary<object, int>(); var pi = new Dictionary<object, int>(); var keys = new HashSet<(object, object)>();
        foreach (var value in values) { cancellationToken.ThrowIfCancellationRequested(); Finite(value.Value); if (value.Entity is null || value.Period is null || !keys.Add((value.Entity, value.Period))) throw new ArgumentException("Each entity-period pair must be unique and non-null."); if (!ei.ContainsKey(value.Entity)) { ei.Add(value.Entity, entities.Count); entities.Add(value.Entity); } if (!pi.ContainsKey(value.Period)) { pi.Add(value.Period, periods.Count); periods.Add(value.Period); } if (!slope && !rankFromValues && value.Value <= 0) throw new ArgumentException("Ranks must be positive."); }
        if (slope && periods.Count != 0 && periods.Count != 2) throw new ArgumentException("Slope requires exactly two periods."); var ranks = new double[values.Length];
        foreach (var group in Enumerable.Range(0, values.Length).GroupBy(i => values[i].Period)) { var ordered = group.OrderByDescending(i => values[i].Value).ThenBy(i => i).ToArray(); double previous = double.NaN; var rank = 0; for (var j = 0; j < ordered.Length; j++) { cancellationToken.ThrowIfCancellationRequested(); var i = ordered[j]; if (values[i].Value != previous) rank = j + 1; previous = values[i].Value; ranks[i] = slope || !rankFromValues ? values[i].Value : rank; } }
        var minimum = slope ? ranks.DefaultIfEmpty(0).Min() : 1; var maximum = ranks.DefaultIfEmpty(1).Max(); var points = values.Select((x, i) => new OperationalRank(i, x, ei[x.Entity], pi[x.Period], ranks[i], new(periods.Count < 2 ? .5 : pi[x.Period] / (periods.Count - 1d), slope ? 1 - Fraction(ranks[i], minimum, maximum) : Fraction(ranks[i], minimum, maximum)))).ToArray(); return new(entities.AsReadOnly(), periods.AsReadOnly(), Array.AsReadOnly(points));
    }
    /// <summary>Deterministic measured-rectangle shelf packing, descending weight then source order. Omitted words remain explicit; no text is silently shortened or combined.</summary>
    public static OperationalWords Words(IEnumerable<WordPoint> source, double width, double height, Func<string, double, (double Width, double Height)> measure, double minimumFontSize = 12, double maximumFontSize = 48, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(measure); Finite(width, true); Finite(height, true); if (!double.IsFinite(minimumFontSize) || !double.IsFinite(maximumFontSize) || minimumFontSize < 1 || maximumFontSize > 512 || maximumFontSize < minimumFontSize) throw new ArgumentOutOfRangeException(nameof(minimumFontSize)); var values = Snapshot(source, x => x.Id, cancellationToken); if (values.Length > MaximumWords) throw new ArgumentOutOfRangeException(nameof(source), "Word count budget exceeded."); foreach (var value in values) { Finite(value.Weight, true); if (string.IsNullOrWhiteSpace(value.Text) || value.Text.Length > MaximumWordCharacters) throw new ArgumentException("Words require 1..128 nonblank UTF-16 characters."); }
        var max = values.Select(x => x.Weight).DefaultIfEmpty(0).Max(); var output = new List<OperationalWord>(); var omitted = new List<int>(); double x = 0, y = 0, rowHeight = 0; var work = 0;
        foreach (var i in Enumerable.Range(0, values.Length).OrderByDescending(i => values[i].Weight).ThenBy(i => i)) { cancellationToken.ThrowIfCancellationRequested(); if (++work > MaximumWordWork) throw new ArgumentOutOfRangeException(nameof(source)); if (values[i].Weight == 0) { omitted.Add(i); continue; } var font = minimumFontSize + (maximumFontSize - minimumFontSize) * Math.Sqrt(values[i].Weight / max); var measured = measure(values[i].Text, font); Finite(measured.Width, true); Finite(measured.Height, true); var w = measured.Width + 4; var h = measured.Height + 4; if (w > width || h > height) { omitted.Add(i); continue; } if (x + w > width) { x = 0; y += rowHeight; rowHeight = 0; } if (y + h > height) { omitted.Add(i); continue; } output.Add(new(i, values[i], font, x, y, w, h)); x += w; rowHeight = Math.Max(rowHeight, h); }
        return new(output.AsReadOnly(), omitted.AsReadOnly());
    }
    /// <summary>Aligned complete streams, first-seen slot order. Centered baseline matches ThemeRiverStack; no wiggle optimizer is implied.</summary>
    public static OperationalRiver River(IEnumerable<ThemeRiverPoint> source, OperationalRiverBaseline baseline = OperationalRiverBaseline.Centered, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(baseline)) throw new ArgumentOutOfRangeException(nameof(baseline)); var values = Snapshot(source, x => x.Id, cancellationToken); var streams = new List<object>(); var slots = new List<object>(); var si = new Dictionary<object, int>(); var xi = new Dictionary<object, int>(); var pairs = new Dictionary<(object, object), int>();
        for (var i = 0; i < values.Length; i++) { var value = values[i]; cancellationToken.ThrowIfCancellationRequested(); Finite(value.Value, true); if (value.Stream is null || value.Slot is null || !pairs.TryAdd((value.Stream, value.Slot), i)) throw new ArgumentException("Every stream-slot pair must be unique and non-null."); if (!si.ContainsKey(value.Stream)) { si.Add(value.Stream, streams.Count); streams.Add(value.Stream); } if (!xi.ContainsKey(value.Slot)) { xi.Add(value.Slot, slots.Count); slots.Add(value.Slot); } }
        if ((long)streams.Count * slots.Count != values.Length) throw new ArgumentException("River streams must contain every aligned slot; aggregate or fill missing values explicitly."); var totals = slots.Select(slot => Sum(streams.Select(stream => values[pairs[(stream, slot)]].Value))).ToArray(); var max = totals.DefaultIfEmpty(0).Max(); var lower = totals.Select(total => baseline == OperationalRiverBaseline.Centered ? (max - total) / 2 : 0).ToArray(); var bands = new List<OperationalRiverBand>();
        for (var stream = 0; stream < streams.Count; stream++) { var indices = new int[slots.Count]; var lows = new ChartPoint[slots.Count]; var highs = new ChartPoint[slots.Count]; for (var slot = 0; slot < slots.Count; slot++) { cancellationToken.ThrowIfCancellationRequested(); var index = pairs[(streams[stream], slots[slot])]; indices[slot] = index; lows[slot] = new(slot, lower[slot]); var upper = lower[slot] + values[index].Value; Finite(upper); highs[slot] = new(slot, upper); lower[slot] = upper; } bands.Add(new(stream, streams[stream], Array.AsReadOnly(indices), Array.AsReadOnly(lows), Array.AsReadOnly(highs))); }
        return new(slots.AsReadOnly(), bands.AsReadOnly(), max, baseline);
    }
}
