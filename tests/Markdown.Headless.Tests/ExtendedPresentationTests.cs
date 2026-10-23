using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ExtendedPresentationTests
{
    [Theory]
    [InlineData("NOTE")]
    [InlineData("TIP")]
    [InlineData("IMPORTANT")]
    [InlineData("WARNING")]
    [InlineData("CAUTION")]
    public Task AlertDecorationsKeepTheLogicalTitleAndBodySelectable(string kind) => HeadlessTestScope.Run(() => AlertDecorationsKeepTheLogicalTitleAndBodySelectableCore(kind));

    private async Task AlertDecorationsKeepTheLogicalTitleAndBodySelectableCore(string kind)
    {
        var reader = new Reader { Text = $"> [!{kind}]\n> body **中😀**" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout(); reader.SelectAll();
            Assert.Equal(kind + "\nbody 中😀", reader.SelectedText);
            var icon = reader.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(path => path.Classes.Contains("alerticon"));
            Assert.Contains(kind.ToLowerInvariant(), icon.Classes); Assert.NotNull(icon.Data); Assert.NotNull(icon.Fill);
            Assert.True(icon.Bounds.Width > 0 && icon.Bounds.Height > 0);
            var title = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single(text => text.Classes.Contains("alert-title"));
            Assert.Equal(kind, title.Text); Assert.Equal(kind.Length, title.TextLength);
            Assert.Single(reader.Document.Blocks);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task TightAndLooseListsHaveDifferentNativeSpacingWithoutChangingMarkersOrNestedCode() => HeadlessTestScope.Run(() => TightAndLooseListsHaveDifferentNativeSpacingWithoutChangingMarkersOrNestedCodeCore());

    private async Task TightAndLooseListsHaveDifferentNativeSpacingWithoutChangingMarkersOrNestedCodeCore()
    {
        var tight = new Reader { Text = "- first\n- second" };
        var loose = new Reader { Text = "- first\n\n- second" };
        var window = Show(new StackPanel { Children = { tight, loose } });
        try
        {
            await tight.WhenUpdatedAsync(); await loose.WhenUpdatedAsync(); window.UpdateLayout();
            tight.SelectAll(); loose.SelectAll(); Assert.Equal(tight.SelectedText, loose.SelectedText);
            var tightBlock = tight.GetVisualDescendants().OfType<MarkdownBlockPresenter>().Single(block => block.Classes.Contains("list"));
            var looseBlock = loose.GetVisualDescendants().OfType<MarkdownBlockPresenter>().Single(block => block.Classes.Contains("list"));
            Assert.Contains("tight", tightBlock.Classes); Assert.Contains("loose", looseBlock.Classes);
            Assert.True(looseBlock.Bounds.Height > tightBlock.Bounds.Height);
            Assert.All(tightBlock.GetVisualDescendants().OfType<MarkdownBlockPresenter>().Where(block => block.Classes.Contains("paragraph")), paragraph => Assert.Contains("tight-list", paragraph.Classes));
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task RealizationBoundUsesActualVisibleBoundsAtViewportEdgesIncludingBlockMargins() => HeadlessTestScope.Run(() => RealizationBoundUsesActualVisibleBoundsAtViewportEdgesIncludingBlockMarginsCore());

    private async Task RealizationBoundUsesActualVisibleBoundsAtViewportEdgesIncludingBlockMarginsCore()
    {
        var reader = new Reader { Text = string.Join("\n\n", Enumerable.Range(0, 10_000).Select(i => $"Block {i:D5} 中文 **bold**")) };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            var scroll = reader.GetVisualDescendants().OfType<ScrollViewer>().Single(node => node.Name == "PART_ScrollViewer");
            foreach (var fraction in new[] { 0d, .333, .5, .777, 1 })
            {
                scroll.Offset = new Vector(0, fraction * Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));
                await Flush(window);
                var children = reader.DocumentPresenter!.Children;
                var visible = children.Count(child => child.Bounds.Bottom > scroll.Offset.Y && child.Bounds.Top < scroll.Offset.Y + scroll.Viewport.Height);
                Assert.InRange(reader.DocumentPresenter.RealizedBlockCount, visible, visible + 16);
                Assert.InRange(reader.DocumentPresenter.RetainedBlockCount, 0, 16);
                var first = children.Where(child => child.Bounds.Bottom > scroll.Offset.Y).OrderBy(child => child.Bounds.Y).First();
                var last = children.Where(child => child.Bounds.Top < scroll.Offset.Y + scroll.Viewport.Height).OrderBy(child => child.Bounds.Y).Last();
                Assert.True(first.Bounds.Top <= scroll.Offset.Y + 30);
                Assert.True(last.Bounds.Bottom >= scroll.Offset.Y + scroll.Viewport.Height - 30 || fraction == 1);
            }
            reader.SelectAll(); Assert.Equal(reader.Document.Text, reader.SelectedText);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task RendererChangingTextDuringLayoutCannotPublishAnOldUpdatedEvent() => HeadlessTestScope.Run(() => RendererChangingTextDuringLayoutCannotPublishAnOldUpdatedEventCore());

    private async Task RendererChangingTextDuringLayoutCannotPublishAnOldUpdatedEventCore()
    {
        var reader = new Reader { Text = "old source" }; var events = new List<string>(); var changed = false;
        reader.Updated += (_, _) => events.Add(reader.Document.Source);
        reader.ConfigureParser(new() { Converters = [new MarkdownNodeConverterRegistration<Markdig.Syntax.ParagraphBlock>(_ => new("card"))] });
        reader.NodeRenderers = new([new("card", context =>
        {
            if (!changed) { changed = true; context.Owner.Text = "external replacement"; }
            return context.CreateText();
        })]);
        var window = Show(reader);
        try
        {
            for (var i = 0; i < 100 && reader.Document.Source != "external replacement"; i++) { await Task.Delay(10, TestContext.Current.CancellationToken); window.UpdateLayout(); }
            Assert.Equal("external replacement", reader.Document.Source); Assert.DoesNotContain("old source", events);
            Assert.Contains("external replacement", events);
        }
        finally { window.Close(); }
    }

    private static Window Show(Control reader) { var window = new Window { Width = 600, Height = 320, Content = reader }; window.Show(); return window; }
    private static async Task Flush(Window window) { await Task.Delay(20, TestContext.Current.CancellationToken); await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded); await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded); }
}

