// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;
using System.IO;

namespace MonoGame.Effect
{
    /// <summary>
    /// Preprocesses effect source by resolving includes and macros.
    /// </summary>
    public static class Preprocessor
    {
        /// <summary>
        /// Preprocesses effect source using the provided macro definitions.
        /// </summary>
        /// <param name="effectCode">The source code to preprocess.</param>
        /// <param name="filePath">The path used to resolve relative includes.</param>
        /// <param name="defines">The preprocessor symbols to define.</param>
        /// <param name="dependencies">Receives the files referenced during preprocessing.</param>
        /// <param name="output">The diagnostic sink for preprocessing messages.</param>
        /// <returns>The preprocessed effect source.</returns>
        public static string Preprocess(
            string effectCode, string filePath, IDictionary<string, string> defines, List<string> dependencies,
            IEffectCompilerOutput output)
        {
            var fullPath = Path.GetFullPath(filePath);
            return new EffectPreprocessor(defines, dependencies, output, fullPath).Process(effectCode, fullPath);
        }
    }
}
