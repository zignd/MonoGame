// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework.Content.Pipeline;

namespace MonoGame.Framework.Content.Pipeline.Builder
{
    /// <summary>
    /// Provides importer context services backed by the pipeline builder.
    /// </summary>
    public class PipelineImporterContext : ContentImporterContext
    {
        private readonly PipelineManager _manager;

        private readonly PipelineBuildEvent _pipelineEvent;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineImporterContext"/> class.
        /// </summary>
        /// <param name="manager">The pipeline manager that owns the build.</param>
        /// <param name="pipelineEvent">The build event being populated.</param>
        public PipelineImporterContext(PipelineManager manager, PipelineBuildEvent pipelineEvent)
        {
            _manager = manager;
            _pipelineEvent = pipelineEvent;
        }

        /// <inheritdoc/>
        public override string IntermediateDirectory { get { return _manager.IntermediateDirectory; } }
        /// <inheritdoc/>
        public override string OutputDirectory { get { return _manager.OutputDirectory; } }
        /// <inheritdoc/>
        public override ContentBuildLogger Logger { get { return _manager.Logger; } }

        /// <inheritdoc/>
        public override void AddDependency(string filename)
        {
            _pipelineEvent.Dependencies.AddUnique(filename);
        }
    }
}
