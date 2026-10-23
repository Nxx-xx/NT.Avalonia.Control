using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.TreeDataGrid;

[PseudoClasses(":selected", ":current", ":editing", ":invalid")]
public sealed partial class TreeDataGridCellPresenter
{
    private TreeDataGridEditSession? _editorSession;
    private Control? _editor;
    private CompositionClient? _composition;
    private bool _synchronizingEditor;
    public string? ValidationError => _editorSession?.ValidationError;
    internal bool IsComposing => _composition?.IsComposing == true;
    public TreeDataGridCellPresenter() => AddHandler(TextInputMethodClientRequestedEvent, InputClientRequested, RoutingStrategies.Bubble, handledEventsToo: true);

    internal void ApplyInteractionState()
    {
        var owner = _owner; var row = Row; var column = Column;
        if (owner is null || row is null || column is null) return;
        var state = owner.State;
        var address = new CellAddress(row.Id, column.Id);
        PseudoClasses.Set(":selected", owner.SelectionUnit == TreeDataGridSelectionUnit.Row ? state?.IsRowSelected(row.Id) == true : state?.IsCellSelected(address) == true);
        PseudoClasses.Set(":current", state?.CurrentCell == address);
        var session = state?.EditSession is { } edit && edit.Cell == address ? edit : null;
        PseudoClasses.Set(":editing", session is not null);
        PseudoClasses.Set(":invalid", session?.ValidationError is not null);
        ToolTip.SetTip(this, session?.ValidationError);
        if (ReferenceEquals(session, _editorSession))
        {
            if (session is not null)
            {
                _synchronizingEditor = true;
                try
                {
                    if (_editor is TextBox text) text.Text = session.Value?.ToString() ?? "";
                    else if (_editor is CheckBox check) check.IsChecked = session.Value is bool value ? value : null;
                }
                finally { _synchronizingEditor = false; }
            }
            return;
        }
        ClearEditor();
        if (session is null)
        {
            SetInteractionContent(_display);
            return;
        }
        _editorSession = session;
        _editor = column.EffectiveColumn switch
        {
            TreeDataGridCheckBoxColumn check => new CheckBox { Name = "PART_Editor", MinHeight = 0, IsThreeState = check.IsThreeState, IsChecked = session.Value is bool value ? value : null },
            TreeDataGridTemplateColumn { CellEditingTemplate: { } template } => new ContentPresenter { Name = "PART_Editor", Content = session, ContentTemplate = template },
            _ => new TextBox { Name = "PART_Editor", MinHeight = 0, Text = session.Value?.ToString() ?? "" }
        };
        // Keep unthemed editors compact while allowing the cell's editing styles to override it.
        if (_editor is TextBox textEditor)
            textEditor.SetValue(TextBox.PaddingProperty, new Thickness(4, 1), BindingPriority.Style);
        _editor.DataContext = session;
        _editor.PropertyChanged += EditorPropertyChanged;
        SetInteractionContent(_editor);
        PseudoClasses.Set(":editing", true);
        PseudoClasses.Set(":invalid", session.ValidationError is not null);
        ToolTip.SetTip(this, session.ValidationError);
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_editorSession, session) || _editor is null || TopLevel.GetTopLevel(this) is null) return;
            var target = _editor is TextBox or CheckBox ? _editor : _editor.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.Focusable);
            target?.Focus();
        }, DispatcherPriority.Input);
    }
    private void SetInteractionContent(Control? content)
    {
        if (Column is TreeDataGridHierarchicalExpanderColumn && _expanderLayout is { } layout)
        {
            if (layout.Children.Count > 1) layout.Children.RemoveAt(1);
            if (content is not null)
            {
                Grid.SetColumn(content, 1);
                layout.Children.Add(content);
            }
            Content = layout;
        }
        else Content = content;
    }
    private void EditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_synchronizingEditor || _editorSession is not { } edit || !ReferenceEquals(_owner?.State?.EditSession, edit)) return;
        if (sender is TextBox text && e.Property == TextBox.TextProperty) edit.Value = text.Text ?? "";
        else if (sender is CheckBox check && e.Property == CheckBox.IsCheckedProperty) edit.Value = check.IsChecked;
    }
    private void ClearEditor()
    {
        _composition?.Dispose(); _composition = null;
        if (_editor is { } editor)
        {
            editor.PropertyChanged -= EditorPropertyChanged;
            editor.DataContext = null;
            if (editor is ContentPresenter presenter) { presenter.Content = null; presenter.ContentTemplate = null; presenter.UpdateChild(); }
        }
        _editor = null; _editorSession = null;
        PseudoClasses.Set(":editing", false); PseudoClasses.Set(":invalid", false);
        ToolTip.SetTip(this, null);
    }
    private void InputClientRequested(object? sender, TextInputMethodClientRequestedEventArgs e)
    {
        if (_editorSession is null || e.Client is null || e.Client is CompositionClient) return;
        _composition?.Dispose();
        _composition = new CompositionClient(e.Client);
        e.Client = _composition;
    }
    private sealed class CompositionClient : TextInputMethodClient, IDisposable
    {
        private readonly TextInputMethodClient _inner;
        public bool IsComposing { get; private set; }
        public CompositionClient(TextInputMethodClient inner)
        {
            _inner = inner;
            inner.TextViewVisualChanged += ViewChanged; inner.CursorRectangleChanged += CursorChanged;
            inner.SurroundingTextChanged += SurroundingChanged; inner.SelectionChanged += SelectedChanged;
            inner.ResetRequested += Reset; inner.InputPaneActivationRequested += PaneRequested;
        }
        public override Visual TextViewVisual => _inner.TextViewVisual;
        public override bool SupportsPreedit => _inner.SupportsPreedit;
        public override bool SupportsSurroundingText => _inner.SupportsSurroundingText;
        public override string SurroundingText => _inner.SurroundingText;
        public override Rect CursorRectangle => _inner.CursorRectangle;
        public override TextSelection Selection { get => _inner.Selection; set => _inner.Selection = value; }
        public override void SetPreeditText(string? text) { IsComposing = !string.IsNullOrEmpty(text); _inner.SetPreeditText(text); }
        public override void SetPreeditText(string? text, int? cursorPos) { IsComposing = !string.IsNullOrEmpty(text); _inner.SetPreeditText(text, cursorPos); }
        public override void ExecuteContextMenuAction(ContextMenuAction action) => _inner.ExecuteContextMenuAction(action);
        public void Dispose()
        {
            _inner.TextViewVisualChanged -= ViewChanged; _inner.CursorRectangleChanged -= CursorChanged;
            _inner.SurroundingTextChanged -= SurroundingChanged; _inner.SelectionChanged -= SelectedChanged;
            _inner.ResetRequested -= Reset; _inner.InputPaneActivationRequested -= PaneRequested;
            IsComposing = false;
        }
        private void ViewChanged(object? s, EventArgs e) => RaiseTextViewVisualChanged();
        private void CursorChanged(object? s, EventArgs e) => RaiseCursorRectangleChanged();
        private void SurroundingChanged(object? s, EventArgs e) => RaiseSurroundingTextChanged();
        private void SelectedChanged(object? s, EventArgs e) => RaiseSelectionChanged();
        private void Reset(object? s, EventArgs e) { IsComposing = false; RequestReset(); }
        private void PaneRequested(object? s, EventArgs e) => RaiseInputPaneActivationRequested();
    }
}
