// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace MonoGame.Effect
{
    /// <summary>
    /// Defines command-line options for the effect compiler.
    /// </summary>
    public class Options
    {
        /// <summary>
        /// The source effect file to compile.
        /// </summary>
        [CommandLineParser.Required]
        public string SourceFile = string.Empty;

        /// <summary>
        /// The destination path for the compiled output.
        /// </summary>
        [CommandLineParser.Required]
        [CommandLineParser.Name("OutputFile", "\t - The output file path.  Use a .h extension to generate a C header file.")]
        public string OutputFile = string.Empty;

        /// <summary>
        /// The shader profile used to compile the effect.
        /// </summary>
        [CommandLineParser.ProfileName]
        public ShaderProfile Profile = ShaderProfile.OpenGL;

        /// <summary>
        /// Enables debug information in the compiled output.
        /// </summary>
        [CommandLineParser.Name("Debug", "\t\t - Include extra debug information in the compiled effect.")]
        public bool Debug;

        /// <summary>
        /// Semicolon-delimited preprocessor define assignments.
        /// </summary>
        [CommandLineParser.Name("Defines", "\t - Semicolon-delimited define assignments")]
        public string Defines = string.Empty;
    }
}
