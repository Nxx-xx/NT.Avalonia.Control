using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed class ChartDataSelectionOverlay(Action<DrawingContext> draw) : Control
{
    public ChartDataSelectionOverlay() : this(_ => { }) { }
    internal void Initialize()
    {
        Name = "PART_ChartDataSelectionOverlay"; IsHitTestVisible = false; Focusable = false; ClipToBounds = true; ZIndex = 28;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
    }
    public override void Render(DrawingContext context) { base.Render(context); draw(context); }
}
