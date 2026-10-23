using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public enum ChartLegendPosition { Top, Bottom, Left, Right }
public sealed record ChartLegendItem(ChartSeries Series, string Text, IBrush Brush, ChartLegendSymbol Symbol, Rect Bounds)
{
    public bool IsVisible => Series.EffectiveVisibility;
}
public sealed class ChartLegendEventArgs(ChartSeries series) : EventArgs
{
    public ChartSeries Series { get; } = series;
    public bool Cancel { get; set; }
}

public abstract partial class ChartSeries
{
    public static readonly StyledProperty<bool> IsVisibleProperty = AvaloniaProperty.Register<ChartSeries, bool>(nameof(IsVisible), true, defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<bool> ShowInLegendProperty = AvaloniaProperty.Register<ChartSeries, bool>(nameof(ShowInLegend), true);
    /// <summary>Hidden series keep their palette identity and source subscriptions, but do not contribute ranges or selection.</summary>
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
    public bool ShowInLegend { get => GetValue(ShowInLegendProperty); set => SetValue(ShowInLegendProperty, value); }
}

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<IReadOnlyList<IBrush>?> ColorPaletteProperty = AvaloniaProperty.Register<CartesianChart, IReadOnlyList<IBrush>?>(nameof(ColorPalette));
    public static readonly StyledProperty<ChartLegendPosition> LegendPositionProperty = AvaloniaProperty.Register<CartesianChart, ChartLegendPosition>(nameof(LegendPosition), validate: Enum.IsDefined);
    public static readonly StyledProperty<Orientation> LegendOrientationProperty = AvaloniaProperty.Register<CartesianChart, Orientation>(nameof(LegendOrientation), Orientation.Horizontal, validate: Enum.IsDefined);
    public static readonly StyledProperty<bool> IsLegendInteractiveProperty = AvaloniaProperty.Register<CartesianChart, bool>(nameof(IsLegendInteractive), true);
    public static readonly StyledProperty<bool> IsLegendFloatingProperty = AvaloniaProperty.Register<CartesianChart, bool>(nameof(IsLegendFloating));
    public static readonly StyledProperty<HorizontalAlignment> LegendHorizontalAlignmentProperty = AvaloniaProperty.Register<CartesianChart, HorizontalAlignment>(nameof(LegendHorizontalAlignment), HorizontalAlignment.Left, validate: Enum.IsDefined);
    public static readonly StyledProperty<VerticalAlignment> LegendVerticalAlignmentProperty = AvaloniaProperty.Register<CartesianChart, VerticalAlignment>(nameof(LegendVerticalAlignment), VerticalAlignment.Top, validate: Enum.IsDefined);
    public static readonly StyledProperty<Thickness> FloatingLegendMarginProperty = AvaloniaProperty.Register<CartesianChart, Thickness>(nameof(FloatingLegendMargin), new Thickness(8),
        validate: value => double.IsFinite(value.Left) && double.IsFinite(value.Top) && double.IsFinite(value.Right) && double.IsFinite(value.Bottom) &&
            value.Left >= 0 && value.Top >= 0 && value.Right >= 0 && value.Bottom >= 0);
    public static readonly StyledProperty<IBrush?> FloatingLegendBackgroundProperty = AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(FloatingLegendBackground));
    public static readonly StyledProperty<Action<DrawingContext, ChartLegendItem>?> LegendRendererProperty = AvaloniaProperty.Register<CartesianChart, Action<DrawingContext, ChartLegendItem>?>(nameof(LegendRenderer));
    private readonly ConditionalWeakTable<ChartSeries, PaletteIdentity> _paletteIdentities = new();
    private long _nextPaletteIdentity;
    private sealed record PaletteIdentity(long Index);

    /// <summary>Replace this list to change the palette. Entries are assigned once per series instance, not by current collection order.</summary>
    public IReadOnlyList<IBrush>? ColorPalette { get => GetValue(ColorPaletteProperty); set => SetValue(ColorPaletteProperty, value); }
    public ChartLegendPosition LegendPosition { get => GetValue(LegendPositionProperty); set => SetValue(LegendPositionProperty, value); }
    public Orientation LegendOrientation { get => GetValue(LegendOrientationProperty); set => SetValue(LegendOrientationProperty, value); }
    public bool IsLegendInteractive { get => GetValue(IsLegendInteractiveProperty); set => SetValue(IsLegendInteractiveProperty, value); }
    /// <summary>Place the legend inside the final plot without reserving axis or series space.</summary>
    public bool IsLegendFloating { get => GetValue(IsLegendFloatingProperty); set => SetValue(IsLegendFloatingProperty, value); }
    /// <summary>Alignment along the top/bottom edge. Stretch uses the same origin as Left.</summary>
    public HorizontalAlignment LegendHorizontalAlignment { get => GetValue(LegendHorizontalAlignmentProperty); set => SetValue(LegendHorizontalAlignmentProperty, value); }
    /// <summary>Alignment along the left/right edge. Stretch uses the same origin as Top.</summary>
    public VerticalAlignment LegendVerticalAlignment { get => GetValue(LegendVerticalAlignmentProperty); set => SetValue(LegendVerticalAlignmentProperty, value); }
    public Thickness FloatingLegendMargin { get => GetValue(FloatingLegendMarginProperty); set => SetValue(FloatingLegendMarginProperty, value); }
    /// <summary>Floating legend fill; null uses the chart Background.</summary>
    public IBrush? FloatingLegendBackground { get => GetValue(FloatingLegendBackgroundProperty); set => SetValue(FloatingLegendBackgroundProperty, value); }
    /// <summary>Optional bounded drawing callback for each legend item; use Bounds to clip custom content.</summary>
    public Action<DrawingContext, ChartLegendItem>? LegendRenderer { get => GetValue(LegendRendererProperty); set => SetValue(LegendRendererProperty, value); }
    public IReadOnlyList<ChartLegendItem> LegendItems
    {
        get
        {
            var scene = GetScene();
            return UsesLegendPresentation ? VisiblePresentedLegendItems() : scene?.Legend ?? Array.Empty<ChartLegendItem>();
        }
    }
    public event EventHandler<ChartLegendEventArgs>? LegendItemInvoked;

    public IBrush GetSeriesBrush(ChartSeries series)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(series);
        if (!_paletteIdentities.TryGetValue(series, out var identity))
        {
            identity = new(_nextPaletteIdentity++);
            _paletteIdentities.Add(series, identity);
        }
        if ((series.Stroke ?? series.Fill) is { } explicitBrush) return explicitBrush;
        var palette = ColorPalette is { Count: > 0 } custom ? custom : Palette;
        return palette[(int)(identity.Index % palette.Count)] ?? Palette[(int)(identity.Index % Palette.Length)];
    }

    public void ToggleSeriesVisibility(ChartSeries series)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(series);
        if (!ActiveSeries.Contains(series)) return;
        var args = new ChartLegendEventArgs(series); LegendItemInvoked?.Invoke(this, args);
        if (!args.Cancel && ActiveSeries.Contains(series)) series.SetCurrentValue(ChartSeries.IsVisibleProperty, !series.IsVisible);
    }

    private IReadOnlyList<ChartLegendItem> LayoutLegend(ChartSeries[] series, ref double left, ref double right, ref double top, ref double bottom)
    {
        if (!ShowLegend || IsLegendFloating) return Array.Empty<ChartLegendItem>();
        return LayoutLegendCore(series, ref left, ref right, ref top, ref bottom);
    }

    private IReadOnlyList<ChartLegendItem> LayoutLegendCore(ChartSeries[] series, ref double left, ref double right, ref double top, ref double bottom)
    {
        if (UsesLegendPresentation) return LayoutPresentedLegend(ref left, ref right, ref top, ref bottom);
        var side = LegendPosition is ChartLegendPosition.Left or ChartLegendPosition.Right;
        var availableWidth = Math.Max(1, right - left);
        var maxWidth = side ? Math.Min(availableWidth * .35, 220) : availableWidth;
        var items = CreateLegendItems(series, maxWidth, side || LegendOrientation == Orientation.Vertical, out var width, out var naturalHeight);
        if (items.Count == 0) return Array.Empty<ChartLegendItem>();
        var height = Math.Min(naturalHeight, Math.Max(0, bottom - top - (side ? 0 : 120)));
        if (height <= 0) return Array.Empty<ChartLegendItem>();
        var originX = LegendPosition == ChartLegendPosition.Right ? right - width : left;
        var originY = LegendPosition == ChartLegendPosition.Bottom ? bottom - height : top;
        if (side) originY += VerticalLegendOffset(bottom - top - height);
        else originX += HorizontalLegendOffset(availableWidth - width);
        var result = PositionLegendItems(items, new Rect(originX, originY, width, height));
        switch (LegendPosition)
        {
            case ChartLegendPosition.Top: top += height + 5; break;
            case ChartLegendPosition.Bottom: bottom -= height + 5; break;
            case ChartLegendPosition.Left: left += width + 8; break;
            case ChartLegendPosition.Right: right -= width + 8; break;
        }
        return result;
    }

    private IReadOnlyList<ChartLegendItem> LayoutFloatingLegend(ChartSeries[] series, Rect plot)
    {
        if (UsesLegendPresentation) return LayoutFloatingPresentedLegend(plot);
        var margin = FloatingLegendMargin;
        var left = Math.Min(margin.Left, plot.Width);
        var top = Math.Min(margin.Top, plot.Height);
        var right = Math.Min(margin.Right, plot.Width - left);
        var bottom = Math.Min(margin.Bottom, plot.Height - top);
        var available = new Rect(plot.Left + left, plot.Top + top, plot.Width - left - right, plot.Height - top - bottom);
        if (available.Width <= 0 || available.Height <= 0) return Array.Empty<ChartLegendItem>();
        var side = LegendPosition is ChartLegendPosition.Left or ChartLegendPosition.Right;
        var maxWidth = side ? Math.Min(available.Width * .35, 220) : available.Width;
        var items = CreateLegendItems(series, maxWidth, side || LegendOrientation == Orientation.Vertical, out var width, out var naturalHeight);
        if (items.Count == 0) return Array.Empty<ChartLegendItem>();
        var height = Math.Min(naturalHeight, available.Height);
        var x = LegendPosition == ChartLegendPosition.Right ? available.Right - width : available.Left;
        var y = LegendPosition == ChartLegendPosition.Bottom ? available.Bottom - height : available.Top;
        if (side) y += VerticalLegendOffset(available.Height - height);
        else x += HorizontalLegendOffset(available.Width - width);
        return PositionLegendItems(items, new Rect(x, y, width, height));
    }

    private List<ChartLegendItem> CreateLegendItems(ChartSeries[] series, double maxWidth, bool vertical, out double width, out double height)
    {
        var items = new List<ChartLegendItem>();
        var rowHeight = FontSize + 12;
        var x = 0d; var y = 0d; width = 0;
        foreach (var item in series)
        {
            if (!item.ShowInLegend) continue;
            var title = item.Title ?? $"系列 {Array.IndexOf(series, item) + 1}";
            var itemWidth = Math.Min(maxWidth, MakeText(title).Width + 40);
            if (items.Count > 0 && (vertical || x + itemWidth > maxWidth)) { y += rowHeight; x = 0; }
            items.Add(new(item, title, GetSeriesBrush(item), item.Renderer.LegendSymbol, new Rect(x, y, itemWidth, rowHeight)));
            width = Math.Max(width, x + itemWidth); x += itemWidth;
        }
        height = items.Count == 0 ? 0 : y + rowHeight;
        return items;
    }

    private double HorizontalLegendOffset(double free) => LegendHorizontalAlignment switch
    { HorizontalAlignment.Center => Math.Max(0, free) / 2, HorizontalAlignment.Right => Math.Max(0, free), _ => 0 };
    private double VerticalLegendOffset(double free) => LegendVerticalAlignment switch
    { VerticalAlignment.Center => Math.Max(0, free) / 2, VerticalAlignment.Bottom => Math.Max(0, free), _ => 0 };

    private static ChartLegendItem[] PositionLegendItems(List<ChartLegendItem> items, Rect clip) => items
        .Select(item => item with { Bounds = item.Bounds.Translate(new Vector(clip.Left, clip.Top)).Intersect(clip) })
        .Where(item => item.Bounds.Width > 0 && item.Bounds.Height > 0).ToArray();

    private ChartLegendItem? LegendItemAt(ChartScene scene, Point position)
    {
        if (UsesLegendPresentation)
        {
            foreach (var item in VisiblePresentedLegendItems()) if (item.Bounds.Contains(position)) return item;
            return null;
        }
        foreach (var item in scene.Legend) if (item.Bounds.Contains(position)) return item;
        return null;
    }

    private Rect LegendBounds(ChartScene scene)
    {
        if (UsesLegendPresentation) return _legendPresentationViewport;
        if (scene.Legend.Count == 0) return default;
        var bounds = scene.Legend[0].Bounds;
        for (var index = 1; index < scene.Legend.Count; index++) bounds = bounds.Union(scene.Legend[index].Bounds);
        return bounds;
    }

    private bool LegendPanelContains(ChartScene scene, Point position) => UsesLegendPresentation
        ? _legendPresentationViewport.Width > 0 && _legendPresentationViewport.Height > 0 && _legendPresentationViewport.Contains(position)
        : scene.Legend.Count != 0 && LegendBounds(scene).Contains(position);

    private void RenderLegend(DrawingContext context, ChartScene scene, bool floating = false)
    {
        if (UsesLegendPresentation) return;
        if (scene.Legend.Count != 0 && IsLegendFloating == floating) RenderLegendCore(context, scene);
    }

    private void RenderLegendCore(DrawingContext context, ChartScene scene)
    {
        if (IsLegendFloating && (FloatingLegendBackground ?? Background) is { } background)
        {
            context.DrawRectangle(background, null, LegendBounds(scene));
        }
        foreach (var legend in scene.Legend)
        {
            using var clip = context.PushClip(legend.Bounds);
            using var opacity = context.PushOpacity(legend.IsVisible ? 1 : .4);
            if (LegendRenderer is { } renderer) { renderer(context, legend); continue; }
            var origin = legend.Bounds.TopLeft;
            if (legend.Symbol == ChartLegendSymbol.Circle) context.DrawEllipse(legend.Brush, null, origin + new Vector(6, 7), 4, 4);
            else context.DrawRectangle(legend.Brush, null, new Rect(origin.X, origin.Y + 5, 12, legend.Symbol == ChartLegendSymbol.Rectangle ? 9 : 4), 2, 2);
            DrawLabel(context, legend.Text, origin + new Vector(20, 0), Math.Max(0, legend.Bounds.Width - 22));
            if (!legend.IsVisible) context.DrawLine(new Pen(Foreground ?? Brushes.Gray, 1), origin + new Vector(20, 7), new Point(legend.Bounds.Right - 3, origin.Y + 7));
        }
    }
}
