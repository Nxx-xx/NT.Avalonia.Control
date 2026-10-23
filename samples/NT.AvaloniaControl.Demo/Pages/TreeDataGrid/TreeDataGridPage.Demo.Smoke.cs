using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using NT.AvaloniaControl.Demo.Pages.TreeDataGrid.Mvvm;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    internal async Task RunDemoSmokeAsync(string[] args)
    {
        var outputPath = DemoArgument(args, "--smoke-output") ?? Path.Combine("artifacts", "platform", "windows-demo-smoke.json");
        var imageArgument = DemoArgument(args, "--smoke-image");
        var imagePath = imageArgument is null ? null : Path.GetFullPath(imageArgument);
        var checks = new Dictionary<string, bool>();
        var snapshots = new List<object>();
        var images = new List<string>();
        string? error = null;
        try
        {
            await Task.Delay(250);
            checks["nativeWindowCreated"] = _hostWindow.TryGetPlatformHandle() is { Handle: not 0 };
            var shell = (MainWindow)_hostWindow;
            var navigation = DemoFind<ListBox>(shell, "ControlNavigation");
            checks["treePageInitiallySelected"] = shell.SelectedPageId == "treedatagrid" && navigation.SelectedIndex == 1
                && shell.GetVisualDescendants().Contains(this);
            var modes = _modeTabs.ItemsSource!.Cast<TabItem>().ToArray();
            checks["parallelModeTabs"] = modes.Length == 2 && Equals(modes[0].Header, "普通模式") && Equals(modes[1].Header, "MVVM 模式");
            _modeTabs.SelectedIndex = 0;
            await DemoSelectScenarioAsync("editing");
            checks["normalTabVisible"] = this.GetVisualDescendants().Contains(_grid) && !this.GetVisualDescendants().Contains(_mvvmView);
            checks["normalIdleButtonStates"] = _beginEditButton.IsEnabled && !_commitEditButton.IsEnabled && !_cancelEditButton.IsEnabled;
            DemoPressButton(_beginEditButton);
            UpdateLayout();
            checks["normalEditingButtonStates"] = !_beginEditButton.IsEnabled && _commitEditButton.IsEnabled && _cancelEditButton.IsEnabled
                && _grid.State?.EditSession is not null;
            DemoPressButton(_cancelEditButton);
            checks["normalCancelRestoresButtonStates"] = _grid.State?.EditSession is null && _beginEditButton.IsEnabled
                && !_commitEditButton.IsEnabled && !_cancelEditButton.IsEnabled;
            if (imagePath is not null) { CaptureSmokeImage(imagePath); images.Add(imagePath); }

            _modeTabs.SelectedIndex = 1;
            await Task.Delay(100);
            UpdateLayout();
            var vm = (MvvmDemoViewModel)_mvvmView.DataContext!;
            var boundGrid = DemoFind<GridControl>(_mvvmView, "BoundGrid");
            checks["mvvmTabVisible"] = this.GetVisualDescendants().Contains(_mvvmView) && !this.GetVisualDescendants().Contains(_grid)
                && ReferenceEquals(boundGrid.Source, vm.Source);
            var originalQuantity = vm.Items[0].Quantity;
            vm.CurrentCell = new CellAddress(vm.Source.Rows[0].Id, boundGrid.Columns[1].Id);
            DemoPressButton(DemoFind<Button>(_mvvmView, "BeginEditButton"));
            UpdateLayout();
            var draft = DemoFind<TextBox>(_mvvmView, "DraftEditor");
            draft.Text = "63";
            var session = boundGrid.State!.EditSession;
            _modeTabs.SelectedIndex = 0;
            await Task.Delay(80);
            _modeTabs.SelectedIndex = 1;
            await Task.Delay(100);
            UpdateLayout();
            checks["mvvmDraftSurvivesTabSwitch"] = ReferenceEquals(vm, _mvvmView.DataContext) && ReferenceEquals(session, boundGrid.State.EditSession)
                && vm.Editing.IsEditing && draft.Text == "63" && vm.Editing.EditSession?.Value?.ToString() == "63" && vm.Items[0].Quantity == originalQuantity;
            if (imagePath is not null) DemoCapture(imagePath, "mvvm", images);
            var normalSource = _source;
            var mvvmSource = boundGrid.Source;
            var treeNavigationItem = navigation.SelectedItem;
            shell.SelectPage("overview");
            await Task.Delay(100);
            _hostWindow.UpdateLayout();
            checks["overviewNavigationHidesTreePage"] = shell.SelectedPageId == "overview" && navigation.SelectedIndex == 0
                && !shell.GetVisualDescendants().Contains(this);
            checks["pageStatusTimerStopsOnOverview"] = !_timer.IsEnabled;
            if (imagePath is not null) DemoCapture(imagePath, "overview", images);
            var openTreeButton = shell.GetVisualDescendants().OfType<Button>()
                .Single(button => Equals(button.Content, "打开 TreeDataGrid"));
            DemoPressButton(openTreeButton);
            await Task.Delay(100);
            _hostWindow.UpdateLayout();
            checks["overviewButtonNavigatesAndSyncsSidebar"] = shell.SelectedPageId == "treedatagrid"
                && ReferenceEquals(treeNavigationItem, navigation.SelectedItem) && shell.GetVisualDescendants().Contains(this);
            checks["treeNavigationRestoresSamePageAndSources"] = shell.SelectedPageId == "treedatagrid" && navigation.SelectedIndex == 1
                && shell.GetVisualDescendants().Contains(this) && _modeTabs.SelectedIndex == 1
                && ReferenceEquals(normalSource, _source) && ReferenceEquals(mvvmSource, boundGrid.Source)
                && ReferenceEquals(vm, _mvvmView.DataContext);
            checks["mvvmDraftSurvivesPageNavigation"] = ReferenceEquals(session, boundGrid.State.EditSession)
                && vm.Editing.IsEditing && DemoFind<TextBox>(_mvvmView, "DraftEditor").Text == "63"
                && vm.Editing.EditSession?.Value?.ToString() == "63" && vm.Items[0].Quantity == originalQuantity;
            checks["pageStatusTimerResumesOnReturn"] = _timer.IsEnabled;

            var initialTheme = _hostWindow.ActualThemeVariant;
            var requestedTheme = _hostWindow.RequestedThemeVariant;
            var themeButton = DemoFind<Button>(shell, "ThemeToggleButton");
            var sidebar = navigation.GetVisualAncestors().OfType<DockPanel>().First();
            DemoPressButton(themeButton);
            var themeWait = await DemoWaitForAsync(() => !Equals(_hostWindow.ActualThemeVariant, initialTheme)
                && Equals(ActualThemeVariant, _hostWindow.ActualThemeVariant)
                && Equals(_mvvmView.ActualThemeVariant, _hostWindow.ActualThemeVariant)
                && Equals(boundGrid.ActualThemeVariant, _hostWindow.ActualThemeVariant)
                && Equals(navigation.ActualThemeVariant, _hostWindow.ActualThemeVariant)
                && Equals(sidebar.ActualThemeVariant, _hostWindow.ActualThemeVariant));
            _hostWindow.UpdateLayout();
            checks["themeButtonChangesHostTheme"] = themeWait.Satisfied && !Equals(_hostWindow.ActualThemeVariant, initialTheme);
            checks["pageMvvmAndSidebarFollowHostTheme"] = Equals(ActualThemeVariant, _hostWindow.ActualThemeVariant)
                && Equals(_mvvmView.ActualThemeVariant, _hostWindow.ActualThemeVariant)
                && Equals(boundGrid.ActualThemeVariant, _hostWindow.ActualThemeVariant)
                && Equals(navigation.ActualThemeVariant, _hostWindow.ActualThemeVariant)
                && Equals(sidebar.ActualThemeVariant, _hostWindow.ActualThemeVariant);
            checks["themeChangePreservesPageSourcesAndDraft"] = shell.SelectedPageId == "treedatagrid"
                && shell.GetVisualDescendants().Contains(this) && ReferenceEquals(treeNavigationItem, navigation.SelectedItem)
                && ReferenceEquals(normalSource, _source) && ReferenceEquals(mvvmSource, boundGrid.Source)
                && ReferenceEquals(vm, _mvvmView.DataContext) && ReferenceEquals(session, boundGrid.State.EditSession)
                && vm.Editing.IsEditing && DemoFind<TextBox>(_mvvmView, "DraftEditor").Text == "63"
                && vm.Editing.EditSession?.Value?.ToString() == "63" && vm.Items[0].Quantity == originalQuantity;
            if (imagePath is not null) DemoCapture(imagePath, "mvvm-theme", images);
            DemoPressButton(themeButton);
            _hostWindow.RequestedThemeVariant = requestedTheme;
            var themeRestoreWait = await DemoWaitForAsync(() => Equals(_hostWindow.ActualThemeVariant, initialTheme)
                && Equals(_mvvmView.ActualThemeVariant, initialTheme) && Equals(navigation.ActualThemeVariant, initialTheme));
            checks["themeButtonRestoresOriginalThemeAndDraft"] = themeRestoreWait.Satisfied
                && ReferenceEquals(session, boundGrid.State.EditSession) && vm.Editing.IsEditing
                && DemoFind<TextBox>(_mvvmView, "DraftEditor").Text == "63"
                && vm.Items[0].Quantity == originalQuantity;
            DemoPressButton(DemoFind<Button>(_mvvmView, "CancelEditButton"));
            checks["mvvmCancelAfterTabSwitch"] = !vm.Editing.IsEditing && vm.Items[0].Quantity == originalQuantity;
            _modeTabs.SelectedIndex = 0;
            await Task.Delay(80);

            checks["declaredScenarioOptions"] = _scenarioChoices.Count == 14 && _scenarioChoices.Select(choice => choice.Id).SequenceEqual(new[]
            {
                "editing", "tree", "lazy-tree", "columns", "visibility", "appearance", "rowmoving", "nestedbindings", "flat", "wide",
                "template-buttons", "template-colors", "template-clock", "template-composite",
            });
            foreach (var choice in _scenarioChoices.ToArray())
            {
                await DemoSelectScenarioAsync(choice.Id);
                checks[$"scenarioSelectable:{choice.Id}"] = ReferenceEquals(_scenarioPicker.SelectedItem, choice) && _source!.Rows.Count > 0
                    && _grid.RowsPresenter?.RealizedRowCount > 0;
                if (!choice.Id.StartsWith("template-", StringComparison.Ordinal)) continue;
                checks[$"templateControlsRendered:{choice.Id}"] = _source is FlatTreeDataGridSource<TemplateDemoItem> && _source.Rows.Count == 24
                    && _grid.IsReadOnly && _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>()
                        .Any(cell => cell.Column is TreeDataGridTemplateColumn && cell.GetVisualDescendants().OfType<Control>().Any());
                switch (choice.Id)
                {
                    case "template-buttons":
                        DemoTemplateButtons(checks, "buttons");
                        break;
                    case "template-colors":
                        await DemoTemplateColorAsync(checks, "colors");
                        break;
                    case "template-clock":
                        await DemoTemplateClockAsync(checks, snapshots);
                        break;
                    case "template-composite":
                        DemoTemplateButtons(checks, "composite");
                        await DemoTemplateColorAsync(checks, "composite");
                        DemoTemplateSettings(checks, "composite");
                        var state = DemoTemplateItem().State;
                        var slider = DemoTemplateCell().GetVisualDescendants().OfType<Slider>().Single();
                        var progress = DemoTemplateCell().GetVisualDescendants().OfType<ProgressBar>().Single();
                        slider.Value = 73;
                        UpdateLayout();
                        checks["compositeSliderBinding"] = state.Progress == 73 && progress.Value == 73
                            && DemoTemplateCell().GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "完成 73%");
                        checks["compositeControlsRemainDisplayTemplate"] = _grid.State!.EditSession is null;
                        break;
                }
                snapshots.Add(Snapshot(choice.Id));
                if (imagePath is not null) DemoCapture(imagePath, choice.Id, images);
            }
            await RunP6DemoSmokeAsync(checks, snapshots, imagePath, images);
            await RunRowMoveDemoSmokeAsync(checks, snapshots, imagePath, images);
            await DemoSelectScenarioAsync("editing");
            checks["editingLayoutRestored"] = _grid.RowHeight == 28 && !_grid.IsReadOnly && !_grid.AutoRowHeight && _grid.RowHeightSelector is null
                && _source is FlatTreeDataGridSource<EditingItem> && _templateClockItems is null && _templateClockTimer?.IsEnabled != true
                && _grid.GridLinesVisibility == TreeDataGridGridLinesVisibility.All && Equals(_grid.EmptyContent, "暂无数据") && _grid.EmptyContentTemplate is null
                && !_grid.CanUserReorderColumns && !_grid.CanUserMoveRows;
        }
        catch (Exception exception) { error = exception.ToString(); }

        var passed = error is null && checks.Count > 0 && checks.Values.All(value => value);
        try
        {
            var fullOutput = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            File.WriteAllText(fullOutput, JsonSerializer.Serialize(new
            {
                Passed = passed, TimestampUtc = DateTimeOffset.UtcNow,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Runtime = Environment.Version.ToString(), AvaloniaVersion = typeof(Application).Assembly.GetName().Version?.ToString(),
                WindowWidthDip = _hostWindow.ClientSize.Width, WindowHeightDip = _hostWindow.ClientSize.Height, RenderScaling = _hostWindow.RenderScaling,
                Checks = checks, Snapshots = snapshots, ImageOutputs = images, Error = error,
                ImageCaptureKind = "Avalonia RenderTargetBitmap offscreen native Window rendering; not a desktop screenshot.",
                NativeImeValidated = false, KylinValidated = false,
                Notes = "Unified native Demo uses its actual control navigation, tabs, scenario selector, template controls, Flyout, bindings and routed button Enter input. Physical keyboard, IME and desktop pixels are not automated.",
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(passed ? 0 : 1);
            else _hostWindow.Close();
        }
    }

    private async Task DemoSelectScenarioAsync(string id)
    {
        _scenarioPicker.SelectedIndex = _scenarioChoices.FindIndex(choice => choice.Id == id);
        await Task.Delay(100);
        UpdateLayout();
    }

    private TemplateDemoItem DemoTemplateItem() => (TemplateDemoItem)_source!.Rows[0].Model;

    private TreeDataGridCellPresenter DemoTemplateCell() => _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>()
        .Single(cell => ReferenceEquals(cell.Row?.Model, DemoTemplateItem()) && cell.ColumnIndex == 2);

    private void DemoTemplateButtons(Dictionary<string, bool> checks, string prefix)
    {
        var state = DemoTemplateItem().State;
        var buttons = DemoTemplateCell().GetVisualDescendants().OfType<Button>().ToArray();
        var increment = buttons.Single(button => Equals(button.Content, "执行 +1"));
        var reset = buttons.Single(button => Equals(button.Content, "重置"));
        var run = buttons.Single(button => Equals(button.Content, "暂停"));
        DemoPressButton(increment);
        UpdateLayout();
        checks[$"{prefix}ButtonEnterUpdatesModel"] = state.Count == 1 && ReferenceEquals(increment.DataContext, state)
            && DemoTemplateCell().GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "已执行 1 次");
        DemoPressButton(run);
        UpdateLayout();
        checks[$"{prefix}PauseDisablesIncrement"] = !state.IsRunning && !increment.IsEffectivelyEnabled
            && increment.Command?.CanExecute(increment.CommandParameter) == false && Equals(run.Content, "启动");
        DemoPressButton(reset);
        checks[$"{prefix}ResetUpdatesModel"] = state.Count == 0 && state.Progress == 0;
        DemoPressButton(run);
        checks[$"{prefix}ControlsDoNotStartEditing"] = state.IsRunning && _grid.State!.EditSession is null;
    }

    private async Task DemoTemplateColorAsync(Dictionary<string, bool> checks, string prefix)
    {
        var item = DemoTemplateItem();
        var state = item.State;
        var picker = DemoTemplateCell().GetVisualDescendants().OfType<TemplateColorPicker>().Single();
        var opener = (Button)picker.Content!;
        var flyout = (Flyout)opener.Flyout!;
        DemoPressButton(opener);
        await Task.Delay(100);
        var palette = (Control)flyout.Content!;
        var swatches = palette.GetVisualDescendants().OfType<Button>().ToArray();
        checks[$"{prefix}RealPaletteOpens"] = flyout.IsOpen && TopLevel.GetTopLevel(palette) is not null && swatches.Length == TemplateDemoState.Palette.Count;
        DemoPressButton(swatches[5]);
        UpdateLayout();
        var expectedColor = TemplateDemoState.Palette[5].Hex;
        checks[$"{prefix}PaletteUpdatesModelAndPreview"] = state.ColorHex == expectedColor && !flyout.IsOpen
            && _grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains(state.ColorDescription, StringComparison.Ordinal) == true)
            && picker.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == expectedColor);
        _grid.RowsPresenter!.Offset = new Vector(0, _grid.RowHeight * 20);
        await Task.Delay(100);
        var recycled = !_grid.RowsPresenter.RealizedRows.Any(row => ReferenceEquals(row.Row?.Model, item));
        _grid.RowsPresenter.Offset = default;
        await Task.Delay(100);
        UpdateLayout();
        var restoredPicker = DemoTemplateCell().GetVisualDescendants().OfType<TemplateColorPicker>().Single();
        checks[$"{prefix}ColorSurvivesRecycling"] = recycled && state.ColorHex == expectedColor && ReferenceEquals(restoredPicker.DataContext, state)
            && restoredPicker.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == expectedColor) && _grid.State!.EditSession is null;
    }

    private void DemoTemplateSettings(Dictionary<string, bool> checks, string prefix)
    {
        var state = DemoTemplateItem().State;
        var picker = DemoTemplateCell().GetVisualDescendants().OfType<TimePicker>().Single();
        var toggle = DemoTemplateCell().GetVisualDescendants().OfType<ToggleSwitch>().Single();
        var time = new TimeSpan(16, 35, 0);
        picker.SelectedTime = time;
        toggle.IsChecked = false;
        UpdateLayout();
        checks[$"{prefix}TimePickerTwoWayBinding"] = state.TargetTime == time && _grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.Contains("16:35", StringComparison.Ordinal) == true);
        checks[$"{prefix}ToggleTwoWayBinding"] = !state.IsRunning && toggle.IsChecked == false;
        toggle.IsChecked = true;
        checks[$"{prefix}ToggleResumesModel"] = state.IsRunning;
    }

    private async Task DemoTemplateClockAsync(Dictionary<string, bool> checks, List<object> snapshots)
    {
        DemoTemplateSettings(checks, "clock");
        var state = DemoTemplateItem().State;
        var cell = DemoTemplateCell();
        var face = cell.GetVisualDescendants().OfType<TemplateClockFace>().Single();
        var picker = cell.GetVisualDescendants().OfType<TimePicker>().Single();
        var toggle = cell.GetVisualDescendants().OfType<ToggleSwitch>().Single();
        var sourceVersion = _source!.Version;
        var time = state.ClockTime;
        var tickWait = await DemoWaitForAsync(() => state.ClockTime != time && face.Time == state.ClockTime
            && DemoTemplateCell().GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == state.CurrentTimeText));
        checks["clockTicksThroughBinding"] = tickWait.Satisfied;
        checks["clockTickKeepsControlsAndProjection"] = _source.Version == sourceVersion && ReferenceEquals(face, DemoTemplateCell().GetVisualDescendants().OfType<TemplateClockFace>().Single())
            && ReferenceEquals(picker, DemoTemplateCell().GetVisualDescendants().OfType<TimePicker>().Single());
        snapshots.Add(new
        {
            Name = "clock-binding-wait", tickWait.Satisfied, tickWait.Polls,
            InitialTime = time, ModelTime = state.ClockTime, BoundFaceTime = face.Time, ExpectedDigitalText = state.CurrentTimeText,
            RenderedTexts = DemoTemplateCell().GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToArray(),
            state.IsRunning, TimerEnabled = _templateClockTimer?.IsEnabled, SelectedTab = _modeTabs.SelectedIndex,
            InitialSourceVersion = sourceVersion, FinalSourceVersion = _source.Version,
            FacePreserved = ReferenceEquals(face, DemoTemplateCell().GetVisualDescendants().OfType<TemplateClockFace>().Single()),
            PickerPreserved = ReferenceEquals(picker, DemoTemplateCell().GetVisualDescendants().OfType<TimePicker>().Single()),
        });
        toggle.IsChecked = false;
        var pausedTime = state.ClockTime;
        var otherState = ((TemplateDemoItem)_source.Rows[1].Model).State;
        var otherTime = otherState.ClockTime;
        var pauseWait = await DemoWaitForAsync(() => otherState.ClockTime != otherTime);
        checks["clockRowPauseIsIndependent"] = pauseWait.Satisfied && state.ClockTime == pausedTime;
        snapshots.Add(new
        {
            Name = "clock-independent-pause-wait", pauseWait.Satisfied, pauseWait.Polls,
            InitialPausedTime = pausedTime, FinalPausedTime = state.ClockTime,
            InitialRunningTime = otherTime, FinalRunningTime = otherState.ClockTime,
            PausedRowRunning = state.IsRunning, OtherRowRunning = otherState.IsRunning,
            TimerEnabled = _templateClockTimer?.IsEnabled,
        });
        toggle.IsChecked = true;
        _modeTabs.SelectedIndex = 1;
        await Task.Delay(80);
        var inactiveTime = state.ClockTime;
        await Task.Delay(1200);
        checks["clockTimerStopsOnInactiveTab"] = _templateClockTimer?.IsEnabled == false && state.ClockTime == inactiveTime;
        snapshots.Add(new
        {
            Name = "clock-inactive-tab", InitialTime = inactiveTime, FinalTime = state.ClockTime,
            TimerEnabled = _templateClockTimer?.IsEnabled, SelectedTab = _modeTabs.SelectedIndex,
        });
        _modeTabs.SelectedIndex = 0;
        await Task.Delay(100);
        UpdateLayout();
        checks["clockTimerResumesOnNormalTab"] = _templateClockTimer?.IsEnabled == true
            && DemoTemplateCell().GetVisualDescendants().OfType<TemplateClockFace>().Single().Time == state.ClockTime;

        var shell = (MainWindow)_hostWindow;
        var source = _source;
        var clockItems = _templateClockItems;
        shell.SelectPage("overview");
        await Task.Delay(100);
        var inactivePageTime = state.ClockTime;
        await Task.Delay(1200);
        checks["clockTimerStopsOnInactivePage"] = shell.SelectedPageId == "overview"
            && _templateClockTimer?.IsEnabled == false && !_timer.IsEnabled && state.ClockTime == inactivePageTime;
        snapshots.Add(new
        {
            Name = "clock-inactive-page", InitialTime = inactivePageTime, FinalTime = state.ClockTime,
            TimerEnabled = _templateClockTimer?.IsEnabled, SelectedPage = shell.SelectedPageId,
        });
        shell.SelectPage("treedatagrid");
        await Task.Delay(100);
        _hostWindow.UpdateLayout();
        checks["clockNavigationKeepsSourceAndModels"] = shell.GetVisualDescendants().Contains(this)
            && ReferenceEquals(source, _source) && ReferenceEquals(clockItems, _templateClockItems)
            && ReferenceEquals(state, DemoTemplateItem().State);
        var returnedTime = state.ClockTime;
        var resumeWait = await DemoWaitForAsync(() => state.ClockTime != returnedTime
            && DemoTemplateCell().GetVisualDescendants().OfType<TemplateClockFace>().Single().Time == state.ClockTime);
        checks["clockTimerResumesOnPageReturn"] = _templateClockTimer?.IsEnabled == true && _timer.IsEnabled && resumeWait.Satisfied;
        snapshots.Add(new
        {
            Name = "clock-page-return", resumeWait.Satisfied, resumeWait.Polls,
            InitialTime = returnedTime, FinalTime = state.ClockTime,
            TimerEnabled = _templateClockTimer?.IsEnabled, SelectedPage = shell.SelectedPageId,
        });
    }

    private async Task<(bool Satisfied, int Polls)> DemoWaitForAsync(Func<bool> condition)
    {
        // Dispatcher timers and reflection bindings may complete on separate UI turns.
        // Observe the result for up to three seconds instead of assuming a single delay is enough.
        const int maximumPolls = 30;
        for (var polls = 0; polls <= maximumPolls; polls++)
        {
            UpdateLayout();
            if (condition()) return (true, polls);
            if (polls < maximumPolls) await Task.Delay(100);
        }
        return (false, maximumPolls);
    }

    private static T DemoFind<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static void DemoPressButton(Button button)
    {
        if (!button.IsEffectivelyEnabled) throw new InvalidOperationException($"Button {button.Name ?? button.Content?.ToString()} is disabled.");
        button.Focus();
        RouteSmokeKey(button, Key.Enter);
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Source = button, Key = Key.Enter });
    }

    private void DemoCapture(string imagePath, string scenario, List<string> images)
    {
        var path = ScenarioImagePath(imagePath, scenario);
        CaptureSmokeImage(path);
        images.Add(path);
    }

    private static string? DemoArgument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
