namespace BuildScripts;

[TaskName("Build Native Metal")]
[IsDependentOn(typeof(BuildShadersVulkanTask))]
[IsDependentOn(typeof(BuildNativeDependenciesTask))]
public sealed class BuildNativeMetalTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (context.Environment.Platform.Family != PlatformFamily.OSX)
            throw new PlatformNotSupportedException("Build Native Metal requires macOS.");

        var buildPremake = new BuildPremake();
        buildPremake.Run(
            context,
            "mgruntime Metal",
            "native/monogame",
            "monogame.sln",
            "--metal-only",
            "desktopmetal mgmetalcompiler");
    }
}