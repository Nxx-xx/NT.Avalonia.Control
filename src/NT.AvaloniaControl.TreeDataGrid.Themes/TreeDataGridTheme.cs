using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace NT.AvaloniaControl.TreeDataGrid.Themes;

/// <summary>The optional default TreeDataGrid template and light/dark resources.</summary>
public sealed class TreeDataGridTheme : Styles
{
    public TreeDataGridTheme() => Add(new StyleInclude(new Uri("avares://NT.AvaloniaControl.TreeDataGrid.Themes/"))
    {
        Source = new Uri("avares://NT.AvaloniaControl.TreeDataGrid.Themes/Themes/Generic.axaml"),
    });
}
