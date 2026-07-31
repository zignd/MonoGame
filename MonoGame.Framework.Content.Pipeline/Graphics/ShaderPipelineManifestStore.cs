using System.Text.Json;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics;

/// <summary>
/// Validates, merges, and persists backend-neutral pipeline usage manifests.
/// </summary>
public static class ShaderPipelineManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static ShaderPipelineManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var manifest = JsonSerializer.Deserialize<ShaderPipelineManifest>(File.ReadAllBytes(path), JsonOptions)
            ?? throw new InvalidDataException($"Pipeline manifest '{path}' is empty.");
        Validate(manifest);
        return manifest;
    }

    public static void Save(string path, ShaderPipelineManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(manifest);
        var canonical = Merge([manifest]);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, JsonSerializer.SerializeToUtf8Bytes(canonical, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static ShaderPipelineManifest Merge(IEnumerable<ShaderPipelineManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        var entries = new Dictionary<(string PipelineKey, string PriorityGroup), ShaderPipelineManifestEntry>();
        var descriptions = new SortedDictionary<string, ShaderPipelineDescription>(StringComparer.Ordinal);

        foreach (var manifest in manifests)
        {
            Validate(manifest);
            foreach (var pair in manifest.Descriptions)
                descriptions.TryAdd(pair.Key, pair.Value);

            foreach (var entry in manifest.Pipelines)
            {
                var identity = (entry.PipelineKey, entry.PriorityGroup);
                if (!entries.TryGetValue(identity, out var current))
                {
                    entries.Add(identity, entry);
                    continue;
                }

                var sources = current.Source.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Concat(entry.Source.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal);
                entries[identity] = current with
                {
                    Priority = Math.Max(current.Priority, entry.Priority),
                    Source = string.Join(';', sources),
                };
            }
        }

        return new ShaderPipelineManifest
        {
            Pipelines = entries.Values
                .OrderByDescending(entry => entry.Priority)
                .ThenBy(entry => entry.PriorityGroup, StringComparer.Ordinal)
                .ThenBy(entry => entry.PipelineKey, StringComparer.Ordinal)
                .ToArray(),
            Descriptions = descriptions,
        };
    }

    public static void Validate(ShaderPipelineManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion != ShaderPipelineManifest.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported pipeline manifest schema {manifest.SchemaVersion}; expected {ShaderPipelineManifest.CurrentSchemaVersion}.");

        foreach (var pair in manifest.Descriptions)
        {
            if (!StringComparer.Ordinal.Equals(pair.Key, pair.Value.ComputeKey()))
                throw new InvalidDataException($"Pipeline description '{pair.Key}' does not match its computed identity.");
        }

        foreach (var entry in manifest.Pipelines)
        {
            if (string.IsNullOrWhiteSpace(entry.PriorityGroup))
                throw new InvalidDataException($"Pipeline '{entry.PipelineKey}' has no priority group.");
            if (!manifest.Descriptions.ContainsKey(entry.PipelineKey))
                throw new InvalidDataException($"Pipeline '{entry.PipelineKey}' has no replayable description.");
        }
    }
}