using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TreeDataGrid.Headless.Tests;

public sealed partial class XamlConsumerView : UserControl
{
    public XamlConsumerView() => AvaloniaXamlLoader.Load(this);
}
