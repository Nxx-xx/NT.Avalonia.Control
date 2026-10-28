namespace NT.AvaloniaControl.TreeDataGrid;

public partial class TreeDataGrid
{
    private bool _searching;
    /// <summary>Searches visible, enabled columns by prefix and selects the matching cell. A failed editor commit or invalidated search returns null.</summary>
    public CellAddress? SearchText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_searching) throw new InvalidOperationException("Text search cannot be reentered from its callbacks.");
        _searching = true;
        try { return SearchTextCore(text); }
        finally { _searching = false; }
    }

    private CellAddress? SearchTextCore(string text)
    {
        var state = State;
        var source = Source;
        if (text.Length == 0 || state is null || source is null) return null;
        var mayCommit = true;
        while (true)
        {
            var snapshot = source.Rows;
            if (snapshot.Count == 0) return null;
            var version = source.Version;
            var columnVersion = ColumnOperationVersion;
            var columns = VisibleColumns.ToArray();
            var start = state.CurrentCell is { } current ? source.GetViewIndex(current.Node) + 1 : 0;
            var restart = false;
            for (var offset = 0; offset < snapshot.Count && !restart; ++offset)
            {
                var row = snapshot[(Math.Max(0, start) + offset) % snapshot.Count];
                foreach (var column in columns)
                {
                    var participates = column.TryReadSearchText(row.Model, out var value);
                    // User getters/converters can change the source, columns or view. Do not act on that snapshot.
                    if (!ReferenceEquals(source, Source) || !ReferenceEquals(state, _state) || source.Version != version
                        || ColumnOperationVersion != columnVersion) return null;
                    if (!participates || value?.StartsWith(text, StringComparison.CurrentCultureIgnoreCase) != true) continue;
                    var found = new CellAddress(row.Id, column.Id);
                    var edit = state.EditSession;
                    var previousValidator = state.ValidateSelectionTarget;
                    bool accepted;
                    // The cancellable event runs before the edit. If its writer changes the view,
                    // abandon that proposal and search the fresh projection once.
                    state.ValidateSelectionTarget = () => source.Version == version && ColumnOperationVersion == columnVersion;
                    try { accepted = SelectAddress(found, extend: false, toggle: false, TreeDataGridSelectionChangeReason.Search); }
                    finally { state.ValidateSelectionTarget = previousValidator; }
                    if (!ReferenceEquals(source, Source) || !ReferenceEquals(state, _state) || ColumnOperationVersion != columnVersion) return null;
                    if (!accepted)
                    {
                        if (mayCommit && edit is not null && state.EditSession is null && source.Version != version)
                        { mayCommit = false; restart = true; break; }
                        return null;
                    }
                    if (source.Version != version || state.CurrentCell != found) return null;
                    RowsPresenter?.ScrollIntoView(row.Id);
                    BringColumnIntoView(column.Id);
                    return found;
                }
            }
            if (!restart) return null;
        }
    }
}
