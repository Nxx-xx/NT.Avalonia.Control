using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class CategoryLabelOwnershipTests
{
    [AvaloniaFact]
    public void Category_views_share_one_owned_label_copy_and_do_not_revisit_the_caller_list()
    {
        var source = new CountingLabels(100_000);
        var axis = new CategoryAxis { Labels = source };
        var full = ChartScale.Create(0, 99_999, axis, paddingFraction: 0);
        var owned = Assert.IsAssignableFrom<IReadOnlyList<string>>(full.CategoryLabels);
        Assert.NotSame(source, owned); Assert.Equal(100_000, source.Reads);
        Assert.False(owned is string[]); Assert.False(owned is IList<string>);
        for (var view = 0; view < 30; view++)
        {
            var slice = ChartScale.CreateForView(axis, view * 100, view * 100 + 50, full.CategoryLabels);
            Assert.Same(owned, slice.CategoryLabels);
            Assert.Equal($"Label {view * 100}", slice.FormatLabel(view * 100));
        }
        Assert.Equal(100_000, source.Reads);
    }

    [AvaloniaFact]
    public void A_new_scale_clones_changed_caller_labels_while_existing_views_retain_their_snapshot()
    {
        var labels = new List<string> { "Original A", "Original B", "Original C" };
        var axis = new CategoryAxis { Labels = labels };
        var full = ChartScale.Create(0, 2, axis, paddingFraction: 0);
        labels[1] = "Updated B";
        var retained = ChartScale.CreateForView(axis, 0, 2, full.CategoryLabels);
        var fresh = ChartScale.CreateForView(axis, 0, 2);
        Assert.Equal("Original B", full.FormatLabel(1));
        Assert.Equal("Original B", retained.FormatLabel(1));
        Assert.Equal("Updated B", fresh.FormatLabel(1));
        Assert.Same(full.CategoryLabels, retained.CategoryLabels);
        Assert.NotSame(full.CategoryLabels, fresh.CategoryLabels);
    }

    [AvaloniaFact]
    public void Explicit_category_labels_keep_defensive_ownership_independent_of_axis_labels()
    {
        string[] explicitLabels = ["Projected A", "Projected B"];
        var axis = new CategoryAxis { Labels = new[] { "Axis A", "Axis B" } };
        var full = ChartScale.Create(0, 1, axis, paddingFraction: 0, categoryLabels: explicitLabels);
        explicitLabels[0] = "External mutation";
        var slice = ChartScale.CreateForView(axis, 0, 1, full.CategoryLabels);
        Assert.Equal("Projected A", slice.FormatLabel(0));
        Assert.Same(full.CategoryLabels, slice.CategoryLabels);
        var fresh = ChartScale.CreateForView(axis, 0, 1, explicitLabels);
        Assert.Equal("External mutation", fresh.FormatLabel(0));
    }

    private sealed class CountingLabels(int count) : IReadOnlyList<string>
    {
        public int Reads;
        public int Count => count;
        public string this[int index] { get { Reads++; return $"Label {index}"; } }
        public IEnumerator<string> GetEnumerator()
        { for (var index = 0; index < count; index++) yield return this[index]; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
