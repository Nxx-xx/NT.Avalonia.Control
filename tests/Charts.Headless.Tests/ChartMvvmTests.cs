using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartMvvmTests
{
    [AvaloniaFact]
    public void Compiled_nested_bindings_inherit_context_and_resources_and_follow_context_replacement()
    {
        var first = new ChartBindingModel();
        var second = new ChartBindingModel { CategoryTitle = "Replacement categories", ValueTitle = "Replacement values", SeriesTitle = "Replacement series" };
        var view = new MvvmConsumerView { DataContext = first };
        using var fixture = new BindingFixture(view);
        var chart = view.FindControl<CartesianChart>("Declared")!;
        var line = Assert.IsType<LineSeries>(Assert.Single(chart.Series));
        Assert.Same(first.Records, line.ItemsSource);
        Assert.Equal(first.SeriesTitle, line.Title);
        Assert.Equal(first.CategoryTitle, chart.XAxis.Title);
        Assert.Equal(first.ValueTitle, chart.YAxis.Title);
        Assert.Equal(Color.Parse("#1234AB"), Assert.IsAssignableFrom<ISolidColorBrush>(line.Stroke).Color);

        first.ValueTitle = "Changed axis";
        first.SeriesTitle = "Changed line";
        fixture.Drain();
        Assert.Equal("Changed axis", chart.YAxis.Title);
        Assert.Equal("Changed line", line.Title);
        view.Resources["BoundSeriesBrush"] = Brushes.Orange;
        fixture.Drain();
        Assert.Equal(Colors.Orange, Assert.IsAssignableFrom<ISolidColorBrush>(line.Stroke).Color);

        view.DataContext = second;
        fixture.Drain();
        Assert.Same(second.Records, line.ItemsSource);
        Assert.Equal(second.SeriesTitle, line.Title);
        Assert.Equal(second.ValueTitle, chart.YAxis.Title);
        Assert.All(first.Records, record => Assert.Equal(0, record.SubscriberCount));
        var range = chart.VisibleRange;
        first.Records[0].Value = 10_000;
        fixture.Drain();
        Assert.Equal(range, chart.VisibleRange);
    }

    [AvaloniaFact]
    public void Business_item_notifications_replaceable_series_and_viewport_two_way_binding_remain_live()
    {
        var model = new ChartBindingModel();
        var view = new MvvmConsumerView { DataContext = model };
        using var fixture = new BindingFixture(view);
        var declared = view.FindControl<CartesianChart>("Declared")!;
        var sourced = view.FindControl<CartesianChart>("Sourced")!;
        Assert.Same(model.SeriesSource, sourced.SeriesSource);
        var initial = Assert.IsType<ChartViewport>(declared.VisibleRange);
        model.Records[0].Value = 500;
        fixture.Drain();
        Assert.True(Assert.IsType<ChartViewport>(declared.VisibleRange).YMaximum > initial.YMaximum);
        var oldSeries = model.SeriesSource;
        model.SeriesSource = [new BarSeries { ItemsSource = model.Records, CategoryPath = nameof(ChartBindingRecord.Category), ValuePath = nameof(ChartBindingRecord.Value), IdPath = nameof(ChartBindingRecord.Id) }];
        fixture.Drain();
        Assert.NotSame(oldSeries, sourced.SeriesSource);
        Assert.Same(model.SeriesSource, sourced.SeriesSource);

        var requested = new ChartViewport(.5, 2.5, 10, 80);
        model.Viewport = requested;
        fixture.Drain();
        Assert.Equal(requested, declared.Viewport);
        Assert.Equal(requested, sourced.Viewport);
        var fromControl = new ChartViewport(.75, 2.25, 20, 70);
        declared.SetCurrentValue(CartesianChart.ViewportProperty, fromControl);
        fixture.Drain();
        Assert.Equal(fromControl, model.Viewport);
        Assert.Equal(fromControl, sourced.Viewport);
        sourced.ResetViewportCommand.Execute(null);
        fixture.Drain();
        Assert.Null(model.Viewport);
        Assert.Null(declared.Viewport);
        Assert.Null(sourced.Viewport);
        var range = Assert.IsType<ChartViewport>(declared.VisibleRange);
        var plot = declared.PlotArea;
        var position = new Point(plot.Left + (1 - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (model.Records[1].Value - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
        var hit = Assert.IsType<ChartPointHit>(declared.HitTestPoint(position));
        declared.SetCurrentValue(CartesianChart.SelectedPointProperty, hit);
        fixture.Drain();
        Assert.Same(hit, model.SelectedPoint);
        Assert.Same(model.PointInvokedCommand, declared.PointInvokedCommand);
        Assert.IsAssignableFrom<ICommand>(declared.PointInvokedCommand).Execute(hit);
        Assert.Same(hit, model.LastInvokedPoint);

        fixture.Window.Content = null;
        fixture.Drain();
        Assert.All(model.Records, record => Assert.Equal(0, record.SubscriberCount));
        fixture.Window.Content = view;
        fixture.Drain();
        Assert.All(model.Records, record => Assert.True(record.SubscriberCount > 0));
    }

    private sealed class BindingFixture : IDisposable
    {
        public BindingFixture(Control view) { Window = new Window { Width = 1000, Height = 450, Content = view }; Window.Show(); Drain(); }
        public Window Window { get; }
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() => Window.Close();
    }
}

public sealed partial class MvvmConsumerView : UserControl
{
    public MvvmConsumerView() => AvaloniaXamlLoader.Load(this);
}

public sealed class ChartBindingModel : INotifyPropertyChanged
{
    private ChartViewport? _viewport;
    private ChartPointHit? _selectedPoint;
    private ObservableCollection<ChartSeries> _seriesSource;
    private string _categoryTitle = "Category";
    private string _valueTitle = "Value";
    private string _seriesTitle = "Series";
    public ChartBindingModel()
    {
        Records = [new("a", "Alpha", 10), new("b", "Beta", 20), new("c", "Gamma", 30), new("d", "Delta", 40)];
        _seriesSource = [new ScatterSeries { ItemsSource = Records, CategoryPath = nameof(ChartBindingRecord.Category), ValuePath = nameof(ChartBindingRecord.Value), IdPath = nameof(ChartBindingRecord.Id) }];
        PointInvokedCommand = new ChartBindingCommand(parameter => LastInvokedPoint = parameter as ChartPointHit);
    }
    public ObservableCollection<ChartBindingRecord> Records { get; }
    public ObservableCollection<ChartSeries> SeriesSource { get => _seriesSource; set { _seriesSource = value; Changed(); } }
    public ChartViewport? Viewport { get => _viewport; set { if (_viewport == value) return; _viewport = value; Changed(); } }
    public ChartPointHit? SelectedPoint { get => _selectedPoint; set { _selectedPoint = value; Changed(); } }
    public ChartPointHit? LastInvokedPoint { get; private set; }
    public ICommand PointInvokedCommand { get; }
    public string CategoryTitle { get => _categoryTitle; set { _categoryTitle = value; Changed(); } }
    public string ValueTitle { get => _valueTitle; set { _valueTitle = value; Changed(); } }
    public string SeriesTitle { get => _seriesTitle; set { _seriesTitle = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

internal sealed class ChartBindingCommand(Action<object?> execute) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}

public sealed class ChartBindingRecord(string id, string category, double value) : INotifyPropertyChanged
{
    private double _value = value;
    private PropertyChangedEventHandler? _changed;
    public string Id { get; } = id;
    public string Category { get; } = category;
    public double Value { get => _value; set { if (_value == value) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
