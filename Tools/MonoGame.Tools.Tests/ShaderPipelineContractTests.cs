using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using NUnit.Framework;

namespace MonoGame.Tests.ContentPipeline;

[TestFixture]
public class ShaderPipelineContractTests
{
    [Test]
    public void ArtifactKeyIsStableAcrossInputOrderingAndPathSeparators()
    {
        var first = CreateArtifact(
            [new("effects\\main.fx", "source"), new("effects/common.fxh", "include")],
            new Dictionary<string, string> { ["QUALITY"] = "2", ["SKINNING"] = "1" });
        var second = CreateArtifact(
            [new("effects/common.fxh", "include"), new("effects/main.fx", "source")],
            new Dictionary<string, string> { ["SKINNING"] = "1", ["QUALITY"] = "2" });

        Assert.That(first.ComputeKey(), Is.EqualTo(second.ComputeKey()));
        Assert.That(first.ComputeKey(), Has.Length.EqualTo(64));
    }

    [TestCase("compiler")]
    [TestCase("compilerExecutable")]
    [TestCase("profile")]
    [TestCase("shaderModel")]
    [TestCase("source")]
    [TestCase("define")]
    [TestCase("binding")]
    [TestCase("reflection")]
    [TestCase("target")]
    public void ArtifactKeyChangesForEveryCompilationDimension(string dimension)
    {
        var baseline = CreateArtifact([new("main.fx", "source")], new Dictionary<string, string>());
        var changed = dimension switch
        {
            "compiler" => CreateArtifact([new("main.fx", "source")], new Dictionary<string, string>(), compilerVersion: "2"),
            "compilerExecutable" => CreateArtifact([new("main.fx", "source")], new Dictionary<string, string>(), compilerExecutableVersion: "2"),
            "profile" => CreateArtifact([new("main.fx", "source")], new Dictionary<string, string>(), profile: "Vulkan"),
            "shaderModel" => CreateArtifact([new("main.fx", "source")], new Dictionary<string, string>(), shaderModel: "6.7"),
            "source" => CreateArtifact([new("main.fx", "changed")], new Dictionary<string, string>()),
            "define" => CreateArtifact([new("main.fx", "source")], new Dictionary<string, string> { ["A"] = "1" }),
            "binding" => CreateArtifact([new("main.fx", "source")], new Dictionary<string, string>(), bindingVersion: "2"),
            "reflection" => CreateArtifact([new("main.fx", "source")], new Dictionary<string, string>(), reflectionVersion: "2"),
            "target" => CreateArtifact([new("main.fx", "source")], new Dictionary<string, string>(), target: "macOS"),
            _ => throw new AssertionException("Unknown dimension."),
        };

        Assert.That(changed.ComputeKey(), Is.Not.EqualTo(baseline.ComputeKey()));
    }

    [Test]
    public void PipelineKeyIsStableAcrossShaderOrdering()
    {
        var first = CreatePipeline(new Dictionary<string, string> { ["Vertex"] = "vs", ["Pixel"] = "ps" });
        var second = CreatePipeline(new Dictionary<string, string> { ["Pixel"] = "ps", ["Vertex"] = "vs" });

        Assert.That(first.ComputeKey(), Is.EqualTo(second.ComputeKey()));
    }

    [Test]
    public void CacheCompatibilityRejectsEveryIdentityMismatch()
    {
        var expected = new ShaderCacheMetadata(1, "Metal", "M3", "1", "macOS", "content", "compiler");
        var mismatches = new[]
        {
            expected with { SchemaVersion = 2 },
            expected with { Backend = "Vulkan" },
            expected with { Device = "M2" },
            expected with { DriverVersion = "2" },
            expected with { OperatingSystem = "Linux" },
            expected with { ContentHash = "other" },
            expected with { CompilerVersion = "other" },
        };

        Assert.That(expected.IsCompatibleWith(expected), Is.True);
        Assert.That(mismatches, Has.All.Matches<ShaderCacheMetadata>(value => !value.IsCompatibleWith(expected)));
    }

    [Test]
    public void ManifestMergeIsDeterministicAndPreservesPriorityGroups()
    {
        var description = CreatePipeline(new Dictionary<string, string> { ["Vertex"] = "vs", ["Pixel"] = "ps" });
        var key = description.ComputeKey();
        var recorded = CreateManifest(description, new ShaderPipelineManifestEntry(key, 10, "recorded", "Gameplay"));
        var declared = CreateManifest(
            description,
            new ShaderPipelineManifestEntry(key, 50, "declared", "Gameplay"),
            new ShaderPipelineManifestEntry(key, 100, "declared", "Startup"));

        var first = ShaderPipelineManifestStore.Merge([recorded, declared]);
        var second = ShaderPipelineManifestStore.Merge([declared, recorded]);

        Assert.That(first.Pipelines, Is.EqualTo(second.Pipelines));
        Assert.That(first.Descriptions.Keys, Is.EqualTo(second.Descriptions.Keys));
        Assert.That(first.Pipelines, Has.Count.EqualTo(2));
        Assert.That(first.Pipelines[0].PriorityGroup, Is.EqualTo("Startup"));
        Assert.That(first.Pipelines[1].Priority, Is.EqualTo(50));
        Assert.That(first.Pipelines[1].Source, Is.EqualTo("declared;recorded"));
    }

    [Test]
    public void ManifestValidationRejectsTamperedDescriptionIdentity()
    {
        var description = CreatePipeline(new Dictionary<string, string> { ["Vertex"] = "vs", ["Pixel"] = "ps" });
        var manifest = new ShaderPipelineManifest
        {
            Pipelines = [new("wrong", 1, "test")],
            Descriptions = new Dictionary<string, ShaderPipelineDescription> { ["wrong"] = description },
        };

        Assert.That(() => ShaderPipelineManifestStore.Validate(manifest), Throws.TypeOf<System.IO.InvalidDataException>());
    }

    [Test]
    public void NativePipelineCacheRejectsIncompatibleAndCorruptDataWithoutErasingCompilerArtifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-native-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NativePipelineCacheStore(root);
            var expected = new ShaderCacheMetadata(1, "Metal", "M3", "1", "macOS", "content", "compiler");
            var payload = new byte[] { 1, 2, 3, 4 };
            var compilerArtifact = Path.Combine(root, "compiler-artifact.mgfxo");
            File.WriteAllText(compilerArtifact, "valid");

            store.Write("startup", expected, payload);
            Assert.That(store.TryRead("startup", expected, out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(payload));

            store.Write("startup", expected, payload);
            Assert.That(store.TryRead("startup", expected with { Device = "M2" }, out _, out var incompatibleResult), Is.False);
            Assert.That(incompatibleResult, Is.EqualTo(NativePipelineCacheReadResult.Incompatible));
            Assert.That(File.Exists(compilerArtifact), Is.True);

            store.Write("startup", expected, payload);
            File.WriteAllBytes(Path.Combine(root, "startup.bin"), new byte[] { 9 });
            Assert.That(store.TryRead("startup", expected, out _, out var corruptResult), Is.False);
            Assert.That(corruptResult, Is.EqualTo(NativePipelineCacheReadResult.LengthMismatch));
            Assert.That(File.Exists(compilerArtifact), Is.True);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static ShaderArtifactIdentity CreateArtifact(
        IReadOnlyList<ShaderSourceInput> sources,
        IReadOnlyDictionary<string, string> defines,
        string compilerVersion = "1",
        string compilerExecutableVersion = "1",
        string profile = "DirectX12",
        string shaderModel = "6.0",
        string bindingVersion = "1",
        string reflectionVersion = "1",
        string target = "Windows")
        => new()
        {
            CompilerName = "mgfxc",
            CompilerVersion = compilerVersion,
            CompilerExecutableVersion = compilerExecutableVersion,
            ShaderProfile = profile,
            ShaderModel = shaderModel,
            EntryPoint = "Main",
            Stage = "Vertex",
            Optimization = "O3",
            ResourceBindingVersion = bindingVersion,
            ReflectionLayoutVersion = reflectionVersion,
            TargetOperatingSystem = target,
            Sources = sources,
            Defines = defines,
        };

    private static ShaderPipelineDescription CreatePipeline(IReadOnlyDictionary<string, string> shaders)
        => new()
        {
            ShaderArtifacts = shaders,
            VertexLayout = "PositionTexture",
            PrimitiveTopology = "TriangleList",
            BlendState = "Opaque",
            DepthStencilState = "Default",
            RasterizerState = "CullCounterClockwise",
            RenderTargetFormats = "Color",
            DepthStencilFormat = "Depth24",
        };

    private static ShaderPipelineManifest CreateManifest(
        ShaderPipelineDescription description,
        params ShaderPipelineManifestEntry[] entries)
    {
        var key = description.ComputeKey();
        return new ShaderPipelineManifest
        {
            Pipelines = entries,
            Descriptions = new Dictionary<string, ShaderPipelineDescription> { [key] = description },
        };
    }
}