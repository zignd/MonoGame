// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Text.RegularExpressions;

namespace MonoGame.Framework.Content.Pipeline.Builder;

/// <summary>
/// A wildcard based rule by which <see cref="ContentCollection"/> includes or excludes the files.
/// </summary>
public class WildcardRule : ContentRule
{
    /// <summary>
    /// Used in <see cref="ContentCollection"/> to check if the passed filePath matches the current Wildcard <see cref="ContentRule.Pattern"/>.
    /// </summary>
    /// <param name="filePath">Relative path to the content file.</param>
    /// <returns>Returns true if filePath matches the Wildcard <see cref="ContentRule.Pattern"/>.</returns>
    public override bool IsMatch(string filePath)
    {
        var pattern = "^" + Regex.Escape(Pattern.Sanitize())
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return Regex.IsMatch(filePath, pattern, RegexOptions.CultureInvariant);
    }
}
