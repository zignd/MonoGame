// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate
{
    [ContentTypeSerializer]
    class NamedValueDictionarySerializer<T> : ContentTypeSerializer<NamedValueDictionary<T>>
    {
        private ContentTypeSerializer? _keySerializer;

        private ContentSerializerAttribute? _keyFormat;
        private ContentSerializerAttribute? _valueFormat;

        public NamedValueDictionarySerializer() :
            base("namedValueDictionary")
        {
        }

        public override bool CanDeserializeIntoExistingObject
        {
            get { return true; }
        }

        protected internal override void Initialize(IntermediateSerializer serializer)
        {
            _keySerializer = serializer.GetTypeSerializer(typeof(string));

            _keyFormat = new ContentSerializerAttribute
            {
                ElementName = "Key",
                AllowNull = false
            };

            _valueFormat = new ContentSerializerAttribute
            {
                ElementName = "Value",
                AllowNull = typeof(T).IsValueType
            };
        }

        public override bool ObjectIsEmpty([AllowNull] NamedValueDictionary<T> value)
        {
            return value == null || value.Count == 0;
        }

        protected internal override void ScanChildren(IntermediateSerializer serializer, ChildCallback callback, [AllowNull] NamedValueDictionary<T> value)
        {
            if (value == null)
                return;

            foreach (var kvp in value)
                callback(serializer.GetTypeSerializer(typeof(T)), kvp.Value);
        }

        [return: MaybeNull]
        protected internal override NamedValueDictionary<T> Deserialize(IntermediateReader input, ContentSerializerAttribute format, [AllowNull] NamedValueDictionary<T> existingInstance)
        {
            var keyFormat = _keyFormat ?? throw new InvalidOperationException("Named value dictionary key format has not been initialized.");
            var valueFormat = _valueFormat ?? throw new InvalidOperationException("Named value dictionary value format has not been initialized.");
            var keySerializer = _keySerializer ?? throw new InvalidOperationException("Named value dictionary key serializer has not been initialized.");
            var result = existingInstance ?? new NamedValueDictionary<T>();

            var valueSerializer = input.Serializer.GetTypeSerializer(result.DefaultSerializerType);

            while (input.MoveToElement(format.CollectionItemName))
            {
                input.Xml.ReadStartElement();

                var key = input.ReadObject<string>(keyFormat, keySerializer);
                if (key == null)
                    throw input.NewInvalidContentException(null, "Named value dictionary key cannot be null.");

                var value = input.ReadObject<T>(valueFormat, valueSerializer);
                result.Add(key, value);

                input.Xml.ReadEndElement();
            }

            return result;
        }

        protected internal override void Serialize(IntermediateWriter output, [AllowNull] NamedValueDictionary<T> value, ContentSerializerAttribute format)
        {
            if (value == null)
                return;

            var keyFormat = _keyFormat ?? throw new InvalidOperationException("Named value dictionary key format has not been initialized.");
            var valueFormat = _valueFormat ?? throw new InvalidOperationException("Named value dictionary value format has not been initialized.");
            var keySerializer = _keySerializer ?? throw new InvalidOperationException("Named value dictionary key serializer has not been initialized.");
            var valueSerializer = output.Serializer.GetTypeSerializer(value.DefaultSerializerType);

            foreach (var kvp in value)
            {
                output.Xml.WriteStartElement(format.CollectionItemName);

                output.WriteObject(kvp.Key, keyFormat, keySerializer);
                output.WriteObject(kvp.Value, valueFormat, valueSerializer);

                output.Xml.WriteEndElement();
            }
        }
    }
}