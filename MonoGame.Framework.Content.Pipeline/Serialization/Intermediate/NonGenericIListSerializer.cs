// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate
{
    class NonGenericIListSerializer : ContentTypeSerializer
    {
        public NonGenericIListSerializer(Type targetType) :
            base(targetType, targetType.Name)
        {
        }

        public override bool CanDeserializeIntoExistingObject
        {
            get { return true; }
        }

        public override bool ObjectIsEmpty([AllowNull] object value)
        {
            return value is IList list && list.Count == 0;
        }

        [return: MaybeNull]
        protected internal override object Deserialize(IntermediateReader input, ContentSerializerAttribute format, [AllowNull] object existingInstance)
        {
            var result = existingInstance as IList;
            if (result == null)
            {
                result = Activator.CreateInstance(TargetType) as IList
                    ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not create list instance for '{0}'.", TargetType.FullName));
            }

            // Create the item serializer attribute.
            var itemFormat = new ContentSerializerAttribute();
            itemFormat.ElementName = format.CollectionItemName;

            // Read all the items.
            while (input.MoveToElement(itemFormat.ElementName))
            {
                var value = input.ReadObject<object>(itemFormat);
                result.Add(value);
            }

            return result;
        }

        protected internal override void Serialize(IntermediateWriter output, [AllowNull] object value, ContentSerializerAttribute format)
        {
            if (value is not IList list)
                return;

            // Create the item serializer attribute.
            var itemFormat = new ContentSerializerAttribute();
            itemFormat.ElementName = format.CollectionItemName;

            // Read all the items.
            foreach (var item in list)
                output.WriteObject(item, itemFormat);
        }
    }
}