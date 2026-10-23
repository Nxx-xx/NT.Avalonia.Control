namespace NT.AvaloniaControl.VirtualKeyboard;

/// <summary>The built-in ASCII keyboard layouts.</summary>
public enum VirtualKeyboardLayout { Numeric, English }

/// <summary>An application-local text or editing action.</summary>
public enum VirtualKeyboardKeyKind
{
    Character, Shift, CapsLock, Backspace, Delete, Left, Right, Home, End, SelectAll, Return, Tab
}

/// <summary>An immutable key definition, independent of its visual representation.</summary>
public sealed class VirtualKeyboardKey
{
    internal VirtualKeyboardKey(string id, VirtualKeyboardKeyKind kind, string label,
        string? text = null, string? shiftText = null, double width = 1)
    {
        Id = id; Kind = kind; Label = label; Text = text; ShiftText = shiftText;
        ShiftLabel = shiftText ?? label; Width = width;
    }
    public string Id { get; }
    public VirtualKeyboardKeyKind Kind { get; }
    public string Label { get; }
    public string ShiftLabel { get; }
    public string? Text { get; }
    public string? ShiftText { get; }
    public double Width { get; }
    public bool IsRepeatable => Kind is VirtualKeyboardKeyKind.Backspace or VirtualKeyboardKeyKind.Delete
        or VirtualKeyboardKeyKind.Left or VirtualKeyboardKeyKind.Right;
}

/// <summary>A stable ordered row consumed by custom and default templates.</summary>
public sealed class VirtualKeyboardRow
{
    internal VirtualKeyboardRow(params VirtualKeyboardKey[] keys) => Keys = Array.AsReadOnly(keys);
    public IReadOnlyList<VirtualKeyboardKey> Keys { get; }
}

internal static class KeyboardLayouts
{
    internal static IReadOnlyList<VirtualKeyboardRow> Create(VirtualKeyboardLayout layout, bool decimalPoint, bool negativeSign) =>
        layout == VirtualKeyboardLayout.Numeric ? Numeric(decimalPoint, negativeSign) : English;

    private static VirtualKeyboardKey Character(string value, string? shifted = null, double width = 1, string? label = null) =>
        new(value == " " ? "Space" : value, VirtualKeyboardKeyKind.Character, label ?? value, value, shifted, width);
    private static VirtualKeyboardKey Action(VirtualKeyboardKeyKind kind, string? label = null, double width = 1) =>
        new(kind.ToString(), kind, label ?? kind.ToString(), width: width);

    internal static readonly IReadOnlyList<VirtualKeyboardRow> English = Array.AsReadOnly(new[]
    {
        new VirtualKeyboardRow(Character("`", "~"), Character("1", "!"), Character("2", "@"), Character("3", "#"),
            Character("4", "$"), Character("5", "%"), Character("6", "^"), Character("7", "&"), Character("8", "*"),
            Character("9", "("), Character("0", ")"), Character("-", "_"), Character("=", "+"), Action(VirtualKeyboardKeyKind.Backspace, width: 2)),
        new VirtualKeyboardRow(new[] { Action(VirtualKeyboardKeyKind.Tab, width: 1.5) }
            .Concat("qwertyuiop".Select(c => Character(c.ToString(), c.ToString().ToUpperInvariant())))
            .Concat(new[] { Character("[", "{"), Character("]", "}"), Character("\\", "|", 1.5) }).ToArray()),
        new VirtualKeyboardRow(new[] { Action(VirtualKeyboardKeyKind.CapsLock, "Caps Lock", 1.8) }
            .Concat("asdfghjkl".Select(c => Character(c.ToString(), c.ToString().ToUpperInvariant())))
            .Concat(new[] { Character(";", ":"), Character("'", "\""), Action(VirtualKeyboardKeyKind.Return, "Enter", 2.2) }).ToArray()),
        new VirtualKeyboardRow(new[] { Action(VirtualKeyboardKeyKind.Shift, width: 2.5) }
            .Concat("zxcvbnm".Select(c => Character(c.ToString(), c.ToString().ToUpperInvariant())))
            .Concat(new[] { Character(",", "<"), Character(".", ">"), Character("/", "?"), Action(VirtualKeyboardKeyKind.Delete, width: 2.5) }).ToArray()),
        new VirtualKeyboardRow(Action(VirtualKeyboardKeyKind.Home), Action(VirtualKeyboardKeyKind.Left, "←"),
            Character(" ", width: 7, label: "Space"), Action(VirtualKeyboardKeyKind.Right, "→"),
            Action(VirtualKeyboardKeyKind.End), Action(VirtualKeyboardKeyKind.SelectAll, "Select All", 2))
    });

    private static IReadOnlyList<VirtualKeyboardRow> Numeric(bool decimalPoint, bool negativeSign)
    {
        var bottom = new List<VirtualKeyboardKey>();
        if (negativeSign) bottom.Add(Character("-"));
        bottom.Add(Character("0"));
        if (decimalPoint) bottom.Add(Character("."));
        bottom.Add(Action(VirtualKeyboardKeyKind.Return, "Enter"));
        return Array.AsReadOnly(new[]
        {
            new VirtualKeyboardRow(Character("7"), Character("8"), Character("9"), Action(VirtualKeyboardKeyKind.Backspace)),
            new VirtualKeyboardRow(Character("4"), Character("5"), Character("6"), Action(VirtualKeyboardKeyKind.Delete)),
            new VirtualKeyboardRow(Character("1"), Character("2"), Character("3"), Action(VirtualKeyboardKeyKind.Tab)),
            new VirtualKeyboardRow(bottom.ToArray()),
            new VirtualKeyboardRow(Action(VirtualKeyboardKeyKind.Home), Action(VirtualKeyboardKeyKind.Left, "←"),
                Action(VirtualKeyboardKeyKind.Right, "→"), Action(VirtualKeyboardKeyKind.End), Action(VirtualKeyboardKeyKind.SelectAll, "Select All"))
        });
    }
}
