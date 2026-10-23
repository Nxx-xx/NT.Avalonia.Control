using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class StreamingBindingTests
{
    [Fact]
    public Task MT23_TwoWaySetterTransformationWinsOverTheStreamingValue() => HeadlessTestScope.Run(() => MT23_TwoWaySetterTransformationWinsOverTheStreamingValueCore());

    private async Task MT23_TwoWaySetterTransformationWinsOverTheStreamingValueCore()
    {
        var model = new TransformingModel { Text = "seed" };
        var reader = new Reader { DataContext = model };
        reader.Bind(Reader.TextProperty, new Binding(nameof(TransformingModel.Text)) { Mode = BindingMode.TwoWay });
        var window = new Window { Width = 600, Height = 300, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync();
            var published = new List<string>();
            reader.Updated += (_, _) => published.Add(reader.Document.Source);
            var session = reader.BeginStreaming();
            session.Append(" **stream**");
            var result = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await reader.WhenUpdatedAsync();
            Assert.Equal("host replacement", model.Text);
            Assert.Equal("host replacement", reader.Text);
            Assert.Equal("host replacement", reader.Document.Source);
            Assert.Equal(MarkdownUpdateStatus.Cancelled, result.Status);
            Assert.DoesNotContain("seed **stream**", published);
            Assert.Throws<InvalidOperationException>(() => session.Append("stale tail"));
            model.Text = "later host value";
            await reader.WhenUpdatedAsync();
            Assert.Equal("later host value", reader.Document.Source);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT23_ReentrantExternalPropertyReplacementNeverPublishesTheOldDocument() => HeadlessTestScope.Run(() => MT23_ReentrantExternalPropertyReplacementNeverPublishesTheOldDocumentCore());

    private async Task MT23_ReentrantExternalPropertyReplacementNeverPublishesTheOldDocumentCore()
    {
        var reader = new Reader { Text = "seed" };
        var window = new Window { Width = 600, Height = 300, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync();
            var published = new List<string>();
            reader.Updated += (_, _) => published.Add(reader.Document.Source);
            reader.PropertyChanged += (_, change) =>
            {
                if (change.Property == Reader.TextProperty && reader.Text == "seed streamed")
                    reader.SetCurrentValue(Reader.TextProperty, "external from callback");
            };
            var session = reader.BeginStreaming();
            session.Append(" streamed");
            Assert.Equal(MarkdownUpdateStatus.Cancelled,
                (await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
            await reader.WhenUpdatedAsync();
            Assert.Equal("external from callback", reader.Document.Source);
            Assert.Equal(["external from callback"], published);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT23_SameValueTwoWayEchoKeepsTheSessionSelectionAndBinding() => HeadlessTestScope.Run(() => MT23_SameValueTwoWayEchoKeepsTheSessionSelectionAndBindingCore());

    private async Task MT23_SameValueTwoWayEchoKeepsTheSessionSelectionAndBindingCore()
    {
        var model = new EchoModel { Text = "seed" };
        var reader = new Reader { DataContext = model };
        reader.Bind(Reader.TextProperty, new Binding(nameof(EchoModel.Text)) { Mode = BindingMode.TwoWay });
        var window = new Window { Width = 600, Height = 300, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync();
            reader.SetSelection(0, 4);
            var selected = reader.SelectedText;
            var session = reader.BeginStreaming();
            session.Append(" **stream**");
            var result = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            Assert.Equal(selected, reader.SelectedText);
            Assert.Equal("seed **stream**", model.Text);
            Assert.InRange(model.WriteCount, 1, 12);
            model.Text = "binding still active";
            await reader.WhenUpdatedAsync();
            Assert.Equal("binding still active", reader.Document.Source);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT24_MissingTemplateHasAnErrorBoundaryAndVisibleCompletionIncludesNativeLayout() => HeadlessTestScope.Run(() => MT24_MissingTemplateHasAnErrorBoundaryAndVisibleCompletionIncludesNativeLayoutCore());

    private async Task MT24_MissingTemplateHasAnErrorBoundaryAndVisibleCompletionIncludesNativeLayoutCore()
    {
        var reader = new Reader { Text = "seed" };
        var window = new Window { Width = 600, Height = 300, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync();
            var session = reader.BeginStreaming();
            session.Append("\n\n## final paragraph");
            var result = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            var texts = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().ToArray();
            Assert.NotEmpty(texts);
            Assert.All(texts, text => Assert.True(text.TextLayout.Height > 0));
            reader.Template = new FuncControlTemplate<Reader>((_, _) => new Border());
            window.UpdateLayout();
            var missingTemplate = reader.BeginStreaming();
            missingTemplate.Append(" tail");
            var error = await missingTemplate.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(MarkdownUpdateStatus.Error, error.Status);
            Assert.IsType<InvalidOperationException>(error.Error);
        }
        finally { window.Close(); }
    }

    private sealed class TransformingModel : INotifyPropertyChanged
    {
        private string? _text;
        public string? Text
        {
            get => _text;
            set
            {
                var next = value == "seed **stream**" ? "host replacement" : value;
                if (_text == next) return;
                _text = next;
                PropertyChanged?.Invoke(this, new(nameof(Text)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class EchoModel : INotifyPropertyChanged
    {
        private string? _text;
        public int WriteCount { get; private set; }
        public string? Text
        {
            get => _text;
            set
            {
                WriteCount++;
                _text = value;
                PropertyChanged?.Invoke(this, new(nameof(Text)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

