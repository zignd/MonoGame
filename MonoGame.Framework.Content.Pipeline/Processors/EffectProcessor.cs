// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using MonoGame.Effect;

namespace Microsoft.Xna.Framework.Content.Pipeline.Processors;

/// <summary>
/// Processes a string representation to a platform-specific compiled effect.
/// </summary>
[ContentProcessor(DisplayName = "Effect - MonoGame")]
public class EffectProcessor() : ContentProcessor<EffectContent, CompiledEffectContent>
{
    private static readonly Regex errorOrWarning = new(@"(.*)\((\d*,\d*(?>,\d*,\d*)?)\):\s*(.*)", RegexOptions.Compiled);

    /// <summary>
    /// The debug mode for compiling effects.
    /// </summary>
    /// <value>The debug mode to use when compiling effects.</value>
    public virtual EffectProcessorDebugMode DebugMode { get; set; } = EffectProcessorDebugMode.Auto;

    /// <summary>
    /// Define assignments for the effect.
    /// </summary>
    /// <value>A list of define assignments delimited by semicolons.</value>
    public virtual string Defines { get; set; } = "";

    /// <summary>
    /// Enables the persistent content-addressed compiler cache.
    /// </summary>
    public virtual bool EnableCache { get; set; } = true;

    /// <summary>
    /// Overrides the compiler cache directory. An empty value uses the content intermediate directory.
    /// </summary>
    public virtual string CacheDirectory { get; set; } = Environment.GetEnvironmentVariable("MONOGAME_SHADER_CACHE_DIRECTORY") ?? "";

    /// <summary>
    /// Gets or sets the maximum compiler cache size in megabytes.
    /// </summary>
    public virtual int CacheSizeMegabytes { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the shader build mode. Local builds default to development behavior.
    /// </summary>
    public virtual ShaderBuildMode BuildMode { get; set; } = GetBuildMode();

    /// <summary>
    /// Gets or sets whether production Metal libraries must be cooked offline.
    /// </summary>
    public virtual bool MetalOfflineLibraries { get; set; } = GetBoolean("MONOGAME_METAL_OFFLINE_LIBRARIES");

    /// <summary>
    /// Gets or sets the macOS deployment target used for offline Metal libraries.
    /// </summary>
    public virtual string MetalDeploymentTarget { get; set; } = Environment.GetEnvironmentVariable("MONOGAME_METAL_DEPLOYMENT_TARGET") ?? "11.0";

    /// <summary>
    /// Gets or sets whether prepared Metal shaders may fall back to runtime SPIR-V translation.
    /// </summary>
    public virtual bool MetalRuntimeFallback { get; set; } = GetBoolean("MONOGAME_METAL_RUNTIME_FALLBACK");

    /// <summary>
    /// Overrides the bundled offline SPIR-V-to-MSL compiler path.
    /// </summary>
    public virtual string MetalShaderCompilerPath { get; set; } = Environment.GetEnvironmentVariable("MONOGAME_METAL_SHADER_COMPILER") ?? "";

    /// <summary>
    /// Gets or sets whether production effect passes are stripped using manifests and keep rules.
    /// </summary>
    public virtual bool EnableVariantStripping { get; set; } = GetBoolean("MONOGAME_ENABLE_VARIANT_STRIPPING");

    /// <summary>
    /// Gets or sets semicolon-separated pipeline manifest paths used for variant discovery.
    /// </summary>
    public virtual string VariantManifestPaths { get; set; } = Environment.GetEnvironmentVariable("MONOGAME_VARIANT_MANIFEST_PATHS") ?? "";

    /// <summary>
    /// Gets or sets semicolon-separated variant keep rules.
    /// </summary>
    public virtual string VariantKeepRules { get; set; } = Environment.GetEnvironmentVariable("MONOGAME_VARIANT_KEEP_RULES") ?? "";

    /// <summary>
    /// Gets or sets the human-readable variant report path.
    /// </summary>
    public virtual string VariantReportPath { get; set; } = Environment.GetEnvironmentVariable("MONOGAME_VARIANT_REPORT_PATH") ?? "";

    /// <summary>
    /// Gets or sets whether framework stock-effect variants are conservatively retained.
    /// </summary>
    public virtual bool KeepFrameworkVariants { get; set; } = GetBoolean("MONOGAME_KEEP_FRAMEWORK_VARIANTS", true);

    /// <summary>
    /// Gets or sets the pass count that triggers a combinatorial-growth warning.
    /// </summary>
    public virtual int VariantWarningThreshold { get; set; } = 256;

    /// <summary>
    /// Processes the string representation of the specified effect into a platform-specific binary format using the specified context.
    /// </summary>
    /// <param name="input">The effect string to be processed.</param>
    /// <param name="context">Context for the specified processor.</param>
    /// <returns>A platform-specific compiled binary effect.</returns>
    /// <remarks>If you get an error during processing, compilation stops immediately. The effect processor displays an error message. Once you fix the current error, it is possible you may get more errors on subsequent compilation attempts.</remarks>
    public override CompiledEffectContent Process(EffectContent input, ContentProcessorContext context)
    {
        context.Logger.Log(LogLevel.Info, $"Shader build mode: {BuildMode}");
        MetalShaderCooker? metalCooker = null;
        if (MetalOfflineLibraries)
        {
            if (BuildMode != ShaderBuildMode.Production)
                throw new InvalidContentException("Offline Metal libraries require ShaderBuildMode.Production.", input.Identity);
            if (!OperatingSystem.IsMacOS() || !File.Exists("/usr/bin/xcrun"))
                throw new InvalidContentException("Production Metal cooking requires macOS with Xcode command-line tools. Install Xcode and run 'xcode-select --install'.", input.Identity);
        }

        var sourceIdentity = input.Identity ?? throw new InvalidContentException("Effect content is missing source identity.");
        var sourceFile = sourceIdentity.SourceFilename;
        if (string.IsNullOrEmpty(sourceFile))
            throw new InvalidContentException("Effect content is missing a source file.", sourceIdentity);

        var options = new Options
        {
            SourceFile = sourceFile,
            Profile = ShaderProfile.GetProfileForPlatform(context.TargetPlatform),
            Debug = DebugMode == EffectProcessorDebugMode.Debug,
            Defines = Defines,
            OutputFile = context.OutputFilename
        };
        if (MetalOfflineLibraries)
        {
            if (options.Profile != ShaderProfile.Vulkan)
                throw new InvalidContentException("Offline Metal libraries require the Vulkan effect profile used by DesktopVK and MacMetal.", input.Identity);
            metalCooker = MetalShaderCooker.Create(
                MetalShaderCompilerPath,
                MetalDeploymentTarget,
                MetalRuntimeFallback,
                input.Identity ?? new ContentIdentity());
        }
        if (EnableVariantStripping && BuildMode != ShaderBuildMode.Production)
            throw new InvalidContentException("Variant stripping requires ShaderBuildMode.Production.", input.Identity);
        var variantStripper = EffectVariantStripper.Create(
            EnableVariantStripping,
            KeepFrameworkVariants,
            VariantWarningThreshold,
            VariantReportPath,
            VariantManifestPaths,
            VariantKeepRules,
            input.Identity ?? new ContentIdentity(sourceFile),
            context);

        // Parse the MGFX file expanding includes, macros, and returning the techniques.
        ShaderResult shaderResult;
        try
        {
            shaderResult = ShaderResult.FromFile(options.SourceFile, options,
                new ContentPipelineEffectCompilerOutput(context));

            // Add the include dependencies so that if they change
            // it will trigger a rebuild of this effect.
            foreach (var dep in shaderResult.Dependencies)
                context.AddDependency(dep);
        }
        catch (InvalidContentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // TODO: Extract good line numbers from mgfx parser!
            throw new InvalidContentException(ex.Message, input.Identity, ex);
        }

        var compilerIdentity = $"{typeof(EffectObject).Assembly.GetName().Version}:{typeof(EffectObject).Module.ModuleVersionId:N}:{EffectObject.Version}";
        var cache = new EffectCompilerCache(string.IsNullOrWhiteSpace(CacheDirectory)
            ? Path.Combine(context.IntermediateDirectory, "mgshadercache")
            : CacheDirectory);
        var cacheKey = EffectCompilerCache.ComputeKey(
            sourceFile,
            shaderResult.Dependencies,
            options.Profile.Name,
            options.Debug,
            options.Defines,
            compilerIdentity,
            BuildMode,
            MetalOfflineLibraries,
            metalCooker?.Identity ?? string.Empty,
            variantStripper.Identity);
        var cacheReadResult = EffectCompilerCacheReadResult.Missing;
        if (EnableCache && cache.TryRead(cacheKey, out var cachedEffectCode, out cacheReadResult))
        {
            context.Logger.Log(LogLevel.Info, $"Shader compiler cache hit: {cacheKey}");
            return new CompiledEffectContent(cachedEffectCode);
        }
        if (EnableCache)
            context.Logger.Log(LogLevel.Info, $"Shader compiler cache miss: {cacheKey} ({cacheReadResult})");

        // Create the effect object.
        EffectObject? effect = null;
        var shaderErrorsAndWarnings = string.Empty;
        try
        {
            effect = EffectObject.CompileEffect(shaderResult, out shaderErrorsAndWarnings);
            variantStripper.Process(effect, context);
            metalCooker?.Cook(effect, context);

            // If there were any additional output files we register
            // them so that the cleanup process can manage them.
            foreach (var outfile in shaderResult.AdditionalOutputFiles)
                context.AddOutputFile(outfile);
        }
        catch (ShaderCompilerException)
        {
            // This will log any warnings and errors and throw.
            ProcessErrorsAndWarnings(true, shaderErrorsAndWarnings, input, context);
        }

        // Process any warning messages that the shader compiler might have produced.
        ProcessErrorsAndWarnings(false, shaderErrorsAndWarnings, input, context);

        // Write out the effect to a runtime format.
        CompiledEffectContent result;
        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            (effect ?? throw new InvalidContentException("Effect compilation did not produce an effect object.", sourceIdentity)).Write(writer, options);

            var effectCode = stream.ToArray();
            result = new CompiledEffectContent(effectCode);
            if (EnableCache)
            {
                cache.Write(cacheKey, effectCode, compilerIdentity, options.Profile.Name, options.Debug, options.Defines);
                cache.Trim(Math.Max(0, CacheSizeMegabytes) * 1024L * 1024L);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidContentException("Failed to serialize the effect!", input.Identity, ex);
        }

        return result;
    }

    private static void ProcessErrorsAndWarnings(bool buildFailed, string shaderErrorsAndWarnings, EffectContent input, ContentProcessorContext context)
    {
        // Split the errors and warnings into individual lines.
        var errorsAndWarningArray = shaderErrorsAndWarnings.Split(["\n", "\r", Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        ContentIdentity? identity = null;
        var allErrorsAndWarnings = new System.Text.StringBuilder();

        // Process all the lines.
        foreach (var errorOrWarningLine in errorsAndWarningArray)
        {
            var match = errorOrWarning.Match(errorOrWarningLine);
            if (!match.Success || match.Groups.Count != 4)
            {
                // Just log anything we don't recognize as a warning.
                if (buildFailed)
                    allErrorsAndWarnings.AppendLine(errorOrWarningLine);
                else
                    context.Logger.Log(LogLevel.Warning, input.Identity ?? new ContentIdentity(), errorOrWarningLine);

                continue;
            }

            var fileName = match.Groups[1].Value;
            var lineAndColumn = match.Groups[2].Value;
            var message = match.Groups[3].Value;

            // Try to ensure a good file name for the error message.
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = input.Identity?.SourceFilename ?? string.Empty;
            }
            else if (!File.Exists(fileName))
            {
                var folder = Path.GetDirectoryName(input.Identity?.SourceFilename ?? string.Empty) ?? "";
                fileName = Path.Combine(folder, fileName);
            }

            var newIdentity = new ContentIdentity(fileName, input.Identity?.SourceTool, lineAndColumn);

            // If we got an exception then we'll be throwing an exception
            // below, so just gather the lines to throw later.
            if (buildFailed)
            {
                if (identity == null)
                {
                    identity = newIdentity;
                    allErrorsAndWarnings.AppendLine(message);
                }
                else
                    allErrorsAndWarnings.AppendLine(errorOrWarningLine);
            }
            else
                context.Logger.Log(LogLevel.Warning, newIdentity, message);
        }

        if (buildFailed)
        {
            throw new InvalidContentException(allErrorsAndWarnings.ToString(), identity ?? input.Identity);
        }
    }

    private class ContentPipelineEffectCompilerOutput : IEffectCompilerOutput
    {
        private readonly ContentProcessorContext _context;

        public ContentPipelineEffectCompilerOutput(ContentProcessorContext context)
        {
            _context = context;
        }

        public void WriteWarning(string file, int line, int column, string message)
        {
            _context.Logger.Log(LogLevel.Warning, CreateContentIdentity(file, line, column), message);
        }

        public void WriteError(string file, int line, int column, string message)
        {
            throw new InvalidContentException(message, CreateContentIdentity(file, line, column));
        }

        private static ContentIdentity CreateContentIdentity(string file, int line, int column)
        {
            return new ContentIdentity(file, null, line + "," + column);
        }
    }

    private static ShaderBuildMode GetBuildMode()
        => Enum.TryParse<ShaderBuildMode>(Environment.GetEnvironmentVariable("MONOGAME_SHADER_BUILD_MODE"), true, out var mode)
            ? mode
            : ShaderBuildMode.Development;

    private static bool GetBoolean(string name, bool defaultValue = false)
        => bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : defaultValue;
}
