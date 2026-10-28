namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>User gestures that can begin editing. WhenSelected restricts pointer gestures to an already current, selected cell.</summary>
[Flags]
public enum TreeDataGridBeginEditGestures
{
    None = 0,
    F2 = 1,
    Tap = 2,
    DoubleTap = 4,
    WhenSelected = 8,
}

public abstract partial class TreeDataGridColumn
{
    private bool _isTextSearchEnabled = true;
    private TreeDataGridBeginEditGestures? _beginEditGestures;

    /// <summary>Whether this column participates in text search. Both the expander and its inner column must allow it.</summary>
    public bool IsTextSearchEnabled { get => _isTextSearchEnabled; set => Set(ref _isTextSearchEnabled, value); }

    /// <summary>Overrides the inner column or grid editing gestures. Null inherits; None disables user editing gestures.</summary>
    public TreeDataGridBeginEditGestures? BeginEditGestures
    {
        get => _beginEditGestures;
        set
        {
            const TreeDataGridBeginEditGestures valid = TreeDataGridBeginEditGestures.F2 | TreeDataGridBeginEditGestures.Tap
                | TreeDataGridBeginEditGestures.DoubleTap | TreeDataGridBeginEditGestures.WhenSelected;
            if (value is { } gestures && (gestures & ~valid) != 0) throw new ArgumentOutOfRangeException(nameof(value));
            Set(ref _beginEditGestures, value);
        }
    }

    internal TreeDataGridBeginEditGestures GetEditGestures(TreeDataGridEditTriggers inherited) =>
        BeginEditGestures ?? EffectiveColumn.BeginEditGestures ?? (TreeDataGridBeginEditGestures)(inherited &
            (TreeDataGridEditTriggers.F2 | TreeDataGridEditTriggers.SingleClick | TreeDataGridEditTriggers.DoubleClick));
}
