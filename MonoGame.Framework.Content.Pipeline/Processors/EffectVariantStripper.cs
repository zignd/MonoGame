// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using MonoGame.Effect;

namespace Microsoft.Xna.Framework.Content.Pipeline.Processors;

internal sealed class EffectVariantStripper
{
    private readonly bool _enabled;
    private readonly bool _keepFrameworkVariants;
    private readonly int _warningThreshold;
    private readonly string _reportPath;
    private readonly ContentIdentity _contentIdentity;
    private readonly HashSet<string> _requiredPairs;
    private readonly string[] _keepRules;

    private EffectVariantStripper(
        bool enabled,
        bool keepFrameworkVariants,
        int warningThreshold,
        string reportPath,
        ContentIdentity contentIdentity,
        HashSet<string> requiredPairs,
        string[] keepRules,
        string identity)
    {
        _enabled = enabled;
        _keepFrameworkVariants = keepFrameworkVariants;
        _warningThreshold = warningThreshold;
        _reportPath = reportPath;
        _contentIdentity = contentIdentity;
        _requiredPairs = requiredPairs;
        _keepRules = keepRules;
        Identity = identity;
    }

    public string Identity { get; }

    public static EffectVariantStripper Create(
        bool enabled,
        bool keepFrameworkVariants,
        int warningThreshold,
        string reportPath,
        string manifestPaths,
        string keepRules,
        ContentIdentity contentIdentity,
        ContentProcessorContext context)
    {
        var paths = Split(manifestPaths).Select(Path.GetFullPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var manifests = new List<ShaderPipelineManifest>();
        using var identityStream = new MemoryStream();
        using var identityWriter = new BinaryWriter(identityStream, Encoding.UTF8, leaveOpen: true);
        identityWriter.Write(enabled);
        identityWriter.Write(keepFrameworkVariants);
        identityWriter.Write(warningThreshold);
        foreach (var path in paths)
        {
            context.AddDependency(path);
            var bytes = File.ReadAllBytes(path);
            identityWriter.Write(path.Replace('\\', '/'));
            identityWriter.Write(bytes.Length);
            identityWriter.Write(bytes);
            manifests.Add(ShaderPipelineManifestStore.Load(path));
        }

        var rules = Split(keepRules).OrderBy(rule => rule, StringComparer.Ordinal).ToArray();
        foreach (var rule in rules)
            identityWriter.Write(rule);
        identityWriter.Flush();

        var requiredPairs = new HashSet<string>(StringComparer.Ordinal);
        if (manifests.Count > 0)
        {
            var manifest = ShaderPipelineManifestStore.Merge(manifests);
            foreach (var description in manifest.Descriptions.Values)
            {
                description.ShaderArtifacts.TryGetValue("Vertex", out var vertexShader);
                description.ShaderArtifacts.TryGetValue("Pixel", out var pixelShader);
                requiredPairs.Add(Pair(vertexShader, pixelShader));
            }
        }

        return new EffectVariantStripper(
            enabled,
            keepFrameworkVariants,
            Math.Max(1, warningThreshold),
            reportPath,
            contentIdentity,
            requiredPairs,
            rules,
            Convert.ToHexStringLower(SHA256.HashData(identityStream.GetBuffer().AsSpan(0, checked((int)identityStream.Length)))));
    }

    public void Process(EffectObject effect, ContentProcessorContext context)
    {
        var sourcePath = Path.GetFullPath(_contentIdentity.SourceFilename).Replace('\\', '/');
        var frameworkEffect = sourcePath.Contains("/MonoGame.Framework/Platform/Graphics/Effect/Resources/", StringComparison.Ordinal);
        var matchedRules = new HashSet<string>(StringComparer.Ordinal);
        var retainedPasses = new HashSet<(int Technique, int Pass)>();
        var variants = new List<VariantDecision>();
        for (var techniqueIndex = 0; techniqueIndex < effect.Techniques.Length; techniqueIndex++)
        {
            var technique = effect.Techniques[techniqueIndex];
            for (var passIndex = 0; passIndex < technique.pass_handles.Length; passIndex++)
            {
                var pass = technique.pass_handles[passIndex];
                var vertexIndex = EffectObject.GetShaderIndex(EffectObject.STATE_CLASS.VERTEXSHADER, pass.states);
                var pixelIndex = EffectObject.GetShaderIndex(EffectObject.STATE_CLASS.PIXELSHADER, pass.states);
                var vertexKey = vertexIndex < 0 ? string.Empty : ComputeShaderKey(effect.Shaders[vertexIndex]);
                var pixelKey = pixelIndex < 0 ? string.Empty : ComputeShaderKey(effect.Shaders[pixelIndex]);
                var retained = !_enabled;
                var reason = retained ? "stripping-disabled" : "not-declared";

                if (_enabled && frameworkEffect && _keepFrameworkVariants)
                {
                    retained = true;
                    reason = "framework-required";
                }
                if (_enabled && _requiredPairs.Contains(Pair(vertexKey, pixelKey)))
                {
                    retained = true;
                    reason = "manifest";
                }
                foreach (var rule in _keepRules)
                {
                    if (!Matches(rule, technique.name, pass.name, vertexKey, pixelKey))
                        continue;
                    retained = true;
                    reason = $"keep-rule:{rule}";
                    matchedRules.Add(rule);
                }

                if (retained)
                    retainedPasses.Add((techniqueIndex, passIndex));
                variants.Add(new VariantDecision(technique.name, pass.name, vertexKey, pixelKey, retained, reason));
            }
        }

        var combinatorialWarning = variants.Count > _warningThreshold
            ? CreateGrowthWarning(variants)
            : string.Empty;
        if (combinatorialWarning.Length > 0)
            context.Logger.Log(LogLevel.Warning, _contentIdentity, combinatorialWarning);
        WriteReport(context, variants, combinatorialWarning);
        if (_enabled)
        {
            var unmatchedRules = _keepRules.Where(rule => !matchedRules.Contains(rule)).ToArray();
            if (unmatchedRules.Length > 0)
                throw new InvalidContentException($"Variant keep rules matched no passes: {string.Join(", ", unmatchedRules)}", _contentIdentity);
            if (retainedPasses.Count == 0)
                throw new InvalidContentException("Variant stripping would remove every effect pass. Add a usage manifest or VariantKeepRules.", _contentIdentity);
        }
        if (_enabled)
            effect.RetainVariants(retainedPasses);
    }

    private static string CreateGrowthWarning(IReadOnlyList<VariantDecision> variants)
    {
        var techniques = variants
            .GroupBy(variant => variant.Technique, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Take(5)
            .Select(group => $"{group.Key}={group.Count()}");
        var vertexShaders = variants.Select(variant => variant.VertexShader).Where(key => key.Length > 0).Distinct(StringComparer.Ordinal).Count();
        var pixelShaders = variants.Select(variant => variant.PixelShader).Where(key => key.Length > 0).Distinct(StringComparer.Ordinal).Count();
        return $"Effect variant growth: passes={variants.Count}, techniques={variants.Select(variant => variant.Technique).Distinct(StringComparer.Ordinal).Count()}, vertexShaders={vertexShaders}, pixelShaders={pixelShaders}; largest technique axes: {string.Join(", ", techniques)}.";
    }

    private void WriteReport(ContentProcessorContext context, IReadOnlyList<VariantDecision> variants, string warning)
    {
        var path = string.IsNullOrWhiteSpace(_reportPath)
            ? Path.Combine(context.IntermediateDirectory, Path.GetFileNameWithoutExtension(context.OutputFilename) + ".variants.json")
            : Path.GetFullPath(_reportPath);
        var report = new VariantReport(
            1,
            _contentIdentity.SourceFilename,
            _enabled,
            variants.Count,
            variants.Count(variant => variant.Retained),
            variants.Count(variant => !variant.Retained),
            warning,
            variants);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory());
        var temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, path, overwrite: true);
        context.AddOutputFile(path);
        context.Logger.Log(LogLevel.Info, $"Shader variant report: {path} ({report.RetainedCount} retained, {report.RemovedCount} removed)");
    }

    private static bool Matches(string rule, string technique, string pass, string vertexKey, string pixelKey)
    {
        if (rule == "*")
            return true;
        if (rule.StartsWith("technique:", StringComparison.Ordinal))
            return StringComparer.Ordinal.Equals(rule[10..], technique);
        if (rule.StartsWith("pass:", StringComparison.Ordinal))
            return StringComparer.Ordinal.Equals(rule[5..], technique + "/" + pass);
        var shader = rule.StartsWith("shader:", StringComparison.Ordinal) ? rule[7..] : rule;
        return StringComparer.Ordinal.Equals(shader, vertexKey) || StringComparer.Ordinal.Equals(shader, pixelKey);
    }

    private static string ComputeShaderKey(ShaderData shader)
    {
        var identity = new byte[shader.ShaderCode.Length + 1];
        identity[0] = shader.IsVertexShader ? (byte)0 : (byte)1;
        shader.ShaderCode.CopyTo(identity, 1);
        return Convert.ToHexStringLower(SHA256.HashData(identity));
    }

    private static string Pair(string? vertexShader, string? pixelShader)
        => (vertexShader ?? string.Empty) + ":" + (pixelShader ?? string.Empty);

    private static string[] Split(string values)
        => values.Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed record VariantDecision(
        string Technique,
        string Pass,
        string VertexShader,
        string PixelShader,
        bool Retained,
        string Reason);

    private sealed record VariantReport(
        int SchemaVersion,
        string Source,
        bool StrippingEnabled,
        int VariantCount,
        int RetainedCount,
        int RemovedCount,
        string CombinatorialWarning,
        IReadOnlyList<VariantDecision> Variants);
}