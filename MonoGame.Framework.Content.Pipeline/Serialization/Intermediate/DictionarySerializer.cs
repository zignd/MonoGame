// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate
{
    [ContentTypeSerializer]
    class DictionarySerializer<TKey,TValue> : ContentTypeSerializer<Dictionary<TKey,TValue>> where TKey : notnull
    {
        private ContentTypeSerializer? _keySerializer;
        private ContentTypeSerializer? _valueSerializer;

        private ContentSerializerAttribute? _keyFormat;
        private ContentSerializerAttribute? _valueFormat;

        public DictionarySerializer() :
            base("dictionary")
        {
        }

        public override bool CanDeserializeIntoExistingObject
        {
            get { return true; }
        }

        protected internal override void Initialize(IntermediateSerializer serializer)
        {
            _keySerializer = serializer.GetTypeSerializer(typeof(TKey));
            _valueSerializer = serializer.GetTypeSerializer(typeof(TValue));

            _keyFormat = new ContentSerializerAttribute
            {
                ElementName = "Key",
                AllowNull = false
            };

            _valueFormat = new ContentSerializerAttribute()
            {
                ElementName = "Value",
                AllowNull = typeof(TValue).IsValueType
            };
        }

        public override bool ObjectIsEmpty([AllowNull] Dictionary<TKey, TValue> value)
        {
            return value == null || value.Count == 0;
        }

        protected internal override void ScanChildren(IntermediateSerializer serializer, ChildCallback callback, [AllowNull] Dictionary<TKey, TValue> value)
        {
            if (value == null || _keySerializer == null || _valueSerializer == null)
                return;

            foreach (var kvp in value)
            {
                callback(_keySerializer, kvp.Key);
                callback(_valueSerializer, kvp.Value);
            }
        }

        [return: MaybeNull]
        protected internal override Dictionary<TKey, TValue> Deserialize(IntermediateReader input, ContentSerializerAttribute format, [AllowNull] Dictionary<TKey, TValue> existingInstance)
        {
            var keySerializer = _keySerializer ?? throw new InvalidOperationException("Dictionary key serializer has not been initialized.");
            var valueSerializer = _valueSerializer ?? throw new InvalidOperationException("Dictionary value serializer has not been initialized.");
            var keyFormat = _keyFormat ?? throw new InvalidOperationException("Dictionary key format has not been initialized.");
            var valueFormat = _valueFormat ?? throw new InvalidOperationException("Dictionary value format has not been initialized.");
            var result = existingInstance ?? new Dictionary<TKey, TValue>();

            while (input.MoveToElement(format.CollectionItemName))
            {
                input.Xml.ReadStartElement();

                var key = input.ReadObject<TKey>(keyFormat, keySerializer);
                if (key == null)
                    throw input.NewInvalidContentException(null, "Dictionary key cannot be null.");

                var value = input.ReadObject<TValue>(valueFormat, valueSerializer);
                ((IDictionary)result).Add(key, value);

                input.Xml.ReadEndElement();
            }

            return result;
        }

        protected internal override void Serialize(IntermediateWriter output, [AllowNull] Dictionary<TKey, TValue> value, ContentSerializerAttribute format)
        {
            if (value == null)
                return;

            var keyFormat = _keyFormat ?? throw new InvalidOperationException("Dictionary key format has not been initialized.");
            var valueFormat = _valueFormat ?? throw new InvalidOperationException("Dictionary value format has not been initialized.");
            var keySerializer = _keySerializer ?? throw new InvalidOperationException("Dictionary key serializer has not been initialized.");
            var valueSerializer = _valueSerializer ?? throw new InvalidOperationException("Dictionary value serializer has not been initialized.");

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