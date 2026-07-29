// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;

namespace MonoGame.Effect
{
    /// <summary>
    /// Stores the parsed source span and pass list for an effect technique.
    /// </summary>
    public class TechniqueInfo
    {
        /// <summary>
        /// The source start position of the technique declaration.
        /// </summary>
        public int startPos;

        /// <summary>
        /// The source length of the technique declaration.
        /// </summary>
        public int length;

        /// <summary>
        /// The technique name.
        /// </summary>
        public string name = string.Empty;

        /// <summary>
        /// The passes declared within the technique.
        /// </summary>
        public List<PassInfo> Passes = new List<PassInfo>();
    }
}
