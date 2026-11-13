using NT.AvaloniaControl.VirtualKeyboard;

namespace VirtualKeyboard.Core.Tests;

public sealed class KeyboardModelTests
{
    [Theory]
    [InlineData(false, false, "a", "1")]
    [InlineData(true, false, "A", "!")]
    [InlineData(false, true, "A", "1")]
    [InlineData(true, true, "a", "!")]
    public void ShiftAndCapsLockHaveDistinctLetterAndPunctuationRules(bool shift, bool caps, string letter, string digit)
    {
        var state = new KeyboardState();
        if (shift) state.ToggleShift();
        if (caps) state.ToggleCapsLock();
        Assert.Equal(letter, state.ResolveText(Find("a")));
        Assert.Equal(digit, state.ResolveText(Find("1")));
    }

    [Fact]
    public void HandlingACharacterClearsOnlyOneShotShift()
    {
        var state = new KeyboardState();
        state.ToggleShift(); state.ToggleCapsLock();
        state.CharacterHandled();
        Assert.False(state.Shift);
        Assert.True(state.CapsLock);
        Assert.Equal("A", state.ResolveText(Find("a")));
        state.Reset();
        Assert.False(state.CapsLock);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void NumericOptionsChangeOnlyOptionalAsciiKeys(bool decimalPoint, bool negativeSign)
    {
        var keys = KeyboardLayouts.Create(VirtualKeyboardLayout.Numeric, decimalPoint, negativeSign).SelectMany(row => row.Keys).ToArray();
        Assert.Equal(decimalPoint, keys.Any(key => key.Text == "."));
        Assert.Equal(negativeSign, keys.Any(key => key.Text == "-"));
        Assert.Equal(10, keys.Count(key => key.Text is { Length: 1 } text && char.IsAsciiDigit(text[0])));
        Assert.Equal(keys.Length, keys.Select(key => key.Id).Distinct().Count());
        Assert.Contains(keys, key => key.Kind == VirtualKeyboardKeyKind.Return);
        Assert.Contains(keys, key => key.Kind == VirtualKeyboardKeyKind.SelectAll);
    }

    [Fact]
    public void EnglishProvidesAllPrintableAsciiCharactersWithStableUniqueKeys()
    {
        var keys = KeyboardLayouts.English.SelectMany(row => row.Keys).ToArray();
        var texts = keys.SelectMany(key => new[] { key.Text, key.ShiftText }).Where(text => text is not null).ToHashSet();
        foreach (var character in Enumerable.Range(32, 95)) Assert.Contains(((char)character).ToString(), texts);
        Assert.Equal(keys.Length, keys.Select(key => key.Id).Distinct().Count());
        Assert.All(keys, key => Assert.True(key.Width > 0 && double.IsFinite(key.Width)));
        Assert.Equal(new[] { VirtualKeyboardKeyKind.Backspace, VirtualKeyboardKeyKind.Delete, VirtualKeyboardKeyKind.Left, VirtualKeyboardKeyKind.Right }.Order(),
            keys.Where(key => key.IsRepeatable).Select(key => key.Kind).Order());
    }

    [Fact]
    public void CoreSourcesDoNotReferenceAvaloniaOrTheDefaultTheme()
    {
        var root = FindRoot();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "NT.AvaloniaControl.VirtualKeyboard", "Core"), "*.cs"))
            Assert.DoesNotContain("using Avalonia", File.ReadAllText(file));
        Assert.DoesNotContain(typeof(VirtualKeyboardKey).Assembly.GetReferencedAssemblies(), assembly => assembly.Name == "NT.AvaloniaControl.VirtualKeyboard.Themes");
    }
    [Theory]
    [InlineData(VirtualKeyboardLayout.Email)]
    [InlineData(VirtualKeyboardLayout.Url)]
    [InlineData(VirtualKeyboardLayout.Phone)]
    public void ConvenienceLayoutsHaveStableUniqueImmutableKeysAndIgnoreNumericOptions(VirtualKeyboardLayout layout)
    {
        var rows = KeyboardLayouts.Create(layout, false, false);
        Assert.Same(rows, KeyboardLayouts.Create(layout, true, true));
        var keys = rows.SelectMany(row => row.Keys).ToArray();
        Assert.Equal(keys.Length, keys.Select(key => key.Id).Distinct().Count());
        Assert.All(keys, key => Assert.True(key.Width > 0 && double.IsFinite(key.Width)));
        Assert.All(keys.Where(key => key.Text is not null), key => Assert.All(key.Text!, c => Assert.InRange((int)c, 32, 126)));
        Assert.Contains(keys, key => key.Kind == VirtualKeyboardKeyKind.Return);
        Assert.Contains(keys, key => key.Kind == VirtualKeyboardKeyKind.Tab);
        Assert.Throws<NotSupportedException>(() => ((IList<VirtualKeyboardKey>)rows[0].Keys).Clear());
    }

    [Theory]
    [InlineData(VirtualKeyboardLayout.Email, "EmailAt", "@")]
    [InlineData(VirtualKeyboardLayout.Email, "EmailCom", ".com")]
    [InlineData(VirtualKeyboardLayout.Url, "UrlScheme", "https://")]
    [InlineData(VirtualKeyboardLayout.Phone, "+", "+")]
    public void ConvenienceKeysDoNotChangeWithShiftOrCaps(VirtualKeyboardLayout layout, string id, string text)
    {
        var key = KeyboardLayouts.Create(layout, false, false).SelectMany(row => row.Keys).Single(key => key.Id == id);
        var state = new KeyboardState();
        Assert.Equal(text, state.ResolveText(key));
        state.ToggleShift(); state.ToggleCapsLock();
        Assert.Equal(text, state.ResolveText(key));
    }

    private static VirtualKeyboardKey Find(string id) => KeyboardLayouts.English.SelectMany(row => row.Keys).Single(key => key.Id == id);
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NT.AvaloniaControl.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
