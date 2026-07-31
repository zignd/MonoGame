using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Effect;
using NUnit.Framework;

namespace MonoGame.Tests.ContentPipeline;

[TestFixture]
internal sealed class EffectVariantStripperTests
{
    private static readonly string BasicEffectPath = TestEnvironment.GetRepositoryPath(
        "MonoGame.Framework", "Platform", "Graphics", "Effect", "Resources", "BasicEffect.fx");

    [Test]
    public void ManifestRetainsObservedPairAndWritesReasonedReport()
    {
        using var directory = new TemporaryDirectory();
        using var context = new TestProcessorContext(TargetPlatform.DesktopVK, "BasicEffect.xnb");
        var effect = CompileBasicEffect();
        var originalTechniqueCount = effect.Techniques.Length;
        var pass = effect.Techniques[0].pass_handles[0];
        var vertexIndex = EffectObject.GetShaderIndex(EffectObject.STATE_CLASS.VERTEXSHADER, pass.states);
        var pixelIndex = EffectObject.GetShaderIndex(EffectObject.STATE_CLASS.PIXELSHADER, pass.states);
        var description = Description(
            Shader.ComputeArtifactKey(ShaderStage.Vertex, effect.Shaders[vertexIndex].ShaderCode),
            Shader.ComputeArtifactKey(ShaderStage.Pixel, effect.Shaders[pixelIndex].ShaderCode));
        var manifestPath = Path.Combine(directory.Path, "usage.json");
        var pipelineKey = description.ComputeKey();
        ShaderPipelineManifestStore.Save(manifestPath, new ShaderPipelineManifest
        {
            Pipelines = new[] { new ShaderPipelineManifestEntry(pipelineKey, 100, "test") },
            Descriptions = new System.Collections.Generic.Dictionary<string, ShaderPipelineDescription>
            {
                [pipelineKey] = description,
            },
        });
        var reportPath = Path.Combine(directory.Path, "variants.json");
        var stripper = EffectVariantStripper.Create(
            true, false, 256, reportPath, manifestPath, "", new ContentIdentity(BasicEffectPath), context);

        stripper.Process(effect, context);

        Assert.That(effect.Techniques.Length, Is.LessThan(originalTechniqueCount));
        Assert.That(effect.Techniques.Length, Is.GreaterThan(0));
        AssertShaderIndicesAreValid(effect);
        Assert.That(File.ReadAllText(reportPath), Does.Contain("\"Reason\": \"manifest\""));
    }

    [Test]
    public void KeepRuleRetainsNamedTechniqueAndRemapsShaders()
    {
        using var directory = new TemporaryDirectory();
        using var context = new TestProcessorContext(TargetPlatform.DesktopVK, "BasicEffect.xnb");
        var effect = CompileBasicEffect();
        var stripper = EffectVariantStripper.Create(
            true,
            false,
            256,
            Path.Combine(directory.Path, "variants.json"),
            "",
            "technique:BasicEffect",
            new ContentIdentity(BasicEffectPath),
            context);

        stripper.Process(effect, context);

        Assert.That(effect.Techniques, Has.Length.EqualTo(1));
        Assert.That(effect.Techniques[0].name, Is.EqualTo("BasicEffect"));
        AssertShaderIndicesAreValid(effect);
    }

    [Test]
    public void UnmatchedKeepRuleFailsBeforeRemovingEveryPass()
    {
        using var directory = new TemporaryDirectory();
        using var context = new TestProcessorContext(TargetPlatform.DesktopVK, "BasicEffect.xnb");
        var effect = CompileBasicEffect();
        var stripper = EffectVariantStripper.Create(
            true,
            false,
            256,
            Path.Combine(directory.Path, "variants.json"),
            "",
            "technique:Missing",
            new ContentIdentity(BasicEffectPath),
            context);

        Assert.Throws<InvalidContentException>(() => stripper.Process(effect, context));
        Assert.That(File.ReadAllText(Path.Combine(directory.Path, "variants.json")), Does.Contain("\"RetainedCount\": 0"));
    }

    [Test]
    public void GrowthWarningIdentifiesResponsibleAxes()
    {
        using var directory = new TemporaryDirectory();
        using var context = new TestProcessorContext(TargetPlatform.DesktopVK, "BasicEffect.xnb");
        var effect = CompileBasicEffect();
        var reportPath = Path.Combine(directory.Path, "variants.json");
        var stripper = EffectVariantStripper.Create(
            false, true, 1, reportPath, "", "", new ContentIdentity(BasicEffectPath), context);

        stripper.Process(effect, context);

        var report = File.ReadAllText(reportPath);
        Assert.That(report, Does.Contain("techniques=").And.Contain("vertexShaders=").And.Contain("pixelShaders="));
    }

    [Test]
    public void DevelopmentModeRejectsVariantStripping()
    {
        using var context = new TestProcessorContext(TargetPlatform.DesktopVK, "SpriteEffect.xnb");
        var input = new EffectContent { Identity = new ContentIdentity(BasicEffectPath) };
        var processor = new EffectProcessor { EnableVariantStripping = true };

        Assert.Throws<InvalidContentException>(() => processor.Process(input, context));
    }

    private static EffectObject CompileBasicEffect()
    {
        var options = new Options
        {
            SourceFile = BasicEffectPath,
            OutputFile = "BasicEffect.mgfxo",
            Profile = ShaderProfile.Vulkan,
        };
        var result = ShaderResult.FromFile(BasicEffectPath, options, new NullEffectCompilerOutput());
        var effect = EffectObject.CompileEffect(result, out var errorsAndWarnings);
        Assert.That(errorsAndWarnings, Is.Empty);
        return effect;
    }

    private static ShaderPipelineDescription Description(string vertexShader, string pixelShader)
        => new()
        {
            ShaderArtifacts = new System.Collections.Generic.Dictionary<string, string>
            {
                ["Vertex"] = vertexShader,
                ["Pixel"] = pixelShader,
            },
            VertexLayout = "test",
            PrimitiveTopology = "TriangleList",
            BlendState = "Opaque",
            DepthStencilState = "None",
            RasterizerState = "CullNone",
            RenderTargetFormats = "Color",
            DepthStencilFormat = "None",
        };

    private static void AssertShaderIndicesAreValid(EffectObject effect)
    {
        foreach (var pass in effect.Techniques.SelectMany(technique => technique.pass_handles))
        {
            var vertexIndex = EffectObject.GetShaderIndex(EffectObject.STATE_CLASS.VERTEXSHADER, pass.states);
            var pixelIndex = EffectObject.GetShaderIndex(EffectObject.STATE_CLASS.PIXELSHADER, pass.states);
            Assert.That(vertexIndex, Is.InRange(-1, effect.Shaders.Count - 1));
            Assert.That(pixelIndex, Is.InRange(-1, effect.Shaders.Count - 1));
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mg-variant-tests-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory()
            => Directory.CreateDirectory(Path);

        public void Dispose()
            => Directory.Delete(Path, recursive: true);
    }
}