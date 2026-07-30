using System.Runtime.InteropServices;

namespace BuildScripts;

[TaskName("Build Native Dependencies")]
public sealed class BuildNativeDependenciesTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (context.Environment.Platform.Family == PlatformFamily.Windows)
        {
            // Cross-compile both architectures on the same x64 runner
            BuildDependenciesForArch(context, "x64");
            BuildDependenciesForArch(context, "arm64");
        }
        else
        {
            // Linux/macOS: build for the host architecture only
            var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            BuildDependenciesForArch(context, arch);
        }
    }

    private void BuildDependenciesForArch(BuildContext context, string targetArch)
    {
        BuildSDL3(context, targetArch);
        BuildFAudio(context, targetArch);
    }

    private void BuildSDL3(BuildContext context, string targetArch)
    {
        var sdlSourceDir = "native/monogame/external/sdl3";
        var sdlBuildDir = System.IO.Path.Combine(sdlSourceDir, "build", targetArch);
        if (context.Environment.Platform.Family != PlatformFamily.Windows)
            sdlBuildDir = System.IO.Path.Combine(sdlSourceDir, "build");

        RecreateDirectory(context, sdlBuildDir);

        var configureArgs = new ProcessArgumentBuilder()
            .Append("-S").AppendQuoted(context.MakeAbsolute(new DirectoryPath(sdlSourceDir)).FullPath)
            .Append("-B").AppendQuoted(context.MakeAbsolute(new DirectoryPath(sdlBuildDir)).FullPath)
            .Append("-DSDL_STATIC=ON")
            .Append("-DSDL_SHARED=OFF")
            .Append("-DSDL_TEST_LIBRARY=OFF")
            .Append("-DSDL_TESTS=OFF");

        AppendPlatformCMakeArgs(configureArgs, context, isSDL: true, targetArch);

        RunCMake(context, configureArgs, "SDL3 CMake configuration failed!");

        RunCMakeBuild(context, sdlBuildDir, "Release", "SDL3 build failed!");
    }

    private void BuildFAudio(BuildContext context, string targetArch)
    {
        var faudioSourceDir = "native/monogame/external/faudio";
        var faudioBuildDir = System.IO.Path.Combine(faudioSourceDir, "build-sdl3", targetArch);
        if (context.Environment.Platform.Family != PlatformFamily.Windows)
            faudioBuildDir = System.IO.Path.Combine(faudioSourceDir, "build-sdl3");

        RecreateDirectory(context, faudioBuildDir);

        var sdlIncludeDir = System.IO.Path.Combine("native/monogame/external/sdl3", "include");

        var configureArgs = new ProcessArgumentBuilder()
            .Append("-S").AppendQuoted(context.MakeAbsolute(new DirectoryPath(faudioSourceDir)).FullPath)
            .Append("-B").AppendQuoted(context.MakeAbsolute(new DirectoryPath(faudioBuildDir)).FullPath)
            .Append("-DBUILD_SHARED_LIBS=OFF")
            .Append($"-DCMAKE_C_STANDARD_INCLUDE_DIRECTORIES=\"{context.MakeAbsolute(new DirectoryPath(sdlIncludeDir))}\"")
            .Append("-DBUILD_SDL3=ON");

        AppendPlatformCMakeArgs(configureArgs, context, isSDL: false, targetArch);

        RunCMake(context, configureArgs, "FAudio CMake configuration failed!");

        RunCMakeBuild(context, faudioBuildDir, "Release", "FAudio build failed!");
    }

    private void AppendPlatformCMakeArgs(ProcessArgumentBuilder args, BuildContext context, bool isSDL, string targetArch)
    {
        switch (context.Environment.Platform.Family)
        {
            case PlatformFamily.Windows:
                args.Append("-A").Append(targetArch == "arm64" ? "ARM64" : "x64");
                if (isSDL)
                {
                    args.Append("-DSDL_FORCE_STATIC_VCRT=ON");
                }
                else
                {
                    args.Append("-DCMAKE_C_FLAGS_DEBUG=\"/MTd /Zi /Ob0 /Od /RTC1\"");
                    args.Append("-DCMAKE_C_FLAGS_RELEASE=\"/MT /O2 /Ob2 /DNDEBUG\"");
                }
                break;

            case PlatformFamily.Linux:
                args.Append("-DCMAKE_POSITION_INDEPENDENT_CODE=ON");
                break;

            case PlatformFamily.OSX:
                args.Append("-DCMAKE_OSX_ARCHITECTURES=x86_64;arm64");
                args.Append("-DCMAKE_OSX_DEPLOYMENT_TARGET=10.15");
                if (isSDL)
                    args.Append("-DCMAKE_C_FLAGS=\"-Wno-deprecated-declarations -Wno-gnu-folding-constant\"");
                else
                    args.Append("-DCMAKE_C_FLAGS=\"-Wno-tautological-compare\"");
                break;
        }
    }

    private void RunCMake(BuildContext context, ProcessArgumentBuilder args, string errorMessage)
    {
        var settings = new ProcessSettings { Arguments = args };
        if (context.StartProcess("cmake", settings) != 0)
        {
            throw new Exception(errorMessage);
        }
    }

    private void RunCMakeBuild(BuildContext context, string buildDir, string config, string errorMessage)
    {
        var buildArgs = new ProcessArgumentBuilder()
            .Append("--build")
            .AppendQuoted(context.MakeAbsolute(new DirectoryPath(buildDir)).FullPath)
            .Append("--config").Append(config)
            .Append("--parallel");

        RunCMake(context, buildArgs, errorMessage);
    }

    private void RecreateDirectory(BuildContext context, string dir)
    {
        if (context.DirectoryExists(dir))
        {
            context.DeleteDirectory(dir, new DeleteDirectorySettings { Recursive = true });
        }
        context.CreateDirectory(dir);
    }
}
