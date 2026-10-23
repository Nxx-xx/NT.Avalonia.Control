using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace NT.AvaloniaControl.VirtualKeyboard;

internal sealed class VirtualKeyboardKeyAutomationPeer(VirtualKeyboardKeyButton owner) : ControlAutomationPeer(owner), IInvokeProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
    protected override string GetClassNameCore() => nameof(VirtualKeyboardKeyButton);
    protected override string? GetNameCore()
    {
        if (base.GetNameCore() is { } explicitName && !string.IsNullOrWhiteSpace(explicitName)) return explicitName;
        return owner.Key?.Kind switch
        {
            VirtualKeyboardKeyKind.Left => "Move left",
            VirtualKeyboardKeyKind.Right => "Move right",
            VirtualKeyboardKeyKind.Home => "Move to beginning",
            VirtualKeyboardKeyKind.End => "Move to end",
            _ => owner.Label
        };
    }
    protected override bool IsEnabledCore() => base.IsEnabledCore() && owner.IsInvocationAvailable;
    public void Invoke()
    {
        EnsureEnabled();
        owner.Invoke();
    }
}
