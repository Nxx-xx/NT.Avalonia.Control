using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    // Observation only: native pointer input must not be swallowed to make a smoke
    // test pass. Record competing events alongside the synthetic route so a native
    // runner can distinguish input interference from hit-test/export regressions.
    private sealed class ChartSmokeInputEvidence : IDisposable
    {
        private readonly ChartBase _chart;
        public List<object> Events { get; } = [];

        public ChartSmokeInputEvidence(ChartBase chart)
        {
            _chart = chart;
            chart.AddHandler(InputElement.PointerMovedEvent, OnPointer, RoutingStrategies.Direct | RoutingStrategies.Bubble, true);
            chart.AddHandler(InputElement.PointerExitedEvent, OnPointer, RoutingStrategies.Direct | RoutingStrategies.Bubble, true);
            chart.PointerCaptureLost += OnCaptureLost;
        }

        public object Snapshot(string stage)
        {
            object? active = _chart switch
            {
                CartesianChart chart => chart.HoveredPoint is { } hit ? new { hit.Index, stableId = hit.StableId?.ToString() } : null,
                AnalysisChartBase chart => chart.ActivePoint is { } hit ? new { hit.Index, stableId = hit.StableId?.ToString() } : null,
                EngineeringChartBase chart => chart.ActivePoint is { } hit ? new { members = hit.Members.Select(member => new { member.SourceIndex, stableId = member.StableId?.ToString() }).ToArray() } : null,
                _ => null,
            };
            var snapshot = new { stage, timestamp = Stopwatch.GetTimestamp(), bounds = _chart.Bounds, active };
            Events.Add(snapshot);
            return snapshot;
        }

        private void OnPointer(object? sender, PointerEventArgs args)
        {
            Events.Add(new { eventName = args.RoutedEvent?.Name, timestamp = Stopwatch.GetTimestamp(), pointerId = args.Pointer.Id,
                pointerType = args.Pointer.Type.ToString(), position = args.GetPosition(_chart), args.Handled });
        }

        private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs args) =>
            Events.Add(new { eventName = "PointerCaptureLost", timestamp = Stopwatch.GetTimestamp(), pointerId = args.Pointer.Id });

        public void Dispose()
        {
            _chart.RemoveHandler(InputElement.PointerMovedEvent, OnPointer);
            _chart.RemoveHandler(InputElement.PointerExitedEvent, OnPointer);
            _chart.PointerCaptureLost -= OnCaptureLost;
        }
    }
}
