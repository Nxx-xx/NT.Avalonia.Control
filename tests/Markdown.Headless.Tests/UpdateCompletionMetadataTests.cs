using Avalonia.Controls;
using Avalonia.Threading;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class UpdateCompletionMetadataTests
{
    [Fact]
    public Task UnmountedTextCompletionConfirmsTheModelAndReportsPendingLayout() =>
        HeadlessTestScope.Run(async () =>
        {
            const string source = "# unmounted **complete model** 中文";
            var reader = new Reader { Text = source };
            var result = await reader.WhenUpdatedAsync();
            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            Assert.True(result.LayoutPending);
            Assert.Equal(source, result.Source);
            Assert.Equal(source, reader.Document.Source);
            Assert.Contains("complete model", reader.Document.Text);
            Assert.False(reader.HasPresentationEnvironment);
        });

    [Fact]
    public Task HiddenTextCompletionConfirmsTheNewModelAndReportsPendingLayout() =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = new Reader { Text = "visible seed" };
            var window = new Window { Width = 600, Height = 280, Content = reader };
            window.Show();
            try
            {
                await reader.WhenUpdatedAsync();
                await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
                Assert.True(reader.HasPresentationEnvironment);
                reader.IsVisible = false;
                const string source = "# hidden **updated model** 中文";
                reader.Text = source;
                var result = await reader.WhenUpdatedAsync();
                Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
                Assert.True(result.LayoutPending);
                Assert.Equal(source, result.Source);
                Assert.Equal(source, reader.Document.Source);
                Assert.Contains("updated model", reader.Document.Text);
                Assert.True(reader.HasPresentationEnvironment);
            }
            finally { window.Close(); }
        });

    [Fact]
    public Task AnEmptyStreamWithAVisibleValidTemplateHasNoUnconfirmedTailLayout() =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = new Reader { AutoScrollToEnd = true, Text = string.Empty };
            var window = new Window { Width = 600, Height = 280, Content = reader };
            window.Show();
            try
            {
                await reader.WhenUpdatedAsync();
                await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
                Assert.True(reader.HasPresentationEnvironment);
                using var session = reader.BeginStreaming();
                session.Append(string.Empty);
                var result = await session.CompleteAsync();
                Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
                Assert.False(result.LayoutPending);
                Assert.Equal(string.Empty, result.Source);
                Assert.Equal(string.Empty, reader.Document.Source);
                Assert.Empty(reader.Document.Blocks);
            }
            finally { window.Close(); }
        });
}
