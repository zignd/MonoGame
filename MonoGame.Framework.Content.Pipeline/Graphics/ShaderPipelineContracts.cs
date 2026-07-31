// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics;

/// <summary>
/// Controls whether shader processing favors iteration speed or fully cooked artifacts.
/// </summary>
public enum ShaderBuildMode
{
    Development,
    Production,
}

/// <summary>
/// Identifies a source input and its normalized content hash.
/// </summary>
public sealed record ShaderSourceInput(string Path, string ContentHash);

/// <summary>
/// Describes every input that can affect a compiled shader artifact.
/// </summary>
public sealed class ShaderArtifactIdentity
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string CompilerName { get; init; }
    public required string CompilerVersion { get; init; }
    public required string CompilerExecutableVersion { get; init; }
    public required string ShaderProfile { get; init; }
    public required string ShaderModel { get; init; }
    public required string EntryPoint { get; init; }
    public required string Stage { get; init; }
    public required string Optimization { get; init; }
    public required string ResourceBindingVersion { get; init; }
    public required string ReflectionLayoutVersion { get; init; }
    public required string TargetOperatingSystem { get; init; }
    public string MinimumDeploymentTarget { get; init; } = string.Empty;
    public IReadOnlyList<ShaderSourceInput> Sources { get; init; } = [];
    public IReadOnlyDictionary<string, string> Defines { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// Computes a lowercase SHA-256 key from a canonical binary representation.
    /// </summary>
    public string ComputeKey()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(SchemaVersion);
        Write(writer, CompilerName);
        Write(writer, CompilerVersion);
        Write(writer, CompilerExecutableVersion);
        Write(writer, ShaderProfile);
        Write(writer, ShaderModel);
        Write(writer, EntryPoint);
        Write(writer, Stage);
        Write(writer, Optimization);
        Write(writer, ResourceBindingVersion);
        Write(writer, ReflectionLayoutVersion);
        Write(writer, TargetOperatingSystem);
        Write(writer, MinimumDeploymentTarget);

        var sources = Sources
            .Select(source => new ShaderSourceInput(NormalizePath(source.Path), source.ContentHash))
            .OrderBy(source => source.Path, StringComparer.Ordinal)
            .ThenBy(source => source.ContentHash, StringComparer.Ordinal)
            .ToArray();
        writer.Write(sources.Length);
        foreach (var source in sources)
        {
            Write(writer, source.Path);
            Write(writer, source.ContentHash);
        }

        var defines = Defines.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        writer.Write(defines.Length);
        foreach (var define in defines)
        {
            Write(writer, define.Key);
            Write(writer, define.Value);
        }

        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(stream.GetBuffer().AsSpan(0, (int)stream.Length)));
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/').Normalize(NormalizationForm.FormC);

    internal static void Write(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.Normalize(NormalizationForm.FormC));
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}

/// <summary>
/// Backend-neutral state required to identify a graphics pipeline.
/// </summary>
public sealed class ShaderPipelineDescription
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public IReadOnlyDictionary<string, string> ShaderArtifacts { get; init; }
        = new Dictionary<string, string>();
    public required string VertexLayout { get; init; }
    public required string PrimitiveTopology { get; init; }
    public required string BlendState { get; init; }
    public required string DepthStencilState { get; init; }
    public required string RasterizerState { get; init; }
    public required string RenderTargetFormats { get; init; }
    public required string DepthStencilFormat { get; init; }
    public int SampleCount { get; init; } = 1;
    public uint SampleMask { get; init; } = uint.MaxValue;
    public string SpecializationConstants { get; init; } = string.Empty;
    public string BackendCompatibilityFlags { get; init; } = string.Empty;

    /// <summary>
    /// Computes a stable pipeline key independent of dictionary insertion order.
    /// </summary>
    public string ComputeKey()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(SchemaVersion);
        var shaders = ShaderArtifacts.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        writer.Write(shaders.Length);
        foreach (var shader in shaders)
        {
            ShaderArtifactIdentity.Write(writer, shader.Key);
            ShaderArtifactIdentity.Write(writer, shader.Value);
        }

        ShaderArtifactIdentity.Write(writer, VertexLayout);
        ShaderArtifactIdentity.Write(writer, PrimitiveTopology);
        ShaderArtifactIdentity.Write(writer, BlendState);
        ShaderArtifactIdentity.Write(writer, DepthStencilState);
        ShaderArtifactIdentity.Write(writer, RasterizerState);
        ShaderArtifactIdentity.Write(writer, RenderTargetFormats);
        ShaderArtifactIdentity.Write(writer, DepthStencilFormat);
        writer.Write(SampleCount);
        writer.Write(SampleMask);
        ShaderArtifactIdentity.Write(writer, SpecializationConstants);
        ShaderArtifactIdentity.Write(writer, BackendCompatibilityFlags);
        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(stream.GetBuffer().AsSpan(0, (int)stream.Length)));
    }
}

/// <summary>
/// Metadata used to reject incompatible persistent cache data.
/// </summary>
public sealed record ShaderCacheMetadata(
    int SchemaVersion,
    string Backend,
    string Device,
    string DriverVersion,
    string OperatingSystem,
    string ContentHash,
    string CompilerVersion)
{
    public const int CurrentSchemaVersion = 1;

    public bool IsCompatibleWith(ShaderCacheMetadata expected)
        => SchemaVersion == expected.SchemaVersion
        && StringComparer.Ordinal.Equals(Backend, expected.Backend)
        && StringComparer.Ordinal.Equals(Device, expected.Device)
        && StringComparer.Ordinal.Equals(DriverVersion, expected.DriverVersion)
        && StringComparer.Ordinal.Equals(OperatingSystem, expected.OperatingSystem)
        && StringComparer.Ordinal.Equals(ContentHash, expected.ContentHash)
        && StringComparer.Ordinal.Equals(CompilerVersion, expected.CompilerVersion);
}

/// <summary>
/// One observed or explicitly declared pipeline and its prewarming priority.
/// </summary>
public sealed record ShaderPipelineManifestEntry(
    string PipelineKey,
    int Priority,
    string Source,
    string PriorityGroup = "Default");

/// <summary>
/// Backend-neutral collection of pipelines required by a cooked application.
/// </summary>
public sealed class ShaderPipelineManifest
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public IReadOnlyList<ShaderPipelineManifestEntry> Pipelines { get; init; } = [];
    public IReadOnlyDictionary<string, ShaderPipelineDescription> Descriptions { get; init; }
        = new Dictionary<string, ShaderPipelineDescription>();
}