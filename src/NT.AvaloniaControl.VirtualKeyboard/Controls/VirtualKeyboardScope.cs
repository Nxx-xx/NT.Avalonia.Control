using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.VirtualKeyboard;

public enum VirtualKeyboardDisplayMode { Manual, OnFocus, OnTouch }
/// <summary>Layout conveniences only; not validation, normalization, or an input method.</summary>
public enum VirtualKeyboardInputPurpose { Default, Text, Integer, Decimal, Email, Url, Phone }

/// <summary>
/// Optional UI-thread window coordinator. Clear any explicit Target before installing it.
/// Owns Target and visibility until disposed or an external Target assignment takes over.
/// Default purpose restores the layout/options captured at construction. Dispose on page deactivation.
/// Window close and keyboard unload dispose automatically. No system-wide input or text inspection.
/// </summary>
public sealed class VirtualKeyboardScope : IDisposable
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<VirtualKeyboardScope, TextBox, bool>("IsEnabled", true);
    public static readonly AttachedProperty<VirtualKeyboardInputPurpose> InputPurposeProperty =
        AvaloniaProperty.RegisterAttached<VirtualKeyboardScope, TextBox, VirtualKeyboardInputPurpose>(
            "InputPurpose", VirtualKeyboardInputPurpose.Default, validate: value => Enum.IsDefined(value));
    private static readonly ConditionalWeakTable<Window, VirtualKeyboardScope> Windows = new();
    private static readonly ConditionalWeakTable<VirtualKeyboard, VirtualKeyboardScope> Keyboards = new();
    private readonly Window _window;
    private readonly VirtualKeyboard _keyboard;
    private readonly HashSet<Visual> _watched = [];
    private readonly VirtualKeyboardLayout _layout;
    private readonly bool _decimal, _negative;
    private TextBox? _editor, _ownedTarget, _suppressed, _touch;
    private bool _open, _updating, _disposed, _pending, _queued, _takeover, _visible;
    private int _touchVersion;
    private VirtualKeyboardDisplayMode _mode;

    public VirtualKeyboardScope(Window window, VirtualKeyboard keyboard,
        VirtualKeyboardDisplayMode displayMode = VirtualKeyboardDisplayMode.OnFocus)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(keyboard);
        Dispatcher.UIThread.VerifyAccess();
        if (!Enum.IsDefined(displayMode)) throw new ArgumentOutOfRangeException(nameof(displayMode));
        if (keyboard.Target is not null) throw new InvalidOperationException("Clear explicit Target before installing a scope.");
        if (TopLevel.GetTopLevel(keyboard) is { } root && !ReferenceEquals(root, window))
            throw new InvalidOperationException("Keyboard must belong to this Window.");
        if (Windows.TryGetValue(window, out _) || Keyboards.TryGetValue(keyboard, out _))
            throw new InvalidOperationException("Only one scope can own a Window and keyboard.");
        _window = window; _keyboard = keyboard; _mode = displayMode;
        _layout = keyboard.Layout; _decimal = keyboard.ShowDecimalPoint; _negative = keyboard.ShowNegativeSign;
        Windows.Add(window, this); Keyboards.Add(keyboard, this);
        window.AddHandler(InputElement.GotFocusEvent, GotFocus, RoutingStrategies.Bubble, true);
        window.AddHandler(InputElement.LostFocusEvent, LostFocus, RoutingStrategies.Bubble, true);
        window.AddHandler(InputElement.PointerPressedEvent, PointerPressed, RoutingStrategies.Tunnel, true);
        window.Activated += Activated; window.Deactivated += Deactivated; window.Closed += Closed;
        keyboard.AttachedToVisualTree += Attached; keyboard.DetachedFromVisualTree += Detached;
        keyboard.PropertyChanged += KeyboardChanged;
        Watch(null); Refresh();
    }

    public static bool GetIsEnabled(TextBox editor) => editor.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(TextBox editor, bool value) => editor.SetValue(IsEnabledProperty, value);
    public static VirtualKeyboardInputPurpose GetInputPurpose(TextBox editor) => editor.GetValue(InputPurposeProperty);
    public static void SetInputPurpose(TextBox editor, VirtualKeyboardInputPurpose value) => editor.SetValue(InputPurposeProperty, value);
    public VirtualKeyboardDisplayMode DisplayMode
    {
        get => _mode;
        set
        {
            Dispatcher.UIThread.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_mode == value) return;
            _mode = value; Reset(); Refresh();
        }
    }
    /// <summary>Open only for the currently focused eligible editor. Does not move focus.</summary>
    public bool Open()
    {
        Dispatcher.UIThread.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        Refresh();
        if (_disposed || _editor is null || !Eligible(_editor, true)) return false;
        _suppressed = null; _open = true; Refresh();
        return !_disposed && _keyboard.IsVisible && ReferenceEquals(_keyboard.Target, _editor);
    }
    /// <summary>Suppress this focus session until a new touch, explicit Open, or focus change.</summary>
    public void Close()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed) return;
        _suppressed = _editor; _open = false; ClearTouch();
        if (_updating) _pending = true; else Apply(null, false);
    }
    private void GotFocus(object? sender, RoutedEventArgs e)
    {
        Refresh();
        if (_mode != VirtualKeyboardDisplayMode.Manual && _editor is not null && ReferenceEquals(_editor, _touch))
            OpenTouch(_editor);
        else if (_touch is not null && !ReferenceEquals(e.Source, _touch)) ClearTouch();
    }
    private void LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_disposed) return;
        if (ReferenceEquals(e.Source, _touch)) ClearTouch();
        _keyboard.CancelInput();
        if (_updating) _pending = true;
        else { Apply(null, false); QueueRefresh(); }
    }
    private void PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_disposed || e.Pointer.Type != PointerType.Touch || !e.Pointer.IsPrimary) return;
        var visual = e.Source as Visual;
        if (visual is not null && (ReferenceEquals(visual, _keyboard) || visual.GetVisualAncestors().Contains(_keyboard))) return;
        var editor = visual as TextBox ?? visual?.GetVisualAncestors().OfType<TextBox>().FirstOrDefault();
        // A different physical gesture also invalidates any pending authorization.
        ClearTouch();
        if (editor is null || !Eligible(editor, false)) return;
        _touch = editor; var version = _touchVersion;
        if (editor.IsFocused) OpenTouch(editor);
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || version != _touchVersion) return;
            if (editor.IsFocused) OpenTouch(editor);
            ClearTouch();
        }, DispatcherPriority.Input);
    }
    private void OpenTouch(TextBox editor)
    {
        Refresh();
        if (_disposed || _mode == VirtualKeyboardDisplayMode.Manual || !ReferenceEquals(_editor, editor) || !Eligible(editor, true)) return;
        _suppressed = null; _open = true; ClearTouch(); Refresh();
    }
    private void Refresh()
    {
        if (_disposed) return;
        if (_updating) { _pending = true; return; }
        var editor = _window.FocusManager?.GetFocusedElement() as TextBox;
        if (editor is not null && !ReferenceEquals(TopLevel.GetTopLevel(editor), _window)) editor = null;
        if (!ReferenceEquals(editor, _editor))
        {
            Watch(editor); _open = false; _suppressed = null; _keyboard.CancelInput();
        }
        var eligible = _editor is not null && Eligible(_editor, true);
        if (!eligible) { _open = false; ClearTouch(); }
        var show = eligible && !ReferenceEquals(_editor, _suppressed) && (_mode == VirtualKeyboardDisplayMode.OnFocus || _open);
        Apply(show ? _editor : null, show);
    }
    private bool Eligible(TextBox editor, bool focused)
    {
        if (!_window.IsActive || !_window.IsVisible || !GetIsEnabled(editor) || editor.IsReadOnly
            || !editor.IsEffectivelyEnabled || (focused && !editor.IsFocused)
            || !ReferenceEquals(TopLevel.GetTopLevel(editor), _window)
            || !ReferenceEquals(TopLevel.GetTopLevel(_keyboard), _window) || !_keyboard.IsEnabled) return false;
        for (Visual? node = editor; node is not null; node = node.GetVisualParent())
            if (!node.IsVisible || node is InputElement { IsEnabled: false }) return false;
        for (var node = _keyboard.GetVisualParent(); node is not null; node = node.GetVisualParent())
            if (!node.IsVisible || node is InputElement { IsEnabled: false }) return false;
        return true;
    }
    private void Watch(TextBox? editor)
    {
        foreach (var visual in _watched) visual.PropertyChanged -= LifetimeChanged;
        _watched.Clear();
        if (_editor is not null) _editor.DetachedFromVisualTree -= EditorDetached;
        _editor = editor;
        WatchChain(_keyboard);
        if (editor is not null) { WatchChain(editor); editor.DetachedFromVisualTree += EditorDetached; }
    }
    private void WatchChain(Visual visual)
    {
        if (_watched.Add(visual)) visual.PropertyChanged += LifetimeChanged;
        foreach (var ancestor in visual.GetVisualAncestors())
            if (_watched.Add(ancestor)) ancestor.PropertyChanged += LifetimeChanged;
    }
    private void LifetimeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Visual.IsVisibleProperty && e.Property != InputElement.IsEnabledProperty
            && e.Property != InputElement.IsEffectivelyEnabledProperty && e.Property != TextBox.IsReadOnlyProperty
            && e.Property != IsEnabledProperty && e.Property != InputPurposeProperty) return;
        if (_updating && ReferenceEquals(sender, _keyboard) && e.Property == Visual.IsVisibleProperty) return;
        Refresh();
    }
    private void Apply(TextBox? target, bool visible)
    {
        if (_disposed) return;
        _updating = true; _visible = visible;
        try
        {
            if (!ReferenceEquals(_keyboard.Target, target) || !visible) _keyboard.CancelInput();
            _ownedTarget = target;
            _keyboard.SetCurrentValue(VirtualKeyboard.TargetProperty, target);
            if (_disposed || _takeover) return;
            if (target is not null)
            {
                var purpose = GetInputPurpose(target);
                _keyboard.SetCurrentValue(VirtualKeyboard.LayoutProperty, purpose switch
                {
                    VirtualKeyboardInputPurpose.Text => VirtualKeyboardLayout.English,
                    VirtualKeyboardInputPurpose.Integer or VirtualKeyboardInputPurpose.Decimal => VirtualKeyboardLayout.Numeric,
                    VirtualKeyboardInputPurpose.Email => VirtualKeyboardLayout.Email,
                    VirtualKeyboardInputPurpose.Url => VirtualKeyboardLayout.Url,
                    VirtualKeyboardInputPurpose.Phone => VirtualKeyboardLayout.Phone,
                    _ => _layout,
                });
                if (_disposed || _takeover) return;
                _keyboard.SetCurrentValue(VirtualKeyboard.ShowDecimalPointProperty, purpose switch
                {
                    VirtualKeyboardInputPurpose.Integer => false,
                    VirtualKeyboardInputPurpose.Decimal => true,
                    _ => _decimal,
                });
                if (_disposed || _takeover) return;
                _keyboard.SetCurrentValue(VirtualKeyboard.ShowNegativeSignProperty, _negative);
            }
            if (_disposed || _takeover) return;
            _keyboard.SetCurrentValue(Visual.IsVisibleProperty, visible);
        }
        finally
        {
            _updating = false;
            if (_takeover && !_disposed) Dispose();
            else if (_pending && !_disposed) QueueRefresh();
        }
    }
    private void KeyboardChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (_updating)
        {
            if (e.Property == VirtualKeyboard.TargetProperty && !ReferenceEquals(_keyboard.Target, _ownedTarget)) _takeover = true;
            else if (e.Property == Visual.IsVisibleProperty && _visible && !_keyboard.IsVisible) Close();
        }
        else if (e.Property == VirtualKeyboard.TargetProperty) { _takeover = true; Dispose(); }
        else if (e.Property == Visual.IsVisibleProperty && !_keyboard.IsVisible) Close();
    }
    private void QueueRefresh()
    {
        _pending = false;
        if (_queued || _disposed) return;
        _queued = true;
        Dispatcher.UIThread.Post(() => { _queued = false; Refresh(); }, DispatcherPriority.Input);
    }
    private void ClearTouch() { _touchVersion++; _touch = null; }
    private void Reset() { ClearTouch(); _open = false; _suppressed = null; _keyboard.CancelInput(); }
    private void Activated(object? sender, EventArgs e) => Refresh();
    private void Deactivated(object? sender, EventArgs e) => InvalidateEditor();
    private void EditorDetached(object? sender, VisualTreeAttachmentEventArgs e) => InvalidateEditor();
    private void InvalidateEditor()
    {
        if (_disposed) return;
        Reset(); Watch(null);
        if (_updating) _pending = true; else Apply(null, false);
    }
    private void Attached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (!ReferenceEquals(TopLevel.GetTopLevel(_keyboard), _window)) Dispose();
        else { Watch(null); Refresh(); }
    }
    private void Detached(object? sender, VisualTreeAttachmentEventArgs e) => Dispose();
    private void Closed(object? sender, EventArgs e) => Dispose();
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed) return;
        _disposed = true; Reset();
        _window.RemoveHandler(InputElement.GotFocusEvent, GotFocus);
        _window.RemoveHandler(InputElement.LostFocusEvent, LostFocus);
        _window.RemoveHandler(InputElement.PointerPressedEvent, PointerPressed);
        _window.Activated -= Activated; _window.Deactivated -= Deactivated; _window.Closed -= Closed;
        _keyboard.AttachedToVisualTree -= Attached; _keyboard.DetachedFromVisualTree -= Detached;
        _keyboard.PropertyChanged -= KeyboardChanged;
        foreach (var visual in _watched) visual.PropertyChanged -= LifetimeChanged;
        _watched.Clear();
        if (_editor is not null) _editor.DetachedFromVisualTree -= EditorDetached;
        _editor = null;
        try
        {
            if (!_takeover && ReferenceEquals(_keyboard.Target, _ownedTarget))
            {
                _keyboard.SetCurrentValue(VirtualKeyboard.TargetProperty, null);
                _keyboard.SetCurrentValue(Visual.IsVisibleProperty, false);
            }
            _ownedTarget = null;
            _keyboard.SetCurrentValue(VirtualKeyboard.LayoutProperty, _layout);
            _keyboard.SetCurrentValue(VirtualKeyboard.ShowDecimalPointProperty, _decimal);
            _keyboard.SetCurrentValue(VirtualKeyboard.ShowNegativeSignProperty, _negative);
        }
        finally { Windows.Remove(_window); Keyboards.Remove(_keyboard); }
    }
}
