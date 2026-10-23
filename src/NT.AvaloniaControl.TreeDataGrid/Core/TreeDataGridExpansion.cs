using System.Runtime.ExceptionServices;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>A model row snapshot for an expansion notification, independent of visual row containers.</summary>
public class TreeDataGridRowModelEventArgs : EventArgs
{
    public TreeDataGridRowModelEventArgs(TreeDataGridRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Row = row;
    }

    public TreeDataGridRow Row { get; }
}

/// <summary>A cancellable expansion request. Synchronous child loading is allowed in this notification.</summary>
public sealed class TreeDataGridRowCancelEventArgs(TreeDataGridRow row) : TreeDataGridRowModelEventArgs(row)
{
    public bool Cancel { get; set; }
}

/// <summary>Optional lazy-child hints and a model expansion binding. Delegate instances are captured on construction.</summary>
public sealed class HierarchicalTreeDataGridSourceOptions<TModel> where TModel : class
{
    /// <summary>Advertises unloaded children. Existing children always make HasChildren true.</summary>
    public Func<TModel, bool>? HasChildrenSelector { get; init; }

    /// <summary>Reads the initial and externally changed expansion state. Provide a writer together with this selector.</summary>
    public Func<TModel, bool>? IsExpandedSelector { get; init; }

    /// <summary>Stores a requested expansion state. It must succeed or throw without changing the model.</summary>
    public Action<TModel, bool>? IsExpandedWriter { get; init; }
}

public abstract partial class TreeDataGridSource
{
    public event EventHandler<TreeDataGridRowCancelEventArgs>? RowExpanding;
    public event EventHandler<TreeDataGridRowModelEventArgs>? RowExpanded;
    public event EventHandler<TreeDataGridRowCancelEventArgs>? RowCollapsing;
    public event EventHandler<TreeDataGridRowModelEventArgs>? RowCollapsed;

    /// <summary>Resolves a published source path to its identity before requesting an expansion change.</summary>
    public virtual bool SetExpandedAt(IndexPath modelIndex, bool expanded) =>
        TryGetNodeId(modelIndex, out var id) && SetExpanded(id, expanded);

    public virtual int ExpandAll() => SetExpandedRecursive(true);
    public virtual int CollapseAll() => SetExpandedRecursive(false);

    /// <summary>
    /// Changes a tree recursively. A false predicate prunes the whole subtree. The default implementation
    /// traverses the currently indexed tree; hierarchical sources also visit children loaded by before events.
    /// </summary>
    public virtual int SetExpandedRecursive(bool expanded, Func<TreeDataGridRow, bool>? predicate = null)
    {
        var ordered = new List<NodeId>();
        void Visit(IndexPath parent)
        {
            for (var index = 0; TryGetNodeId(parent.Append(index), out var id); index++)
            {
                if (!TryGetRow(id, out var row) || predicate?.Invoke(row!) == false) continue;
                if (expanded && row!.HasChildren) ordered.Add(id);
                Visit(parent.Append(index));
                if (!expanded && row!.IsExpanded) ordered.Add(id);
            }
        }
        Visit(default);
        using var scope = DeferRefresh();
        return ordered.Count(id => SetExpanded(id, expanded));
    }

    protected void OnRowExpanding(TreeDataGridRowCancelEventArgs args) => InvokeExpansionHandlers(RowExpanding, args);
    protected void OnRowExpanded(TreeDataGridRowModelEventArgs args) => InvokeExpansionHandlers(RowExpanded, args);
    protected void OnRowCollapsing(TreeDataGridRowCancelEventArgs args) => InvokeExpansionHandlers(RowCollapsing, args);
    protected void OnRowCollapsed(TreeDataGridRowModelEventArgs args) => InvokeExpansionHandlers(RowCollapsed, args);

    protected void ClearExpansionEventHandlers()
    {
        RowExpanding = null;
        RowExpanded = null;
        RowCollapsing = null;
        RowCollapsed = null;
    }

    private void InvokeExpansionHandlers<TArgs>(EventHandler<TArgs>? handlers, TArgs args) where TArgs : EventArgs
    {
        if (handlers is null) return;
        List<Exception>? errors = null;
        var cancelled = false;
        foreach (EventHandler<TArgs> handler in handlers.GetInvocationList())
        {
            try { handler(this, args); }
            catch (Exception error) { (errors ??= []).Add(error); }
            if (args is TreeDataGridRowCancelEventArgs cancellable)
            {
                cancelled |= cancellable.Cancel;
                cancellable.Cancel = cancelled;
            }
        }
        if (errors is { Count: 1 }) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is { Count: > 1 }) throw new AggregateException("Expansion listeners failed.", errors);
    }
}
