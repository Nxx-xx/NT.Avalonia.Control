using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public abstract partial class ChartBase
{
    internal void AttachDataTooltipOverlay(Control overlay, Func<bool> isCurrent)
    {
        var logical = ((ILogical)overlay).LogicalParent; var visual = overlay.GetVisualParent();
        if (logical is not null && !ReferenceEquals(logical, this) || visual is not null && !ReferenceEquals(visual, this))
            throw new InvalidOperationException("The chart tooltip overlay already has another parent.");
        if (logical is null) LogicalChildren.Add(overlay);
        // Logical attachment can run application callbacks which remove this host.
        if (!isCurrent()) { DetachDataTooltipOverlay(overlay); return; }
        if (!ReferenceEquals(((ILogical)overlay).LogicalParent, this)) return;
        if (overlay.GetVisualParent() is null) VisualChildren.Add(overlay);
        if (!isCurrent()) DetachDataTooltipOverlay(overlay);
    }

    internal void DetachDataTooltipOverlay(Control overlay)
    {
        try { if (ReferenceEquals(overlay.GetVisualParent(), this)) VisualChildren.Remove(overlay); }
        finally { if (ReferenceEquals(((ILogical)overlay).LogicalParent, this)) LogicalChildren.Remove(overlay); }
    }
}
