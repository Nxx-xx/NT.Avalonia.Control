using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using System.ComponentModel;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ReaderTests
{
    [Fact]
    public Task Full_selection_spans_paragraph_code_table_and_unrealized_tail() => HeadlessTestScope.Run(() => Full_selection_spans_paragraph_code_table_and_unrealized_tailCore());

    private async Task Full_selection_spans_paragraph_code_table_and_unrealized_tailCore()
    {
        using var f = await Fixture.Create("first **strong**\n\n```cs\n  x();\n```\n\n| A | B |\n|---|---|\n| c | d |\n\n" + string.Join("\n\n", Enumerable.Range(0, 1000).Select(i => $"tail {i}")));
        f.Reader.SelectAll();
        Assert.Equal(f.Reader.Document.Text, f.Reader.SelectedText);
        Assert.Contains("  x();", f.Reader.SelectedText);
        Assert.Contains("A\tB\nc\td", f.Reader.SelectedText);
        Assert.EndsWith("tail 999", f.Reader.SelectedText);
        Assert.InRange(f.Reader.DocumentPresenter!.RealizedBlockCount, 1, 50);
        Assert.True(f.Reader.CanCopy);
    }

    [Fact]
    public Task Copy_awaits_clipboard_and_can_be_cancelled() => HeadlessTestScope.Run(() => Copy_awaits_clipboard_and_can_be_cancelledCore());

    private async Task Copy_awaits_clipboard_and_can_be_cancelledCore()
    {
        using var f = await Fixture.Create("copy **text**");
        f.Reader.SelectAll();
        Assert.Equal(MarkdownCopyResult.Copied, await f.Reader.CopyAsync());
        Assert.Equal("copy text", await f.Window.Clipboard!.TryGetTextAsync());
        f.Reader.CopyingToClipboard += (_, args) => args.Cancel = true;
        Assert.Equal(MarkdownCopyResult.Cancelled, await f.Reader.CopyAsync());
        f.Reader.ClearSelection();
        Assert.Equal(MarkdownCopyResult.EmptySelection, await f.Reader.CopyAsync());
    }

    [Fact]
    public Task Replacement_clears_selection_and_stale_session_cannot_append() => HeadlessTestScope.Run(() => Replacement_clears_selection_and_stale_session_cannot_appendCore());

    private async Task Replacement_clears_selection_and_stale_session_cannot_appendCore()
    {
        using var f = await Fixture.Create("old");
        var session = f.Reader.BeginStreaming();
        session.Append(" prefix");
        f.Reader.SelectAll();
        f.Reader.Text = "new **document**";
        await f.Reader.WhenUpdatedAsync();
        Assert.Equal("new document", f.Reader.Document.Text);
        Assert.Empty(f.Reader.SelectedText);
        Assert.Equal(MarkdownUpdateStatus.Cancelled, (await session.Completion).Status);
        Assert.Throws<InvalidOperationException>(() => session.Append("stale"));
    }

    [Fact]
    public Task Streaming_retains_TwoWay_binding_and_host_replacement_wins() => HeadlessTestScope.Run(() => Streaming_retains_TwoWay_binding_and_host_replacement_winsCore());

    private async Task Streaming_retains_TwoWay_binding_and_host_replacement_winsCore()
    {
        var vm = new TextModel { Text = "seed" };
        using var f = await Fixture.Create(vm: vm);
        var session = f.Reader.BeginStreaming();
        await Task.Run(() => session.Append(" **stream**"));
        var result = await session.CompleteAsync();
        Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
        Assert.Equal("seed **stream**", vm.Text);
        Assert.Equal("seed stream", f.Reader.Document.Text);
        vm.Text = "external";
        await f.Reader.WhenUpdatedAsync();
        Assert.Equal("external", f.Reader.Document.Text);
        Assert.Equal("external", f.Reader.Text);
    }

    [Fact]
    public Task Every_split_finishes_with_same_structure_and_links() => HeadlessTestScope.Run(() => Every_split_finishes_with_same_structure_and_linksCore());

    private async Task Every_split_finishes_with_same_structure_and_linksCore()
    {
        const string source = "start [link][ref]\n\n```cs\na();\n```\n\n|A|B|\n|-|-|\n|x|y|\n\n[ref]: https://example.test/";
        using var f = await Fixture.Create("");
        var expected = new MarkdownParser().Parse(source);
        foreach (var cut in Enumerable.Range(0, source.Length + 1))
        {
            f.Reader.Text = "";
            await f.Reader.WhenUpdatedAsync();
            var session = f.Reader.BeginStreaming();
            session.Append(source[..cut]);
            session.Append(source[cut..]);
            Assert.Equal(MarkdownUpdateStatus.Completed, (await session.CompleteAsync()).Status);
            Assert.Equal(expected.Text, f.Reader.Document.Text);
            Assert.Equal(source, f.Reader.Text);
        }
    }

    [Fact]
    public Task Hidden_completion_finishes_without_layout_and_unmounted_returns_error() => HeadlessTestScope.Run(() => Hidden_completion_finishes_without_layout_and_unmounted_returns_errorCore());

    private async Task Hidden_completion_finishes_without_layout_and_unmounted_returns_errorCore()
    {
        using var f = await Fixture.Create("seed");
        f.Reader.IsVisible = false;
        var hidden = f.Reader.BeginStreaming(); hidden.Append(" hidden");
        var result = await hidden.CompleteAsync();
        Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
        Assert.True(result.LayoutPending);
        f.Reader.IsVisible = true;
        f.Window.UpdateLayout();
        Assert.Equal("seed hidden", f.Reader.Document.Text);
        var detached = new Reader();
        var session = detached.BeginStreaming(); session.Append("unmounted");
        Assert.Equal(MarkdownUpdateStatus.Error, (await session.CompleteAsync()).Status);
    }

    [Fact]
    public Task Detach_cancels_session_and_reattach_does_not_revive_it() => HeadlessTestScope.Run(() => Detach_cancels_session_and_reattach_does_not_revive_itCore());

    private async Task Detach_cancels_session_and_reattach_does_not_revive_itCore()
    {
        using var f = await Fixture.Create("seed");
        var session = f.Reader.BeginStreaming(); session.Append(" tail");
        f.Window.Content = null;
        Assert.Equal(MarkdownUpdateStatus.Cancelled, (await session.Completion).Status);
        f.Window.Content = f.Reader;
        await f.Reader.WhenUpdatedAsync();
        Assert.Throws<InvalidOperationException>(() => session.Append("revived"));
        Assert.Equal("seed tail", f.Reader.Text);
    }

    [Fact]
    public Task Selection_normalizes_graphemes_and_survives_width_theme_and_service_change() => HeadlessTestScope.Run(() => Selection_normalizes_graphemes_and_survives_width_theme_and_service_changeCore());

    private async Task Selection_normalizes_graphemes_and_survives_width_theme_and_service_changeCore()
    {
        using var f = await Fixture.Create("中文 e\u0301 👨‍👩‍👧‍👦 עברית");
        f.Reader.SetSelection(4, f.Reader.Document.Text.Length - 1);
        var selected = f.Reader.SelectedText;
        f.Window.Width = 240;
        f.Window.RequestedThemeVariant = ThemeVariant.Dark;
        f.Reader.CodeHighlighter = new FakeHighlighter();
        f.Window.UpdateLayout();
        Assert.Equal(selected, f.Reader.SelectedText);
        Assert.Equal(3, f.Reader.Selection.Start);
    }

    [Fact]
    public Task Real_inline_style_changes_native_layout_and_preserves_text() => HeadlessTestScope.Run(() => Real_inline_style_changes_native_layout_and_preserves_textCore());

    private async Task Real_inline_style_changes_native_layout_and_preserves_textCore()
    {
        var style = new Style(selector => selector.OfType<MarkdownSpan>().Class("strong")) { Setters = { new Setter(MarkdownSpan.FontSizeProperty, 48d), new Setter(MarkdownSpan.ForegroundProperty, Brushes.Red) } };
        Application.Current!.Styles.Add(style);
        try
        {
            using var f = await Fixture.Create("small **big** small");
            var text = f.Reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            var span = text.Inlines!.OfType<MarkdownSpan>().Single(node => node.Classes.Contains("strong"));
            Assert.Equal(48, span.FontSize);
            Assert.True(text.TextLayout.Height > 40);
            var before = text.TextLayout.Height;
            span.FontSize = 64;
            f.Window.UpdateLayout();
            Assert.True(text.TextLayout.Height > before);
            f.Reader.SelectAll(); Assert.Equal("small big small", f.Reader.SelectedText);
        }
        finally { Application.Current!.Styles.Remove(style); }
    }

    [Fact]
    public Task Old_image_completion_cannot_reappear_and_loader_is_host_owned() => HeadlessTestScope.Run(() => Old_image_completion_cannot_reappear_and_loader_is_host_ownedCore());

    private async Task Old_image_completion_cannot_reappear_and_loader_is_host_ownedCore()
    {
        var loader = new DelayedLoader();
        using var f = await Fixture.Create("![alt](https://example.test/old.png)");
        f.Reader.ImageLoader = loader;
        f.Window.UpdateLayout();
        await loader.Started.Task;
        f.Reader.Text = "replacement";
        await f.Reader.WhenUpdatedAsync();
        loader.Finish();
        await Task.Delay(30);
        f.Window.UpdateLayout();
        Assert.DoesNotContain(f.Reader.GetVisualDescendants().OfType<Image>(), image => image.Source is not null);
        Assert.Equal(0, loader.DisposeCount);
    }

    [Fact]
    public Task Late_highlight_after_service_switch_preserves_plain_code() => HeadlessTestScope.Run(() => Late_highlight_after_service_switch_preserves_plain_codeCore());

    private async Task Late_highlight_after_service_switch_preserves_plain_codeCore()
    {
        var old = new FakeHighlighter(delay: true);
        using var f = await Fixture.Create("```cs\nvar x = 1;\n```");
        f.Reader.CodeHighlighter = old; f.Window.UpdateLayout();
        f.Reader.CodeHighlighter = null; f.Window.UpdateLayout();
        old.Finish(); await Task.Delay(30);
        f.Reader.SelectAll();
        Assert.Equal("var x = 1;\n", f.Reader.SelectedText);
        var runs = f.Reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().SelectMany(text => text.GetLogicalDescendants().OfType<MarkdownRun>()).ToArray();
        var code = f.Reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single(text => text.Classes.Contains("code"));
        Assert.Equal("var x = 1;\n", code.Text);
        Assert.Empty(code.Inlines!);
        Assert.DoesNotContain(runs, run => run.Foreground is ISolidColorBrush brush && brush.Color == Colors.Red);
    }

    [Fact]
    public Task Wide_grid_table_uses_native_grid_and_horizontal_scrolling() => HeadlessTestScope.Run(() => Wide_grid_table_uses_native_grid_and_horizontal_scrollingCore());

    private async Task Wide_grid_table_uses_native_grid_and_horizontal_scrollingCore()
    {
        using var f = await Fixture.Create("+----------+----------+\n| A        | B        |\n+==========+==========+\n| **bold** | 中文     |\n+----------+----------+");
        f.Reader.Width = 220; f.Window.UpdateLayout();
        Assert.Contains(f.Reader.GetVisualDescendants().OfType<Grid>(), grid => grid.ColumnDefinitions.Count == 2);
        Assert.Contains(f.Reader.GetVisualDescendants().OfType<ScrollViewer>(), scroll => scroll.Extent.Width > scroll.Viewport.Width);
        f.Reader.SelectAll(); Assert.Contains("bold\t中文", f.Reader.SelectedText);
    }

    private sealed class Fixture : IDisposable
    {
        public Reader Reader { get; } = new();
        public Window Window { get; }
        private Fixture() { Window = new Window { Width = 700, Height = 350, Content = Reader }; }
        public static async Task<Fixture> Create(string? source = null, TextModel? vm = null)
        {
            var f = new Fixture();
            if (vm is not null) { f.Reader.DataContext = vm; f.Reader.Bind(Reader.TextProperty, new Binding(nameof(TextModel.Text)) { Mode = BindingMode.TwoWay }); }
            else f.Reader.Text = source;
            f.Window.Show();
            await f.Reader.WhenUpdatedAsync();
            f.Window.UpdateLayout();
            return f;
        }
        public void Dispose() => Window.Close();
    }
    private sealed class TextModel : INotifyPropertyChanged
    {
        private string? _text;
        public string? Text { get => _text; set { if (_text == value) return; _text = value; PropertyChanged?.Invoke(this, new(nameof(Text))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class DelayedLoader : IMarkdownImageLoader, IDisposable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<MarkdownImageLease> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DisposeCount { get; private set; }
        public ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken token) { Started.TrySetResult(); return new(_result.Task); }
        public void Finish() => _result.TrySetResult(new(new WriteableBitmap(new PixelSize(80, 200), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul)));
        public void Dispose() => DisposeCount++;
    }
    private sealed class FakeHighlighter(bool delay = false) : IMarkdownCodeHighlighter
    {
        private readonly TaskCompletionSource<IReadOnlyList<MarkdownHighlightSpan>> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event EventHandler? Invalidated { add { } remove { } }
        public ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(string code, string? language, CancellationToken token) => delay ? new(_result.Task) : ValueTask.FromResult<IReadOnlyList<MarkdownHighlightSpan>>([]);
        public void Finish() => _result.TrySetResult([new(0, 3, "#ff0000")]);
    }
}

