// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

#if ANDROID
using Android.Views;
#endif

namespace Microsoft.Xna.Framework
{
    /// <summary>
    /// Manages graphics device creation and presentation settings for a <see cref="Game"/>.
    /// </summary>
    public class GraphicsDeviceManager : IGraphicsDeviceService, IDisposable, IGraphicsDeviceManager
    {
        private Game _game;
        private GraphicsDevice _graphicsDevice;
        private int _preferredBackBufferHeight;
        private int _preferredBackBufferWidth;
        private SurfaceFormat _preferredBackBufferFormat;
        private DepthFormat _preferredDepthStencilFormat;
        private bool _preferMultiSampling;
        private DisplayOrientation _supportedOrientations;
        private bool _synchronizedWithVerticalRetrace = true;
        private bool _drawBegun;
        bool disposed;
        private bool _hardwareModeSwitch = true;
        private bool _preferHalfPixelOffset = false;

#if WINDOWS && DIRECTX
        private bool _firstLaunch = true;
#endif

        private bool _wantFullScreen = false;
        /// <summary>
        /// The default back-buffer height.
        /// </summary>
        public static readonly int DefaultBackBufferHeight = 480;

        /// <summary>
        /// The default back-buffer width.
        /// </summary>
        public static readonly int DefaultBackBufferWidth = 800;

        /// <summary>
        /// Initializes a new graphics device manager for the provided game.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public GraphicsDeviceManager(Game game)
        {
            if (game == null)
                throw new ArgumentNullException("The game cannot be null!");

            _game = game;

            _supportedOrientations = DisplayOrientation.Default;

#if WINDOWS || MONOMAC || DESKTOPGL
            _preferredBackBufferHeight = DefaultBackBufferHeight;
            _preferredBackBufferWidth = DefaultBackBufferWidth;
#else
            // Preferred buffer width/height is used to determine default supported orientations,
            // so set the default values to match Xna behaviour of landscape only by default.
            // Note also that it's using the device window dimensions.
            _preferredBackBufferWidth = Math.Max(_game.Window.ClientBounds.Height, _game.Window.ClientBounds.Width);
            _preferredBackBufferHeight = Math.Min(_game.Window.ClientBounds.Height, _game.Window.ClientBounds.Width);
#endif

            _preferredBackBufferFormat = SurfaceFormat.Color;
            _preferredDepthStencilFormat = DepthFormat.Depth24;
            _synchronizedWithVerticalRetrace = true;

            // XNA would read this from the manifest, but it would always default
            // to Reach unless changed.  So lets mimic that without the manifest bit.
            GraphicsProfile = GraphicsProfile.Reach;

            if (_game.Services.GetService(typeof(IGraphicsDeviceManager)) != null)
                throw new ArgumentException("Graphics Device Manager Already Present");

            _game.Services.AddService(typeof(IGraphicsDeviceManager), this);
            _game.Services.AddService(typeof(IGraphicsDeviceService), this);
        }

        /// <summary>
        /// Finalizes the graphics device manager.
        /// </summary>
        ~GraphicsDeviceManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// Creates and initializes the graphics device.
        /// </summary>
        public void CreateDevice()
        {
            Initialize();

            OnDeviceCreated(EventArgs.Empty);
        }

        /// <summary>
        /// Prepares the graphics device for drawing.
        /// </summary>
        /// <returns><see langword="true"/> if drawing can begin; otherwise <see langword="false"/>.</returns>
        public bool BeginDraw()
        {
            if (_graphicsDevice == null)
                return false;

            _drawBegun = true;
            return true;
        }

        /// <summary>
        /// Presents the back buffer when drawing has begun.
        /// </summary>
        public void EndDraw()
        {
            if (_graphicsDevice != null && _drawBegun)
            {
                _drawBegun = false;
                _graphicsDevice.Present();
            }
        }

        #region IGraphicsDeviceService Members

        /// <summary>
        /// Raised when the graphics device is created.
        /// </summary>
        public event EventHandler<EventArgs> DeviceCreated;

        /// <summary>
        /// Raised before the graphics device is disposed.
        /// </summary>
        public event EventHandler<EventArgs> DeviceDisposing;

        /// <summary>
        /// Raised after the graphics device is reset.
        /// </summary>
        public event EventHandler<EventArgs> DeviceReset;

        /// <summary>
        /// Raised before the graphics device is reset.
        /// </summary>
        public event EventHandler<EventArgs> DeviceResetting;

        /// <summary>
        /// Raised before device settings are used to create the graphics device.
        /// </summary>
        public event EventHandler<PreparingDeviceSettingsEventArgs> PreparingDeviceSettings;

        // FIXME: Why does the GraphicsDeviceManager not know enough about the
        //        GraphicsDevice to raise these events without help?
        internal void OnDeviceDisposing(EventArgs e)
        {
            EventHelpers.Raise(this, DeviceDisposing, e);
        }

        // FIXME: Why does the GraphicsDeviceManager not know enough about the
        //        GraphicsDevice to raise these events without help?
        internal void OnDeviceResetting(EventArgs e)
        {
            EventHelpers.Raise(this, DeviceResetting, e);
        }

        // FIXME: Why does the GraphicsDeviceManager not know enough about the
        //        GraphicsDevice to raise these events without help?
        internal void OnDeviceReset(EventArgs e)
        {
            EventHelpers.Raise(this, DeviceReset, e);
        }

        // FIXME: Why does the GraphicsDeviceManager not know enough about the
        //        GraphicsDevice to raise these events without help?
        internal void OnDeviceCreated(EventArgs e)
        {
            EventHelpers.Raise(this, DeviceCreated, e);
        }

        #endregion

        #region IDisposable Members

        /// <inheritdoc cref="IDisposable.Dispose()"/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release managed resources; otherwise <see langword="false"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (_graphicsDevice != null)
                    {
                        _graphicsDevice.Dispose();
                        _graphicsDevice = null;
                    }
                }
                disposed = true;
            }
        }

        #endregion

        /// <summary>
        /// Applies the current graphics device settings.
        /// </summary>
        public void ApplyChanges()
        {
            // Calling ApplyChanges() before CreateDevice() should have no effect
            if (_graphicsDevice == null)
                return;

#if WINDOWS && DIRECTX

            _graphicsDevice.PresentationParameters.BackBufferFormat = _preferredBackBufferFormat;
            _graphicsDevice.PresentationParameters.BackBufferWidth = _preferredBackBufferWidth;
            _graphicsDevice.PresentationParameters.BackBufferHeight = _preferredBackBufferHeight;
            _graphicsDevice.PresentationParameters.DepthStencilFormat = _preferredDepthStencilFormat;
            _graphicsDevice.PresentationParameters.PresentationInterval = _synchronizedWithVerticalRetrace ? PresentInterval.Default : PresentInterval.Immediate;
            _graphicsDevice.PresentationParameters.IsFullScreen = _wantFullScreen;

            // TODO: We probably should be resetting the whole 
            // device if this changes as we are targeting a different
            // hardware feature level.
            _graphicsDevice.GraphicsProfile = GraphicsProfile;

            _graphicsDevice.PresentationParameters.DeviceWindowHandle = _game.Window.Handle;

            // Update the back buffer.
            _graphicsDevice.CreateSizeDependentResources();
            _graphicsDevice.ApplyRenderTargets(null);

            ((MonoGame.Framework.WinFormsGamePlatform)_game.Platform).ResetWindowBounds();

#elif DESKTOPGL
            var displayIndex = Sdl.Window.GetDisplayIndex (SdlGameWindow.Instance.Handle);
            var displayName = Sdl.Display.GetDisplayName (displayIndex);

            _graphicsDevice.PresentationParameters.BackBufferFormat = _preferredBackBufferFormat;
            _graphicsDevice.PresentationParameters.BackBufferWidth = _preferredBackBufferWidth;
            _graphicsDevice.PresentationParameters.BackBufferHeight = _preferredBackBufferHeight;
            _graphicsDevice.PresentationParameters.DepthStencilFormat = _preferredDepthStencilFormat;
            _graphicsDevice.PresentationParameters.PresentationInterval = _synchronizedWithVerticalRetrace ? PresentInterval.Default : PresentInterval.Immediate;
            _graphicsDevice.PresentationParameters.IsFullScreen = _wantFullScreen;

            //Set the swap interval based on if vsync is desired or not.
            //See GetSwapInterval for more details
            int swapInterval;
            if (_synchronizedWithVerticalRetrace)
                swapInterval = _graphicsDevice.PresentationParameters.PresentationInterval.GetSwapInterval();
            else
                swapInterval = 0;
            _graphicsDevice.Context.SwapInterval = swapInterval;

            _graphicsDevice.ApplyRenderTargets (null);

            _game.Platform.BeginScreenDeviceChange (GraphicsDevice.PresentationParameters.IsFullScreen);
            _game.Platform.EndScreenDeviceChange (displayName, GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);

#elif MONOMAC
            _graphicsDevice.PresentationParameters.IsFullScreen = _wantFullScreen;

            // TODO: Implement multisampling (aka anti-alising) for all platforms!

			_game.applyChanges(this);
#else

#if ANDROID
            // Trigger a change in orientation in case the supported orientations have changed
            ((AndroidGameWindow)_game.Window).SetOrientation(_game.Window.CurrentOrientation, false);
#endif
            // Ensure the presentation parameter orientation and buffer size matches the window
            _graphicsDevice.PresentationParameters.DisplayOrientation = _game.Window.CurrentOrientation;

            // Set the presentation parameters' actual buffer size to match the orientation
            bool isLandscape = (0 != (_game.Window.CurrentOrientation & (DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight)));
            int w = PreferredBackBufferWidth;
            int h = PreferredBackBufferHeight;

            _graphicsDevice.PresentationParameters.BackBufferWidth = isLandscape ? Math.Max(w, h) : Math.Min(w, h);
            _graphicsDevice.PresentationParameters.BackBufferHeight = isLandscape ? Math.Min(w, h) : Math.Max(w, h);

            ResetClientBounds();
#endif

            // Set the new display size on the touch panel.
            //
            // TODO: In XNA this seems to be done as part of the 
            // GraphicsDevice.DeviceReset event... we need to get 
            // those working.
            //
            TouchPanel.DisplayWidth = _graphicsDevice.PresentationParameters.BackBufferWidth;
            TouchPanel.DisplayHeight = _graphicsDevice.PresentationParameters.BackBufferHeight;

#if WINDOWS && DIRECTX

            if (!_firstLaunch)
            {
                if (IsFullScreen)
                {
                    _game.Platform.EnterFullScreen();
                }
                else
                {
                   _game.Platform.ExitFullScreen();
                }
            }
            _firstLaunch = false;
#endif
        }

        private void Initialize()
        {
            var presentationParameters = new PresentationParameters();
            presentationParameters.DepthStencilFormat = DepthFormat.Depth24;

#if WINDOWS && !DESKTOPGL
            _game.Window.SetSupportedOrientations(_supportedOrientations);

            presentationParameters.BackBufferFormat = _preferredBackBufferFormat;
            presentationParameters.BackBufferWidth = _preferredBackBufferWidth;
            presentationParameters.BackBufferHeight = _preferredBackBufferHeight;
            presentationParameters.DepthStencilFormat = _preferredDepthStencilFormat;
            presentationParameters.IsFullScreen = false;

            presentationParameters.DeviceWindowHandle = _game.Window.Handle;
#else

#if MONOMAC || DESKTOPGL
            presentationParameters.IsFullScreen = _wantFullScreen;
#elif WEB
            presentationParameters.IsFullScreen = false;
#else
            // Set "full screen"  as default
            presentationParameters.IsFullScreen = true;
#endif // MONOMAC

#endif // WINDOWS

            // TODO: Implement multisampling (aka anti-alising) for all platforms!
            var preparingDeviceSettingsHandler = PreparingDeviceSettings;

            if (preparingDeviceSettingsHandler != null)
            {
                GraphicsDeviceInformation gdi = new GraphicsDeviceInformation();
                gdi.GraphicsProfile = GraphicsProfile; // Microsoft defaults this to Reach.
                gdi.Adapter = GraphicsAdapter.DefaultAdapter;
                gdi.PresentationParameters = presentationParameters;
                PreparingDeviceSettingsEventArgs pe = new PreparingDeviceSettingsEventArgs(gdi);
                preparingDeviceSettingsHandler(this, pe);
                presentationParameters = pe.GraphicsDeviceInformation.PresentationParameters;
                GraphicsProfile = pe.GraphicsDeviceInformation.GraphicsProfile;
            }

            // Needs to be before ApplyChanges()
            _graphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile, this.PreferHalfPixelOffset, presentationParameters);

#if !MONOMAC
            ApplyChanges();
#endif

            // Set the new display size on the touch panel.
            //
            // TODO: In XNA this seems to be done as part of the 
            // GraphicsDevice.DeviceReset event... we need to get 
            // those working.
            //
            TouchPanel.DisplayWidth = _graphicsDevice.PresentationParameters.BackBufferWidth;
            TouchPanel.DisplayHeight = _graphicsDevice.PresentationParameters.BackBufferHeight;
            TouchPanel.DisplayOrientation = _graphicsDevice.PresentationParameters.DisplayOrientation;
        }

        /// <summary>
        /// Toggles fullscreen mode.
        /// </summary>
        public void ToggleFullScreen()
        {
            IsFullScreen = !IsFullScreen;

#if (WINDOWS && DIRECTX) || DESKTOPGL
            ApplyChanges();
#endif
        }

        /// <summary>
        /// Gets or sets the graphics profile used when creating the graphics device.
        /// </summary>
        public GraphicsProfile GraphicsProfile { get; set; }

        /// <summary>
        /// Gets the active graphics device.
        /// </summary>
        public GraphicsDevice GraphicsDevice
        {
            get
            {
                return _graphicsDevice;
            }
        }

        /// <summary>
        /// Gets or sets whether fullscreen mode is enabled.
        /// </summary>
        public bool IsFullScreen
        {
            get
            {
                if (_graphicsDevice != null)
                    return _graphicsDevice.PresentationParameters.IsFullScreen;
                return _wantFullScreen;
            }
            set
            {
#if WINDOWS && DIRECTX
                _wantFullScreen = value;
#else
                _wantFullScreen = value;
                if (_graphicsDevice != null)
                {
                    _graphicsDevice.PresentationParameters.IsFullScreen = value;
#if ANDROID
                    ForceSetFullScreen();
#endif
                }
#endif
            }
        }

#if ANDROID
        internal void ForceSetFullScreen()
        {
            if (IsFullScreen)
			{
                Game.Activity.Window.ClearFlags(global::Android.Views.WindowManagerFlags.ForceNotFullscreen);
                Game.Activity.Window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
			}
            else
                Game.Activity.Window.SetFlags(WindowManagerFlags.ForceNotFullscreen, WindowManagerFlags.ForceNotFullscreen);
        }
#endif
        
        /// <summary>
        /// Indicates if DX9 style pixel addressing or current standard
        /// pixel addressing should be used. This flag is set to
        /// <c>false</c> by default. It should be set to <c>true</c>
        /// for XNA compatibility. It is recommended to leave this flag
        /// set to <c>false</c> for projects that are not ported from
        /// XNA. This value is passed to <see cref="GraphicsDevice.UseHalfPixelOffset"/>.
        /// </summary>
        /// <remarks>
        /// XNA uses DirectX9 for its graphics. DirectX9 interprets UV
        /// coordinates differently from other graphics API's. This is
        /// typically referred to as the half-pixel offset. MonoGame
        /// replicates XNA behavior if this flag is set to <c>true</c>.
        /// </remarks>
        public bool PreferHalfPixelOffset
        {
            get { return _preferHalfPixelOffset; }
            set
            {
                if (this.GraphicsDevice != null)
                    throw new InvalidOperationException("Setting PreferHalfPixelOffset is not allowed after the creation of GraphicsDevice.");
                _preferHalfPixelOffset = value;
            }
        }

        /// <summary>
        /// Gets or sets the boolean which defines how window switches from windowed to fullscreen state.
        /// "Hard" mode(true) is slow to switch, but more effecient for performance, while "soft" mode(false) is vice versa.
        /// The default value is <c>true</c>.
        /// </summary>
        public bool HardwareModeSwitch
        {
            get { return _hardwareModeSwitch; }
            set
            {
                _hardwareModeSwitch = value;
            }
        }

        /// <summary>
        /// Gets or sets whether multisampling is preferred.
        /// </summary>
        public bool PreferMultiSampling
        {
            get
            {
                return _preferMultiSampling;
            }
            set
            {
                _preferMultiSampling = value;
            }
        }

        /// <summary>
        /// Gets or sets the preferred back-buffer surface format.
        /// </summary>
        public SurfaceFormat PreferredBackBufferFormat
        {
            get
            {
                return _preferredBackBufferFormat;
            }
            set
            {
                _preferredBackBufferFormat = value;
            }
        }

        /// <summary>
        /// Gets or sets the preferred back-buffer height.
        /// </summary>
        public int PreferredBackBufferHeight
        {
            get
            {
                return _preferredBackBufferHeight;
            }
            set
            {
                _preferredBackBufferHeight = value;
            }
        }

        /// <summary>
        /// Gets or sets the preferred back-buffer width.
        /// </summary>
        public int PreferredBackBufferWidth
        {
            get
            {
                return _preferredBackBufferWidth;
            }
            set
            {
                _preferredBackBufferWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets the preferred depth-stencil format.
        /// </summary>
        public DepthFormat PreferredDepthStencilFormat
        {
            get
            {
                return _preferredDepthStencilFormat;
            }
            set
            {
                _preferredDepthStencilFormat = value;
            }
        }

        /// <summary>
        /// Gets or sets whether vertical retrace synchronization is enabled.
        /// </summary>
        public bool SynchronizeWithVerticalRetrace
        {
            get
            {
                return _synchronizedWithVerticalRetrace;
            }
            set
            {
                _synchronizedWithVerticalRetrace = value;
            }
        }

        /// <summary>
        /// Gets or sets the supported display orientations.
        /// </summary>
        public DisplayOrientation SupportedOrientations
        {
            get
            {
                return _supportedOrientations;
            }
            set
            {
                _supportedOrientations = value;
                if (_game.Window != null)
                    _game.Window.SetSupportedOrientations(_supportedOrientations);
            }
        }

        /// <summary>
        /// This method is used by MonoGame Android to adjust the game's drawn to area to fill
        /// as much of the screen as possible whilst retaining the aspect ratio inferred from
        /// aspectRatio = (PreferredBackBufferWidth / PreferredBackBufferHeight)
        ///
        /// NOTE: this is a hack that should be removed if proper back buffer to screen scaling
        /// is implemented. To disable it's effect, in the game's constructor use:
        ///
        ///     graphics.IsFullScreen = true;
        ///     graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
        ///     graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
        ///
        /// </summary>
        internal void ResetClientBounds()
        {
#if ANDROID
            float preferredAspectRatio = (float)PreferredBackBufferWidth /
                                         (float)PreferredBackBufferHeight;
            float displayAspectRatio = (float)GraphicsDevice.DisplayMode.Width / 
                                       (float)GraphicsDevice.DisplayMode.Height;

            float adjustedAspectRatio = preferredAspectRatio;

            if ((preferredAspectRatio > 1.0f && displayAspectRatio < 1.0f) ||
                (preferredAspectRatio < 1.0f && displayAspectRatio > 1.0f))
            {
                // Invert preferred aspect ratio if it's orientation differs from the display mode orientation.
                // This occurs when user sets preferredBackBufferWidth/Height and also allows multiple supported orientations
                adjustedAspectRatio = 1.0f / preferredAspectRatio;
            }

            const float EPSILON = 0.00001f;
            var newClientBounds = new Rectangle();
            if (displayAspectRatio > (adjustedAspectRatio + EPSILON))
            {
                // Fill the entire height and reduce the width to keep aspect ratio
                newClientBounds.Height = _graphicsDevice.DisplayMode.Height;
                newClientBounds.Width = (int)(newClientBounds.Height * adjustedAspectRatio);
                newClientBounds.X = (_graphicsDevice.DisplayMode.Width - newClientBounds.Width) / 2;
            }
            else if (displayAspectRatio < (adjustedAspectRatio - EPSILON))
            {
                // Fill the entire width and reduce the height to keep aspect ratio
                newClientBounds.Width = _graphicsDevice.DisplayMode.Width;
                newClientBounds.Height = (int)(newClientBounds.Width / adjustedAspectRatio);
                newClientBounds.Y = (_graphicsDevice.DisplayMode.Height - newClientBounds.Height) / 2;
            }
            else
            {
                // Set the ClientBounds to match the DisplayMode
                newClientBounds.Width = GraphicsDevice.DisplayMode.Width;
                newClientBounds.Height = GraphicsDevice.DisplayMode.Height;
            }

            // Ensure buffer size is reported correctly
            _graphicsDevice.PresentationParameters.BackBufferWidth = newClientBounds.Width;
            _graphicsDevice.PresentationParameters.BackBufferHeight = newClientBounds.Height;

            // Set the veiwport so the (potentially) resized client bounds are drawn in the middle of the screen
            _graphicsDevice.Viewport = new Viewport(newClientBounds.X, -newClientBounds.Y, newClientBounds.Width, newClientBounds.Height);

            ((AndroidGameWindow)_game.Window).ChangeClientBounds(newClientBounds);

            // Touch panel needs latest buffer size for scaling
            TouchPanel.DisplayWidth = newClientBounds.Width;
            TouchPanel.DisplayHeight = newClientBounds.Height;

            global::Android.Util.Log.Debug("MonoGame", "GraphicsDeviceManager.ResetClientBounds: newClientBounds=" + newClientBounds.ToString());
#endif
        }

    }
}
