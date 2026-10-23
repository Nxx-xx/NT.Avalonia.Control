using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace TreeDataGrid.Themes.Tests;

public sealed class UriThemeFixture : Styles
{
    public UriThemeFixture() => AvaloniaXamlLoader.Load(this);
}
