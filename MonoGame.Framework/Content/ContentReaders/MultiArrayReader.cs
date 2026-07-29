// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics.CodeAnalysis;
using MonoGame.Framework.Utilities;

namespace Microsoft.Xna.Framework.Content
{
    /// <summary>
    /// This type is not meant to be used directly by MonoGame users.
    /// Its purpose is to allow to work-around AOT issues when loading assets with the ContentManager fail due to the absence of runtime-reflection support in that context (i.e. missing types due to trimming and inability to statically discover them at compile-time).
    /// If ContentManager.Load() throws an NotSupportedExeception, the message should provide insights on how to fix it.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public class MultiArrayReader<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : ContentTypeReader<Array>
    {
        ContentTypeReader elementReader;

        /// <summary/>
        public MultiArrayReader() { }

        /// <summary/>
        protected internal override void Initialize(ContentTypeReaderManager manager)
        {
            Type readerType = typeof(T);
            elementReader = manager.GetTypeReader(readerType);
        }

        /// <summary/>
        protected internal override Array Read(ContentReader input, Array existingInstance)
        {
            var rank = input.ReadInt32();
            if (rank < 1)
                throw new RankException();

            var dimensions = new int[rank];
            var count = 1;
            for (int d = 0; d < dimensions.Length; d++)
                count *= dimensions[d] = input.ReadInt32();
            // The programmer utilizing this function must ensure that the type T is not trimmed.
            var array = existingInstance;
            if (array == null)
                array = CreateArray(dimensions);
            else if (dimensions.Length != array.Rank)
                throw new RankException("existingInstance");
            var indices = new int[rank];

            for (int i = 0; i < count; i++)
            {
                T value;
                if (ReflectionHelpers.IsValueType(typeof(T)))
                    value = input.ReadObject<T>(elementReader);
                else
                {
                    var readerType = input.Read7BitEncodedInt();
                    if (readerType > 0)
                        value = input.ReadObject<T>(input.TypeReaders[readerType - 1]);
                    else
                        value = default(T);
                }

                CalcIndices(array, i, indices);
                array.SetValue(value, indices);
            }

            return array;
        }

        static void CalcIndices(Array array, int index, int[] indices)
        {
            if (array.Rank != indices.Length)
                throw new Exception("indices");

            for (int d = 0; d < indices.Length; d++)
            {
                if (index == 0)
                    indices[d] = 0;
                else
                {
                    indices[d] = index % array.GetLength(d);
                    index /= array.GetLength(d);
                }
            }

            if (index != 0)
                throw new ArgumentOutOfRangeException("index");
        }

        static Array CreateArray(int[] dimensions)
        {
            switch (dimensions.Length)
            {
                case 1: return new T[dimensions[0]];
                case 2: return new T[dimensions[0], dimensions[1]];
                case 3: return new T[dimensions[0], dimensions[1], dimensions[2]];
                case 4: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3]];
                case 5: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4]];
                case 6: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5]];
                case 7: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6]];
                case 8: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7]];
                case 9: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8]];
                case 10: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9]];
                case 11: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10]];
                case 12: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11]];
                case 13: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12]];
                case 14: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13]];
                case 15: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14]];
                case 16: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15]];
                case 17: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16]];
                case 18: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17]];
                case 19: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18]];
                case 20: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19]];
                case 21: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20]];
                case 22: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21]];
                case 23: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21], dimensions[22]];
                case 24: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21], dimensions[22], dimensions[23]];
                case 25: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21], dimensions[22], dimensions[23], dimensions[24]];
                case 26: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21], dimensions[22], dimensions[23], dimensions[24], dimensions[25]];
                case 27: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21], dimensions[22], dimensions[23], dimensions[24], dimensions[25], dimensions[26]];
                case 28: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21], dimensions[22], dimensions[23], dimensions[24], dimensions[25], dimensions[26], dimensions[27]];
                case 29: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21], dimensions[22], dimensions[23], dimensions[24], dimensions[25], dimensions[26], dimensions[27], dimensions[28]];
                case 30: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21], dimensions[22], dimensions[23], dimensions[24], dimensions[25], dimensions[26], dimensions[27], dimensions[28], dimensions[29]];
                case 31: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21], dimensions[22], dimensions[23], dimensions[24], dimensions[25], dimensions[26], dimensions[27], dimensions[28], dimensions[29], dimensions[30]];
                case 32: return new T[dimensions[0], dimensions[1], dimensions[2], dimensions[3], dimensions[4], dimensions[5], dimensions[6], dimensions[7], dimensions[8], dimensions[9], dimensions[10], dimensions[11], dimensions[12], dimensions[13], dimensions[14], dimensions[15], dimensions[16], dimensions[17], dimensions[18], dimensions[19], dimensions[20], dimensions[21], dimensions[22], dimensions[23], dimensions[24], dimensions[25], dimensions[26], dimensions[27], dimensions[28], dimensions[29], dimensions[30], dimensions[31]];
                default:
                    throw new RankException();
            }
        }
    }
}
