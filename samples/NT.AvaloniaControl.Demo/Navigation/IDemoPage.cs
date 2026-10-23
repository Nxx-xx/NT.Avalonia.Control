using Avalonia.Controls;

namespace NT.AvaloniaControl.Demo.Navigation;

internal interface IDemoPage : IDisposable
{
    Control View { get; }
    IReadOnlyList<DemoNavigationItem> CreateGallery();
    void Activate();
    void Deactivate();
}
