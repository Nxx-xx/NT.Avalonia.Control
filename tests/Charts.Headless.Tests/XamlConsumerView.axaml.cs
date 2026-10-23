using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Charts.Headless.Tests;

public sealed partial class XamlConsumerView : UserControl
{
    public XamlConsumerView() => AvaloniaXamlLoader.Load(this);
}
