using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Microsoft.Xna.Framework.Graphics;

public sealed class PipelineUsageDescription
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public IReadOnlyDictionary<string, string> ShaderArtifacts { get; set; } = new Dictionary<string, string>();
    public string VertexLayout { get; set; }
    public string PrimitiveTopology { get; set; }
    public string BlendState { get; set; }
    public string DepthStencilState { get; set; }
    public string RasterizerState { get; set; }
    public string RenderTargetFormats { get; set; }
    public string DepthStencilFormat { get; set; }
    public int SampleCount { get; set; } = 1;
    public uint SampleMask { get; set; } = uint.MaxValue;
    public string SpecializationConstants { get; set; } = string.Empty;
    public string BackendCompatibilityFlags { get; set; } = string.Empty;

    public string ComputeKey()
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        using (var sha256 = SHA256.Create())
        {
            writer.Write(SchemaVersion);
            var shaders = ShaderArtifacts.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
            writer.Write(shaders.Length);
            foreach (var shader in shaders)
            {
                Write(writer, shader.Key);
                Write(writer, shader.Value);
            }
            Write(writer, VertexLayout);
            Write(writer, PrimitiveTopology);
            Write(writer, BlendState);
            Write(writer, DepthStencilState);
            Write(writer, RasterizerState);
            Write(writer, RenderTargetFormats);
            Write(writer, DepthStencilFormat);
            writer.Write(SampleCount);
            writer.Write(SampleMask);
            Write(writer, SpecializationConstants);
            Write(writer, BackendCompatibilityFlags);
            writer.Flush();
            return BitConverter.ToString(sha256.ComputeHash(stream.GetBuffer(), 0, (int)stream.Length))
                .Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    private static void Write(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes((value ?? string.Empty).Normalize(NormalizationForm.FormC));
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}

public sealed class PipelineUsageManifestEntry
{
    public string PipelineKey { get; set; }
    public int Priority { get; set; }
    public string Source { get; set; }
    public string PriorityGroup { get; set; } = "Default";
}

public sealed class PipelineUsageManifest
{
    public int SchemaVersion { get; set; } = 2;
    public IReadOnlyList<PipelineUsageManifestEntry> Pipelines { get; set; } = Array.Empty<PipelineUsageManifestEntry>();
    public IReadOnlyDictionary<string, PipelineUsageDescription> Descriptions { get; set; }
        = new Dictionary<string, PipelineUsageDescription>();
}

public class PipelinePrewarmProgress
{
    public int Completed { get; set; }
    public int Total { get; set; }
    public int Prepared { get; set; }
    public int CacheHits { get; set; }
    public int Unresolved { get; set; }
    public int Failed { get; set; }
    public string PipelineKey { get; set; }
    public TimeSpan Elapsed { get; set; }
}

public sealed class PipelinePrewarmReport : PipelinePrewarmProgress
{
    public bool Cancelled { get; set; }
}

public sealed class PipelineUsageRecorder
{
    private readonly object _sync = new object();
    private readonly Dictionary<(string Key, string Group), PipelineUsageManifestEntry> _entries = new();
    private readonly Dictionary<string, PipelineUsageDescription> _descriptions = new(StringComparer.Ordinal);

    public void Declare(PipelineUsageDescription description, int priority, string priorityGroup = "Default", string source = "Declared")
    {
        if (description == null)
            throw new ArgumentNullException(nameof(description));
        if (string.IsNullOrWhiteSpace(priorityGroup))
            throw new ArgumentException("A priority group is required.", nameof(priorityGroup));
        Record(description, priority, source, priorityGroup);
    }

    internal void Observe(PipelineUsageDescription description)
        => Record(description, 0, "Recorded", "Development");

    public PipelineUsageManifest Snapshot()
    {
        lock (_sync)
        {
            return new PipelineUsageManifest
            {
                Pipelines = _entries.Values
                    .OrderByDescending(entry => entry.Priority)
                    .ThenBy(entry => entry.PriorityGroup, StringComparer.Ordinal)
                    .ThenBy(entry => entry.PipelineKey, StringComparer.Ordinal)
                    .ToArray(),
                Descriptions = _descriptions.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            };
        }
    }

    private void Record(PipelineUsageDescription description, int priority, string source, string group)
    {
        var key = description.ComputeKey();
        lock (_sync)
        {
            _descriptions[key] = description;
            var identity = (key, group);
            if (!_entries.TryGetValue(identity, out var entry))
            {
                _entries.Add(identity, new PipelineUsageManifestEntry
                {
                    PipelineKey = key,
                    Priority = priority,
                    Source = source,
                    PriorityGroup = group,
                });
            }
            else if (priority > entry.Priority)
            {
                entry.Priority = priority;
            }
        }
    }
}

public partial class GraphicsDevice
{
    private PipelineUsageRecorder _pipelineUsageRecorder;

    public PipelineUsageRecorder BeginPipelineUsageRecording()
    {
        if (_pipelineUsageRecorder != null)
            throw new InvalidOperationException("Pipeline usage recording is already active.");
        _pipelineUsageRecorder = new PipelineUsageRecorder();
        return _pipelineUsageRecorder;
    }

    public PipelineUsageManifest EndPipelineUsageRecording()
    {
        if (_pipelineUsageRecorder == null)
            throw new InvalidOperationException("Pipeline usage recording is not active.");
        var manifest = _pipelineUsageRecorder.Snapshot();
        _pipelineUsageRecorder = null;
        return manifest;
    }

#if NATIVE
    public PipelinePrewarmReport PrewarmPipelines(
        PipelineUsageManifest manifest,
        Func<PipelineUsageDescription, bool> resolve,
        string priorityGroup = null,
        CancellationToken cancellationToken = default,
        IProgress<PipelinePrewarmProgress> progress = null)
    {
        if (manifest == null)
            throw new ArgumentNullException(nameof(manifest));
        if (resolve == null)
            throw new ArgumentNullException(nameof(resolve));

        var entries = manifest.Pipelines
            .Where(entry => priorityGroup == null || StringComparer.Ordinal.Equals(entry.PriorityGroup, priorityGroup))
            .OrderByDescending(entry => entry.Priority)
            .ThenBy(entry => entry.PipelineKey, StringComparer.Ordinal)
            .ToArray();
        var report = new PipelinePrewarmReport { Total = entries.Length };
        var started = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                report.Cancelled = true;
                break;
            }

            report.PipelineKey = entry.PipelineKey;
            if (!manifest.Descriptions.TryGetValue(entry.PipelineKey, out var description) || !resolve(description))
            {
                report.Unresolved++;
            }
            else if (!Enum.TryParse(description.PrimitiveTopology, out PrimitiveType primitiveType))
            {
                report.Failed++;
            }
            else
            {
                var before = GetShaderPipelineDiagnostics();
                if (PrewarmCurrentPipeline(primitiveType))
                {
                    var after = GetShaderPipelineDiagnostics();
                    report.Prepared++;
                    if (after.PipelineCreationCount == before.PipelineCreationCount)
                        report.CacheHits++;
                }
                else
                {
                    report.Failed++;
                }
            }

            report.Completed++;
            report.Elapsed = DateTime.UtcNow - started;
            progress?.Report(new PipelinePrewarmProgress
            {
                Completed = report.Completed,
                Total = report.Total,
                Prepared = report.Prepared,
                CacheHits = report.CacheHits,
                Unresolved = report.Unresolved,
                Failed = report.Failed,
                PipelineKey = report.PipelineKey,
                Elapsed = report.Elapsed,
            });
        }

        report.Elapsed = DateTime.UtcNow - started;
        return report;
    }
#endif

    private void RecordPipelineUsage(PrimitiveType primitiveType)
    {
        if (_pipelineUsageRecorder == null)
            return;

        _pipelineUsageRecorder.Observe(CaptureCurrentPipelineDescription(primitiveType));
    }

    public PipelineUsageDescription GetCurrentPipelineDescription(PrimitiveType primitiveType)
    {
        ApplyState(true);
        return CaptureCurrentPipelineDescription(primitiveType);
    }

    private PipelineUsageDescription CaptureCurrentPipelineDescription(PrimitiveType primitiveType)
    {
        var layout = new StringBuilder();
        foreach (var binding in _vertexBuffers.Get())
        {
            layout.Append(binding.VertexBuffer.VertexDeclaration.VertexStride).Append('@').Append(binding.InstanceFrequency).Append(':');
            foreach (var element in binding.VertexBuffer.VertexDeclaration.GetVertexElements())
                layout.Append(element.VertexElementFormat).Append('/').Append(element.VertexElementUsage).Append(element.UsageIndex).Append('@').Append(element.Offset).Append(',');
            layout.Append('|');
        }

        var blend = new StringBuilder();
        for (var index = 0; index < 4; index++)
        {
            var target = _actualBlendState[index];
            blend.Append(target.AlphaBlendFunction).Append(',').Append(target.AlphaSourceBlend).Append(',').Append(target.AlphaDestinationBlend).Append(',')
                .Append(target.ColorBlendFunction).Append(',').Append(target.ColorSourceBlend).Append(',').Append(target.ColorDestinationBlend).Append(',')
                .Append(target.ColorWriteChannels).Append('|');
        }

        var renderTargetFormats = new List<string>();
        for (var index = 0; index < _currentRenderTargetCount; index++)
            renderTargetFormats.Add(_currentRenderTargetBindings[index].RenderTarget.Format.ToString());
        if (renderTargetFormats.Count == 0)
            renderTargetFormats.Add(PresentationParameters.BackBufferFormat.ToString());

        return new PipelineUsageDescription
        {
            ShaderArtifacts = new Dictionary<string, string>
            {
                ["Pixel"] = _pixelShader.ArtifactKey,
                ["Vertex"] = _vertexShader.ArtifactKey,
            },
            VertexLayout = layout.ToString(),
            PrimitiveTopology = primitiveType.ToString(),
            BlendState = blend.ToString(),
            DepthStencilState = string.Join(",", _actualDepthStencilState.DepthBufferEnable, _actualDepthStencilState.DepthBufferWriteEnable,
                _actualDepthStencilState.DepthBufferFunction, _actualDepthStencilState.StencilEnable, _actualDepthStencilState.StencilFunction,
                _actualDepthStencilState.StencilFail, _actualDepthStencilState.StencilDepthBufferFail, _actualDepthStencilState.StencilPass,
                _actualDepthStencilState.StencilMask, _actualDepthStencilState.StencilWriteMask, _actualDepthStencilState.ReferenceStencil),
            RasterizerState = string.Join(",", _actualRasterizerState.CullMode, _actualRasterizerState.FillMode,
                _actualRasterizerState.DepthBias.ToString("R", CultureInfo.InvariantCulture),
                _actualRasterizerState.SlopeScaleDepthBias.ToString("R", CultureInfo.InvariantCulture),
                _actualRasterizerState.DepthClipEnable, _actualRasterizerState.MultiSampleAntiAlias, _actualRasterizerState.ScissorTestEnable),
            RenderTargetFormats = string.Join(",", renderTargetFormats),
            DepthStencilFormat = ActiveDepthFormat.ToString(),
            SampleCount = GetActiveSampleCount(),
            SampleMask = uint.MaxValue,
            BackendCompatibilityFlags = "NativeProfile:" + Shader.Profile.ToString(CultureInfo.InvariantCulture),
        };
    }

    private int GetActiveSampleCount()
    {
        if (_currentRenderTargetCount == 0)
            return PresentationParameters.MultiSampleCount;
        var target = _currentRenderTargetBindings[0].RenderTarget;
        if (target is RenderTarget2D target2D)
            return target2D.MultiSampleCount;
        if (target is RenderTargetCube targetCube)
            return targetCube.MultiSampleCount;
#if DIRECTX
        if (target is RenderTarget3D target3D)
            return target3D.MultiSampleCount;
#endif
        return 1;
    }
}