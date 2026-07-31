using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics;

/// <summary>
/// Stores disposable backend-native pipeline cache blobs with strict compatibility metadata.
/// </summary>
public sealed class NativePipelineCacheStore
{
    private sealed record CacheEnvelope(ShaderCacheMetadata Metadata, string Checksum, long Length);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _rootDirectory;

    public NativePipelineCacheStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    public void Write(string name, ShaderCacheMetadata metadata, byte[] payload)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(payload);
        var envelope = new CacheEnvelope(metadata, Hash(payload), payload.LongLength);
        WriteAtomically(GetPayloadPath(name), payload);
        WriteAtomically(GetMetadataPath(name), JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions));
    }

    public bool TryRead(string name, ShaderCacheMetadata expected, out byte[] payload)
        => TryRead(name, expected, out payload, out _);

    public bool TryRead(string name, ShaderCacheMetadata expected, out byte[] payload, out NativePipelineCacheReadResult result)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(expected);
        var payloadPath = GetPayloadPath(name);
        var metadataPath = GetMetadataPath(name);
        payload = Array.Empty<byte>();
        try
        {
            if (!File.Exists(payloadPath) || !File.Exists(metadataPath))
            {
                result = NativePipelineCacheReadResult.Missing;
                return false;
            }
            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(File.ReadAllBytes(metadataPath), JsonOptions);
            var candidate = File.ReadAllBytes(payloadPath);
            result = envelope switch
            {
                null => NativePipelineCacheReadResult.InvalidMetadata,
                _ when !envelope.Metadata.IsCompatibleWith(expected) => NativePipelineCacheReadResult.Incompatible,
                _ when envelope.Length != candidate.LongLength => NativePipelineCacheReadResult.LengthMismatch,
                _ when !StringComparer.Ordinal.Equals(envelope.Checksum, Hash(candidate)) => NativePipelineCacheReadResult.ChecksumMismatch,
                _ => NativePipelineCacheReadResult.Hit,
            };
            if (result != NativePipelineCacheReadResult.Hit)
            {
                Delete(name);
                return false;
            }
            payload = candidate;
            result = NativePipelineCacheReadResult.Hit;
            return true;
        }
        catch (JsonException)
        {
            Delete(name);
            result = NativePipelineCacheReadResult.InvalidMetadata;
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Delete(name);
            result = NativePipelineCacheReadResult.IoError;
            return false;
        }
    }

    public void Delete(string name)
    {
        ValidateName(name);
        TryDelete(GetPayloadPath(name));
        TryDelete(GetMetadataPath(name));
    }

    private string GetPayloadPath(string name) => Path.Combine(_rootDirectory, name + ".bin");
    private string GetMetadataPath(string name) => Path.Combine(_rootDirectory, name + ".json");

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
            throw new ArgumentException("Cache names may contain only ASCII letters, digits, '.', '-', and '_'.", nameof(name));
    }

    private static string Hash(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void WriteAtomically(string path, byte[] bytes)
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

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public enum NativePipelineCacheReadResult
{
    Hit,
    Missing,
    InvalidMetadata,
    Incompatible,
    LengthMismatch,
    ChecksumMismatch,
    IoError,
}
