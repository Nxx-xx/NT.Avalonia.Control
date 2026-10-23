using Avalonia;
using Avalonia.Input;

namespace NT.AvaloniaControl.Charts;

/// <summary>The operation assigned to wheel and precision scrolling deltas. Avalonia reports both
/// through PointerWheelChanged; these modes do not infer the physical input device.</summary>
public enum ChartWheelMode
{
    Zoom = 0,
    Pan = 1,
    Auto = 2,
    Disabled = 3
}

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<ChartWheelMode> WheelModeProperty =
        AvaloniaProperty.Register<CartesianChart, ChartWheelMode>(nameof(WheelMode), ChartWheelMode.Zoom,
            validate: value => Enum.IsDefined(value));
    public static readonly StyledProperty<KeyModifiers> WheelZoomModifiersProperty =
        AvaloniaProperty.Register<CartesianChart, KeyModifiers>(nameof(WheelZoomModifiers), KeyModifiers.None,
            validate: value => (value & ~(KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0);
    public static readonly StyledProperty<double> WheelPanSensitivityProperty =
        AvaloniaProperty.Register<CartesianChart, double>(nameof(WheelPanSensitivity), 48,
            validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<double> WheelZoomSensitivityProperty =
        AvaloniaProperty.Register<CartesianChart, double>(nameof(WheelZoomSensitivity), 1.2,
            validate: value => double.IsFinite(value) && value > 1);

    /// <summary>Zoom preserves the original opt-in wheel behavior. Auto uses a zoom modifier and pans otherwise.</summary>
    public ChartWheelMode WheelMode { get => GetValue(WheelModeProperty); set => SetValue(WheelModeProperty, value); }
    /// <summary>The modifiers required for Zoom. In Auto, None means Control or Meta; a nonempty mask
    /// replaces that default shortcut. Additional modifiers are allowed. Pan ignores this setting.</summary>
    public KeyModifiers WheelZoomModifiers { get => GetValue(WheelZoomModifiersProperty); set => SetValue(WheelZoomModifiersProperty, value); }
    /// <summary>Content translation in device-independent pixels per wheel delta unit. Fractional and two-axis deltas are retained.</summary>
    public double WheelPanSensitivity { get => GetValue(WheelPanSensitivityProperty); set => SetValue(WheelPanSensitivityProperty, value); }
    /// <summary>The zoom scale per positive vertical delta unit; larger than one. Positive deltas zoom in.</summary>
    public double WheelZoomSensitivity { get => GetValue(WheelZoomSensitivityProperty); set => SetValue(WheelZoomSensitivityProperty, value); }

    private void HandleChartWheel(PointerWheelEventArgs e)
    {
        if (e.Handled || !IsEffectivelyEnabled || !_attached || WheelMode == ChartWheelMode.Disabled ||
            InputRoutingMode == ChartInputRoutingMode.ScrollViewer || InteractionAxes == ChartInteractionAxes.None ||
            !double.IsFinite(e.Delta.X) || !double.IsFinite(e.Delta.Y) || e.Delta == default) return;

        var modifiers = WheelZoomModifiers;
        var hasZoomModifiers = modifiers == KeyModifiers.None
            ? WheelMode != ChartWheelMode.Auto || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0
            : (e.KeyModifiers & modifiers) == modifiers;
        var zoom = WheelMode == ChartWheelMode.Zoom || WheelMode == ChartWheelMode.Auto && hasZoomModifiers;
        if (zoom ? !IsZoomEnabled || !hasZoomModifiers || e.Delta.Y == 0 : !IsPanEnabled) return;

        var scene = GetScene();
        var position = e.GetPosition(this);
        if (scene is null || !scene.HasPoints || !scene.Plot.Contains(position) ||
            IsLegendFloating && LegendPanelContains(scene, position) ||
            !double.IsFinite(scene.Plot.Width) || !double.IsFinite(scene.Plot.Height) || scene.Plot.Width <= 0 || scene.Plot.Height <= 0) return;

        // Shift redirects a conventional single vertical wheel into horizontal content movement.
        // Native two-axis precision packets retain both independently supplied components.
        var delta = !zoom && (e.KeyModifiers & KeyModifiers.Shift) != 0 && e.Delta.X == 0
            ? new Vector(e.Delta.Y, 0) : e.Delta;
        var contentDelta = new Vector(WheelTranslation(delta.X), WheelTranslation(delta.Y));
        if (!zoom && WheelMode == ChartWheelMode.Auto && InputRoutingMode == ChartInputRoutingMode.Auto &&
            CanInputScrollAncestorMove(contentDelta)) return;

        // Saturate extreme but finite configuration/input at a usable transform. Dropping an
        // overflowing Pow or translation would turn a deliberate navigation packet into a no-op.
        var factor = zoom ? Math.Exp(Math.Clamp(-Math.Clamp(e.Delta.Y, -20, 20) * Math.Log(WheelZoomSensitivity), -700, 700)) : 1;

        var revision = _inputTransformRevision;
        StopTouchInput();
        StopPan();
        StopRectangle();
        // Capture release can synchronously invoke application code. Respect a newer view or
        // detached chart instead of applying a wheel packet prepared against its earlier scene.
        if (!_attached || !IsEffectivelyEnabled || revision != _inputTransformRevision) return;
        scene = GetScene();
        if (scene is null || !scene.HasPoints || !scene.Plot.Contains(position) || revision != _inputTransformRevision) return;

        // The shared commit also considers explicit secondary-axis windows. No-op packets bubble
        // to nested scrolling controls, including at a chart's extent or zoom limits.
        if (TryApplyInputTransform(scene, zoom ? position : scene.Plot.Center,
            zoom ? position : scene.Plot.Center + contentDelta, factor, InteractionAxes))
            e.Handled = true;
    }

    private double WheelTranslation(double delta)
    {
        var result = delta * WheelPanSensitivity;
        return double.IsFinite(result) ? result : Math.CopySign(double.MaxValue / 4, delta);
    }
}
