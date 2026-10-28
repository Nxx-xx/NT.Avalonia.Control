using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Selection semantics and identity-based access for TreeDataGrid. Avalonia 12 does not expose grid/table provider interfaces.</summary>
public sealed class TreeDataGridAutomationPeer(TreeDataGrid owner) : ControlAutomationPeer(owner), ISelectionProvider, IScrollProvider
{
    private readonly TreeDataGridAutomationPropertyCache _notifications = new();
    private readonly List<(WeakReference<Control> Control, NodeId? Node, ColumnId? Column)> _children = [];
    private TreeDataGridSelectionSnapshot? _selection;
    private TreeDataGridSelectionUnit? _selectionUnit;
    private bool _childrenKnown;
    public int RowCount => owner.Source?.Rows.Count ?? 0;
    public int ColumnCount => owner.VisibleColumns.Count;
    public bool CanSelectMultiple => owner.SelectionMode == TreeDataGridSelectionMode.Multiple;
    public bool IsSelectionRequired => false;
    private IScrollProvider? ScrollProvider => owner.AutomationScrollViewer is { } viewer
        ? CreatePeerForElement(viewer)?.GetProvider<IScrollProvider>() : null;
    public bool HorizontallyScrollable => ScrollProvider?.HorizontallyScrollable == true;
    public double HorizontalScrollPercent => ScrollProvider?.HorizontalScrollPercent ?? -1;
    public double HorizontalViewSize => ScrollProvider?.HorizontalViewSize ?? 100;
    public bool VerticallyScrollable => ScrollProvider?.VerticallyScrollable == true;
    public double VerticalScrollPercent => ScrollProvider?.VerticalScrollPercent ?? -1;
    public double VerticalViewSize => ScrollProvider?.VerticalViewSize ?? 100;

    public void Scroll(ScrollAmount horizontalAmount, ScrollAmount verticalAmount)
    {
        owner.VerifyAccess();
        EnsureEnabled();
        var provider = ScrollProvider ?? throw new InvalidOperationException("The template has no automation scroll provider.");
        provider.Scroll(horizontalAmount, verticalAmount);
        TopLevel.GetTopLevel(owner)?.UpdateLayout();
        NotifyStateChanged();
    }

    public void SetScrollPercent(double horizontalPercent, double verticalPercent)
    {
        owner.VerifyAccess();
        EnsureEnabled();
        var provider = ScrollProvider ?? throw new InvalidOperationException("The template has no automation scroll provider.");
        provider.SetScrollPercent(horizontalPercent, verticalPercent);
        TopLevel.GetTopLevel(owner)?.UpdateLayout();
        NotifyStateChanged();
    }

    /// <summary>Returns a peer for an existing node, including a node outside the viewport. Hidden nodes can be inspected but cannot be selected until visible.</summary>
    public AutomationPeer? GetRowPeer(NodeId node)
    {
        owner.VerifyAccess();
        if (owner.Source is not { } source || !source.TryGetRow(node, out _)) return null;
        return new TreeDataGridLogicalRowPeer(new TreeDataGridAutomationTarget(owner, source, node, null), this);
    }

    /// <summary>Returns an identity-bound peer. Calling BringIntoView realizes a visible row/column without changing selection.</summary>
    public AutomationPeer? GetCellPeer(NodeId node, ColumnId column)
    {
        owner.VerifyAccess();
        if (owner.Source is not { } source || !source.TryGetRow(node, out _) || owner.Columns.All(item => item.Id != column)) return null;
        return new TreeDataGridLogicalCellPeer(new TreeDataGridAutomationTarget(owner, source, node, column), GetRowPeer(node)!);
    }

    public AutomationPeer? GetColumnHeaderPeer(ColumnId column)
    {
        owner.VerifyAccess();
        if (!owner.ShowColumnHeaders || owner.Columns.All(item => item.Id != column)) return null;
        return new TreeDataGridLogicalHeaderPeer(owner, column, this);
    }

    public IReadOnlyList<AutomationPeer> GetSelection()
    {
        owner.VerifyAccess();
        var state = owner.State;
        if (state is null) return [];
        return owner.SelectionUnit == TreeDataGridSelectionUnit.Row
            ? state.SelectedNodes.Select(GetRowPeer).OfType<AutomationPeer>().ToArray()
            : state.SelectedCells.Select(cell => GetCellPeer(cell.Node, cell.Column)).OfType<AutomationPeer>().ToArray();
    }

    internal void NotifyStateChanged()
    {
        var children = CurrentChildren();
        var changed = !_childrenKnown || children.Count != _children.Count;
        for (var i = 0; !changed && i < children.Count; i++)
            changed = !_children[i].Control.TryGetTarget(out var control) || !ReferenceEquals(control, children[i].Control)
                || _children[i].Node != children[i].Node || _children[i].Column != children[i].Column;
        if (changed)
        {
            _childrenKnown = true; _children.Clear();
            foreach (var child in children) _children.Add((new(child.Control), child.Node, child.Column));
            RaiseChildrenChangedEvent();
        }
        var selection = owner.State?.CaptureSelection();
        var selectionChanged = _selectionUnit != owner.SelectionUnit || !SameSelection(_selection, selection);
        _selection = selection; _selectionUnit = owner.SelectionUnit;
        if (selectionChanged) RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null, null);
        _notifications.Notify(this, SelectionPatternIdentifiers.CanSelectMultipleProperty, CanSelectMultiple);
        _notifications.Notify(this, AutomationElementIdentifiers.ItemStatusProperty, GetItemStatus());
    }

    private static bool SameSelection(TreeDataGridSelectionSnapshot? a, TreeDataGridSelectionSnapshot? b) =>
        a is null || b is null ? a is null && b is null : a.SelectedNodes.ToHashSet().SetEquals(b.SelectedNodes)
            && a.CellRows.ToHashSet().SetEquals(b.CellRows) && a.CellColumns.ToHashSet().SetEquals(b.CellColumns);

    private List<(Control Control, NodeId? Node, ColumnId? Column)> CurrentChildren()
    {
        var result = new List<(Control, NodeId?, ColumnId?)>();
        var headers = owner.GetVisualDescendants().OfType<TreeDataGridColumnHeadersPresenter>().FirstOrDefault();
        if (owner.ShowColumnHeaders && headers is { IsEffectivelyVisible: true })
            foreach (var header in headers.Children.OfType<TreeDataGridColumnHeader>().OrderBy(h => h.Column is { } c ? owner.GetColumnIndex(c.Id) : -1))
                result.Add((header, null, header.Column?.Id));
        if (owner.RowsPresenter is { } rows)
            foreach (var row in rows.RealizedRows) result.Add((row, row.Row?.Id, null));
        return result;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;
    protected override string GetClassNameCore() => nameof(TreeDataGrid);
    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : nameof(TreeDataGrid);
    protected override string GetItemStatusCore() => $"Rows={RowCount}; Columns={ColumnCount}";
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore()
    {
        var result = new List<AutomationPeer>();
        var headers = owner.GetVisualDescendants().OfType<TreeDataGridColumnHeadersPresenter>().FirstOrDefault();
        if (owner.ShowColumnHeaders && headers is { IsEffectivelyVisible: true })
            result.AddRange(headers.Children.OfType<TreeDataGridColumnHeader>()
                .OrderBy(header => header.Column is { } column ? owner.GetColumnIndex(column.Id) : -1)
                .Select(CreatePeerForElement).OfType<AutomationPeer>());
        if (owner.RowsPresenter is { } rows)
            result.AddRange(rows.RealizedRows.Select(CreatePeerForElement).OfType<AutomationPeer>());
        return result;
    }
}

public partial class TreeDataGrid
{
    internal ScrollViewer? AutomationScrollViewer { get; private set; }
    protected override AutomationPeer OnCreateAutomationPeer() => new TreeDataGridAutomationPeer(this);

    internal void NotifyAutomationStateChanged()
    {
        if (ControlAutomationPeer.FromElement(this) is not TreeDataGridAutomationPeer peer) return;
        peer.NotifyStateChanged();
        if (RowsPresenter is not { } rows) return;
        foreach (var row in rows.RealizedRows)
        {
            if (ControlAutomationPeer.FromElement(row) is TreeDataGridRowPresenterAutomationPeer rowPeer) rowPeer.NotifyStateChanged();
            foreach (var cell in row.RealizedCells)
                if (ControlAutomationPeer.FromElement(cell) is TreeDataGridCellPresenterAutomationPeer cellPeer) cellPeer.NotifyStateChanged();
        }
    }
}
