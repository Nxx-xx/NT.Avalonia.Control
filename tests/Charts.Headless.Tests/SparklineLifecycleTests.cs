using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class SparklineLifecycleTests(ITestOutputHelper output)
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Inherited_source_binding_can_detach_reattach_and_replace_source_without_retaining_observers(bool reattach, bool replace)
    {
        var original = new Reading("same", 20);
        var replacement = new Reading("same", 80);
        var initialSource = new ObservableCollection<Reading> { original };
        var replacementSource = new ObservableCollection<Reading> { replacement };
        var model = new Model(initialSource);
        var chart = new SparklineChart
        {
            Width = 300, Height = 100, Padding = new(0), DataContext = model,
            Minimum = 0, Maximum = 100, ValuePath = nameof(Reading.Value), IdPath = nameof(Reading.Id)
        };
        // This is an inherited DataContext binding on the public Series, rather than
        // an explicit Source binding that could remain alive independently of ownership.
        chart.Series.Bind(ChartSeries.ItemsSourceProperty, new Binding(nameof(Model.Items)));
        var window = new Window { Width = 300, Height = 100 };
        var changed = false;
        var mountingReturned = false;
        var callbackWasSynchronous = false;
        var callbackInheritedContext = false;
        var callbackCompleted = false;
        Exception? callbackFailure = null;
        void State(string stage) => output.WriteLine($"{stage}: content={ReferenceEquals(window.Content, chart)}, visual={chart.IsAttachedToVisualTree()}, logical={ReferenceEquals(((ILogical)chart.Series).LogicalParent, chart)}, sourceInitial={ReferenceEquals(chart.Series.ItemsSource, initialSource)}, sourceReplacement={ReferenceEquals(chart.Series.ItemsSource, replacementSource)}, originalObservers={original.Observers}, replacementObservers={replacement.Observers}, modelObservers={model.Observers}");
        chart.AttachedToVisualTree += (_, _) => State("visual attached");
        chart.DetachedFromVisualTree += (_, _) => State("visual detached");
        chart.Series.PropertyChanged += (_, e) =>
        {
            if (changed || e.Property != ChartSeries.ItemsSourceProperty || !ReferenceEquals(chart.Series.ItemsSource, initialSource)) return;
            changed = true;
            State("binding before mutation");
            callbackWasSynchronous = !mountingReturned;
            callbackInheritedContext = ReferenceEquals(chart.Series.DataContext, model);
            // In Avalonia the inherited binding runs before LogicalParent is assigned.
            // Capture callback errors explicitly: the binding engine otherwise reports
            // them as a binding error, hiding a mutation that never actually completed.
            try
            {
                window.Content = null;
                State("binding after detach");
                if (replace) model.Items = replacementSource;
                if (reattach) window.Content = chart;
                State("binding after final mutation");
                callbackCompleted = true;
            }
            catch (Exception exception) { callbackFailure = exception; }
        };
        try
        {
            window.Content = chart;
            window.Show();
            mountingReturned = true;
            Drain(window);
            State("after mount drain");
            Assert.True(changed); Assert.True(callbackWasSynchronous); Assert.True(callbackInheritedContext);
            Assert.Null(callbackFailure); Assert.True(callbackCompleted);
            var current = replace ? replacement : original;
            if (reattach)
            {
                Assert.Same(chart, window.Content); Assert.True(chart.IsAttachedToVisualTree());
                Assert.Same(chart, ((ILogical)chart.Series).LogicalParent);
                Assert.Equal(1, current.Observers); Assert.Equal(1, model.Observers);
                Assert.Same(current, Assert.Single(chart.GetPoints()).Hit.Item);
                Key(chart, Avalonia.Input.Key.Home); Assert.Same(current, chart.ActivePoint?.Item);
                current.Value = 60; Drain(window); Assert.Equal(60, chart.ActivePoint?.Point.Y);
                window.Content = null; Drain(window);
            }
            Assert.Null(window.Content); Assert.False(chart.IsAttachedToVisualTree());
            Assert.Null(((ILogical)chart.Series).LogicalParent); Assert.Null(chart.ActivePoint);
            Assert.Equal(0, original.Observers); Assert.Equal(0, replacement.Observers); Assert.Equal(0, model.Observers);

            // A second ordinary attach/detach also exposes a duplicated Series.Changed
            // subscription: removing one handler would leave the business source observed.
            window.Content = chart; Drain(window);
            Assert.True(chart.IsAttachedToVisualTree()); Assert.Equal(1, current.Observers); Assert.Equal(1, model.Observers);
            Key(chart, Avalonia.Input.Key.Home); Assert.Same(current, chart.ActivePoint?.Item);
            current.Value = 35; Drain(window); Assert.Equal(35, chart.ActivePoint?.Point.Y);
            window.Content = null; Drain(window);
            Assert.Null(chart.ActivePoint); Assert.Null(((ILogical)chart.Series).LogicalParent);
            Assert.Equal(0, original.Observers); Assert.Equal(0, replacement.Observers); Assert.Equal(0, model.Observers);
        }
        finally { window.Close(); Dispatcher.UIThread.RunJobs(); }
    }

    private static void Drain(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Key(SparklineChart chart, Key key) => chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    private sealed class Model(ObservableCollection<Reading> items) : INotifyPropertyChanged
    {
        private ObservableCollection<Reading> _items = items;
        private PropertyChangedEventHandler? _changed;
        public int Observers { get; private set; }
        public ObservableCollection<Reading> Items { get => _items; set { _items = value; _changed?.Invoke(this, new(nameof(Items))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }

    private sealed class Reading(string id, double value) : INotifyPropertyChanged
    {
        private double _value = value;
        private PropertyChangedEventHandler? _changed;
        public string Id { get; } = id;
        public int Observers { get; private set; }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
}
