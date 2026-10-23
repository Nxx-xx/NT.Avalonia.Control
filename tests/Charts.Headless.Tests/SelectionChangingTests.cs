using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class SelectionChangingTests
{
    [AvaloniaFact]
    public void Pointer_keyboard_rectangle_and_clear_can_be_canceled_without_swallowing_point_invocation()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart;
        chart.SelectedPoint = fixture.Hit(0);
        var previous = chart.SelectedPoints;
        var reasons = new List<ChartSelectionChangeReason>();
        var changed = 0; var invoked = 0;
        chart.SelectionChanged += (_, _) => changed++;
        chart.PointInvokedCommand = new Command(_ => invoked++);
        chart.SelectionChanging += (_, args) =>
        {
            Assert.True(args.CanCancel);
            Assert.Same(previous, args.OldSelection);
            reasons.Add(args.Reason); args.Cancel = true;
        };

        fixture.Click(fixture.Point(4, 40));
        chart.Focus(); fixture.Key(Key.Right);
        var rectangle = chart.SelectRectangle(chart.PlotArea);
        chart.ClearSelection();

        Assert.Equal(new[] { ChartSelectionChangeReason.Pointer, ChartSelectionChangeReason.Keyboard,
            ChartSelectionChangeReason.Rectangle, ChartSelectionChangeReason.Clear }, reasons);
        Assert.Same(previous, chart.SelectedPoints);
        Assert.Equal(0, chart.SelectedPoint?.Index);
        Assert.Equal(1, invoked); Assert.Equal(0, changed);
        Assert.True(rectangle.IsCanceled); Assert.Equal(3, rectangle.MatchedCount); Assert.Equal(1, rectangle.SelectedCount);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Canceled_external_two_way_values_restore_the_model_and_leave_the_binding_active(bool multipleProperty)
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; var first = fixture.Hit(0); var second = fixture.Hit(1);
        chart.SelectedPoint = first;
        var model = new SelectionModel { Point = first, Points = chart.SelectedPoints };
        using var binding = multipleProperty
            ? chart.Bind(CartesianChart.SelectedPointsProperty, new Binding(nameof(SelectionModel.Points)) { Source = model, Mode = BindingMode.TwoWay })
            : chart.Bind(CartesianChart.SelectedPointProperty, new Binding(nameof(SelectionModel.Point)) { Source = model, Mode = BindingMode.TwoWay });
        fixture.Drain();
        var old = chart.SelectedPoints;
        var notifications = 0;
        void Cancel(object? sender, ChartSelectionChangingEventArgs args)
        { Assert.Equal(ChartSelectionChangeReason.ExternalValue, args.Reason); notifications++; args.Cancel = true; }
        chart.SelectionChanging += Cancel;

        if (multipleProperty) model.Points = new[] { second }; else model.Point = second;
        fixture.Drain();

        Assert.Equal(1, notifications); Assert.Same(old, chart.SelectedPoints); Assert.Equal(first, chart.SelectedPoint);
        if (multipleProperty) Assert.Same(chart.SelectedPoints, model.Points); else Assert.Equal(first, model.Point);
        chart.SelectionChanging -= Cancel;
        if (multipleProperty) model.Points = new[] { second }; else model.Point = second;
        fixture.Drain();
        Assert.Equal(second.Index, chart.SelectedPoint?.Index);
        chart.ClearSelection(); fixture.Drain();
        if (multipleProperty) Assert.Empty(model.Points!); else Assert.Null(model.Point);
    }

    [AvaloniaTheory]
    [InlineData("remove-point")]
    [InlineData("reset-source")]
    [InlineData("remove-series")]
    [InlineData("hide-series")]
    [InlineData("disable")]
    [InlineData("limit")]
    public void Mandatory_cleanup_cannot_be_vetoed_or_resurrect_an_invalid_identity(string action)
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart;
        chart.SelectRectangle(chart.PlotArea);
        var removed = chart.SelectedPoints![0];
        var events = new List<ChartSelectionChangingEventArgs>();
        chart.SelectionChanging += (_, args) =>
        {
            events.Add(args); args.Cancel = true;
            if (action is "remove-point" or "reset-source" or "remove-series" or "hide-series" or "disable")
                chart.SelectedPoint = removed;
        };

        switch (action)
        {
            case "remove-point": fixture.Values.RemoveAt(0); break;
            case "reset-source": fixture.Values.Clear(); break;
            case "remove-series": chart.Series.Clear(); break;
            case "hide-series": fixture.Series.IsVisible = false; break;
            case "disable": chart.SelectionMode = ChartSelectionMode.None; break;
            case "limit": chart.MaxSelectionCount = 1; break;
        }
        fixture.Drain();

        Assert.NotEmpty(events); Assert.All(events, args => Assert.False(args.CanCancel));
        Assert.All(events, args => Assert.Equal(action is "disable" or "limit" ? ChartSelectionChangeReason.Configuration : ChartSelectionChangeReason.Reconcile, args.Reason));
        if (action == "limit") Assert.Single(chart.SelectedPoints!);
        else Assert.DoesNotContain(chart.SelectedPoints!, point => ReferenceEquals(point.StableId, removed.StableId));
        if (action is not ("remove-point" or "limit")) Assert.Empty(chart.SelectedPoints!);
    }

    [AvaloniaFact]
    public void A_nested_selection_supersedes_the_outer_request_without_recursive_changing_notifications()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; chart.SelectedPoint = fixture.Hit(0);
        var nested = fixture.Hit(2); var changing = 0; var changed = 0;
        chart.SelectionChanging += (_, args) =>
        { changing++; chart.SelectedPoint = nested; args.Cancel = true; };
        chart.SelectionChanged += (_, _) => changed++;

        var result = chart.SelectRectangle(chart.PlotArea);
        fixture.Drain();

        Assert.True(result.IsCanceled); Assert.Equal(1, result.SelectedCount);
        Assert.Equal(1, changing); Assert.Equal(1, changed);
        Assert.Equal(nested, chart.SelectedPoint); Assert.Equal(nested, Assert.Single(chart.SelectedPoints!));
    }

    [AvaloniaFact]
    public void A_callback_removing_the_proposed_point_aborts_the_outer_request_and_keeps_the_old_canonical_point()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; chart.SelectedPoint = fixture.Hit(0);
        var old = chart.SelectedPoint; var proposed = fixture.Hit(1);
        var changing = 0; var changed = 0;
        chart.SelectionChanging += (_, _) => { changing++; fixture.Values.RemoveAt(1); };
        chart.SelectionChanged += (_, _) => changed++;

        chart.SelectedPoint = proposed;
        fixture.Drain();

        Assert.Equal(1, changing); Assert.Equal(0, changed); Assert.Equal(old, chart.SelectedPoint);
        Assert.DoesNotContain(chart.SelectedPoints!, point => ReferenceEquals(point.StableId, proposed.StableId));
    }

    [AvaloniaFact]
    public void No_semantic_change_does_not_raise_changing_and_a_throwing_callback_does_not_poison_later_selection()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; var first = fixture.Hit(0); chart.SelectedPoint = first;
        var changing = 0;
        void Throw(object? sender, ChartSelectionChangingEventArgs args)
        { changing++; throw new InvalidOperationException("business callback"); }
        chart.SelectionChanging += Throw;
        chart.SelectedPoints = new[] { first, first };
        Assert.Equal(0, changing); Assert.Single(chart.SelectedPoints!);
        Assert.Throws<InvalidOperationException>(() => chart.ClearSelection());
        Assert.Equal(1, changing); Assert.Equal(first, chart.SelectedPoint);
        chart.SelectionChanging -= Throw;
        chart.SelectedPoint = fixture.Hit(2);
        Assert.Equal(2, chart.SelectedPoint?.Index);
    }

    [AvaloniaFact]
    public void Reading_layout_in_the_callback_does_not_supersede_a_valid_selection()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; chart.SelectedPoint = fixture.Hit(0);
        var count = 0;
        chart.SelectionChanging += (_, _) => { count++; Assert.True(chart.PlotArea.Width > 100); Assert.NotNull(chart.VisibleRange); };
        chart.SelectedPoint = fixture.Hit(1);
        Assert.Equal(1, count); Assert.Equal(1, chart.SelectedPoint?.Index);
    }

    [AvaloniaFact]
    public void Equal_business_item_replacement_refreshes_original_item_without_a_semantic_event()
    {
        using var fixture = new Fixture();
        var original = new BusinessRow("selected", 2, 20);
        var values = new ObservableCollection<BusinessRow> { original };
        fixture.Series.CategoryPath = nameof(BusinessRow.X); fixture.Series.ValuePath = nameof(BusinessRow.Y);
        fixture.Series.IdPath = nameof(BusinessRow.Id); fixture.Series.ItemsSource = values;
        fixture.Drain();
        fixture.Chart.SelectedPoint = fixture.Chart.HitTestPoint(fixture.Point(2, 20));
        Assert.Same(original, fixture.Chart.SelectedPoint!.Item);
        var changing = 0; var changed = 0;
        fixture.Chart.SelectionChanging += (_, _) => changing++;
        fixture.Chart.SelectionChanged += (_, _) => changed++;
        var replacement = new BusinessRow("selected", 2, 20);
        Assert.Equal(original, replacement); Assert.NotSame(original, replacement);

        values[0] = replacement;
        fixture.Drain();

        Assert.Same(replacement, fixture.Chart.SelectedPoint!.Item);
        Assert.Same(replacement, Assert.Single(fixture.Chart.SelectedPoints!).Item);
        Assert.Equal(0, changing); Assert.Equal(0, changed);
    }

    private sealed record BusinessRow(string Id, double X, double Y);

    private sealed class Fixture : IDisposable
    {
        public ObservableCollection<ChartPoint> Values { get; } = [new(2, 20), new(4, 40), new(6, 60)];
        public ScatterSeries Series { get; }
        public CartesianChart Chart { get; }
        public Window Window { get; }
        public Fixture()
        {
            Series = new ScatterSeries { ItemsSource = Values };
            Chart = new CartesianChart
            {
                ShowLegend = false, SelectionMode = ChartSelectionMode.Multiple, IsKeyboardNavigationEnabled = true,
                XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            };
            Chart.Series.Add(Series);
            Window = new Window { Width = 760, Height = 460, Content = Chart }; Window.Show(); Drain();
        }
        public Point Point(double x, double y) => new(Chart.PlotArea.Left + x / 10 * Chart.PlotArea.Width,
            Chart.PlotArea.Bottom - y / 100 * Chart.PlotArea.Height);
        public ChartPointHit Hit(int index) => Chart.HitTestPoint(Point(Values[index].X, Values[index].Y))!;
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); using var drawing = new DrawingGroup().Open(); Chart.Render(drawing); }
        public void Click(Point point)
        { var position = Chart.TranslatePoint(point, Window)!.Value; Window.MouseDown(position, MouseButton.Left); Window.MouseUp(position, MouseButton.Left); Drain(); }
        public void Key(Key key)
        { Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null); Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null); Drain(); }
        public void Dispose() => Window.Close();
    }

    private sealed class Command(Action<object?> action) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
    private sealed class SelectionModel : INotifyPropertyChanged
    {
        private ChartPointHit? _point;
        private IReadOnlyList<ChartPointHit>? _points;
        public ChartPointHit? Point { get => _point; set { if (Equals(_point, value)) return; _point = value; Changed(); } }
        public IReadOnlyList<ChartPointHit>? Points { get => _points; set { if (ReferenceEquals(_points, value)) return; _points = value; Changed(); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    }
}
