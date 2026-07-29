// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Globalization;
using Microsoft.Xna.Framework.Content.Pipeline;

namespace MonoGame.Framework.Content.Pipeline.Builder
{
    /// <inheritdoc cref="ContentBuildLogger">ContentBuildLogger</inheritdoc>
    public class PipelineBuildLogger : ContentBuildLogger
    {
        /// <inheritdoc/>
        [Obsolete("LogMessage is deprecated, please use Log instead.")]
        public override void LogMessage(string message, params object[] messageArgs)
        {
			System.Diagnostics.Trace.WriteLine(string.Format(CultureInfo.InvariantCulture, message, messageArgs));
        }

        /// <inheritdoc/>
        [Obsolete("LogImportantMessage is deprecated, please use Log instead.")]
        public override void LogImportantMessage(string message, params object[] messageArgs)
        {
            // TODO: How do i make it high importance?
			System.Diagnostics.Trace.WriteLine(string.Format(CultureInfo.InvariantCulture, message, messageArgs));
        }

        /// <inheritdoc/>
        [Obsolete("LogWarning is deprecated, please use Log instead.")]
        public override void LogWarning(string helpLink, ContentIdentity contentIdentity, string message, params object[] messageArgs)
        {
            var msg = string.Format(CultureInfo.InvariantCulture, message, messageArgs);
            var fileName = GetCurrentFilename(contentIdentity);
			System.Diagnostics.Trace.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", fileName, msg));
        }

    }

}
