// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Globalization;
using Microsoft.Xna.Framework.Content.Pipeline;

namespace MonoGame.Content.Builder
{
    public class ConsoleLogger : ContentBuildLogger
    {
        [Obsolete("LogMessage is deprecated, please use Log instead.")]
        public override void LogMessage(string message, params object[] messageArgs)
        {
			Console.WriteLine(IndentString + message, messageArgs);
        }

        [Obsolete("LogImportantMessage is deprecated, please use Log instead.")]
        public override void LogImportantMessage(string message, params object[] messageArgs)
        {
            // TODO: How do i make it high importance?
            Console.WriteLine(IndentString + message, messageArgs);
        }

        [Obsolete("LogWarning is deprecated, please use Log instead.")]
        public override void LogWarning(string helpLink, ContentIdentity contentIdentity, string message, params object[] messageArgs)
        {
            var warning = string.Empty;
            if (contentIdentity != null && !string.IsNullOrEmpty(contentIdentity.SourceFilename))
            {
                warning = contentIdentity.SourceFilename;
                if (!string.IsNullOrEmpty(contentIdentity.FragmentIdentifier))
                    warning += "(" + contentIdentity.FragmentIdentifier + ")";
                warning += ": ";
            }

            if (messageArgs != null && messageArgs.Length != 0)
                warning += string.Format(CultureInfo.InvariantCulture, message, messageArgs);
            else if (!string.IsNullOrEmpty(message))
                warning += message;

            Console.WriteLine(warning);
        }
    }
}