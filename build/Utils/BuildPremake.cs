
using System.Runtime.InteropServices;

namespace BuildScripts;

public sealed class BuildPremake
{
    public void Run(
        BuildContext context,
        string name,
        string workingDirectory,
        string solutionFile,
        string generationOptions = "",
        string makeTarget = "")
    {
        switch (context.Environment.Platform.Family)
        {
            case PlatformFamily.Windows:
            {
                // Generate multi-arch solution in one go.
                Scaffold(context, name, workingDirectory, "--verbose vs2022");

                // Build for both architectures.
                var platformToolset = Environment.GetEnvironmentVariable("MONOGAME_WINDOWS_PLATFORM_TOOLSET");
                BuildForArch(context, name, workingDirectory, solutionFile, "x64", platformToolset);
                BuildForArch(context, name, workingDirectory, solutionFile, "ARM64", platformToolset);
                
                break;
            }
            case PlatformFamily.Linux:
            case PlatformFamily.OSX:
            {
                // Linux/macOS build for the host architecture only
                var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
                Scaffold(context, name, workingDirectory, $"--arch={arch} {generationOptions} gmake", generationOptions);
                Make(context, name, workingDirectory, makeTarget);

                break;
            }
            default:
            {
                throw new NotSupportedException($"Platform {context.Environment.Platform.Family} is not supported for building the {name}.");
            }
        }
    }

    private void Scaffold(BuildContext context, string name, string workingDirectory, string premakeArguments, string cleanOptions = "")
    {
        int exit;
        exit = context.StartProcess("premake5", new ProcessSettings { WorkingDirectory = workingDirectory, Arguments = $"{cleanOptions} clean" });
        if (exit != 0)
        {
            throw new Exception($"{name} Premake clean failed! {exit}");
        }

        exit = context.StartProcess("premake5", new ProcessSettings { WorkingDirectory = workingDirectory, Arguments = premakeArguments });
        if (exit != 0)
        {
            throw new Exception($"{name} Premake generation failed! {exit}");
        }
    }

    private void BuildForArch(
        BuildContext context,
        string name,
        string workingDirectory,
        string solutionFile,
        string arch,
        string? platformToolset)
    {
        var toolsetArgument = string.IsNullOrWhiteSpace(platformToolset)
            ? string.Empty
            : $" /p:PlatformToolset={platformToolset}";
        int exit = context.StartProcess("msbuild", new ProcessSettings { WorkingDirectory = workingDirectory, Arguments = $"{solutionFile} /p:Configuration=Release /p:Platform={arch}{toolsetArgument}" });
        if (exit != 0)
        {
            throw new Exception($"{name} build failed with msbuild! {exit}");
        }
    }

    private void Make(BuildContext context, string name, string workingDirectory, string target)
    {
        int exit = context.StartProcess("make", new ProcessSettings { WorkingDirectory = workingDirectory, Arguments = $"config=release {target}" });
        if (exit != 0)
        {
            throw new Exception($"{name} build failed with make! {exit}");
        }
    }
}
