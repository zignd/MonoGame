using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate
{
    [ContentTypeSerializer]
    class NullableSerializer<T> : ContentTypeSerializer<T?> where T : struct
    {
        private ContentTypeSerializer? _serializer;
        private ContentSerializerAttribute? _format;

        private ContentTypeSerializer Serializer => _serializer
            ?? throw new InvalidOperationException("Nullable serializer has not been initialized.");

        private ContentSerializerAttribute Format => _format
            ?? throw new InvalidOperationException("Nullable serializer format has not been initialized.");

        protected internal override void Initialize(IntermediateSerializer serializer)
        {
            _serializer = serializer.GetTypeSerializer(typeof(T));
            _format = new ContentSerializerAttribute
            {
                FlattenContent = true
            };
        }

        protected internal override T? Deserialize(IntermediateReader input, ContentSerializerAttribute format, T? existingInstance)
        {
            return input.ReadRawObject<T>(Format, Serializer);
        }

        protected internal override void Serialize(IntermediateWriter output, T? value, ContentSerializerAttribute format)
        {
            if (!value.HasValue)
                throw new ArgumentNullException(nameof(value));

            output.WriteRawObject<T>(value.Value, Format, Serializer);
        }
    }
}
