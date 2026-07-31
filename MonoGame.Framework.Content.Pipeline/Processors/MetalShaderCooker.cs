// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using MonoGame.Effect;

namespace Microsoft.Xna.Framework.Content.Pipeline.Processors;

internal sealed class MetalShaderCooker
{
    private const string Xcrun = "/usr/bin/xcrun";
    private readonly string _compilerPath;
    private readonly string _deploymentTarget;
    private readonly bool _allowRuntimeFallback;
    private readonly ContentIdentity _contentIdentity;

    private MetalShaderCooker(
        string compilerPath,
        string deploymentTarget,
        bool allowRuntimeFallback,
        ContentIdentity contentIdentity,
        string translatorIdentity,
        string sdkVersion,
        string metalCompilerVersion)
    {
        _compilerPath = compilerPath;
        _deploymentTarget = deploymentTarget;
        _allowRuntimeFallback = allowRuntimeFallback;
        _contentIdentity = contentIdentity;
        CompatibilityMetadata = JsonSerializer.Serialize(new
        {
            schemaVersion = MetalShaderPayload.Version,
            translator = translatorIdentity,
            platform = "macos",
            mslLanguageVersion = "2.0",
            deploymentTarget,
            sdkVersion,
            metalCompilerVersion,
        });
        Identity = CompatibilityMetadata + $";runtime-fallback={allowRuntimeFallback}";
    }

    public string CompatibilityMetadata { get; }

    public string Identity { get; }

    public static MetalShaderCooker Create(
        string compilerPath,
        string deploymentTarget,
        bool allowRuntimeFallback,
        ContentIdentity contentIdentity)
    {
        if (!OperatingSystem.IsMacOS() || !File.Exists(Xcrun))
            throw new InvalidContentException("Production Metal cooking requires macOS with Xcode command-line tools. Install Xcode and run 'xcode-select --install'.", contentIdentity);
        if (string.IsNullOrWhiteSpace(deploymentTarget))
            throw new InvalidContentException("MetalDeploymentTarget must specify a macOS deployment version such as '11.0'.", contentIdentity);

        var resolvedCompiler = string.IsNullOrWhiteSpace(compilerPath) ? FindBundledCompiler() : Path.GetFullPath(compilerPath);
        var translatorIdentity = Run(resolvedCompiler, ["--identity"], contentIdentity).Trim();
        var sdkVersion = Run(Xcrun, ["--sdk", "macosx", "--show-sdk-version"], contentIdentity).Trim();
        var metalVersion = Run(Xcrun, ["--sdk", "macosx", "metal", "--version"], contentIdentity).Trim();
        return new MetalShaderCooker(
            resolvedCompiler,
            deploymentTarget,
            allowRuntimeFallback,
            contentIdentity,
            translatorIdentity,
            sdkVersion,
            metalVersion);
    }

    public void Cook(EffectObject effect, ContentProcessorContext context)
    {
        foreach (var shader in effect.Shaders)
            Cook(shader, context.IntermediateDirectory);
        context.Logger.Log(LogLevel.Info, $"Cooked {effect.Shaders.Count} Metal shader libraries for macOS {_deploymentTarget}.");
    }

    private void Cook(ShaderData shader, string intermediateDirectory)
    {
        if (shader.Bytecode.Length == 0 || (shader.Bytecode.Length & 3) != 0 ||
            shader.ShaderCode.Length < shader.Bytecode.Length ||
            !shader.ShaderCode.AsSpan(shader.ShaderCode.Length - shader.Bytecode.Length).SequenceEqual(shader.Bytecode))
            throw new InvalidContentException("Metal offline cooking requires a Vulkan SPIR-V shader payload.", _contentIdentity);

        var shaderKey = Convert.ToHexString(SHA256.HashData(shader.Bytecode)).ToLowerInvariant();
        var directory = Path.Combine(intermediateDirectory, "mgmetal", shaderKey);
        Directory.CreateDirectory(directory);
        var spirvPath = Path.Combine(directory, "shader.spv");
        var mslPath = Path.Combine(directory, "shader.metal");
        var entryPath = Path.Combine(directory, "shader.entry");
        var airPath = Path.Combine(directory, "shader.air");
        var libraryPath = Path.Combine(directory, "shader.metallib");
        File.WriteAllBytes(spirvPath, shader.Bytecode);

        Run(_compilerPath, [spirvPath, mslPath, entryPath], _contentIdentity);
        Run(Xcrun,
            ["--sdk", "macosx", "metal", "-std=macos-metal2.0", $"-mmacosx-version-min={_deploymentTarget}", "-c", mslPath, "-o", airPath],
            _contentIdentity);
        Run(Xcrun, ["--sdk", "macosx", "metallib", airPath, "-o", libraryPath], _contentIdentity);

        var entryPoint = File.ReadAllText(entryPath).Trim();
        var library = File.ReadAllBytes(libraryPath);
        shader.ShaderCode = MetalShaderPayload.Append(
            shader.ShaderCode,
            shader.Bytecode.Length,
            library,
            entryPoint,
            CompatibilityMetadata,
            _allowRuntimeFallback);
    }

    private static string FindBundledCompiler()
    {
        var executableName = "mgmetalcompiler";
        var assemblyDirectory = Path.GetDirectoryName(typeof(MetalShaderCooker).Assembly.Location) ?? AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(assemblyDirectory, executableName),
            Path.Combine(assemblyDirectory, "runtimes", "osx", "native", executableName),
            Path.Combine(AppContext.BaseDirectory, executableName),
        };
        return candidates.FirstOrDefault(File.Exists) ?? executableName;
    }

    private static string Run(string executable, IReadOnlyList<string> arguments, ContentIdentity contentIdentity)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidContentException($"Could not start '{executable}'.", contentIdentity);
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidContentException($"'{Path.GetFileName(executable)}' failed with exit code {process.ExitCode}: {standardError.Trim()}", contentIdentity);
            return standardOutput;
        }
        catch (Win32Exception exception)
        {
            throw new InvalidContentException(
                $"Production Metal cooking requires '{Path.GetFileName(executable)}'. Run 'Build Native Metal' or set MetalShaderCompilerPath to the bundled compiler.",
                contentIdentity,
                exception);
        }
    }
}