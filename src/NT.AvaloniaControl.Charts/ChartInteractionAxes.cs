namespace NT.AvaloniaControl.Charts;

/// <summary>The data axes affected by a chart's zoom or pan gesture.</summary>
[Flags]
public enum ChartInteractionAxes
{
    None = 0,
    X = 1,
    Y = 2,
    Both = X | Y,
}
