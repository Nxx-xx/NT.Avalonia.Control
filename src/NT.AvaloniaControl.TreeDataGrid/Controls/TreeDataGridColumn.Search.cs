using Avalonia;

namespace NT.AvaloniaControl.TreeDataGrid;

public abstract partial class TreeDataGridColumn
{
    internal bool TryReadSearchText(object model, out string? text)
    {
        text = null;
        var effective = EffectiveColumn;
        if (!IsTextSearchEnabled || !effective.IsTextSearchEnabled) return false;
        if (effective.TextSearchValueSelector is { } selector)
        {
            text = selector(model);
            if (text is not null || effective is not TreeDataGridTextColumn) return true;
        }
        if (effective is TreeDataGridTemplateColumn { TextSearchBinding: { } binding })
        {
            var sink = new BindingSink { DataContext = model };
            try
            {
                using var subscription = sink.Bind(BindingSink.ResultProperty, binding);
                text = sink.GetValue(BindingSink.ResultProperty)?.ToString();
            }
            finally { sink.DataContext = null; }
            return true;
        }
        if (effective is not TreeDataGridTextColumn) return false;
        text = ReadValue(model)?.ToString();
        return true;
    }
}
