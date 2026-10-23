using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;
using Inline = Avalonia.Controls.Documents.Inline;

namespace Markdown.Headless.Tests;

public sealed class CustomContainerRendererTests
{
    private const string PipeTable = "| A | Longer B |\n| :--- | ---: |\n| one | two |";
    private const string RowSpanTable = "+----------+----------+\n| left     | right    |\n+==========+==========+\n| span     | A        |\n+          +----------+\n|          | B        |\n+----------+----------+";
    private const string ColumnSpanTable = "+---------+---------+\n| A       | B       |\n+=========+=========+\n| one     | two     |\n+---------+---------+\n| merged            |\n+-------------------+";

    [Theory]
    [InlineData("table")]
    [InlineData("code")]
    public Task NativeTableAndCodeDefaultsKeepMappedTextInsideTemplateControls(string kind) => HeadlessTestScope.Run(() => NativeTableAndCodeDefaultsKeepMappedTextInsideTemplateControlsCore(kind));

    private async Task NativeTableAndCodeDefaultsKeepMappedTextInsideTemplateControlsCore(string kind)
    {
        var calls = 0;
        Control? renderedRoot = null;
        var prepared = new Dictionary<StyledElement, int>();
        var source = kind == "table" ? PipeTable : "```cs\n  code\t \n```";
        MarkdownNodeConverterRegistration converter = kind == "table"
            ? new MarkdownNodeConverterRegistration<Table>(_ => new("native"))
            : new MarkdownNodeConverterRegistration<FencedCodeBlock>(_ => new("native"));
        var reader = CreateReader(source, converter);
        reader.ElementPrepared += (_, args) => prepared[args.Element] = prepared.GetValueOrDefault(args.Element) + 1;
        reader.NodeRenderers = new([new("native", context => { calls++; return renderedRoot = context.CreateDefault(); })]);
        var window = Show(reader);
        try
        {
            await CompleteLayout(reader, window);
            Assert.Equal(1, calls);
            Assert.NotNull(renderedRoot);
            Assert.Equal(1, prepared[renderedRoot]);
            Assert.Empty(reader.RenderingDiagnostics);
            reader.SelectAll();
            Assert.Equal(kind == "table" ? "A\tLonger B\none\ttwo" : "  code\t \n", reader.SelectedText);
            var texts = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().ToArray();
            Assert.Equal(kind == "table" ? 4 : 1, texts.Length);
            Assert.All(texts, text => AssertMappedText(reader, text));
            Assert.Contains(reader.GetVisualDescendants().OfType<MarkdownBlockPresenter>(), block => block.Classes.Contains(kind));
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task AContentControlWrapperCanPresentAContextTextHelperThroughItsTemplate() => HeadlessTestScope.Run(() => AContentControlWrapperCanPresentAContextTextHelperThroughItsTemplateCore());

    private async Task AContentControlWrapperCanPresentAContextTextHelperThroughItsTemplateCore()
    {
        var reader = CreateReader("wrapped 中😀", new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new("wrapper")));
        reader.NodeRenderers = new([new("wrapper", context => new ContentControl { Content = context.CreateText() })]);
        var window = Show(reader);
        try
        {
            await CompleteLayout(reader, window);
            Assert.Empty(reader.RenderingDiagnostics);
            var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            AssertMappedText(reader, text);
            Drag(window, text, 0, text.TextLength);
            Assert.Equal("wrapped 中😀", reader.SelectedText);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task ReplacedTableRowsInvokeTheirRendererAndKeepCopyAndPointerGeometry() => HeadlessTestScope.Run(() => ReplacedTableRowsInvokeTheirRendererAndKeepCopyAndPointerGeometryCore());

    private async Task ReplacedTableRowsInvokeTheirRendererAndKeepCopyAndPointerGeometryCore()
    {
        var calls = 0;
        var reader = CreateReader(PipeTable, new MarkdownNodeConverterRegistration<TableRow>(_ => new("row", "ROW 中😀")));
        reader.NodeRenderers = new([new("row", context => { calls++; return new Border { Child = context.CreateText() }; })]);
        var window = Show(reader);
        try
        {
            await CompleteLayout(reader, window);
            Assert.Equal(2, calls);
            Assert.Empty(reader.RenderingDiagnostics);
            Assert.Equal("ROW 中😀\nROW 中😀", reader.Document.Text);
            var rows = reader.Document.Blocks[0].Children;
            var texts = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().OrderBy(text => text.TextStart).ToArray();
            Assert.Equal(2, texts.Length);
            for (var index = 0; index < rows.Length; index++)
            {
                Assert.Equal(rows[index].TextStart, texts[index].TextStart);
                Assert.Equal(rows[index].TextLength, texts[index].TextLength);
                AssertMappedText(reader, texts[index]);
            }
            var firstOrigin = Origin(texts[0], reader.DocumentPresenter!);
            var secondOrigin = Origin(texts[1], reader.DocumentPresenter!);
            Assert.True(secondOrigin.Y > firstOrigin.Y);
            window.MouseDown(PointAt(texts[0], window, 0), MouseButton.Left);
            window.MouseMove(PointAt(texts[1], window, texts[1].TextLength), RawInputModifiers.LeftMouseButton);
            window.MouseUp(PointAt(texts[1], window, texts[1].TextLength), MouseButton.Left);
            Assert.Equal(reader.Document.Text, reader.SelectedText);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MissingTableRowRendererReportsTheFallbackAndDisplaysItsCopyProjection() => HeadlessTestScope.Run(() => MissingTableRowRendererReportsTheFallbackAndDisplaysItsCopyProjectionCore());

    private async Task MissingTableRowRendererReportsTheFallbackAndDisplaysItsCopyProjectionCore()
    {
        var reader = CreateReader(PipeTable, new MarkdownNodeConverterRegistration<TableRow>(_ => new("missing.row", "row fallback")));
        var window = Show(reader);
        try
        {
            await CompleteLayout(reader, window);
            Assert.Equal(2, reader.RenderingDiagnostics.Count);
            Assert.All(reader.RenderingDiagnostics, diagnostic => Assert.Equal("MDRENDER", diagnostic.Code));
            var texts = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().ToArray();
            Assert.Equal(2, texts.Length);
            Assert.All(texts, text =>
            {
                Assert.Equal("row fallback", Text(text));
                AssertMappedText(reader, text);
            });
            reader.SelectAll();
            Assert.Equal("row fallback\nrow fallback", reader.SelectedText);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task NullTextRowDefaultsRetainSharedColumnWidthsOrderingAndAlignment() => HeadlessTestScope.Run(() => NullTextRowDefaultsRetainSharedColumnWidthsOrderingAndAlignmentCore());

    private async Task NullTextRowDefaultsRetainSharedColumnWidthsOrderingAndAlignmentCore()
    {
        var calls = 0;
        var reader = CreateReader(PipeTable, new MarkdownNodeConverterRegistration<TableRow>(_ => new("row")));
        reader.NodeRenderers = new([new("row", context => { calls++; return context.CreateDefault(); })]);
        var window = Show(reader);
        try
        {
            await CompleteLayout(reader, window);
            Assert.Equal(2, calls);
            Assert.Empty(reader.RenderingDiagnostics);
            var rows = reader.Document.Blocks[0].Children;
            var presenter = reader.DocumentPresenter!;
            var cells = rows.Select(row => row.Children.Select(cell => Cell(reader, cell)).ToArray()).ToArray();
            Assert.True(Origin(cells[1][0], presenter).Y > Origin(cells[0][0], presenter).Y);
            for (var column = 0; column < 2; column++)
            {
                Assert.Equal(Origin(cells[0][column], presenter).X, Origin(cells[1][column], presenter).X, 3);
                Assert.Equal(cells[0][column].Bounds.Width, cells[1][column].Bounds.Width, 3);
                Assert.True(cells[0][column].Bounds.Width >= 140);
            }
            Assert.All(cells.SelectMany(row => row), cell =>
            {
                var text = cell.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
                AssertMappedText(reader, text);
                Assert.Equal(cell.NodeId == rows[0].Children[1].Id || cell.NodeId == rows[1].Children[1].Id
                    ? Avalonia.Media.TextAlignment.Right : Avalonia.Media.TextAlignment.Left, text.TextAlignment);
            });
            reader.SelectAll(); Assert.Equal("A\tLonger B\none\ttwo", reader.SelectedText);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullTextCustomRowsPreserveGridRowSpansAndGlobalSelectionGeometry(bool allRowsCustom) => HeadlessTestScope.Run(() => NullTextCustomRowsPreserveGridRowSpansAndGlobalSelectionGeometryCore(allRowsCustom));

    private async Task NullTextCustomRowsPreserveGridRowSpansAndGlobalSelectionGeometryCore(bool allRowsCustom)
    {
        var calls = 0;
        var reader = CreateReader(RowSpanTable, new MarkdownNodeConverterRegistration<TableRow>(context =>
            allRowsCustom || context.Node.OfType<TableCell>().Any(cell => cell.RowSpan > 1) ? new("row") : null));
        reader.NodeRenderers = new([new("row", context => { calls++; return context.CreateDefault(); })]);
        var window = Show(reader);
        try
        {
            await CompleteLayout(reader, window);
            Assert.Equal(allRowsCustom ? 3 : 1, calls);
            Assert.Empty(reader.RenderingDiagnostics);
            var rows = reader.Document.Blocks[0].Children;
            Assert.Equal("left\tright\nspan\tA\n\tB", reader.Document.Text);
            var span = Cell(reader, rows[1].Children[0]);
            var topRight = Cell(reader, rows[1].Children[1]);
            var bottomRight = Cell(reader, rows[2].Children[0]);
            var presenter = reader.DocumentPresenter!;
            var spanOrigin = Origin(span, presenter);
            var topOrigin = Origin(topRight, presenter);
            var bottomOrigin = Origin(bottomRight, presenter);
            Assert.Equal(spanOrigin.Y, topOrigin.Y, 3);
            Assert.Equal(topOrigin.X, bottomOrigin.X, 3);
            Assert.True(bottomOrigin.Y > topOrigin.Y);
            Assert.True(span.Bounds.Height > topRight.Bounds.Height + 5);
            Assert.Equal(bottomOrigin.Y + bottomRight.Bounds.Height, spanOrigin.Y + span.Bounds.Height, 3);
            var texts = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().ToArray();
            Assert.Equal(5, texts.Length);
            Assert.All(texts, text => AssertMappedText(reader, text));
            var first = texts.Single(text => text.TextStart == 0);
            var last = texts.Single(text => Text(text) == "B");
            window.MouseDown(PointAt(first, window, 0), MouseButton.Left);
            window.MouseMove(PointAt(last, window, last.TextLength), RawInputModifiers.LeftMouseButton);
            window.MouseUp(PointAt(last, window, last.TextLength), MouseButton.Left);
            Assert.Equal(reader.Document.Text, reader.SelectedText);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData("row", false)]
    [InlineData("row", true)]
    [InlineData("column", false)]
    [InlineData("column", true)]
    public Task ReplacedCellContentRetainsParentSpansWithNativeOrCustomRows(string axis, bool customRows) => HeadlessTestScope.Run(() => ReplacedCellContentRetainsParentSpansWithNativeOrCustomRowsCore(axis, customRows));

    private async Task ReplacedCellContentRetainsParentSpansWithNativeOrCustomRowsCore(string axis, bool customRows)
    {
        var cellCalls = 0;
        var rowCalls = 0;
        var reader = new Reader { Text = axis == "row" ? RowSpanTable : ColumnSpanTable };
        var converters = new List<MarkdownNodeConverterRegistration>
        {
            new MarkdownNodeConverterRegistration<TableCell>(context => context.Node.ColumnSpan > 1 || context.Node.RowSpan > 1
                ? new("cell", "replaced") { Metadata = ImmutableDictionary<string, string>.Empty.Add("column-span", "99").Add("row-span", "99") }
                : null)
        };
        if (customRows) converters.Add(new MarkdownNodeConverterRegistration<TableRow>(_ => new("row")));
        reader.ConfigureParser(new() { Converters = [.. converters] });
        reader.NodeRenderers = new([new("cell", context => { cellCalls++; return context.CreateDefault(); }),
            new("row", context => { rowCalls++; return context.CreateDefault(); })]);
        var window = Show(reader);
        try
        {
            await CompleteLayout(reader, window);
            Assert.Equal(1, cellCalls);
            Assert.Equal(customRows ? 3 : 0, rowCalls);
            Assert.Empty(reader.RenderingDiagnostics);
            var rows = reader.Document.Blocks[0].Children;
            var replaced = reader.Document.Descendants().Single(block => block.Metadata.GetValueOrDefault(MarkdownCustomNodeMetadata.RendererKey) == "cell");
            Assert.Equal("2", replaced.Metadata[axis + "-span"]);
            Assert.False(replaced.Metadata.ContainsKey(MarkdownCustomNodeMetadata.NativeKind));
            var view = Cell(reader, replaced);
            var presenter = reader.DocumentPresenter!;
            if (axis == "row")
            {
                var topRight = Cell(reader, rows[1].Children[1]);
                var bottomRight = Cell(reader, rows[2].Children[0]);
                Assert.Equal(Origin(topRight, presenter).Y, Origin(view, presenter).Y, 3);
                Assert.True(view.Bounds.Height > topRight.Bounds.Height + 5);
                Assert.Equal(Origin(bottomRight, presenter).Y + bottomRight.Bounds.Height, Origin(view, presenter).Y + view.Bounds.Height, 3);
            }
            else
            {
                var left = Cell(reader, rows[0].Children[0]);
                var right = Cell(reader, rows[0].Children[1]);
                Assert.Equal(Origin(left, presenter).X, Origin(view, presenter).X, 3);
                Assert.Equal(left.Bounds.Width + right.Bounds.Width, view.Bounds.Width, 3);
            }
            var text = view.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            AssertMappedText(reader, text);
            Drag(window, text, 0, text.TextLength);
            Assert.Equal("replaced", reader.SelectedText);
            reader.SelectAll();
            Assert.Equal(axis == "row" ? "left\tright\nreplaced\tA\n\tB" : "A\tB\none\ttwo\nreplaced\t", reader.SelectedText);
        }
        finally { window.Close(); }
    }

    private static Reader CreateReader(string source, MarkdownNodeConverterRegistration converter)
    {
        var reader = new Reader { Text = source };
        reader.ConfigureParser(new() { Converters = [converter] });
        return reader;
    }
    private static Window Show(Control reader) { var window = new Window { Width = 700, Height = 420, Content = reader }; window.Show(); return window; }
    private static async Task CompleteLayout(Reader reader, Window window)
    {
        Assert.Equal(MarkdownUpdateStatus.Completed, (await reader.WhenUpdatedAsync()).Status);
        window.UpdateLayout();
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
    }
    private static MarkdownBlockPresenter Cell(Reader reader, MarkdownBlock cell) => reader.GetVisualDescendants().OfType<MarkdownBlockPresenter>().Single(control => control.NodeId == cell.Id);
    private static Point Origin(Control control, Control relativeTo) => control.TranslatePoint(default, relativeTo)!.Value;
    private static string Text(MarkdownTextPresenter text) => text.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.Select(InlineText)) : text.Text ?? string.Empty;
    private static string InlineText(Inline inline) => inline switch { Run run => run.Text ?? string.Empty, Span span => string.Concat(span.Inlines.Select(InlineText)), LineBreak => "\n", _ => throw new InvalidOperationException("Unexpected inline in the fixture.") };
    private static void AssertMappedText(Reader reader, MarkdownTextPresenter text)
    {
        Assert.Equal(reader.Document.Text.Substring(text.TextStart, text.TextLength), Text(text));
        Assert.True(text.Bounds.Width > 0 && text.Bounds.Height > 0);
    }
    private static Point PointAt(MarkdownTextPresenter text, Window window, int position)
    {
        var rectangle = text.TextLayout.HitTestTextPosition(position);
        return text.TranslatePoint(new Point(rectangle.X + text.Padding.Left + .1, rectangle.Y + rectangle.Height / 2 + text.Padding.Top), window)!.Value;
    }
    private static void Drag(Window window, MarkdownTextPresenter text, int start, int end)
    {
        window.MouseDown(PointAt(text, window, start), MouseButton.Left);
        window.MouseMove(PointAt(text, window, end), RawInputModifiers.LeftMouseButton);
        window.MouseUp(PointAt(text, window, end), MouseButton.Left);
    }
}

