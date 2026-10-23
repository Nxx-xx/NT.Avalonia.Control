using Avalonia.Controls;

namespace NT.AvaloniaControl.Demo.Navigation;

/// <summary>A directory or a demo destination. Directories never activate demo content.</summary>
internal sealed class DemoNavigationItem(
    string title,
    string description,
    string preview,
    Func<IReadOnlyList<DemoNavigationItem>>? children = null,
    Func<Control>? open = null)
{
    private IReadOnlyList<DemoNavigationItem>? _children;
    internal string Title { get; } = title;
    internal string Description { get; } = description;
    internal string Preview { get; } = preview;
    internal bool IsDirectory => children is not null;
    internal IReadOnlyList<DemoNavigationItem> Children => _children ??= children?.Invoke() ?? [];
    internal Control Open() => open?.Invoke() ?? throw new InvalidOperationException("Directories have no demo view.");

    internal static DemoNavigationItem Group(string title, string description, string preview, params DemoNavigationItem[] items)
        => new(title, description, preview, () => items);
}
