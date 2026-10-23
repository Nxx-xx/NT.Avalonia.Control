using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Metadata;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>A themed header. An optional PART_ResizeThumb enables pointer-based column resizing.</summary>
[PseudoClasses(":sortable", ":sort-ascending", ":sort-descending", ":reorder-before", ":reorder-after", ":reorder-dragging")]
public sealed class TreeDataGridColumnHeader : ContentControl
{
    public static readonly DirectProperty<TreeDataGridColumnHeader, TreeDataGridSortDirection> SortDirectionProperty =
        AvaloniaProperty.RegisterDirect<TreeDataGridColumnHeader, TreeDataGridSortDirection>(nameof(SortDirection), header => header.SortDirection);
    public static readonly DirectProperty<TreeDataGridColumnHeader, string> SortIndicatorProperty =
        AvaloniaProperty.RegisterDirect<TreeDataGridColumnHeader, string>(nameof(SortIndicator), header => header.SortIndicator);
    private TreeDataGrid? _owner;
    private Thumb? _thumb;
    private TreeDataGridSortDirection _sortDirection;
    private string _sortIndicator = "";
    private TreeDataGridColumn? _pressedColumn;
    private TreeDataGridSource? _pressedSource;
    private Point _pressPosition;
    private IPointer? _pressedPointer;
    private long _pressOperationVersion;
    private long? _pressSourceVersion;
    static TreeDataGridColumnHeader()
    {
        PaddingProperty.OverrideDefaultValue<TreeDataGridColumnHeader>(new Thickness(8, 3));
        FocusableProperty.OverrideDefaultValue<TreeDataGridColumnHeader>(true);
        TemplateProperty.OverrideDefaultValue<TreeDataGridColumnHeader>(new FuncControlTemplate<TreeDataGridColumnHeader>((_, scope) =>
        {
            var thumb = new Thumb { Name = "PART_ResizeThumb", Width = 8, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Right, Cursor = new Cursor(StandardCursorType.SizeWestEast) };
            thumb.Template = new FuncControlTemplate<Thumb>((_, _) => new Border { Background = Brushes.Transparent });
            scope.Register("PART_ResizeThumb", thumb);
            return new Grid { Background = Brushes.Transparent, Children = { new ContentPresenter { Margin = new Thickness(0, 0, 22, 0), [!ContentPresenter.ContentProperty] = new TemplateBinding(ContentProperty),
                [!ContentPresenter.PaddingProperty] = new TemplateBinding(PaddingProperty) },
                new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0), IsHitTestVisible = false, [!TextBlock.TextProperty] = new TemplateBinding(SortIndicatorProperty) }, thumb } };
        }));
    }
    public TreeDataGridColumn? Column { get; private set; }
    public TreeDataGridSortDirection SortDirection => _sortDirection;
    public string SortIndicator => _sortIndicator;
    internal TreeDataGrid? GridOwner => _owner;
    protected override AutomationPeer OnCreateAutomationPeer() => new TreeDataGridColumnHeaderAutomationPeer(this);
    internal void Prepare(TreeDataGrid owner, TreeDataGridColumn column)
    {
        if (!ReferenceEquals(_owner, owner) || !ReferenceEquals(Column, column)) ClearPress();
        _owner = owner;
        Column = column;
        Content = column.Header;
        Theme = owner.ColumnHeaderTheme;
        UpdateThumb();
        UpdateSortState();
    }
    internal void Clear()
    {
        ClearPress();
        _owner = null; Column = null; Content = null;
        SetReorderIndicator(false, false, false);
        UpdateThumb(); UpdateSortState();
    }
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ClearPress();
        if (_thumb is not null) _thumb.DragDelta -= Resize;
        base.OnApplyTemplate(e);
        _thumb = e.NameScope.Find<Thumb>("PART_ResizeThumb");
        if (_thumb is not null) _thumb.DragDelta += Resize;
        UpdateThumb();
    }
    private void UpdateThumb() { if (_thumb is not null) _thumb.IsVisible = _owner?.CanUserResizeColumns == true && Column?.CanUserResize == true; }
    private void UpdateSortState()
    {
        var direction = Column is { } column && _owner?.Source is { } source && source.SortColumn == column.Id
            ? source.SortDirection : TreeDataGridSortDirection.None;
        SetAndRaise(SortDirectionProperty, ref _sortDirection, direction);
        SetAndRaise(SortIndicatorProperty, ref _sortIndicator, direction switch
        { TreeDataGridSortDirection.Ascending => "▲", TreeDataGridSortDirection.Descending => "▼", _ => "" });
        PseudoClasses.Set(":sortable", _owner?.CanUserSortColumns == true && Column?.CanUserSort == true);
        PseudoClasses.Set(":sort-ascending", direction == TreeDataGridSortDirection.Ascending);
        PseudoClasses.Set(":sort-descending", direction == TreeDataGridSortDirection.Descending);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _owner is not { } owner || Column is not { } column ||
            !(owner.CanUserSortColumns && column.CanUserSort || owner.CanUserReorderColumns && column.CanUserReorder)) return;
        if (e.Source is Visual visual && (ReferenceEquals(visual, _thumb) || visual.GetVisualAncestors().Any(parent => ReferenceEquals(parent, _thumb)))) return;
        _pressedColumn = Column;
        _pressedSource = _owner.Source;
        _pressOperationVersion = owner.ColumnOperationVersion;
        _pressSourceVersion = owner.Source?.Version;
        _pressPosition = e.GetPosition(this);
        _pressedPointer = e.Pointer;
        e.Pointer.Capture(this);
        Focus();
        e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressedColumn is not { } column || _owner is not { } owner || !ReferenceEquals(e.Pointer, _pressedPointer)) return;
        if (!ReferenceEquals(Column, column) || !ReferenceEquals(owner.Source, _pressedSource) || owner.Source?.Version != _pressSourceVersion || owner.ColumnOperationVersion != _pressOperationVersion)
        { ClearPress(); return; }
        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _pressPosition.X) < 4 && Math.Abs(position.Y - _pressPosition.Y) < 4) return;
        if (owner.BeginColumnReorder(column, e.Pointer, e.GetPosition(owner))) e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var column = _pressedColumn;
        var source = _pressedSource;
        var position = e.GetPosition(this);
        var click = column is not null && ReferenceEquals(Column, column) && ReferenceEquals(_owner?.Source, source)
            && _owner?.Source?.Version == _pressSourceVersion && _owner?.ColumnOperationVersion == _pressOperationVersion
            && _owner?.CanUserSortColumns == true && column.CanUserSort
            && new Rect(Bounds.Size).Contains(position) && Math.Abs(position.X - _pressPosition.X) < 4 && Math.Abs(position.Y - _pressPosition.Y) < 4;
        ClearPress();
        if (click) { _owner?.ToggleColumnSort(column!); e.Handled = true; }
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressedColumn = null; _pressedSource = null; _pressedPointer = null;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (e.Key == Key.Escape)
        {
            var active = _pressedPointer is not null || _owner?.IsColumnReordering == true;
            ClearPress();
            _owner?.CancelColumnReorder();
            if (active) e.Handled = true;
            return;
        }
        if (e.Key is Key.Enter or Key.Space && _owner?.CanUserSortColumns == true && Column is { CanUserSort: true } column)
        { _owner.ToggleColumnSort(column); e.Handled = true; }
    }
    private void ClearPress()
    {
        var pointer = _pressedPointer;
        _pressedPointer = null; _pressedColumn = null; _pressedSource = null;
        if (ReferenceEquals(pointer?.Captured, this)) pointer.Capture(null);
    }
    internal void SetReorderIndicator(bool before, bool after, bool dragging)
    {
        PseudoClasses.Set(":reorder-before", before);
        PseudoClasses.Set(":reorder-after", after);
        PseudoClasses.Set(":reorder-dragging", dragging);
    }
    private void Resize(object? sender, VectorEventArgs e)
    {
        if (_owner?.CanUserResizeColumns != true || Column is not { CanUserResize: true } column) return;
        var current = column.Width.IsAbsolute ? column.Width.Value : Bounds.Width;
        var width = Math.Clamp(current + e.Vector.X, column.MinWidth, column.MaxWidth);
        column.Width = new GridLength(width);
        e.Handled = true;
    }
}
