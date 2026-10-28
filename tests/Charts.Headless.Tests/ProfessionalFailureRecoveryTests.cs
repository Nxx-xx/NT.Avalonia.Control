using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ProfessionalFailureRecoveryTests
{
    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void Invalid_relation_status_settles_without_rebuilding_errors_and_same_control_recovers(int kind)
    {
        RelationsChartBase chart = kind switch
        {
            0 => new SankeyChart(), 1 => new SankeyChart { Mode = RelationSankeyMode.Alluvial }, 2 => new NetworkChart(),
            3 => new NetworkChart { Mode = RelationNetworkMode.ForceDirected },
            4 => new NetworkChart { Mode = RelationNetworkMode.Arc }, 5 => new ChordDiagramChart(),
            6 => new FlowChart(), 7 => new FlowChart { Mode = RelationFlowMode.Process },
            _ => new FlowChart { Mode = RelationFlowMode.Mindmap },
        };
        chart.ItemsSource = new[] { new GraphNode("a"), new GraphNode("b") };
        chart.EdgesSource = new[] { new GraphEdge("a", "b", id: "ab") };
        var window = new Window { Content = chart, Width = 640, Height = 400 };
        try
        {
            window.Show(); Drain(); Assert.Null(chart.DataError);
            chart.SelectedDataKeys = new object[] { chart.GetMarks().First(mark => mark.Key is RelationGlyphKey { Kind: RelationElementKind.Node, Identity: "a" }).Key! };
            Drain(); Assert.Single(chart.SelectedData);
            var errors = 0;
            chart.PropertyChanged += (_, change) => { if (change.Property == RelationsChartBase.LayoutErrorProperty) errors++; };
            chart.EdgesSource = new[] { new GraphEdge("a", "b", -1, id: "ab") };
            Drain(); var error = chart.DataError; Assert.NotNull(error); Assert.Empty(chart.GetMarks()); Assert.Empty(chart.SelectedData);
            Assert.Equal(RelationLayoutState.Failed, chart.LayoutState); Assert.Same(error, chart.LayoutError); Assert.Equal(1, errors);
            for (var index = 0; index < 5; index++) { Drain(); Assert.Empty(chart.GetMarks()); Assert.Same(error, chart.DataError); Assert.Same(error, chart.LayoutError); }
            Assert.Equal(1, errors);
            chart.EdgesSource = new[] { new GraphEdge("a", "b", id: "ab") };
            Drain(); Assert.Null(chart.DataError); Assert.Null(chart.LayoutError); Assert.Equal(RelationLayoutState.Completed, chart.LayoutState);
            Assert.Equal(3, chart.GetMarks().Count); Assert.Equal("a", Assert.Single(chart.SelectedData).StableId);
            var frame = chart.GetMarks(); Drain(); Assert.Same(frame, chart.GetMarks());
        }
        finally { window.Close(); Drain(); }
        void Drain()
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        }
    }
}
