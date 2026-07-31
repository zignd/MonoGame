// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using MonoGame.Interop;

namespace Microsoft.Xna.Framework.Graphics;

public enum PipelineCacheStatus
{
    None = 0,
    Success = 1,
    Empty = 2,
    InvalidData = 3,
    Incompatible = 4,
    Unsupported = 5,
    Error = 6,
}

/// <summary>
/// Cumulative Native shader and graphics-pipeline creation diagnostics.
/// </summary>
public readonly struct ShaderPipelineDiagnostics
{
    public string BuildMode { get; }
    public ulong ShaderCreationCount { get; }
    public ulong PipelineCacheHits { get; }
    public ulong PipelineCacheMisses { get; }
    public ulong PipelineCreationCount { get; }
    public ulong RuntimeTranslationCount { get; }
    public ulong PipelineCacheImports { get; }
    public ulong PipelineCacheRejections { get; }
    public PipelineCacheStatus LastPipelineCacheStatus { get; }
    public double ShaderCreationMilliseconds { get; }
    public double PipelineCreationMilliseconds { get; }
    public double RuntimeTranslationMilliseconds { get; }

    internal ShaderPipelineDiagnostics(
        ulong shaderCreationCount,
        ulong pipelineCacheHits,
        ulong pipelineCacheMisses,
        ulong pipelineCreationCount,
        ulong runtimeTranslationCount,
        ulong pipelineCacheImports,
        ulong pipelineCacheRejections,
        PipelineCacheStatus lastPipelineCacheStatus,
        double shaderCreationMilliseconds,
        double pipelineCreationMilliseconds,
        double runtimeTranslationMilliseconds)
    {
        BuildMode = System.Environment.GetEnvironmentVariable("MG_SHADER_BUILD_MODE") ?? "Development";
        ShaderCreationCount = shaderCreationCount;
        PipelineCacheHits = pipelineCacheHits;
        PipelineCacheMisses = pipelineCacheMisses;
        PipelineCreationCount = pipelineCreationCount;
        RuntimeTranslationCount = runtimeTranslationCount;
        PipelineCacheImports = pipelineCacheImports;
        PipelineCacheRejections = pipelineCacheRejections;
        LastPipelineCacheStatus = lastPipelineCacheStatus;
        ShaderCreationMilliseconds = shaderCreationMilliseconds;
        PipelineCreationMilliseconds = pipelineCreationMilliseconds;
        RuntimeTranslationMilliseconds = runtimeTranslationMilliseconds;
    }
}

public partial class GraphicsDevice
{
    /// <summary>
    /// Gets cumulative shader and graphics-pipeline diagnostics for this device.
    /// </summary>
    public unsafe ShaderPipelineDiagnostics GetShaderPipelineDiagnostics()
    {
        MGG.GraphicsDevice_GetShaderPipelineDiagnostics(Handle, out var diagnostics);
        return new ShaderPipelineDiagnostics(
            diagnostics.ShaderCreationCount,
            diagnostics.PipelineCacheHits,
            diagnostics.PipelineCacheMisses,
            diagnostics.PipelineCreationCount,
            diagnostics.RuntimeTranslationCount,
            diagnostics.PipelineCacheImports,
            diagnostics.PipelineCacheRejections,
            diagnostics.LastPipelineCacheStatus,
            diagnostics.ShaderCreationMilliseconds,
            diagnostics.PipelineCreationMilliseconds,
            diagnostics.RuntimeTranslationMilliseconds);
    }

    /// <summary>
    /// Resets cumulative shader and graphics-pipeline diagnostics for this device.
    /// </summary>
    public unsafe void ResetShaderPipelineDiagnostics()
        => MGG.GraphicsDevice_ResetShaderPipelineDiagnostics(Handle);

    public unsafe byte[] GetPipelineCacheData()
    {
        var size = MGG.GraphicsDevice_GetPipelineCacheDataSize(Handle);
        if (size <= 0)
            return Array.Empty<byte>();
        var data = new byte[size];
        fixed (byte* dataPointer = data)
        {
            if (!MGG.GraphicsDevice_GetPipelineCacheData(Handle, dataPointer, data.Length))
                return Array.Empty<byte>();
        }
        return data;
    }

    public unsafe PipelineCacheStatus ImportPipelineCache(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return PipelineCacheStatus.Empty;
        fixed (byte* dataPointer = data)
            return MGG.GraphicsDevice_ImportPipelineCache(Handle, dataPointer, data.Length);
    }
}