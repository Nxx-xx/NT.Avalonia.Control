using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace NT.AvaloniaControl.TreeDataGrid;

internal sealed class TreeDataGridCellPresenterAutomationPeer(TreeDataGridCellPresenter owner) : ControlAutomationPeer(owner), ISelectionItemProvider, IValueProvider
{
    private TreeDataGridAutomationTarget? Target => owner.GridOwner is { Source: { } source } grid && owner.Row is { } row && owner.Column is { } column
        ? new TreeDataGridAutomationTarget(grid, source, row.Id, column.Id) : null;
    private TreeDataGridAutomationTarget RequireTarget() => Target ?? throw new InvalidOperationException("The cell has been recycled.");
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;
    protected override string GetClassNameCore() => nameof(TreeDataGridCellPresenter);
    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Target?.Name;
    protected override string GetAutomationIdCore() => Target?.AutomationId ?? "";
    protected override string GetItemStatusCore() => Target?.Status ?? "";
    protected override string? GetHelpTextCore() => owner.ValidationError ?? Target?.Help ?? base.GetHelpTextCore();
    protected override bool HasKeyboardFocusCore() => Target?.HasFocus == true;
    protected override bool IsKeyboardFocusableCore() => Target?.IsVisible == true;
    protected override void SetFocusCore() => RequireTarget().Focus();
    public bool IsSelected => Target?.IsSelected == true;
    public ISelectionProvider SelectionContainer => RequireTarget().SelectionContainer;
    public void Select() => RequireTarget().Select(additive: false);
    public void AddToSelection() => RequireTarget().Select(additive: true);
    public void RemoveFromSelection() => RequireTarget().RemoveFromSelection();
    public bool IsReadOnly => Target?.IsReadOnly != false;
    public string Value => Target?.Value ?? "";
    public void SetValue(string? value) => RequireTarget().SetValue(value);
}

internal sealed class TreeDataGridRowPresenterAutomationPeer(TreeDataGridRowPresenter owner) : ControlAutomationPeer(owner), ISelectionItemProvider, IExpandCollapseProvider
{
    protected override object? GetProviderCore(Type providerType) => providerType == typeof(ISelectionItemProvider) && owner.GridOwner?.SelectionUnit != TreeDataGridSelectionUnit.Row
        ? null : base.GetProviderCore(providerType);
    private TreeDataGridAutomationTarget? Target => owner.GridOwner is { Source: { } source } grid && owner.Row is { } row
        ? new TreeDataGridAutomationTarget(grid, source, row.Id, null) : null;
    private TreeDataGridAutomationTarget RequireTarget() => Target ?? throw new InvalidOperationException("The row has been recycled.");
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;
    protected override string GetClassNameCore() => nameof(TreeDataGridRowPresenter);
    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Target?.Name;
    protected override string GetAutomationIdCore() => Target?.AutomationId ?? "";
    protected override string GetItemStatusCore() => Target?.Status ?? "";
    protected override string? GetHelpTextCore() => Target?.Help ?? base.GetHelpTextCore();
    protected override AutomationPeer? GetParentCore() => owner.GridOwner is { } grid ? CreatePeerForElement(grid) : base.GetParentCore();
    protected override bool HasKeyboardFocusCore() => Target?.HasFocus == true;
    protected override bool IsKeyboardFocusableCore() => Target?.IsVisible == true;
    protected override void SetFocusCore() => RequireTarget().Focus();
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => owner.RealizedCells.Select(CreatePeerForElement).OfType<AutomationPeer>().ToArray();
    public bool IsSelected => Target?.IsSelected == true;
    public ISelectionProvider SelectionContainer => RequireTarget().SelectionContainer;
    public void Select() => RequireTarget().Select(additive: false);
    public void AddToSelection() => RequireTarget().Select(additive: true);
    public void RemoveFromSelection() => RequireTarget().RemoveFromSelection();
    public ExpandCollapseState ExpandCollapseState => Target?.ExpandState ?? ExpandCollapseState.LeafNode;
    public bool ShowsMenu => false;
    public void Expand() => RequireTarget().Expand(expanded: true);
    public void Collapse() => RequireTarget().Expand(expanded: false);
}

internal sealed class TreeDataGridColumnHeaderAutomationPeer(TreeDataGridColumnHeader owner) : ControlAutomationPeer(owner), IInvokeProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.HeaderItem;
    protected override string GetClassNameCore() => nameof(TreeDataGridColumnHeader);
    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Column?.Header?.ToString();
    protected override string GetAutomationIdCore() => owner.Column is { } column ? $"TreeDataGrid.Column.{column.Id.Value}" : "";
    protected override AutomationPeer? GetParentCore() => owner.GridOwner is { } grid ? CreatePeerForElement(grid) : base.GetParentCore();
    protected override string GetItemStatusCore() => $"columnIndex={(owner.Column is { } column ? owner.GridOwner?.GetColumnIndex(column.Id) ?? -1 : -1)}; sortDirection={owner.SortDirection}";
    public void Invoke()
    {
        EnsureEnabled();
        if (owner.GridOwner is { CanUserSortColumns: true } grid && owner.Column is { CanUserSort: true, IsVisible: true } column) grid.ToggleColumnSort(column);
    }
}
