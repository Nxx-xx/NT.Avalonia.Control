using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.TreeDataGrid;

internal abstract class TreeDataGridLogicalPeer : UnrealizedElementAutomationPeer
{
    private readonly AutomationPeer _parent;
    protected TreeDataGridLogicalPeer(TreeDataGridAutomationTarget target, AutomationPeer parent)
    { Target = target; _parent = parent; SetParent(parent); }
    protected TreeDataGridAutomationTarget Target { get; }
    protected override string GetAutomationIdCore() => Target.AutomationId;
    protected override string GetClassNameCore() => "TreeDataGridItem";
    protected override AutomationPeer GetParentCore() => _parent;
    protected override string? GetAcceleratorKeyCore() => null;
    protected override string? GetAccessKeyCore() => null;
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override string GetNameCore() => Target.Name;
    protected override string GetHelpTextCore() => Target.Help;
    protected override string GetItemStatusCore() => Target.Status;
    protected override Rect GetBoundingRectangleCore() => Target.Bounds;
    protected override bool IsOffscreenCore() => !Target.IsVisible || Target.RealizedControl is null;
    protected override bool IsEnabledCore() => Target.IsVisible && Target.Grid.IsEffectivelyEnabled;
    protected override bool IsKeyboardFocusableCore() => Target.IsVisible;
    protected override bool HasKeyboardFocusCore() => Target.HasFocus;
    protected override void SetFocusCore() => Target.Focus();
    protected override void BringIntoViewCore() => Target.BringIntoView();
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => [];
}

internal sealed class TreeDataGridLogicalRowPeer(TreeDataGridAutomationTarget target, AutomationPeer parent)
    : TreeDataGridLogicalPeer(target, parent), ISelectionItemProvider, IExpandCollapseProvider
{
    protected override object? GetProviderCore(Type providerType) => providerType == typeof(ISelectionItemProvider) && Target.Grid.SelectionUnit != TreeDataGridSelectionUnit.Row
        ? null : base.GetProviderCore(providerType);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;
    public bool IsSelected => Target.IsSelected;
    public ISelectionProvider SelectionContainer => Target.SelectionContainer;
    public void Select() => Target.Select(additive: false);
    public void AddToSelection() => Target.Select(additive: true);
    public void RemoveFromSelection() => Target.RemoveFromSelection();
    public ExpandCollapseState ExpandCollapseState => Target.ExpandState;
    public bool ShowsMenu => false;
    public void Expand() => Target.Expand(expanded: true);
    public void Collapse() => Target.Expand(expanded: false);
}

internal sealed class TreeDataGridLogicalCellPeer(TreeDataGridAutomationTarget target, AutomationPeer parent)
    : TreeDataGridLogicalPeer(target, parent), ISelectionItemProvider, IValueProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;
    public bool IsSelected => Target.IsSelected;
    public ISelectionProvider SelectionContainer => Target.SelectionContainer;
    public void Select() => Target.Select(additive: false);
    public void AddToSelection() => Target.Select(additive: true);
    public void RemoveFromSelection() => Target.RemoveFromSelection();
    public bool IsReadOnly => Target.IsReadOnly;
    public string Value => Target.Value;
    public void SetValue(string? value) => Target.SetValue(value);
}

internal sealed class TreeDataGridLogicalHeaderPeer : UnrealizedElementAutomationPeer, IInvokeProvider
{
    private readonly TreeDataGrid _grid;
    private readonly ColumnId _column;
    private readonly AutomationPeer _parent;
    public TreeDataGridLogicalHeaderPeer(TreeDataGrid grid, ColumnId column, AutomationPeer parent)
    { _grid = grid; _column = column; _parent = parent; SetParent(parent); }
    private TreeDataGridColumn? Column => _grid.Columns.FirstOrDefault(item => item.Id == _column);
    private TreeDataGridColumnHeader? RealizedHeader => _grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().FirstOrDefault(header => header.Column?.Id == _column);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.HeaderItem;
    protected override string GetAutomationIdCore() => $"TreeDataGrid.Column.{_column.Value}";
    protected override string GetClassNameCore() => nameof(TreeDataGridColumnHeader);
    protected override AutomationPeer GetParentCore() => _parent;
    protected override string? GetAcceleratorKeyCore() => null;
    protected override string? GetAccessKeyCore() => null;
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override string GetNameCore() => Column?.Header?.ToString() ?? "";
    protected override string GetItemStatusCore() => $"columnIndex={_grid.GetColumnIndex(_column)}; sortDirection={(_grid.Source?.SortColumn == _column ? _grid.Source.SortDirection : TreeDataGridSortDirection.None)}";
    protected override string GetHelpTextCore() => $"ColumnId={_column.Value}";
    protected override bool IsOffscreenCore() => !_grid.ShowColumnHeaders || _grid.GetColumnIndex(_column) < 0 || RealizedHeader is null;
    protected override Rect GetBoundingRectangleCore() => RealizedHeader is { } header ? ControlAutomationPeer.CreatePeerForElement(header)!.GetBoundingRectangle() : default;
    protected override bool IsEnabledCore() => _grid.IsEffectivelyEnabled && _grid.ShowColumnHeaders && _grid.GetColumnIndex(_column) >= 0;
    protected override bool IsKeyboardFocusableCore() => IsEnabledCore();
    protected override void BringIntoViewCore()
    {
        _grid.VerifyAccess();
        if (!_grid.ShowColumnHeaders || _grid.GetColumnIndex(_column) < 0) throw new InvalidOperationException("The column header is hidden or removed.");
        _grid.BringColumnIntoView(_column);
    }
    protected override void SetFocusCore() { BringIntoViewCore(); _grid.Focus(); }
    public void Invoke()
    {
        _grid.VerifyAccess();
        EnsureEnabled();
        if (Column is { CanUserSort: true } column && _grid.CanUserSortColumns) _grid.ToggleColumnSort(column);
    }
}
