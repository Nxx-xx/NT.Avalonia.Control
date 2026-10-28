using System.Runtime.CompilerServices;
using System.Reflection;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts.Skia;

/// <summary>Opt-in rendering for the pinned Avalonia 12.0.3 Skia backend.</summary>
public static class ChartSkiaRenderingExtensions
{
    /// <summary>Registers the optional backend without changing renderer selection.</summary>
    public static AppBuilder WithChartsSkiaRendering(this AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Initialize();
        return builder;
    }

    /// <summary>Registers eligible circles; historical Core DLLs keep their original path.</summary>
    public static void Initialize()
    {
        // Do not reference the registry type in this method. This identical host
        // must also be able to load a genuine older Core during fidelity tests.
        if (!HasCurrentCoreContract()) return;
        Install();
    }

    private static bool HasCurrentCoreContract()
    {
        var core = typeof(CartesianChart).Assembly;
        var registry = core.GetType("NT.AvaloniaControl.Charts.ChartNativeCircleBackend", false);
        if (registry is null) return false;
        // Metadata inspection also rejects transitional Core builds whose
        // registry exists but predates the per-series factory/clear contract.
        // Keep the new interface out of this startup type's method signatures.
        var owner = typeof(ChartSeries).GetProperty("CanShareRenderingGeometryHistory", BindingFlags.Instance | BindingFlags.NonPublic);
        if (owner?.PropertyType != typeof(bool) || owner.GetGetMethod(nonPublic: true) is null) return false;
        foreach (var method in registry.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (method.Name != "Register" || method.ReturnType != typeof(void)) continue;
            var parameters = method.GetParameters();
            if (parameters.Length != 2 || parameters[1].ParameterType != typeof(Action<ChartSeries>)) continue;
            var factory = parameters[0].ParameterType;
            if (!factory.IsGenericType || factory.GetGenericTypeDefinition() != typeof(Func<,,,,,>)) continue;
            var arguments = factory.GetGenericArguments();
            if (arguments[0] == typeof(ChartSeries) && arguments[1] == typeof(Point[]) &&
                arguments[2] == typeof(double) && arguments[3] == typeof(IBrush) && arguments[4] == typeof(Pen) &&
                arguments[5].Assembly == core && arguments[5].FullName == "NT.AvaloniaControl.Charts.IChartNativeCirclePlan")
                return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Install() => ChartSkiaProviderInstaller.Install();
}

// Keep every signature involving the new Core contract outside the public
// startup type so its method table remains loadable with a historical Core.
internal static class ChartSkiaProviderInstaller
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Install() => ChartNativeCircleBackend.Register(Create, ChartNativeCircleOperation.ClearHistory);

    private static IChartNativeCirclePlan? Create(ChartSeries series, Point[] centers, double radius, IBrush fill, Pen stroke)
    {
        if (!ChartNativeCircleOperation.CanOptimize(fill, stroke) || !double.IsFinite(radius) || radius <= 0)
        {
            ChartNativeCircleOperation.ClearHistory(series);
            return null;
        }
        return new NativePlan(ChartNativeCircleOperation.CreateForSeries(series, centers, radius, fill, stroke));
    }

    private sealed class NativePlan(ChartNativeCircleOperation template) : IChartNativeCirclePlan
    {
        public long EstimatedBytes => template.EstimatedBytes;
        public long SharedHistoryEstimatedBytes => template.SharedHistoryEstimatedBytes;
        public bool SharedHistoryActive => template.SharedHistoryActive;
        public Avalonia.Rendering.SceneGraph.ICustomDrawOperation CreateFrame(IBrush fill, Pen stroke, bool snapshotPaint) =>
            template.WithPaint(fill, stroke, snapshotPaint);
    }
}
