using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Markdig.Syntax.Inlines;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class LinkFocusPreservationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MT20_FocusedLinkSurvivesAppendOrReconfigurationWithTheSameNativeSemantics(bool configure) => HeadlessTestScope.Run(() => MT20_FocusedLinkSurvivesAppendOrReconfigurationWithTheSameNativeSemanticsCore(configure));

    private async Task MT20_FocusedLinkSurvivesAppendOrReconfigurationWithTheSameNativeSemanticsCore(bool configure)
    {
        var reader = new Reader { Text = "prefix [focused link](https://example.test/kept)\n\ntail" };
        reader.NodeRenderers = new(inlines: [new("host-link", context => context.CreateDefault())]);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var activated = new List<string>();
            reader.LinkClicked += (_, args) => { activated.Add(args.Url); args.Handled = true; };
            reader.Focus(); Press(window, Key.Tab);
            var focused = reader.Selection.Active;
            Assert.Equal("prefix ".Length, focused);
            if (configure)
            {
                reader.ConfigureParser(new()
                { Converters = [new MarkdownNodeConverterRegistration<LinkInline>(_ => new("host-link"))] });
                await reader.WhenUpdatedAsync();
            }
            else
            {
                using var session = reader.BeginStreaming();
                session.Append("\n\nnew tail after focused link");
                Assert.Equal(MarkdownUpdateStatus.Completed, (await session.CompleteAsync()).Status);
            }
            window.UpdateLayout(); Press(window, Key.Enter);
            Assert.Equal(focused, reader.Selection.Active);
            Assert.Equal("https://example.test/kept", Assert.Single(activated));
            Assert.Empty(reader.RenderingDiagnostics);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT20_ConfiguringTheFocusedNodeIntoPlainCustomTextClearsItsOldLinkAction() => HeadlessTestScope.Run(() => MT20_ConfiguringTheFocusedNodeIntoPlainCustomTextClearsItsOldLinkActionCore());

    private async Task MT20_ConfiguringTheFocusedNodeIntoPlainCustomTextClearsItsOldLinkActionCore()
    {
        var reader = new Reader { Text = "prefix [focused link](https://example.test/old)\n\ntail" };
        reader.NodeRenderers = new(inlines: [new("plain", context => context.CreateText())]);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var calls = 0;
            reader.LinkClicked += (_, args) => { calls++; args.Handled = true; };
            reader.Focus(); Press(window, Key.Tab);
            Assert.Equal("prefix ".Length, reader.Selection.Active);
            reader.ConfigureParser(new()
            { Converters = [new MarkdownNodeConverterRegistration<LinkInline>(_ => new("plain", "focused link"))] });
            await reader.WhenUpdatedAsync();
            window.UpdateLayout(); Press(window, Key.Enter);
            Assert.Equal(0, calls);
            Assert.Equal("prefix focused link\ntail", reader.Document.Text);
            Assert.Empty(reader.RenderingDiagnostics);
        }
        finally { window.Close(); }
    }

    private static Window Show(Reader reader)
    { var window = new Window { Width = 600, Height = 280, Content = reader }; window.Show(); return window; }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.UpdateLayout();
    }
}

