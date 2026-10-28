using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class SelectionTransactionTests
{
    [AvaloniaFact]
    public void Pointer_keyboard_search_and_location_cancel_before_validation_write_and_scroll()
    {
        var models = Enumerable.Range(0, 150).Select(i => new SearchOptionRow(i == 149 ? "target" : "row " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<SearchOptionRow>(models);
        var writes = 0; var validations = 0;
        var column = Text(); column.ValueSetter = (_, _) => writes++; column.ValueValidator = _ => { validations++; return null; };
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var first = new CellAddress(source.Rows[0].Id, column.Id);
            grid.State!.SelectCell(first); Assert.True(grid.BeginEdit(first)); var edit = grid.EditSession!; edit.Value = "draft";
            var reasons = new List<TreeDataGridSelectionChangeReason>();
            grid.SelectionChanging += (_, e) => { reasons.Add(e.Reason); e.Cancel = true; };
            A1TestHost.Click(window, A1TestHost.Cell(grid, 1));
            grid.Focus(); A1TestHost.Key(window, Key.Enter);
            Assert.Null(grid.SearchText("target")); Assert.False(grid.TryScrollIntoView(source.Rows[149].Id, select: true));
            Assert.Equal(new[] { TreeDataGridSelectionChangeReason.Pointer, TreeDataGridSelectionChangeReason.Keyboard,
                TreeDataGridSelectionChangeReason.Search, TreeDataGridSelectionChangeReason.Programmatic }, reasons);
            Assert.Equal(0, writes); Assert.Equal(0, validations); Assert.Same(edit, grid.EditSession);
            Assert.Equal("draft", edit.Value); Assert.Equal(first, grid.CurrentCell); Assert.Equal(first, grid.State.SelectionAnchor);
            Assert.Equal(first, Assert.Single(grid.State.SelectedCells)); Assert.Equal(default(Vector), grid.RowsPresenter!.Offset);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Cancelled_two_way_selected_item_index_and_current_restore_state_without_removing_bindings()
    {
        var a = new SearchOptionRow("a"); var b = new SearchOptionRow("b");
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([a, b]);
        var grid = A1TestHost.Create(source, Text()); var vm = new SelectionVm { Item = a };
        grid.DataContext = vm;
        grid.Bind(GridControl.SelectedItemProperty, new ReflectionBinding(nameof(SelectionVm.Item)) { Mode = BindingMode.TwoWay });
        var window = A1TestHost.Show(grid);
        try
        {
            var cancel = true;
            grid.SelectionChanging += (_, e) => { Assert.Equal(TreeDataGridSelectionChangeReason.Binding, e.Reason); e.Cancel = cancel; };
            vm.Item = b; Assert.Same(a, grid.SelectedItem); Assert.Same(a, vm.Item);
            grid.SelectedIndex = new(1); Assert.Equal(new IndexPath(0), grid.SelectedIndex); Assert.Same(a, grid.SelectedItem);
            grid.CurrentCell = new(source.Rows[1].Id, grid.Columns[0].Id); Assert.Null(grid.CurrentCell);
            cancel = false; vm.Item = b; Assert.Same(b, grid.SelectedItem);
            vm.Item = a; Assert.Same(a, grid.SelectedItem); // Binding is still active after the cancelled write.
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Cancelled_selected_items_collection_batch_is_restored_and_can_be_used_again()
    {
        var models = new[] { new SearchOptionRow("a"), new SearchOptionRow("b"), new SearchOptionRow("c") };
        using var source = new FlatTreeDataGridSource<SearchOptionRow>(models);
        var grid = A1TestHost.Create(source, Text()); grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        var selected = new ObservableCollection<object> { models[0] };
        grid.SelectedItems = selected; var window = A1TestHost.Show(grid);
        try
        {
            var cancel = true; var before = 0;
            grid.SelectionChanging += (_, e) => { before++; e.Cancel = cancel; };
            selected.Add(models[1]); Dispatcher.UIThread.RunJobs();
            Assert.Same(selected, grid.SelectedItems); Assert.Same(models[0], Assert.Single(selected)); Assert.Single(grid.State!.SelectedNodes);
            Assert.Equal(1, before);
            cancel = false; selected.Add(models[2]); Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, grid.State.SelectedNodes.Count); Assert.Equal(2, selected.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Automation_select_remove_and_focus_share_cancellation_without_scrolling()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>(Enumerable.Range(0, 150).Select(i => new SearchOptionRow("row " + i)));
        var grid = A1TestHost.Create(source, Text()); var window = A1TestHost.Show(grid);
        try
        {
            var first = source.Rows[0].Id; grid.State!.Select(first);
            var peer = (TreeDataGridAutomationPeer)ControlAutomationPeer.CreatePeerForElement(grid)!;
            var target = peer.GetCellPeer(source.Rows[149].Id, grid.Columns[0].Id)!;
            var selected = (ISelectionItemProvider)peer.GetRowPeer(first)!;
            var reasons = new List<TreeDataGridSelectionChangeReason>();
            grid.SelectionChanging += (_, e) => { reasons.Add(e.Reason); e.Cancel = true; };
            ((ISelectionItemProvider)target).Select(); selected.RemoveFromSelection(); target.SetFocus();
            Assert.Equal(3, reasons.Count); Assert.All(reasons, reason => Assert.Equal(TreeDataGridSelectionChangeReason.Automation, reason));
            Assert.Equal(first, Assert.Single(grid.State.SelectedNodes)); Assert.Null(grid.CurrentCell);
            Assert.Equal(default(Vector), grid.RowsPresenter!.Offset);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Source_replacement_in_before_event_cleans_old_state_and_never_writes_old_draft()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("a"), new("b")]);
        using var replacement = new FlatTreeDataGridSource<SearchOptionRow>([new("new")]);
        var column = Text(); var writes = 0; column.ValueSetter = (_, _) => writes++;
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var first = new CellAddress(source.Rows[0].Id, column.Id); grid.State!.SelectCell(first); grid.BeginEdit(first); grid.EditSession!.Value = "draft";
            var completed = new List<TreeDataGridSelectionChangedEventArgs>();
            grid.SelectionChangeCompleted += (_, e) => completed.Add(e);
            grid.SelectionChanging += (_, _) => grid.Source = replacement;
            var old = grid.State; old.SelectCell(new(source.Rows[1].Id, column.Id));
            Assert.Same(replacement, grid.Source); Assert.Equal(0, writes); Assert.Null(grid.EditSession); Assert.Empty(grid.State!.SelectedCells);
            Assert.Contains(completed, e => e.IsForced && e.Reason == TreeDataGridSelectionChangeReason.SourceChanged);
            Assert.Throws<ObjectDisposedException>(() => old.ClearSelection());
            window.UpdateLayout(); Assert.Equal("new", Assert.Single(grid.RowsPresenter!.RealizedRows).Row!.Model is SearchOptionRow row ? row.Name : "");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Hidden_column_is_forced_cleanup_and_completion_observers_see_synchronized_mvvm()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("a")]);
        var first = Text(); var second = Text(); var grid = A1TestHost.Create(source, first, second); var window = A1TestHost.Show(grid);
        try
        {
            grid.State!.SelectCell(new(source.Rows[0].Id, first.Id));
            var before = 0; TreeDataGridSelectionChangedEventArgs? completed = null;
            grid.SelectionChanging += (_, e) => { before++; e.Cancel = true; };
            grid.SelectionChangeCompleted += (_, e) => { completed = e; Assert.Equal(grid.State.CurrentCell, grid.CurrentCell); };
            first.IsVisible = false;
            Assert.Equal(0, before); Assert.Equal(second.Id, grid.CurrentCell!.Value.Column);
            Assert.Empty(grid.State.SelectedCells); Assert.True(completed!.IsForced);
            Assert.Equal(TreeDataGridSelectionChangeReason.ColumnsChanged, completed.Reason);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Search_rechecks_changed_projection_after_a_committed_edit_and_emits_only_one_selection_completion()
    {
        var a = new SearchOptionRow("first"); var b = new SearchOptionRow("target"); var c = new SearchOptionRow("other");
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([a, b, c]);
        var column = Text(); column.ValueSetter = (row, value) => { ((SearchOptionRow)row).Name = (string)value!; b.Name = "gone"; c.Name = "target new"; };
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var first = new CellAddress(source.Rows[0].Id, column.Id); grid.State!.SelectCell(first); grid.BeginEdit(first); grid.EditSession!.Value = "written";
            var completions = 0; grid.SelectionChangeCompleted += (_, e) => { Assert.Equal(TreeDataGridSelectionChangeReason.Search, e.Reason); completions++; };
            Assert.Equal(new CellAddress(source.Rows[2].Id, column.Id), grid.SearchText("target"));
            Assert.Equal(1, completions); Assert.Equal("written", a.Name); Assert.Null(grid.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Pre_event_exception_or_reentry_does_not_commit_and_next_attempt_succeeds()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("a"), new("b")]);
        var column = Text(); var writes = 0; column.ValueSetter = (_, _) => writes++;
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var first = new CellAddress(source.Rows[0].Id, column.Id); grid.State!.SelectCell(first); grid.BeginEdit(first);
            EventHandler<TreeDataGridSelectionChangingEventArgs> handler = (_, _) => grid.State.ClearSelection();
            grid.SelectionChanging += handler;
            Assert.Throws<InvalidOperationException>(() => grid.State.SelectCell(new(source.Rows[1].Id, column.Id)));
            Assert.Equal(0, writes); Assert.Equal(first, grid.CurrentCell); Assert.NotNull(grid.EditSession);
            grid.SelectionChanging -= handler; grid.State.SelectCell(new(source.Rows[1].Id, column.Id));
            Assert.Equal(1, writes); Assert.Null(grid.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Converter_selection_reentry_is_rejected_before_write_and_recovery_is_possible()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("a"), new("b")]);
        var column = Text(); var writes = 0; column.ValueSetter = (_, _) => writes++;
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var first = new CellAddress(source.Rows[0].Id, column.Id); grid.State!.SelectCell(first); grid.BeginEdit(first);
            column.ValueConverter = value => { grid.State.ClearSelection(); return value; };
            grid.State.SelectCell(new(source.Rows[1].Id, column.Id));
            Assert.Equal(0, writes); Assert.Equal(first, grid.CurrentCell); Assert.NotNull(grid.EditSession!.ValidationError);
            column.ValueConverter = null; grid.State.SelectCell(new(source.Rows[1].Id, column.Id));
            Assert.Equal(1, writes); Assert.Null(grid.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Forced_source_completion_exception_does_not_prevent_source_or_presenter_reconnection()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("old")]);
        using var replacement = new FlatTreeDataGridSource<SearchOptionRow>([new("new")]);
        var grid = A1TestHost.Create(source, Text()); var window = A1TestHost.Show(grid);
        try
        {
            grid.State!.SelectCell(new(source.Rows[0].Id, grid.Columns[0].Id));
            var observed = 0;
            EventHandler<TreeDataGridSelectionChangedEventArgs> failure = (_, e) => { if (e.IsForced) throw new InvalidOperationException("completion"); };
            grid.SelectionChangeCompleted += failure; grid.SelectionChangeCompleted += (_, _) => observed++;
            Assert.Throws<InvalidOperationException>(() => grid.Source = replacement);
            grid.SelectionChangeCompleted -= failure; window.UpdateLayout();
            Assert.Equal(1, observed); Assert.Same(replacement, grid.Source);
            Assert.Equal(replacement.Rows[0].Id, Assert.Single(grid.RowsPresenter!.RealizedRows).Row!.Id);
            replacement.Refresh(); window.UpdateLayout(); Assert.Single(grid.RowsPresenter.RealizedRows);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Writer_hiding_proposed_column_keeps_successful_write_and_rejects_hidden_target()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("a"), new("b")]);
        var first = Text(); var target = Text(); var writes = 0;
        first.ValueSetter = (_, _) => { writes++; target.IsVisible = false; };
        var grid = A1TestHost.Create(source, first, target); var window = A1TestHost.Show(grid);
        try
        {
            var original = new CellAddress(source.Rows[0].Id, first.Id); grid.State!.SelectCell(original); grid.BeginEdit(original);
            grid.State.SelectCell(new(source.Rows[1].Id, target.Id));
            Assert.Equal(1, writes); Assert.Null(grid.EditSession); Assert.Equal(original, grid.CurrentCell);
            Assert.Equal(original, Assert.Single(grid.State.SelectedCells)); Assert.False(target.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Mode_change_in_before_event_invalidates_proposal_even_when_previous_selection_is_empty()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("a"), new("b")]);
        var grid = A1TestHost.Create(source, Text()); grid.SelectionMode = TreeDataGridSelectionMode.Multiple;
        var window = A1TestHost.Show(grid);
        try
        {
            EventHandler<TreeDataGridSelectionChangingEventArgs> handler = (_, _) => grid.SelectionMode = TreeDataGridSelectionMode.Single;
            grid.SelectionChanging += handler; grid.State!.SelectRowRange(source.Rows[0].Id, source.Rows[1].Id);
            Assert.Empty(grid.State.SelectedNodes); Assert.Equal(TreeDataGridSelectionMode.Single, grid.State.SelectionMode);
            grid.SelectionChanging -= handler; grid.State.Select(source.Rows[1].Id); Assert.Single(grid.State.SelectedNodes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Current_only_edit_navigation_preserves_an_existing_hidden_selection_after_filtering()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("hide"), new("visible")]);
        var column = Text(); column.ValueSetter = (row, value) => ((SearchOptionRow)row).Name = (string)value!;
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var hidden = new CellAddress(source.Rows[0].Id, column.Id); var visible = new CellAddress(source.Rows[1].Id, column.Id);
            grid.State!.SelectCell(hidden); source.SetFilter(row => ((SearchOptionRow)row).Name == "visible"); window.UpdateLayout();
            Assert.Equal(hidden, Assert.Single(grid.State.SelectedCells));
            Assert.True(grid.BeginEdit(visible)); Assert.Equal(visible, grid.CurrentCell);
            Assert.Equal(hidden, Assert.Single(grid.State.SelectedCells));
            grid.CancelEdit();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Binding_entry_completion_observers_see_all_selection_mirrors_already_synchronized()
    {
        var models = new[] { new SearchOptionRow("a"), new SearchOptionRow("b") };
        using var source = new FlatTreeDataGridSource<SearchOptionRow>(models);
        var grid = A1TestHost.Create(source, Text()); grid.SelectedItem = models[0]; var window = A1TestHost.Show(grid);
        try
        {
            grid.Columns[0].ValueSetter = (row, value) => ((SearchOptionRow)row).Name = (string)value!;
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, grid.Columns[0].Id)));
            var observed = 0;
            grid.SelectionChangeCompleted += (_, e) =>
            {
                observed++; Assert.Equal(TreeDataGridSelectionChangeReason.Binding, e.Reason);
                Assert.Same(models[1], grid.SelectedItem); Assert.Equal(new IndexPath(1), grid.SelectedIndex);
                Assert.Same(models[1], Assert.Single(grid.SelectedItems!.Cast<object>()));
                Assert.Equal(grid.State!.CurrentCell, grid.CurrentCell);
                Assert.Null(grid.EditSession); Assert.False(grid.IsEditing);
            };
            grid.SelectedItem = models[1]; Assert.Equal(1, observed);
        }
        finally { window.Close(); }
    }

    private static TreeDataGridTextColumn Text() => new() { ValueGetter = row => ((SearchOptionRow)row).Name, Width = new(160) };
    private sealed class SelectionVm : INotifyPropertyChanged
    {
        private object? _item;
        public object? Item { get => _item; set { if (ReferenceEquals(_item, value)) return; _item = value; PropertyChanged?.Invoke(this, new(nameof(Item))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
