using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace NT.AvaloniaControl.TreeDataGrid;

// Store only scalar/string property values, never peers, sources or models.
internal sealed class TreeDataGridAutomationPropertyCache
{
    private readonly Dictionary<AutomationProperty, object?> _values = [];
    internal void Notify(AutomationPeer peer, AutomationProperty property, object? value)
    {
        if (_values.TryGetValue(property, out var previous) && Equals(previous, value)) return;
        _values[property] = value; // Publish before callbacks can re-enter.
        peer.RaisePropertyChangedEvent(property, previous, value);
    }
}
