using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace Microsoft.Xna.Framework.Design
{
    internal static class VectorConversion
    {
        public static bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            if (destinationType == typeof(float))
                return true;
            if (destinationType == typeof(Vector2))
                return true;
            if (destinationType == typeof(Vector3))
                return true;
            if (destinationType == typeof(Vector4))
                return true;
            if (typeof(IPackedVector).IsAssignableFrom(destinationType))
                return true;

            return false;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "TypeConverter.ConvertTo cannot annotate its destinationType override parameter; this design-time fallback intentionally activates arbitrary user-provided IPackedVector implementations.")]
        public static object ConvertToFromVector4(ITypeDescriptorContext context, CultureInfo culture, Vector4 value, Type destinationType)
        {
            if (destinationType == typeof(float))
                return value.X;
            if (destinationType == typeof(Vector2))
                return new Vector2(value.X, value.Y);
            if (destinationType == typeof(Vector3))
                return new Vector3(value.X, value.Y, value.Z);
            if (destinationType == typeof(Vector4))
                return new Vector4(value.X, value.Y, value.Z, value.W);
            if (typeof(IPackedVector).IsAssignableFrom(destinationType))
            {
                var packedVec = TypeDescriptor.CreateInstance(context, destinationType, null, null) as IPackedVector;
                if (packedVec == null)
                    return null;

                packedVec.PackFromVector4(value);
                return packedVec;
            }

            return null;
        }
    }
}
