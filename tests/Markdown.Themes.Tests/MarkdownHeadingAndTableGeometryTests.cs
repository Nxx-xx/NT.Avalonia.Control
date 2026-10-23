using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using MarkdownControl = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Themes.Tests;

public sealed class MarkdownHeadingAndTableGeometryTests
{
    private static readonly string CombinedGrid = string.Join("\n", new[]
    {
        "+----------+----------+----------+",
        "| left     | middle   | right    |",
        "+==========+==========+==========+",
        "| A        | span     | C        |",
        "+----------+          +----------+",
        "| B        |          | D target |",
        "+----------+----------+----------+",
        "| merged columns".PadRight(33) + "|",
        "+" + new string('-', 32) + "+"
    });

    [Fact]
    public Task AllSixDefaultHeadingLevelsHaveNativeDecreasingGeometryAndKeepSourceAndCopy() =>
        HeadlessTestScope.Run(AllSixDefaultHeadingLevelsHaveNativeDecreasingGeometryAndKeepSourceAndCopyCore);

    private async Task AllSixDefaultHeadingLevelsHaveNativeDecreasingGeometryAndKeepSourceAndCopyCore()
    {
        const string label = "Heading 中文";
        var source = string.Join("\n\n", Enumerable.Range(1, 6).Select(level => new string('#', level) + " " + label));
        var projection = string.Join("\n", Enumerable.Repeat(label, 6));
        var reader = new MarkdownControl { Text = source };
        var window = Show(reader, 800, 700);
        try
        {
            await CompleteLayout(reader, window);
            Assert.Equal(source, reader.Text);
            Assert.Equal(source, reader.Document.Source);
            Assert.Equal(projection, reader.Document.Text);
            Assert.Equal(6, reader.Document.Blocks.Length);
            var sizes = new[] { 32d, 26d, 22d, 19d, 17d, 16d };
            double previousHeight = double.PositiveInfinity;
            double previousTextBoundsHeight = double.PositiveInfinity;
            double previousBottom = double.NegativeInfinity;
            for (var index = 0; index < sizes.Length; index++)
            {
                var block = reader.Document.Blocks[index];
                Assert.Equal(MarkdownBlockKind.Heading, block.Kind);
                Assert.Equal((index + 1).ToString(), block.Metadata["level"]);
                var view = BlockView(reader, block);
                Assert.Contains("h" + (index + 1), view.Classes);
                var text = Assert.Single(view.GetVisualDescendants().OfType<MarkdownTextPresenter>());
                Assert.Equal(sizes[index], text.FontSize);
                Assert.Equal(FontWeight.SemiBold, text.FontWeight);
                Assert.Equal(label, VisibleText(text));
                Assert.Equal(label, reader.Document.Text.Substring(text.TextStart, text.TextLength));
                Assert.True(text.TextLayout.Height > 0 && text.Bounds.Height > 0 && text.Bounds.Width > 0);
                Assert.True(text.TextLayout.Height < previousHeight, "Equal heading text must have decreasing native line height from h1 through h6.");
                Assert.True(text.Bounds.Height < previousTextBoundsHeight, "Arranged heading text height must decrease with the default level size.");
                var origin = Origin(view, reader.DocumentPresenter!);
                Assert.True(origin.Y >= previousBottom - .01, "Heading block geometry must follow document order without overlap.");
                Assert.True(view.Bounds.Height >= text.Bounds.Height);
                previousHeight = text.TextLayout.Height;
                previousTextBoundsHeight = text.Bounds.Height;
                previousBottom = origin.Y + view.Bounds.Height;
            }
            reader.SelectAll();
            Assert.Equal(projection, reader.SelectedText);
            Assert.Equal(source, reader.Document.Source);
            Assert.Empty(reader.Document.Diagnostics);
            Assert.Empty(reader.RenderingDiagnostics);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task CombinedGridSpansKeepActualCellGeometryAndLocalSelectionAfterNarrowViewportScroll() =>
        HeadlessTestScope.Run(CombinedGridSpansKeepActualCellGeometryAndLocalSelectionAfterNarrowViewportScrollCore);

    private async Task CombinedGridSpansKeepActualCellGeometryAndLocalSelectionAfterNarrowViewportScrollCore()
    {
        const string projection = "left\tmiddle\tright\nA\tspan\tC\nB\t\tD target\nmerged columns\t\t";
        var reader = new MarkdownControl { Text = CombinedGrid };
        var window = Show(reader, 280, 460);
        try
        {
            await CompleteLayout(reader, window);
            Assert.Equal(CombinedGrid, reader.Text);
            Assert.Equal(CombinedGrid, reader.Document.Source);
            Assert.Equal(projection, reader.Document.Text);
            var table = Assert.Single(reader.Document.Blocks);
            Assert.Equal(MarkdownBlockKind.Table, table.Kind);
            Assert.Equal("3", table.Metadata["columns"]);
            Assert.Equal(4, table.Children.Length);
            var headers = table.Children[0].Children.Select(block => BlockView(reader, block)).ToArray();
            Assert.Equal(3, headers.Length);
            var spanBlock = table.Children[1].Children.Single(block => block.Metadata["row-span"] == "2");
            Assert.Equal("1", spanBlock.Metadata["column"]);
            var mergedBlock = Assert.Single(table.Children[3].Children);
            Assert.Equal("3", mergedBlock.Metadata["column-span"]);
            var span = BlockView(reader, spanBlock);
            var merged = BlockView(reader, mergedBlock);
            var topLeft = BlockView(reader, table.Children[1].Children.Single(block => block.Metadata["column"] == "0"));
            var topRight = BlockView(reader, table.Children[1].Children.Single(block => block.Metadata["column"] == "2"));
            var bottomLeft = BlockView(reader, table.Children[2].Children.Single(block => block.Metadata["column"] == "0"));
            var bottomRight = BlockView(reader, table.Children[2].Children.Single(block => block.Metadata["column"] == "2"));
            var tableView = BlockView(reader, table);
            var scroll = Assert.Single(tableView.GetVisualDescendants().OfType<ScrollViewer>());
            var grid = Assert.IsAssignableFrom<Grid>(scroll.Content);
            Assert.True(scroll.Viewport.Width > 0 && scroll.Extent.Width > scroll.Viewport.Width);
            Assert.Equal(2, Grid.GetRowSpan(span));
            Assert.Equal(3, Grid.GetColumnSpan(merged));
            Assert.Equal(Origin(headers[0], grid).X, Origin(topLeft, grid).X, 3);
            Assert.Equal(Origin(headers[0], grid).X, Origin(bottomLeft, grid).X, 3);
            Assert.Equal(Origin(headers[1], grid).X, Origin(span, grid).X, 3);
            Assert.Equal(Origin(headers[2], grid).X, Origin(topRight, grid).X, 3);
            Assert.Equal(Origin(topRight, grid).X, Origin(bottomRight, grid).X, 3);
            Assert.Equal(headers[2].Bounds.Width, bottomRight.Bounds.Width, 3);
            Assert.Equal(Origin(topLeft, grid).Y, Origin(span, grid).Y, 3);
            Assert.Equal(Origin(topRight, grid).Y, Origin(span, grid).Y, 3);
            Assert.True(Origin(bottomLeft, grid).Y > Origin(topLeft, grid).Y);
            Assert.Equal(Origin(bottomRight, grid).Y + bottomRight.Bounds.Height, Origin(span, grid).Y + span.Bounds.Height, 3);
            Assert.Equal(Origin(headers[0], grid).X, Origin(merged, grid).X, 3);
            Assert.Equal(headers.Sum(header => header.Bounds.Width), merged.Bounds.Width, 3);
            Assert.True(Origin(merged, grid).Y >= Origin(span, grid).Y + span.Bounds.Height - .01);
            foreach (var text in tableView.GetVisualDescendants().OfType<MarkdownTextPresenter>())
            {
                Assert.Equal(reader.Document.Text.Substring(text.TextStart, text.TextLength), VisibleText(text));
                Assert.True(text.Bounds.Width > 0 && text.Bounds.Height > 0);
            }

            var target = Assert.Single(bottomRight.GetVisualDescendants().OfType<MarkdownTextPresenter>());
            Assert.Equal("D target", VisibleText(target));
            var beforeScroll = Origin(target, window);
            var snapshot = reader.Document;
            scroll.Offset = new Vector(scroll.Extent.Width - scroll.Viewport.Width, 0);
            window.UpdateLayout();
            Assert.True(scroll.Offset.X > 0);
            Assert.Equal(beforeScroll.X - scroll.Offset.X, Origin(target, window).X, 3);
            Assert.Equal(beforeScroll.Y, Origin(target, window).Y, 3);
            Assert.Same(snapshot, reader.Document);
            var start = PointAt(target, window, 2);
            var end = PointAt(target, window, target.TextLength);
            var scrollOrigin = Origin(scroll, window);
            Assert.InRange(start.X, scrollOrigin.X, scrollOrigin.X + scroll.Viewport.Width);
            Assert.InRange(end.X, scrollOrigin.X, scrollOrigin.X + scroll.Viewport.Width);
            RunInput(() => HeadlessWindowExtensions.MouseDown(window, start, MouseButton.Left));
            RunInput(() => HeadlessWindowExtensions.MouseMove(window, end, RawInputModifiers.LeftMouseButton));
            RunInput(() => HeadlessWindowExtensions.MouseUp(window, end, MouseButton.Left));
            Assert.Equal(new MarkdownSelection(target.TextStart + 2, target.TextStart + target.TextLength), reader.Selection);
            Assert.Equal("target", reader.SelectedText);
            var rangeGeometry = target.TextLayout.HitTestTextRange(2, 6).ToArray();
            Assert.NotEmpty(rangeGeometry);
            Assert.All(rangeGeometry, rectangle => Assert.True(rectangle.Width > 0 && rectangle.Height > 0));
            reader.SelectAll();
            Assert.Equal(projection, reader.SelectedText);
            Assert.Equal(CombinedGrid, reader.Document.Source);
            Assert.Empty(reader.Document.Diagnostics);
            Assert.Empty(reader.RenderingDiagnostics);
        }
        finally { window.Close(); }
    }

    private static Window Show(Control reader, double width, double height)
    {
        var window = new Window { Width = width, Height = height, Content = reader };
        window.Show();
        return window;
    }

    private static async Task CompleteLayout(MarkdownControl reader, Window window)
    {
        Assert.Equal(MarkdownUpdateStatus.Completed, (await reader.WhenUpdatedAsync()).Status);
        window.UpdateLayout();
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
    }

    private static MarkdownBlockPresenter BlockView(MarkdownControl reader, MarkdownBlock block) =>
        reader.GetVisualDescendants().OfType<MarkdownBlockPresenter>().Single(view => view.NodeId == block.Id);

    private static Point Origin(Control control, Control relativeTo) => control.TranslatePoint(default, relativeTo)!.Value;

    private static string VisibleText(MarkdownTextPresenter text) => text.Inlines is { Count: > 0 } inlines
        ? string.Concat(inlines.Select(InlineText)) : text.Text ?? string.Empty;

    private static string InlineText(Inline inline) => inline switch
    {
        Run run => run.Text ?? string.Empty,
        Span span => string.Concat(span.Inlines.Select(InlineText)),
        LineBreak => "\n",
        _ => throw new InvalidOperationException("Unexpected inline in the fixed geometry fixture.")
    };

    private static Point PointAt(MarkdownTextPresenter text, Window window, int localPosition)
    {
        var rectangle = text.TextLayout.HitTestTextPosition(localPosition);
        return text.TranslatePoint(new Point(rectangle.X + text.Padding.Left + .1,
            rectangle.Y + rectangle.Height / 2 + text.Padding.Top), window)!.Value;
    }

    private static void RunInput(Action action)
    {
        if (ExecutionContext.Capture() is { } caller)
            ExecutionContext.Run(caller, static callback => ((Action)callback!).Invoke(), action);
        else action();
    }
}
