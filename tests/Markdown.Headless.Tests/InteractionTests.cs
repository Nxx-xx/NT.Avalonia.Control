using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using MarkdownControl = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class InteractionTests
{
    [Fact]
    public Task PointerDragSelectsAcrossParagraphCodeTableAndFinalParagraph() => HeadlessTestScope.Run(() => PointerDragSelectsAcrossParagraphCodeTableAndFinalParagraphCore());

    private async Task PointerDragSelectsAcrossParagraphCodeTableAndFinalParagraphCore()
    {
        var reader = new MarkdownControl { Text = "first paragraph\n\n```text\ncode\tspace  \n```\n\n| A | B |\n| - | - |\n| one | two |\n\nlast paragraph" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            var start = PointAt(window, reader, 0);
            var end = PointAt(window, reader, reader.Document.Text.Length);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            window.MouseUp(end, MouseButton.Left);
            Assert.Equal(reader.Document.Text, reader.SelectedText);
            Assert.Contains("code\tspace  \n", reader.SelectedText);
            Assert.Contains("A\tB\none\ttwo", reader.SelectedText);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task DoubleClickSelectsAWholeWordContainingUnicodeGraphemes() => HeadlessTestScope.Run(() => DoubleClickSelectsAWholeWordContainingUnicodeGraphemesCore());

    private async Task DoubleClickSelectsAWholeWordContainingUnicodeGraphemesCore()
    {
        const string word = "😀e\u0301👨‍👩‍👧‍👦";
        var reader = new MarkdownControl { Text = "before " + word + " after" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            var point = PointAt(window, reader, "before ".Length);
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Assert.Equal(word, reader.SelectedText);
            Assert.Equal(reader.Selection, reader.Selection.Normalize(reader.Document.Text));
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task ShiftArrowExtendsByWholeGraphemesAndKeepsTheAnchor() => HeadlessTestScope.Run(() => ShiftArrowExtendsByWholeGraphemesAndKeepsTheAnchorCore());

    private async Task ShiftArrowExtendsByWholeGraphemesAndKeepsTheAnchorCore()
    {
        var reader = new MarkdownControl { Text = "中😀e\u0301👩‍💻 end" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout(); Assert.True(reader.Focus());
            reader.SetSelection(1, 1);
            Press(window, Key.Right, RawInputModifiers.Shift);
            Assert.Equal(new MarkdownSelection(1, 3), reader.Selection);
            Press(window, Key.Right, RawInputModifiers.Shift);
            Assert.Equal(new MarkdownSelection(1, 5), reader.Selection);
            Assert.Equal("😀e\u0301", reader.SelectedText);
            Press(window, Key.Left, RawInputModifiers.Shift);
            Assert.Equal(new MarkdownSelection(1, 3), reader.Selection);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 6)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 6)]
    public Task PlainArrowCollapsesAnExistingSelectionAtItsDirectionalEdge(bool reverse, bool right, int expected) => HeadlessTestScope.Run(() => PlainArrowCollapsesAnExistingSelectionAtItsDirectionalEdgeCore(reverse, right, expected));

    private async Task PlainArrowCollapsesAnExistingSelectionAtItsDirectionalEdgeCore(bool reverse, bool right, int expected)
    {
        var reader = new MarkdownControl { Text = "a😀bcd efg" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout(); Assert.True(reader.Focus());
            reader.SetSelection(reverse ? 6 : 1, reverse ? 1 : 6);
            Press(window, right ? Key.Right : Key.Left);
            Assert.Equal(new MarkdownSelection(expected, expected), reader.Selection);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task TabAndEnterVisitTheFirstAndSubsequentLinksEvenWhenSelectionIsDisabled(bool selectionEnabled) => HeadlessTestScope.Run(() => TabAndEnterVisitTheFirstAndSubsequentLinksEvenWhenSelectionIsDisabledCore(selectionEnabled));

    private async Task TabAndEnterVisitTheFirstAndSubsequentLinksEvenWhenSelectionIsDisabledCore(bool selectionEnabled)
    {
        var reader = new MarkdownControl { Text = "[first](https://first.example) and [second](https://second.example)", SelectionEnabled = selectionEnabled };
        var window = Show(reader);
        var activated = new List<string>();
        reader.LinkClicked += (_, args) => { activated.Add(args.Url); args.Handled = true; };
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout(); Assert.True(reader.Focus());
            Press(window, Key.Tab); Press(window, Key.Enter);
            Press(window, Key.Tab); Press(window, Key.Enter);
            Assert.Equal(new[] { "https://first.example/", "https://second.example/" }, activated);
            Assert.All(LinkSpans(reader), span => Assert.DoesNotContain("visited", span.Classes));
            Assert.Empty(reader.SelectedText);
            Assert.False(reader.CanCopy);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task DisabledSelectionStillAllowsPointerLinkActivation() => HeadlessTestScope.Run(() => DisabledSelectionStillAllowsPointerLinkActivationCore());

    private async Task DisabledSelectionStillAllowsPointerLinkActivationCore()
    {
        var reader = new MarkdownControl { Text = "prefix [link](https://example.com) suffix", SelectionEnabled = false };
        var window = Show(reader);
        var count = 0;
        reader.LinkClicked += (_, args) => { count++; args.Handled = true; };
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            var point = PointAt(window, reader, "prefix ".Length + 1);
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Assert.Equal(1, count); Assert.Empty(reader.SelectedText); Assert.False(reader.CanCopy);
            Assert.DoesNotContain("visited", Assert.Single(LinkSpans(reader)).Classes);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task PointerOverAndVisitedClassesApplyToActualInlineLinkNode() => HeadlessTestScope.Run(() => PointerOverAndVisitedClassesApplyToActualInlineLinkNodeCore());

    private async Task PointerOverAndVisitedClassesApplyToActualInlineLinkNodeCore()
    {
        var reader = new MarkdownControl { Text = "prefix [link](https://example.com) suffix" };
        var window = Show(reader);
        reader.LinkClicked += (_, args) => args.Handled = false;
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            var span = LinkSpans(reader).Single();
            var point = PointAt(window, reader, "prefix ".Length + 1);
            window.MouseMove(point);
            Assert.Contains("pointerover", span.Classes);
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Assert.Contains("visited", span.Classes);
            window.MouseMove(new Point(2, 2));
            Assert.DoesNotContain("pointerover", span.Classes);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task CopyCodeButtonCopiesOriginalCodeWithoutCapturingOrChangingDocumentSelection() => HeadlessTestScope.Run(() => CopyCodeButtonCopiesOriginalCodeWithoutCapturingOrChangingDocumentSelectionCore());

    private async Task CopyCodeButtonCopiesOriginalCodeWithoutCapturingOrChangingDocumentSelectionCore()
    {
        var reader = new MarkdownControl { Text = "first\n\n```text\ncode\tspace  \n```\n\nlast" };
        var window = Show(reader);
        string? copied = null;
        reader.CopyingToClipboard += (_, args) => { copied = args.Text; args.Cancel = true; };
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout(); reader.SetSelection(0, 3);
            var selection = reader.Selection;
            var button = reader.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "复制代码"));
            var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Assert.Equal("code\tspace  \n", copied);
            Assert.Equal(selection, reader.Selection);
            Assert.Equal("fir", reader.SelectedText);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task EnterOnCodeCopyButtonDoesNotAlsoActivateTheDocumentLink() => HeadlessTestScope.Run(() => EnterOnCodeCopyButtonDoesNotAlsoActivateTheDocumentLinkCore());

    private async Task EnterOnCodeCopyButtonDoesNotAlsoActivateTheDocumentLinkCore()
    {
        var reader = new MarkdownControl { Text = "[link](https://example.com)\n\n```text\ncode\n```" };
        var window = Show(reader);
        var links = 0;
        reader.LinkClicked += (_, _) => links++;
        reader.CopyingToClipboard += (_, args) => args.Cancel = true;
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            var button = reader.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "复制代码"));
            Assert.True(button.Focus()); Press(window, Key.Enter);
            Assert.Equal(0, links);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task TableAlignmentReachesItsRealParagraphTextPresenter() => HeadlessTestScope.Run(() => TableAlignmentReachesItsRealParagraphTextPresenterCore());

    private async Task TableAlignmentReachesItsRealParagraphTextPresenterCore()
    {
        var reader = new MarkdownControl { Text = "| left | center | right |\n| :--- | :---: | ---: |\n| a | b | c |" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            var cells = reader.GetVisualDescendants().OfType<MarkdownBlockPresenter>().Where(block => block.Classes.Contains("tablecell")).ToArray();
            Assert.Equal(6, cells.Length);
            foreach (var cell in cells)
            {
                var model = reader.Document.FindBlock(cell.NodeId)!;
                var expected = model.Metadata["alignment"] switch { "center" => TextAlignment.Center, "right" => TextAlignment.Right, _ => TextAlignment.Left };
                Assert.All(cell.GetVisualDescendants().OfType<MarkdownTextPresenter>(), text => Assert.Equal(expected, text.TextAlignment));
            }
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task EmptyDocumentAcceptsEveryNavigationKeyWithoutThrowing() => HeadlessTestScope.Run(() => EmptyDocumentAcceptsEveryNavigationKeyWithoutThrowingCore());

    private async Task EmptyDocumentAcceptsEveryNavigationKeyWithoutThrowingCore()
    {
        var reader = new MarkdownControl { Text = string.Empty };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout(); Assert.True(reader.Focus());
            foreach (var key in new[] { Key.Left, Key.Right, Key.Up, Key.Down, Key.Home, Key.End, Key.PageUp, Key.PageDown, Key.Tab, Key.Enter }) Press(window, key);
            Assert.Equal(default(MarkdownSelection), reader.Selection);
        }
        finally { window.Close(); }
    }

    private static IEnumerable<MarkdownSpan> LinkSpans(MarkdownControl reader) => reader.GetVisualDescendants().OfType<MarkdownTextPresenter>()
        .SelectMany(text => Flatten(text.Inlines ?? [])).Where(span => span.Classes.Contains("link"));
    private static IEnumerable<MarkdownSpan> Flatten(IEnumerable<Inline> inlines)
    {
        foreach (var inline in inlines.OfType<MarkdownSpan>()) { yield return inline; foreach (var descendant in Flatten(inline.Inlines)) yield return descendant; }
    }
    private static Window Show(Control control) { var window = new Window { Width = 920, Height = 820, Content = control }; window.Show(); window.UpdateLayout(); return window; }
    private static Point PointAt(Window window, MarkdownControl reader, int position)
    {
        var texts = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().ToArray();
        var text = texts.FirstOrDefault(text => text.TextStart <= position && position < text.TextStart + text.TextLength) ??
            texts.Last(text => text.TextStart <= position && position <= text.TextStart + text.TextLength);
        var rect = text.TextLayout.HitTestTextPosition(position - text.TextStart);
        return text.TranslatePoint(new Point(rect.X + text.Padding.Left + .1, rect.Y + rect.Height / 2 + text.Padding.Top), window)!.Value;
    }
    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None) { window.KeyPress(key, modifiers, PhysicalKey.None, null); window.KeyRelease(key, modifiers, PhysicalKey.None, null); window.UpdateLayout(); }
}

