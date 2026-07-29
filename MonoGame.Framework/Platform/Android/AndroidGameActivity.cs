// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;

namespace Microsoft.Xna.Framework
{
    /// <summary>
    /// Android activity used to host a MonoGame game.
    /// </summary>
    public class AndroidGameActivity : Activity
    {
        internal Game Game { private get; set; }

        private ScreenReceiver screenReceiver;
        private OrientationListener _orientationListener;

        /// <summary>
        /// Controls whether media playback is paused and resumed automatically with activity lifecycle events.
        /// </summary>
        public bool AutoPauseAndResumeMediaPlayer = true;

        /// <summary>
        /// Controls whether rendering executes on the Android UI thread.
        /// </summary>
        public bool RenderOnUIThread = true; 

		/// <summary>
		/// OnCreate called when the activity is launched from cold or after the app
		/// has been killed due to a higher priority app needing the memory
		/// </summary>
		/// <param name='savedInstanceState'>
		/// Saved instance state.
		/// </param>
		protected override void OnCreate (Bundle savedInstanceState)
		{
            RequestWindowFeature(WindowFeatures.NoTitle);
            base.OnCreate(savedInstanceState);

			IntentFilter filter = new IntentFilter();
		    filter.AddAction(Intent.ActionScreenOff);
		    filter.AddAction(Intent.ActionScreenOn);
		    filter.AddAction(Intent.ActionUserPresent);
		    
		    screenReceiver = new ScreenReceiver();
		    RegisterReceiver(screenReceiver, filter);

            _orientationListener = new OrientationListener(this);

			Game.Activity = this;
		}

        /// <summary>
        /// Raised when the activity is paused.
        /// </summary>
        public static event EventHandler Paused;

        /// <inheritdoc />
        public override void OnConfigurationChanged (global::Android.Content.Res.Configuration newConfig)
		{
			// we need to refresh the viewport here.
			base.OnConfigurationChanged (newConfig);
		}

        /// <inheritdoc />
        protected override void OnPause()
        {
            base.OnPause();
            EventHelpers.Raise(this, Paused, EventArgs.Empty);

            if (_orientationListener.CanDetectOrientation())
                _orientationListener.Disable();
        }

        /// <summary>
        /// Raised when the activity is resumed.
        /// </summary>
        public static event EventHandler Resumed;

        /// <inheritdoc />
        protected override void OnResume()
        {
            base.OnResume();
            EventHelpers.Raise(this, Resumed, EventArgs.Empty);

            if (Game != null)
            {
                var deviceManager = (IGraphicsDeviceManager)Game.Services.GetService(typeof(IGraphicsDeviceManager));
                if (deviceManager == null)
                    return;
                ((GraphicsDeviceManager)deviceManager).ForceSetFullScreen();
                ((AndroidGameWindow)Game.Window).GameView.RequestFocus();
                if (_orientationListener.CanDetectOrientation())
                    _orientationListener.Enable();
            }
        }

        /// <inheritdoc />
		protected override void OnDestroy ()
		{
            UnregisterReceiver(screenReceiver);
            ScreenReceiver.ScreenLocked = false;
            _orientationListener = null;
            if (Game != null)
                Game.Dispose();
            Game = null;
			base.OnDestroy ();
		}
    }

	/// <summary>
	/// Extension helpers for Android activity metadata.
	/// </summary>
	public static class ActivityExtensions
    {
        /// <summary>
        /// Gets the <see cref="ActivityAttribute"/> applied to the activity type.
        /// </summary>
        /// <param name="obj">The activity instance.</param>
        /// <returns>The activity attribute when found; otherwise <see langword="null"/>.</returns>
        public static ActivityAttribute GetActivityAttribute(this AndroidGameActivity obj)
        {			
            var attr = obj.GetType().GetCustomAttributes(typeof(ActivityAttribute), true);
			if (attr != null)
			{
            	return ((ActivityAttribute)attr[0]);
			}
			return null;
        }
    }

}
