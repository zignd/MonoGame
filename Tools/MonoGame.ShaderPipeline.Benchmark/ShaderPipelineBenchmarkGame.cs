using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Framework.Utilities;

namespace MonoGame.ShaderPipeline.Benchmark;

public sealed class ShaderPipelineBenchmarkGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly BenchmarkOptions _options;
    private readonly VertexPositionNormalTexture[] _vertices =
    {
        new(new Vector3(-0.8f, -0.7f, 0), Vector3.Backward, new Vector2(0, 1)),
        new(new Vector3(0, 0.8f, 0), Vector3.Backward, new Vector2(0.5f, 0)),
        new(new Vector3(0.8f, -0.7f, 0), Vector3.Backward, new Vector2(1, 1)),
    };
    private readonly VertexPositionColorTexture[] _hotReloadVertices =
    {
        new(new Vector3(-0.8f, -0.7f, 0), Color.White, new Vector2(0, 1)),
        new(new Vector3(0, 0.8f, 0), Color.White, new Vector2(0.5f, 0)),
        new(new Vector3(0.8f, -0.7f, 0), Color.White, new Vector2(1, 1)),
    };
    private BasicEffect _effect;
    private EffectHotReloadService _hotReload;
    private Texture2D _texture;
    private VertexBuffer _prewarmVertexBuffer;
    private PipelineUsageRecorder _pipelineUsageRecorder;
    private PipelinePrewarmReport _prewarmReport;
    private PipelineCacheStatus _pipelineCacheImportStatus;
    private int _pipelineCacheBytes;
    private readonly Stopwatch _launchStopwatch = Stopwatch.StartNew();
    private readonly List<double> _frameMilliseconds = new();
    private double _launchToFirstDrawMilliseconds;
    private long _peakManagedMemoryBytes;
    private int _renderedFrames;

    public ShaderPipelineBenchmarkGame(BenchmarkOptions options)
    {
        _options = options;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 800,
            PreferredBackBufferHeight = 600,
            SynchronizeWithVerticalRetrace = false,
        };
        IsFixedTimeStep = false;
        Window.Title = "MonoGame Shader Pipeline Benchmark";
    }

    protected override void LoadContent()
    {
        if (_options.PipelineCachePath != null)
        {
            _pipelineCacheImportStatus = File.Exists(_options.PipelineCachePath)
                ? GraphicsDevice.ImportPipelineCache(File.ReadAllBytes(_options.PipelineCachePath))
                : PipelineCacheStatus.Empty;
        }
        _pipelineUsageRecorder = GraphicsDevice.BeginPipelineUsageRecording();
        _texture = new Texture2D(GraphicsDevice, 1, 1);
        _texture.SetData(new[] { Color.White });
        _effect = new BasicEffect(GraphicsDevice)
        {
            Texture = _texture,
            World = Matrix.Identity,
            View = Matrix.Identity,
            Projection = Matrix.Identity,
        };
        _effect.EnableDefaultLighting();
        _prewarmVertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionNormalTexture), _vertices.Length, BufferUsage.WriteOnly);
        _prewarmVertexBuffer.SetData(_vertices);
        var variants = new Dictionary<string, (int Variant, int Pass)>();
        GraphicsDevice.SetVertexBuffer(_prewarmVertexBuffer);
        for (var variant = 0; variant < 16; variant++)
        {
            ConfigureVariant(variant);
            for (var passIndex = 0; passIndex < _effect.CurrentTechnique.Passes.Count; passIndex++)
            {
                _effect.CurrentTechnique.Passes[passIndex].Apply();
                var description = GraphicsDevice.GetCurrentPipelineDescription(PrimitiveType.TriangleList);
                _pipelineUsageRecorder.Declare(description, 100, "Startup", "Benchmark");
                variants[description.ComputeKey()] = (variant, passIndex);
            }
        }
        _prewarmReport = GraphicsDevice.PrewarmPipelines(
            _pipelineUsageRecorder.Snapshot(),
            description =>
            {
                if (!variants.TryGetValue(description.ComputeKey(), out var binding))
                    return false;
                ConfigureVariant(binding.Variant);
                GraphicsDevice.SetVertexBuffer(_prewarmVertexBuffer);
                _effect.CurrentTechnique.Passes[binding.Pass].Apply();
                return true;
            },
            priorityGroup: "Startup");
        GraphicsDevice.ResetShaderPipelineDiagnostics();
        if (_options.WatchedEffectPath != null)
        {
            var watchedEffectPath = Path.GetFullPath(_options.WatchedEffectPath);
            var initialEffect = new Effect(GraphicsDevice, File.ReadAllBytes(watchedEffectPath));
            _hotReload = new EffectHotReloadService(
                GraphicsDevice,
                initialEffect,
                watchedEffectPath,
                async cancellationToken => new HotReloadCompilation(
                    await File.ReadAllBytesAsync(watchedEffectPath, cancellationToken).ConfigureAwait(false),
                    cacheHit: false),
                debounce: TimeSpan.Zero);
            _hotReload.NotifyChanged();
        }
        base.LoadContent();
    }

    protected override void Draw(GameTime gameTime)
    {
        var frameStopwatch = Stopwatch.StartNew();
        if (_renderedFrames == 0)
            _launchToFirstDrawMilliseconds = _launchStopwatch.Elapsed.TotalMilliseconds;
        GraphicsDevice.Clear(Color.CornflowerBlue);
        for (var variant = 0; variant < 16; variant++)
        {
            ConfigureVariant(variant);

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _vertices, 0, 1);
            }
        }

        if (_hotReload != null)
        {
            _hotReload.Update();
            if (!_hotReload.IsReloading &&
                _hotReload.SuccessfulReloadCount + _hotReload.FailedReloadCount < _options.ReloadCount)
            {
                _hotReload.NotifyChanged();
            }
            var effect = _hotReload.Current;
            effect.Parameters["MatrixTransform"]?.SetValue(Matrix.Identity);
            effect.Parameters["Texture"]?.SetValue(_texture);
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _hotReloadVertices, 0, 1);
            }
        }

        base.Draw(gameTime);
        frameStopwatch.Stop();
        _frameMilliseconds.Add(frameStopwatch.Elapsed.TotalMilliseconds);
        _peakManagedMemoryBytes = Math.Max(_peakManagedMemoryBytes, GC.GetTotalMemory(forceFullCollection: false));
        _renderedFrames++;
        var reloadsComplete = _hotReload == null ||
            _hotReload.SuccessfulReloadCount + _hotReload.FailedReloadCount >= _options.ReloadCount;
        if (_renderedFrames >= _options.FrameCount && reloadsComplete)
        {
            WriteReport();
            Exit();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotReload?.Dispose();
            _effect?.Dispose();
            _prewarmVertexBuffer?.Dispose();
            _texture?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void WriteReport()
    {
        var diagnostics = GraphicsDevice.GetShaderPipelineDiagnostics();
        var pipelineCache = GraphicsDevice.GetPipelineCacheData();
        _pipelineCacheBytes = pipelineCache.Length;
        if (_options.PipelineCachePath != null && pipelineCache.Length > 0)
        {
            var cachePath = Path.GetFullPath(_options.PipelineCachePath);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            var temporaryPath = cachePath + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(temporaryPath, pipelineCache);
            File.Move(temporaryPath, cachePath, overwrite: true);
        }
        var pipelineManifest = _pipelineUsageRecorder.Snapshot();
        var cacheLookups = diagnostics.PipelineCacheHits + diagnostics.PipelineCacheMisses;
        var sortedFrameMilliseconds = _frameMilliseconds.OrderBy(value => value).ToArray();
        var nativeRuntimeBytes = GetNativeRuntimeSize();
        var outputPath = Path.GetFullPath(_options.OutputPath);
        var manifestPath = Path.Combine(
            Path.GetDirectoryName(outputPath),
            Path.GetFileNameWithoutExtension(outputPath) + ".pipelines.json");
        var report = new
        {
            schemaVersion = 1,
            workload = "BasicEffect-16-variant-matrix",
            capturedAtUtc = DateTimeOffset.UtcNow,
            hostOperatingSystem = Environment.OSVersion.ToString(),
            hostArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            backend = PlatformInfo.GraphicsBackend.ToString(),
            shaderBuildMode = diagnostics.BuildMode,
            renderedFrames = _renderedFrames,
            launchToFirstDrawMilliseconds = _launchToFirstDrawMilliseconds,
            frameTimeP50Milliseconds = Percentile(sortedFrameMilliseconds, 0.50),
            frameTimeP95Milliseconds = Percentile(sortedFrameMilliseconds, 0.95),
            frameTimeP99Milliseconds = Percentile(sortedFrameMilliseconds, 0.99),
            peakManagedMemoryBytes = _peakManagedMemoryBytes,
            nativeRuntimeBytes,
            watchedEffectBytes = _options.WatchedEffectPath == null ? 0 : new FileInfo(_options.WatchedEffectPath).Length,
            variantsPerFrame = 16,
            shaderCreationCount = diagnostics.ShaderCreationCount,
            shaderCreationMilliseconds = diagnostics.ShaderCreationMilliseconds,
            pipelineCacheHits = diagnostics.PipelineCacheHits,
            pipelineCacheMisses = diagnostics.PipelineCacheMisses,
            pipelineCacheHitRatio = cacheLookups == 0 ? 1.0 : (double)diagnostics.PipelineCacheHits / cacheLookups,
            pipelineCreationCount = diagnostics.PipelineCreationCount,
            pipelineCreationMilliseconds = diagnostics.PipelineCreationMilliseconds,
            pipelineCacheImports = diagnostics.PipelineCacheImports + (_pipelineCacheImportStatus == PipelineCacheStatus.Success ? 1UL : 0UL),
            pipelineCacheRejections = diagnostics.PipelineCacheRejections + (IsPipelineCacheRejection(_pipelineCacheImportStatus) ? 1UL : 0UL),
            pipelineCacheImportStatus = _pipelineCacheImportStatus.ToString(),
            pipelineCacheLastStatus = (diagnostics.LastPipelineCacheStatus == PipelineCacheStatus.None
                ? _pipelineCacheImportStatus
                : diagnostics.LastPipelineCacheStatus).ToString(),
            pipelineCacheBytes = _pipelineCacheBytes,
            prewarmTotal = _prewarmReport.Total,
            prewarmPrepared = _prewarmReport.Prepared,
            prewarmCacheHits = _prewarmReport.CacheHits,
            prewarmUnresolved = _prewarmReport.Unresolved,
            prewarmFailed = _prewarmReport.Failed,
            prewarmMilliseconds = _prewarmReport.Elapsed.TotalMilliseconds,
            recordedPipelineCount = pipelineManifest.Descriptions.Count,
            pipelineManifestPath = manifestPath,
            runtimeTranslationCount = diagnostics.RuntimeTranslationCount,
            runtimeTranslationMilliseconds = diagnostics.RuntimeTranslationMilliseconds,
            hotReloadEnabled = _hotReload != null,
            hotReloadCreationMode = _hotReload == null ? null : "RenderThreadSynchronous",
            hotReloadSucceeded = _hotReload?.LastStatus.Succeeded,
            hotReloadCacheHit = _hotReload?.LastStatus.CacheHit,
            hotReloadMilliseconds = _hotReload?.LastStatus.Elapsed.TotalMilliseconds,
            hotReloadMessage = _hotReload?.LastStatus.Message,
            successfulReloadCount = _hotReload?.SuccessfulReloadCount ?? 0,
            failedReloadCount = _hotReload?.FailedReloadCount ?? 0,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        var serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, serializerOptions));
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(pipelineManifest, serializerOptions));
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
            return 0;
        var index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
    }

    private static long GetNativeRuntimeSize()
    {
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "mgruntime.dll"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "libmgruntime.dylib" : "libmgruntime.so";
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }

    private static bool IsPipelineCacheRejection(PipelineCacheStatus status)
        => status is PipelineCacheStatus.InvalidData or PipelineCacheStatus.Incompatible or
            PipelineCacheStatus.Unsupported or PipelineCacheStatus.Error;

    private void ConfigureVariant(int variant)
    {
        _effect.TextureEnabled = (variant & 1) != 0;
        _effect.FogEnabled = (variant & 2) != 0;
        _effect.LightingEnabled = (variant & 4) != 0;
        _effect.PreferPerPixelLighting = (variant & 8) != 0;
        _effect.FogStart = 0;
        _effect.FogEnd = 1;
    }
}

public sealed class BenchmarkOptions
{
    public string OutputPath { get; }
    public int FrameCount { get; }
    public string WatchedEffectPath { get; }
    public int ReloadCount { get; }
    public string PipelineCachePath { get; }

    private BenchmarkOptions(string outputPath, int frameCount, string watchedEffectPath, int reloadCount, string pipelineCachePath)
    {
        OutputPath = outputPath;
        FrameCount = frameCount;
        WatchedEffectPath = watchedEffectPath;
        ReloadCount = reloadCount;
        PipelineCachePath = pipelineCachePath;
    }

    public static BenchmarkOptions Parse(string[] args)
    {
        string outputPath = null;
        string watchedEffectPath = null;
        string pipelineCachePath = null;
        var frameCount = 120;
        var reloadCount = 1;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output" when index + 1 < args.Length:
                    outputPath = args[++index];
                    break;
                case "--frames" when index + 1 < args.Length && int.TryParse(args[++index], NumberStyles.None, CultureInfo.InvariantCulture, out frameCount) && frameCount > 0:
                    break;
                case "--watch-effect" when index + 1 < args.Length:
                    watchedEffectPath = args[++index];
                    break;
                case "--reload-count" when index + 1 < args.Length && int.TryParse(args[++index], NumberStyles.None, CultureInfo.InvariantCulture, out reloadCount) && reloadCount > 0:
                    break;
                case "--pipeline-cache" when index + 1 < args.Length:
                    pipelineCachePath = Path.GetFullPath(args[++index]);
                    break;
                default:
                    throw new ArgumentException($"Unknown or invalid benchmark argument: {args[index]}");
            }
        }

        if (outputPath == null)
            throw new ArgumentException("The shader benchmark requires --output <path>.");
        if (watchedEffectPath != null && !File.Exists(watchedEffectPath))
            throw new ArgumentException($"The watched effect does not exist: {watchedEffectPath}");
        if (watchedEffectPath == null && reloadCount != 1)
            throw new ArgumentException("--reload-count requires --watch-effect.");
        return new BenchmarkOptions(outputPath, frameCount, watchedEffectPath, reloadCount, pipelineCachePath);
    }
}