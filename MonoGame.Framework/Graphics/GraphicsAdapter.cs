// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.ObjectModel;

namespace Microsoft.Xna.Framework.Graphics
{
    /// <summary>
    /// Represents a graphics adapter that can create and present a graphics device.
    /// </summary>
    public sealed partial class GraphicsAdapter : IDisposable
    {
        /// <summary>
        /// Defines the driver type for graphics adapter. Usable only on DirectX platforms for now.
        /// </summary>
        public enum DriverType
        {
            /// <summary>
            /// Hardware device been used for rendering. Maximum speed and performance.
            /// </summary>
            Hardware,
            /// <summary>
            /// Emulates the hardware device on CPU. Slowly, only for testing.
            /// </summary>
            Reference,
            /// <summary>
            /// Useful when <see cref="DriverType.Hardware"/> acceleration does not work.
            /// </summary>
            FastSoftware
        }

        // This is nulled from GamePlatform on destruction.
        internal static ReadOnlyCollection<GraphicsAdapter> _adapters;

        private DisplayModeCollection _supportedDisplayModes;

        private DisplayMode _currentDisplayMode;

        private static void Initialize()
        {
            if (_adapters != null)
                return;

            // NOTE: An adapter is a monitor+device combination, so we expect
            // at lease one adapter per connected monitor.
            PlatformInitializeAdapters(out _adapters);

            if (_adapters.Count == 0)
            {
                throw new NoSuitableGraphicsDeviceException(
                    "No graphics adapters were found!");
            }

            // The first adapter is considered the default.
            _adapters[0].IsDefaultAdapter = true;
        }

        /// <summary>
        /// Gets the default graphics adapter.
        /// </summary>
        public static GraphicsAdapter DefaultAdapter
        {
            get
            {
                Initialize();
                return _adapters[0];
            }
        }

        /// <summary>
        /// Gets the available graphics adapters.
        /// </summary>
        public static ReadOnlyCollection<GraphicsAdapter> Adapters
        {
            get
            {
                Initialize();
                return _adapters;
            }
        }

        /// <summary>
        /// Used to request creation of the reference graphics device,
        /// or the default hardware accelerated device (when set to false).
        /// </summary>
        /// <remarks>
        /// This only works on DirectX platforms where a reference graphics
        /// device is available and must be defined before the graphics device
        /// is created. It defaults to false.
        /// </remarks>
        public static bool UseReferenceDevice
        {
            get { return UseDriverType == DriverType.Reference; }
            set { UseDriverType = value ? DriverType.Reference : DriverType.Hardware; }
        }

        /// <summary>
        /// Used to request creation of a specific kind of driver.
        /// </summary>
        /// <remarks>
        /// These values only work on DirectX platforms and must be defined before the graphics device
        /// is created. <see cref="DriverType.Hardware"/> by default.
        /// </remarks>
        public static DriverType UseDriverType { get; set; }

        /// <summary>
        /// Used to request the graphics device should be created with debugging
        /// features enabled.
        /// </summary>
        public static bool UseDebugLayers { get; set; }

        /// <summary>
        /// Gets the adapter description.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Gets the adapter device identifier.
        /// </summary>
        public int DeviceId { get; private set; }

        /// <summary>
        /// Gets the adapter device name.
        /// </summary>
        public string DeviceName { get; private set; }

        /// <summary>
        /// Gets the adapter vendor identifier.
        /// </summary>
        public int VendorId { get; private set; }

        /// <summary>
        /// Gets whether this adapter is the default adapter.
        /// </summary>
        public bool IsDefaultAdapter { get; private set; }

        /// <summary>
        /// Gets the native monitor handle associated with the adapter.
        /// </summary>
        public IntPtr MonitorHandle { get; private set; }

        /// <summary>
        /// Gets the adapter revision.
        /// </summary>
        public int Revision { get; private set; }

        /// <summary>
        /// Gets the adapter subsystem identifier.
        /// </summary>
        public int SubSystemId { get; private set; }

        /// <summary>
        /// Gets the display modes supported by this adapter.
        /// </summary>
        public DisplayModeCollection SupportedDisplayModes
        {
            get { return _supportedDisplayModes; }
        }

        /// <summary>
        /// Gets the current display mode for this adapter.
        /// </summary>
        public DisplayMode CurrentDisplayMode
        {
            get { return _currentDisplayMode; }
        }

        /// <summary>
        /// Returns true if the <see cref="GraphicsAdapter.CurrentDisplayMode"/> is widescreen.
        /// </summary>
        /// <remarks>
        /// Common widescreen modes include 16:9, 16:10 and 2:1.
        /// </remarks>
        public bool IsWideScreen
        {
            get
            {
                // Seems like XNA treats aspect ratios above 16:10 as wide screen.
                const float minWideScreenAspect = 16.0f / 10.0f;
                return CurrentDisplayMode.AspectRatio >= minWideScreenAspect;
            }
        }

        /// <summary>
        /// Queries for support of the requested render target format on the adaptor.
        /// </summary>
        /// <param name="graphicsProfile">The graphics profile.</param>
        /// <param name="format">The requested surface format.</param>
        /// <param name="depthFormat">The requested depth stencil format.</param>
        /// <param name="multiSampleCount">The requested multisample count.</param>
        /// <param name="selectedFormat">Set to the best format supported by the adaptor for the requested surface format.</param>
        /// <param name="selectedDepthFormat">Set to the best format supported by the adaptor for the requested depth stencil format.</param>
        /// <param name="selectedMultiSampleCount">Set to the best count supported by the adaptor for the requested multisample count.</param>
        /// <returns>True if the requested format is supported by the adaptor. False if one or more of the values was changed.</returns>
		public bool QueryRenderTargetFormat(
			GraphicsProfile graphicsProfile,
			SurfaceFormat format,
			DepthFormat depthFormat,
			int multiSampleCount,
			out SurfaceFormat selectedFormat,
			out DepthFormat selectedDepthFormat,
			out int selectedMultiSampleCount)
		{
            selectedFormat = format;
            selectedDepthFormat = depthFormat;
            selectedMultiSampleCount = multiSampleCount;

            // fallback for unsupported renderTarget surface formats.
            if (selectedFormat == SurfaceFormat.Alpha8 ||
                selectedFormat == SurfaceFormat.NormalizedByte2 ||
                selectedFormat == SurfaceFormat.NormalizedByte4 ||
                selectedFormat == SurfaceFormat.Dxt1 ||
                selectedFormat == SurfaceFormat.Dxt3 ||
                selectedFormat == SurfaceFormat.Dxt5 ||
                selectedFormat == SurfaceFormat.Dxt1a ||
                selectedFormat == SurfaceFormat.Dxt1SRgb ||
                selectedFormat == SurfaceFormat.Dxt3SRgb ||
                selectedFormat == SurfaceFormat.Dxt5SRgb)
                selectedFormat = SurfaceFormat.Color;

            return (format == selectedFormat) && (depthFormat == selectedDepthFormat) && (multiSampleCount == selectedMultiSampleCount);
		}

        /// <summary>
        /// Returns whether this adapter supports the requested graphics profile.
        /// </summary>
        /// <param name="graphicsProfile">The graphics profile to query.</param>
        /// <returns><c>true</c> if the profile is supported; otherwise, <c>false</c>.</returns>
        public bool IsProfileSupported(GraphicsProfile graphicsProfile)
        {
            return PlatformIsProfileSupported(graphicsProfile);
        }

        /// <summary>
        /// Releases resources used by this adapter.
        /// </summary>
        public void Dispose()
        {
            // We don't keep any resources, so we have
            // nothing to do... just here for XNA compatibility.
        }
    }
}
