using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class BindingWritebackTests
{
    [AvaloniaFact]
    public void Compiled_xaml_adapter_stages_cancels_and_commits_without_an_explicit_setter()
    {
        var row = new A3Row(); var view = new A3BindingView(); var grid = view.FindControl<GridControl>("BindingGrid")!;
        grid.ItemsSource = new[] { row }; var window = A1TestHost.Show(view, 700);
        try
        {
            var column = grid.Columns[1]; var cell = new CellAddress(grid.Source!.Rows[0].Id, column.Id);
            Assert.Null(column.GetEditBindingError(row)); Assert.True(grid.BeginEdit(cell));
            Assert.Equal(12, grid.EditSession!.OriginalValue); grid.EditSession.Value = "42";
            Assert.Equal(12, row.Quantity); Assert.Equal(0, row.QuantityWrites); grid.CancelEdit(); Assert.Equal(0, row.QuantityWrites);
            var values = new List<TreeDataGridCellValueChangedEventArgs>(); grid.CellValueChanged += (_, e) => { if (e.Cell == cell) values.Add(e); };
            Assert.True(grid.BeginEdit(cell)); grid.EditSession!.Value = "42"; Assert.True(grid.CommitEdit()); window.UpdateLayout();
            Assert.Equal(42, row.Quantity); Assert.Equal(1, row.QuantityWrites);
            var changed = Assert.Single(values); Assert.Equal(12, changed.OldValue); Assert.Equal(42, changed.NewValue);
            Assert.Equal(TreeDataGridCellValueChangeReason.EditCommit, changed.Reason);
        }
        finally { window.Close(); grid.ItemsSource = null; }
        Assert.Equal(0, row.Subscribers); Assert.Equal(0, row.Hierarchy.Subscribers); Assert.Equal(0, row.Detail.Subscribers);
    }

    [AvaloniaFact]
    public void ConvertBack_receives_model_type_parameter_and_culture_before_validator_and_single_writer()
    {
        var row = new A3Row(); var view = new A3BindingView(); var grid = view.FindControl<GridControl>("BindingGrid")!;
        grid.ItemsSource = new[] { row }; var binding = (CompiledBinding)((TreeDataGridTextColumn)grid.Columns[2]).Binding!;
        var converter = (A3Converter)binding.Converter!; var validated = 0;
        grid.Columns[2].ValueValidator = value => { Assert.Equal(0, row.AmountWrites); Assert.Equal(25.75m, value); validated++; return null; };
        var window = A1TestHost.Show(view, 700);
        try
        {
            var cell = new CellAddress(grid.Source!.Rows[0].Id, grid.Columns[2].Id); Assert.True(grid.BeginEdit(cell));
            Assert.Equal(12.5m, grid.EditSession!.OriginalValue); Assert.Equal("amount 12,5", grid.EditSession.Value);
            grid.EditSession.Value = "amount 25,75"; Assert.True(grid.CommitEdit());
            Assert.Equal(typeof(decimal), converter.LastType); Assert.Equal("units", converter.LastParameter);
            Assert.Equal("fr-FR", converter.LastCulture!.Name); Assert.Equal(1, converter.Calls); Assert.Equal(1, validated);
            Assert.Equal(1, row.AmountWrites); Assert.Equal(25.75m, row.Amount);
        }
        finally { window.Close(); grid.ItemsSource = null; }
    }

    [AvaloniaTheory]
    [InlineData("unset")]
    [InlineData("nothing")]
    [InlineData("throw")]
    [InlineData("wrong-type")]
    [InlineData("null")]
    [InlineData("notification")]
    public void Failed_ConvertBack_preserves_draft_without_validation_writer_or_value_event(string failure)
    {
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var converter = new A3Converter { Back = (_, _, _, _) => failure switch
        { "unset" => AvaloniaProperty.UnsetValue, "nothing" => BindingOperations.DoNothing, "wrong-type" => "not decimal", "null" => null,
            "notification" => new BindingNotification(new FormatException("binding error"), BindingErrorType.Error), _ => throw new FormatException("invalid") } };
        var binding = TreeDataGridBinding.Create<A3Row, decimal>(r => r.Amount); binding.Converter = converter;
        var column = new TreeDataGridTextColumn { Binding = binding }; var validated = 0; column.ValueValidator = _ => { validated++; return null; };
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var values = 0; grid.CellValueChanged += (_, _) => values++;
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, column.Id))); var edit = grid.EditSession!; edit.Value = "draft";
            Assert.False(grid.CommitEdit()); Assert.Same(edit, grid.EditSession); Assert.Equal("draft", edit.Value);
            Assert.NotNull(edit.ValidationError); Assert.Equal(0, validated); Assert.Equal(0, row.AmountWrites); Assert.Equal(0, values);
            converter.Back = (_, _, _, _) => 30m; Assert.True(grid.CommitEdit()); Assert.Equal(1, row.AmountWrites); Assert.Equal(1, values);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Reflection_converter_and_display_only_StringFormat_keep_the_raw_original_separate_from_draft()
    {
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var converter = new A3Converter(); var binding = new ReflectionBinding(nameof(A3Row.Amount))
        { Converter = converter, ConverterCulture = A3Converter.French, ConverterParameter = "parameter", StringFormat = "[{0}]" };
        var column = new TreeDataGridTextColumn { Binding = binding }; var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            var cell = new CellAddress(source.Rows[0].Id, column.Id); Assert.True(grid.BeginEdit(cell));
            Assert.Equal(12.5m, grid.EditSession!.OriginalValue); Assert.Equal("amount 12,5", grid.EditSession.Value);
            grid.EditSession.Value = "amount 21,5"; Assert.True(grid.CommitEdit()); Assert.Equal(21.5m, row.Amount);
            binding.Converter = null; binding.StringFormat = "amount {0}"; source.Refresh(); window.UpdateLayout();
            Assert.True(grid.BeginEdit(cell)); Assert.Equal(21.5m, grid.EditSession!.Value);
            grid.EditSession.Value = "amount 40"; Assert.False(grid.CommitEdit()); Assert.Equal(1, row.AmountWrites);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Intermediate_replacement_rejects_old_draft_for_reflection_and_compiled_adapter(bool compiled)
    {
        var row = new A3Row(); var old = row.Detail;
        using var source = new FlatTreeDataGridSource<A3Row>([row]);
        BindingBase binding = compiled ? TreeDataGridBinding.Create<A3Row, string>(r => r.Detail.Name) : new ReflectionBinding("Detail.Name");
        var column = new TreeDataGridTextColumn { Binding = binding }; var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var cell = new CellAddress(source.Rows[0].Id, column.Id); grid.BeginEdit(cell); grid.EditSession!.Value = "draft";
            row.Detail = new(); window.UpdateLayout(); Assert.Equal(0, old.Subscribers);
            Assert.False(grid.CommitEdit()); Assert.Equal(0, old.Writes); Assert.Equal(0, row.Detail.Writes);
            grid.CancelEdit(); Assert.True(grid.BeginEdit(cell)); grid.EditSession!.Value = "new target"; Assert.True(grid.CommitEdit());
            Assert.Equal(1, row.Detail.Writes); Assert.Equal("new target", row.Detail.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("Slots[0].Name")]
    [InlineData("Map['key'].Name")]
    [InlineData("Array[0].Name")]
    public void Constant_index_reference_targets_are_captured_and_replacement_cannot_receive_old_draft(string path)
    {
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var column = new TreeDataGridTextColumn { Binding = new ReflectionBinding(path) }; var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            var old = path.StartsWith("Slots", StringComparison.Ordinal) ? row.Slots[0] : path.StartsWith("Map", StringComparison.Ordinal) ? row.Map["key"] : row.Array[0];
            var cell = new CellAddress(source.Rows[0].Id, column.Id); Assert.True(grid.BeginEdit(cell)); grid.EditSession!.Value = "draft";
            if (path.StartsWith("Slots", StringComparison.Ordinal)) row.Slots[0] = new();
            else if (path.StartsWith("Map", StringComparison.Ordinal)) row.Map["key"] = new();
            else row.Array[0] = new();
            Assert.False(grid.CommitEdit()); Assert.Equal(0, old.Writes);
            grid.CancelEdit(); Assert.True(grid.BeginEdit(cell)); grid.EditSession!.Value = "committed"; Assert.True(grid.CommitEdit());
            Assert.Equal(0, old.Writes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Factory_preserves_index_arguments_that_diagnostic_path_strings_lose()
    {
        var row = new A3Row(); row.Slots.Add(new());
        using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var binding = TreeDataGridBinding.Create<A3Row, string>(r => r.Slots[1].Name);
        var column = new TreeDataGridTextColumn { Binding = binding }; var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, column.Id))); grid.EditSession!.Value = "second"; Assert.True(grid.CommitEdit());
            Assert.Equal(0, row.Slots[0].Writes); Assert.Equal("second", row.Slots[1].Name); Assert.Equal(1, row.Slots[1].Writes);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("path")]
    [InlineData("binding")]
    [InlineData("mode")]
    [InlineData("converter")]
    public void Mutable_binding_changes_after_begin_reject_commit_before_write(string change)
    {
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var binding = TreeDataGridBinding.Create<A3Row, int>(r => r.Quantity);
        var column = new TreeDataGridTextColumn { Binding = binding }; var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            grid.BeginEdit(new(source.Rows[0].Id, column.Id)); grid.EditSession!.Value = "42";
            if (change == "path") binding.Path = CompiledBinding.Create<A3Row, decimal>(r => r.Amount).Path;
            else if (change == "binding") column.Binding = new ReflectionBinding(nameof(A3Row.Quantity));
            else if (change == "mode") binding.Mode = BindingMode.OneWay;
            else binding.Converter = new A3Converter();
            Assert.False(grid.CommitEdit()); Assert.Equal(0, row.QuantityWrites); Assert.Equal(0, row.AmountWrites);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Explicit_source_writes_only_captured_source_and_replacement_is_rejected()
    {
        var row = new A3Row(); var external = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var binding = new ReflectionBinding(nameof(A3Row.Quantity)) { Source = external };
        var column = new TreeDataGridTextColumn { Binding = binding }; var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var cell = new CellAddress(source.Rows[0].Id, column.Id); Assert.True(grid.BeginEdit(cell)); grid.EditSession!.Value = "42";
            Assert.True(grid.CommitEdit()); Assert.Equal(42, external.Quantity); Assert.Equal(0, row.QuantityWrites);
            Assert.True(grid.BeginEdit(cell)); grid.EditSession!.Value = "51"; binding.Source = new A3Row();
            Assert.False(grid.CommitEdit()); Assert.Equal(1, external.QuantityWrites); Assert.Equal(0, ((A3Row)binding.Source).QuantityWrites);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Converter_or_validator_replacing_nested_target_never_calls_its_setter()
    {
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var binding = TreeDataGridBinding.Create<A3Row, string>(r => r.Detail.Name);
        var column = new TreeDataGridTextColumn { Binding = binding }; var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var cell = new CellAddress(source.Rows[0].Id, column.Id); var old = row.Detail;
            column.ValueConverter = value => { row.Detail = new(); return value; };
            grid.BeginEdit(cell); grid.EditSession!.Value = "draft"; Assert.False(grid.CommitEdit()); Assert.Equal(0, old.Writes); Assert.Equal(0, row.Detail.Writes);
            grid.CancelEdit(); column.ValueConverter = null; old = row.Detail;
            column.ValueValidator = _ => { row.Detail = new(); return null; };
            grid.BeginEdit(cell); grid.EditSession!.Value = "draft"; Assert.False(grid.CommitEdit()); Assert.Equal(0, old.Writes); Assert.Equal(0, row.Detail.Writes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Unregistered_compiled_visual_paths_value_intermediates_and_index_slots_give_diagnostics()
    {
        var row = new A3Row();
        var raw = new TreeDataGridTextColumn { Binding = CompiledBinding.Create<A3Row, string>(r => r.Name) };
        Assert.Contains("opaque", raw.GetEditBindingError(row));
        Assert.Contains("Value-type", new TreeDataGridTextColumn { Binding = new ReflectionBinding("Struct.Name") }.GetEditBindingError(row));
        Assert.Contains("index slots", new TreeDataGridTextColumn { Binding = new ReflectionBinding("Slots[0]") }.GetEditBindingError(row));
        Assert.Contains("ElementName", new TreeDataGridTextColumn { Binding = new ReflectionBinding("Name") { ElementName = "Other" } }.GetEditBindingError(row));
        raw.SetValueAccessors<A3Row, string>(r => r.Name, (r, value) => r.Name = value); Assert.Null(raw.GetEditBindingError(row));
        raw.SetValueAccessors<A3Row, string>(r => r.Name); Assert.Contains("no setter", raw.GetEditBindingError(row));
    }

    [Fact]
    public void Factory_refuses_computed_and_variable_index_paths_instead_of_guessing_an_edit_target()
    {
        var slot = 0;
        Assert.Throws<NotSupportedException>(() => TreeDataGridBinding.Create<A3Row, string>(r => r.Slots[slot].Name));
        Assert.Throws<NotSupportedException>(() => TreeDataGridBinding.Create<A3Row, bool>(r => !r.Hierarchy.Expanded));
        Assert.Throws<NotSupportedException>(() => TreeDataGridBinding.Create<A3Row, string>(r => r.Name.ToUpperInvariant()));
    }

    [AvaloniaFact]
    public void Recycled_compiled_editor_commits_once_by_identity_and_model_target_replacement_is_still_guarded()
    {
        var rows = Enumerable.Range(0, 100).Select(_ => new A3Row()).ToArray(); using var source = new FlatTreeDataGridSource<A3Row>(rows);
        var column = new TreeDataGridTextColumn { Binding = TreeDataGridBinding.Create<A3Row, string>(r => r.Detail.Name) };
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var cell = new CellAddress(source.Rows[0].Id, column.Id); grid.BeginEdit(cell); grid.EditSession!.Value = "offscreen";
            grid.RowsPresenter!.Offset = new Vector(0, 1800); window.UpdateLayout();
            var changes = new List<TreeDataGridCellValueChangedEventArgs>(); grid.CellValueChanged += (_, e) => { if (e.Cell == cell) changes.Add(e); };
            Assert.True(grid.CommitEdit()); Assert.Equal("offscreen", rows[0].Detail.Name); Assert.Equal(1, rows[0].Detail.Writes); Assert.Single(changes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Validator_rejects_converted_model_value_and_retry_preserves_the_original_draft()
    {
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]); var allowed = false;
        var binding = TreeDataGridBinding.Create<A3Row, decimal>(r => r.Amount); binding.Converter = new A3Converter { Back = (_, _, _, _) => 42m };
        var column = new TreeDataGridTextColumn { Binding = binding, ValueValidator = value => { Assert.Equal(42m, value); return allowed ? null : "blocked"; } };
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            grid.BeginEdit(new(source.Rows[0].Id, column.Id)); var edit = grid.EditSession!; edit.Value = "formatted draft";
            Assert.False(grid.CommitEdit()); Assert.Same(edit, grid.EditSession); Assert.Equal("formatted draft", edit.Value); Assert.Equal("blocked", edit.ValidationError);
            Assert.Equal(0, row.AmountWrites); allowed = true; Assert.True(grid.CommitEdit()); Assert.Equal(1, row.AmountWrites);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("selection")]
    [InlineData("source")]
    public void ConvertBack_callback_reentry_or_source_replacement_never_writes_a_stale_model(string action)
    {
        var row = new A3Row(); var replacementRow = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        using var replacement = new FlatTreeDataGridSource<A3Row>([replacementRow]);
        var binding = TreeDataGridBinding.Create<A3Row, decimal>(r => r.Amount); var converter = new A3Converter(); binding.Converter = converter;
        var column = new TreeDataGridTextColumn { Binding = binding }; var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            converter.Back = (_, _, _, _) => { if (action == "selection") grid.State!.Select(source.Rows[0].Id); else grid.Source = replacement; return 42m; };
            grid.BeginEdit(new(source.Rows[0].Id, column.Id)); grid.EditSession!.Value = "draft";
            Assert.False(grid.CommitEdit()); Assert.Equal(0, row.AmountWrites); Assert.Equal(0, replacementRow.AmountWrites);
            if (action == "selection") { Assert.NotNull(grid.EditSession!.ValidationError); grid.CancelEdit(); }
            else { Assert.Same(replacement, grid.Source); Assert.Null(grid.EditSession); }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Default_converter_culture_uses_current_culture_and_cancel_never_calls_ConvertBack()
    {
        var previous = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = A3Converter.French;
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var converter = new A3Converter(); var binding = TreeDataGridBinding.Create<A3Row, decimal>(r => r.Amount); binding.Converter = converter;
        var column = new TreeDataGridTextColumn { Binding = binding }; var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var cell = new CellAddress(source.Rows[0].Id, column.Id); grid.BeginEdit(cell); grid.EditSession!.Value = "amount 31,5"; grid.CancelEdit(); Assert.Equal(0, converter.Calls);
            grid.BeginEdit(cell); grid.EditSession!.Value = "amount 31,5"; Assert.True(grid.CommitEdit());
            Assert.Equal("fr-FR", converter.LastCulture!.Name); Assert.Equal(31.5m, row.Amount);
        }
        finally { window.Close(); CultureInfo.CurrentCulture = previous; }
    }

    [AvaloniaFact]
    public void Explicit_column_converter_takes_precedence_and_no_update_tokens_are_never_model_values()
    {
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var converter = new A3Converter(); var binding = TreeDataGridBinding.Create<A3Row, decimal>(r => r.Amount); binding.Converter = converter;
        var allowed = false; var column = new TreeDataGridTextColumn { Binding = binding, ValueConverter = _ => allowed ? 77m : BindingOperations.DoNothing };
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            grid.BeginEdit(new(source.Rows[0].Id, column.Id)); Assert.False(grid.CommitEdit()); Assert.Equal(0, row.AmountWrites); Assert.Equal(0, converter.Calls);
            allowed = true; Assert.True(grid.CommitEdit()); Assert.Equal(77m, row.Amount); Assert.Equal(0, converter.Calls);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Null_intermediate_and_restored_reference_do_not_receive_an_old_compiled_draft()
    {
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]); var old = row.Detail;
        var column = new TreeDataGridTextColumn { Binding = TreeDataGridBinding.Create<A3Row, string>(r => r.Detail.Name) };
        var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var cell = new CellAddress(source.Rows[0].Id, column.Id); grid.BeginEdit(cell); grid.EditSession!.Value = "draft";
            row.Detail = null!; Assert.False(grid.CommitEdit()); Assert.Equal(0, old.Writes);
            grid.CancelEdit(); Assert.False(grid.BeginEdit(cell)); row.Detail = new(); Assert.True(grid.BeginEdit(cell));
            grid.EditSession!.Value = "restored"; Assert.True(grid.CommitEdit()); Assert.Equal("restored", row.Detail.Name); Assert.Equal(0, old.Writes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Factory_does_not_guess_a_hidden_runtime_member_from_the_compiled_display_name()
    {
        var row = new DerivedRow();
        var column = new TreeDataGridTextColumn { Binding = TreeDataGridBinding.Create<BaseRow, string>(r => r.Name) };
        Assert.Contains("compiled expression member", column.GetEditBindingError(row));
        using var source = new FlatTreeDataGridSource<BaseRow>([row]); var grid = A1TestHost.Create(source, column);
        Assert.False(grid.BeginEdit(new(source.Rows[0].Id, column.Id))); Assert.Equal("base", ((BaseRow)row).Name); Assert.Equal("derived", row.Name);
    }

    [AvaloniaFact]
    public void Reregistering_an_adapter_path_during_edit_invalidates_the_captured_configuration()
    {
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var binding = TreeDataGridBinding.Create<A3Row, string>(r => r.Detail.Name); var column = new TreeDataGridTextColumn { Binding = binding };
        var grid = A1TestHost.Create(source, column); Assert.True(grid.BeginEdit(new(source.Rows[0].Id, column.Id))); grid.EditSession!.Value = "draft";
        TreeDataGridBinding.Attach(binding, "Name"); Assert.False(grid.CommitEdit()); Assert.Equal(0, row.NameWrites); Assert.Equal(0, row.Detail.Writes);
    }
    private class BaseRow { public string Name { get; set; } = "base"; }
    private sealed class DerivedRow : BaseRow { public new string Name { get; set; } = "derived"; }

    [AvaloniaFact]
    public void Nullable_source_type_is_passed_to_ConvertBack_and_known_null_is_committed_once()
    {
        var row = new A3Row(); using var source = new FlatTreeDataGridSource<A3Row>([row]);
        var converter = new A3Converter { Back = (_, _, _, _) => null };
        var binding = TreeDataGridBinding.Create<A3Row, int?>(r => r.OptionalNumber); binding.Converter = converter;
        var column = new TreeDataGridTextColumn { Binding = binding }; var grid = A1TestHost.Create(source, column); var window = A1TestHost.Show(grid);
        try
        {
            var changes = new List<TreeDataGridCellValueChangedEventArgs>(); grid.CellValueChanged += (_, e) => changes.Add(e);
            grid.BeginEdit(new(source.Rows[0].Id, column.Id)); grid.EditSession!.Value = "clear";
            Assert.True(grid.CommitEdit()); Assert.Equal(typeof(int?), converter.LastType); Assert.Null(row.OptionalNumber); Assert.Equal(1, row.OptionalWrites);
            var change = Assert.Single(changes); Assert.Equal(5, change.OldValue); Assert.True(change.HasNewValue); Assert.Null(change.NewValue);
        }
        finally { window.Close(); }
    }
}
