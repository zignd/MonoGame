// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.IO;

namespace MonoGame.Effect
{
    /// <summary>
    /// Contains the parsed techniques and sampler states for an effect.
    /// </summary>
    public class ShaderInfo
	{
		/// <summary>
		/// The techniques declared by the effect.
		/// </summary>
		public List<TechniqueInfo> Techniques = new List<TechniqueInfo>();

		/// <summary>
		/// The sampler states declared by name.
		/// </summary>
        public Dictionary<string, SamplerStateInfo> SamplerStates = new Dictionary<string, SamplerStateInfo>();
	}
}
