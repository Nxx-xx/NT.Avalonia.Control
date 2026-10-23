namespace NT.AvaloniaControl.VirtualKeyboard;

internal sealed class KeyboardState
{
    internal bool Shift { get; private set; }
    internal bool CapsLock { get; private set; }
    internal void ToggleShift() => Shift = !Shift;
    internal void ToggleCapsLock() => CapsLock = !CapsLock;
    internal void Reset() { Shift = false; CapsLock = false; }
    internal void CharacterHandled() => Shift = false;
    internal string ResolveText(VirtualKeyboardKey key)
    {
        var text = key.Text ?? string.Empty;
        var isLetter = text.Length == 1 && text[0] is >= 'a' and <= 'z';
        return (isLetter ? Shift ^ CapsLock : Shift) ? key.ShiftText ?? text : text;
    }
    internal string ResolveLabel(VirtualKeyboardKey key) =>
        key.Kind == VirtualKeyboardKeyKind.Character && key.Text != " " ? ResolveText(key) : key.Label;
}
