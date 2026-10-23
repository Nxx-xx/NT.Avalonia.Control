using System.Globalization;
using Avalonia;

namespace NT.AvaloniaControl.Charts;

public abstract partial class ChartSeries
{
    private bool IsFinancialMappingProperty(AvaloniaProperty property) => property == FinancialSeries.TimePathProperty ||
        property == FinancialSeries.OpenPathProperty || property == FinancialSeries.ClosePathProperty ||
        property == FinancialSeries.VolumePathProperty || property == FinancialSeries.GapPathProperty ||
        property == FinancialSeries.TransformProperty || property == FinancialSeries.BoxSizeProperty ||
        property == FinancialSeries.MaximumGeneratedSamplesProperty ||
        property == FinancialSeries.ReversalBoxesProperty || property == FinancialSeries.ReversalAmountProperty;

    private ChartDataSnapshot CreateFinancialSnapshot(long version)
    {
        var financial = (FinancialSeries)this;
        var entries = _entries.ToArray();
        var issues = new List<ChartDataIssue>();
        IReadOnlyList<ChartFinancialSample> samples;
        var precomputed = entries.All(entry => entry.Item is ChartFinancialSample);
        if (precomputed)
        {
            var admitted = 0L;
            foreach (var entry in entries)
            {
                var sample = (ChartFinancialSample)entry.Item!;
                admitted = checked(admitted + (sample.Kind == ChartFinancialTransformKind.PointAndFigure ? sample.BoxCount : 1L));
                if (admitted > financial.MaximumGeneratedSamples)
                    throw new InvalidOperationException("Precomputed financial samples exceed the configured generated-glyph budget.");
            }
            samples = entries.Select(entry => (ChartFinancialSample)entry.Item!).ToArray();
        }
        else
        {
            var raw = new ChartFinancialPoint[entries.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                var item = entries[index].Item;
                if (item is ChartFinancialPoint direct) raw[index] = direct;
                else
                {
                    var timeValue = Read(item, financial.TimePath, index, issues);
                    var time = timeValue switch { DateTimeOffset offset => offset, DateTime date when date.Kind != DateTimeKind.Unspecified => new DateTimeOffset(date),
                        _ => throw new ArgumentException($"Financial TimePath at source index {index} must be DateTimeOffset or an explicitly zoned DateTime.") };
                    raw[index] = new(time, Optional(item, financial.OpenPath, index, issues) ?? double.NaN,
                        Optional(item, HighPath, index, issues) ?? double.NaN, Optional(item, LowPath, index, issues) ?? double.NaN,
                        Optional(item, financial.ClosePath, index, issues) ?? double.NaN, Optional(item, financial.VolumePath, index, issues),
                        !string.IsNullOrWhiteSpace(financial.GapPath) && Read(item, financial.GapPath, index, issues) is true);
                }
                var value = raw[index];
                if (!value.IsGap && (!double.IsFinite(value.Open) || !double.IsFinite(value.High) || !double.IsFinite(value.Low) || !double.IsFinite(value.Close) ||
                    value.Low > Math.Min(value.Open, value.Close) || value.High < Math.Max(value.Open, value.Close) || value.Low > value.High ||
                    value.Volume is { } volume && (!double.IsFinite(volume) || volume < 0)))
                {
                    ReportIssue(new(index, financial.ClosePath, "Financial OHLC must be finite and ordered Low ≤ Open/Close ≤ High; volume must be finite and nonnegative. The record is a segment break."), issues);
                    raw[index] = value with { IsGap = true };
                }
            }
            samples = financial.TransformPoints(raw);
        }
        var points = new ChartDataPoint[samples.Count];
        var explicitIds = string.IsNullOrWhiteSpace(IdPath) ? null : new Dictionary<int, object>();
        if (explicitIds is not null && !precomputed)
        {
            var unique = new HashSet<object>();
            for (var index = 0; index < entries.Length; index++)
            {
                var id = Read(entries[index].Item, IdPath, index, issues);
                if (id is null || !unique.Add(id)) throw new InvalidOperationException($"IdPath '{IdPath}' produced a null or duplicate identity at source index {index}.");
                explicitIds[index] = id;
            }
        }
        for (var ordinal = 0; ordinal < samples.Count; ordinal++)
        {
            var sample = samples[ordinal];
            object? item = precomputed ? sample.SourceEnd : entries[sample.SourceEndIndex].Item;
            object? identity = precomputed ? sample : explicitIds?.GetValueOrDefault(sample.SourceEndIndex) ??
                (item is not null && !item.GetType().IsValueType ? item : entries[sample.SourceEndIndex].Identity);
            if (sample.Kind is not ChartFinancialTransformKind.Raw and not ChartFinancialTransformKind.HeikinAshi)
                identity = new FinancialGlyphIdentity(identity, sample.SourceStartIndex, sample.SourceEndIndex, ordinal);
            var y = this is FinancialVolumeSeries ? sample.Volume ?? double.NaN : sample.Close;
            var sourceIndex = sample.Kind is ChartFinancialTransformKind.Raw or ChartFinancialTransformKind.HeikinAshi ? sample.SourceEndIndex : ordinal;
            points[ordinal] = new(sourceIndex, identity, item, sample.Time, new(sample.Slot, y), !double.IsFinite(y), sample.High, sample.Low, sample.Volume)
            { WasEmpty = !double.IsFinite(y), Financial = sample, Value = sample.Close,
                RawPoint = new(sample.Time.ToUnixTimeMilliseconds(), this is FinancialVolumeSeries ? sample.SourceEnd.Volume ?? double.NaN : sample.SourceEnd.Close) };
        }
        if (version != SourceVersion) throw new ChartDataMappingException("source changed while mapping financial data; retry");
        return ChartDataSnapshot.FromOwned(version, points, issues);
    }

    private sealed record FinancialGlyphIdentity(object? SourceIdentity, int SourceStartIndex, int SourceEndIndex, int GlyphOrdinal);
}
