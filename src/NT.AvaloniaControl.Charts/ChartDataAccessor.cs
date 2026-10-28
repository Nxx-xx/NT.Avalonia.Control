using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace NT.AvaloniaControl.Charts;

internal static class ChartDataAccessor
{
    private static readonly ConcurrentDictionary<string, string[]> Segments = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(Type Type, string Property), Func<object, object?>> Readers = new();
    private static readonly ConcurrentDictionary<(Type Type, string Path), bool> ObservableDescendants = new();
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

    // A mapping operation owns these small readers. Homogeneous rows can reuse
    // the compiled segment getter without hashing its type/path for every point;
    // nested/open/heterogeneous values still resolve their actual runtime type.
    internal sealed class PathReader
    {
        private readonly SegmentReader[] _segments;
        internal PathReader(string? path) => _segments = string.IsNullOrWhiteSpace(path) ? [] :
            Segments.GetOrAdd(path, static key => key.Split('.', StringSplitOptions.TrimEntries))
                .Select(static segment => new SegmentReader(segment)).ToArray();

        internal bool TryRead(object? item, out object? value)
        {
            value = item;
            try
            {
                foreach (var segment in _segments)
                {
                    if (value is null) return true;
                    value = segment.Read(value);
                    if (ReferenceEquals(value, Missing)) { value = null; return false; }
                }
                return true;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not ChartDataMappingException)
            { value = null; return false; }
        }

        private sealed class SegmentReader(string name)
        {
            private Type? _type;
            private Func<object, object?>? _reader;
            internal object? Read(object item)
            {
                var type = item.GetType();
                if (type != _type)
                {
                    var reader = Readers.GetOrAdd((type, name), static key => Compile(key.Type, key.Property));
                    _reader = reader; _type = type;
                }
                return _reader!(item);
            }
        }
    }

    // The root is observed separately. A sealed/value leaf such as double or
    // string cannot hide a notifying child, so do not invoke/box its getter just
    // to discover that fact for every row. Open types retain the runtime walk.
    public static bool MayHaveObservableDescendants(Type type, string path) =>
        ObservableDescendants.GetOrAdd((type, path), static key =>
        {
            try
            {
                var current = key.Type;
                foreach (var segment in Segments.GetOrAdd(key.Path, static path => path.Split('.', StringSplitOptions.TrimEntries)))
                {
                    var property = current.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
                    if (property?.GetMethod is null || property.GetIndexParameters().Length != 0) return false;
                    // Boxing Nullable<T> exposes T to the runtime reader.
                    current = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    if (typeof(INotifyPropertyChanged).IsAssignableFrom(current) || !current.IsValueType && !current.IsSealed) return true;
                }
                return false;
            }
            // Ambiguous or otherwise unusual reflected members keep the ordinary
            // guarded walk, which also owns the existing getter-failure policy.
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException) { return true; }
        });

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
