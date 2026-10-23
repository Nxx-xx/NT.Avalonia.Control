using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace VirtualKeyboard.Themes.Tests;

public sealed partial class UriThemeFixture : Styles
{
    public UriThemeFixture() => AvaloniaXamlLoader.Load(this);
}
