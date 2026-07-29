
namespace MonoGame.Effect
{
    /// <summary>
    /// Describes a sampler's filter mode at a single filtering stage.
    /// </summary>
    public enum TextureFilterType
    {
        /// <summary>
        /// Disables filtering for the stage.
        /// </summary>
        None,

        /// <summary>
        /// Uses point sampling.
        /// </summary>
        Point,

        /// <summary>
        /// Uses linear sampling.
        /// </summary>
        Linear, 

        /// <summary>
        /// Uses anisotropic sampling.
        /// </summary>
        Anisotropic,
    }
}
