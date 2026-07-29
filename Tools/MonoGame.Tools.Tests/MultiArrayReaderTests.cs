using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Content;
using NUnit.Framework;

namespace MonoGame.Tools.Tests
{
    [TestFixture]
    internal class MultiArrayReaderTests
    {
        [Test]
        public void CreateArray_SupportsThirtyTwoDimensions()
        {
            var method = typeof(MultiArrayReader<int>).GetMethod("CreateArray", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var lengths = Enumerable.Repeat(1, 32).ToArray();
            var array = (Array)method.Invoke(null, new object[] { lengths });

            Assert.AreEqual(32, array.Rank);
            for (var i = 0; i < array.Rank; i++)
            {
                Assert.AreEqual(1, array.GetLength(i));
            }
        }
    }
}