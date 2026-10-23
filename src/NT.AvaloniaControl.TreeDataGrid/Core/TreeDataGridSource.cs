namespace NT.AvaloniaControl.TreeDataGrid;

public enum ProjectionChangeReason
{
    Refresh,
    CollectionChanged,
    ModelChanged,
    ExpansionChanged,
    SortChanged,
    FilterChanged,
}

/// <summary>Describes a committed projection. All source mappings already reflect <see cref="Version"/>.</summary>
public sealed class ProjectionChangedEventArgs : EventArgs
{
    public ProjectionChangedEventArgs(long version, ProjectionChangeReason reason, IEnumerable<NodeId> removedNodes)
    {
        ArgumentNullException.ThrowIfNull(removedNodes);
        Version = version;
        Reason = reason;
        RemovedNodes = Array.AsReadOnly(removedNodes.ToArray());
    }

    public long Version { get; }
    public ProjectionChangeReason Reason { get; }
    public IReadOnlyList<NodeId> RemovedNodes { get; }
}

/// <summary>
/// Owns source subscriptions and the visible projection. Access and model/collection notifications must use
/// the thread that created the source. Dispose it when its owner no longer needs it.
/// </summary>
public abstract partial class TreeDataGridSource : IDisposable
{
    public abstract IReadOnlyList<TreeDataGridRow> Rows { get; }
    public abstract long Version { get; }
    /// <summary>
    /// True while a refresh scope, projection callback, queued mutations, or a failed projection delay an update.
    /// Editing must wait until this becomes false because the published mappings may describe an earlier graph.
    /// </summary>
    public abstract bool IsRefreshDeferred { get; }
    /// <summary>
    /// Runs after a projection is committed. Listener failures are rethrown after the other listeners
    /// and queued model changes have been processed, so an unrelated listener cannot prevent state cleanup.
    /// </summary>
    public abstract event EventHandler<ProjectionChangedEventArgs>? ProjectionChanged;
    // Controlled collections publish their projection before their notification transaction ends.
    // Command consumers need a second notification when editing is safe again.
    internal event EventHandler? RefreshResumed;
    internal void NotifyRefreshResumed()
    {
        if (RefreshResumed is not { } handlers) return;
        List<Exception>? errors = null;
        foreach (EventHandler handler in handlers.GetInvocationList())
            try { handler(this, EventArgs.Empty); } catch (Exception error) { (errors ??= []).Add(error); }
        if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is { Count: > 1 }) throw new AggregateException("Refresh resumed listeners failed.", errors);
    }
    internal void ClearRefreshResumedHandlers() => RefreshResumed = null;
    /// <summary>Resolves both visible and hidden nodes.</summary>
    public abstract bool TryGetRow(NodeId id, out TreeDataGridRow? row);
    public abstract bool TryGetNodeId(IndexPath modelIndex, out NodeId id);
    /// <summary>Returns -1 for an unknown or hidden node.</summary>
    public abstract int GetViewIndex(NodeId id);
    internal virtual bool IsEditNodeCurrent(NodeId id, object model) => TryGetRow(id, out var row) && ReferenceEquals(row!.Model, model);
    /// <summary>Changes a node's expansion state. Returns false for a missing node or unchanged state.</summary>
    public abstract bool SetExpanded(NodeId id, bool expanded);
    /// <summary>
    /// Keeps the current projection until the outermost scope is disposed, then publishes one change.
    /// Model subscriptions are maintained during the scope. Scopes must be disposed on the creating thread.
    /// </summary>
    public abstract IDisposable DeferRefresh();
    /// <summary>Reconciles non-notifying changes while retaining identities for surviving model occurrences.</summary>
    public abstract void Refresh();
    public abstract void Dispose();
}
