// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MonoGame.Effect.TPGParser;

namespace MonoGame.Effect
{
    /// <summary>
    /// Represents parsed effect source and the metadata needed for shader compilation.
    /// </summary>
    public class ShaderResult
    {
        /// <summary>
        /// Gets the parsed shader techniques and sampler state declarations.
        /// </summary>
        public ShaderInfo? ShaderInfo { get; private set; }

        /// <summary>
        /// Gets the absolute path to the source effect file.
        /// </summary>
        public string FilePath { get; private set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the source file relative to the compilation root.
        /// </summary>
        public string RelativeFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets the preprocessed source content with effect-only syntax removed.
        /// </summary>
        public string FileContent { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the absolute path to the primary compiled output file, when one is configured.
        /// </summary>
        public string? OutputFilePath { get; private set; }

        /// <summary>
        /// Gets the source file dependencies discovered during preprocessing.
        /// </summary>
        public List<string> Dependencies { get; private set; } = [];

        /// <summary>
        /// Gets additional generated output files.
        /// </summary>
        public List<string> AdditionalOutputFiles { get; private set; } = [];

        /// <summary>
        /// Gets the shader profile selected for compilation.
        /// </summary>
        public ShaderProfile? Profile { get; private set; }

        /// <summary>
        /// Gets a value indicating whether debug compilation was requested.
        /// </summary>
        public bool Debug { get; private set; }


        /// <summary>
        /// Creates a <see cref="ShaderResult"/> by reading effect source from a file.
        /// </summary>
        /// <param name="path">The path to the source effect file.</param>
        /// <param name="options">The effect compiler options.</param>
        /// <param name="output">The diagnostic sink for preprocessing messages.</param>
        /// <returns>The parsed shader result.</returns>
        static public ShaderResult FromFile(string path, Options options, IEffectCompilerOutput output)
        {
            var effectSource = File.ReadAllText(path);
            return FromString(effectSource, path, options, output);
        }

        /// <summary>
        /// Creates a <see cref="ShaderResult"/> from effect source text.
        /// </summary>
        /// <param name="effectSource">The effect source code to parse.</param>
        /// <param name="filePath">The source file path used for diagnostics and include resolution.</param>
        /// <param name="options">The effect compiler options.</param>
        /// <param name="output">The diagnostic sink for preprocessing messages.</param>
        /// <returns>The parsed shader result.</returns>
        static public ShaderResult FromString(string effectSource, string filePath, Options options, IEffectCompilerOutput output)
        {
            var macros = new Dictionary<string, string>();
            macros.Add("MGFX", "1");

            options.Profile.AddMacros(macros);

            // If we're building shaders for debug set that flag too.
            if (options.Debug)
                macros.Add("DEBUG", "1");

            if (!string.IsNullOrEmpty(options.Defines))
            {
                var defines = options.Defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var define in defines)
                {
                    var name = define;
                    var value = "1";
                    if (define.Contains("="))
                    {
                        var parts = define.Split('=');

                        if (parts.Length > 0)
                            name = parts[0].Trim();

                        if (parts.Length > 1)
                            value = parts[1].Trim();
                    }

                    macros.Add(name, value);
                }
            }

            // Use the D3DCompiler to pre-process the file resolving
            // all #includes and macros.... this even works for GLSL.
            string newFile;
            var fullPath = Path.GetFullPath(filePath);
            var dependencies = new List<string>();
            newFile = Preprocessor.Preprocess(effectSource, fullPath, macros, dependencies, output);

            // Parse the resulting file for techniques and passes.
            var tree = new Parser(new Scanner()).Parse(newFile, fullPath);
            if (tree.Errors.Count > 0)
            {
                var errors = String.Empty;
                foreach (var error in tree.Errors)
                    errors += string.Format(CultureInfo.InvariantCulture, "{0}({1},{2}) : {3}\r\n", error.File, error.Line, error.Column, error.Message);

                throw new Exception(errors);
            }

            // Evaluate the results of the parse tree.
            var shaderInfo = tree.Eval() as ShaderInfo
                ?? throw new Exception("Failed to evaluate shader info from the parse tree.");

            // Remove the samplers and techniques so that the shader compiler
            // gets a clean file without any FX file syntax in it.
            var cleanFile = newFile;
            WhitespaceNodes(TokenType.Technique_Declaration, tree.Nodes, ref cleanFile);
            WhitespaceNodes(TokenType.Sampler_Declaration_States, tree.Nodes, ref cleanFile);

            // Setup the rest of the shader info.
            ShaderResult result = new ShaderResult();
            result.ShaderInfo = shaderInfo;
            result.Dependencies = dependencies;
            result.FilePath = fullPath;
            result.FileContent = cleanFile;
            if (!string.IsNullOrEmpty(options.OutputFile))
                result.OutputFilePath = Path.GetFullPath(options.OutputFile);
            result.AdditionalOutputFiles = new List<string>();

            // Remove empty techniques.
            for (var i = 0; i < shaderInfo.Techniques.Count; i++)
            {
                var tech = shaderInfo.Techniques[i];
                if (tech.Passes.Count <= 0)
                {
                    shaderInfo.Techniques.RemoveAt(i);
                    i--;
                }
            }

            // We must have at least one technique.
            if (shaderInfo.Techniques.Count <= 0)
                throw new Exception("The effect must contain at least one technique and pass!");

            result.Profile = options.Profile;
            result.Debug = options.Debug;

            return result;
        }

        static void WhitespaceNodes(TokenType type, List<ParseNode> nodes, ref string sourceFile)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.Token.Type != type)
                {
                    WhitespaceNodes(type, n.Nodes, ref sourceFile);
                    continue;
                }

                // Get the full content of this node.
                var start = n.Token.StartPos;
                var end = n.Token.EndPos;
                var length = end - n.Token.StartPos;
                var content = sourceFile.Substring(start, length);

                // Replace the content of this node with whitespace.
                for (var c = 0; c < length; c++)
                {
                    if (!char.IsWhiteSpace(content[c]))
                        content = content.Replace(content[c], ' ');
                }

                // Add the whitespace back to the source file.
                var newfile = sourceFile.Substring(0, start);
                newfile += content;
                newfile += sourceFile.Substring(end);
                sourceFile = newfile;
            }
        }
    }
}
