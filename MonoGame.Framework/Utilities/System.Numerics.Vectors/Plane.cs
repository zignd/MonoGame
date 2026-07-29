namespace System.Numerics
{
    /// <summary>
    /// Represents a compatibility implementation of a plane for platforms without System.Numerics.Vectors.
    /// </summary>
    public struct Plane
    {
        /// <summary>The normal vector of the plane.</summary>
        public Vector3 Normal;

        /// <summary>The distance of the plane from the origin along its normal.</summary>
        public float D;
 
        /// <summary>Initializes a plane from its normal components and distance.</summary>
        /// <param name="x">The X component of the plane normal.</param>
        /// <param name="y">The Y component of the plane normal.</param>
        /// <param name="z">The Z component of the plane normal.</param>
        /// <param name="d">The plane distance from the origin.</param>
        public Plane(float x, float y, float z, float d)
        {
            Normal = new Vector3(x, y, z);
            D = d;
        }
 
        /// <summary>Initializes a plane from a normal vector and distance.</summary>
        /// <param name="normal">The normal vector of the plane.</param>
        /// <param name="d">The plane distance from the origin.</param>
        public Plane(Vector3 normal, float d)
        {
            Normal = normal;
            D = d;
        }
    }
}
