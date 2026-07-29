using System;
using System.Runtime.InteropServices;

namespace MonoGame.Effect
{
	internal class MarshalHelper
	{
        public static T Unmarshal<T>(IntPtr ptr)
        {
            var type = typeof(T);
            var result = Marshal.PtrToStructure(ptr, type);
            if (result is not T typedResult)
                throw new InvalidOperationException($"Failed to unmarshal '{type.FullName}' from native memory.");

            return typedResult;
        }

		public static T[] UnmarshalArray<T>(IntPtr ptr, int count)
        {
			var type = typeof(T);
            var size = Marshal.SizeOf(type);
            var ret = new T[count];

            for (int i = 0; i < count; i++)
            {
                var offset = i * size;
				var structPtr = new IntPtr(ptr.ToInt64() + offset);
                var value = Marshal.PtrToStructure(structPtr, type);
                if (value is not T typedValue)
                    throw new InvalidOperationException($"Failed to unmarshal '{type.FullName}' element {i} from native memory.");

                ret[i] = typedValue;
            }

			return ret;
		}

        public static byte[] UnmarshalArray(IntPtr ptr, int count)
        {
            var result = new byte[count];
            Marshal.Copy(ptr, result, 0, count);
            return result;
        }
	}
}

