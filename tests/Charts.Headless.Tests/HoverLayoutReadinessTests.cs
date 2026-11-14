using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class HoverLayoutReadinessTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pending_position_or_width_change_cancels_old_hover_but_settled_routing_keeps_original_identity(bool move)
    {
        var row = new Row("original-record", 5, 5);
        var chart = new CartesianChart
        {
            Height = 260, ShowLegend = false, IsTooltipEnabled = true,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        };
        chart.Series.Add(new ScatterSeries
        {
            ItemsSource = new[] { row }, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y),
            IdPath = nameof(Row.Id), MarkerSize = 12,
        });
        var summary = new Border { Height = 20 };
        var panel = new StackPanel { Width = 500, Children = { summary, chart } };
        var window = new Window { Width = 600, Height = 400, Content = panel };
        try
        {
            window.Show(); Flush();
            var before = chart.Bounds;
            if (move) summary.Height = 58; else panel.Width = 466;
            Move();
            AssertIdentity(); // The hit algorithm succeeds even before pending layout is drained.
            Flush();
            Assert.NotEqual(before, chart.Bounds);
            Assert.Null(chart.HoveredPoint); // Bounds changes deliberately invalidate old input.
            var settled = chart.Bounds;
            Move(); Flush();
            Assert.Equal(settled, chart.Bounds);
            AssertIdentity();
        }
        finally { window.Close(); }

        void Flush() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        void Move()
        {
            var point = chart.PlotArea.Center;
            using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            var position = chart.TranslatePoint(point, window)!.Value;
            chart.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, chart, pointer, window, position, 1, default, KeyModifiers.None));
        }
        void AssertIdentity()
        {
            var hit = Assert.IsType<ChartPointHit>(chart.HoveredPoint);
            Assert.Equal(0, hit.Index); Assert.Same(row, hit.Item); Assert.Equal(row.Id, hit.StableId);
        }
    }

    private sealed record Row(string Id, double X, double Y);
}
