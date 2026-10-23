using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace NT.AvaloniaControl.Charts;

internal static class ChartDataAccessor
{
    private static readonly ConcurrentDictionary<string, string[]> Segments = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(Type Type, string Property), Func<object, object?>> Readers = new();
    private static readonly object Missing = new();

    public static bool TryRead(object? item, string? path, out object? value)
    {
        value = item;
        if (string.IsNullOrWhiteSpace(path)) return true;
        try
        {
            foreach (var segment in Segments.GetOrAdd(path, static key => key.Split('.', StringSplitOptions.TrimEntries)))
            {
                if (value is null) return true;
                value = Readers.GetOrAdd((value.GetType(), segment), static key => Compile(key.Type, key.Property))(value);
                if (ReferenceEquals(value, Missing)) { value = null; return false; }
            }
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not ChartDataMappingException) { value = null; return false; }
    }

    public static IEnumerable<object> Owners(object? item, string? path)
    {
        if (item is null) yield break;
        yield return item;
        if (string.IsNullOrWhiteSpace(path)) yield break;
        foreach (var segment in Segments.GetOrAdd(path, static key => key.Split('.', StringSplitOptions.TrimEntries)))
        {
            if (!TryRead(item, segment, out item) || item is null) yield break;
            yield return item;
        }
    }

    public static bool TryNumber(object? value, out double number)
    {
        number = double.NaN;
        if (value is null || value is bool or char) return false;
        if (value is DateTimeOffset offset) number = offset.ToUnixTimeMilliseconds();
        else if (value is DateTime date)
        {
            var utc = date.Kind switch { DateTimeKind.Utc => date, DateTimeKind.Local => date.ToUniversalTime(), _ => DateTime.SpecifyKind(date, DateTimeKind.Utc) };
            number = new DateTimeOffset(utc).ToUnixTimeMilliseconds();
        }
        else if (value is string text) { if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return false; }
        else if (value is IConvertible convertible)
        {
            try { number = convertible.ToDouble(CultureInfo.InvariantCulture); }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException) { return false; }
        }
        return double.IsFinite(number);
    }

    private static Func<object, object?> Compile(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetMethod is null || property.GetIndexParameters().Length != 0) return _ => Missing;
        var item = Expression.Parameter(typeof(object), "item");
        var body = Expression.Convert(Expression.Property(Expression.Convert(item, type), property), typeof(object));
        return Expression.Lambda<Func<object, object?>>(body, item).Compile();
    }
}
