namespace System.Numerics
{
    /// <summary>
    /// Represents a compatibility implementation of a 4x4 matrix for platforms without System.Numerics.Vectors.
    /// </summary>
    public struct Matrix4x4
    {
        /// <summary>The value at row 1, column 1.</summary>
        public float M11;

        /// <summary>The value at row 1, column 2.</summary>
        public float M12;

        /// <summary>The value at row 1, column 3.</summary>
        public float M13;

        /// <summary>The value at row 1, column 4.</summary>
        public float M14;
 
        /// <summary>The value at row 2, column 1.</summary>
        public float M21;

        /// <summary>The value at row 2, column 2.</summary>
        public float M22;

        /// <summary>The value at row 2, column 3.</summary>
        public float M23;

        /// <summary>The value at row 2, column 4.</summary>
        public float M24;
 
        /// <summary>The value at row 3, column 1.</summary>
        public float M31;

        /// <summary>The value at row 3, column 2.</summary>
        public float M32;

        /// <summary>The value at row 3, column 3.</summary>
        public float M33;

        /// <summary>The value at row 3, column 4.</summary>
        public float M34;
 
        /// <summary>The value at row 4, column 1.</summary>
        public float M41;

        /// <summary>The value at row 4, column 2.</summary>
        public float M42;

        /// <summary>The value at row 4, column 3.</summary>
        public float M43;

        /// <summary>The value at row 4, column 4.</summary>
        public float M44;
        
        /// <summary>Initializes a matrix from its sixteen elements.</summary>
        /// <param name="m11">The value at row 1, column 1.</param>
        /// <param name="m12">The value at row 1, column 2.</param>
        /// <param name="m13">The value at row 1, column 3.</param>
        /// <param name="m14">The value at row 1, column 4.</param>
        /// <param name="m21">The value at row 2, column 1.</param>
        /// <param name="m22">The value at row 2, column 2.</param>
        /// <param name="m23">The value at row 2, column 3.</param>
        /// <param name="m24">The value at row 2, column 4.</param>
        /// <param name="m31">The value at row 3, column 1.</param>
        /// <param name="m32">The value at row 3, column 2.</param>
        /// <param name="m33">The value at row 3, column 3.</param>
        /// <param name="m34">The value at row 3, column 4.</param>
        /// <param name="m41">The value at row 4, column 1.</param>
        /// <param name="m42">The value at row 4, column 2.</param>
        /// <param name="m43">The value at row 4, column 3.</param>
        /// <param name="m44">The value at row 4, column 4.</param>
        public Matrix4x4(
            float m11, float m12, float m13, float m14,
            float m21, float m22, float m23, float m24,
            float m31, float m32, float m33, float m34,
            float m41, float m42, float m43, float m44)
        {
            M11 = m11;
            M12 = m12;
            M13 = m13;
            M14 = m14;
 
            M21 = m21;
            M22 = m22;
            M23 = m23;
            M24 = m24;
 
            M31 = m31;
            M32 = m32;
            M33 = m33;
            M34 = m34;
 
            M41 = m41;
            M42 = m42;
            M43 = m43;
            M44 = m44;
        }
    }
}
