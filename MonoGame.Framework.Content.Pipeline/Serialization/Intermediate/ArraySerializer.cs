// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate
{
    class ArraySerializer<T> : ContentTypeSerializer<T[]>
    {
        private readonly ListSerializer<T> _listSerializer;

        public ArraySerializer() :
            base("array")
        {
            _listSerializer = new ListSerializer<T>();
        }

        protected internal override void Initialize(IntermediateSerializer serializer)
        {
            _listSerializer.Initialize(serializer);
        }

        public override bool ObjectIsEmpty([AllowNull] T[] value)
        {
            return value == null || value.Length == 0;
        }

        protected internal override void ScanChildren(IntermediateSerializer serializer, ChildCallback callback, [AllowNull] T[] value)
        {
            if (value == null)
                return;

            _listSerializer.ScanChildren(serializer, callback, new List<T>(value));
        }

        [return: MaybeNull]
        protected internal override T[] Deserialize(IntermediateReader input, ContentSerializerAttribute format, [AllowNull] T[] existingInstance)
        {
            if (existingInstance != null)
                throw new InvalidOperationException("You cannot deserialize an array into a getter-only property.");
            var result = _listSerializer.Deserialize(input, format, null);
            if (result == null)
                return null;

            return result.ToArray();
        }

        protected internal override void Serialize(IntermediateWriter output, [AllowNull] T[] value, ContentSerializerAttribute format)
        {
            if (value == null)
                return;

            _listSerializer.Serialize(output, new List<T>(value), format);
        }
    }
}