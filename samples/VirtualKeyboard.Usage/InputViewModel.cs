using CommunityToolkit.Mvvm.ComponentModel;

namespace VirtualKeyboard.Usage;

public sealed class InputViewModel : ObservableObject
{
    private string? _text = "Hello";
    public string? Text { get => _text; set => SetProperty(ref _text, value); }
}
