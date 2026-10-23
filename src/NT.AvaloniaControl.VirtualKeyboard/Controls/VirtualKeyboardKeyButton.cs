using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.VirtualKeyboard;

/// <summary>A keyboard key that preserves editor focus and accepts one active pointer per keyboard.</summary>
[PseudoClasses(":pressed", ":locked", ":unavailable")]
public class VirtualKeyboardKeyButton : TemplatedControl
{
    public static readonly StyledProperty<VirtualKeyboardKey?> KeyProperty =
        AvaloniaProperty.Register<VirtualKeyboardKeyButton, VirtualKeyboardKey?>(nameof(Key));
    public static readonly DirectProperty<VirtualKeyboardKeyButton, string> LabelProperty =
        AvaloniaProperty.RegisterDirect<VirtualKeyboardKeyButton, string>(nameof(Label), owner => owner.Label);
    public static readonly DirectProperty<VirtualKeyboardKeyButton, bool> IsLockedProperty =
        AvaloniaProperty.RegisterDirect<VirtualKeyboardKeyButton, bool>(nameof(IsLocked), owner => owner.IsLocked);
    private VirtualKeyboard? _owner;
    private readonly HashSet<Visual> _watchedAncestors = [];
    private IPointer? _pointer;
    private string _label = string.Empty;
    private bool _isLocked;

    public VirtualKeyboardKeyButton() { Focusable = false; IsTabStop = false; }
    public VirtualKeyboardKey? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }
    public string Label => _label;
    public bool IsLocked => _isLocked;
    internal bool IsInvocationAvailable => _owner?.IsInputAvailable == true && IsEnabled && IsEffectivelyEnabled && Key is not null
        && IsVisible && this.GetVisualAncestors().All(parent => parent.IsVisible && parent is not InputElement { IsEnabled: false });
    internal bool Invoke() => IsInvocationAvailable && Key is { } key && _owner!.InvokeKey(key);
    protected override AutomationPeer OnCreateAutomationPeer() => new VirtualKeyboardKeyAutomationPeer(this);
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SetOwner(this.GetVisualAncestors().OfType<VirtualKeyboard>().FirstOrDefault());
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _owner?.CancelPress(this); CancelPress(); SetOwner(null);
        base.OnDetachedFromVisualTree(e);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KeyProperty)
        { _owner?.CancelPress(this); UpdateState(); (this.GetVisualParent() as Control)?.InvalidateMeasure(); }
        else if (change.Property == IsEffectivelyEnabledProperty || change.Property == IsVisibleProperty)
        {
            if (!IsEffectivelyEnabled || !IsEffectivelyVisible) _owner?.CancelPress(this);
            UpdateState();
        }
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
        e.PreventGestureRecognition();
        if (_pointer is not null || Key is not { } key || _owner is null || !IsInvocationAvailable
            || (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)) return;
        if (!_owner.TryBeginPress(this, key)) return;
        _pointer = e.Pointer;
        PseudoClasses.Set(":pressed", true);
        _pointer.Capture(this);
        _owner.StartPress(this);
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pointer?.Id != e.Pointer.Id) return;
        e.Handled = true;
        if (!Contains(e.GetPosition(this))) _owner?.CancelPress(this);
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pointer?.Id != e.Pointer.Id) return;
        e.Handled = true;
        _owner?.CompletePress(this, Contains(e.GetPosition(this)));
        CancelPress();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pointer?.Id != e.Pointer.Id) return;
        _owner?.CancelPress(this); CancelPress();
    }
    internal void CancelPress()
    {
        var pointer = _pointer; _pointer = null;
        PseudoClasses.Set(":pressed", false);
        if (pointer?.Captured == this) pointer.Capture(null);
    }
    private bool Contains(Point point) => new Rect(Bounds.Size).Contains(point);
    private void SetOwner(VirtualKeyboard? owner)
    {
        if (_owner is not null) _owner.StateChanged -= OwnerStateChanged;
        foreach (var ancestor in _watchedAncestors) ancestor.PropertyChanged -= AncestorPropertyChanged;
        _watchedAncestors.Clear();
        _owner = owner;
        if (_owner is not null) _owner.StateChanged += OwnerStateChanged;
        if (_owner is not null)
            foreach (var ancestor in this.GetVisualAncestors().TakeWhile(parent => parent != _owner))
                if (_watchedAncestors.Add(ancestor)) ancestor.PropertyChanged += AncestorPropertyChanged;
        UpdateState();
    }
    private void AncestorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsVisibleProperty && e.Property != IsEnabledProperty && e.Property != IsEffectivelyEnabledProperty) return;
        if (!IsInvocationAvailable) _owner?.CancelPress(this);
        UpdateState();
    }
    private void OwnerStateChanged(object? sender, EventArgs e) => UpdateState();
    private void UpdateState()
    {
        SetAndRaise(LabelProperty, ref _label, Key is { } key ? _owner?.GetLabel(key) ?? key.Label : string.Empty);
        SetAndRaise(IsLockedProperty, ref _isLocked, Key is { } lockedKey && _owner?.GetLocked(lockedKey) == true);
        PseudoClasses.Set(":locked", IsLocked);
        PseudoClasses.Set(":unavailable", !IsInvocationAvailable);
    }
}
