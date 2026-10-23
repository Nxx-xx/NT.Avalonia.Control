using System.Reflection;
using NT.AvaloniaControl.VirtualKeyboard;

namespace VirtualKeyboard.Core.Tests;

public sealed class PublicApiTests
{
    [Fact]
    public void PublicSurfaceMatchesReviewedContract()
    {
        var path = Path.Combine(FindRoot(), "docs", "validation", "virtualkeyboard-public-api.txt");
        var actual = string.Join('\n', GetApiLines()) + "\n";
        if (Environment.GetEnvironmentVariable("VIRTUALKEYBOARD_UPDATE_API") == "1") File.WriteAllText(path, actual);
        Assert.True(File.Exists(path), "The VirtualKeyboard public API must have a reviewed snapshot.");
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), actual);
    }
    private static IEnumerable<string> GetApiLines()
    {
        foreach (var type in typeof(VirtualKeyboardKey).Assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            yield return $"{type.FullName} : {type.BaseType} [abstract={type.IsAbstract}, sealed={type.IsSealed}]";
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var member in type.GetMembers(flags).Where(IsApi).Select(Describe).Order(StringComparer.Ordinal)) yield return member;
        }
    }
    private static bool Visible(MethodBase? method) => method is not null && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);
    private static bool IsApi(MemberInfo member) => member switch
    {
        ConstructorInfo constructor => Visible(constructor),
        PropertyInfo property => Visible(property.GetMethod) || Visible(property.SetMethod),
        EventInfo @event => Visible(@event.AddMethod),
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        MethodInfo method => Visible(method) && !method.IsSpecialName,
        _ => false
    };
    private static string Describe(MemberInfo member)
    {
        var suffix = member switch
        {
            PropertyInfo property => $" [get={Access(property.GetMethod)}, set={Access(property.SetMethod)}]",
            MethodBase method => $" [{Access(method)}, abstract={method.IsAbstract}, virtual={method.IsVirtual}]",
            FieldInfo { IsLiteral: true } field => $" = {field.GetRawConstantValue()}",
            _ => string.Empty
        };
        return $"  {member.MemberType}: {member}{suffix}";
    }
    private static string Access(MethodBase? method) => method is null ? "none" : method.IsPublic ? "public" :
        method.IsFamily || method.IsFamilyOrAssembly ? "protected" : "non-public";
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NT.AvaloniaControl.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
