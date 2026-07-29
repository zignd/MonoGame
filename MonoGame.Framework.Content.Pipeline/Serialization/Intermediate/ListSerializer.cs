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
    class ListSerializer<T> : ContentTypeSerializer<List<T>>
    {
        private ContentTypeSerializer? _itemSerializer;

        public ListSerializer() :
            base("list")
        {
        }

        public override bool CanDeserializeIntoExistingObject
        {
            get { return true; }
        }

        protected internal override void Initialize(IntermediateSerializer serializer)
        {
            _itemSerializer = serializer.GetTypeSerializer(typeof(T));
        }

        public override bool ObjectIsEmpty([AllowNull] List<T> value)
        {
            return value == null || value.Count == 0;
        }

        protected internal override void ScanChildren(IntermediateSerializer serializer, ChildCallback callback, [AllowNull] List<T> value)
        {
            if (value == null || _itemSerializer == null)
                return;

            foreach (var item in value)
                callback(_itemSerializer, item);
        }

        [return: MaybeNull]
        protected internal override List<T> Deserialize(IntermediateReader input, ContentSerializerAttribute format, [AllowNull] List<T> existingInstance)
        {
            var itemSerializer = _itemSerializer ?? throw new InvalidOperationException("List serializer has not been initialized.");
            var result = existingInstance ?? new List<T>();

            var elementSerializer = itemSerializer as ElementSerializer<T>;
            if (elementSerializer != null)
                elementSerializer.Deserialize(input, result);
            else
            {
                // Create the item serializer attribute.
                var itemFormat = new ContentSerializerAttribute();
                itemFormat.ElementName = format.CollectionItemName;

                // Read all the items.
                while (input.MoveToElement(itemFormat.ElementName))
                {
                    var value = input.ReadObject<T>(itemFormat, itemSerializer);
                    ((IList)result).Add(value);
                }
            }

            return result;
        }

        protected internal override void Serialize(IntermediateWriter output, [AllowNull] List<T> value, ContentSerializerAttribute format)
        {
            if (value == null)
                return;

            var itemSerializer = _itemSerializer ?? throw new InvalidOperationException("List serializer has not been initialized.");
            var elementSerializer = itemSerializer as ElementSerializer<T>;
            if (elementSerializer != null)
                elementSerializer.Serialize(output, value);
            else
            {
                // Create the item serializer attribute.
                var itemFormat = new ContentSerializerAttribute();
                itemFormat.ElementName = format.CollectionItemName;

                // Read all the items.
                foreach (var item in value)
                    output.WriteObject(item, itemFormat, itemSerializer);
            }
        }
    }
}