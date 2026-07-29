// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

namespace MonoGame.Effect
{
	/// <summary>
	/// Represents a shader compilation failure.
	/// </summary>
	public class ShaderCompilerException : Exception
	{
	    /// <summary>
	    /// Initializes the exception with the default shader compilation message.
	    /// </summary>
	    public ShaderCompilerException()
	        : base("A shader failed to compile!")
	    {	        
	    }
	}
}

