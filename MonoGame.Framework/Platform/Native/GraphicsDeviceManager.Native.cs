// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework
{
    public partial class GraphicsDeviceManager
    {
        // High-DPI is the default on the native backend where the OS distinguishes logical points
        // from physical pixels (currently macOS, via SDL_WINDOW_ALLOW_HIGHDPI). PreferredBackBuffer*
        // are treated as points; here they're scaled to physical pixels so the back buffer, viewport
        // and Vulkan swapchain are sized to the drawable and rendering is crisp. The window itself
        // stays in points (see NativeGameWindow). Scale is 1 on non-HiDPI displays, so this is a
        // no-op there and on Windows/Linux.
        partial void PlatformPreparePresentationParameters(PresentationParameters presentationParameters)
        {
            if (_game.Window is not NativeGameWindow window)
                return;

            var scale = window.Scale;
            if (scale == 1f)
                return;

            presentationParameters.BackBufferWidth = (int)(presentationParameters.BackBufferWidth * scale);
            presentationParameters.BackBufferHeight = (int)(presentationParameters.BackBufferHeight * scale);
        }
    }
}
