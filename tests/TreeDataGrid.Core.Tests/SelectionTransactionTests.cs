using System.Collections.ObjectModel;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Core.Tests;

public sealed class SelectionTransactionTests
{
    [Fact]
    public void Cancelled_rectangle_preserves_current_anchor_and_edit_without_calling_validator_or_writer()
    {
        using var source = new FlatTreeDataGridSource<string>(["a", "b", "c"]);
        using var state = new TreeDataGridState(source);
        ColumnId[] columns = [new(1), new(2), new(3)]; state.SetColumns(columns);
        var first = new CellAddress(source.Rows[0].Id, columns[0]);
        state.SelectCell(first);
        var validations = 0; var writes = 0;
        state.BeginEdit(first, row => row, (_, _) => writes++, _ => { validations++; return null; });
        var edit = state.EditSession!; edit.Value = "draft";
        TreeDataGridSelectionSnapshot? captured = null;
        state.SelectionChanging += (_, e) =>
        {
            captured = e.ProposedSelection;
            Assert.Equal(first, state.CurrentCell); Assert.Equal(first, state.SelectionAnchor);
            Assert.Equal(3, e.ProposedSelection.CellRows.Count); Assert.Equal(3, e.ProposedSelection.CellColumns.Count);
            e.Cancel = true;
        };
        state.SelectCellRange(first, new(source.Rows[2].Id, columns[2]), columns);
        Assert.Equal(first, Assert.Single(state.SelectedCells)); Assert.Equal(first, state.CurrentCell);
        Assert.Equal(first, state.SelectionAnchor); Assert.Same(edit, state.EditSession);
        Assert.Equal("draft", edit.Value); Assert.Equal(0, writes); Assert.Equal(0, validations);
        Assert.Equal(3, captured!.CellRows.Count); // Snapshot remains independent of later mutations.
    }

    [Fact]
    public void Nested_batch_raises_one_before_and_after_and_cancel_restores_original_selection()
    {
        using var source = new FlatTreeDataGridSource<string>(["a", "b", "c"]);
        using var state = new TreeDataGridState(source);
        state.Select(source.Rows[0].Id);
        var before = 0; var after = 0; var cancel = true;
        state.SelectionChanging += (_, e) => { before++; Assert.Equal(3, e.ProposedSelection.SelectedNodes.Count); e.Cancel = cancel; };
        state.SelectionChanged += (_, e) => { after++; Assert.IsType<TreeDataGridSelectionChangedEventArgs>(e); };
        void Batch()
        {
            using (state.DeferSelectionNotifications())
            {
                state.ClearSelection();
                using (state.DeferSelectionNotifications()) foreach (var row in source.Rows) state.Select(row.Id, true);
            }
        }
        Batch(); Assert.Equal(source.Rows[0].Id, Assert.Single(state.SelectedNodes)); Assert.Equal(1, before); Assert.Equal(0, after);
        cancel = false; Batch(); Assert.Equal(3, state.SelectedNodes.Count); Assert.Equal(2, before); Assert.Equal(1, after);
    }

    [Fact]
    public void Reentrant_selection_and_exception_before_commit_leave_state_usable()
    {
        using var source = new FlatTreeDataGridSource<string>(["a", "b"]);
        using var state = new TreeDataGridState(source);
        state.Select(source.Rows[0].Id);
        EventHandler<TreeDataGridSelectionChangingEventArgs> handler = (_, _) => state.ClearSelection();
        state.SelectionChanging += handler;
        Assert.Throws<InvalidOperationException>(() => state.Select(source.Rows[1].Id));
        Assert.Equal(source.Rows[0].Id, Assert.Single(state.SelectedNodes));
        state.SelectionChanging -= handler;
        state.Select(source.Rows[1].Id); Assert.Equal(source.Rows[1].Id, Assert.Single(state.SelectedNodes));
    }

    [Fact]
    public void Before_event_precedes_validation_and_successful_write_then_completion()
    {
        using var source = new FlatTreeDataGridSource<string>(["a", "b"]);
        using var state = new TreeDataGridState(source);
        state.Select(source.Rows[0].Id);
        var order = new List<string>();
        state.BeginEdit(new(source.Rows[0].Id, new(1)), row => row, (_, _) => order.Add("write"), _ => { order.Add("validate"); return null; });
        state.SelectionChanging += (_, _) => order.Add("before");
        state.SelectionChanged += (_, _) => order.Add("after");
        state.Select(source.Rows[1].Id);
        Assert.Equal(new[] { "before", "validate", "write", "after" }, order);
        Assert.Null(state.EditSession); Assert.Equal(source.Rows[1].Id, Assert.Single(state.SelectedNodes));
    }

    [Fact]
    public void Failed_validation_preserves_selection_anchor_and_current()
    {
        using var source = new FlatTreeDataGridSource<string>(["a", "b"]);
        using var state = new TreeDataGridState(source);
        var first = new CellAddress(source.Rows[0].Id, new(1)); state.SelectCell(first);
        state.BeginEdit(first, row => row, (_, _) => throw new Exception("must not write"), _ => "invalid");
        state.SelectCell(new(source.Rows[1].Id, new(1)));
        Assert.Equal(first, Assert.Single(state.SelectedCells)); Assert.Equal(first, state.CurrentCell);
        Assert.Equal(first, state.SelectionAnchor); Assert.Equal("invalid", state.EditSession!.ValidationError);
    }

    [Fact]
    public void Collection_change_in_before_event_invalidates_proposal_and_forced_cleanup_cannot_be_cancelled()
    {
        var items = new ObservableCollection<string>(["a", "b"]);
        using var source = new FlatTreeDataGridSource<string>(items); using var state = new TreeDataGridState(source);
        var old = source.Rows[0].Id; state.Select(old);
        var completions = new List<TreeDataGridSelectionChangedEventArgs>();
        state.SelectionChanged += (_, e) => completions.Add(Assert.IsType<TreeDataGridSelectionChangedEventArgs>(e));
        state.SelectionChanging += (_, e) => { items.RemoveAt(0); e.Cancel = true; };
        state.Select(source.Rows[1].Id);
        Assert.Empty(state.SelectedNodes);
        var completed = Assert.Single(completions); Assert.True(completed.IsForced);
        Assert.Equal(TreeDataGridSelectionChangeReason.ProjectionChanged, completed.Reason);
        Assert.Equal(old, Assert.Single(completed.OldSelection.SelectedNodes));
    }

    [Fact]
    public void Successful_writer_that_deletes_proposed_target_does_not_apply_stale_selection()
    {
        var items = new ObservableCollection<string>(["a", "b"]);
        using var source = new FlatTreeDataGridSource<string>(items); using var state = new TreeDataGridState(source);
        var old = source.Rows[0].Id; var target = source.Rows[1].Id; state.Select(old);
        var writes = 0;
        state.BeginEdit(new(old, new(1)), row => row, (_, _) => { writes++; items.RemoveAt(1); });
        state.Select(target);
        Assert.Equal(1, writes); Assert.Null(state.EditSession); Assert.Equal(old, Assert.Single(state.SelectedNodes));
    }

    [Fact]
    public void Forced_column_cleanup_reports_reason_without_before_event()
    {
        using var source = new FlatTreeDataGridSource<string>(["a"]); using var state = new TreeDataGridState(source);
        ColumnId[] columns = [new(1), new(2)]; state.SetColumns(columns);
        state.SelectCell(new(source.Rows[0].Id, columns[1]));
        var before = 0; TreeDataGridSelectionChangedEventArgs? completed = null;
        state.SelectionChanging += (_, e) => { before++; e.Cancel = true; };
        state.SelectionChanged += (_, e) => completed = Assert.IsType<TreeDataGridSelectionChangedEventArgs>(e);
        state.SetColumns([columns[0]]);
        Assert.Equal(0, before); Assert.Empty(state.SelectedCells); Assert.Null(state.CurrentCell);
        Assert.True(completed!.IsForced); Assert.Equal(TreeDataGridSelectionChangeReason.ColumnsChanged, completed.Reason);
    }

    [Fact]
    public void Completion_observer_failures_do_not_skip_other_observers_or_undo_the_commit()
    {
        using var source = new FlatTreeDataGridSource<string>(["a", "b"]); using var state = new TreeDataGridState(source);
        var observed = 0;
        state.Changed += (_, _) => throw new InvalidOperationException("changed");
        state.SelectionChanged += (_, _) => throw new ArgumentException("selection");
        state.SelectionChanged += (_, _) => observed++;
        Assert.Throws<AggregateException>(() => state.Select(source.Rows[1].Id));
        Assert.Equal(1, observed); Assert.Equal(source.Rows[1].Id, Assert.Single(state.SelectedNodes));
    }

    [Fact]
    public void External_removal_inside_batch_cannot_restore_a_deleted_identity()
    {
        var items = new ObservableCollection<string>(["a", "b", "c"]);
        using var source = new FlatTreeDataGridSource<string>(items); using var state = new TreeDataGridState(source);
        state.Select(source.Rows[0].Id);
        using (state.DeferSelectionNotifications()) { state.Select(source.Rows[1].Id); items.RemoveAt(0); }
        Assert.Empty(state.SelectedNodes);
        state.Select(source.Rows[0].Id); Assert.Single(state.SelectedNodes);
    }

    [Fact]
    public void Successful_writer_that_splits_proposed_rectangle_does_not_publish_a_nonrectangular_selection()
    {
        using var source = new FlatTreeDataGridSource<string>(["a", "b", "c"]); using var state = new TreeDataGridState(source);
        ColumnId column = new(1); state.SetColumns([column]); var a = source.Rows[0].Id; var b = source.Rows[1].Id;
        state.SelectCell(new(a, column));
        state.BeginEdit(new(a, column), row => row, (_, _) => source.Sort(column,
            (left, right) => Rank((string)left).CompareTo(Rank((string)right)), TreeDataGridSortDirection.Ascending));
        state.SelectCellRange(new(a, column), new(b, column), [column]);
        Assert.Null(state.EditSession); Assert.Equal(new CellAddress(a, column), Assert.Single(state.SelectedCells));
        static int Rank(string value) => value == "a" ? 0 : value == "c" ? 1 : 2;
    }
}
