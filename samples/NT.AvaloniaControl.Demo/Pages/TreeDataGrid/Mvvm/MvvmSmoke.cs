using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid.Mvvm;

/// <summary>Native-window binding verification kept outside both the View and ViewModel.</summary>
internal static class MvvmSmoke
{
    public static async Task RunAsync(Window hostWindow, MvvmView view, MvvmDemoViewModel initial, string[] args)
    {
        var output = Argument(args, "--smoke-output") ?? Path.Combine("artifacts", "platform", "windows-mvvm-smoke.json");
        var imagePath = Argument(args, "--smoke-image");
        var checks = new Dictionary<string, bool>();
        MvvmDemoViewModel? replacement = null;
        string? error = null;
        var removeNotifications = 0;
        var modifyNotifications = 0;
        var rememberNotifications = 0;
        var restoreNotifications = 0;
        var summaryNotifications = 0;
        void RemoveChanged(object? sender, EventArgs e) => removeNotifications++;
        void ModifyChanged(object? sender, EventArgs e) => modifyNotifications++;
        void RememberChanged(object? sender, EventArgs e) => rememberNotifications++;
        void RestoreChanged(object? sender, EventArgs e) => restoreNotifications++;
        void SummaryChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MvvmDemoViewModel.SelectionSummary)) summaryNotifications++;
        }
        initial.RemoveCommand.CanExecuteChanged += RemoveChanged;
        initial.ModifyCommand.CanExecuteChanged += ModifyChanged;
        initial.RememberCurrentCommand.CanExecuteChanged += RememberChanged;
        initial.RestoreCurrentCommand.CanExecuteChanged += RestoreChanged;
        initial.PropertyChanged += SummaryChanged;
        try
        {
            await Task.Delay(250);
            var grid = Find<GridControl>(view, "BoundGrid");
            checks["nativeWindowCreated"] = hostWindow.TryGetPlatformHandle() is { Handle: not 0 };
            checks["compiledViewSourceBinding"] = ReferenceEquals(grid.Source, initial.Source) && grid.Source.Rows.Count == 100;
            checks["declarativeColumns"] = grid.Columns.Count == 4 && grid.Columns[0] is TreeDataGridTextColumn && grid.Columns[2] is TreeDataGridCheckBoxColumn;
            checks["modelBindingDisplayed"] = grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == initial.Items[0].Name);
            // Command.CanExecute contributes to effective enablement without changing IsEnabled.
            checks["selectionCommandsInitiallyDisabled"] = !initial.RemoveCommand.CanExecute(null) && !initial.ModifyCommand.CanExecute(null)
                && !Find<Button>(view, "RemoveRecordButton").IsEffectivelyEnabled && !Find<Button>(view, "ModifyRecordButton").IsEffectivelyEnabled;
            var rememberedCommandsInitiallyDisabled = !initial.RememberCurrentCommand.CanExecute(null) && !initial.RestoreCurrentCommand.CanExecute(null);

            Execute(view, "AddRecordButton");
            view.UpdateLayout();
            checks["observableCollectionAdd"] = initial.Items.Count == 101 && grid.Source!.Rows.Count == 101;
            checks["vmSelectedItemToView"] = ReferenceEquals(grid.SelectedItem, initial.Items[^1]) && ReferenceEquals(initial.SelectedItem, initial.Items[^1]);
            checks["selectionCommandsEnabledAfterSelection"] = initial.RemoveCommand.CanExecute(null) && initial.ModifyCommand.CanExecute(null)
                && removeNotifications > 0 && modifyNotifications > 0
                && Find<Button>(view, "RemoveRecordButton").IsEffectivelyEnabled && Find<Button>(view, "ModifyRecordButton").IsEffectivelyEnabled;
            initial.SelectFirstCommand.Execute(null);
            var first = initial.Items[0];
            var originalName = first.Name;
            summaryNotifications = 0;
            first.Name = originalName + "（已修改）";
            view.UpdateLayout();
            checks["selectedNameNotifiesSelectionSummary"] = summaryNotifications > 0 && initial.SelectionSummary.Contains(first.Name, StringComparison.Ordinal);
            first.Name = originalName;
            var oldQuantity = first.Quantity;
            Execute(view, "ModifyRecordButton");
            grid.RowsPresenter!.ScrollIntoView(initial.Source.Rows[0].Id);
            view.UpdateLayout();
            checks["modelPropertyChangeDisplayed"] = first.Quantity == oldQuantity + 1
                && grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().Any(cell => ReferenceEquals(cell.Row?.Model, first) && cell.ColumnIndex == 1 && Equals(cell.Value, first.Quantity));
            initial.SelectedItem = initial.Items[^1];
            var removeBeforeRemoval = removeNotifications;
            var modifyBeforeRemoval = modifyNotifications;
            Execute(view, "RemoveRecordButton");
            view.UpdateLayout();
            checks["observableCollectionRemove"] = initial.Items.Count == 100 && grid.Source!.Rows.Count == 100;
            checks["selectionCommandsDisabledAfterRemoval"] = !initial.RemoveCommand.CanExecute(null) && !initial.ModifyCommand.CanExecute(null)
                && removeNotifications > removeBeforeRemoval && modifyNotifications > modifyBeforeRemoval
                && !Find<Button>(view, "RemoveRecordButton").IsEffectivelyEnabled && !Find<Button>(view, "ModifyRecordButton").IsEffectivelyEnabled;

            initial.SelectThirdByPathCommand.Execute(null);
            view.UpdateLayout();
            checks["vmIndexPathSelection"] = grid.SelectedIndex == new IndexPath(2) && ReferenceEquals(grid.SelectedItem, initial.Items[2]);
            initial.SelectTwoCommand.Execute(null);
            view.UpdateLayout();
            checks["vmSelectedItemsToView"] = grid.State!.SelectedNodes.Count == 2 && initial.SelectedItems.Count == 2;
            var previousSelection = initial.SelectedItems;
            var newSelection = new ObservableCollection<MvvmRecord>(previousSelection);
            summaryNotifications = 0;
            initial.SelectedItems = newSelection;
            view.UpdateLayout();
            var replacementNotified = summaryNotifications > 0;
            summaryNotifications = 0;
            previousSelection.Clear();
            var previousCollectionDetached = summaryNotifications == 0;
            newSelection.Add(initial.Items[2]);
            view.UpdateLayout();
            checks["selectedItemsReplacementSubscriptions"] = replacementNotified && previousCollectionDetached && summaryNotifications > 0
                && ReferenceEquals(initial.SelectedItems, newSelection) && newSelection.Count == 3
                && initial.SelectionSummary.Contains("SelectedItems：3 条", StringComparison.Ordinal);
            var nameCell = new CellAddress(initial.Source.Rows[0].Id, grid.Columns[0].Id);
            var quantityCell = new CellAddress(initial.Source.Rows[0].Id, grid.Columns[1].Id);
            var rememberBeforeCurrentCell = rememberNotifications;
            var restoreBeforeRemembering = restoreNotifications;
            initial.CurrentCell = nameCell;
            view.UpdateLayout();
            checks["vmCurrentCellToView"] = grid.CurrentCell == nameCell && grid.State.CurrentCell == nameCell;
            var rememberEnabled = initial.RememberCurrentCommand.CanExecute(null) && rememberNotifications > rememberBeforeCurrentCell;
            initial.RememberCurrentCommand.Execute(null);
            checks["rememberedCellCommandEligibility"] = rememberedCommandsInitiallyDisabled && rememberEnabled
                && initial.RestoreCurrentCommand.CanExecute(null) && restoreNotifications > restoreBeforeRemembering;
            initial.CurrentCell = quantityCell;
            initial.RestoreCurrentCommand.Execute(null);
            view.UpdateLayout();
            checks["restoreRememberedCell"] = initial.CurrentCell == nameCell && grid.CurrentCell == nameCell;
            grid.Focus();
            RouteKey(grid, Key.Down);
            view.UpdateLayout();
            checks["viewSelectionAndCurrentToVm"] = ReferenceEquals(initial.SelectedItem, initial.Items[1])
                && initial.SelectedIndex == new IndexPath(1) && initial.CurrentCell?.Node == initial.Source.Rows[1].Id
                && initial.SelectedItems.Count == 1 && ReferenceEquals(initial.SelectedItems[0], initial.Items[1]);

            initial.CurrentCell = quantityCell;
            Execute(view, "BeginEditButton");
            view.UpdateLayout();
            var draft = Find<TextBox>(view, "DraftEditor");
            checks["boundBeginEditCommand"] = initial.Editing.IsEditing && initial.Editing.EditSession?.Cell == quantityCell && draft.IsEnabled;
            draft.Text = "200";
            view.UpdateLayout();
            checks["externalDraftBindingToEditor"] = initial.Editing.EditSession?.Value?.ToString() == "200"
                && grid.GetVisualDescendants().OfType<TextBox>().Single(text => text.Name == "PART_Editor").Text == "200";
            Execute(view, "CommitEditButton");
            view.UpdateLayout();
            checks["validationErrorBound"] = first.Quantity == oldQuantity + 1 && initial.Editing.IsEditing
                && !string.IsNullOrEmpty(initial.Editing.EditSession?.ValidationError)
                && Find<TextBlock>(view, "ValidationErrorText").Text == initial.Editing.EditSession.ValidationError;
            if (imagePath is not null) Capture(hostWindow, VariantPath(imagePath, "validation"));
            draft.Text = "42";
            Execute(view, "CommitEditButton");
            view.UpdateLayout();
            checks["boundCommitCommand"] = first.Quantity == 42 && !initial.Editing.IsEditing && !draft.IsEnabled;
            Execute(view, "BeginEditButton");
            view.UpdateLayout();
            var editor = grid.GetVisualDescendants().OfType<TextBox>().Single(text => text.Name == "PART_Editor");
            editor.Text = "9";
            view.UpdateLayout();
            checks["cellEditorDraftToVm"] = initial.Editing.EditSession?.Value?.ToString() == "9" && draft.Text == "9";
            Execute(view, "CancelEditButton");
            view.UpdateLayout();
            checks["boundCancelCommand"] = first.Quantity == 42 && !initial.Editing.IsEditing;

            initial.CurrentCell = nameCell;
            Execute(view, "BeginEditButton");
            view.UpdateLayout();
            draft.Text = "replacement must discard this";
            var oldSource = initial.Source;
            var rememberBeforeReplacement = rememberNotifications;
            var restoreBeforeReplacement = restoreNotifications;
            Execute(view, "ReplaceSourceButton");
            view.UpdateLayout();
            checks["vmSourceReplacement"] = !ReferenceEquals(oldSource, grid.Source) && ReferenceEquals(initial.Source, grid.Source)
                && !initial.Editing.IsEditing && initial.SelectedItems.Count == 0 && initial.CurrentCell is null && first.Name == "数据集 1 · 记录 001";
            checks["sourceReplacementResetsRememberedCommands"] = !initial.RememberCurrentCommand.CanExecute(null) && !initial.RestoreCurrentCommand.CanExecute(null)
                && rememberNotifications > rememberBeforeReplacement && restoreNotifications > restoreBeforeReplacement;

            var oldEditing = initial.Editing;
            replacement = new MvvmDemoViewModel();
            view.DataContext = replacement;
            await Task.Delay(100);
            checks["dataContextReplacement"] = ReferenceEquals(grid.Source, replacement.Source)
                && ReferenceEquals(grid.EditCommands, replacement.Editing) && !oldEditing.BeginEditCommand.CanExecute(null);
            replacement.CurrentCell = new CellAddress(replacement.Source.Rows[0].Id, grid.Columns[1].Id);
            Execute(view, "BeginEditButton");
            view.UpdateLayout();
            draft.Text = "55";
            Execute(view, "CommitEditButton");
            view.UpdateLayout();
            checks["commandsReboundAfterDataContext"] = replacement.Items[0].Quantity == 55 && initial.Items[0].Quantity == 11;
            if (imagePath is not null) Capture(hostWindow, imagePath);
        }
        catch (Exception exception) { error = exception.ToString(); }

        var passed = error is null && checks.Count == 29 && checks.Values.All(value => value);
        try
        {
            var fullOutput = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            File.WriteAllText(fullOutput, JsonSerializer.Serialize(new
            {
                Passed = passed, TimestampUtc = DateTimeOffset.UtcNow,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Runtime = Environment.Version.ToString(), AvaloniaVersion = typeof(Application).Assembly.GetName().Version?.ToString(),
                WindowWidthDip = hostWindow.ClientSize.Width, WindowHeightDip = hostWindow.ClientSize.Height, RenderScaling = hostWindow.RenderScaling,
                Checks = checks, Error = error,
                ImageOutputs = imagePath is null ? null : new[] { Path.GetFullPath(imagePath), Path.GetFullPath(VariantPath(imagePath, "validation")) },
                ImageCaptureKind = "Avalonia RenderTargetBitmap offscreen Window rendering; not a desktop screenshot.",
                NativeImeValidated = false, KylinValidated = false,
                Notes = "Native Window, compiled XAML View bindings, bound commands, programmatic editor and routed control input. This does not automate a physical keyboard or native IME.",
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            initial.RemoveCommand.CanExecuteChanged -= RemoveChanged;
            initial.ModifyCommand.CanExecuteChanged -= ModifyChanged;
            initial.RememberCurrentCommand.CanExecuteChanged -= RememberChanged;
            initial.RestoreCurrentCommand.CanExecuteChanged -= RestoreChanged;
            initial.PropertyChanged -= SummaryChanged;
            view.DataContext = null;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(passed ? 0 : 1);
            else hostWindow.Close();
            replacement?.Dispose();
        }
    }

    private static string? Argument(string[] args, string name) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    private static void Execute(MvvmView view, string name)
    {
        var button = Find<Button>(view, name);
        var command = button.Command ?? throw new InvalidOperationException($"No bound command on {name}.");
        if (!button.IsEffectivelyEnabled || !command.CanExecute(button.CommandParameter)) throw new InvalidOperationException($"Command {name} is disabled.");
        button.Focus();
        RouteKey(button, Key.Enter);
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Source = button, Key = Key.Enter });
    }
    private static T Find<T>(MvvmView view, string name) where T : Control =>
        view.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static void RouteKey(Control target, Key key) => target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = target, Key = key });
    private static string VariantPath(string path, string name) => Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + "-" + name + ".png");
    private static void Capture(Window hostWindow, string path)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        hostWindow.UpdateLayout();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(hostWindow.ClientSize.Width * hostWindow.RenderScaling), (int)Math.Ceiling(hostWindow.ClientSize.Height * hostWindow.RenderScaling)), new Vector(96 * hostWindow.RenderScaling, 96 * hostWindow.RenderScaling));
        bitmap.Render(hostWindow);
        bitmap.Save(path);
    }
}
