namespace System.Numerics
{
    /// <summary>
    /// Represents a compatibility implementation of a 4D vector for platforms without System.Numerics.Vectors.
    /// </summary>
    public struct Vector4
    {
        /// <summary>The X component of the vector.</summary>
        public Single X;

        /// <summary>The Y component of the vector.</summary>
        public Single Y;

        /// <summary>The Z component of the vector.</summary>
        public Single Z;

        /// <summary>The W component of the vector.</summary>
        public Single W;
 
        /// <summary>Creates a vector with the specified values.</summary>
        /// <param name="x">The value assigned to the <see cref="X"/> field.</param>
        /// <param name="y">The value assigned to the <see cref="Y"/> field.</param>
        /// <param name="z">The value assigned to the <see cref="Z"/> field.</param>
        /// <param name="w">The value assigned to the <see cref="W"/> field.</param>
        public Vector4(Single x, Single y, Single z, Single w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }
}