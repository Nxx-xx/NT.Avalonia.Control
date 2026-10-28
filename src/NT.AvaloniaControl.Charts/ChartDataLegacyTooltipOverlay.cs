using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Keeps the family's painted tooltip above permanent control-based labels and legends.</summary>
internal sealed class ChartDataLegacyTooltipOverlay : Control
{
    private readonly Action<DrawingContext> _draw;
    internal ChartDataLegacyTooltipOverlay(Action<DrawingContext> draw)
    {
        _draw = draw; Name = "PART_ChartDataLegacyTooltipOverlay"; IsHitTestVisible = false;
        Focusable = false; ClipToBounds = true; ZIndex = 30;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
    }
    public override void Render(DrawingContext context) { base.Render(context); _draw(context); }
}
