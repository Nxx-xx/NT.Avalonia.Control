using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using PdfViewer.P0;

var arguments = args.Chunk(2).ToDictionary(pair => pair[0], pair => pair.Length == 2 ? pair[1] : "");
var probe = arguments.GetValueOrDefault("--probe", "save");
var output = Path.GetFullPath(arguments.GetValueOrDefault("--output", "artifacts/pdfviewer/p0-local"));
Directory.CreateDirectory(output);
var destination = Path.Combine(output, probe);
Directory.CreateDirectory(destination);
foreach (var name in new[] { "result.json", "failure.json" }) File.Delete(Path.Combine(destination, name));
var started = DateTimeOffset.UtcNow;
object result;
try
{
    if (probe == "baseline") result = await BaselineProbe.RunAsync(destination);
    else if (probe == "production") result = await ProductionWriterProbe.RunAsync(destination);
    else if (probe == "production-redaction") result = await ProductionRedactionProbe.RunAsync(destination);
    else if (probe == "font-subset") result = await ProductionFontSubsetProbe.RunAsync(destination);
    else if (probe == "cjk-cluster-redaction") result = await CjkClusterRedactionProbe.RunAsync(destination);
    else if (probe == "partial-image-redaction") result = await PartialImageRedactionProbe.RunAsync(destination);
    else if (probe == "real-corpus") result = await RealCorpusProbe.RunAsync(destination);
    else
    {
        SaveNative.FPDF_InitLibrary();
        result = probe switch
        {
            "save" => SaveProbe.Run(destination),
            "forms" => Invoke("FormsProbe"),
            "forms-advanced" => Invoke("AdvancedFormsProbe"),
            "cjk-forms" => Invoke("CjkFormsProbe"),
            "fonts" => Invoke("AnnotationFontProbe"),
            "redaction" => Invoke("RedactionProbe"),
            "redaction-advanced" => Invoke("RedactionAdvancedProbe"),
            _ => throw new ArgumentException($"Unknown probe: {probe}")
        };
    }
    using var serializedResult = JsonDocument.Parse(JsonSerializer.Serialize(result));
    var expectationsPassed = ChecksPass(serializedResult.RootElement);
    var module = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Single(item => item.ModuleName.Equals("pdfium.dll", StringComparison.OrdinalIgnoreCase) || item.ModuleName == "libpdfium.so" || item.ModuleName == "libpdfium.dylib");
    var report = new { Probe = probe, StartedUtc = started, CompletedUtc = DateTimeOffset.UtcNow,
        ManagedAssemblySHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Assembly.GetExecutingAssembly().Location))),
        NativeModule = new { Path = module.FileName, SHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module.FileName))) },
        OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), ExpectationsPassed = expectationsPassed, Result = result };
    var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(destination, "result.json"), json);
    Console.WriteLine(json);
    if (!expectationsPassed) return 1;
}
catch (Exception exception)
{
    var error = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
    var json = JsonSerializer.Serialize(new { Probe = probe, StartedUtc = started, FailedUtc = DateTimeOffset.UtcNow,
        Status = "failed", Error = error.ToString() }, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(destination, "failure.json"), json);
    Console.Error.WriteLine(json);
    return 1;
}
return 0;

object Invoke(string name)
{
    var type = Assembly.GetExecutingAssembly().GetType($"PdfViewer.P0.{name}")
        ?? throw new InvalidOperationException($"Probe {name} has not been compiled.");
    var method = type.GetMethod("Run", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(name, "Run");
    return method.Invoke(null, [destination]) ?? throw new InvalidOperationException("Probe returned no report.");
}

static bool ChecksPass(JsonElement value)
{
    if (value.ValueKind == JsonValueKind.Object)
        return value.EnumerateObject().All(property =>
            property.Name is "Passed" or "passed" ? property.Value.ValueKind == JsonValueKind.True : ChecksPass(property.Value));
    if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().All(ChecksPass);
    return true;
}
