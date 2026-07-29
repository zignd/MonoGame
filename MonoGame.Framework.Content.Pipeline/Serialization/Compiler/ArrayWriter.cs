// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler
{
    /// <summary>
    /// Writes the array value to the output.
    /// </summary>
    [ContentTypeWriter]
    class ArrayWriter<T> : BuiltInContentWriter<T[]>
    {
        ContentTypeWriter? _elementWriter;

        ContentTypeWriter ElementWriter => _elementWriter
            ?? throw new InvalidOperationException("Array writer has not been initialized with an element writer.");

        /// <inheritdoc/>
        internal override void OnAddedToContentWriter(ContentWriter output)
        {
            base.OnAddedToContentWriter(output);

            _elementWriter = output.GetTypeWriter(typeof(T));
        }

        public override string GetRuntimeReader(TargetPlatform targetPlatform)
        {
            return string.Concat(   typeof(ContentTypeReader).Namespace,
                                    ".",
                                    "ArrayReader`1[[",
                                        ElementWriter.GetRuntimeType(targetPlatform),
                                    "]]");
        }

        protected internal override void Write(ContentWriter output, [AllowNull] T[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            output.Write(value.Length);
            foreach (var element in value)
                output.WriteObject(element, ElementWriter);
        }
    }
}
