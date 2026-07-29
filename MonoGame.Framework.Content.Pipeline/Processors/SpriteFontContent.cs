// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics
{
    /// <summary>
    /// Represents the generated data for a sprite font asset.
    /// </summary>
    public class SpriteFontContent
    {
        /// <summary>
        /// Initializes an empty sprite font content instance.
        /// </summary>
        public SpriteFontContent() { }

        /// <summary>
        /// Initializes sprite font content from a font description.
        /// </summary>
        /// <param name="desc">The source font description.</param>
        public SpriteFontContent(FontDescription desc)
        {
            FontName = desc.FontName;
            Style = desc.Style;
            FontSize = desc.Size;
            CharacterMap = new List<char>(desc.Characters.Count);
            VerticalLineSpacing = (int)desc.Spacing; // Will be replaced in the pipeline.
            HorizontalSpacing = desc.Spacing;

            DefaultCharacter = desc.DefaultCharacter;
        }

        /// <summary>
        /// The source font face name.
        /// </summary>
        public string FontName = string.Empty;

        FontDescriptionStyle Style = FontDescriptionStyle.Regular;

        /// <summary>
        /// The sprite font size in points.
        /// </summary>
        public float FontSize;

        /// <summary>
        /// The texture atlas containing the glyphs.
        /// </summary>
        public Texture2DContent Texture = new Texture2DContent();

        /// <summary>
        /// The source rectangles for each glyph in the atlas.
        /// </summary>
        public List<Rectangle> Glyphs = new List<Rectangle>();

        /// <summary>
        /// The cropping rectangles for each glyph.
        /// </summary>
        public List<Rectangle> Cropping = new List<Rectangle>();

        /// <summary>
        /// The character assigned to each glyph.
        /// </summary>
        public List<char> CharacterMap = new List<char>();

        /// <summary>
        /// The vertical line spacing in pixels.
        /// </summary>
        public int VerticalLineSpacing;

        /// <summary>
        /// The additional horizontal spacing applied between glyphs.
        /// </summary>
        public float HorizontalSpacing;

        /// <summary>
        /// The kerning triplets for each glyph.
        /// </summary>
        public List<Vector3> Kerning = new List<Vector3>();

        /// <summary>
        /// The fallback character used when a glyph is missing.
        /// </summary>
        public Nullable<char> DefaultCharacter;

    }
}
