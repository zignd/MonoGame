// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;

namespace Microsoft.Xna.Framework.Content.Pipeline.Processors;

internal sealed class EffectCompilerCache
{
    private const int SchemaVersion = 2;
    private readonly string _rootDirectory;

    public EffectCompilerCache(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    public static string ComputeKey(
        string sourceFile,
        IEnumerable<string> dependencies,
        string profile,
        bool debug,
        string defines,
        string compilerIdentity,
        ShaderBuildMode buildMode = ShaderBuildMode.Development,
        bool metalOfflineLibraries = false,
        string metalCompilerIdentity = "",
        string variantIdentity = "")
    {
        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceFile)) ?? Directory.GetCurrentDirectory();
        var inputs = dependencies
            .Append(sourceFile)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .Select(path => new
            {
                Path = Path.GetRelativePath(sourceDirectory, path).Replace('\\', '/'),
                Content = File.ReadAllBytes(path),
            })
            .OrderBy(input => input.Path, StringComparer.Ordinal)
            .ToArray();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(SchemaVersion);
        Write(writer, compilerIdentity);
        Write(writer, profile);
        writer.Write(debug);
        Write(writer, defines);
        writer.Write((byte)buildMode);
        writer.Write(metalOfflineLibraries);
        Write(writer, metalCompilerIdentity);
        Write(writer, variantIdentity);
        foreach (var input in inputs)
        {
            Write(writer, input.Path);
            writer.Write(input.Content.Length);
            writer.Write(input.Content);
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)))).ToLowerInvariant();
    }

    public bool TryRead(string key, out byte[] effectCode)
        => TryRead(key, out effectCode, out _);

    public bool TryRead(string key, out byte[] effectCode, out EffectCompilerCacheReadResult result)
    {
        var artifactPath = GetArtifactPath(key);
        var metadataPath = GetMetadataPath(key);
        effectCode = Array.Empty<byte>();
        try
        {
            if (!File.Exists(artifactPath) || !File.Exists(metadataPath))
            {
                result = EffectCompilerCacheReadResult.Missing;
                return false;
            }

            var metadata = JsonSerializer.Deserialize<CacheEntryMetadata>(File.ReadAllText(metadataPath));
            var bytes = File.ReadAllBytes(artifactPath);
            result = metadata switch
            {
                null => EffectCompilerCacheReadResult.InvalidMetadata,
                { SchemaVersion: not SchemaVersion } => EffectCompilerCacheReadResult.SchemaMismatch,
                _ when metadata.Key != key => EffectCompilerCacheReadResult.KeyMismatch,
                _ when metadata.Length != bytes.LongLength => EffectCompilerCacheReadResult.LengthMismatch,
                _ when metadata.Sha256 != Hash(bytes) => EffectCompilerCacheReadResult.ChecksumMismatch,
                _ => EffectCompilerCacheReadResult.Hit,
            };
            if (result != EffectCompilerCacheReadResult.Hit)
            {
                Remove(key);
                return false;
            }

            metadata.LastAccessUtc = DateTimeOffset.UtcNow;
            WriteMetadataAtomically(metadataPath, metadata);
            effectCode = bytes;
            result = EffectCompilerCacheReadResult.Hit;
            return true;
        }
        catch (IOException)
        {
            Remove(key);
            result = EffectCompilerCacheReadResult.IoError;
            return false;
        }
        catch (JsonException)
        {
            Remove(key);
            result = EffectCompilerCacheReadResult.InvalidMetadata;
            return false;
        }
    }

    public void Write(string key, byte[] effectCode, string compilerIdentity, string profile, bool debug, string defines)
    {
        Directory.CreateDirectory(_rootDirectory);
        var artifactPath = GetArtifactPath(key);
        var metadataPath = GetMetadataPath(key);
        var metadata = new CacheEntryMetadata
        {
            SchemaVersion = SchemaVersion,
            Key = key,
            CompilerIdentity = compilerIdentity,
            Profile = profile,
            Debug = debug,
            Defines = defines,
            Length = effectCode.LongLength,
            Sha256 = Hash(effectCode),
            CreatedUtc = DateTimeOffset.UtcNow,
            LastAccessUtc = DateTimeOffset.UtcNow,
        };

        WriteBytesAtomically(artifactPath, effectCode);
        WriteMetadataAtomically(metadataPath, metadata);
    }

    public void Remove(string key)
    {
        TryDelete(GetArtifactPath(key));
        TryDelete(GetMetadataPath(key));
    }

    public IReadOnlyList<EffectCompilerCacheEntry> Inspect()
    {
        if (!Directory.Exists(_rootDirectory))
            return Array.Empty<EffectCompilerCacheEntry>();

        var entries = new List<EffectCompilerCacheEntry>();
        foreach (var metadataPath in Directory.EnumerateFiles(_rootDirectory, "*.json"))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<CacheEntryMetadata>(File.ReadAllText(metadataPath));
                if (metadata != null)
                    entries.Add(metadata.ToEntry());
            }
            catch (JsonException)
            {
                entries.Add(new EffectCompilerCacheEntry(Path.GetFileNameWithoutExtension(metadataPath), 0, default, "invalid"));
            }
            catch (IOException)
            {
                entries.Add(new EffectCompilerCacheEntry(Path.GetFileNameWithoutExtension(metadataPath), 0, default, "unreadable"));
            }
        }
        return entries.OrderByDescending(entry => entry.LastAccessUtc).ToArray();
    }

    public IReadOnlyList<string> Validate()
    {
        var invalidKeys = new List<string>();
        foreach (var entry in Inspect())
        {
            if (!TryRead(entry.Key, out _))
                invalidKeys.Add(entry.Key);
        }
        return invalidKeys;
    }

    public void Trim(long maximumBytes)
    {
        if (maximumBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        var entries = Inspect().OrderBy(entry => entry.LastAccessUtc).ToArray();
        var totalBytes = entries.Sum(entry => entry.Length);
        foreach (var entry in entries)
        {
            if (totalBytes <= maximumBytes)
                break;
            Remove(entry.Key);
            totalBytes -= entry.Length;
        }
    }

    public void Clear()
    {
        if (Directory.Exists(_rootDirectory))
            Directory.Delete(_rootDirectory, recursive: true);
    }

    private string GetArtifactPath(string key) => Path.Combine(_rootDirectory, key + ".mgfxo");
    private string GetMetadataPath(string key) => Path.Combine(_rootDirectory, key + ".json");

    private static void Write(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.Normalize(NormalizationForm.FormC));
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void WriteBytesAtomically(string path, byte[] bytes)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, bytes);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static void WriteMetadataAtomically(string path, CacheEntryMetadata metadata)
    {
        WriteBytesAtomically(path, JsonSerializer.SerializeToUtf8Bytes(metadata, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class CacheEntryMetadata
    {
        public int SchemaVersion { get; set; }
        public string Key { get; set; } = string.Empty;
        public string CompilerIdentity { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
        public bool Debug { get; set; }
        public string Defines { get; set; } = string.Empty;
        public long Length { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; }
        public DateTimeOffset LastAccessUtc { get; set; }

        public EffectCompilerCacheEntry ToEntry()
            => new(Key, Length, LastAccessUtc, "valid");
    }
}

internal readonly record struct EffectCompilerCacheEntry(string Key, long Length, DateTimeOffset LastAccessUtc, string Status);

internal enum EffectCompilerCacheReadResult
{
    Hit,
    Missing,
    InvalidMetadata,
    SchemaMismatch,
    KeyMismatch,
    LengthMismatch,
    ChecksumMismatch,
    IoError,
}