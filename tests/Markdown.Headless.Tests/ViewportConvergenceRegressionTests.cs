using System.Collections.Specialized;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using ParagraphBlock = Markdig.Syntax.ParagraphBlock;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

// Private preparation only. Compile once, then copy the complete test output
// into independent before/after hosts and replace only the product DLLs.
public sealed class ViewportConvergenceRegressionTests
{
    [Fact]
    public Task ATallFirstCodeBlockDoesNotConstructRootsWhollyBelowItsActualViewport() =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = new Reader { Text = string.Empty, FontSize = 14 };
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                using var created = new RootCreations(reader.DocumentPresenter!);
                reader.Text = Tall("code") + "\n\n" + Paragraphs(120);
                await Updated(reader, window);
                var presenter = reader.DocumentPresenter!;
                var first = Assert.Single(presenter.Children.OfType<MarkdownBlockPresenter>());
                Assert.Equal(reader.Document.Blocks[0].Id, first.NodeId);
                Assert.True(first.Bounds.Height > Scroll(reader).Viewport.Height + 100);
                Observe("cold-tall-code", reader, created);
                AssertViewportCovered(reader);
                AssertTextProjection(reader);
                // Natural geometry above proves every later root is below the
                // viewport. Code has no ordinary overscan. The old algorithm
                // creates estimated successors before it learns this height.
                Assert.Single(created.Added);
                Assert.Same(first, created.Added[0]);
            }
            finally { window.Close(); }
        });

    [Theory]
    [InlineData("code")]
    [InlineData("paragraph")]
    [InlineData("list")]
    [InlineData("table")]
    public Task TallNaturalRootsCoverTheColdViewportAndKeepTheirCompleteText(string kind) =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = new Reader { Text = string.Empty, FontSize = 14 };
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                using var created = new RootCreations(reader.DocumentPresenter!);
                reader.Text = Tall(kind) + "\n\n" + Paragraphs(80);
                await Updated(reader, window);
                var first = reader.DocumentPresenter!.Children.OfType<MarkdownBlockPresenter>()
                    .Single(root => root.NodeId == reader.Document.Blocks[0].Id);
                Assert.True(first.Bounds.Height > Scroll(reader).Viewport.Height + 100);
                AssertViewportCovered(reader);
                AssertTextProjection(reader);
                var expected = new MarkdownParser().Parse(reader.Text);
                reader.SelectAll();
                Assert.Equal(expected.Text, reader.SelectedText);
                Assert.Equal(expected.Diagnostics.ToArray(), reader.Document.Diagnostics.ToArray());
                Observe("cold-" + kind, reader, created);
            }
            finally { window.Close(); }
        });

    [Fact]
    public Task MiddleJumpsAndNarrowWideReflowsCoverTheViewportAndPreserveItsReadingGrapheme() =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = new Reader { Text = Paragraphs(180, 18), FontSize = 14 };
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                var scroll = Scroll(reader);
                foreach (var fraction in new[] { .23, .68, .41 })
                {
                    scroll.Offset = new Vector(0, fraction * (scroll.Extent.Height - scroll.Viewport.Height));
                    window.UpdateLayout();
                    // Check the first synchronous layout, not only a later drain.
                    AssertViewportCovered(reader);
                    AssertTextProjection(reader);
                }
                await Flush(window);
                var reading = FirstVisibleGrapheme(reader);
                var document = reader.Document;
                window.Width = 310;
                await Flush(window);
                AssertViewportCovered(reader);
                AssertReadingGrapheme(reader, reading);
                var beforeWiden = FirstVisibleGrapheme(reader);
                window.Width = 640;
                await Flush(window);
                AssertViewportCovered(reader);
                AssertReadingGrapheme(reader, beforeWiden);
                Assert.Same(document, reader.Document);
                AssertTextProjection(reader);
            }
            finally { window.Close(); }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FollowingCommitsExposeTheActualTallTailInsideUpdated(bool streaming) =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = new Reader { Text = Paragraphs(60), AutoScrollToEnd = true };
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                reader.ScrollToEnd();
                await Flush(window);
                var next = reader.Text + "\n\n" + Tall("code");
                var observed = false;
                reader.Updated += (_, _) =>
                {
                    if (reader.Document.Source != next) return;
                    observed = true;
                    AssertTailVisible(reader);
                    AssertViewportCovered(reader);
                    Assert.True(reader.DocumentPresenter!.IsMeasureValid);
                    Assert.True(reader.DocumentPresenter!.IsArrangeValid);
                };
                MarkdownUpdateResult result;
                if (streaming)
                {
                    using var session = reader.BeginStreaming();
                    session.Append(next.AsMemory(reader.Text!.Length));
                    result = await session.CompleteAsync();
                }
                else
                {
                    reader.Text = next;
                    result = await reader.WhenUpdatedAsync();
                }
                Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
                Assert.False(result.LayoutPending);
                Assert.True(observed);
                AssertTailVisible(reader);
                Scroll(reader).Offset = default;
                window.UpdateLayout();
                AssertViewportCovered(reader);
                AssertTextProjection(reader);
            }
            finally { window.Close(); }
        });

    [Fact]
    public Task AppendingBelowAUserReadingPositionKeepsItsGraphemeAndSelection() =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = new Reader { Text = Paragraphs(120, 12), AutoScrollToEnd = true };
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                var scroll = Scroll(reader);
                scroll.Offset = new Vector(0, .4 * (scroll.Extent.Height - scroll.Viewport.Height));
                await Flush(window);
                var reading = FirstVisibleGrapheme(reader);
                reader.SetSelection(reading.Position, MarkdownTextBoundary.Next(reader.Document.Text, reading.Position));
                var selection = reader.SelectedText;
                using var session = reader.BeginStreaming();
                session.Append("\n\n" + Tall("paragraph") + "\n\nnew final tail");
                Assert.Equal(MarkdownUpdateStatus.Completed, (await session.CompleteAsync()).Status);
                await Flush(window);
                AssertReadingGrapheme(reader, reading);
                Assert.Equal(selection, reader.SelectedText);
                AssertViewportCovered(reader);
                AssertTextProjection(reader);
            }
            finally { window.Close(); }
        });

    [Fact]
    public Task HostMarginsAndAnImageGrowingAboveTheViewportKeepTheNaturalReadingAnchor() =>
        HeadlessTestScope.Run(async () =>
        {
            var loader = new DelayedLoader();
            var reader = new Reader
            {
                Text = "![pending](https://example.test/range)\n\n" + Paragraphs(60, 8),
                ImageLoader = loader,
            };
            // This intentionally tests the conservative host-callback path.
            reader.ElementPrepared += (_, args) =>
            {
                if (args.Element is MarkdownBlockPresenter block)
                    block.Margin = new Thickness(0, 17, 0, 23);
            };
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                await loader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                var image = Assert.Single(reader.GetVisualDescendants().OfType<MarkdownImage>());
                var nextText = TextFor(reader, reader.Document.Blocks[1].TextStart);
                var textOrigin = nextText.TranslatePoint(new Point(nextText.Padding.Left, nextText.Padding.Top),
                    reader.DocumentPresenter!)!.Value;
                Scroll(reader).Offset = new Vector(0, textOrigin.Y + 5.25);
                await Flush(window);
                var reading = FirstVisibleGrapheme(reader);
                loader.Finish(320);
                for (var attempt = 0; attempt < 100 && image.IsLoading; attempt++) await Flush(window);
                Assert.False(image.IsLoading);
                await Flush(window);
                Assert.Same(image, Assert.Single(reader.GetVisualDescendants().OfType<MarkdownImage>()));
                AssertReadingGrapheme(reader, reading);
                AssertViewportCovered(reader);
                AssertTextProjection(reader);
                Assert.Empty(reader.RenderingDiagnostics);
            }
            finally { window.Close(); loader.CancelPending(); }
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CustomNativeTextRootsUseTheConservativePathWithoutViewportOrTextLoss(bool wrap) =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = new Reader { Text = Paragraphs(100, 8) };
            reader.ConfigureParser(new()
            {
                Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new("range-native"))],
            });
            var renders = 0;
            reader.NodeRenderers = new(blocks: [new("range-native", context =>
            {
                renders++;
                return wrap ? new Border { Padding = new Thickness(13), Child = context.CreateText() }
                    : context.CreateText();
            })]);
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                var scroll = Scroll(reader);
                scroll.Offset = new Vector(0, .48 * (scroll.Extent.Height - scroll.Viewport.Height));
                window.UpdateLayout();
                AssertViewportCovered(reader);
                AssertTextProjection(reader);
                Assert.True(renders > 0);
                var reading = FirstVisibleGrapheme(reader);
                window.Width = 330;
                await Flush(window);
                AssertReadingGrapheme(reader, reading);
                AssertViewportCovered(reader);
                reader.SelectAll();
                Assert.Equal(new MarkdownParser().Parse(reader.Text).Text, reader.SelectedText);
                Assert.Empty(reader.RenderingDiagnostics);
            }
            finally { window.Close(); }
        });

    [Fact]
    public Task DuplicateParagraphsAndLateReferencesKeepActualGlobalPointerCopyAndLinkSemantics() =>
        HeadlessTestScope.Run(async () =>
        {
            const string duplicate = "duplicate 😀e\u0301";
            var reader = new Reader
            {
                Text = duplicate + "\n\n" + duplicate + "\n\nread [jump][target] and note[^n]\n\n" + Paragraphs(70),
            };
            var window = Show(reader);
            string? copied = null;
            reader.CopyingToClipboard += (_, args) => { copied = args.Text; args.Cancel = true; };
            var activated = new List<string>();
            reader.LinkClicked += (_, args) => { activated.Add(args.Url); args.Handled = true; };
            try
            {
                await Updated(reader, window);
                using var session = reader.BeginStreaming();
                session.Append("\n\n[target]: https://example.test/target\n\n[^n]: late footnote 中文😀\n");
                Assert.Equal(MarkdownUpdateStatus.Completed, (await session.CompleteAsync()).Status);
                var expected = new MarkdownParser().Parse(reader.Text);
                Assert.Equal(expected.Text, reader.Document.Text);
                Assert.Equal(expected.Diagnostics.ToArray(), reader.Document.Diagnostics.ToArray());
                Assert.Equal(reader.Document.Descendants().Count(), reader.Document.Descendants().Select(block => block.Id).Distinct().Count());
                Assert.True(reader.Focus());
                Press(window, Key.Home, RawInputModifiers.Control);
                await Flush(window);
                var second = reader.Document.Blocks[1];
                Assert.Equal(duplicate, second.Text);
                var position = second.TextStart + "duplicate ".Length;
                var text = TextFor(reader, position);
                var point = PointAt(text, window, position);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);
                Assert.Equal(new MarkdownSelection(position, position), reader.Selection);
                Press(window, Key.Right, RawInputModifiers.Shift);
                Assert.Equal("😀", reader.SelectedText);
                Assert.Equal(reader.Selection, reader.Selection.Normalize(reader.Document.Text));
                Press(window, Key.C, RawInputModifiers.Control);
                Assert.Equal("😀", copied);
                reader.SetSelection(0, 0);
                Press(window, Key.Tab);
                Press(window, Key.Enter);
                Assert.Contains("https://example.test/target", activated);
                reader.SelectAll();
                Assert.Equal(expected.Text, reader.SelectedText);
                Assert.Contains("late footnote 中文😀", reader.SelectedText);
                AssertViewportCovered(reader);
                AssertTextProjection(reader);
            }
            finally { window.Close(); }
        });

    private static string Tall(string kind) => kind switch
    {
        "code" => "```text\n" + string.Concat(Enumerable.Repeat("x\n", 100)) + "```",
        "paragraph" => string.Join("  \n", Enumerable.Repeat("x", 100)),
        "list" => string.Join("\n", Enumerable.Range(0, 100).Select(index => "- item " + index)),
        "table" => "| A | B |\n| - | - |\n" + string.Join("\n", Enumerable.Range(0, 100).Select(index => $"| row {index} | value |")),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string Paragraphs(int count, int repetitions = 1) => string.Join("\n\n",
        Enumerable.Range(0, count).Select(index => $"record {index:D4} " +
            string.Concat(Enumerable.Repeat("中文😀e\u0301 ordinary reading text ", repetitions))));

    private static Window Show(Reader reader)
    {
        var window = new Window { Width = 640, Height = 320, Content = reader };
        window.Show();
        return window;
    }

    private static ScrollViewer Scroll(Reader reader) => reader.GetVisualDescendants().OfType<ScrollViewer>()
        .Single(scroll => scroll.Name == "PART_ScrollViewer");

    private static async Task Updated(Reader reader, Window window)
    {
        Assert.Equal(MarkdownUpdateStatus.Completed,
            (await reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
        window.UpdateLayout();
    }

    private static async Task Flush(Window window)
    {
        await Task.Delay(15, TestContext.Current.CancellationToken);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
    }

    private static void AssertViewportCovered(Reader reader)
    {
        var scroll = Scroll(reader);
        var presenter = reader.DocumentPresenter!;
        Assert.True(scroll.Viewport.Height > 0);
        var top = scroll.Padding.Top;
        var bottom = top + Math.Min(scroll.Viewport.Height, Math.Max(0, scroll.Extent.Height - scroll.Offset.Y));
        var roots = presenter.Children.Select(view =>
        {
            var origin = view.TranslatePoint(default, scroll)!.Value;
            return (View: view, Top: origin.Y, Bottom: origin.Y + view.Bounds.Height);
        }).Where(root => root.Bottom + root.View.Margin.Bottom > top + .01 &&
            root.Top - root.View.Margin.Top < bottom - .01).OrderBy(root => root.Top).ToArray();
        Assert.NotEmpty(roots);
        Assert.True(roots[0].Top - roots[0].View.Margin.Top <= top + 1,
            $"Missing top coverage: top={top}, first={roots[0].Top}, offset={scroll.Offset.Y}.");
        Assert.True(roots[^1].Bottom + roots[^1].View.Margin.Bottom >= bottom - 1,
            $"Missing bottom coverage: bottom={bottom}, last={roots[^1].Bottom}, offset={scroll.Offset.Y}.");
        for (var index = 1; index < roots.Length; index++)
            Assert.True(roots[index].Top - roots[index - 1].Bottom <=
                roots[index - 1].View.Margin.Bottom + roots[index].View.Margin.Top + 1,
                $"Unrealized hole between actual roots at y={roots[index - 1].Bottom}..{roots[index].Top}.");
        var visible = presenter.Children.Count(view =>
            view.Bounds.Bottom > scroll.Offset.Y && view.Bounds.Top < scroll.Offset.Y + scroll.Viewport.Height);
        Assert.InRange(presenter.RealizedBlockCount, visible, visible + 16);
        Assert.InRange(presenter.RetainedBlockCount, 0, 16);
    }

    private static void AssertTailVisible(Reader reader)
    {
        var scroll = Scroll(reader);
        var tailId = reader.Document.Blocks[^1].Id;
        var tail = reader.DocumentPresenter!.Children.OfType<MarkdownBlockPresenter>().Single(root => root.NodeId == tailId);
        var origin = tail.TranslatePoint(default, scroll)!.Value;
        Assert.InRange(Math.Abs(scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y), 0, .5);
        Assert.True(origin.Y + tail.Bounds.Height > scroll.Padding.Top + .01);
        Assert.True(origin.Y < scroll.Padding.Top + scroll.Viewport.Height - .01);
    }

    private static void AssertTextProjection(Reader reader)
    {
        foreach (var text in reader.GetVisualDescendants().OfType<MarkdownTextPresenter>())
        {
            Assert.Equal(reader.Document.Text.Substring(text.TextStart, text.TextLength), Projection(text));
            var cursor = 0;
            foreach (var line in text.TextLayout.TextLines)
            {
                Assert.Equal(cursor, line.FirstTextSourceIndex);
                Assert.True(line.Length > 0);
                Assert.True(double.IsFinite(line.Height) && line.Height > 0);
                cursor += line.Length;
            }
            Assert.True(cursor >= text.TextLength);
        }
        Assert.Empty(reader.RenderingDiagnostics);
    }

    private static string Projection(MarkdownTextPresenter text) => text.Inlines is { Count: > 0 } inlines
        ? string.Concat(inlines.Select(InlineProjection)) : text.Text ?? string.Empty;

    private static string InlineProjection(Inline inline) => inline switch
    {
        Run run => run.Text ?? string.Empty,
        Span span => string.Concat(span.Inlines.Select(InlineProjection)),
        LineBreak => "\n",
        _ => throw new InvalidOperationException("The fixture expected native text inlines."),
    };

    private static MarkdownTextPresenter TextFor(Reader reader, int position) => reader.GetVisualDescendants()
        .OfType<MarkdownTextPresenter>().First(text => position >= text.TextStart && position < text.TextStart + text.TextLength);

    private static Point PointAt(MarkdownTextPresenter text, Window window, int position)
    {
        var rect = text.TextLayout.HitTestTextPosition(position - text.TextStart);
        return text.TranslatePoint(new Point(rect.X + text.Padding.Left + .1,
            rect.Y + rect.Height / 2 + text.Padding.Top), window)!.Value;
    }

    private static ReadingGrapheme FirstVisibleGrapheme(Reader reader)
    {
        var scroll = Scroll(reader);
        var candidates = new List<ReadingGrapheme>();
        foreach (var text in reader.GetVisualDescendants().OfType<MarkdownTextPresenter>())
        {
            var origin = text.TranslatePoint(new Point(text.Padding.Left, text.Padding.Top), scroll)!.Value;
            var y = origin.Y;
            foreach (var line in text.TextLayout.TextLines)
            {
                if (y + line.Height > scroll.Padding.Top + .01 && y < scroll.Padding.Top + scroll.Viewport.Height &&
                    line.FirstTextSourceIndex < text.TextLength)
                {
                    var position = MarkdownTextBoundary.SnapBackward(reader.Document.Text,
                        text.TextStart + line.FirstTextSourceIndex);
                    var rectangle = text.TextLayout.HitTestTextPosition(position - text.TextStart);
                    candidates.Add(new(position, origin.Y + rectangle.Y - scroll.Padding.Top,
                        reader.Document.Text[position..MarkdownTextBoundary.Next(reader.Document.Text, position)]));
                    break;
                }
                y += line.Height;
            }
        }
        Assert.NotEmpty(candidates);
        return candidates.OrderBy(candidate => candidate.Offset).First();
    }

    private static void AssertReadingGrapheme(Reader reader, ReadingGrapheme before)
    {
        var text = TextFor(reader, before.Position);
        var rect = text.TextLayout.HitTestTextPosition(before.Position - text.TextStart);
        var origin = text.TranslatePoint(new Point(text.Padding.Left, text.Padding.Top), Scroll(reader))!.Value;
        var offset = origin.Y + rect.Y - Scroll(reader).Padding.Top;
        Assert.InRange(Math.Abs(offset - before.Offset), 0, 1);
        Assert.Equal(before.Grapheme,
            reader.Document.Text[before.Position..MarkdownTextBoundary.Next(reader.Document.Text, before.Position)]);
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        window.UpdateLayout();
    }

    private static void Observe(string name, Reader reader, RootCreations created) =>
        TestContext.Current.TestOutputHelper?.WriteLine(JsonSerializer.Serialize(new
        {
            scenario = name,
            sourceLength = reader.Document.Source.Length,
            rootsCreated = created.Added.Count,
            rootsRealized = reader.DocumentPresenter!.RealizedBlockCount,
            rootsCreatedThenDiscarded = created.Added.Count(root => !reader.DocumentPresenter!.Children.Contains(root)),
            viewportHeight = Scroll(reader).Viewport.Height,
        }));

    private readonly record struct ReadingGrapheme(int Position, double Offset, string Grapheme);

    private sealed class RootCreations : IDisposable
    {
        private readonly INotifyCollectionChanged _children;
        public List<Control> Added { get; } = [];
        public RootCreations(MarkdownDocumentPresenter presenter)
        {
            _children = presenter.Children;
            _children.CollectionChanged += Changed;
        }
        private void Changed(object? sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.NewItems is not { } items) return;
            foreach (var item in items)
                if (item is Control view && !Added.Contains(view)) Added.Add(view);
        }
        public void Dispose() => _children.CollectionChanged -= Changed;
    }

    private sealed class DelayedLoader : IMarkdownImageLoader
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<MarkdownImageLease> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken token)
        {
            Started.TrySetResult();
            return new(_result.Task);
        }
        public void Finish(int height) => _result.TrySetResult(new(new WriteableBitmap(new PixelSize(80, height),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul)));
        public void CancelPending() => _result.TrySetCanceled();
    }
}
