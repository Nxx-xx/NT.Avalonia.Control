using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace NT.AvaloniaControl.Charts;

/// <summary>A virtual original data item. Renderer-decimated and interpolated readings are never exposed as source points.</summary>
public sealed class ChartPointAutomationPeer : UnrealizedElementAutomationPeer, IValueProvider, ISelectionItemProvider, IInvokeProvider
{
    private readonly ChartAutomationPeer _root;
    private readonly ChartSeriesAutomationPeer _parent;
    private readonly long _identity;
    private ChartPointHit? _point;
    private bool _retired;
    private bool _lastSelected;
    private string _lastName;
    private string _lastValue;
    internal ChartSeries Series => _parent.Series;
    internal ChartPointAutomationPeer(ChartAutomationPeer root, ChartSeriesAutomationPeer parent, ChartPointHit point, long identity)
    {
        _root = root; _parent = parent; _identity = identity; _point = point; SetParent(parent);
        _lastSelected = IsSelected; _lastName = GetName(); _lastValue = Value;
    }
    /// <summary>Current original item and projected position, or null for stale, hidden or unavailable data.</summary>
    public ChartPointHit? CurrentPoint
    { get { _root.Chart.VerifyAccess(); return TryCurrent(out var point) ? point : null; } }
    public bool IsReadOnly => true;
    public string Value { get { _root.Chart.VerifyAccess(); return CurrentPoint is { } point ? _root.Chart.AccessibilityPointName(point) : "unavailable"; } }
    public void SetValue(string? value) => throw new InvalidOperationException("Chart point values are read-only.");
    public bool IsSelected { get { _root.Chart.VerifyAccess(); return TryCurrent(out var point) && _root.Chart.AccessibilityPointIsSelected(point); } }
    public ISelectionProvider SelectionContainer => _root;
    public void Select() => ChangeSelection(false, false);
    public void AddToSelection() => ChangeSelection(true, false);
    public void RemoveFromSelection() => ChangeSelection(false, true);
    private void ChangeSelection(bool additive, bool remove)
    {
        _root.Chart.VerifyAccess(); EnsureEnabled();
        if (!TryCurrent(out var point) || _root.Chart.SelectionMode == ChartSelectionMode.None)
            throw new InvalidOperationException("Point selection is unavailable.");
        if (additive && _root.Chart.SelectionMode == ChartSelectionMode.Multiple && !IsSelected &&
            (_root.Chart.SelectedPoints?.Count ?? 0) >= _root.Chart.MaxSelectionCount)
            throw new InvalidOperationException("The chart selection limit has been reached.");
        if (remove) _root.Chart.AccessibilityRemovePoint(point);
        else if (additive && _root.Chart.SelectionMode != ChartSelectionMode.Multiple &&
            (_root.Chart.SelectedPoints ?? []).Any(item => !_root.Chart.AccessibilitySamePoint(item, point)))
            throw new InvalidOperationException("The chart does not allow multiple selection.");
        else _root.Chart.AccessibilitySelectPoint(point, additive);
    }
    public void Invoke()
    {
        _root.Chart.VerifyAccess(); EnsureEnabled();
        if (!TryCurrent(out var point) || !_root.Chart.AccessibilityInvokePoint(point))
            throw new InvalidOperationException("Point invocation is unavailable, disabled, or superseded by a callback.");
    }
    internal bool Matches(ChartPointHit point) => !_retired && _point is { } current && _root.Chart.AccessibilitySamePoint(current, point);
    internal void UpdatePoint(ChartPointHit point) { if (!_retired) _point = point; }
    private bool TryCurrent(out ChartPointHit point)
    {
        if (!_retired && _point is { } previous && _root.Chart.AccessibilityTryResolvePoint(previous, out point))
        { _point = point; return true; }
        point = null!; return false;
    }
    internal bool RefreshAndIsRetired()
    {
        if (_retired) return true;
        if (!_root.Chart.AccessibilityHasScene || !Series.IsVisible) return false;
        return !TryCurrent(out _);
    }
    internal void Retire() { _retired = true; _point = null; ChartChanged(); }
    protected override string GetClassNameCore() => "ChartPoint";
    protected override string GetAutomationIdCore() => $"Chart.Point.{_identity}";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;
    protected override bool IsControlElementCore() => true;
    protected override bool IsContentElementCore() => true;
    protected override string GetNameCore() => TryCurrent(out var point) ? _root.Chart.AccessibilityPointName(point) : $"{Series.Title ?? "Series"}; point unavailable";
    protected override string GetItemStatusCore() => CurrentPoint is null ? "unavailable" : IsSelected ? "selected" : "not selected";
    protected override string? GetHelpTextCore() => "Original data point. Selection follows SelectionMode and cancellable SelectionChanging. Invoke executes PointInvokedCommand independently of selection.";
    protected override bool IsEnabledCore() => _root.Chart.IsEffectivelyEnabled && CurrentPoint is not null;
    protected override bool IsOffscreenCore() => _root.IsOffscreen() || CurrentPoint is null || GetBoundingRectangleCore() == default;
    protected override Rect GetBoundingRectangleCore() => CurrentPoint is { } point ? _root.Chart.AccessibilityPointBounds(point) : default;
    protected override AutomationPeer GetParentCore() => _parent;
    protected override string? GetAcceleratorKeyCore() => null;
    protected override string? GetAccessKeyCore() => null;
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override bool IsKeyboardFocusableCore() => _root.Chart.IsPointBrowsingEnabled && IsEnabledCore();
    protected override bool HasKeyboardFocusCore() => _root.Chart.IsKeyboardFocusWithin && _root.Chart.CurrentPoint is { } current &&
        CurrentPoint is { } point && _root.Chart.AccessibilitySamePoint(current, point);
    protected override void SetFocusCore()
    {
        _root.Chart.VerifyAccess(); EnsureEnabled();
        if (!_root.Chart.IsPointBrowsingEnabled || CurrentPoint is not { } point || !_root.Chart.NavigateToPoint(point.Series, point.Index))
            throw new InvalidOperationException("Point keyboard browsing is unavailable.");
        _root.Chart.Focus();
    }
    protected override void BringIntoViewCore() { _root.Chart.VerifyAccess(); EnsureEnabled(); _root.BringIntoView(); }
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => [];
    internal void ChartChanged()
    {
        var selected = IsSelected; var name = GetName(); var value = Value;
        if (_lastSelected != selected) RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, _lastSelected, selected);
        if (_lastName != name) RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, _lastName, name);
        if (_lastValue != value) RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, _lastValue, value);
        _lastSelected = selected; _lastName = name; _lastValue = value;
    }
}
