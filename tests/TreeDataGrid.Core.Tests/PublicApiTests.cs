using System.Reflection;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Core.Tests;

public class PublicApiTests
{
    [Fact]
    public void PublicSurfaceMatchesReviewedContract()
    {
        var root = FindRoot();
        var path = Path.Combine(root, "docs", "public-api.txt");
        var actual = string.Join('\n', GetApiLines()) + "\n";
        if (Environment.GetEnvironmentVariable("TREEGRID_UPDATE_API") == "1")
            File.WriteAllText(path, actual);
        Assert.True(File.Exists(path), "The API snapshot must be generated and reviewed before opening the integration gate.");
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), actual);
    }

    [Fact]
    public void P1PublicAndProtectedMembersRemainAvailable()
    {
        var baseline = ReadHistoricalApi("p1-public-api.txt");
        var current = QualifiedLines(GetApiLines()).ToHashSet(StringComparer.Ordinal);
        var missing = QualifiedLines(baseline).Where(line => !current.Contains(line)).ToArray();
        Assert.True(missing.Length == 0, "P1 public contracts changed:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void P2P3PublicAndProtectedMembersRemainAvailable()
    {
        var baseline = ReadHistoricalApi("p2-p3-public-api.txt");
        var current = QualifiedLines(GetApiLines()).ToHashSet(StringComparer.Ordinal);
        var missing = QualifiedLines(baseline).Where(line => !current.Contains(line)).ToArray();
        Assert.True(missing.Length == 0, "P2/P3 public contracts changed:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void P4P5PublicAndProtectedMembersRemainAvailable()
    {
        var baseline = ReadHistoricalApi("p4-p5-public-api.txt");
        var current = QualifiedLines(GetApiLines()).ToHashSet(StringComparer.Ordinal);
        var missing = QualifiedLines(baseline).Where(line => !current.Contains(line)).ToArray();
        Assert.True(missing.Length == 0, "P4/P5 public contracts changed:\n" + string.Join('\n', missing));
    }

    private static IEnumerable<string> ReadHistoricalApi(string name) =>
        File.ReadLines(Path.Combine(FindRoot(), "docs", "validation", name))
            .Select(line => line.Replace("NT.AvaloniaControl.", "NT.AvaloniaControl.TreeDataGrid.", StringComparison.Ordinal));

    private static IEnumerable<string> QualifiedLines(IEnumerable<string> lines)
    {
        var declaringType = "";
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("  ", StringComparison.Ordinal)) declaringType = line;
            yield return declaringType + " | " + line;
        }
    }

    private static IReadOnlyList<string> GetApiLines()
    {
        var lines = new List<string>();
        foreach (var type in typeof(TreeDataGridSource).Assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            lines.Add($"{type.FullName} : {type.BaseType} [abstract={type.IsAbstract}, sealed={type.IsSealed}]");
            foreach (var argument in type.GetGenericArguments())
                lines.Add($"  Constraint: {argument.Name}: {argument.GenericParameterAttributes}");
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var member in type.GetMembers(flags).Where(IsApi).Select(Describe).Order(StringComparer.Ordinal))
                lines.Add(member);
        }
        return lines;
    }

    private static bool Visible(MethodBase? method) => method is not null &&
        (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);

    private static bool IsApi(MemberInfo member) => member switch
    {
        ConstructorInfo constructor => Visible(constructor),
        PropertyInfo property => Visible(property.GetMethod) || Visible(property.SetMethod),
        EventInfo @event => Visible(@event.AddMethod),
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        MethodInfo method => Visible(method) && (!method.IsSpecialName || method.Name.StartsWith("op_", StringComparison.Ordinal)),
        _ => false,
    };

    private static string Describe(MemberInfo member)
    {
        var suffix = member switch
        {
            PropertyInfo property => $" [get={Access(property.GetMethod)}, set={Access(property.SetMethod)}]",
            MethodBase method => $" [{Access(method)}, abstract={method.IsAbstract}, virtual={method.IsVirtual}]",
            FieldInfo { IsLiteral: true } field => $" = {field.GetRawConstantValue()}",
            _ => "",
        };
        return $"  {member.MemberType}: {member}{suffix}";
    }

    private static string Access(MethodBase? method) => method is null ? "none" : method.IsPublic ? "public" :
        method.IsFamily || method.IsFamilyOrAssembly ? "protected" : "non-public";

    [Fact]
    public void CoreModuleHasNoVisualDependenciesAndProductHasNoCommercialGridDependency()
    {
        var root = FindRoot();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "NT.AvaloniaControl.TreeDataGrid", "Core"), "*.cs"))
            Assert.DoesNotContain("using Avalonia", File.ReadAllText(file));
        var references = typeof(TreeDataGridSource).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, r => r.Name == "NT.AvaloniaControl.TreeDataGrid.Themes" || r.Name!.Contains("TreeDataGrid"));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NT.AvaloniaControl.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Cannot locate repository contract snapshot.");
    }
}
