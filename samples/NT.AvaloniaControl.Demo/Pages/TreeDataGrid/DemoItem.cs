using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

/// <summary>Deterministic notifying data used by both the interactive samples and the smoke probe.</summary>
internal sealed partial class DemoItem(int id, string name) : ObservableObject
{
    [ObservableProperty]
    private string _name = name;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool? _enabled = id % 3 == 0 ? null : id % 3 == 1;

    [ObservableProperty]
    private bool _hasUnloadedChildren;

    public int Id { get; } = id;
    public string Description => Id % 4 == 0
        ? $"节点 {Id:N0}：这是一段用于检查自动行高、中文换行、列宽变化和滚动锚点的较长文本。"
        : $"节点 {Id:N0} 的简短说明。";
    public ObservableCollection<DemoItem> Children { get; } = [];
}
