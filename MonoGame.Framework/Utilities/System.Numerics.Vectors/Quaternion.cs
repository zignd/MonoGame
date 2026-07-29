namespace System.Numerics
{
    /// <summary>
    /// Represents a compatibility implementation of a quaternion for platforms without System.Numerics.Vectors.
    /// </summary>
    public struct Quaternion
    {
        /// <summary>The X component of the quaternion vector part.</summary>
        public float X;

        /// <summary>The Y component of the quaternion vector part.</summary>
        public float Y;

        /// <summary>The Z component of the quaternion vector part.</summary>
        public float Z;

        /// <summary>The scalar component of the quaternion.</summary>
        public float W;
 
        /// <summary>Initializes a quaternion from its four components.</summary>
        /// <param name="x">The X component of the quaternion vector part.</param>
        /// <param name="y">The Y component of the quaternion vector part.</param>
        /// <param name="z">The Z component of the quaternion vector part.</param>
        /// <param name="w">The scalar component of the quaternion.</param>
        public Quaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }
}
