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
    protected override AutomationPeer OnCreateAutomationPeer() => new TreeDataGridCellPresenterAutomationPeer(this);

    internal void Prepare(TreeDataGrid owner, TreeDataGridRow row, int rowIndex, TreeDataGridColumn column, int columnIndex)
    {
        Clear();
        var generation = _generation;
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
    }
    internal void Clear()
    {
        _generation++;
        ClearEditor();
        _binding?.Dispose();
        _binding = null;
        _owner = null;
        ApplyGridLines();
        Row = null;
        Column = null;
        ColumnIndex = -1;
        _displayColumn = null;
        ClearValue(ValueProperty);
        DataContext = null;
        if (_display is ContentPresenter presenter) { presenter.Content = null; presenter.ContentTemplate = null; presenter.UpdateChild(); }
        if (_display is TextBlock text) text.Text = null;
        Content = null;
        _expanderLayout?.Children.Clear();
    }
    internal double MeasureNaturalWidth()
    {
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return DesiredSize.Width;
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
        if (change.Property == ValueProperty) UpdateDisplay();
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
            text.TextWrapping = (_displayColumn as TreeDataGridTextColumn)?.TextWrapping ?? TextWrapping.NoWrap;
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
