namespace MonoGame.Effect
{
    /// <summary>
    /// Receives diagnostics emitted while preprocessing and compiling effects.
    /// </summary>
    public interface IEffectCompilerOutput
    {
        /// <summary>
        /// Writes a compiler warning.
        /// </summary>
        /// <param name="file">The source file associated with the warning.</param>
        /// <param name="line">The 1-based source line.</param>
        /// <param name="column">The 1-based source column.</param>
        /// <param name="message">The warning message.</param>
        void WriteWarning(string file, int line, int column, string message);

        /// <summary>
        /// Writes a compiler error.
        /// </summary>
        /// <param name="file">The source file associated with the error.</param>
        /// <param name="line">The 1-based source line.</param>
        /// <param name="column">The 1-based source column.</param>
        /// <param name="message">The error message.</param>
        void WriteError(string file, int line, int column, string message);
    }
}