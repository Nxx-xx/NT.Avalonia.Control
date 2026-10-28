using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>A reusable cell. Templates may style this ContentControl; its content is the column's display control.</summary>
[PseudoClasses(":horizontal-grid-lines", ":vertical-grid-lines")]
public sealed partial class TreeDataGridCellPresenter : ContentControl
{
    public static readonly StyledProperty<object?> ValueProperty = AvaloniaProperty.Register<TreeDataGridCellPresenter, object?>(nameof(Value));
    private TreeDataGrid? _owner;
    private IDisposable? _binding;
    private Type? _kind;
    private Control? _display;
    private Button? _expander;
    private Grid? _expanderLayout;
    private TreeDataGridColumn? _displayColumn;
    private int _rowIndex;
    private long _generation;
    private bool _prepared;
    private bool _clearing;
    private ElementFactorySession? _elementSession;
    internal void DetachFactory() => _elementSession = null;
    internal long Generation => _generation;

    static TreeDataGridCellPresenter()
    {
        PaddingProperty.OverrideDefaultValue<TreeDataGridCellPresenter>(new Thickness(8, 3));
        BackgroundProperty.OverrideDefaultValue<TreeDataGridCellPresenter>(Brushes.Transparent);
        HorizontalContentAlignmentProperty.OverrideDefaultValue<TreeDataGridCellPresenter>(HorizontalAlignment.Stretch);
        TemplateProperty.OverrideDefaultValue<TreeDataGridCellPresenter>(new FuncControlTemplate<TreeDataGridCellPresenter>((_, _) =>
            new ContentPresenter
            {
                [!ContentPresenter.ContentProperty] = new TemplateBinding(ContentProperty),
                [!ContentPresenter.BackgroundProperty] = new TemplateBinding(BackgroundProperty),
                [!ContentPresenter.PaddingProperty] = new TemplateBinding(PaddingProperty),
                [!ContentPresenter.HorizontalContentAlignmentProperty] = new TemplateBinding(HorizontalContentAlignmentProperty)
            }));
    }
    public TreeDataGridRow? Row { get; private set; }
    public TreeDataGridColumn? Column { get; private set; }
    public int ColumnIndex { get; private set; } = -1;
    public object? Value => GetValue(ValueProperty);
    internal TreeDataGrid? GridOwner => _owner;
    // The default display is represented by the cell's Name/Value providers.
    // Editors and application-provided content keep their complete visual tree.
    internal bool IsPlainAutomationText => IsPlainAutomationContent(Content);
    internal bool IsPlainAutomationContent(object? content) => content is null || _display is TextBlock && ReferenceEquals(content, _display);
    protected override AutomationPeer OnCreateAutomationPeer() => new TreeDataGridCellPresenterAutomationPeer(this);

    internal void Prepare(TreeDataGrid owner, TreeDataGridRow row, int rowIndex, TreeDataGridColumn column, int columnIndex)
    {
        Clear();
        var generation = _generation;
        var source = owner.Source;
        _elementSession = owner.FactorySession;
        _owner = owner;
        Row = row;
        _rowIndex = rowIndex;
        Column = column;
        ColumnIndex = columnIndex;
        Theme = owner.CellTheme;
        ApplyGridLines();
        DataContext = row.Model;
        _displayColumn = column is TreeDataGridHierarchicalExpanderColumn tree ? tree.InnerColumn : column;
        EnsureDisplay();
        if (column is TreeDataGridHierarchicalExpanderColumn expanderColumn)
        {
            _expander ??= CreateExpander();
            _expanderLayout ??= new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            _expanderLayout.Children.Clear();
            _expander.Margin = new Thickness(row.Depth * expanderColumn.Indent, 0, 0, 0);
            _expander.Opacity = row.HasChildren ? 1 : 0;
            _expander.IsHitTestVisible = row.HasChildren;
            _expander.Content = row.IsExpanded ? "▾" : "▸";
            _expanderLayout.Children.Add(_expander);
            Grid.SetColumn(_display!, 1);
            _expanderLayout.Children.Add(_display!);
            Content = _expanderLayout;
        }
        else Content = _display;
        var binding = _displayColumn switch
        {
            TreeDataGridTextColumn text => text.Binding,
            TreeDataGridCheckBoxColumn check => check.Binding,
            _ => null
        };
        if (binding is not null)
        {
            var subscription = Bind(ValueProperty, binding);
            if (_generation != generation) { subscription.Dispose(); return; }
            _binding = subscription;
        }
        else
        {
            var value = _displayColumn.ValueGetter is { } getter ? getter(row.Model) : row.Model;
            if (_generation != generation) return;
            SetValue(ValueProperty, value);
        }
        if (_generation != generation) return;
        UpdateDisplay();
        ApplyInteractionState();
        if (_generation != generation || !owner.TrackCell(this, rowIndex) || _generation != generation) return;
        try
        {
            if (!_elementSession.Prepare(this, source, row, rowIndex, column, columnIndex) || _generation != generation) return;
            _prepared = true;
            owner.NotifyCellPrepared(this);
        }
        catch (Exception error)
        {
            List<Exception>? errors = [error];
            if (_generation == generation) try { Clear(); } catch (Exception clearingError) { errors.Add(clearingError); }
            TreeDataGridEventDispatch.Throw(errors);
        }
    }
    internal void Clear()
    {
        if (_clearing) return;
        _clearing = true;
        _generation++;
        List<Exception>? errors = null;
        try
        {
            try
            {
                if (_prepared) { _prepared = false; _owner?.NotifyCellClearing(this); }
                else _owner?.UntrackCell(this);
            }
            catch (Exception error) { (errors ??= []).Add(error); }
            try { _elementSession?.Clear(this); } catch (Exception error) { (errors ??= []).Add(error); }
        }
        finally
        {
            try
            {
                // Clearing observers see the old visual and bindings. Detach content afterwards,
                // before nulling model values, so user templates are not rebuilt with a null model.
                Content = null;
                _expanderLayout?.Children.Clear();
                try { ClearEditor(); } catch (Exception error) { (errors ??= []).Add(error); }
                try { _binding?.Dispose(); } catch (Exception error) { (errors ??= []).Add(error); }
                _binding = null;
                _owner = null;
                ApplyGridLines();
                Row = null;
                Column = null;
                ColumnIndex = -1;
                _displayColumn = null;
                ClearValue(ValueProperty);
                DataContext = null;
                if (_display is ContentPresenter presenter) { presenter.ContentTemplate = null; presenter.Content = null; presenter.UpdateChild(); }
                if (_display is TextBlock text) text.Text = null;
            }
            finally { _clearing = false; }
        }
        TreeDataGridEventDispatch.Throw(errors);
    }
    internal double MeasureNaturalWidth()
    {
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return DesiredSize.Width;
    }
    internal void InvalidateRecycledMeasure(bool autoHeight)
    {
        // Invalidating only the ContentControl can leave a valid Grid/Border inside
        // its template returning the previous lifetime's DesiredSize on the first
        // measure. That transient height must never enter the row's maximum cache.
        // Detachment previously invalidated this whole chain, including user content.
        if (autoHeight) InvalidateSubtree(this);
        else InvalidateMeasure();

        static void InvalidateSubtree(Visual visual)
        {
            foreach (var child in visual.GetVisualChildren()) InvalidateSubtree(child);
            if (visual is Control control) control.InvalidateMeasure();
        }
    }
    internal void ApplyGridLines()
    {
        var lines = _owner?.GridLinesVisibility ?? TreeDataGridGridLinesVisibility.None;
        PseudoClasses.Set(":horizontal-grid-lines", lines is TreeDataGridGridLinesVisibility.Horizontal or TreeDataGridGridLinesVisibility.All);
        PseudoClasses.Set(":vertical-grid-lines", lines is TreeDataGridGridLinesVisibility.Vertical or TreeDataGridGridLinesVisibility.All);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            UpdateDisplay();
            if (_prepared && !_clearing) _owner?.ObserveCellValue(this);
        }
    }
    private void EnsureDisplay()
    {
        var kind = _displayColumn!.GetType();
        if (_kind != kind)
        {
            _kind = kind;
            _display = _displayColumn switch
            {
                TreeDataGridCheckBoxColumn => new CheckBox { IsHitTestVisible = false, Focusable = false, HorizontalAlignment = HorizontalAlignment.Left, MinHeight = 0 },
                TreeDataGridTemplateColumn => new ContentPresenter(),
                _ => new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
            };
        }
        if (_display is TextBlock text)
        {
            var textColumn = _displayColumn as TreeDataGridTextColumn;
            text.TextWrapping = textColumn?.TextWrapping ?? TextWrapping.NoWrap;
            text.TextAlignment = textColumn?.TextAlignment ?? TextAlignment.Left;
            text.TextTrimming = textColumn?.TextTrimming ?? TextTrimming.CharacterEllipsis;
        }
        if (_display is CheckBox check) check.IsThreeState = ((TreeDataGridCheckBoxColumn)_displayColumn).IsThreeState;
        if (_display is ContentPresenter presenter) presenter.ContentTemplate = ((TreeDataGridTemplateColumn)_displayColumn).CellTemplate;
    }
    private void UpdateDisplay()
    {
        if (_displayColumn is null || Row is null) return;
        if (_display is TextBlock text)
        {
            var value = _displayColumn is TreeDataGridRowHeaderColumn && _displayColumn.ValueGetter is null ? _rowIndex + 1 : Value;
            text.Text = _displayColumn is TreeDataGridTextColumn { StringFormat: { } format }
                ? string.Format(CultureInfo.CurrentCulture, format.Contains('{') ? format : "{0:" + format + "}", value)
                : value?.ToString();
        }
        else if (_display is CheckBox check) check.IsChecked = Value is bool boolean ? boolean : check.IsThreeState ? null : false;
        else if (_display is ContentPresenter presenter) presenter.Content = Value;
    }
    private Button CreateExpander()
    {
        var button = new Button { Width = 20, MinWidth = 0, MinHeight = 0, Padding = default, Focusable = false };
        button.Classes.Add("tree-data-grid-expander");
        button.Click += ExpandClicked;
        return button;
    }
    private void ExpandClicked(object? sender, RoutedEventArgs e)
    {
        if (_owner?.Source is { } source && Row is { } row && source.TryGetRow(row.Id, out var current) && current is not null)
            source.SetExpanded(row.Id, !current.IsExpanded);
        e.Handled = true;
    }
}
