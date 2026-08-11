#if VULKAN || DIRECTX12 || METAL
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Interop;
using NUnit.Framework;

namespace MonoGame.Tests.Framework.Platform;

[RunOnUiTestFixture]
[NonParallelizable]
[Category("NativeSdl3")]
internal unsafe partial class NativeSdl3SmokeTest
{
    private const uint SdlEventKeyDown = 0x300;
    private const uint SdlEventKeyUp = 0x301;
    private const uint SdlEventMouseMotion = 0x400;
    private const uint SdlEventMouseButtonDown = 0x401;
    private const uint SdlEventGamepadButtonDown = 0x651;
    private const ulong SdlWindowFullscreen = 0x1;

    private MGP_Platform* _platform;
    private MGP_Window* _window;

    [SetUp]
    public void SetUp()
    {
        Assert.That(MGP.Platform_GetSdlVersion() / 1_000_000, Is.EqualTo(3), "The Native test runner must load SDL3.");

        _platform = MGP.Platform_Create(out GameRunBehavior behavior);
        Assert.Multiple(() =>
        {
            Assert.That((nint)_platform, Is.Not.EqualTo(nint.Zero));
            Assert.That(behavior, Is.EqualTo(GameRunBehavior.Synchronous));
        });

        var width = 640;
        var height = 360;
        _window = MGP.Window_Create(_platform, ref width, ref height, nameof(NativeSdl3SmokeTest));
        Assert.That((nint)_window, Is.Not.EqualTo(nint.Zero));
        MGP.Window_Show(_window, 1);
    }

    [TearDown]
    public void TearDown()
    {
        if (_window != null)
        {
            MGP.Window_Destroy(_window);
            _window = null;
        }

        if (_platform != null)
        {
            MGP.Platform_Destroy(_platform);
            _platform = null;
        }
    }

    [TestCase(SdlEventKeyDown, EventType.KeyDown)]
    [TestCase(SdlEventKeyUp, EventType.KeyUp)]
    public void KeyboardEvent_IsTranslated(uint sdlEventType, EventType expectedType)
    {
        var sdlEvent = new SdlEvent
        {
            Type = sdlEventType,
            Key = 'w'
        };

        Assert.That(MGP.Platform_PushSdlEvent((nint)(&sdlEvent)), Is.Not.Zero);
        Assert.That(TryPoll(expectedType, out MGP_Event translated), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(translated.Key.Character, Is.EqualTo((uint)'w'));
            Assert.That(translated.Key.Key, Is.EqualTo(Keys.W));
        });
    }

    [Test]
    public void MouseMotionEvent_IsTranslated()
    {
        var sdlEvent = new SdlEvent
        {
            Type = SdlEventMouseMotion,
            MotionX = 123,
            MotionY = 45
        };

        Assert.That(MGP.Platform_PushSdlEvent((nint)(&sdlEvent)), Is.Not.Zero);
        Assert.That(TryPollMouseMove(123, 45), Is.True);
    }

    [Test]
    public void MouseButtonEvent_PreservesClickPosition()
    {
        var sdlEvent = new SdlEvent
        {
            Type = SdlEventMouseButtonDown,
            MouseButton = 1,
            MotionX = 321,
            MotionY = 87
        };

        Assert.That(MGP.Platform_PushSdlEvent((nint)(&sdlEvent)), Is.Not.Zero);
        Assert.That(TryPoll(EventType.MouseButtonDown, out MGP_Event translated), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(translated.MouseButton.Button, Is.EqualTo(MouseButton.Left));
            Assert.That(translated.MouseButton.X, Is.EqualTo(321));
            Assert.That(translated.MouseButton.Y, Is.EqualTo(87));
        });
    }

    private bool TryPollMouseMove(int expectedX, int expectedY)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            while (MGP.Platform_PollEvent(_platform, out MGP_Event translated) != 0)
            {
                if (translated.Type == EventType.MouseMove &&
                    translated.MouseMove.X == expectedX &&
                    translated.MouseMove.Y == expectedY)
                    return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    [Test]
    public void GamepadButtonEvent_IsTranslated()
    {
        var sdlEvent = new SdlEvent
        {
            Type = SdlEventGamepadButtonDown,
            GamepadWhich = 42,
            GamepadButton = 0
        };

        Assert.That(MGP.Platform_PushSdlEvent((nint)(&sdlEvent)), Is.Not.Zero);
        Assert.That(TryPoll(EventType.ControllerStateChange, out MGP_Event translated), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(translated.Controller.Id, Is.EqualTo(42));
            Assert.That(translated.Controller.Input, Is.EqualTo(ControllerInput.A));
            Assert.That(translated.Controller.Value, Is.EqualTo(1));
        });
    }

    [Test]
    public void NativeBackend_CreatesGraphicsDeviceAndRunsFrame()
    {
        using var game = new Game();
        using var graphicsDeviceManager = new GraphicsDeviceManager(game);

        Assert.DoesNotThrow(game.RunOneFrame);
        Assert.Multiple(() =>
        {
            Assert.That(game.GraphicsDevice, Is.Not.Null);
            Assert.That(game.GraphicsDevice.GraphicsDeviceStatus, Is.EqualTo(GraphicsDeviceStatus.Normal));
        });
    }

#if METAL
    [Test]
    [Category("Fullscreen")]
    public void MetalFullscreenTransitions_NeverAcquireStaleSizeDrawable()
    {
        using var game = new MetalFullscreenTransitionGame();

        game.Run();

        Assert.That(game.CompletedTransitions, Is.EqualTo(MetalFullscreenTransitionGame.TransitionCount));
        Assert.That(
            MGG.GraphicsDevice_GetDrawableSizeMismatchCount(game.GraphicsDevice.Handle),
            Is.Zero,
            "Metal drawable-size validation was disabled or an acquired drawable did not match the live Cocoa backing size.");
    }
#endif

    [Test]
    [Category("Fullscreen")]
    public void BorderlessFullscreen_EntersAndRestoresWindowedAspect()
    {
        MGP.Window_GetDrawableSize(_window, out int initialWidth, out int initialHeight);
        AssertPositiveSize(initialWidth, initialHeight);

        MGP.Window_EnterFullScreen(_window, 0);
        Assert.That(WaitForFullscreen(true), Is.True, "SDL3 did not enter fullscreen.");
        MGP.Window_GetDrawableSize(_window, out int fullscreenWidth, out int fullscreenHeight);
        AssertPositiveSize(fullscreenWidth, fullscreenHeight);

        MGP.Window_ExitFullScreen(_window);
        Assert.That(WaitForFullscreen(false), Is.True, "SDL3 did not exit fullscreen.");
        MGP.Window_GetDrawableSize(_window, out int restoredWidth, out int restoredHeight);
        AssertPositiveSize(restoredWidth, restoredHeight);

        var initialAspect = (double)initialWidth / initialHeight;
        var restoredAspect = (double)restoredWidth / restoredHeight;
        Assert.That(restoredAspect, Is.EqualTo(initialAspect).Within(0.01));
    }

    private bool TryPoll(EventType expectedType, out MGP_Event translated)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            while (MGP.Platform_PollEvent(_platform, out translated) != 0)
            {
                if (translated.Type == expectedType)
                    return true;
            }

            Thread.Sleep(10);
        }

        translated = default;
        return false;
    }

    private bool WaitForFullscreen(bool expected)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            while (MGP.Platform_PollEvent(_platform, out _) != 0)
            {
            }

            var isFullscreen = (MGP.Window_GetSdlFlags(_window) & SdlWindowFullscreen) != 0;
            if (isFullscreen == expected)
                return true;

            Thread.Sleep(10);
        }

        return false;
    }

    private static void AssertPositiveSize(int width, int height)
    {
        Assert.Multiple(() =>
        {
            Assert.That(width, Is.GreaterThan(0));
            Assert.That(height, Is.GreaterThan(0));
        });
    }

#if METAL
    private sealed class MetalFullscreenTransitionGame : Game
    {
        public const int TransitionCount = 8;
        private const int FramesPerState = 12;
        private readonly GraphicsDeviceManager _graphics;
        private int _framesInState;

        public MetalFullscreenTransitionGame()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 640,
                PreferredBackBufferHeight = 360,
                SynchronizeWithVerticalRetrace = false,
            };
            IsFixedTimeStep = false;
        }

        public int CompletedTransitions { get; private set; }

        protected override void Update(GameTime gameTime)
        {
            if (++_framesInState < FramesPerState)
            {
                base.Update(gameTime);
                return;
            }

            _framesInState = 0;
            if (CompletedTransitions == TransitionCount)
            {
                Exit();
                return;
            }

            _graphics.IsFullScreen = !_graphics.IsFullScreen;
            _graphics.ApplyChanges();
            CompletedTransitions++;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            base.Draw(gameTime);
        }
    }
#endif

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct SdlEvent
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(28)]
        public uint Key;

        [FieldOffset(28)]
        public float MotionX;

        [FieldOffset(32)]
        public float MotionY;

        [FieldOffset(24)]
        public byte MouseButton;

        [FieldOffset(16)]
        public int GamepadWhich;

        [FieldOffset(20)]
        public byte GamepadButton;
    }

}
#endif
