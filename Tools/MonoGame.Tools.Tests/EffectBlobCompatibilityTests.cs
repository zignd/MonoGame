using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MonoGame.Tests.ContentPipeline;

[TestFixture]
public class EffectBlobCompatibilityTests
{
    private static readonly byte[] MgfxSignature = { (byte)'M', (byte)'G', (byte)'F', (byte)'X' };

    [TestCase("DirectX12", 2)]
    [TestCase("Vulkan", 80)]
    public void ExistingNativeEffectBlobsRetainCompatibleHeaders(string profileDirectory, int expectedProfile)
    {
        var directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Assets", "Effects", profileDirectory);
        var effectFiles = Directory.GetFiles(directory, "*.xnb").OrderBy(path => path).ToArray();

        Assert.That(effectFiles, Is.Not.Empty, $"No compatibility fixtures found in {directory}.");
        foreach (var effectFile in effectFiles)
        {
            var bytes = File.ReadAllBytes(effectFile);
            var headerOffset = FindMgfxHeader(bytes);

            Assert.That(headerOffset, Is.GreaterThanOrEqualTo(0), $"{effectFile} has no MGFX header.");
            Assert.That(bytes[headerOffset + 4], Is.InRange(10, 11), $"{effectFile} uses an unsupported MGFX version.");
            Assert.That(bytes[headerOffset + 5], Is.EqualTo(expectedProfile), $"{effectFile} changed shader profile.");
        }
    }

    private static int FindMgfxHeader(byte[] bytes)
    {
        for (var index = 0; index <= bytes.Length - MgfxSignature.Length - 2; index++)
        {
            if (bytes.AsSpan(index, MgfxSignature.Length).SequenceEqual(MgfxSignature))
                return index;
        }

        return -1;
    }
}