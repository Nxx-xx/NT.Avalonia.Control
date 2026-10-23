using Avalonia;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private long _inputTransformRevision;
    private bool _writingInputViewport;
    private ChartViewport? _expectedInputViewport;
    private ChartAxis? _writingInputAxis;
    private AvaloniaProperty? _writingInputAxisProperty;
    private double? _expectedInputAxisValue;

    private bool IsInputTransformPropertyWrite(AvaloniaPropertyChangedEventArgs change) =>
        _writingInputViewport && change.Property == ViewportProperty &&
        change.GetNewValue<ChartViewport?>() == _expectedInputViewport;

    private bool IsInputTransformAxisWrite(ChartAxis axis, ChartAxisChangedEventArgs change) =>
        ReferenceEquals(axis, _writingInputAxis) && change.Property == _writingInputAxisProperty &&
        (change.Property == ChartAxis.ViewMinimumProperty ? axis.ViewMinimum : axis.ViewMaximum) == _expectedInputAxisValue;

    private void InvalidateInputTransform(bool preserveTouch = false)
    {
        ++_inputTransformRevision;
        if (!preserveTouch) StopTouchInput();
    }

    // All participating ranges are prepared before publishing any property. A newer business
    // callback invalidates this generation; the remaining old writes then stop immediately.
    private bool TryApplyInputTransform(ChartScene initial, Point originalAnchor, Point currentAnchor,
        double factor, ChartInteractionAxes axes)
    {
        if (!_attached || !IsEffectivelyEnabled || !IsEffectivelyVisible || axes == ChartInteractionAxes.None ||
            !double.IsFinite(factor) || factor <= 0 || !double.IsFinite(originalAnchor.X) || !double.IsFinite(originalAnchor.Y) ||
            !double.IsFinite(currentAnchor.X) || !double.IsFinite(currentAnchor.Y) || initial.Plot.Width <= 0 || initial.Plot.Height <= 0)
            return false;
        var revision = ++_inputTransformRevision;
        var current = GetScene();
        if (current is null || revision != _inputTransformRevision) return false;
        var writes = new List<(ChartAxis Axis, double Minimum, double Maximum)>();
        (double Minimum, double Maximum) Prepare(ChartAxis axis, ChartScale full, ChartScale view, bool horizontal)
        {
            var lower = AscendingFraction(full, view.Minimum); var upper = AscendingFraction(full, view.Maximum);
            var span = upper - lower;
            var before = horizontal ? (originalAnchor.X - initial.Plot.Left) / initial.Plot.Width : (initial.Plot.Bottom - originalAnchor.Y) / initial.Plot.Height;
            var after = horizontal ? (currentAnchor.X - initial.Plot.Left) / initial.Plot.Width : (initial.Plot.Bottom - currentAnchor.Y) / initial.Plot.Height;
            if (axis.IsInversed) { before = 1 - before; after = 1 - after; }
            var limits = ZoomLimits(axis);
            var newSpan = Math.Clamp(span * factor, limits.MinimumSpan, limits.MaximumSpan);
            if (before == after && (factor == 1 || newSpan == span)) return (view.Minimum, view.Maximum);
            var start = Math.Clamp(Math.FusedMultiplyAdd(before, span, lower) - after * newSpan, 0, Math.Max(0, 1 - newSpan));
            var minimum = AscendingValue(full, start); var maximum = AscendingValue(full, start + newSpan);
            if (minimum >= maximum)
            {
                minimum = Math.Max(full.Minimum, Math.BitDecrement(minimum));
                maximum = Math.Min(full.Maximum, Math.BitIncrement(maximum));
            }
            return (minimum, maximum);
        }
        var x = (axes & ChartInteractionAxes.X) != 0 ? Prepare(XAxis, initial.BaseX, initial.XScale, true) : (initial.Range.XMinimum, initial.Range.XMaximum);
        var y = (axes & ChartInteractionAxes.Y) != 0 ? Prepare(YAxis, initial.BaseY, initial.YScale, false) : (initial.Range.YMinimum, initial.Range.YMaximum);
        var prepared = new ChartViewport(x.Item1, x.Item2, y.Item1, y.Item2);
        if (!prepared.IsValid) return false;
        var primaryChanged = prepared != current.Range;
        foreach (var axis in initial.ExtraXAxes.Concat(initial.ExtraYAxes))
        {
            var horizontal = initial.ExtraXAxes.Contains(axis);
            if ((axes & (horizontal ? ChartInteractionAxes.X : ChartInteractionAxes.Y)) == 0 ||
                axis.ViewMinimum is null && axis.ViewMaximum is null) continue;
            var full = GetBaseScale(axis, initial.Snapshots, horizontal, initial.Scales[axis].CategoryLabels);
            var view = initial.Scales[axis]; var pair = Prepare(axis, full, view, horizontal);
            if (!current.Scales.TryGetValue(axis, out var currentView)) return false;
            if (pair.Minimum != currentView.Minimum || pair.Maximum != currentView.Maximum) writes.Add((axis, pair.Minimum, pair.Maximum));
        }
        if (!primaryChanged && writes.Count == 0) return false;
        if (prepared.XMinimum != current.Range.XMinimum || prepared.XMaximum != current.Range.XMaximum)
        {
            PauseFollowingForNavigation();
            if (revision != _inputTransformRevision) return false;
        }
        if (primaryChanged)
        {
            if ((axes & ChartInteractionAxes.X) != 0) writes.Add((XAxis, prepared.XMinimum, prepared.XMaximum));
            if ((axes & ChartInteractionAxes.Y) != 0) writes.Add((YAxis, prepared.YMinimum, prepared.YMaximum));
        }
        if (revision != _inputTransformRevision) return false;
        var previousViewportWrite = _writingInputViewport; var previousExpected = _expectedInputViewport;
        var previousAxis = _writingInputAxis; var previousProperty = _writingInputAxisProperty; var previousAxisValue = _expectedInputAxisValue;
        _applyingSynchronization++;
        try
        {
            if (primaryChanged)
            {
                _writingInputViewport = true; _expectedInputViewport = prepared;
                SetCurrentValue(ViewportProperty, prepared);
                _writingInputViewport = previousViewportWrite; _expectedInputViewport = previousExpected;
                if (_inputTransformRevision != revision) return true;
            }
            foreach (var write in writes)
            {
                _writingInputAxis = write.Axis; _writingInputAxisProperty = ChartAxis.ViewMinimumProperty; _expectedInputAxisValue = write.Minimum;
                write.Axis.SetCurrentValue(ChartAxis.ViewMinimumProperty, write.Minimum);
                if (_inputTransformRevision != revision) return true;
                _writingInputAxisProperty = ChartAxis.ViewMaximumProperty; _expectedInputAxisValue = write.Maximum;
                write.Axis.SetCurrentValue(ChartAxis.ViewMaximumProperty, write.Maximum);
                if (_inputTransformRevision != revision) return true;
            }
            return true;
        }
        finally
        {
            _writingInputViewport = previousViewportWrite; _expectedInputViewport = previousExpected;
            _writingInputAxis = previousAxis; _writingInputAxisProperty = previousProperty; _expectedInputAxisValue = previousAxisValue;
            _applyingSynchronization--;
            NotifyViewSynchronizationChanged();
        }
    }
}
