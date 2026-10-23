using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Metadata;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Stable identity of a column, independent of its current position.</summary>
public readonly record struct ColumnId(long Value);

/// <summary>Presentation metadata shared by XAML and code-created columns.</summary>
public abstract partial class TreeDataGridColumn : INotifyPropertyChanged
{
    private static long s_nextId;
    private object? _header;
    private GridLength _width = new(120);
    private double _minWidth = 24;
    private double _maxWidth = double.PositiveInfinity;
    private double _estimatedAutoWidth = 120;
    private Func<object, object?>? _valueGetter;
    private bool _canUserResize = true;
    private bool _isVisible = true;
    private bool _canUserReorder = true;

    public ColumnId Id { get; } = new(Interlocked.Increment(ref s_nextId));
    public object? Header { get => _header; set => Set(ref _header, value); }
    public GridLength Width { get => _width; set => Set(ref _width, value); }
    public double MinWidth
    {
        get => _minWidth;
        set { if (!double.IsFinite(value) || value < 0 || value > MaxWidth) throw new ArgumentOutOfRangeException(nameof(value)); Set(ref _minWidth, value); }
    }
    public double MaxWidth
    {
        get => _maxWidth;
        set { if (double.IsNaN(value) || value < MinWidth) throw new ArgumentOutOfRangeException(nameof(value)); Set(ref _maxWidth, value); }
    }
    /// <summary>Initial width before this column's header or realized cells have been measured.</summary>
    public double EstimatedAutoWidth
    {
        get => _estimatedAutoWidth;
        set { if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Set(ref _estimatedAutoWidth, value); }
    }
    public Func<object, object?>? ValueGetter { get => _valueGetter; set => Set(ref _valueGetter, value); }
    public bool CanUserResize { get => _canUserResize; set => Set(ref _canUserResize, value); }
    /// <summary>Hides presentation while preserving this column's identity, width and collection position.</summary>
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
    public bool CanUserReorder { get => _canUserReorder; set => Set(ref _canUserReorder, value); }
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class TreeDataGridTextColumn : TreeDataGridColumn
{
    private BindingBase? _binding;
    private string? _stringFormat;
    private TextWrapping _textWrapping;
    /// <summary>Optional Avalonia binding. When supplied, takes precedence over ValueGetter.</summary>
    [AssignBinding]
    public BindingBase? Binding { get => _binding; set => Set(ref _binding, value); }
    public string? StringFormat { get => _stringFormat; set => Set(ref _stringFormat, value); }
    public TextWrapping TextWrapping { get => _textWrapping; set => Set(ref _textWrapping, value); }
}

public sealed class TreeDataGridCheckBoxColumn : TreeDataGridColumn
{
    private BindingBase? _binding;
    private bool _isThreeState;
    [AssignBinding]
    public BindingBase? Binding { get => _binding; set => Set(ref _binding, value); }
    public bool IsThreeState { get => _isThreeState; set => Set(ref _isThreeState, value); }
}

public sealed class TreeDataGridTemplateColumn : TreeDataGridColumn
{
    public TreeDataGridTemplateColumn() => CanUserSort = false;
    private IDataTemplate? _cellTemplate;
    private IDataTemplate? _cellEditingTemplate;
    public IDataTemplate? CellTemplate { get => _cellTemplate; set => Set(ref _cellTemplate, value); }
    /// <summary>Reserved editor template contract. Editing gestures and write-back are implemented in P4.</summary>
    public IDataTemplate? CellEditingTemplate { get => _cellEditingTemplate; set => Set(ref _cellEditingTemplate, value); }
}

/// <summary>Displays the one-based current view row number, unless ValueGetter supplies custom text.</summary>
public sealed class TreeDataGridRowHeaderColumn : TreeDataGridColumn
{
    public TreeDataGridRowHeaderColumn() => CanUserSort = false;
}

public sealed class TreeDataGridHierarchicalExpanderColumn : TreeDataGridColumn
{
    private TreeDataGridColumn _innerColumn = new TreeDataGridTextColumn();
    private double _indent = 16;
    [Content]
    public TreeDataGridColumn InnerColumn
    {
        get => _innerColumn;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value is TreeDataGridHierarchicalExpanderColumn) throw new ArgumentException("Nested expander columns are not supported.", nameof(value));
            if (ReferenceEquals(value, _innerColumn)) return;
            Set(ref _innerColumn, value);
        }
    }
    private BindingBase? _childrenBinding;
    private BindingBase? _hasChildrenBinding;
    private BindingBase? _isExpandedBinding;
    /// <summary>Direct model collection property used by the ItemsSource adapter.</summary>
    [AssignBinding]
    public BindingBase? ChildrenBinding { get => _childrenBinding; set { ItemsSourceHierarchyBinding.Validate(value, writable: false); Set(ref _childrenBinding, value); } }
    /// <summary>Optional direct boolean model property advertising lazily loaded children.</summary>
    [AssignBinding]
    public BindingBase? HasChildrenBinding { get => _hasChildrenBinding; set { ItemsSourceHierarchyBinding.Validate(value, writable: false); Set(ref _hasChildrenBinding, value); } }
    /// <summary>Optional direct writable boolean model property synchronized with expansion.</summary>
    [AssignBinding]
    public BindingBase? IsExpandedBinding { get => _isExpandedBinding; set { ItemsSourceHierarchyBinding.Validate(value, writable: true); Set(ref _isExpandedBinding, value); } }
    public double Indent
    {
        get => _indent;
        set { if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Set(ref _indent, value); }
    }
}
