using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ImmediateViewportTests
{
    [Theory]
    [InlineData(0d)]
    [InlineData(.5d)]
    [InlineData(1d)]
    public Task ADirectJumpRealizesTheActualViewportInOneLayoutPass(double fraction) =>
        HeadlessTestScope.Run(() => ADirectJumpRealizesTheActualViewportInOneLayoutPassCore(fraction));

    private static async Task ADirectJumpRealizesTheActualViewportInOneLayoutPassCore(double fraction)
    {
        var reader = new Reader
        {
            Text = string.Join("\n\n", Enumerable.Range(0, 10_000).Select(index => $"Block {index:D5} 中文 **bold**")),
        };
        var window = new Window { Width = 600, Height = 320, Content = reader };
        window.Show();
        try
        {
            Assert.Equal(MarkdownUpdateStatus.Completed, (await reader.WhenUpdatedAsync()).Status);
            var scroll = reader.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "PART_ScrollViewer");
            var presenter = reader.DocumentPresenter!;
            Assert.Equal(10_000, reader.Document.Blocks.Length);

            // Establish a settled range far from the tested destination. The
            // assertions below run immediately after that destination's first
            // UpdateLayout, without yielding to a delayed ScrollChanged/frame.
            scroll.Offset = new Vector(0, fraction == 0 ? scroll.Extent.Height : 0);
            await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
            await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
            Assert.True(scroll.Viewport.Height > 0);
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height * 100);

            scroll.Offset = new Vector(0, fraction * (scroll.Extent.Height - scroll.Viewport.Height));
            window.UpdateLayout();

            var offset = scroll.Offset.Y;
            var viewport = scroll.Viewport.Height;
            var visible = presenter.Children.Count(child => child.Bounds.Bottom > offset && child.Bounds.Top < offset + viewport);
            TestContext.Current.TestOutputHelper?.WriteLine($"fraction={fraction}; offset={offset}; viewport={viewport}; realized={presenter.RealizedBlockCount}; visible={visible}");
            Assert.True(visible > 0, $"fraction={fraction}; offset={offset}; viewport={viewport}; realized={presenter.RealizedBlockCount}; visible={visible}");
            Assert.InRange(presenter.RealizedBlockCount, visible, visible + 16);
            Assert.InRange(presenter.RetainedBlockCount, 0, 16);
        }
        finally { window.Close(); }
    }
}
