using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.UI;
using MonoGame.Framework.Utilities;

namespace MonoGame.UI.Catalog;

public sealed class CatalogGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly UIContext _ui;
    private CatalogShell _catalog;
    private SpriteFont _font;
    private SpriteFont _displayFont;
    private Texture2D _catalogTexture;
    private readonly CatalogMetricsOptions _metricsOptions;
    private readonly VertexPositionColorTexture[] _hotReloadVertices =
    {
        new(new Vector3(0.82f, -0.92f, 0), Color.White, new Vector2(0, 1)),
        new(new Vector3(0.9f, -0.72f, 0), Color.White, new Vector2(0.5f, 0)),
        new(new Vector3(0.98f, -0.92f, 0), Color.White, new Vector2(1, 1)),
    };
    private EffectHotReloadService _hotReload;
    private int _renderedFrames;

    public CatalogGame(CatalogMetricsOptions metricsOptions = null)
    {
        _metricsOptions = metricsOptions;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1440,
            PreferredBackBufferHeight = 900,
        };
        _ui = new UIContext { Theme = CreateTheme() };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "MonoGame UI Catalog";
        Window.TextInput += OnTextInput;
    }

    protected override void LoadContent()
    {
        _font = Content.Load<SpriteFont>("Fonts/Catalog");
        _displayFont = Content.Load<SpriteFont>("Fonts/Catalog@2x");
        _catalogTexture = CreateCatalogTexture();
        Window.Title = $"MonoGame UI Catalog - {PlatformInfo.GraphicsBackend}";
        _ui.TooltipFont = _font;
        _ui.DisplayFontResolver = (font, scale) => ReferenceEquals(font, _font) && scale > 1f ? _displayFont : null;
        _catalog = new CatalogShell(StoryCatalog.Create(_catalogTexture), _font);
        _ui.Add(_catalog);
        if (_metricsOptions?.WatchedEffectPath != null)
        {
            var watchedEffectPath = Path.GetFullPath(_metricsOptions.WatchedEffectPath);
            _hotReload = new EffectHotReloadService(
                GraphicsDevice,
                new Effect(GraphicsDevice, File.ReadAllBytes(watchedEffectPath)),
                watchedEffectPath,
                async cancellationToken => new HotReloadCompilation(
                    await File.ReadAllBytesAsync(watchedEffectPath, cancellationToken).ConfigureAwait(false),
                    cacheHit: false));
            _hotReload.NotifyChanged();
        }
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        var viewport = GraphicsDevice.Viewport;
        var displayScale = GetDisplayScale(viewport);
        _ui.DisplayScale = displayScale;
        _ui.ViewportSize = new Vector2(viewport.Width / displayScale, viewport.Height / displayScale);
        if (_catalog != null) _catalog.Size = _ui.ViewportSize;
        _ui.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_ui.Theme.BackgroundColor);
        _ui.Draw(GraphicsDevice);
        if (_hotReload != null)
        {
            _hotReload.Update();
            var effect = _hotReload.Current;
            effect.Parameters["MatrixTransform"]?.SetValue(Matrix.Identity);
            effect.Parameters["Texture"]?.SetValue(_catalogTexture);
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _hotReloadVertices, 0, 1);
            }
        }
        base.Draw(gameTime);

        if (_metricsOptions != null && ++_renderedFrames >= _metricsOptions.FrameCount)
        {
            WriteMetrics();
            Exit();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Window.TextInput -= OnTextInput;
            _hotReload?.Dispose();
            _ui.Dispose();
            _catalogTexture?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void OnTextInput(object sender, TextInputEventArgs args) => _ui.TextInput(args.Character);

    private float GetDisplayScale(Viewport viewport)
    {
        var clientBounds = Window.ClientBounds;
        if (clientBounds.Width <= 0 || viewport.Width <= 0) return 1f;
        var scale = viewport.Width / (float)clientBounds.Width;
        return float.IsFinite(scale) && scale > 0 ? scale : 1f;
    }

    private void WriteMetrics()
    {
        var diagnostics = GraphicsDevice.GetShaderPipelineDiagnostics();
        var report = new
        {
            schemaVersion = 1,
            capturedAtUtc = DateTimeOffset.UtcNow,
            hostOperatingSystem = Environment.OSVersion.ToString(),
            hostArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            backend = PlatformInfo.GraphicsBackend.ToString(),
            shaderBuildMode = diagnostics.BuildMode,
            renderedFrames = _renderedFrames,
            shaderCreationCount = diagnostics.ShaderCreationCount,
            shaderCreationMilliseconds = diagnostics.ShaderCreationMilliseconds,
            pipelineCacheHits = diagnostics.PipelineCacheHits,
            pipelineCacheMisses = diagnostics.PipelineCacheMisses,
            pipelineCreationCount = diagnostics.PipelineCreationCount,
            pipelineCreationMilliseconds = diagnostics.PipelineCreationMilliseconds,
            runtimeTranslationCount = diagnostics.RuntimeTranslationCount,
            runtimeTranslationMilliseconds = diagnostics.RuntimeTranslationMilliseconds,
            hotReloadEnabled = _hotReload != null,
            hotReloadCreationMode = _hotReload == null ? null : "RenderThreadSynchronous",
            hotReloadSucceeded = _hotReload?.LastStatus.Succeeded,
            hotReloadCacheHit = _hotReload?.LastStatus.CacheHit,
            hotReloadMilliseconds = _hotReload?.LastStatus.Elapsed.TotalMilliseconds,
            hotReloadMessage = _hotReload?.LastStatus.Message,
        };
        var outputPath = Path.GetFullPath(_metricsOptions.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
    }

    private Texture2D CreateCatalogTexture()
    {
        const int size = 48;
        var texture = new Texture2D(GraphicsDevice, size, size);
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var checker = (x / 8 + y / 8) % 2 == 0;
            pixels[y * size + x] = checker ? new Color(48, 185, 164) : new Color(246, 185, 73);
        }
        texture.SetData(pixels);
        return texture;
    }

    private static Theme CreateTheme() => new Theme
    {
        BackgroundColor = new Color(20, 24, 31),
        PanelColor = new Color(29, 35, 45),
        PanelBorderColor = new Color(56, 66, 82),
        TextColor = new Color(235, 239, 246),
        DisabledTextColor = new Color(143, 153, 170),
        AccentColor = new Color(48, 185, 164),
        HoverColor = new Color(43, 52, 66),
        PressedColor = new Color(23, 28, 36),
        FocusColor = new Color(246, 185, 73),
    };
}

public sealed class CatalogMetricsOptions
{
    public string OutputPath { get; }
    public int FrameCount { get; }
    public string WatchedEffectPath { get; }

    private CatalogMetricsOptions(string outputPath, int frameCount, string watchedEffectPath)
    {
        OutputPath = outputPath;
        FrameCount = frameCount;
        WatchedEffectPath = watchedEffectPath;
    }

    public static CatalogMetricsOptions Parse(string[] args)
    {
        string outputPath = null;
        string watchedEffectPath = null;
        var frameCount = 120;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--metrics" when index + 1 < args.Length:
                    outputPath = args[++index];
                    break;
                case "--frames" when index + 1 < args.Length && int.TryParse(args[++index], out frameCount) && frameCount > 0:
                    break;
                case "--watch-effect" when index + 1 < args.Length:
                    watchedEffectPath = args[++index];
                    break;
                default:
                    throw new ArgumentException($"Unknown or invalid catalog argument: {args[index]}");
            }
        }

        if (watchedEffectPath != null && !File.Exists(watchedEffectPath))
            throw new ArgumentException($"The watched effect does not exist: {watchedEffectPath}");
        return outputPath == null && watchedEffectPath == null ? null : new CatalogMetricsOptions(outputPath, frameCount, watchedEffectPath);
    }
}