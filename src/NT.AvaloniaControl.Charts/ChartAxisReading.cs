namespace NT.AvaloniaControl.Charts;

/// <summary>Crosshair line/label direction; MVVM axis readings retain both orientations.</summary>
public enum ChartCrosshairDirection { Both, Horizontal, Vertical }

/// <summary>A raw axis value inverse-projected from the primary hover's chart-local position.</summary>
public sealed record ChartAxisReading(ChartAxis Axis, bool IsHorizontal, double Value, string Text);
