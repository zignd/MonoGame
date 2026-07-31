using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Effect;
using NUnit.Framework;

namespace MonoGame.Tests.ContentPipeline;

[TestFixture]
internal sealed class EffectCompilerCacheTests
{
    private static readonly string BasicEffectPath = TestEnvironment.GetRepositoryPath(
        "MonoGame.Framework", "Platform", "Graphics", "Effect", "Resources", "BasicEffect.fx");
    private static readonly string SpriteEffectPath = TestEnvironment.GetRepositoryPath(
        "MonoGame.Framework", "Platform", "Graphics", "Effect", "Resources", "SpriteEffect.fx");

    [Test]
    public void KeyTracksContentOptionsAndCompilerIdentity()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.WriteFile("Main.fx", "#include \"Common.fxh\"\n");
        var include = directory.WriteFile("Common.fxh", "float Value = 1;\n");
        var baseline = Key(source, include);

        Assert.That(Key(source, include), Is.EqualTo(baseline));
        Assert.That(Key(source, include, profile: "Vulkan"), Is.Not.EqualTo(baseline));
        Assert.That(Key(source, include, debug: true), Is.Not.EqualTo(baseline));
        Assert.That(Key(source, include, defines: "A=1"), Is.Not.EqualTo(baseline));
        Assert.That(Key(source, include, compiler: "2"), Is.Not.EqualTo(baseline));
        Assert.That(Key(source, include, buildMode: ShaderBuildMode.Production), Is.Not.EqualTo(baseline));
        Assert.That(Key(source, include, metalOfflineLibraries: true), Is.Not.EqualTo(baseline));

        File.WriteAllText(include, "float Value = 2;\n");
        Assert.That(Key(source, include), Is.Not.EqualTo(baseline));
    }

    [Test]
    public void IncludeEditInvalidatesOnlyDependentEffects()
    {
        using var directory = new TemporaryDirectory();
        var include = directory.WriteFile("Shared.fxh", "float Value = 1;\n");
        var firstSource = directory.WriteFile("First.fx", "#include \"Shared.fxh\"\n");
        var secondSource = directory.WriteFile("Second.fx", "#include \"Shared.fxh\"\n");
        var unrelatedSource = directory.WriteFile("Unrelated.fx", "float Other = 1;\n");
        var firstKey = Key(firstSource, include);
        var secondKey = Key(secondSource, include);
        var unrelatedKey = EffectCompilerCache.ComputeKey(unrelatedSource, Array.Empty<string>(), "Vulkan", false, "", "1");

        File.WriteAllText(include, "float Value = 2;\n");

        Assert.That(Key(firstSource, include), Is.Not.EqualTo(firstKey));
        Assert.That(Key(secondSource, include), Is.Not.EqualTo(secondKey));
        Assert.That(EffectCompilerCache.ComputeKey(unrelatedSource, Array.Empty<string>(), "Vulkan", false, "", "1"), Is.EqualTo(unrelatedKey));
    }

    [Test]
    public void UnrelatedTextureDoesNotAffectCompilerKey()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.WriteFile("Main.fx", "float Value = 1;\n");
        var texture = directory.WriteFile("Texture.png", "first");
        var baseline = EffectCompilerCache.ComputeKey(source, Array.Empty<string>(), "Vulkan", false, "", "1");

        File.WriteAllText(texture, "second");

        Assert.That(EffectCompilerCache.ComputeKey(source, Array.Empty<string>(), "Vulkan", false, "", "1"), Is.EqualTo(baseline));
    }

    [Test]
    public void EffectObjectDeduplicatesStagesSharedAcrossPasses()
    {
        var options = new Options
        {
            SourceFile = BasicEffectPath,
            OutputFile = "BasicEffect.mgfxo",
            Profile = ShaderProfile.Vulkan,
        };
        var result = ShaderResult.FromFile(BasicEffectPath, options, new NullEffectCompilerOutput());
        var stageReferences = result.ShaderInfo!.Techniques
            .SelectMany(technique => technique.Passes)
            .SelectMany(pass => new[] { (pass.vsFunction, pass.vsModel), (pass.psFunction, pass.psModel) })
            .Where(stage => !string.IsNullOrEmpty(stage.Item1))
            .ToArray();

        var effect = EffectObject.CompileEffect(result, out var errorsAndWarnings);

        Assert.That(errorsAndWarnings, Is.Empty);
        Assert.That(effect.Shaders, Has.Count.EqualTo(stageReferences.Distinct().Count()));
        Assert.That(effect.Shaders.Count, Is.LessThan(stageReferences.Length));
    }

    [Test]
    public void CorruptArtifactBecomesMissAndCanRecover()
    {
        using var directory = new TemporaryDirectory();
        var cache = new EffectCompilerCache(directory.Path);
        var expected = new byte[] { 1, 2, 3, 4 };

        cache.Write("key", expected, "compiler", "Vulkan", false, "");
        Assert.That(cache.TryRead("key", out var hit), Is.True);
        Assert.That(hit, Is.EqualTo(expected));

        File.WriteAllBytes(Path.Combine(directory.Path, "key.mgfxo"), new byte[] { 9 });
        Assert.That(cache.TryRead("key", out _, out var corruptResult), Is.False);
        Assert.That(corruptResult, Is.EqualTo(EffectCompilerCacheReadResult.LengthMismatch));

        cache.Write("key", expected, "compiler", "Vulkan", false, "");
        Assert.That(cache.TryRead("key", out var recovered), Is.True);
        Assert.That(recovered, Is.EqualTo(expected));
    }

    [TestCase(TargetPlatform.DesktopVK)]
    [TestCase(TargetPlatform.WindowsDX12)]
    public void EffectProcessorReusesUnchangedCompiledEffect(TargetPlatform targetPlatform)
    {
        using var directory = new TemporaryDirectory();
        using var context = new TestProcessorContext(targetPlatform, "SpriteEffect.xnb");
        var importer = new EffectImporter();
        var input = importer.Import(SpriteEffectPath, new NullImporterContext());
        var processor = new EffectProcessor { CacheDirectory = directory.Path };

        var first = processor.Process(input, context).GetEffectCode();
        var second = processor.Process(input, context).GetEffectCode();

        Assert.That(second, Is.EqualTo(first));
        Assert.That(context.LogMessages.Count(message => message.StartsWith("Shader compiler cache miss:", StringComparison.Ordinal)), Is.EqualTo(1));
        Assert.That(context.LogMessages.Count(message => message.StartsWith("Shader compiler cache hit:", StringComparison.Ordinal)), Is.EqualTo(1));
    }

    [Test]
    public void TrimRemovesLeastRecentlyUsedEntries()
    {
        using var directory = new TemporaryDirectory();
        var cache = new EffectCompilerCache(directory.Path);
        cache.Write("first", new byte[] { 1, 2 }, "compiler", "Vulkan", false, "");
        cache.Write("second", new byte[] { 3, 4 }, "compiler", "Vulkan", false, "");

        cache.Trim(2);

        Assert.That(cache.Inspect(), Has.Count.EqualTo(1));
        Assert.That(cache.Inspect()[0].Key, Is.EqualTo("second"));
    }

    [Test]
    public void ProcessorReportsExplicitBuildMode()
    {
        using var directory = new TemporaryDirectory();
        using var context = new TestProcessorContext(TargetPlatform.DesktopVK, "SpriteEffect.xnb");
        var input = new EffectImporter().Import(SpriteEffectPath, new NullImporterContext());
        var processor = new EffectProcessor
        {
            BuildMode = ShaderBuildMode.Production,
            CacheDirectory = directory.Path,
        };

        processor.Process(input, context);

        Assert.That(context.LogMessages, Does.Contain("Shader build mode: Production"));
    }

    [Test]
    [NonParallelizable]
    public void ProcessorUsesEnvironmentBuildDefaults()
    {
        var previousMode = Environment.GetEnvironmentVariable("MONOGAME_SHADER_BUILD_MODE");
        var previousStripping = Environment.GetEnvironmentVariable("MONOGAME_ENABLE_VARIANT_STRIPPING");
        var previousRules = Environment.GetEnvironmentVariable("MONOGAME_VARIANT_KEEP_RULES");
        try
        {
            Environment.SetEnvironmentVariable("MONOGAME_SHADER_BUILD_MODE", "Production");
            Environment.SetEnvironmentVariable("MONOGAME_ENABLE_VARIANT_STRIPPING", "true");
            Environment.SetEnvironmentVariable("MONOGAME_VARIANT_KEEP_RULES", "*");

            var processor = new EffectProcessor();

            Assert.That(processor.BuildMode, Is.EqualTo(ShaderBuildMode.Production));
            Assert.That(processor.EnableVariantStripping, Is.True);
            Assert.That(processor.VariantKeepRules, Is.EqualTo("*"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MONOGAME_SHADER_BUILD_MODE", previousMode);
            Environment.SetEnvironmentVariable("MONOGAME_ENABLE_VARIANT_STRIPPING", previousStripping);
            Environment.SetEnvironmentVariable("MONOGAME_VARIANT_KEEP_RULES", previousRules);
        }
    }

    [Test]
    public void MetalPayloadRoundTripsAndRejectsCorruptFooter()
    {
        var spirv = new byte[] { 3, 2, 35, 7 };
        var payload = MetalShaderPayload.Append(spirv, spirv.Length, new byte[] { 8, 9 }, "main0", "{\"platform\":\"macos\"}", true);

        Assert.That(MetalShaderPayload.TryRead(payload, out var spirvLength, out var library, out var entryPoint, out var metadata, out var fallback), Is.True);
        Assert.That(spirvLength, Is.EqualTo(spirv.Length));
        Assert.That(library, Is.EqualTo(new byte[] { 8, 9 }));
        Assert.That(entryPoint, Is.EqualTo("main0"));
        Assert.That(metadata, Is.EqualTo("{\"platform\":\"macos\"}"));
        Assert.That(fallback, Is.True);

        payload[^1] ^= 0x7f;
        Assert.That(MetalShaderPayload.TryRead(payload, out _, out _, out _, out _, out _), Is.False);
    }

    [Test]
    public void PreparedMetalPayloadPreservesShaderArtifactIdentity()
    {
        var spirv = new byte[] { 3, 2, 35, 7 };
        var prepared = MetalShaderPayload.Append(spirv, spirv.Length, new byte[] { 8, 9 }, "main0", "{}", false);

        Assert.That(
            Shader.ComputeArtifactKey(ShaderStage.Vertex, prepared),
            Is.EqualTo(Shader.ComputeArtifactKey(ShaderStage.Vertex, spirv)));
        Assert.That(
            Shader.ComputeArtifactKey(ShaderStage.Pixel, prepared),
            Is.Not.EqualTo(Shader.ComputeArtifactKey(ShaderStage.Vertex, spirv)));
    }

    [Test]
    public void EffectProcessorCooksOfflineMetalLibrariesDeterministically()
    {
        if (!OperatingSystem.IsMacOS())
            Assert.Ignore("Offline Metal cooking requires macOS.");
        var compilerPath = TestEnvironment.GetRepositoryPath("Artifacts", "native", "mgmetalcompiler", "macosx", "Release", "mgmetalcompiler");
        if (!File.Exists(compilerPath))
            Assert.Ignore("Build Native Metal has not produced mgmetalcompiler.");

        var input = new EffectImporter().Import(SpriteEffectPath, new NullImporterContext());
        var processor = new EffectProcessor
        {
            BuildMode = ShaderBuildMode.Production,
            MetalOfflineLibraries = true,
            MetalShaderCompilerPath = compilerPath,
            EnableCache = false,
        };
        using var firstContext = new TestProcessorContext(TargetPlatform.DesktopVK, "SpriteEffect.xnb");
        using var secondContext = new TestProcessorContext(TargetPlatform.DesktopVK, "SpriteEffect.xnb");

        var first = processor.Process(input, firstContext).GetEffectCode();
        var second = processor.Process(input, secondContext).GetEffectCode();

        Assert.That(second, Is.EqualTo(first));
        Assert.That(Contains(first, Encoding.UTF8.GetBytes("\"platform\":\"macos\"")), Is.True);
        Assert.That(firstContext.LogMessages.Any(message => message.StartsWith("Cooked ", StringComparison.Ordinal)), Is.True);
    }

    private static bool Contains(byte[] value, byte[] expected)
    {
        for (var offset = 0; offset <= value.Length - expected.Length; offset++)
        {
            if (value.AsSpan(offset, expected.Length).SequenceEqual(expected))
                return true;
        }
        return false;
    }

    private static string Key(
        string source,
        string include,
        string profile = "DirectX12",
        bool debug = false,
        string defines = "",
        string compiler = "1",
        ShaderBuildMode buildMode = ShaderBuildMode.Development,
        bool metalOfflineLibraries = false)
        => EffectCompilerCache.ComputeKey(source, new[] { include }, profile, debug, defines, compiler, buildMode, metalOfflineLibraries);

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mg-cache-tests-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public string WriteFile(string relativePath, string content)
        {
            var path = System.IO.Path.Combine(Path, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class NullImporterContext : ContentImporterContext
    {
        public override string IntermediateDirectory => string.Empty;
        public override ContentBuildLogger Logger => throw new NotSupportedException();
        public override string OutputDirectory => string.Empty;
        public override void AddDependency(string filename)
        {
        }
    }

    private sealed class NullEffectCompilerOutput : IEffectCompilerOutput
    {
        public void WriteWarning(string file, int line, int column, string message)
        {
        }

        public void WriteError(string file, int line, int column, string message)
            => throw new InvalidOperationException(message);
    }
}