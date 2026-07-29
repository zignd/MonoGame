// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate
{
    [ContentTypeSerializer]
    class ExternalReferenceSerializer<T> : ContentTypeSerializer<ExternalReference<T>>
    {
        public ExternalReferenceSerializer() :
            base("ExternalReference")
        {
        }

        protected internal override ExternalReference<T> Deserialize(IntermediateReader input, ContentSerializerAttribute format, [AllowNull] ExternalReference<T> existingInstance)
        {
            var result = existingInstance ?? new ExternalReference<T>();
            input.ReadExternalReference(result);
            return result;
        }

        protected internal override void Serialize(IntermediateWriter output, [AllowNull] ExternalReference<T> value, ContentSerializerAttribute format)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            output.WriteExternalReference(value);
        }
    }
}