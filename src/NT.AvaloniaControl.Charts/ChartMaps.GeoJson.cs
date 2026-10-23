using System.Text;
using System.Text.Json;

namespace NT.AvaloniaControl.Charts;

public static partial class ChartMaps
{
    /// <summary>Reads a bounded Polygon/MultiPolygon Feature or FeatureCollection, or a bare geometry.
    /// Feature IDs use idProperty in properties when configured, otherwise the feature id; absent IDs
    /// receive their zero-based feature index. IDs are strings or finite numbers. Unsupported geometry
    /// types, null geometry, extra coordinate dimensions and malformed rings reject explicitly.
    /// The original source is an owned JsonElement clone independent of the parser document.</summary>
    public static MapGeometrySnapshot ReadGeoJson(string json, string? idProperty = null, string? labelProperty = "name",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json); cancellationToken.ThrowIfCancellationRequested();
        if (json.Length > MaximumJsonBytes || Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
            throw new ArgumentOutOfRangeException(nameof(json), "GeoJSON UTF-8 byte budget exceeded.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaximumJsonDepth });
        var root = document.RootElement; RequireObject(root); var features = new List<MapFeature>(); var vertices = 0; var rings = 0;
        if (Type(root) == "FeatureCollection")
        {
            var array = Required(root, "features"); RequireArray(array);
            if (array.GetArrayLength() > MaximumFeatureCount) throw new ArgumentOutOfRangeException(nameof(json), "GeoJSON feature budget exceeded.");
            foreach (var feature in array.EnumerateArray()) AddFeature(feature, features, idProperty, labelProperty, ref vertices, ref rings, cancellationToken);
        }
        else if (Type(root) == "Feature") AddFeature(root, features, idProperty, labelProperty, ref vertices, ref rings, cancellationToken);
        else features.Add(new(0, ReadGeometry(root, ref vertices, ref rings, cancellationToken), source: root.Clone()));
        cancellationToken.ThrowIfCancellationRequested(); return Snapshot(features, cancellationToken);
    }
    private static void AddFeature(JsonElement feature, List<MapFeature> features, string? idProperty, string? labelProperty,
        ref int vertices, ref int rings, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); RequireObject(feature);
        if (Type(feature) != "Feature") throw new ArgumentException("A GeoJSON FeatureCollection must contain Features.");
        if (features.Count >= MaximumFeatureCount) throw new ArgumentOutOfRangeException(nameof(features));
        var hasProperties = feature.TryGetProperty("properties", out var properties) && properties.ValueKind != JsonValueKind.Null;
        if (hasProperties) RequireObject(properties);
        object id = features.Count;
        if (idProperty is not null)
        {
            if (!hasProperties || !properties.TryGetProperty(idProperty, out var property)) throw new ArgumentException("The configured GeoJSON feature ID property is missing.");
            id = ReadId(property);
        }
        else if (feature.TryGetProperty("id", out var value)) id = ReadId(value);
        string? label = null;
        if (labelProperty is not null && hasProperties && properties.TryGetProperty(labelProperty, out var name) && name.ValueKind != JsonValueKind.Null)
        {
            if (name.ValueKind != JsonValueKind.String) throw new ArgumentException("GeoJSON labels must be strings or null.");
            label = name.GetString();
        }
        features.Add(new(id, ReadGeometry(Required(feature, "geometry"), ref vertices, ref rings, token), label, feature.Clone()));
    }
    private static object ReadId(JsonElement id)
    {
        if (id.ValueKind == JsonValueKind.String) return id.GetString()!;
        if (id.ValueKind == JsonValueKind.Number)
        {
            if (id.TryGetInt64(out var integer)) return integer;
            if (id.TryGetDouble(out var number) && double.IsFinite(number)) return number;
        }
        throw new ArgumentException("GeoJSON IDs must be strings or finite numbers.");
    }
    private static MapPolygon[] ReadGeometry(JsonElement geometry, ref int vertices, ref int rings, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); RequireObject(geometry);
        var type = Type(geometry);
        if (type is not ("Polygon" or "MultiPolygon")) throw new NotSupportedException($"GeoJSON geometry '{type}' is unsupported; only Polygon and MultiPolygon are accepted.");
        var coordinates = Required(geometry, "coordinates"); RequireArray(coordinates);
        if (type == "Polygon") return [ReadPolygon(coordinates, ref vertices, ref rings, token)];
        if (coordinates.GetArrayLength() == 0) throw new ArgumentException("A GeoJSON MultiPolygon cannot be empty.");
        var result = new List<MapPolygon>();
        foreach (var polygon in coordinates.EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            if (result.Count >= MaximumRingCount) throw new ArgumentOutOfRangeException(nameof(geometry), "GeoJSON polygon budget exceeded.");
            result.Add(ReadPolygon(polygon, ref vertices, ref rings, token));
        }
        return result.ToArray();
    }
    private static MapPolygon ReadPolygon(JsonElement polygon, ref int vertices, ref int rings, CancellationToken token)
    {
        RequireArray(polygon); if (polygon.GetArrayLength() == 0) throw new ArgumentException("A GeoJSON Polygon requires an outer ring.");
        var result = new List<MapCoordinate[]>();
        foreach (var ring in polygon.EnumerateArray())
        {
            token.ThrowIfCancellationRequested(); RequireArray(ring); rings++;
            if (rings > MaximumRingCount || ring.GetArrayLength() > MaximumRingVertexCount) throw new ArgumentOutOfRangeException(nameof(polygon), "GeoJSON ring budget exceeded.");
            if ((long)vertices + ring.GetArrayLength() > MaximumVertexCount) throw new ArgumentOutOfRangeException(nameof(polygon), "GeoJSON vertex budget exceeded.");
            vertices += ring.GetArrayLength(); var points = new List<MapCoordinate>();
            foreach (var point in ring.EnumerateArray())
            {
                token.ThrowIfCancellationRequested(); RequireArray(point);
                if (point.GetArrayLength() != 2 || point[0].ValueKind != JsonValueKind.Number || point[1].ValueKind != JsonValueKind.Number
                    || !point[0].TryGetDouble(out var x) || !point[1].TryGetDouble(out var y) || !double.IsFinite(x) || !double.IsFinite(y))
                    throw new ArgumentException("GeoJSON coordinates must contain exactly two finite numbers.");
                points.Add(new(x, y));
            }
            result.Add(points.ToArray());
        }
        return new(result[0], result.Skip(1));
    }
    private static string Type(JsonElement value)
    {
        var type = Required(value, "type");
        if (type.ValueKind != JsonValueKind.String) throw new ArgumentException("A GeoJSON type must be a string.");
        return type.GetString()!;
    }
    private static JsonElement Required(JsonElement value, string name)
    {
        RequireObject(value);
        if (!value.TryGetProperty(name, out var property)) throw new ArgumentException($"GeoJSON property '{name}' is required.");
        return property;
    }
    private static void RequireObject(JsonElement value)
    { if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException("A GeoJSON object is required."); }
    private static void RequireArray(JsonElement value)
    { if (value.ValueKind != JsonValueKind.Array) throw new ArgumentException("A GeoJSON array is required."); }
}
