using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGame.ShaderCache;

internal sealed class MetalCookContext : ContentProcessorContext, IDisposable
{
    private readonly ContentBuildLogger _logger = new();

    public MetalCookContext(string outputFilename)
    {
        OutputFilename = Path.GetFullPath(outputFilename);
        OutputDirectory = Path.GetDirectoryName(OutputFilename) ?? Directory.GetCurrentDirectory();
        IntermediateDirectory = Path.Combine(OutputDirectory, "obj", "mgmetal");
    }

    public override string BuildConfiguration => "Production";
    public override string IntermediateDirectory { get; }
    public override ContentBuildLogger Logger => _logger;
    public override string OutputDirectory { get; }
    public override string OutputFilename { get; }
    public override OpaqueDataDictionary Parameters { get; } = new();
    public override string ProjectDirectory => Directory.GetCurrentDirectory();
    public override ContentIdentity SourceIdentity { get; } = new();
    public override TargetPlatform TargetPlatform => TargetPlatform.DesktopVK;
    public override GraphicsProfile TargetProfile => GraphicsProfile.HiDef;

    public override void AddDependency(string filename)
    {
    }

    public override void AddOutputFile(string filename)
    {
    }

    [Obsolete("Please pass importer and processor instances.")]
    public override TOutput BuildAndLoadAsset<TInput, TOutput>(
        ExternalReference<TInput> sourceAsset,
        string processorName,
        OpaqueDataDictionary? processorParameters,
        string? importerName)
        => throw new NotSupportedException();

    public override TOutput BuildAndLoadAsset<TInput, TOutput>(
        ExternalReference<TInput> sourceAsset,
        IContentImporter importer,
        IContentProcessor processor)
        => throw new NotSupportedException();

    [Obsolete("Please pass importer and processor instances.")]
    public override ExternalReference<TOutput> BuildAsset<TInput, TOutput>(
        ExternalReference<TInput> sourceAsset,
        string processorName,
        OpaqueDataDictionary? processorParameters,
        string? importerName,
        string? assetName)
        => throw new NotSupportedException();

    public override ExternalReference<TOutput> BuildAsset<TInput, TOutput>(
        ExternalReference<TInput> sourceAsset,
        IContentImporter importer,
        IContentProcessor processor,
        string? assetName = null)
        => throw new NotSupportedException();

    [Obsolete("Please pass importer and processor instances.")]
    public override TOutput Convert<TInput, TOutput>(
        TInput input,
        string processorName,
        OpaqueDataDictionary? processorParameters)
        => throw new NotSupportedException();

    public override TOutput Convert<TInput, TOutput>(TInput input, IContentProcessor processor)
        => throw new NotSupportedException();

    public void Dispose()
    {
    }
}