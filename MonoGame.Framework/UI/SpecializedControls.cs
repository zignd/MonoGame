// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Microsoft.Xna.Framework.UI
{
    /// <summary>Texture-backed presentation surface for a render target or externally rendered subviewport.</summary>
    public sealed class SubViewportContainer : Container, IDisposable
    {
        private int _stretchShrink = 1;
        private Texture2D _viewportTexture;
        private RenderTarget2D _hostRenderTarget;
        private bool _stretch;
        private bool _mouseTarget;
        private bool _hostPointerPressed;
        private ButtonState _hostRightButton;
        private ButtonState _hostMiddleButton;
        private ButtonState _hostXButton1;
        private ButtonState _hostXButton2;
        private int _hostScrollWheel;
        private KeyboardState _hostKeyboard;

        /// <summary>Creates a viewport presentation surface with Godot's click focus mode.</summary>
        public SubViewportContainer()
        {
            FocusMode = FocusMode.Click;
            ViewportContext = new UIContext();
        }
        /// <summary>Independent retained tree rendered into and receiving input through this viewport.</summary>
        public UIContext ViewportContext { get; }
        /// <summary>Color used to clear the hosted render target before drawing its independent UI tree.</summary>
        public Color ViewportClearColor { get; set; } = Color.Transparent;
        public Texture2D ViewportTexture { get => _viewportTexture; set { if (_viewportTexture == value) return; _viewportTexture = value; QueueLayout(); } }
        public bool Stretch { get => _stretch; set { if (_stretch == value) return; _stretch = value; QueueLayout(); } }
        /// <summary>Godot's positive integer render-resolution divisor used when stretching a viewport texture.</summary>
        public int StretchShrink { get => _stretchShrink; set { if (value < 1) throw new ArgumentOutOfRangeException(nameof(value)); _stretchShrink = value; } }
        /// <summary>Indicates that pointer input should be considered targeted at the embedded viewport.</summary>
        public bool MouseTarget { get => _mouseTarget; set => _mouseTarget = value; }
        public override Vector2 GetMinimumSize()
        {
            if (Stretch) return CustomMinimumSize;
            var textureSize = ViewportTexture == null ? Vector2.Zero : new Vector2(ViewportTexture.Width, ViewportTexture.Height);
            return Vector2.Max(CustomMinimumSize, textureSize);
        }
        /// <summary>Sets whether the viewport texture stretches to the container bounds.</summary>
        public void SetStretch(bool enable) => Stretch = enable;
        /// <summary>Returns whether viewport stretching is enabled.</summary>
        public bool IsStretchEnabled() => Stretch;
        /// <summary>Sets the positive integer render-resolution divisor.</summary>
        public void SetStretchShrink(int amount) => StretchShrink = amount;
        /// <summary>Returns the active render-resolution divisor.</summary>
        public int GetStretchShrink() => StretchShrink;
        /// <summary>Sets whether this container participates in embedded viewport mouse targeting.</summary>
        public void SetMouseTarget(bool enable) => MouseTarget = enable;
        /// <summary>Returns whether embedded viewport mouse targeting is enabled.</summary>
        public bool IsMouseTargetEnabled() => MouseTarget;
        /// <summary>Returns the render-target size required by the active stretch/shrink settings.</summary>
        public Vector2 GetViewportSize()
        {
            if (Stretch) return Vector2.Max(Vector2.One, Size / StretchShrink);
            if (ViewportContext.ViewportSize != Vector2.Zero) return ViewportContext.ViewportSize;
            return ViewportTexture == null ? Vector2.Zero : new Vector2(ViewportTexture.Width, ViewportTexture.Height);
        }
        /// <summary>Maps a global pointer position into the embedded viewport's coordinate space.</summary>
        public Vector2 MapPointerToViewport(Point position)
        {
            var local = new Vector2(position.X, position.Y) - GlobalPosition;
            return Stretch && StretchShrink > 1 ? local / StretchShrink : local;
        }
        internal override void PointerPressed(Point position)
        {
            base.PointerPressed(position);
            _hostPointerPressed = true;
        }
        internal override void PointerReleased(Point position, bool isInside) => _hostPointerPressed = false;
        internal override void PointerButtonPressed(Point position, PointerButton button)
        {
            base.PointerButtonPressed(position, button);
            SetHostPointerButton(button, ButtonState.Pressed);
        }
        internal override void PointerButtonReleased(Point position, PointerButton button)
        {
            base.PointerButtonReleased(position, button);
            SetHostPointerButton(button, ButtonState.Released);
        }
        internal override bool PointerWheel(int delta)
        {
            _hostScrollWheel += delta;
            return ViewportContext.Roots.Count > 0;
        }
        internal override void KeyPressed(Keys key)
        {
            _hostKeyboard = Context?.CurrentKeyboardState ?? new KeyboardState(key);
        }
        internal override void KeyReleased(Keys key)
        {
            _hostKeyboard = Context?.CurrentKeyboardState ?? new KeyboardState();
        }
        internal override void TextInput(char character) => ViewportContext.TextInput(character);
        internal override void FocusLost()
        {
            _hostKeyboard = new KeyboardState();
            base.FocusLost();
        }
        internal override void Process(GameTime gameTime)
        {
            var viewportSize = GetViewportSize();
            ViewportContext.ViewportSize = viewportSize;
            var mapped = MapPointerToViewport(Context?.PointerPosition ?? Point.Zero);
            var mouse = new MouseState(
                (int)MathF.Round(mapped.X),
                (int)MathF.Round(mapped.Y),
                _hostScrollWheel,
                _hostPointerPressed ? ButtonState.Pressed : ButtonState.Released,
                _hostMiddleButton,
                _hostRightButton,
                _hostXButton1,
                _hostXButton2);
            ViewportContext.Update(gameTime, mouse, _hostKeyboard);
            base.Process(gameTime);
        }
        internal override void Draw(UIRenderContext context)
        {
            var texture = ViewportTexture;
            if (ViewportContext.Roots.Count > 0)
            {
                var viewportSize = GetViewportSize();
                var width = Math.Max(1, (int)MathF.Round(viewportSize.X));
                var height = Math.Max(1, (int)MathF.Round(viewportSize.Y));
                EnsureHostRenderTarget(context.GraphicsDevice, width, height);
                ViewportContext.ViewportSize = new Vector2(width, height);
                context.RenderToTarget(_hostRenderTarget, ViewportClearColor, () => ViewportContext.Draw(context.GraphicsDevice));
                texture = _hostRenderTarget;
            }
            if (texture != null)
            {
                var destination = Bounds;
                if (!Stretch) { destination.Width = texture.Width; destination.Height = texture.Height; }
                context.SpriteBatch.Draw(texture, destination, Color.White);
            }
            base.Draw(context);
        }
        public void Dispose()
        {
            _hostRenderTarget?.Dispose();
            _hostRenderTarget = null;
            ViewportContext.Dispose();
        }
        private void EnsureHostRenderTarget(GraphicsDevice graphicsDevice, int width, int height)
        {
            if (_hostRenderTarget != null && !_hostRenderTarget.IsDisposed && _hostRenderTarget.GraphicsDevice == graphicsDevice && _hostRenderTarget.Width == width && _hostRenderTarget.Height == height) return;
            _hostRenderTarget?.Dispose();
            _hostRenderTarget = new RenderTarget2D(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        }
        private void SetHostPointerButton(PointerButton button, ButtonState state)
        {
            switch (button)
            {
                case PointerButton.Left: _hostPointerPressed = state == ButtonState.Pressed; break;
                case PointerButton.Right: _hostRightButton = state; break;
                case PointerButton.Middle: _hostMiddleButton = state; break;
                case PointerButton.XButton1: _hostXButton1 = state; break;
                case PointerButton.XButton2: _hostXButton2 = state; break;
            }
        }
    }

    /// <summary>On-screen analog joystick with normalized vector output and pointer capture.</summary>
    public sealed class VirtualJoystick : Control
    {
        private bool _active;
        private Vector2 _value;
        public VirtualJoystick() { FocusMode = FocusMode.All; CustomMinimumSize = new Vector2(80, 80); }
        public float DeadZone { get; set; } = .1f;
        public bool FixedCenter { get; set; } = true;
        public Color? BackgroundColor { get; set; }
        public Color? BorderColor { get; set; }
        public Color? KnobColor { get; set; }
        public Vector2 Value { get => _value; private set { if (_value == value) return; _value = value; ValueChanged?.Invoke(this, value); } }
        public bool IsPressed => _active;
        public event Action<VirtualJoystick, Vector2> ValueChanged;
        public event EventHandler Pressed;
        public event EventHandler Released;
        internal override void PointerPressed(Point position)
        {
            base.PointerPressed(position); _active = true; SetFromPoint(position); Pressed?.Invoke(this, EventArgs.Empty);
        }
        internal override void PointerMoved(Point position) { if (_active) SetFromPoint(position); }
        internal override void PointerReleased(Point position, bool isInside)
        {
            if (!_active) return;
            _active = false; Value = Vector2.Zero; Released?.Invoke(this, EventArgs.Empty);
        }
        private void SetFromPoint(Point point)
        {
            var center = GlobalPosition + Size / 2;
            var radius = Math.Max(1, Math.Min(Size.X, Size.Y) / 2);
            var result = (new Vector2(point.X, point.Y) - center) / radius;
            if (result.LengthSquared() > 1) result.Normalize();
            Value = result.Length() < DeadZone ? Vector2.Zero : result;
        }
        internal override void Draw(UIRenderContext context)
        {
            var rect = Bounds;
            context.Fill(rect, new Color(BackgroundColor ?? context.Theme.PanelColor, 150)); context.Border(rect, BorderColor ?? context.Theme.PanelBorderColor);
            var radius = Math.Max(4, Math.Min(rect.Width, rect.Height) / 5);
            var center = new Vector2(rect.Center.X, rect.Center.Y) + Value * (Math.Min(rect.Width, rect.Height) / 2 - radius);
            context.Fill(new Rectangle((int)center.X - radius, (int)center.Y - radius, radius * 2, radius * 2), KnobColor ?? context.Theme.AccentColor);
            base.Draw(context);
        }
    }

    /// <summary>MonoGame VideoPlayer-backed UI control for video assets loaded through Content.</summary>
    public sealed class VideoStreamPlayer : Control, IDisposable
    {
        private VideoPlayer _player;
        private bool _backendUnavailable;
        private Video _stream;
        private bool _autoplay;
        private bool _loop;
        private bool _paused;
        private bool _expand;
        private bool _playRequested;
        private float _volume = 1;
        private float _speedScale = 1;
        private int _bufferingMsec = 500;
        private int _audioTrack;
        private string _bus = "Master";

        public Video Stream { get => _stream; set { if (_stream == value) return; Stop(); _stream = value; QueueLayout(); if (Autoplay && value != null) Play(); } }
        public bool Autoplay { get => _autoplay; set => _autoplay = value; }
        public bool Loop { get => _loop; set { _loop = value; if (_player != null) _player.IsLooped = value; } }
        public bool Paused { get => _paused; set { if (_paused == value) return; _paused = value; if (_stream == null || _player == null) return; if (value) _player.Pause(); else _player.Resume(); } }
        public float Volume { get => _volume; set { _volume = value; if (_player != null) _player.Volume = MathHelper.Clamp(value, 0, 1); } }
        public float SpeedScale { get => _speedScale; set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); _speedScale = value; } }
        public bool Expand { get => _expand; set { if (_expand == value) return; _expand = value; QueueLayout(); } }
        public int BufferingMsec { get => _bufferingMsec; set => _bufferingMsec = value; }
        public int AudioTrack { get => _audioTrack; set => _audioTrack = value; }
        public string Bus { get => _bus; set => _bus = string.IsNullOrEmpty(value) ? "Master" : value; }
        public MediaState PlaybackState => _player?.State ?? MediaState.Stopped;
        public event EventHandler Finished;

        public override Vector2 GetMinimumSize()
        {
            if (Expand || Stream == null) return CustomMinimumSize;
            return Vector2.Max(CustomMinimumSize, new Vector2(Stream.Width, Stream.Height));
        }
        public void Play()
        {
            if (_stream == null || !EnsureBackend()) return;
            _player.Play(_stream);
            if (Paused) _player.Pause();
            _playRequested = true;
        }
        public void Stop() { _player?.Stop(); _playRequested = false; }
        public bool IsPlaying() => _player?.State == MediaState.Playing;
        public void SetStream(Video stream) => Stream = stream;
        public Video GetStream() => Stream;
        public void SetPaused(bool paused) => Paused = paused;
        public bool IsPaused() => Paused;
        public void SetLoop(bool loop) => Loop = loop;
        public bool HasLoop() => Loop;
        public void SetVolume(float volume) => Volume = volume;
        public float GetVolume() => Volume;
        public void SetVolumeDb(float db) => Volume = db < -79 ? 0 : MathF.Pow(10, db / 20);
        public float GetVolumeDb() => Volume == 0 ? -80 : 20 * MathF.Log10(Volume);
        public void SetSpeedScale(float speedScale) => SpeedScale = speedScale;
        public float GetSpeedScale() => SpeedScale;
        public string GetStreamName() => Stream?.FileName ?? "<No Stream>";
        public double GetStreamLength() => Stream?.Duration.TotalSeconds ?? 0;
        public double GetStreamPosition() => _player?.PlayPosition.TotalSeconds ?? 0;
        public void SetStreamPosition(double position)
        {
            if (_player == null) return;
            _player.SetPlayPosition(TimeSpan.FromSeconds(Math.Max(0, position)));
        }
        public void SetAutoplay(bool enabled) => Autoplay = enabled;
        public bool HasAutoplay() => Autoplay;
        public void SetExpand(bool enable) => Expand = enable;
        public bool HasExpand() => Expand;
        public void SetAudioTrack(int track) => AudioTrack = track;
        public int GetAudioTrack() => AudioTrack;
        public void SetBufferingMsec(int msec) => BufferingMsec = msec;
        public int GetBufferingMsec() => BufferingMsec;
        public void SetBus(string bus) => Bus = bus;
        public string GetBus() => Bus;
        public Texture2D GetVideoTexture()
        {
            if (Stream == null || _player == null || _player.State == MediaState.Stopped) return null;
            try { return _player.GetTexture(); }
            catch (InvalidOperationException) { return null; }
        }
        public void Dispose() => _player?.Dispose();
        internal override void Process(GameTime gameTime)
        {
            if (_playRequested && PlaybackState == MediaState.Stopped && !Loop)
            {
                _playRequested = false;
                Finished?.Invoke(this, EventArgs.Empty);
            }
            base.Process(gameTime);
        }
        internal override void Draw(UIRenderContext context)
        {
            var texture = GetVideoTexture();
            if (texture != null)
            {
                var destination = Bounds;
                if (!Expand) { destination.Width = texture.Width; destination.Height = texture.Height; }
                context.SpriteBatch.Draw(texture, destination, Color.White);
            }
            base.Draw(context);
        }
        private bool EnsureBackend()
        {
            if (_player != null) return true;
            if (_backendUnavailable) return false;
            try
            {
                _player = new VideoPlayer { IsLooped = Loop, Volume = MathHelper.Clamp(Volume, 0, 1) };
                return true;
            }
            catch (NotImplementedException)
            {
                _backendUnavailable = true;
                return false;
            }
            catch (PlatformNotSupportedException)
            {
                _backendUnavailable = true;
                return false;
            }
        }
    }

    /// <summary>Transforms a rich-text fragment before it is appended to a <see cref="RichTextLabel"/>.</summary>
    public abstract class RichTextEffect
    {
        public abstract string Process(string source);
    }

    /// <summary>BBCode-oriented text model with an extensible effect pipeline.</summary>
    public sealed class RichTextDocument : RichTextLabel
    {
        private readonly List<RichTextEffect> _effects = new List<RichTextEffect>();
        public IReadOnlyList<RichTextEffect> Effects => _effects;
        public void InstallEffect(RichTextEffect effect)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            _effects.Add(effect);
        }
        public override void AppendBbcode(string bbcode)
        {
            var text = bbcode ?? string.Empty;
            foreach (var effect in _effects) text = effect.Process(text) ?? string.Empty;
            base.AppendBbcode(text);
        }
        public void ClearEffects() => _effects.Clear();
    }
}
