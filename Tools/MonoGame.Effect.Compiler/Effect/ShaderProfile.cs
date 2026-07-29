// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Xna.Framework.Content.Pipeline;
using MonoGame.Effect.TPGParser;

namespace MonoGame.Effect
{
    /// <summary>
    /// Describes a shader compilation target and its effect binary format.
    /// </summary>
    [TypeConverter(typeof(StringConverter))]
    public abstract class ShaderProfile
    {
        private static readonly LoadedTypeCollection<ShaderProfile> _profiles = new LoadedTypeCollection<ShaderProfile>();

        /// <summary>
        /// Initializes a shader profile with the specified name and format identifier.
        /// </summary>
        /// <param name="name">The display name of the profile.</param>
        /// <param name="formatId">The MGFX format identifier for the profile.</param>
        protected ShaderProfile(string name, byte formatId)
        {
            Name = name;
            FormatId = formatId;
        }

        /// <summary>
        /// Gets the shader profile used for OpenGL-family targets.
        /// </summary>
        public static readonly ShaderProfile OpenGL = FromName("OpenGL")
            ?? throw new InvalidOperationException("Shader profile 'OpenGL' was not loaded.");

        /// <summary>
        /// Gets the shader profile used for DirectX 11 targets.
        /// </summary>
        public static readonly ShaderProfile DirectX_11 = FromName("DirectX_11")
            ?? throw new InvalidOperationException("Shader profile 'DirectX_11' was not loaded.");

        /// <summary>
        /// Gets the shader profile used for DirectX 12 targets.
        /// </summary>
        public static readonly ShaderProfile DirectX_12 = FromName("DirectX_12")
            ?? throw new InvalidOperationException("Shader profile 'DirectX_12' was not loaded.");

        /// <summary>
        /// Gets the shader profile used for Vulkan targets.
        /// </summary>
        public static readonly ShaderProfile Vulkan = FromName("Vulkan")
            ?? throw new InvalidOperationException("Shader profile 'Vulkan' was not loaded.");

        /// <summary>
        /// Returns all the loaded shader profiles.
        /// </summary>
        public static IEnumerable<ShaderProfile> All
        {
            get { return _profiles; }
        }

        /// <summary>
        /// Returns the name of the shader profile.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the format identifier used in the MGFX file format.
        /// </summary>
        public byte FormatId { get; private set; }

        /// <summary>
        /// Returns the profile by name or null if no match is found.
        /// </summary>
        public static ShaderProfile? FromName(string name)
        {
            return _profiles.FirstOrDefault(p => p.Name == name);
        }

        internal abstract void AddMacros(Dictionary<string, string> macros);

        internal abstract void ValidateShaderModels(PassInfo pass);

        internal abstract ShaderData CreateShader(ShaderResult shaderResult, string shaderFunction, string shaderProfile, bool isVertexShader, EffectObject effect, ref string errorsAndWarnings);

        /// <summary>
        /// Parses a shader model version from text using the specified regular expression.
        /// </summary>
        /// <param name="text">The text containing the shader model token.</param>
        /// <param name="regex">The expression that captures the major and minor version components.</param>
        /// <param name="major">Receives the parsed major version, or <c>0</c> if no match is found.</param>
        /// <param name="minor">Receives the parsed minor version, or <c>0</c> if no match is found.</param>
        protected static void ParseShaderModel(string text, Regex regex, out int major, out int minor)
        {
            var match = regex.Match(text);
            if (!match.Success)
            {
                major = 0;
                minor = 0;
                return;
            }

            major = int.Parse(match.Groups["major"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
            minor = int.Parse(match.Groups["minor"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the default shader profile for a content pipeline target platform.
        /// </summary>
        /// <param name="platform">The target platform.</param>
        /// <returns>The shader profile used for that platform.</returns>
        public static ShaderProfile GetProfileForPlatform(TargetPlatform platform) => platform switch
        {
            TargetPlatform.Windows => ShaderProfile.DirectX_11,
            TargetPlatform.iOS or TargetPlatform.Android or TargetPlatform.DesktopGL or TargetPlatform.MacOSX or TargetPlatform.RaspberryPi or TargetPlatform.Web => ShaderProfile.OpenGL,
            TargetPlatform.DesktopVK => ShaderProfile.Vulkan,
            TargetPlatform.WindowsDX12 or TargetPlatform.XboxOne or TargetPlatform.XboxSeries => ShaderProfile.DirectX_12,
            _ => ShaderProfile.FromName(platform.ToString())
                ?? throw new InvalidOperationException($"No shader profile is registered for platform '{platform}'.")
        };

        private class StringConverter : TypeConverter
        {
            public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
            {
                if (value is string)
                {
                    var name = value as string;

                    foreach (var e in All)
                    {
                        if (e.Name == name)
                            return e;
                    }
                }

                return base.ConvertFrom(context, culture, value)
                    ?? throw new NotSupportedException($"Could not convert '{value}' to a shader profile.");
            }
        }
    }
}
