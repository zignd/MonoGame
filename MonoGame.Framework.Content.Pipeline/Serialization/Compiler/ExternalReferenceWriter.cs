// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler
{
    /// <summary>
    /// Writes the external reference to the output.
    /// </summary>
    [ContentTypeWriter]
    class ExternalReferenceWriter<T> : BuiltInContentWriter<ExternalReference<T>>
    {
        private ContentTypeWriter? _targetWriter;

        private ContentTypeWriter TargetWriter => _targetWriter
            ?? throw new InvalidOperationException("External reference writer has not been initialized with a target writer.");

        /// <inheritdoc/>
        internal override void OnAddedToContentWriter(ContentWriter output)
        {
            base.OnAddedToContentWriter(output);
            _targetWriter = output.GetTypeWriter(typeof(T));
        }

        /// <summary>
        /// Writes the value to the output.
        /// </summary>
        /// <param name="output">The output writer object.</param>
        /// <param name="value">The value to write to the output.</param>
        protected internal override void Write(ContentWriter output, ExternalReference<T>? value)
        {
            output.WriteExternalReference(value);
        }

        /// <inheritdoc/>
        public override string GetRuntimeReader(TargetPlatform targetPlatform)
        {
            var type = typeof(ContentReader);
            var readerType = type.Namespace + ".ExternalReferenceReader, " + type.Assembly.FullName;
            return readerType;
        }

        /// <inheritdoc/>
        public override string GetRuntimeType(TargetPlatform targetPlatform)
        {
            return TargetWriter.GetRuntimeType(targetPlatform);
        }
    }
}
