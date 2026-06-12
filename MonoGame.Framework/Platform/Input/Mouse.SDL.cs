// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

namespace Microsoft.Xna.Framework.Input
{
    public static partial class Mouse
    {
        internal static int ScrollX;
        internal static int ScrollY;

        private static IntPtr PlatformGetWindowHandle()
        {
            return PrimaryWindow.Handle;
        }
        
        private static void PlatformSetWindowHandle(IntPtr windowHandle)
        {
        }

        private static MouseState PlatformGetState(GameWindow window)
        {
            int x, y;
            var winFlags = Sdl.Window.GetWindowFlags(window.Handle);
            var state = Sdl.Mouse.GetGlobalState(out x, out y);
            var clientBounds = window.ClientBounds;

            window.MouseState.LeftButton = (state & Sdl.Mouse.Button.Left) != 0 ? ButtonState.Pressed : ButtonState.Released;
            window.MouseState.MiddleButton = (state & Sdl.Mouse.Button.Middle) != 0 ? ButtonState.Pressed : ButtonState.Released;
            window.MouseState.RightButton = (state & Sdl.Mouse.Button.Right) != 0 ? ButtonState.Pressed : ButtonState.Released;
            window.MouseState.XButton1 = (state & Sdl.Mouse.Button.X1Mask) != 0 ? ButtonState.Pressed : ButtonState.Released;
            window.MouseState.XButton2 = (state & Sdl.Mouse.Button.X2Mask) != 0 ? ButtonState.Pressed : ButtonState.Released;

            window.MouseState.HorizontalScrollWheelValue = ScrollX;
            window.MouseState.ScrollWheelValue = ScrollY;

            window.MouseState.X = x - clientBounds.X;
            window.MouseState.Y = y - clientBounds.Y;

            // The OS reports the cursor in logical points; convert to back-buffer pixels when a
            // high-DPI drawable is active so hit-testing matches what's rendered. Scale is 1
            // otherwise, so this is a no-op on the default path and on Windows/Linux.
            var scale = ((SdlGameWindow)window).Scale;
            if (scale != 1f)
            {
                window.MouseState.X = (int)(window.MouseState.X * scale);
                window.MouseState.Y = (int)(window.MouseState.Y * scale);
            }

            // Borderless desktop fullscreen on macOS: SDL_GetWindowPosition reports the window
            // origin as (0,0) even though the content area begins below the menu bar, so the
            // subtraction above leaves the cursor offset by the menu-bar band (and any side chrome).
            // The drawable is that much smaller than the full screen, so correct by subtracting
            // (physicalScreen - drawable) per axis. Only fullscreen-desktop windows are affected;
            // the offset is 0 everywhere the window already fills the display, so this is otherwise
            // a no-op.
            var flags = Sdl.Window.GetWindowFlags(window.Handle);
            if ((flags & Sdl.Window.State.FullscreenDesktop) == Sdl.Window.State.FullscreenDesktop)
            {
                var displayIndex = Sdl.Window.GetDisplayIndex(window.Handle);
                Sdl.Rectangle displayRect;
                Sdl.Display.GetBounds(displayIndex, out displayRect);

                var drawW = (int)(displayRect.Width * scale);
                var drawH = (int)(displayRect.Height * scale);
                if (scale != 1f)
                {
                    Sdl.GL.GetDrawableSize(window.Handle, out drawW, out drawH);
                }

                window.MouseState.X -= (int)(displayRect.Width * scale) - drawW;
                window.MouseState.Y -= (int)(displayRect.Height * scale) - drawH;
            }

            return window.MouseState;
        }

        private static void PlatformSetPosition(int x, int y)
        {
            PrimaryWindow.MouseState.X = x;
            PrimaryWindow.MouseState.Y = y;

            // x/y are in back-buffer pixels; WarpInWindow expects logical points.
            var scale = ((SdlGameWindow)PrimaryWindow).Scale;
            if (scale != 1f)
            {
                x = (int)(x / scale);
                y = (int)(y / scale);
            }

            Sdl.Mouse.WarpInWindow(PrimaryWindow.Handle, x, y);
        }

        private static void PlatformSetCursor(MouseCursor cursor)
        {
            Sdl.Mouse.SetCursor(cursor.Handle);
        }
    }
}
