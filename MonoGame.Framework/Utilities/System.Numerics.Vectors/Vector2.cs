namespace System.Numerics
{
    /// <summary>
    /// Represents a compatibility implementation of a 2D vector for platforms without System.Numerics.Vectors.
    /// </summary>
    public struct Vector2
    {
        /// <summary>The X component of the vector.</summary>
        public Single X;

        /// <summary>The Y component of the vector.</summary>
        public Single Y;

        /// <summary>Creates a vector with the specified values.</summary>
        /// <param name="x">The value assigned to the <see cref="X"/> field.</param>
        /// <param name="y">The value assigned to the <see cref="Y"/> field.</param>
        public Vector2(Single x, Single y)
        {
            X = x;
            Y = y;
        }
    }
}
