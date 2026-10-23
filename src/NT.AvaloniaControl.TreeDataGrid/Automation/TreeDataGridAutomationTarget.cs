using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace NT.AvaloniaControl.TreeDataGrid;

internal sealed class TreeDataGridAutomationTarget(TreeDataGrid grid, TreeDataGridSource source, NodeId node, ColumnId? column)
{
    public TreeDataGrid Grid { get; } = grid;
    public TreeDataGridSource Source { get; } = source;
    public NodeId Node { get; } = node;
    public ColumnId? ColumnId { get; } = column;
    public TreeDataGridRow? Row => ReferenceEquals(Grid.Source, Source) && Source.TryGetRow(Node, out var row) ? row : null;
    public TreeDataGridColumn? Column => ColumnId is { } id ? Grid.Columns.FirstOrDefault(item => item.Id == id) : null;
    public int RowIndex => ReferenceEquals(Grid.Source, Source) ? Source.GetViewIndex(Node) : -1;
    public int ColumnIndex => ColumnId is { } id ? Grid.GetColumnIndex(id) : -1;
    public bool IsVisible => RowIndex >= 0 && (ColumnId is null || ColumnIndex >= 0);
    public CellAddress Address => new(Node, ColumnId ?? throw new InvalidOperationException("This automation target is a row."));
    public bool IsSelected => Row is not null && (ColumnId is null || Column is not null) && Grid.State is { } state && (ColumnId is null || Grid.SelectionUnit == TreeDataGridSelectionUnit.Row
        ? state.SelectedNodes.Contains(Node) : state.IsCellSelected(Address));
    public string AutomationId => ColumnId is { } id ? $"TreeDataGrid.Cell.{Node.Value}.{id.Value}" : $"TreeDataGrid.Row.{Node.Value}";
    public TreeDataGridCellPresenter? RealizedCell => Grid.RowsPresenter?.RealizedRows.SelectMany(row => row.RealizedCells)
        .FirstOrDefault(cell => cell.Row?.Id == Node && cell.Column?.Id == ColumnId);
    public TreeDataGridRowPresenter? RealizedRow => Grid.RowsPresenter?.RealizedRows.FirstOrDefault(row => row.Row?.Id == Node);
    public Control? RealizedControl => ColumnId is null ? RealizedRow : RealizedCell;
    public ExpandCollapseState ExpandState => Row is not { HasChildren: true } row ? ExpandCollapseState.LeafNode :
        row.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
    public bool IsReadOnly
    {
        get
        {
            if (!IsVisible || Grid.IsReadOnly || Column is not { } column || column.IsReadOnly || column.EffectiveColumn.IsReadOnly || Row is not { } row) return true;
            return column.GetWriter(row.Model) is null;
        }
    }
    public string Value
    {
        get
        {
            if (!IsVisible || Column is not { } column || Row is not { } row) return "";
            if (Grid.State?.EditSession is { } edit && edit.Cell == Address) return edit.Value?.ToString() ?? "";
            if (RealizedCell is { } cell) return cell.Value?.ToString() ?? "";
            var version = Source.Version;
            var value = column.ReadValue(row.Model)?.ToString() ?? "";
            return ReferenceEquals(Grid.Source, Source) && Source.Version == version && IsVisible ? value : "";
        }
    }
    public string Name => ColumnId is null ? $"Row {RowIndex + 1}" : $"{Column?.Header}: {Value}";
    public string Status => ColumnId is null
        ? $"rowIndex={RowIndex}; depth={Row?.Depth ?? -1}; {ExpandState}; selected={IsSelected}"
        : $"rowIndex={RowIndex}; columnIndex={ColumnIndex}; readOnly={IsReadOnly}; selected={IsSelected}; current={Grid.State?.CurrentCell == Address}";
    public string Help => $"NodeId={Node.Value}" + (ColumnId is { } id ? $"; ColumnId={id.Value}" : "") +
        (Grid.State?.EditSession is { } edit && ColumnId is not null && edit.Cell == Address && edit.ValidationError is { } error ? $"; Error: {error}" : "");
    public bool HasFocus => IsVisible && Grid.IsKeyboardFocusWithin && Grid.State?.CurrentCell is { } cell && cell.Node == Node && (ColumnId is null || cell.Column == ColumnId);

    public void EnsureVisible()
    {
        Grid.VerifyAccess();
        if (!IsVisible) throw new InvalidOperationException("The automation target is hidden, removed, or belongs to a replaced source.");
        if (!Grid.IsEffectivelyEnabled) throw new InvalidOperationException("The TreeDataGrid is disabled.");
    }
    public void BringIntoView()
    {
        EnsureVisible();
        Grid.RowsPresenter?.ScrollIntoView(Node);
        if (ColumnId is { } column) Grid.BringColumnIntoView(column);
        TopLevel.GetTopLevel(Grid)?.UpdateLayout();
        EnsureVisible();
    }
    public void Focus()
    {
        EnsureVisible();
        var destination = ColumnId ?? Grid.VisibleColumns.FirstOrDefault()?.Id;
        if (destination is not { } id) throw new InvalidOperationException("There are no visible columns to focus.");
        var address = new CellAddress(Node, id);
        if (Grid.State?.EditSession is { } edit && edit.Cell != address && !Grid.CommitEdit()) throw new InvalidOperationException("The current edit must be valid before moving focus.");
        EnsureVisible();
        Grid.State?.SetCurrentCell(address);
        BringIntoView();
        Grid.Focus();
    }
    public void Select(bool additive)
    {
        EnsureVisible();
        if (Grid.State?.EditSession is not null && !Grid.CommitEdit()) throw new InvalidOperationException("The current edit must be valid before changing selection.");
        EnsureVisible();
        var state = Grid.State ?? throw new InvalidOperationException("The automation source changed.");
        if (ColumnId is null || Grid.SelectionUnit == TreeDataGridSelectionUnit.Row) state.Select(Node, additive);
        else state.SelectCell(Address, extend: additive);
    }
    public void RemoveFromSelection()
    {
        EnsureVisible();
        if (!IsSelected || Grid.State is not { } state) return;
        if (ColumnId is null || Grid.SelectionUnit == TreeDataGridSelectionUnit.Row)
        {
            if (Grid.SelectionMode == TreeDataGridSelectionMode.Multiple) state.ToggleSelection(Node);
            else state.ClearSelection();
        }
        else
        {
            if (state.SelectedCells.Take(2).Count() > 1) throw new InvalidOperationException("Removing one cell would split the rectangular selection.");
            state.ClearSelection();
        }
    }
    public void Expand(bool expanded)
    {
        EnsureVisible();
        if (Row is { HasChildren: true }) Source.SetExpanded(Node, expanded);
    }
    public void SetValue(string? value)
    {
        ArgumentNullException.ThrowIfNull(value);
        EnsureVisible();
        if (IsReadOnly) throw new InvalidOperationException("The cell is read-only.");
        EnsureVisible(); // Binding-path getters may have replaced the source during the read-only check.
        if (!Grid.BeginEdit(Address)) throw new InvalidOperationException("An edit could not be started for this cell.");
        EnsureVisible();
        var edit = Grid.State?.EditSession;
        if (edit?.Cell != Address) throw new InvalidOperationException("The automation edit target changed.");
        edit.Value = value;
        EnsureVisible();
        if (!Grid.CommitEdit()) throw new InvalidOperationException(edit.ValidationError ?? "The value could not be committed.");
    }
    public ISelectionProvider SelectionContainer => (TreeDataGridAutomationPeer)ControlAutomationPeer.CreatePeerForElement(Grid)!;
    public Rect Bounds => RealizedControl is { } control ? ControlAutomationPeer.CreatePeerForElement(control)!.GetBoundingRectangle() : default;
}
