using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Design;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using NUnit.Framework;

namespace MonoGame.Tools.Tests
{
    [TestFixture]
    internal class VectorTypeConverterTests
    {
        private sealed class CustomPackedVector : IPackedVector
        {
            public Vector4 Value { get; private set; }

            public void PackFromVector4(Vector4 vector)
            {
                Value = vector;
            }

            public Vector4 ToVector4()
            {
                return Value;
            }
        }

        [Test]
        public void Vector4TypeConverter_CanConvertToCustomPackedVector()
        {
            var converter = new Vector4TypeConverter();

            Assert.True(converter.CanConvertTo(null, typeof(CustomPackedVector)));
        }

        [Test]
        public void Vector4TypeConverter_ConvertsToCustomPackedVector()
        {
            var converter = new Vector4TypeConverter();
            var source = new Vector4(1f, 2f, 3f, 4f);

            var result = converter.ConvertTo(null, CultureInfo.InvariantCulture, source, typeof(CustomPackedVector));

            Assert.IsInstanceOf<CustomPackedVector>(result);
            Assert.AreEqual(source, ((CustomPackedVector)result).ToVector4());
        }
    }
}