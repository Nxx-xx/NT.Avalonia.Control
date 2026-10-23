using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace NT.AvaloniaControl.Markdown.Themes;

/// <summary>The optional default reader template, semantic styles and light/dark resources.</summary>
public sealed class MarkdownTheme : Styles
{
    public MarkdownTheme() => Add(new StyleInclude(new Uri("avares://NT.AvaloniaControl.Markdown.Themes/"))
    {
        Source = new Uri("avares://NT.AvaloniaControl.Markdown.Themes/Themes/Generic.axaml")
    });
}
