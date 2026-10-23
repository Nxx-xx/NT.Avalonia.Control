using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private DrawnTooltipOverlay? _foregroundTooltipPresentation;
    private bool _drawingForegroundTooltip;

    private void MeasureForegroundPresentation(Size size)
    {
        if (!_attached && !_exportInProgress || !IsVisible ||
            _labelTemplateHost is null && _legendPresentationHost is null && !HasPlotAreaContentPresentation)
        { ClearForegroundPresentation(); return; }
        if (_foregroundTooltipPresentation is null)
        {
            var overlay = new DrawnTooltipOverlay(this)
            {
                Name = "PART_ChartForegroundTooltip", IsHitTestVisible = false,
                Focusable = false, ClipToBounds = true, ZIndex = 29,
            };
            KeyboardNavigation.SetTabNavigation(overlay, KeyboardNavigationMode.None);
            _foregroundTooltipPresentation = overlay;
        }
        EnsureForegroundPresentationMembership();
        _foregroundTooltipPresentation?.Measure(size);
    }

    private void ArrangeForegroundPresentation(Size size)
    {
        MeasureForegroundPresentation(size);
        _foregroundTooltipPresentation?.Arrange(new Rect(size));
    }

    private void EnsureForegroundPresentationMembership()
    {
        if (_foregroundTooltipPresentation is not { } overlay) return;
        if (((ILogical)overlay).LogicalParent is null) LogicalChildren.Add(overlay);
        if (!ReferenceEquals(_foregroundTooltipPresentation, overlay)) return;
        if (overlay.GetVisualParent() is null) VisualChildren.Add(overlay);
    }

    private void ClearForegroundPresentation()
    {
        if (_foregroundTooltipPresentation is not { } overlay) return;
        _foregroundTooltipPresentation = null;
        overlay.ReleaseOwner();
        try { if (ReferenceEquals(overlay.GetVisualParent(), this)) VisualChildren.Remove(overlay); }
        finally { if (ReferenceEquals(((ILogical)overlay).LogicalParent, this)) LogicalChildren.Remove(overlay); }
    }

    private bool CanDrawForegroundTooltip => !IsTrackballActive && !SuppressDrawnTooltip && IsTooltipEnabled && _hoveredPoint is not null &&
        _panPointer is null && _selectionPointer is null;

    private void RenderForegroundPresentationForExport(DrawingContext context)
    {
        if (_foregroundTooltipPresentation is not { } overlay || !CanDrawForegroundTooltip || overlay.Bounds.Width <= 0 || overlay.Bounds.Height <= 0) return;
        context.DrawRectangle(new VisualBrush(overlay) { Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, overlay.Bounds);
    }

    // Ordinary tooltip drawing must be above control-based plot content, labels and legends too.
    // Keep their one shared drawing routine, and allocate this passive visual only
    // when the chart has opted into one of those control presentation layers.
    private sealed class DrawnTooltipOverlay(CartesianChart owner) : Control
    {
        private CartesianChart? _owner = owner;
        public void ReleaseOwner() => _owner = null;
        protected override Size MeasureOverride(Size availableSize) => default;
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (_owner is not { } chart || chart._scene is not { } scene || !chart.CanDrawForegroundTooltip || chart._drawingForegroundTooltip) return;
            chart._drawingForegroundTooltip = true;
            try
            {
                using var clip = context.PushClip(new Rect(chart.Bounds.Size));
                chart.RenderTooltip(context, scene);
            }
            finally { chart._drawingForegroundTooltip = false; }
        }
    }
}
