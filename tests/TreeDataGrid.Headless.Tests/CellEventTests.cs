using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class CellEventTests
{
    [AvaloniaFact]
    public void Both_axes_recycling_sort_filter_column_changes_and_detach_pair_every_lifetime_without_value_events()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>(Enumerable.Range(0, 120).Select(i => new SearchOptionRow("row " + i)));
        var columns = Enumerable.Range(0, 15).Select(_ => Text()).ToArray();
        var grid = A1TestHost.Create(source, columns); grid.FrozenColumnCount = 1;
        var active = new Dictionary<TreeDataGridCellPresenter, TreeDataGridCellEventArgs>(); var prepared = 0; var clearing = 0; var values = 0;
        grid.CellPrepared += (_, e) =>
        {
            Assert.True(active.TryAdd(e.Container, e)); prepared++;
            Assert.Same(e.Model, e.Container.DataContext); Assert.Equal(e.NodeId, e.Container.Row!.Id);
            Assert.Equal(e.ColumnId, e.Container.Column!.Id); Assert.Equal(e.SourceIndex, e.Container.Row.ModelIndex);
            Assert.NotNull(e.Container.Content); Assert.True(e.RowIndex >= 0); Assert.True(e.ColumnIndex >= 0);
        };
        grid.CellClearing += (_, e) =>
        {
            Assert.True(active.Remove(e.Container, out var old)); Assert.Same(old, e); clearing++;
            Assert.Same(e.Model, e.Container.DataContext); Assert.Equal(e.NodeId, e.Container.Row!.Id); Assert.NotNull(e.Container.Content);
            Assert.IsType<TreeDataGridRowPresenter>(e.Container.Parent);
        };
        grid.CellValueChanged += (_, _) => values++;
        var window = A1TestHost.Show(grid, 450, 250);
        try
        {
            grid.RowsPresenter!.Offset = new Vector(600, 1800); window.UpdateLayout();
            source.Sort(columns[0].Id, (a, b) => string.CompareOrdinal(((SearchOptionRow)a).Name, ((SearchOptionRow)b).Name), TreeDataGridSortDirection.Descending); window.UpdateLayout();
            source.SetFilter(row => ((SearchOptionRow)row).Name.EndsWith('0')); window.UpdateLayout();
            columns[2].IsVisible = false; grid.Columns.Move(3, 1); window.UpdateLayout();
            grid.Template = A1TestHost.Template(); window.UpdateLayout();
            Assert.Equal(grid.RowsPresenter.RealizedCellCount, active.Count); Assert.True(prepared > active.Count);
            window.Content = null;
            Assert.Empty(active); Assert.Equal(prepared, clearing); Assert.Equal(0, values);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Prepared_callback_can_replace_source_and_hide_columns_without_reusing_old_identity()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("old")]);
        using var replacement = new FlatTreeDataGridSource<SearchOptionRow>([new("new")]);
        var column = Text(); var grid = A1TestHost.Create(source, column); var active = new HashSet<TreeDataGridCellPresenter>();
        var replaced = false; var prepared = 0; var clearing = 0;
        grid.CellPrepared += (_, e) =>
        {
            Assert.True(active.Add(e.Container)); prepared++;
            if (!replaced) { replaced = true; grid.Source = replacement; }
        };
        grid.CellClearing += (_, e) => { Assert.True(active.Remove(e.Container)); clearing++; Assert.Same(e.Model, e.Container.DataContext); };
        var window = A1TestHost.Show(grid);
        try
        {
            Assert.True(replaced); Assert.Same(replacement, grid.Source);
            Assert.All(grid.RowsPresenter!.RealizedRows, row => Assert.Equal("new", ((SearchOptionRow)row.Row!.Model).Name));
            column.IsVisible = false; window.UpdateLayout(); Assert.Empty(active); Assert.Equal(prepared, clearing);
            column.IsVisible = true; window.UpdateLayout(); Assert.Single(active);
        }
        finally { window.Close(); }
        Assert.Empty(active); Assert.Equal(prepared, clearing);
    }

    [AvaloniaFact]
    public void Clearing_failures_run_all_observers_and_release_every_binding_before_rethrow()
    {
        var model = new SearchOptionRow("old"); using var source = new FlatTreeDataGridSource<SearchOptionRow>([model]);
        var grid = A1TestHost.Create(source, new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(SearchOptionRow.Name)) },
            new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(SearchOptionRow.Name)) });
        var window = A1TestHost.Show(grid);
        try
        {
            var oldCells = grid.RowsPresenter!.RealizedRows.SelectMany(row => row.RealizedCells).ToArray();
            var subscribers = model.Subscribers;
            var observed = 0;
            EventHandler<TreeDataGridCellEventArgs> failure = (_, _) => throw new InvalidOperationException("clear observer");
            grid.CellClearing += failure; grid.CellClearing += (_, _) => observed++;
            Assert.ThrowsAny<Exception>(() => source.Refresh());
            Assert.Equal(2, observed); Assert.Equal(1, model.Subscribers);
            Assert.All(oldCells, cell => { Assert.Null(cell.Row); Assert.Null(cell.DataContext); Assert.Null(cell.Value); Assert.Null(cell.Content); });
            grid.CellClearing -= failure; window.UpdateLayout();
            Assert.Equal(2, grid.RowsPresenter.RealizedCellCount); Assert.Equal(subscribers, model.Subscribers);
        }
        finally { window.Close(); }
        Assert.Equal(1, model.Subscribers);
    }

    [AvaloniaFact]
    public void Prepared_exception_is_followed_by_one_clearing_and_the_next_layout_can_recover()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("row")]);
        var column = Text(); var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var prepared = 0; var cleared = 0; TreeDataGridCellPresenter? failed = null;
            EventHandler<TreeDataGridCellEventArgs> failure = (_, e) => { prepared++; failed = e.Container; throw new InvalidOperationException("prepare"); };
            grid.CellPrepared += failure; grid.CellClearing += (_, _) => cleared++;
            column.TextAlignment = TextAlignment.Center;
            Assert.Throws<InvalidOperationException>(() => grid.RowsPresenter!.Measure(new Size(499, 231)));
            Assert.Equal(1, prepared); Assert.Equal(2, cleared); Assert.Null(failed!.DataContext);
            grid.CellPrepared -= failure; source.Refresh(); window.UpdateLayout(); Assert.Single(grid.RowsPresenter!.RealizedRows[0].RealizedCells);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Delegate_model_notifications_and_explicit_refresh_report_old_and_new_once()
    {
        var model = new SearchOptionRow("old"); using var source = new FlatTreeDataGridSource<SearchOptionRow>([model]);
        var column = Text(); var grid = A1TestHost.Create(source, column); var changes = new List<TreeDataGridCellValueChangedEventArgs>();
        grid.CellValueChanged += (_, e) => changes.Add(e); var window = A1TestHost.Show(grid);
        try
        {
            Assert.Empty(changes); model.Name = "new";
            var change = Assert.Single(changes); Assert.Equal("old", change.OldValue); Assert.Equal("new", change.NewValue);
            Assert.True(change.HasOldValue); Assert.True(change.HasNewValue); Assert.Equal(TreeDataGridCellValueChangeReason.ModelNotification, change.Reason);
            Assert.Equal(new CellAddress(source.Rows[0].Id, column.Id), change.Cell); Assert.Equal(new IndexPath(0), change.SourceIndex);
            window.UpdateLayout(); source.Refresh(); window.UpdateLayout(); Assert.Single(changes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Nonnotifying_external_write_requires_refresh_and_null_is_a_known_old_value()
    {
        var model = new PlainRow(); using var source = new FlatTreeDataGridSource<PlainRow>([model]);
        var column = new TreeDataGridTextColumn { ValueGetter = row => ((PlainRow)row).Name };
        var grid = A1TestHost.Create(source, column); var changes = new List<TreeDataGridCellValueChangedEventArgs>();
        grid.CellValueChanged += (_, e) => changes.Add(e); var window = A1TestHost.Show(grid);
        try
        {
            model.Name = "new"; Assert.Empty(changes); source.Refresh();
            var change = Assert.Single(changes); Assert.True(change.HasOldValue); Assert.Null(change.OldValue);
            Assert.Equal("new", change.NewValue); Assert.Equal(TreeDataGridCellValueChangeReason.Refresh, change.Reason);
            window.UpdateLayout(); source.Refresh(); Assert.Single(changes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Reflection_nested_binding_changes_and_replacement_are_observed_and_old_subscription_is_released()
    {
        var model = new SearchOptionRow("row") { Detail = new("old") }; var old = model.Detail;
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([model]);
        var grid = A1TestHost.Create(source, new TreeDataGridTextColumn { Binding = new ReflectionBinding("Detail.Name") });
        var changes = new List<TreeDataGridCellValueChangedEventArgs>(); grid.CellValueChanged += (_, e) => changes.Add(e);
        var window = A1TestHost.Show(grid);
        try
        {
            old.Name = "nested"; Assert.Equal("nested", Assert.Single(changes).NewValue);
            model.Detail = new("replacement"); window.UpdateLayout(); Assert.Equal(2, changes.Count);
            Assert.Equal("nested", changes[1].OldValue); Assert.Equal("replacement", changes[1].NewValue);
            Assert.Equal(0, old.Subscribers); old.Name = "detached"; Assert.Equal(2, changes.Count);
        }
        finally { window.Close(); }
        Assert.Equal(0, model.Detail.Subscribers); Assert.Equal(1, model.Subscribers);
    }

    [AvaloniaFact]
    public void Compiled_binding_nested_updates_and_successful_typed_write_are_observed_without_duplicates()
    {
        var model = new CompiledBindingItem(); using var source = new FlatTreeDataGridSource<CompiledBindingItem>([model]);
        var view = new CompiledBindingConsumerView(); var grid = view.FindControl<NT.AvaloniaControl.TreeDataGrid.TreeDataGrid>("CompiledGrid")!; grid.Source = source;
        var changes = new List<TreeDataGridCellValueChangedEventArgs>(); grid.CellValueChanged += (_, e) => changes.Add(e);
        var window = A1TestHost.Show(view);
        try
        {
            model.Details.Name = "nested new"; Assert.Equal("nested new", Assert.Single(changes).NewValue);
            grid.Columns[0].SetValueAccessors<CompiledBindingItem, string>(row => row.Name, (row, value) => row.Name = value); window.UpdateLayout(); changes.Clear();
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, grid.Columns[0].Id))); grid.EditSession!.Value = "committed";
            Assert.True(grid.CommitEdit()); window.UpdateLayout();
            var change = Assert.Single(changes); Assert.Equal(TreeDataGridCellValueChangeReason.EditCommit, change.Reason);
            Assert.Equal("name", change.OldValue); Assert.Equal("committed", change.NewValue);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Successful_edit_notifies_once_even_if_notifying_writer_or_target_is_offscreen(bool offscreen)
    {
        var models = Enumerable.Range(0, 100).Select(i => new SearchOptionRow("row " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<SearchOptionRow>(models);
        var column = Text(); column.ValueSetter = (row, value) => ((SearchOptionRow)row).Name = (string)value!;
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var cell = new CellAddress(source.Rows[0].Id, column.Id); Assert.True(grid.BeginEdit(cell)); grid.EditSession!.Value = "draft";
            if (offscreen) { grid.RowsPresenter!.Offset = new Vector(0, 1600); window.UpdateLayout(); }
            var changes = new List<TreeDataGridCellValueChangedEventArgs>(); grid.CellValueChanged += (_, e) => changes.Add(e);
            Assert.True(grid.CommitEdit()); window.UpdateLayout();
            var change = Assert.Single(changes); Assert.Equal(cell, change.Cell); Assert.Equal("row 0", change.OldValue); Assert.Equal("draft", change.NewValue);
            Assert.Equal(TreeDataGridCellValueChangeReason.EditCommit, change.Reason); Assert.Null(grid.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Failed_validation_cancelled_edit_and_untracked_offscreen_changes_do_not_report_value_changes()
    {
        var models = Enumerable.Range(0, 100).Select(i => new SearchOptionRow("row " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<SearchOptionRow>(models);
        var column = Text(); column.ValueSetter = (_, _) => throw new Exception("must not write"); column.ValueValidator = _ => "invalid";
        var grid = A1TestHost.Create(source, column); var changes = new List<TreeDataGridCellValueChangedEventArgs>(); grid.CellValueChanged += (_, e) => changes.Add(e);
        var window = A1TestHost.Show(grid);
        try
        {
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, column.Id))); grid.EditSession!.Value = "draft"; Assert.False(grid.CommitEdit()); grid.CancelEdit();
            models[99].Name = "offscreen"; window.UpdateLayout(); source.Refresh(); window.UpdateLayout();
            grid.RowsPresenter!.ScrollIntoView(source.Rows[99].Id); window.UpdateLayout(); Assert.Empty(changes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Value_observer_can_replace_source_and_throw_after_commit_without_reporting_validation_failure()
    {
        var model = new SearchOptionRow("old"); using var source = new FlatTreeDataGridSource<SearchOptionRow>([model]);
        using var replacement = new FlatTreeDataGridSource<SearchOptionRow>([new("new")]);
        var writes = 0; var column = Text(); column.ValueSetter = (row, value) => { writes++; ((SearchOptionRow)row).Name = (string)value!; };
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, column.Id))); grid.EditSession!.Value = "committed";
            var observed = 0;
            grid.CellValueChanged += (_, e) => { Assert.Equal(TreeDataGridCellValueChangeReason.EditCommit, e.Reason); grid.Source = replacement; throw new InvalidOperationException("observer"); };
            grid.CellValueChanged += (_, e) => { observed++; Assert.Same(source, e.Source); Assert.Same(model, e.Model); };
            Assert.Throws<InvalidOperationException>(() => grid.CommitEdit());
            Assert.Equal(1, writes); Assert.Equal(1, observed); Assert.Equal("committed", model.Name);
            Assert.Null(grid.EditSession); Assert.Same(replacement, grid.Source); window.UpdateLayout();
            Assert.True(grid.CommitEdit()); Assert.Equal(1, writes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Clearing_observer_failure_after_writer_does_not_skip_commit_value_notification_or_retry_writer()
    {
        var model = new SearchOptionRow("old"); using var source = new FlatTreeDataGridSource<SearchOptionRow>([model]);
        var column = Text(); var writes = 0; column.ValueSetter = (row, value) => { writes++; ((SearchOptionRow)row).Name = (string)value!; };
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            grid.BeginEdit(new(source.Rows[0].Id, column.Id)); grid.EditSession!.Value = "committed";
            var notified = 0;
            EventHandler<TreeDataGridCellEventArgs> failure = (_, _) => throw new InvalidOperationException("clear");
            grid.CellClearing += failure; grid.CellValueChanged += (_, e) => { notified++; Assert.Equal(TreeDataGridCellValueChangeReason.EditCommit, e.Reason); };
            Assert.ThrowsAny<Exception>(() => grid.CommitEdit());
            Assert.Equal(1, notified); Assert.Equal(1, writes); Assert.Equal("committed", model.Name); Assert.Null(grid.EditSession);
            grid.CellClearing -= failure; window.UpdateLayout(); Assert.True(grid.CommitEdit()); Assert.Equal(1, writes);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("remove")]
    [InlineData("hide")]
    [InlineData("template")]
    public void Model_value_observer_mutations_do_not_send_stale_identity_events_or_leak_bindings(string mutation)
    {
        var model = new SearchOptionRow("old"); var models = new System.Collections.ObjectModel.ObservableCollection<SearchOptionRow> { model };
        using var source = new FlatTreeDataGridSource<SearchOptionRow>(models);
        var column = new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(SearchOptionRow.Name)) };
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var changes = 0;
            grid.CellValueChanged += (_, e) =>
            {
                changes++; Assert.Same(model, e.Model); Assert.Same(source, e.Source); Assert.Equal("old", e.OldValue); Assert.Equal("new", e.NewValue);
                if (mutation == "remove") models.Clear();
                else if (mutation == "hide") column.IsVisible = false;
                else grid.Template = A1TestHost.Template();
            };
            model.Name = "new"; window.UpdateLayout();
            Assert.Equal(1, changes);
        }
        finally { window.Close(); }
        Assert.Equal(mutation == "remove" ? 0 : 1, model.Subscribers);
    }

    [AvaloniaFact]
    public void Successful_equal_value_write_still_reports_one_commit_and_no_synthetic_refresh_change()
    {
        var model = new SearchOptionRow("same"); using var source = new FlatTreeDataGridSource<SearchOptionRow>([model]);
        var column = Text(); column.ValueSetter = (row, value) => ((SearchOptionRow)row).Name = (string)value!;
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            grid.BeginEdit(new(source.Rows[0].Id, column.Id));
            var changes = new List<TreeDataGridCellValueChangedEventArgs>(); grid.CellValueChanged += (_, e) => changes.Add(e);
            Assert.True(grid.CommitEdit()); window.UpdateLayout(); source.Refresh(); window.UpdateLayout();
            var change = Assert.Single(changes); Assert.Equal("same", change.OldValue); Assert.Equal("same", change.NewValue);
            Assert.Equal(TreeDataGridCellValueChangeReason.EditCommit, change.Reason);
        }
        finally { window.Close(); }
    }

    private static TreeDataGridTextColumn Text() => new() { ValueGetter = row => ((SearchOptionRow)row).Name, Width = new(120) };
    private sealed class PlainRow { public string? Name { get; set; } }
}
