using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace NT.AvaloniaControl.VirtualKeyboard.Themes;

/// <summary>The optional default keyboard templates and light/dark resources.</summary>
public sealed class VirtualKeyboardTheme : Styles
{
    public VirtualKeyboardTheme() => Add(new StyleInclude(new Uri("avares://NT.AvaloniaControl.VirtualKeyboard.Themes/"))
    {
        Source = new Uri("avares://NT.AvaloniaControl.VirtualKeyboard.Themes/Themes/Generic.axaml"),
    });
}
