// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler
{
    [ContentTypeWriter]
    class VertexBufferWriter : BuiltInContentWriter<VertexBufferContent>
    {
        protected internal override void Write(ContentWriter output, [AllowNull] VertexBufferContent value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            output.WriteRawObject(value.VertexDeclaration);
            if (!value.VertexDeclaration.VertexStride.HasValue)
                throw new InvalidOperationException("Vertex declaration must define a vertex stride before serialization.");

            output.Write((uint)(value.VertexData.Length / value.VertexDeclaration.VertexStride.Value));
            output.Write(value.VertexData);
        }
    }
}
