namespace NT.AvaloniaControl.Demo.Navigation;

internal sealed record DemoPageDefinition(string Id, string Title, string Description, Func<IDemoPage> CreatePage)
{
    public override string ToString() => Title;
}
