// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGame.Framework.Content.Pipeline.Builder
{
    /// <inheritdoc/>
    public class PipelineProcessorContext : ContentProcessorContext
    {
        private readonly PipelineManager _manager;

        private readonly PipelineBuildEvent _pipelineEvent;

        /// <summary>
        /// Creates a new pipeline processor context.
        /// </summary>
        /// <param name="manager">Pipeline manager.</param>
        /// <param name="pipelineEvent">Pipeline event.</param>
        public PipelineProcessorContext(PipelineManager manager, PipelineBuildEvent pipelineEvent)
        {
            _manager = manager;
            _pipelineEvent = pipelineEvent;
        }

        /// <inheritdoc/>
        public override TargetPlatform TargetPlatform { get { return _manager.Platform; } }

        /// <inheritdoc/>
        public override GraphicsProfile TargetProfile { get { return _manager.Profile; } }

        /// <inheritdoc/>
        public override string BuildConfiguration { get { return _manager.Config; } }

        /// <inheritdoc/>
        public override string IntermediateDirectory { get { return _manager.IntermediateDirectory; } }

        /// <inheritdoc/>
        public override string OutputDirectory { get { return _manager.OutputDirectory; } }

        /// <inheritdoc/>
        public override string OutputFilename { get { return _pipelineEvent.DestFile; } }

        /// <inheritdoc/>
        public override OpaqueDataDictionary Parameters { get { return _pipelineEvent.Parameters; } }

        /// <inheritdoc/>
        public override string ProjectDirectory { get { return _manager.ProjectDirectory; } }

        /// <inheritdoc/>
        public override ContentBuildLogger Logger { get { return _manager.Logger; } }

        /// <inheritdoc/>
        public override ContentIdentity SourceIdentity { get { return new ContentIdentity(_pipelineEvent.SourceFile); } }

        /// <inheritdoc/>
        public override void AddDependency(string filename)
        {
            _pipelineEvent.Dependencies.AddUnique(filename);
        }

        /// <inheritdoc/>
        public override void AddOutputFile(string filename)
        {
            _pipelineEvent.BuildOutput.AddUnique(filename);
        }

        /// <inheritdoc/>
        [Obsolete("Please pass importer and processor as instances instead of just their names.")]
        public override TOutput Convert<TInput, TOutput>(TInput input,
                                                            string processorName,
                                                            OpaqueDataDictionary? processorParameters)
            => ConvertByName<TInput, TOutput>(input, processorName, processorParameters);

        private TOutput ConvertByName<TInput, TOutput>(TInput input,
                                                        string processorName,
                                                        OpaqueDataDictionary? processorParameters)
        {
            var effectiveParameters = processorParameters ?? new OpaqueDataDictionary();
            var processor = _manager.CreateProcessor(processorName, effectiveParameters)
                ?? throw new InvalidOperationException($"Could not create processor '{processorName}'.");
            var processContext = new PipelineProcessorContext(_manager, new PipelineBuildEvent { Parameters = effectiveParameters });
            using var _ = ContextScopeFactory.BeginContext(processContext);
            if (input is null)
                throw new InvalidOperationException($"Processor '{processorName}' received null input.");

            var processedObject = processor.Process(input, processContext);

            // Add its dependencies and built assets to ours.
            _pipelineEvent.Dependencies.AddRangeUnique(processContext._pipelineEvent.Dependencies);
            _pipelineEvent.BuildAsset.AddRangeUnique(processContext._pipelineEvent.BuildAsset);

            if (processedObject is TOutput output)
                return output;

            throw new InvalidOperationException($"Processor '{processorName}' returned an unexpected result type '{processedObject?.GetType().FullName ?? "<null>"}'.");
        }

        /// <inheritdoc/>
        public override TOutput Convert<TInput, TOutput>(TInput input, IContentProcessor processor)
        {
            var processorName = processor.GetType().Name.ToString();
            var processorParameters = new OpaqueDataDictionary();

            foreach (var prop in processor.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    processorParameters.Add(prop.Name, prop.GetValue(processor)!);
                }
            }

            return ConvertByName<TInput, TOutput>(input, processorName, processorParameters);
        }

        /// <inheritdoc/>
        [Obsolete("Please pass importer and processor as instances instead of just their names.")]
        public override TOutput BuildAndLoadAsset<TInput, TOutput>(ExternalReference<TInput> sourceAsset,
                                                                    string processorName,
                                                                    OpaqueDataDictionary? processorParameters,
                                                                    string? importerName)
            => BuildAndLoadAssetByName<TInput, TOutput>(sourceAsset, processorName, processorParameters, importerName);

        private TOutput BuildAndLoadAssetByName<TInput, TOutput>(ExternalReference<TInput> sourceAsset,
                                                                  string processorName,
                                                                  OpaqueDataDictionary? processorParameters,
                                                                  string? importerName)
        {
            var effectiveParameters = processorParameters ?? new OpaqueDataDictionary();
            var sourceFilepath = PathHelper.Normalize(sourceAsset.Filename);

            // The processorName can be null or empty. In this case the asset should
            // be imported but not processed. This is, for example, necessary to merge
            // animation files as described here:
            // http://blogs.msdn.com/b/shawnhar/archive/2010/06/18/merging-animation-files.aspx.
            bool processAsset = !string.IsNullOrEmpty(processorName);
            _manager.ResolveImporterAndProcessor(sourceFilepath, ref importerName, ref processorName);

            var buildEvent = new PipelineBuildEvent
            {
                SourceFile = sourceFilepath,
                Importer = importerName ?? string.Empty,
                Processor = processAsset ? processorName : string.Empty,
                Parameters = _manager.ValidateProcessorParameters(processorName, effectiveParameters),
            };

            var processedObject = _manager.ProcessContent(buildEvent);

            // Record that we processed this dependent asset.
            _pipelineEvent.Dependencies.AddUnique(sourceFilepath);

            if (processedObject is TOutput output)
                return output;

            throw new InvalidOperationException($"Processing '{sourceFilepath}' returned an unexpected result type '{processedObject?.GetType().FullName ?? "<null>"}'.");
        }

        /// <inheritdoc/>
        public override TOutput BuildAndLoadAsset<TInput, TOutput>(ExternalReference<TInput> sourceAsset, IContentImporter importer, IContentProcessor processor)
        {
            var importerName = importer.GetType().Name;
            var processorName = processor.GetType().Name;
            var processorParameters = new OpaqueDataDictionary();

            foreach (var prop in processor.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    processorParameters.Add(prop.Name, prop.GetValue(processor)!);
                }
            }

            return BuildAndLoadAssetByName<TInput, TOutput>(sourceAsset, processorName, processorParameters, importerName);
        }

        /// <inheritdoc/>
        [Obsolete("Please pass importer and processor as instances instead of just their names.")]
        public override ExternalReference<TOutput> BuildAsset<TInput, TOutput>(ExternalReference<TInput> sourceAsset,
                                                                                string processorName,
                                                                                OpaqueDataDictionary? processorParameters,
                                                                                string? importerName,
                                                                                string? assetName)
            => BuildAssetByName<TInput, TOutput>(sourceAsset, processorName, processorParameters, importerName, assetName);

        private ExternalReference<TOutput> BuildAssetByName<TInput, TOutput>(ExternalReference<TInput> sourceAsset,
                                                                              string processorName,
                                                                              OpaqueDataDictionary? processorParameters,
                                                                              string? importerName,
                                                                              string? assetName)
        {
            var effectiveParameters = processorParameters ?? new OpaqueDataDictionary();
            var effectiveImporterName = importerName ?? string.Empty;
            // Be sure we have a good absolute path to the source content
            // or it may not cache correctly and create duplicates.
            sourceAsset.Filename = _manager.ResolveSourceFilePath(sourceAsset.Filename);

            if (string.IsNullOrEmpty(assetName))
                assetName = _manager.GetAssetName(sourceAsset.Filename, effectiveImporterName, processorName, effectiveParameters);

            // Build the content.
            var buildEvent = _manager.BuildContent(sourceAsset.Filename, assetName, effectiveImporterName, processorName, effectiveParameters);

            // Record that we built this dependent asset.
            _pipelineEvent.BuildAsset.AddUnique(buildEvent.DestFile);

            return new ExternalReference<TOutput>(buildEvent.DestFile);
        }

        /// <inheritdoc/>
        public override ExternalReference<TOutput> BuildAsset<TInput, TOutput>(ExternalReference<TInput> sourceAsset, IContentImporter importer, IContentProcessor processor, string? assetName)
        {
            var importerName = importer.GetType().Name;
            var processorName = processor.GetType().Name;
            var processorParameters = new OpaqueDataDictionary();

            foreach (var prop in processor.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    processorParameters.Add(prop.Name, prop.GetValue(processor)!);
                }
            }

            return BuildAssetByName<TInput, TOutput>(sourceAsset, processorName, processorParameters, importerName, assetName);
        }
    }
}
