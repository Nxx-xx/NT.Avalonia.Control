using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class KeyboardBrowsingTests
{
    [AvaloniaFact]
    public void Browse_without_selection_skips_missing_points_and_publishes_readings()
    {
        using var host = new Host();
        host.Key(Key.Right); Assert.Equal(0, host.Chart.CurrentPoint?.Index);
        host.Key(Key.Right); Assert.Equal(2, host.Chart.CurrentPoint?.Index);
        Assert.Empty(host.Chart.SelectedPoints!); Assert.Null(host.Chart.SelectedPoint);
        Assert.Equal(50, host.Chart.CurrentPoint!.Point.Y);
        Assert.Contains(host.Chart.HoveredPoints, point => point.Index == 2);
        host.Key(Key.Left); Assert.Equal(0, host.Chart.CurrentPoint?.Index);
    }

    [AvaloniaFact]
    public void Home_end_and_series_keys_are_independent_of_selection()
    {
        using var host = new Host();
        var other = new ScatterSeries { ItemsSource = new ChartPoint[] { new(2, 30), new(6, 60) } }; host.Chart.Series.Add(other); host.Flush();
        host.Key(Key.End); Assert.Same(other, host.Chart.CurrentPoint!.Series); Assert.Equal(1, host.Chart.CurrentPoint.Index);
        host.Key(Key.Up); Assert.Same(host.Series, host.Chart.CurrentPoint!.Series);
        host.Key(Key.Home); Assert.Equal(0, host.Chart.CurrentPoint.Index);
        host.Key(Key.Down); Assert.Same(other, host.Chart.CurrentPoint!.Series); Assert.Equal(0, host.Chart.CurrentPoint.Index);
        host.Chart.Viewport = new(1, 8, 10, 90); host.Flush(); host.Key(Key.Home, RawInputModifiers.Control); Assert.Null(host.Chart.Viewport);
    }

    [AvaloniaFact]
    public void Space_respects_cancellation_and_enter_invokes_without_selecting()
    {
        using var host = new Host();
        host.Chart.SelectionMode = ChartSelectionMode.Multiple; var invoked = 0;
        host.Chart.PointInvokedCommand = new Command(_ => invoked++);
        host.Chart.SelectionChanging += (_, args) => args.Cancel = true;
        host.Key(Key.Right); host.Key(Key.Space);
        Assert.Empty(host.Chart.SelectedPoints!); Assert.Equal(0, host.Chart.CurrentPoint?.Index);
        host.Key(Key.Enter); Assert.Equal(1, invoked); Assert.Empty(host.Chart.SelectedPoints!);
        host.Chart.SelectionMode = ChartSelectionMode.None; host.Key(Key.Enter); Assert.Equal(2, invoked);
    }

    [AvaloniaFact]
    public void Control_space_toggles_multiple_and_escape_preserves_selection()
    {
        using var host = new Host(); host.Chart.SelectionMode = ChartSelectionMode.Multiple;
        host.Key(Key.Right); host.Key(Key.Space); Assert.Single(host.Chart.SelectedPoints!);
        host.Key(Key.Right); host.Key(Key.Space, RawInputModifiers.Control); Assert.Equal(2, host.Chart.SelectedPoints!.Count);
        host.Key(Key.Space, RawInputModifiers.Control); Assert.Single(host.Chart.SelectedPoints!);
        host.Key(Key.Escape); Assert.Null(host.Chart.CurrentPoint); Assert.Single(host.Chart.SelectedPoints!); Assert.Empty(host.Chart.HoveredPoints);
    }

    [AvaloniaFact]
    public void Activation_refreshes_a_scene_invalidated_after_browsing()
    {
        using var host = new Host(); host.Key(Key.Right);
        host.Chart.SelectionMode = ChartSelectionMode.Multiple;
        // No explicit layout/render between invalidation and the routed key.
        host.Key(Key.Space); Assert.Single(host.Chart.SelectedPoints!);
        var invoked = 0; host.Chart.PointInvokedCommand = new Command(_ => invoked++);
        host.Chart.SelectionMode = ChartSelectionMode.None;
        host.Key(Key.Enter); Assert.Equal(1, invoked); Assert.Equal(0, host.Chart.CurrentPoint?.Index);
    }

    [AvaloniaFact]
    public void Browse_is_opt_in_and_preserves_original_selection_keyboard_mode()
    {
        using var host = new Host(); host.Chart.IsPointBrowsingEnabled = false; host.Chart.SelectionMode = ChartSelectionMode.Single;
        host.Key(Key.Right); Assert.Equal(0, host.Chart.SelectedPoint?.Index); Assert.Null(host.Chart.CurrentPoint);
        host.Chart.Viewport = new(1, 8, 10, 90); host.Flush(); host.Key(Key.Home); Assert.Null(host.Chart.Viewport);
    }

    [AvaloniaFact]
    public void Invalid_offscreen_disabled_and_detached_navigation_do_not_publish_a_point()
    {
        using var host = new Host();
        Assert.False(host.Chart.NavigateToPoint(host.Series, 1));
        Assert.False(host.Chart.NavigateToPoint(new LineSeries(), 0));
        Assert.True(host.Chart.NavigateToPoint(host.Series, 2));
        host.Chart.Viewport = new(0, 2, 0, 100); host.Flush(); Assert.Null(host.Chart.CurrentPoint);
        Assert.False(host.Chart.NavigateToPoint(host.Series, 2));
        host.Chart.IsEnabled = false; Assert.False(host.Chart.NavigateToPoint(host.Series, 0)); Assert.Null(host.Chart.CurrentPoint);
        host.Chart.IsEnabled = true; host.Window.Content = null; Assert.False(host.Chart.NavigateToPoint(host.Series, 0));
    }

    [AvaloniaFact]
    public void Focus_loss_and_hidden_ancestor_clear_current_point()
    {
        using var host = new Host();
        host.Key(Key.Right); Assert.NotNull(host.Chart.CurrentPoint);
        var button = new Button { Content = "Other focus" }; var panel = new StackPanel(); host.Window.Content = panel;
        panel.Children.Add(host.Chart); panel.Children.Add(button); host.Flush(); host.Chart.Focus();
        host.Key(Key.Right); Assert.NotNull(host.Chart.CurrentPoint); button.Focus(); Assert.Null(host.Chart.CurrentPoint);
        host.Chart.Focus(); host.Key(Key.Right); panel.IsVisible = false; Assert.Null(host.Chart.CurrentPoint);
    }

    [AvaloniaFact]
    public void Callback_navigation_keeps_latest_intent_and_removed_series_clears_cursor()
    {
        using var host = new Host(); var redirected = false;
        host.Chart.PropertyChanged += (_, args) =>
        {
            if (args.Property == CartesianChart.CurrentPointProperty && !redirected && host.Chart.CurrentPoint?.Index == 0)
            { redirected = true; host.Chart.NavigateToPoint(host.Series, 2); }
        };
        Assert.False(host.Chart.NavigateToPoint(host.Series, 0)); Assert.Equal(2, host.Chart.CurrentPoint?.Index);
        host.Chart.Series.Clear(); host.Flush(); Assert.Null(host.Chart.CurrentPoint);
    }

    [AvaloniaFact]
    public void Page_keys_use_source_pages_and_end_reaches_last_point()
    {
        using var host = new Host(); host.Series.ItemsSource = Enumerable.Range(0, 150).Select(index => new ChartPoint(index / 15d, 40)).ToArray(); host.Flush();
        host.Key(Key.Home); Assert.Equal(0, host.Chart.CurrentPoint?.Index);
        host.Key(Key.PageDown); Assert.Equal(64, host.Chart.CurrentPoint?.Index);
        host.Key(Key.PageUp); Assert.Equal(0, host.Chart.CurrentPoint?.Index);
        host.Key(Key.End); Assert.Equal(149, host.Chart.CurrentPoint?.Index);
        host.Key(Key.Right); Assert.Equal(149, host.Chart.CurrentPoint?.Index);
    }

    [AvaloniaFact]
    public void Routed_key_does_not_walk_past_a_callback_that_redirects_the_cursor()
    {
        using var host = new Host(); var redirected = false;
        host.Chart.PropertyChanged += (_, args) =>
        {
            if (args.Property == CartesianChart.CurrentPointProperty && !redirected && host.Chart.CurrentPoint?.Index == 0)
            { redirected = true; host.Chart.NavigateToPoint(host.Series, 3); }
        };
        host.Key(Key.Right); Assert.Equal(3, host.Chart.CurrentPoint?.Index);
    }

    [AvaloniaFact]
    public void Key_browsing_reuses_snapshot_without_reenumerating_the_source()
    {
        using var host = new Host(); var source = new CountingPoints(); host.Series.ItemsSource = source; host.Flush();
        var before = source.Enumerations; host.Key(Key.Home); host.Key(Key.Right); host.Key(Key.End);
        Assert.Equal(before, source.Enumerations);
    }

    private sealed class CountingPoints : IEnumerable<ChartPoint>
    {
        public int Enumerations;
        public IEnumerator<ChartPoint> GetEnumerator() { Enumerations++; for (var i = 0; i < 100; i++) yield return new(i / 10d, 50); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class Command(Action<object?> action) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
    private sealed class Host : IDisposable
    {
        public CartesianChart Chart { get; } = new() { IsPointBrowsingEnabled = true, IsKeyboardNavigationEnabled = true, SelectionMode = ChartSelectionMode.None, ShowLegend = false,
            Width = 700, Height = 380, XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        public LineSeries Series { get; } = new() { ItemsSource = new ChartPoint[] { new(1, 20), new(3, double.NaN), new(5, 50), new(8, 80) } };
        public Window Window { get; }
        public Host() { Chart.Series.Add(Series); Window = new Window { Width = 800, Height = 500, Content = Chart }; Window.Show(); Flush(); Chart.Focus(); }
        public void Flush() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); using (var drawing = new DrawingGroup().Open()) Chart.Render(drawing); Dispatcher.UIThread.RunJobs(); }
        public void Key(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
        { Window.KeyPress(key, modifiers, PhysicalKey.None, null); Window.KeyRelease(key, modifiers, PhysicalKey.None, null); Flush(); }
        public void Dispose() => Window.Close();
    }
}
