using System.Text.Json;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using MonoGame.ShaderCache;

if (args.Length < 2)
{
    PrintUsage();
    return 2;
}

var command = args[0].ToLowerInvariant();
if (command == "metal")
{
    if (args.Length < 4 || !StringComparer.OrdinalIgnoreCase.Equals(args[1], "cook"))
    {
        PrintUsage();
        return 2;
    }

    try
    {
        var sourcePath = Path.GetFullPath(args[2]);
        var outputPath = Path.GetFullPath(args[3]);
        var compilerPath = GetOption(args, "--compiler") ?? string.Empty;
        var deploymentTarget = GetOption(args, "--deployment-target") ?? "11.0";
        var content = new EffectContent
        {
            Identity = new ContentIdentity(sourcePath),
            EffectCode = File.ReadAllText(sourcePath),
        };
        using var context = new MetalCookContext(outputPath);
        var processor = new EffectProcessor
        {
            BuildMode = ShaderBuildMode.Production,
            MetalOfflineLibraries = true,
            MetalRuntimeFallback = args.Contains("--allow-runtime-fallback", StringComparer.Ordinal),
            MetalShaderCompilerPath = compilerPath,
            MetalDeploymentTarget = deploymentTarget,
            CacheDirectory = Path.Combine(context.IntermediateDirectory, "cache"),
        };
        var effectCode = processor.Process(content, context).GetEffectCode();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
        File.WriteAllBytes(outputPath, effectCode);
        Console.WriteLine($"Cooked Metal-ready effect: {outputPath} ({effectCode.Length} bytes)");
        return 0;
    }
    catch (Exception exception) when (exception is IOException or InvalidContentException or ArgumentException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

if (command == "manifest")
{
    try
    {
        switch (args[1].ToLowerInvariant())
        {
            case "validate" when args.Length == 3:
            {
                var manifest = ShaderPipelineManifestStore.Load(args[2]);
                Console.WriteLine($"Manifest is valid: {manifest.Pipelines.Count} entries, {manifest.Descriptions.Count} descriptions.");
                return 0;
            }
            case "merge" when args.Length >= 5:
            {
                var manifests = args.Skip(3).Select(ShaderPipelineManifestStore.Load);
                var merged = ShaderPipelineManifestStore.Merge(manifests);
                ShaderPipelineManifestStore.Save(args[2], merged);
                Console.WriteLine($"Merged {args.Length - 3} manifests: {merged.Pipelines.Count} entries, {merged.Descriptions.Count} descriptions.");
                return 0;
            }
            default:
                PrintUsage();
                return 2;
        }
    }
    catch (Exception exception) when (exception is IOException or JsonException or ArgumentException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

var cache = new EffectCompilerCache(args[1]);
switch (command)
{
    case "inspect":
    {
        var entries = cache.Inspect();
        if (args.Contains("--json", StringComparer.Ordinal))
        {
            Console.WriteLine(JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine($"Entries: {entries.Count}, bytes: {entries.Sum(entry => entry.Length)}");
            foreach (var entry in entries)
                Console.WriteLine($"{entry.Key}  {entry.Length} bytes  {entry.LastAccessUtc:O}  {entry.Status}");
        }
        return 0;
    }
    case "validate":
    {
        var invalidKeys = cache.Validate();
        Console.WriteLine(invalidKeys.Count == 0 ? "Cache is valid." : $"Removed {invalidKeys.Count} invalid entries.");
        return invalidKeys.Count == 0 ? 0 : 1;
    }
    case "clear":
        cache.Clear();
        Console.WriteLine("Cache cleared.");
        return 0;
    case "trim" when args.Length == 4 && args[2] == "--max-size-mb" && long.TryParse(args[3], out var megabytes) && megabytes >= 0:
        cache.Trim(megabytes * 1024L * 1024L);
        Console.WriteLine("Cache trimmed.");
        return 0;
    default:
        Console.Error.WriteLine($"Unknown or invalid cache command: {string.Join(' ', args)}");
        return 2;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  mgshadercache <inspect|validate|clear|trim> <directory> [--json|--max-size-mb <value>]");
    Console.Error.WriteLine("  mgshadercache manifest validate <manifest.json>");
    Console.Error.WriteLine("  mgshadercache manifest merge <output.json> <input.json> <input.json> [...]");
    Console.Error.WriteLine("  mgshadercache metal cook <input.fx> <output.mgfxo> [--compiler <path>] [--deployment-target <version>] [--allow-runtime-fallback]");
}

static string? GetOption(string[] arguments, string option)
{
    var index = Array.FindIndex(arguments, value => StringComparer.Ordinal.Equals(value, option));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}