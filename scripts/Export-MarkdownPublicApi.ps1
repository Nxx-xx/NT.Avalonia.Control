param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$markdownApiRepo = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $markdownApiRepo 'docs/validation/markdown-public-api.txt' }
$markdownApiWork = Join-Path $markdownApiRepo ('artifacts/markdown-api-helper/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $markdownApiWork -Force | Out-Null
foreach ($markdownApiBoundary in @('Directory.Build.props', 'Directory.Packages.props')) {
    Set-Content -LiteralPath (Join-Path $markdownApiWork $markdownApiBoundary) -Value '<Project />' -Encoding utf8
}
$markdownApiVersionXml = [xml](Get-Content -Raw -LiteralPath (Join-Path $markdownApiRepo 'Directory.Packages.props'))
function Get-MarkdownApiPackageVersion([string]$Name) {
    return [string]($markdownApiVersionXml.Project.ItemGroup.PackageVersion | Where-Object { $_.Include -eq $Name }).Version
}
$markdownApiReferences = foreach ($markdownApiName in @('NT.AvaloniaControl.Markdown', 'NT.AvaloniaControl.Markdown.Themes', 'NT.AvaloniaControl.Markdown.TextMate')) {
    $markdownApiDll = Join-Path $markdownApiRepo "src/$markdownApiName/bin/Release/net10.0/$markdownApiName.dll"
    if (-not (Test-Path -LiteralPath $markdownApiDll)) { throw "Build Release before API export: $markdownApiDll" }
    '<Reference Include="' + $markdownApiName + '"><HintPath>' + [Security.SecurityElement]::Escape($markdownApiDll) + '</HintPath></Reference>'
}
$markdownApiPackageReferences = foreach ($markdownApiPackage in @('Avalonia.Headless', 'Avalonia.Themes.Fluent', 'Markdig', 'TextMateSharp', 'TextMateSharp.Grammars')) {
    $markdownApiVersion = if ($markdownApiPackage -eq 'Avalonia.Headless') { Get-MarkdownApiPackageVersion 'Avalonia' } else { Get-MarkdownApiPackageVersion $markdownApiPackage }
    '<PackageReference Include="' + $markdownApiPackage + '" Version="' + $markdownApiVersion + '" />'
}
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
  <ItemGroup>__REFERENCES__</ItemGroup>
</Project>
'@.Replace('__REFERENCES__', (($markdownApiReferences + $markdownApiPackageReferences) -join [Environment]::NewLine)) |
    Set-Content -LiteralPath (Join-Path $markdownApiWork 'ApiExport.csproj') -Encoding utf8
@'
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using NT.AvaloniaControl.Markdown.Themes;
using NT.AvaloniaControl.Markdown.TextMate;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

AppBuilder.Configure<ApiApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
var output = new StringBuilder();
output.AppendLine("Markdown public contract snapshot: actual Release assemblies, public declared members.");
output.AppendLine("Record generated members are retained; inherited members and internal implementation are excluded.");
output.AppendLine("Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
var assemblies = new[] { typeof(Reader).Assembly, typeof(MarkdownTheme).Assembly, typeof(TextMateCodeHighlighter).Assembly };
const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
foreach (var assembly in assemblies)
{
    output.AppendLine();
    output.AppendLine("Assembly: " + assembly.FullName);
    output.AppendLine("SHA256: " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))));
    foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
    {
        output.AppendLine($"{type.FullName} : {TypeName(type.BaseType)} [abstract={type.IsAbstract}, sealed={type.IsSealed}]");
        foreach (var constructor in type.GetConstructors(flags).OrderBy(member => member.ToString(), StringComparer.Ordinal))
            output.AppendLine("  Constructor: " + constructor + Parameters(constructor.GetParameters()));
        foreach (var field in type.GetFields(flags).OrderBy(member => member.Name, StringComparer.Ordinal))
            output.AppendLine("  Field: " + field + (field.IsLiteral ? " = " + Value(field.GetRawConstantValue()) : "") +
                $" [static={field.IsStatic}, readonly={field.IsInitOnly}]");
        foreach (var property in type.GetProperties(flags).OrderBy(member => member.Name, StringComparer.Ordinal))
            output.AppendLine("  Property: " + property + $" [get={Access(property.GetMethod)}, set={Access(property.SetMethod)}, init={property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) == true}]");
        foreach (var method in type.GetMethods(flags).Where(method => !method.IsSpecialName).OrderBy(member => member.ToString(), StringComparer.Ordinal))
            output.AppendLine("  Method: " + method + Parameters(method.GetParameters()) +
                $" [static={method.IsStatic}, abstract={method.IsAbstract}, virtual={method.IsVirtual}]");
        foreach (var eventInfo in type.GetEvents(flags).OrderBy(member => member.Name, StringComparer.Ordinal))
            output.AppendLine("  Event: " + eventInfo);
    }
}

output.AppendLine();
output.AppendLine("Unattached control defaults (observed before template/theme attachment):");
var reader = new Reader();
foreach (var propertyName in new[] { "Text", "SelectionEnabled", "AutoScrollToEnd", "ImageLoader", "CodeHighlighter", "BaseUri", "ElementServices", "NodeRenderers", "SelectedText", "CanCopy", "SelectionBrush", "CaretBrush", "ActiveParseCount", "PendingRequestCount", "ActiveResourceRequestCount", "ExecutingResourceRequestCount", "PendingResourceRequestCount" })
    output.AppendLine("  " + propertyName + " = " + Value(typeof(Reader).GetProperty(propertyName)!.GetValue(reader)));
output.AppendLine("  NodeRenderers default is Empty = " + ReferenceEquals(reader.NodeRenderers, MarkdownRendererRegistry.Empty));
output.AppendLine("  NodeRenderers registration counts = " + reader.NodeRenderers.Blocks.Count + " / " + reader.NodeRenderers.Inlines.Count);

var theme = new MarkdownTheme();
var window = new Window { Width = 640, Height = 350, Content = reader };
window.Show();
window.UpdateLayout();
output.AppendLine();
output.AppendLine("Default-theme runtime template parts (visuals whose TemplatedParent is this Markdown):");
foreach (var child in reader.GetVisualDescendants().OfType<Control>().Where(control => ReferenceEquals(control.TemplatedParent, reader) && control.Name?.StartsWith("PART_", StringComparison.Ordinal) == true).OrderBy(control => control.Name, StringComparer.Ordinal))
    output.AppendLine("  " + child.Name + " : " + child.GetType().FullName);
output.AppendLine("  HasPresentationEnvironment = " + reader.HasPresentationEnvironment);
if (!reader.HasPresentationEnvironment) throw new InvalidOperationException("Default packaged theme did not create the required presentation environment.");
output.AppendLine("TemplatePart attributes declared in assembly:");
foreach (var attribute in typeof(Reader).GetCustomAttributesData().Where(attribute => attribute.AttributeType.Name == "TemplatePartAttribute"))
    output.AppendLine("  " + attribute);
output.AppendLine("If no attribute is listed, required parts above are runtime observations; template semantics are linked below.");

output.AppendLine();
output.AppendLine("Default-theme resource URIs and keys (actual loaded styles/resources):");
var resourceLines = new SortedSet<string>(StringComparer.Ordinal);
foreach (var include in theme.OfType<StyleInclude>())
{
    resourceLines.Add("  StyleInclude URI: " + include.Source);
    if (include.Loaded is Styles loaded) Resources(loaded.Resources, "Default", resourceLines);
}
foreach (var line in resourceLines) output.AppendLine(line);
output.AppendLine();
output.AppendLine("Semantic/default/lifecycle contracts: [usage](../markdown-usage.md), [status](../markdown-status.md), [official differences](../markdown-official-alignment.md).");
output.AppendLine("This snapshot is exported from compiled assemblies and a Headless theme instance; it does not establish native clipboard, DPI or accessibility validation.");
window.Close();
var fullPath = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
File.WriteAllText(fullPath, output.ToString(), new UTF8Encoding(false));
Console.WriteLine("Exported: " + fullPath);

static string TypeName(Type? type) => type?.FullName ?? "none";
static string Access(MethodInfo? method) => method is null ? "none" : method.IsPublic ? "public" : method.IsFamily ? "protected" : "non-public";
static string Value(object? value) => value switch
{
    null => "null", string text => "\"" + text + "\"", bool boolean => boolean ? "true" : "false",
    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture), _ => value.ToString() ?? "null"
};
static string Parameters(ParameterInfo[] parameters)
{
    var optional = parameters.Where(parameter => parameter.HasDefaultValue).Select(parameter => parameter.Name + "=" + Value(parameter.DefaultValue)).ToArray();
    return optional.Length == 0 ? "" : " [optional: " + string.Join(", ", optional) + "]";
}
static void Resources(IResourceProvider provider, string scope, ISet<string> lines)
{
    if (provider is ResourceInclude include)
    {
        lines.Add("  ResourceInclude URI: " + include.Source);
        Resources(include.Loaded, scope, lines);
    }
    else if (provider is ResourceDictionary dictionary)
    {
        foreach (var key in dictionary.Keys)
            if (key is string name && name.StartsWith("Markdown", StringComparison.Ordinal))
                lines.Add("  " + scope + ": " + name + " : " + dictionary[key]?.GetType().FullName + " = " + Value(dictionary[key]));
        foreach (var merged in dictionary.MergedDictionaries) Resources(merged, scope, lines);
        foreach (var pair in dictionary.ThemeDictionaries) Resources(pair.Value, pair.Key.ToString() ?? "Theme", lines);
    }
}
public sealed class ApiApp : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); Styles.Add(new MarkdownTheme()); }
}
'@ | Set-Content -LiteralPath (Join-Path $markdownApiWork 'Program.cs') -Encoding utf8
Push-Location $markdownApiRepo
try {
    dotnet restore (Join-Path $markdownApiWork 'ApiExport.csproj')
    if ($LASTEXITCODE -ne 0) { throw 'Markdown API helper restore failed.' }
    dotnet run --project (Join-Path $markdownApiWork 'ApiExport.csproj') -c Release --no-restore -- $OutputPath
    if ($LASTEXITCODE -ne 0) { throw 'Markdown public API export failed.' }
    Write-Output "Markdown API helper artifacts: $markdownApiWork"
}
finally { Pop-Location }
