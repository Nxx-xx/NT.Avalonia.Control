using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Skia;
using SkiaSharp;

namespace NT.AvaloniaControl.Charts.Skia;

// The pinned implementation exposes these getters publicly on internal types.
// Read only that public metadata; never read fields or invoke nonpublic members.
// Missing metadata (including trimmed/AOT deployments) leaves the normal native
// geometry renderer available instead of weakening the compatibility contract.
internal static class ChartSkiaPublicPaintBridge
{
    private sealed record Accessor(PropertyInfo? Property);
    private static readonly Assembly SkiaAssembly = typeof(ISkiaSharpApiLeaseFeature).Assembly;
    private static readonly ConcurrentDictionary<Type, Accessor> PathAccessors = new();
    private static readonly ConcurrentDictionary<Type, Accessor> OptionsAccessors = new();
    private static readonly bool CompatibleRuntime = IsCompatibleRuntime();

    internal static SKPath? TryGetFillPath(object geometry)
    {
        if (!CompatibleRuntime || geometry.GetType().Assembly != SkiaAssembly) return null;
        try
        {
            var accessor = PathAccessors.GetOrAdd(geometry.GetType(), static type => FindPublicGetter(type, "FillPath", typeof(SKPath)));
            return accessor.Property?.GetValue(geometry) as SKPath;
        }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException) { return null; }
    }

    internal static bool TryGetRenderOptions(object native, out RenderOptions options)
    {
        options = default;
        if (!CompatibleRuntime || native.GetType().Assembly != SkiaAssembly || native.GetType().FullName != "Avalonia.Skia.DrawingContextImpl") return false;
        try
        {
            var accessor = OptionsAccessors.GetOrAdd(native.GetType(), static type => FindPublicGetter(type, "RenderOptions", typeof(RenderOptions)));
            if (accessor.Property?.GetValue(native) is not RenderOptions value) return false;
            options = value;
            return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException) { return false; }
    }

    internal static SKColor SolidColor(ISolidColorBrush brush)
    {
        var color = brush.Color;
        // Fast rendering requires ambient CurrentOpacity == 1. Keep the same
        // multiplication and alpha truncation as pinned CreatePaint.
        return new(color.R, color.G, color.B, (byte)(color.A * brush.Opacity));
    }

    private static Accessor FindPublicGetter(Type type, string name, Type returnType)
    {
        if (type.Assembly != SkiaAssembly) return new(null);
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        return property?.PropertyType == returnType && property.GetMethod is { IsPublic: true } getter &&
            getter.DeclaringType?.Assembly == SkiaAssembly && property.GetIndexParameters().Length == 0
            ? new(property) : new(null);
    }

    private static bool IsCompatibleRuntime()
    {
        if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled) return false;
        try
        {
            var version = SkiaAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return version == "12.0.3" || version?.StartsWith("12.0.3+", StringComparison.Ordinal) == true;
        }
        catch (Exception error) when (error is not OutOfMemoryException and not StackOverflowException) { return false; }
    }
}
