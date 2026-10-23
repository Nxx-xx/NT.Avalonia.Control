using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.TreeDataGrid;

[Flags]
public enum TreeDataGridEditTriggers { None = 0, F2 = 1, SingleClick = 2, DoubleClick = 4 }

public partial class TreeDataGrid
{
    public static readonly StyledProperty<TreeDataGridSelectionUnit> SelectionUnitProperty = AvaloniaProperty.Register<TreeDataGrid, TreeDataGridSelectionUnit>(nameof(SelectionUnit));
    public static readonly StyledProperty<TreeDataGridSelectionMode> SelectionModeProperty = AvaloniaProperty.Register<TreeDataGrid, TreeDataGridSelectionMode>(nameof(SelectionMode));
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<TreeDataGrid, bool>(nameof(IsReadOnly));
    public static readonly StyledProperty<TreeDataGridEditTriggers> EditTriggersProperty = AvaloniaProperty.Register<TreeDataGrid, TreeDataGridEditTriggers>(nameof(EditTriggers), TreeDataGridEditTriggers.F2 | TreeDataGridEditTriggers.DoubleClick);
    private TreeDataGridState? _state;
    private TreeDataGridSource? _stateSource;
    private string _searchPrefix = "";
    private DateTime _lastSearch;
    public TreeDataGridSelectionUnit SelectionUnit { get => GetValue(SelectionUnitProperty); set => SetValue(SelectionUnitProperty, value); }
    public TreeDataGridSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public TreeDataGridEditTriggers EditTriggers { get => GetValue(EditTriggersProperty); set => SetValue(EditTriggersProperty, value); }
    public TreeDataGridState? State { get { EnsureInteractionState(); RefreshVisibleColumns(); return _state; } }
    public event EventHandler? SelectionChanged;
    internal bool IsInputMethodComposing => RowsPresenter?.RealizedRows.SelectMany(row => row.RealizedCells).Any(cell => cell.IsComposing) == true;

    private void InitializeInteraction()
    {
        Focusable = true;
        AddHandler(PointerPressedEvent, InteractionPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, InteractionKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(TextInputEvent, InteractionTextInput, RoutingStrategies.Bubble);
    }
    private void EnsureInteractionState()
    {
        if (ReferenceEquals(_stateSource, Source)) return;
        var hadSource = _stateSource is not null;
        if (_state is { } old) { old.Changed -= InteractionStateChanged; old.SelectionChanged -= InteractionSelectionChanged; old.ProjectionReconciled -= MvvmProjectionReconciled; old.ReleaseOwner(); }
        _state = null; _stateSource = Source;
        if (Source is { } source)
        {
            _state = new TreeDataGridState(source) { SelectionMode = SelectionMode };
            _state.SetColumns(VisibleColumns.Select(column => column.Id).ToArray());
            if (!_attached) _state.Suspend();
            _state.Changed += InteractionStateChanged;
            _state.SelectionChanged += InteractionSelectionChanged;
            _state.ProjectionReconciled += MvvmProjectionReconciled;
        }
        MvvmSourceChanged(hadSource);
    }
    private void ResumeInteraction() { EnsureInteractionState(); ResumeMvvm(); _state?.Resume(); }
    private void SuspendInteraction() { SuspendMvvm(); _state?.Suspend(); }
    private void SynchronizeStateColumns() { _state?.SynchronizeOwnerColumns(VisibleColumns.Select(column => column.Id).ToArray()); SynchronizeMvvm(); }
    private void OnInteractionPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SourceProperty) EnsureInteractionState();
        else if (change.Property == SelectionModeProperty && _state is not null) _state.SelectionMode = SelectionMode;
        else if (change.Property == SelectionUnitProperty) _state?.ClearSelection();
        else if (change.Property == IsReadOnlyProperty && IsReadOnly) CancelEdit();
        OnMvvmPropertyChanged(change);
    }
    private void InteractionSelectionChanged(object? sender, EventArgs e) => SelectionChanged?.Invoke(this, EventArgs.Empty);
    private void InteractionStateChanged(object? sender, EventArgs e)
    {
        SynchronizeMvvm();
        NotifyAutomationStateChanged();
        PseudoClasses.Set(":editing", _state?.EditSession is not null);
        if (RowsPresenter is { } rows)
            foreach (var cell in rows.RealizedRows.SelectMany(row => row.RealizedCells).ToArray()) cell.ApplyInteractionState();
    }
    public bool BeginEdit(CellAddress cell)
    {
        var source = Source; var state = State;
        if (IsReadOnly || source is null || state is null || source.IsRefreshDeferred || !source.TryGetRow(cell.Node, out var row) || source.GetViewIndex(cell.Node) < 0) return false;
        var column = Columns.FirstOrDefault(item => item.Id == cell.Column);
        if (column is null || !column.IsVisible || column.IsReadOnly || column.EffectiveColumn.IsReadOnly) return false;
        var writer = column.GetWriter(row!.Model, () => ReferenceEquals(Source, source) && ReferenceEquals(_state, state) &&
            state.EditSession?.Cell == cell && source.IsEditNodeCurrent(cell.Node, row.Model) && Columns.Contains(column) && column.IsVisible &&
            !IsReadOnly && !column.IsReadOnly && !column.EffectiveColumn.IsReadOnly);
        if (writer is null) return false;
        if (state.EditSession?.Cell == cell) return true;
        if (state.EditSession is not null && !CommitEdit()) return false;
        if (!ReferenceEquals(Source, source) || !Columns.Contains(column) || !column.IsVisible) return false;
        try { state.BeginEdit(cell, column.ReadValue, writer, column.EffectiveColumn.ValueValidator); }
        catch (InvalidOperationException) when (!ReferenceEquals(_state, state) || !ReferenceEquals(Source, source)) { return false; }
        if (!ReferenceEquals(state, _state) || !ReferenceEquals(Source, source) || state.EditSession?.Cell != cell) return false;
        RowsPresenter?.ScrollIntoView(cell.Node);
        BringColumnIntoView(cell.Column);
        InteractionStateChanged(this, EventArgs.Empty);
        return true;
    }
    public bool CommitEdit()
    {
        var state = State;
        if (state?.EditSession is not { } edit) return true;
        if (IsInputMethodComposing) return false;
        var column = Columns.FirstOrDefault(item => item.Id == edit.Cell.Column);
        if (column is null || !column.IsVisible || IsReadOnly || column.IsReadOnly || column.EffectiveColumn.IsReadOnly) { state.CancelEdit(); return false; }
        try
        {
            var converted = column.ConvertEditValue(edit.Value, edit.Model);
            if (!ReferenceEquals(state, _state) || !ReferenceEquals(state.EditSession, edit)) return false;
            return state.CommitEdit(converted);
        }
        catch (Exception error) when (error is FormatException or InvalidCastException or ArgumentException or NotSupportedException or OverflowException or InvalidOperationException or System.Reflection.TargetInvocationException)
        { if (ReferenceEquals(state, _state)) state.SetValidationError(error.InnerException?.Message ?? error.Message); return false; }
    }
    public void CancelEdit() => _state?.CancelOwnedEdit();
    partial void TryCommitBeforeSort(ref bool canSort) => canSort = CommitEdit();
    public CellAddress? SearchText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var state = State; var source = Source;
        if (text.Length == 0 || state is null || source is null || source.Rows.Count == 0) return null;
        var snapshot = source.Rows;
        var start = state.CurrentCell is { } current ? source.GetViewIndex(current.Node) + 1 : 0;
        var columns = VisibleColumns.ToArray();
        for (var offset = 0; offset < snapshot.Count; ++offset)
        {
            var row = snapshot[(Math.Max(0, start) + offset) % snapshot.Count];
            foreach (var column in columns)
            {
                var effective = column.EffectiveColumn;
                if (effective is not TreeDataGridTextColumn && effective.TextSearchValueSelector is null) continue;
                var value = effective.TextSearchValueSelector?.Invoke(row.Model) ?? column.ReadValue(row.Model)?.ToString();
                if (!ReferenceEquals(source, Source) || !Columns.Contains(column) || !column.IsVisible) return null;
                if (value?.StartsWith(text, StringComparison.CurrentCultureIgnoreCase) != true || source.GetViewIndex(row.Id) < 0) continue;
                var found = new CellAddress(row.Id, column.Id);
                SelectAddress(found, extend: false, toggle: false);
                RowsPresenter?.ScrollIntoView(row.Id); BringColumnIntoView(column.Id);
                return found;
            }
        }
        return null;
    }
    private void InteractionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed != true || e.Source is not Visual visual) return;
        if (visual.GetSelfAndVisualAncestors().OfType<Button>().Any(button => button.Classes.Contains("tree-data-grid-expander"))) return;
        if (IsDisplayTemplateInput(visual)) return;
        var cell = visual.GetSelfAndVisualAncestors().OfType<TreeDataGridCellPresenter>().FirstOrDefault();
        if (cell?.Row is null || cell.Column is null) return;
        var address = new CellAddress(cell.Row.Id, cell.Column.Id);
        var state = State;
        if (state?.EditSession?.Cell == address) return;
        if (state?.EditSession is not null && !CommitEdit()) { e.Handled = true; return; }
        if (Source?.GetViewIndex(address.Node) < 0 || Source is null || GetColumnIndex(address.Column) < 0) { e.Handled = true; return; }
        Focus();
        SelectAddress(address, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Control));
        if (EditTriggers.HasFlag(TreeDataGridEditTriggers.SingleClick) || e.ClickCount >= 2 && EditTriggers.HasFlag(TreeDataGridEditTriggers.DoubleClick)) BeginEdit(address);
    }
    private void InteractionKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsColumnReordering) { if (e.Key == Key.Escape) CancelColumnReorder(); e.Handled = true; return; }
        if (_rowPointerDrag is { Started: true }) { if (e.Key == Key.Escape) CancelRowDrag(); e.Handled = true; return; }
        if (e.Source is Visual origin && origin.GetSelfAndVisualAncestors().OfType<TreeDataGridColumnHeader>().Any()) return;
        if (e.Source is Visual input && IsDisplayTemplateInput(input)) return;
        var state = State;
        if (state is null || Source is null || VisibleColumns.Count == 0 || Source.Rows.Count == 0 || IsInputMethodComposing) return;
        if (e.Key == Key.F2 && EditTriggers.HasFlag(TreeDataGridEditTriggers.F2))
        { if (state.CurrentCell is { } cell) BeginEdit(cell); e.Handled = true; return; }
        if (state.EditSession is not null)
        {
            if (e.Key == Key.Escape) { CancelEdit(); Focus(); e.Handled = true; return; }
            if (e.Key is not Key.Enter and not Key.Tab) return;
            if (!CommitEdit()) { e.Handled = true; return; }
            Focus();
            state = State;
            if (state is null || Source is null || VisibleColumns.Count == 0 || Source.Rows.Count == 0) { e.Handled = true; return; }
        }
        var columns = VisibleColumns;
        var navigationSource = Source;
        var navigationVersion = navigationSource.Version;
        var current = state.CurrentCell ?? new CellAddress(Source.Rows[0].Id, columns[0].Id);
        var row = Math.Max(0, Source.GetViewIndex(current.Node));
        var column = Math.Max(0, GetColumnIndex(current.Column));
        var extend = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Left or Key.Right &&
            columns[column] is TreeDataGridHierarchicalExpanderColumn)
        { NavigateTree(current, e.Key == Key.Right); e.Handled = true; return; }
        if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control) && SelectionMode == TreeDataGridSelectionMode.Multiple)
        {
            if (SelectionUnit == TreeDataGridSelectionUnit.Row) state.SelectRowRange(Source.Rows[0].Id, Source.Rows[^1].Id);
            else state.SelectCellRange(new(Source.Rows[0].Id, columns[0].Id), new(Source.Rows[^1].Id, columns[^1].Id), columns.Select(item => item.Id).ToArray());
            e.Handled = true; return;
        }
        switch (e.Key)
        {
            case Key.Up: row--; break;
            case Key.Down: case Key.Enter: row++; break;
            case Key.Left: column--; break;
            case Key.Right: column++; break;
            case Key.Home: if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) row = 0; column = 0; break;
            case Key.End: if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) row = Source.Rows.Count - 1; column = columns.Count - 1; break;
            case Key.PageUp: row = GetPageRow(row, forward: false); break;
            case Key.PageDown: row = GetPageRow(row, forward: true); break;
            case Key.Tab:
                column += extend ? -1 : 1;
                if (column < 0) { column = columns.Count - 1; row--; }
                if (column >= columns.Count) { column = 0; row++; }
                extend = false; break;
            case Key.Space:
                if (columns[column].EffectiveColumn is TreeDataGridCheckBoxColumn && BeginEdit(current))
                {
                    var session = state.EditSession!;
                    session.Value = session.Value is true ? (columns[column].EffectiveColumn is TreeDataGridCheckBoxColumn { IsThreeState: true } ? null : false) : session.Value is null ? false : true;
                    CommitEdit();
                }
                e.Handled = true; return;
            default: return;
        }
        if (!ReferenceEquals(Source, navigationSource) || navigationSource.Version != navigationVersion || !columns.SequenceEqual(VisibleColumns))
        { e.Handled = true; return; }
        row = Math.Clamp(row, 0, navigationSource.Rows.Count - 1); column = Math.Clamp(column, 0, columns.Count - 1);
        var target = new CellAddress(navigationSource.Rows[row].Id, columns[column].Id);
        SelectAddress(target, extend, toggle: false);
        if (ReferenceEquals(Source, navigationSource)) { RowsPresenter?.ScrollIntoView(target.Node); BringColumnIntoView(target.Column); }
        e.Handled = true;
    }
    private void InteractionTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Source is Visual input && IsDisplayTemplateInput(input)) return;
        if (State?.EditSession is not null || string.IsNullOrEmpty(e.Text)) return;
        var now = DateTime.UtcNow;
        _searchPrefix = now - _lastSearch > TimeSpan.FromSeconds(1) ? e.Text : _searchPrefix + e.Text;
        _lastSearch = now;
        if (SearchText(_searchPrefix) is not null) e.Handled = true;
    }
    private bool IsDisplayTemplateInput(Visual origin)
    {
        // A display template can contain native controls with their own input contract. Do not
        // change selection here: its callbacks can replace the template between press and click.
        // Editors still use the grid's staged Enter/Escape/Tab and composition handling.
        var focusable = false;
        foreach (var ancestor in origin.GetSelfAndVisualAncestors())
        {
            if (ancestor is TreeDataGridCellPresenter cell)
                return focusable && cell.Row is { } row && cell.Column is { } column &&
                    column.EffectiveColumn is TreeDataGridTemplateColumn &&
                    _state?.EditSession?.Cell != new CellAddress(row.Id, column.Id);
            if (ancestor is InputElement { Focusable: true, IsEffectivelyEnabled: true }) focusable = true;
        }
        return false;
    }
    private void SelectAddress(CellAddress address, bool extend, bool toggle)
    {
        if (State is not { } state) return;
        using (state.DeferSelectionNotifications())
        {
            if (SelectionUnit == TreeDataGridSelectionUnit.Cell) state.SelectCell(address, extend);
            else
            {
                var anchor = state.SelectionAnchor;
                state.SetCurrentCell(address);
                if (extend && SelectionMode == TreeDataGridSelectionMode.Multiple && anchor is { } origin && Source?.GetViewIndex(origin.Node) >= 0) state.SelectRowRange(origin.Node, address.Node);
                else if (toggle) state.ToggleSelection(address.Node);
                else state.Select(address.Node);
            }
        }
    }
    internal void BringColumnIntoView(ColumnId id)
    {
        if (RowsPresenter is not { Layout: { } layout } presenter) return;
        var index = GetColumnIndex(id);
        if (index < layout.FrozenColumnCount || index < 0 || index >= layout.ColumnCount) return;
        var left = layout.GetColumnLeft(index, presenter.Offset.X); var right = left + layout.GetColumnWidth(index);
        if (left < layout.FrozenWidth) presenter.Offset = new Vector(presenter.Offset.X + left - layout.FrozenWidth, presenter.Offset.Y);
        else if (right > presenter.Viewport.Width) presenter.Offset = new Vector(presenter.Offset.X + right - presenter.Viewport.Width, presenter.Offset.Y);
    }
}
