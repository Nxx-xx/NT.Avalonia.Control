using Avalonia.Controls;
using Avalonia.Data;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private async Task RunBindingWritebackDemoSmokeAsync(Dictionary<string, bool> checks, List<object> snapshots, string? imagePath, List<string> images)
    {
        await DemoSelectScenarioAsync("binding-writeback");
        var root = _nestedBindingRoots![0]; var source = _source!; var id = source.Rows[0].Id;
        var quantity = (TreeDataGridTextColumn)_grid.Columns[1]; var tree = (TreeDataGridHierarchicalExpanderColumn)_grid.Columns[0];
        checks["a3CompiledAdapterHasAutomaticWriter"] = quantity.Binding is CompiledBinding && quantity.ValueSetter is null
            && quantity.GetEditBindingError(root) is null && tree.ChildrenBinding is CompiledBinding && tree.IsExpandedBinding is CompiledBinding;
        source.SetExpanded(id, false); var collapsed = !root.State!.Expanded && source.Rows.Count == 1;
        source.SetExpanded(id, true); checks["a3CompiledHierarchyExpansionWritesBack"] = collapsed && root.State!.Expanded && source.Rows.Count == 3;
        DemoPressButton(DemoFind<Button>(this, "BeginSafeBindingEditButton"));
        var edit = _grid.EditSession!; edit.Value = "invalid";
        checks["a3ConvertBackFailureHasZeroWrites"] = !_grid.CommitEdit() && ReferenceEquals(edit, _grid.EditSession) && root.QuantityWrites == 0;
        edit.Value = "数量 200";
        checks["a3ConvertedValueIsValidatedBeforeWriter"] = !_grid.CommitEdit() && edit.ValidationError == "数量必须为 0 到 100 的整数。" && root.QuantityWrites == 0;
        edit.Value = "数量 42";
        checks["a3ConvertedCompiledValueCommitsOnce"] = _grid.CommitEdit() && root.Quantity == 42 && root.QuantityWrites == 1;
        _grid.BeginEdit(new(id, quantity.Id)); _grid.EditSession!.Value = "数量 77"; _grid.CancelEdit();
        checks["a3CompiledDraftCancellationDoesNotWrite"] = root.Quantity == 42 && root.QuantityWrites == 1;
        var oldState = root.State!; var oldLabel = oldState.Label;
        _grid.BeginEdit(new(id, _grid.Columns[2].Id)); _grid.EditSession!.Value = "旧目标草稿";
        ReplaceNestedBindingState();
        checks["a3CompiledNestedReplacementRejectsOldDraft"] = !_grid.CommitEdit() && oldState.Label == oldLabel && root.State!.Label == "替换后的状态";
        _grid.CancelEdit(); UpdateLayout(); snapshots.Add(Snapshot("a3-binding-writeback"));
        if (imagePath is not null) DemoCapture(imagePath, "a3-binding-writeback", images);
    }
}
