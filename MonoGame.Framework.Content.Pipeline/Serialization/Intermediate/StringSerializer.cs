// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate
{
    [ContentTypeSerializer]
    class StringSerializer : ContentTypeSerializer<string>
    {
        public StringSerializer() :
            base("string")
        {
        }

        protected internal override string Deserialize(IntermediateReader input, ContentSerializerAttribute format, [AllowNull] string existingInstance)
        {
            return input.Xml.ReadString();
        }

        protected internal override void Serialize(IntermediateWriter output, [AllowNull] string value, ContentSerializerAttribute format)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            output.Xml.WriteString(value);
        }
    }
}