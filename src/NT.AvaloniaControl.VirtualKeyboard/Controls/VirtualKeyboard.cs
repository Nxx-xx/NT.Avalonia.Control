using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.VirtualKeyboard.Input;

namespace NT.AvaloniaControl.VirtualKeyboard;

/// <summary>A manually placed keyboard for an explicitly focused application-local editing target.</summary>
[TemplatePart("PART_RowsPresenter", typeof(ItemsControl), IsRequired = true)]
[PseudoClasses(":unavailable", ":shift", ":capslock")]
public class VirtualKeyboard : TemplatedControl
{
    public static readonly StyledProperty<IInputElement?> TargetProperty =
        AvaloniaProperty.Register<VirtualKeyboard, IInputElement?>(nameof(Target));
    public static readonly StyledProperty<VirtualKeyboardLayout> LayoutProperty =
        AvaloniaProperty.Register<VirtualKeyboard, VirtualKeyboardLayout>(nameof(Layout), VirtualKeyboardLayout.English,
            validate: value => Enum.IsDefined(value));
    public static readonly StyledProperty<bool> ShowDecimalPointProperty =
        AvaloniaProperty.Register<VirtualKeyboard, bool>(nameof(ShowDecimalPoint), true);
    public static readonly StyledProperty<bool> ShowNegativeSignProperty =
        AvaloniaProperty.Register<VirtualKeyboard, bool>(nameof(ShowNegativeSign), true);
    public static readonly StyledProperty<TimeSpan> RepeatDelayProperty =
        AvaloniaProperty.Register<VirtualKeyboard, TimeSpan>(nameof(RepeatDelay), TimeSpan.FromMilliseconds(500),
            validate: value => value >= TimeSpan.Zero && value <= TimeSpan.FromSeconds(10));
    public static readonly StyledProperty<TimeSpan> RepeatIntervalProperty =
        AvaloniaProperty.Register<VirtualKeyboard, TimeSpan>(nameof(RepeatInterval), TimeSpan.FromMilliseconds(70),
            validate: value => value >= TimeSpan.FromMilliseconds(10) && value <= TimeSpan.FromSeconds(10));
    public static readonly DirectProperty<VirtualKeyboard, bool> IsShiftActiveProperty =
        AvaloniaProperty.RegisterDirect<VirtualKeyboard, bool>(nameof(IsShiftActive), owner => owner.IsShiftActive);
    public static readonly DirectProperty<VirtualKeyboard, bool> IsCapsLockActiveProperty =
        AvaloniaProperty.RegisterDirect<VirtualKeyboard, bool>(nameof(IsCapsLockActive), owner => owner.IsCapsLockActive);
    public static readonly DirectProperty<VirtualKeyboard, bool> IsInputAvailableProperty =
        AvaloniaProperty.RegisterDirect<VirtualKeyboard, bool>(nameof(IsInputAvailable), owner => owner.IsInputAvailable);
    public static readonly DirectProperty<VirtualKeyboard, IReadOnlyList<VirtualKeyboardRow>> RowsProperty =
        AvaloniaProperty.RegisterDirect<VirtualKeyboard, IReadOnlyList<VirtualKeyboardRow>>(nameof(Rows), owner => owner.Rows);
    public static readonly DirectProperty<VirtualKeyboard, string?> TemplateDiagnosticProperty =
        AvaloniaProperty.RegisterDirect<VirtualKeyboard, string?>(nameof(TemplateDiagnostic), owner => owner.TemplateDiagnostic);

    private readonly KeyboardState _state = new();
    private readonly RepeatController _repeat = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Input);
    private readonly HashSet<Visual> _watchedVisuals = [];
    private IReadOnlyList<VirtualKeyboardRow> _rows = KeyboardLayouts.English;
    private ItemsControl? _presenter;
    private IControlTemplate? _appliedTemplate;
    private TopLevel? _root;
    private Visual? _targetVisual;
    private bool _attached;
    private bool _isShiftActive;
    private bool _isCapsLockActive;
    private bool _isInputAvailable;
    private string? _templateDiagnostic = "VirtualKeyboard requires a template containing PART_RowsPresenter (ItemsControl).";
    private VirtualKeyboardKeyButton? _pressedButton;
    private VirtualKeyboardKey? _pressedKey;
    private IInputElement? _pressedTarget;
    private long _session;

    public VirtualKeyboard()
    {
        Focusable = false;
        IsTabStop = false;
        ActualThemeVariantChanged += ThemeVariantChanged;
        PseudoClasses.Set(":unavailable", true);
    }
    public IInputElement? Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    public VirtualKeyboardLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }
    public bool ShowDecimalPoint { get => GetValue(ShowDecimalPointProperty); set => SetValue(ShowDecimalPointProperty, value); }
    public bool ShowNegativeSign { get => GetValue(ShowNegativeSignProperty); set => SetValue(ShowNegativeSignProperty, value); }
    /// <summary>Initial repeat delay, from zero through ten seconds. Zero defers the next action by RepeatInterval.</summary>
    public TimeSpan RepeatDelay
    {
        get => GetValue(RepeatDelayProperty);
        set { RepeatController.Validate(value, RepeatInterval); SetValue(RepeatDelayProperty, value); }
    }
    /// <summary>Repeat interval, from ten milliseconds through ten seconds.</summary>
    public TimeSpan RepeatInterval
    {
        get => GetValue(RepeatIntervalProperty);
        set { RepeatController.Validate(RepeatDelay, value); SetValue(RepeatIntervalProperty, value); }
    }
    public bool IsShiftActive => _isShiftActive;
    public bool IsCapsLockActive => _isCapsLockActive;
    public bool IsInputAvailable => _isInputAvailable;
    public IReadOnlyList<VirtualKeyboardRow> Rows => _rows;
    /// <summary>A fixed template-contract diagnostic; never contains target text.</summary>
    public string? TemplateDiagnostic => _templateDiagnostic;
    public event EventHandler<VirtualKeyboardTargetEventArgs>? ReturnRequested;
    public event EventHandler<VirtualKeyboardTargetEventArgs>? NextRequested;
    internal event EventHandler? StateChanged;

    /// <summary>Invalidates an active press and repeat session, for example when its page is deactivated.</summary>
    public void CancelInput()
    {
        ++_session;
        _timer.Stop();
        _repeat.Cancel();
        var button = _pressedButton;
        _pressedButton = null; _pressedKey = null; _pressedTarget = null;
        button?.CancelPress();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        CancelInput();
        if (_presenter is not null)
        {
            _presenter.DetachedFromVisualTree -= PresenterDetached;
            _presenter.ItemsSource = null;
        }
        base.OnApplyTemplate(e);
        _appliedTemplate = Template;
        _presenter = e.NameScope.Find("PART_RowsPresenter") as ItemsControl;
        SetAndRaise(TemplateDiagnosticProperty, ref _templateDiagnostic, _presenter is null
            ? "VirtualKeyboard requires PART_RowsPresenter of type ItemsControl." : null);
        if (_presenter is not null)
        {
            _presenter.ItemsSource = Rows;
            _presenter.DetachedFromVisualTree += PresenterDetached;
        }
        RefreshAvailability();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _timer.Tick += RepeatTick;
        ConnectLifetime();
        RefreshAvailability();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        CancelInput();
        _timer.Tick -= RepeatTick;
        DisconnectLifetime();
        RefreshAvailability();
        base.OnDetachedFromVisualTree(e);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TargetProperty || change.Property == LayoutProperty)
        {
            CancelInput(); _state.Reset(); UpdateState();
            if (change.Property == LayoutProperty) UpdateRows();
            ConnectLifetime(); RefreshAvailability();
        }
        else if (change.Property == ShowDecimalPointProperty || change.Property == ShowNegativeSignProperty)
        { CancelInput(); UpdateRows(); }
        else if (change.Property == RepeatDelayProperty || change.Property == RepeatIntervalProperty)
            CancelInput();
        else if (change.Property == TemplateProperty)
        {
            CancelInput();
            SetAndRaise(TemplateDiagnosticProperty, ref _templateDiagnostic,
                _presenter is not null && ReferenceEquals(Template, _appliedTemplate) ? null :
                "VirtualKeyboard requires a template containing PART_RowsPresenter (ItemsControl).");
            RefreshAvailability();
        }
        else if (change.Property == ThemeProperty) { CancelInput(); RefreshAvailability(); }
        else if (change.Property == IsVisibleProperty || change.Property == IsEffectivelyEnabledProperty)
            RefreshAvailability();
    }
    private void UpdateRows()
    {
        SetAndRaise(RowsProperty, ref _rows, KeyboardLayouts.Create(Layout, ShowDecimalPoint, ShowNegativeSign));
        if (_presenter is not null) _presenter.ItemsSource = Rows;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    private void UpdateState()
    {
        SetAndRaise(IsShiftActiveProperty, ref _isShiftActive, _state.Shift);
        SetAndRaise(IsCapsLockActiveProperty, ref _isCapsLockActive, _state.CapsLock);
        PseudoClasses.Set(":shift", IsShiftActive); PseudoClasses.Set(":capslock", IsCapsLockActive);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    internal string GetLabel(VirtualKeyboardKey key) => _state.ResolveLabel(key);
    internal bool GetLocked(VirtualKeyboardKey key) => key.Kind switch
    { VirtualKeyboardKeyKind.Shift => IsShiftActive, VirtualKeyboardKeyKind.CapsLock => IsCapsLockActive, _ => false };
    internal bool TryBeginPress(VirtualKeyboardKeyButton button, VirtualKeyboardKey key)
    {
        if (_pressedButton is not null || !RefreshAvailability() || !ContainsKey(key)) return false;
        _pressedButton = button; _pressedKey = key; _pressedTarget = Target;
        return true;
    }
    internal void StartPress(VirtualKeyboardKeyButton button)
    {
        if (_pressedButton != button || _pressedKey is not { IsRepeatable: true } key) return;
        var session = _session;
        if (!InvokeKey(key) || session != _session || _pressedButton != button) return;
        _repeat.Begin(session, _clock.Elapsed, RepeatDelay, RepeatInterval);
        _timer.Interval = RepeatDelay == TimeSpan.Zero ? RepeatInterval : RepeatDelay;
        _timer.Start();
    }
    internal void CompletePress(VirtualKeyboardKeyButton button, bool inside)
    {
        if (_pressedButton != button) return;
        var key = _pressedKey;
        var target = _pressedTarget;
        CancelInput();
        if (inside && key is { IsRepeatable: false } && ReferenceEquals(Target, target)) InvokeKey(key);
    }
    internal void CancelPress(VirtualKeyboardKeyButton button)
    { if (_pressedButton == button) CancelInput(); }
    internal bool InvokeKey(VirtualKeyboardKey key)
    {
        if (!RefreshAvailability() || !ContainsKey(key)) return false;
        var target = Target;
        var session = _session;
        if (key.Kind == VirtualKeyboardKeyKind.Shift) { _state.ToggleShift(); UpdateState(); return true; }
        if (key.Kind == VirtualKeyboardKeyKind.CapsLock) { _state.ToggleCapsLock(); UpdateState(); return true; }
        var result = key.Kind == VirtualKeyboardKeyKind.Character
            ? TextBoxInputAdapter.TryCommitText(target, _root, _state.ResolveText(key))
            : TextBoxInputAdapter.TryExecute(target, _root, key.Kind);
        if (key.Kind == VirtualKeyboardKeyKind.Character && result == VirtualKeyboardInputResult.Handled && session == _session)
        { _state.CharacterHandled(); UpdateState(); }
        if (result == VirtualKeyboardInputResult.ReturnRequested && target is not null)
            ReturnRequested?.Invoke(this, new(target));
        else if (result == VirtualKeyboardInputResult.NextRequested && target is not null)
            NextRequested?.Invoke(this, new(target));
        return result != VirtualKeyboardInputResult.Unavailable;
    }
    private bool ContainsKey(VirtualKeyboardKey key) => Rows.Any(row => row.Keys.Contains(key));
    internal TimeSpan RepeatNow => _clock.Elapsed;
    internal void AdvanceRepeat(TimeSpan now) => ProcessRepeat(now);
    private void RepeatTick(object? sender, EventArgs e) => ProcessRepeat(_clock.Elapsed);
    private void ProcessRepeat(TimeSpan now)
    {
        if (_pressedKey is not { IsRepeatable: true } key || _pressedButton?.IsInvocationAvailable != true
            || !ReferenceEquals(_pressedTarget, Target) || !RefreshAvailability())
        { CancelInput(); return; }
        var session = _session;
        if (!_repeat.TryTick(session, now))
        {
            if (_repeat.GetNextTickDelay(session, now) is { } remaining) _timer.Interval = remaining;
            else CancelInput();
            return;
        }
        if (!InvokeKey(key) || session != _session) { CancelInput(); return; }
        _timer.Interval = RepeatInterval;
    }
    private bool RefreshAvailability()
    {
        var available = _attached && _presenter is not null && ReferenceEquals(Template, _appliedTemplate)
            && IsEnabledChain(this) && IsVisibleChain(this)
            && TextBoxInputAdapter.CanInput(Target, _root);
        var changed = _isInputAvailable != available;
        SetAndRaise(IsInputAvailableProperty, ref _isInputAvailable, available);
        PseudoClasses.Set(":unavailable", !available);
        if (!available && _pressedButton is not null) CancelInput();
        if (changed) StateChanged?.Invoke(this, EventArgs.Empty);
        return available;
    }
    private static bool IsVisibleChain(Visual visual) => visual.IsVisible && visual.GetVisualAncestors().All(parent => parent.IsVisible);
    private static bool IsEnabledChain(Visual visual) => visual is InputElement { IsEnabled: true, IsEffectivelyEnabled: true }
        && visual.GetVisualAncestors().OfType<InputElement>().All(parent => parent.IsEnabled && parent.IsEffectivelyEnabled);
    private void ConnectLifetime()
    {
        DisconnectLifetime();
        if (!_attached) return;
        _root = TopLevel.GetTopLevel(this);
        _targetVisual = Target as Visual;
        WatchChain(this);
        if (_targetVisual is not null)
        {
            WatchChain(_targetVisual);
            _targetVisual.AttachedToVisualTree += TargetTreeChanged;
            _targetVisual.DetachedFromVisualTree += TargetTreeChanged;
        }
        if (_root is Window window) { window.Activated += WindowChanged; window.Deactivated += WindowChanged; }
    }
    private void WatchChain(Visual visual)
    {
        if (_watchedVisuals.Add(visual)) visual.PropertyChanged += LifetimePropertyChanged;
        foreach (var parent in visual.GetVisualAncestors())
            if (_watchedVisuals.Add(parent)) parent.PropertyChanged += LifetimePropertyChanged;
    }
    private void DisconnectLifetime()
    {
        foreach (var visual in _watchedVisuals) visual.PropertyChanged -= LifetimePropertyChanged;
        _watchedVisuals.Clear();
        if (_targetVisual is not null)
        {
            _targetVisual.AttachedToVisualTree -= TargetTreeChanged;
            _targetVisual.DetachedFromVisualTree -= TargetTreeChanged;
        }
        if (_root is Window window) { window.Activated -= WindowChanged; window.Deactivated -= WindowChanged; }
        _targetVisual = null; _root = null;
    }
    private void LifetimePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == VisualParentProperty) { CancelInput(); ConnectLifetime(); RefreshAvailability(); }
        else if (e.Property == IsVisibleProperty || e.Property == IsEnabledProperty || e.Property == IsEffectivelyEnabledProperty
            || e.Property == IsFocusedProperty || e.Property == TextBox.IsReadOnlyProperty || e.Property == Window.IsActiveProperty)
        { RefreshAvailability(); }
    }
    private void TargetTreeChanged(object? sender, VisualTreeAttachmentEventArgs e)
    { CancelInput(); ConnectLifetime(); RefreshAvailability(); }
    private void PresenterDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // A theme can briefly clear and restore the same template without rebuilding it.
        // Retain its presenter until the visual is actually removed by a template replacement.
        if (!_attached || ReferenceEquals(Template, _appliedTemplate) || _presenter is not { } previous || sender != previous) return;
        CancelInput();
        _presenter = null; _appliedTemplate = null;
        previous.DetachedFromVisualTree -= PresenterDetached;
        previous.ItemsSource = null;
        RefreshAvailability();
    }
    private void WindowChanged(object? sender, EventArgs e)
    { CancelInput(); RefreshAvailability(); }
    private void ThemeVariantChanged(object? sender, EventArgs e) => CancelInput();
}

/// <summary>A host request containing target identity only, including for password targets.</summary>
public sealed class VirtualKeyboardTargetEventArgs(IInputElement target) : EventArgs
{
    public IInputElement Target { get; } = target;
}
