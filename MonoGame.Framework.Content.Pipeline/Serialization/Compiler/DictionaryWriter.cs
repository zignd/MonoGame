// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler
{
    /// <summary>
    /// Writes the dictionary to the output.
    /// </summary>
    [ContentTypeWriter]
    class DictionaryWriter<K,V> : BuiltInContentWriter<Dictionary<K,V>> where K : notnull
    {
        ContentTypeWriter? _keyWriter;
        ContentTypeWriter? _valueWriter;

        /// <inheritdoc/>
        internal override void OnAddedToContentWriter(ContentWriter output)
        {
            base.OnAddedToContentWriter(output);

            _keyWriter = output.GetTypeWriter(typeof(K));
            _valueWriter = output.GetTypeWriter(typeof(V));
        }

        public override bool CanDeserializeIntoExistingObject
        {
            get { return true; }
        }

        protected internal override void Write(ContentWriter output, [AllowNull] Dictionary<K,V> value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            var keyWriter = _keyWriter ?? throw new InvalidOperationException("Dictionary key writer has not been initialized.");
            var valueWriter = _valueWriter ?? throw new InvalidOperationException("Dictionary value writer has not been initialized.");

            output.Write(value.Count);
            foreach (var element in value)
            {
                output.WriteObject(element.Key, keyWriter);
                output.WriteObject(element.Value, valueWriter);
            }
        }
    }
}
