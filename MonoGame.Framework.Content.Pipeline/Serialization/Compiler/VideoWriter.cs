// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler
{
    [ContentTypeWriter]
    class VideoWriter : BuiltInContentWriter<VideoContent>
    {
        protected internal override void Write(ContentWriter output, [AllowNull] VideoContent value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            output.WriteObject<string>(value.Filename);
            output.WriteObject<int>((int)value.Duration.TotalMilliseconds);
            output.WriteObject<int>(value.Width);
            output.WriteObject<int>(value.Height);
            output.WriteObject<float>(value.FramesPerSecond);
            output.WriteObject<int>((int)value.VideoSoundtrackType);
        }
    }
}
