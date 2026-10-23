using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class GaugeReentrancyTests
{
    [AvaloniaTheory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)]
    public void Inherited_data_binding_can_replace_clear_or_unload_without_reviving_old_item_observers(bool rings, int action)
    {
        var row = new Row { Reading = 77 };
        var window = new Window { Width = 500, Height = 400 };
        GaugeBase chart; StyledElement oldItem, replacement; AvaloniaProperty boundProperty;
        Action replaceSource, clearSource; Func<int> oldObserverCount, replacementObserverCount;
        if (rings)
        {
            var first = new GaugeValue(); first.Bind(GaugeValue.ValueProperty, new Binding(nameof(Row.Reading)));
            var second = new GaugeValue { Value = 25 }; var source = new ObservableCollection<GaugeValue> { first };
            var ringChart = new GradientRingChart { ValuesSource = source };
            chart = ringChart; oldItem = first; replacement = second; boundProperty = GaugeValue.ValueProperty;
            replaceSource = () => ringChart.ValuesSource = new ObservableCollection<GaugeValue> { second }; clearSource = source.Clear;
            oldObserverCount = () => first.ChangedObserverCount; replacementObserverCount = () => second.ChangedObserverCount;
        }
        else
        {
            var first = new GaugeRange(); first.Bind(GaugeRange.EndProperty, new Binding(nameof(Row.Reading)));
            var second = new GaugeRange { End = 25 }; var source = new ObservableCollection<GaugeRange> { first };
            var gaugeChart = new CircularGaugeChart { RangesSource = source };
            chart = gaugeChart; oldItem = first; replacement = second; boundProperty = GaugeRange.EndProperty;
            replaceSource = () => gaugeChart.RangesSource = new ObservableCollection<GaugeRange> { second }; clearSource = source.Clear;
            oldObserverCount = () => first.ChangedObserverCount; replacementObserverCount = () => second.ChangedObserverCount;
        }
        chart.Width = 420; chart.Height = 280; chart.DataContext = row;
        var triggered = false;
        oldItem.PropertyChanged += (_, change) =>
        {
            if (triggered || change.Property != boundProperty || !ReferenceEquals(oldItem.DataContext, row)) return;
            triggered = true;
            if (action == 0) replaceSource(); else if (action == 1) clearSource(); else window.Content = null;
        };
        try
        {
            window.Content = chart; window.Show(); Drain(window);
            Assert.True(triggered); Assert.Null(((ILogical)oldItem).LogicalParent); Assert.Equal(0, oldObserverCount()); Assert.Equal(0, row.Observers);
            if (action == 0) { Assert.Same(chart, ((ILogical)replacement).LogicalParent); Assert.Equal(1, replacementObserverCount()); }
            else { Assert.Null(((ILogical)replacement).LogicalParent); Assert.Equal(0, replacementObserverCount()); }
            if (action == 2)
            {
                Assert.Null(window.Content);
                window.Content = chart; Drain(window); Assert.Same(chart, ((ILogical)oldItem).LogicalParent); Assert.Equal(1, oldObserverCount()); Assert.True(row.Observers > 0);
            }
            window.Content = null; Drain(window);
            Assert.Null(((ILogical)oldItem).LogicalParent); Assert.Null(((ILogical)replacement).LogicalParent);
            Assert.Equal(0, oldObserverCount()); Assert.Equal(0, replacementObserverCount()); Assert.Equal(0, row.Observers);
        }
        finally { window.Content = null; window.Close(); Dispatcher.UIThread.RunJobs(); }
    }

    [AvaloniaFact]
    public void A_liquid_gauge_unloaded_by_inherited_binding_during_attach_does_not_start_a_late_wave_timer()
    {
        var row = new Row { Reading = 77 }; var range = new GaugeRange(); range.Bind(GaugeRange.EndProperty, new Binding(nameof(Row.Reading)));
        var chart = new LiquidFillGaugeChart { Width = 420, Height = 280, Value = 50, WavePhase = .3, IsWaveAnimationEnabled = true, DataContext = row, RangesSource = new ObservableCollection<GaugeRange> { range } };
        var window = new Window { Width = 500, Height = 400 }; var triggered = false;
        range.PropertyChanged += (_, change) =>
        {
            if (triggered || change.Property != GaugeRange.EndProperty || !ReferenceEquals(range.DataContext, row)) return;
            triggered = true; window.Content = null;
        };
        try
        {
            window.Content = chart; window.Show(); Drain(window);
            Assert.True(triggered); Assert.Null(window.Content); Assert.False(chart.IsWaveTimerRunning); Assert.Null(((ILogical)range).LogicalParent); Assert.Equal(0, range.ChangedObserverCount); Assert.Equal(0, row.Observers);
            chart.AdvanceWave(TimeSpan.FromSeconds(.5)); Assert.Equal(.3, chart.WavePhase);
            chart.IsWaveAnimationEnabled = false; chart.IsWaveAnimationEnabled = true; Assert.False(chart.IsWaveTimerRunning);
            window.Content = chart; Drain(window); Assert.True(chart.IsWaveTimerRunning); Assert.Equal(1, range.ChangedObserverCount);
            window.Content = null; Drain(window); Assert.False(chart.IsWaveTimerRunning); Assert.Equal(0, range.ChangedObserverCount); Assert.Equal(0, row.Observers);
        }
        finally { window.Content = null; window.Close(); Dispatcher.UIThread.RunJobs(); }
    }

    private static void Drain(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private sealed class Row : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public int Observers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { _changed += value; Observers++; } remove { _changed -= value; Observers--; } }
        public double Reading { get; set; }
    }
}
