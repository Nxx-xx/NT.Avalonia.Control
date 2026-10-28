using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace NT.AvaloniaControl.Charts;

public enum ChartHighContrastMode { None, Light, Dark }

/// <summary>Opaque series colors with at least 7:1 sRGB contrast on the corresponding white or black canvas.</summary>
public static class ChartContrastPalettes
{
    public static IReadOnlyList<IBrush> Light { get; } = Make("#000000", "#003CB3", "#6B0075", "#8B0000", "#005C5C", "#4D4D00");
    public static IReadOnlyList<IBrush> Dark { get; } = Make("#FFFF00", "#00FFFF", "#FFFFFF", "#FF80FF", "#80FF80", "#FFBF80");
    private static IReadOnlyList<IBrush> Make(params string[] colors) =>
        Array.AsReadOnly(colors.Select(value => (IBrush)new ImmutableSolidColorBrush(Color.Parse(value))).ToArray());
}

public sealed partial class CartesianChart
{
    // Keep the original Cartesian field and CLR accessors for existing compiled consumers.
    // Both entry points identify the same styled property, registered on the shared base.
    public new static readonly StyledProperty<ChartHighContrastMode> HighContrastModeProperty =
        ChartBase.HighContrastModeProperty.AddOwner<CartesianChart>();
    /// <summary>Explicit opt-in contrast preset supplied by ChartsTheme or application styles. Does not probe OS preferences or replace local custom styles.</summary>
    public new ChartHighContrastMode HighContrastMode { get => GetValue(HighContrastModeProperty); set => SetValue(HighContrastModeProperty, value); }
}
