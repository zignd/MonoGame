// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Xna.Framework.Graphics;

internal readonly struct HotReloadCompilation
{
    public byte[] EffectCode { get; }
    public bool CacheHit { get; }

    public HotReloadCompilation(byte[] effectCode, bool cacheHit)
    {
        EffectCode = effectCode ?? throw new ArgumentNullException(nameof(effectCode));
        CacheHit = cacheHit;
    }
}

internal readonly struct HotReloadStatus
{
    public bool Succeeded { get; }
    public bool CacheHit { get; }
    public TimeSpan Elapsed { get; }
    public string Message { get; }

    public HotReloadStatus(bool succeeded, bool cacheHit, TimeSpan elapsed, string message)
    {
        Succeeded = succeeded;
        CacheHit = cacheHit;
        Elapsed = elapsed;
        Message = message;
    }
}

internal sealed class HotReloadService<TResource> : IDisposable where TResource : class, IDisposable
{
    private readonly Func<CancellationToken, Task<HotReloadCompilation>> _compileAsync;
    private readonly Func<byte[], TResource> _createResource;
    private readonly ConcurrentQueue<CompileResult> _completed = new();
    private readonly Queue<RetiredResource> _retired = new();
    private readonly TimeSpan _debounce;
    private readonly int _retirementFrames;
    private readonly int _renderThreadId;
    private readonly FileSystemWatcher _watcher;
    private CancellationTokenSource _compilationCancellation = new();
    private bool _disposed;

    public TResource Current { get; private set; }
    public HotReloadStatus LastStatus { get; private set; }
    public bool IsReloading { get; private set; }
    public int SuccessfulReloadCount { get; private set; }
    public int FailedReloadCount { get; private set; }

    public HotReloadService(
        TResource initialResource,
        string watchedFile,
        Func<CancellationToken, Task<HotReloadCompilation>> compileAsync,
        Func<byte[], TResource> createResource,
        TimeSpan? debounce = null,
        int retirementFrames = 3)
    {
        if (retirementFrames < 0)
            throw new ArgumentOutOfRangeException(nameof(retirementFrames));

        Current = initialResource ?? throw new ArgumentNullException(nameof(initialResource));
        _compileAsync = compileAsync ?? throw new ArgumentNullException(nameof(compileAsync));
        _createResource = createResource ?? throw new ArgumentNullException(nameof(createResource));
        _debounce = debounce ?? TimeSpan.FromMilliseconds(150);
        _retirementFrames = retirementFrames;
        _renderThreadId = Environment.CurrentManagedThreadId;

        var fullPath = Path.GetFullPath(watchedFile);
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(fullPath)!, Path.GetFileName(fullPath))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnRenamed;
    }

    public void NotifyChanged()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HotReloadService<TResource>));
        var replacement = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _compilationCancellation, replacement);
        previous.Cancel();
        previous.Dispose();
        var cancellationToken = replacement.Token;
        IsReloading = true;
        _ = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await Task.Delay(_debounce, cancellationToken).ConfigureAwait(false);
                var compilation = await _compileAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                _completed.Enqueue(CompileResult.Success(compilation, stopwatch.Elapsed));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _completed.Enqueue(CompileResult.Failure(exception, stopwatch.Elapsed));
            }
        }, cancellationToken);
    }

    public void Update()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HotReloadService<TResource>));
        if (Environment.CurrentManagedThreadId != _renderThreadId)
            throw new InvalidOperationException("Hot-reload resources must be created and swapped on the render thread.");

        CompileResult latest = default;
        var hasResult = false;
        while (_completed.TryDequeue(out var result))
        {
            latest = result;
            hasResult = true;
        }

        if (hasResult)
        {
            IsReloading = false;
            if (latest.Error != null)
            {
                LastStatus = new HotReloadStatus(false, false, latest.Elapsed, latest.Error.Message);
                FailedReloadCount++;
            }
            else
            {
                try
                {
                    var replacement = _createResource(latest.Compilation.EffectCode);
                    var previous = Current;
                    Current = replacement;
                    _retired.Enqueue(new RetiredResource(previous, _retirementFrames));
                    LastStatus = new HotReloadStatus(true, latest.Compilation.CacheHit, latest.Elapsed, "Reloaded successfully.");
                    SuccessfulReloadCount++;
                }
                catch (Exception exception)
                {
                    LastStatus = new HotReloadStatus(false, latest.Compilation.CacheHit, latest.Elapsed, exception.Message);
                    FailedReloadCount++;
                }
            }
        }

        for (var count = _retired.Count; count > 0; count--)
        {
            var retired = _retired.Dequeue();
            if (retired.FramesRemaining <= 0)
                retired.Resource.Dispose();
            else
                _retired.Enqueue(new RetiredResource(retired.Resource, retired.FramesRemaining - 1));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _watcher.Dispose();
        _compilationCancellation.Cancel();
        _compilationCancellation.Dispose();
        Current.Dispose();
        while (_retired.Count > 0)
            _retired.Dequeue().Resource.Dispose();
    }

    private void OnChanged(object sender, FileSystemEventArgs args)
    {
        if (!_disposed)
            NotifyChanged();
    }

    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        if (!_disposed)
            NotifyChanged();
    }

    private readonly struct RetiredResource
    {
        public TResource Resource { get; }
        public int FramesRemaining { get; }

        public RetiredResource(TResource resource, int framesRemaining)
        {
            Resource = resource;
            FramesRemaining = framesRemaining;
        }
    }

    private readonly struct CompileResult
    {
        public HotReloadCompilation Compilation { get; }
        public Exception Error { get; }
        public TimeSpan Elapsed { get; }

        private CompileResult(HotReloadCompilation compilation, Exception error, TimeSpan elapsed)
        {
            Compilation = compilation;
            Error = error;
            Elapsed = elapsed;
        }

        public static CompileResult Success(HotReloadCompilation compilation, TimeSpan elapsed)
            => new(compilation, null, elapsed);

        public static CompileResult Failure(Exception error, TimeSpan elapsed)
            => new(default, error, elapsed);
    }
}

internal sealed class EffectHotReloadService : IDisposable
{
    private readonly HotReloadService<Effect> _service;

    public Effect Current => _service.Current;
    public HotReloadStatus LastStatus => _service.LastStatus;
    public bool IsReloading => _service.IsReloading;
    public int SuccessfulReloadCount => _service.SuccessfulReloadCount;
    public int FailedReloadCount => _service.FailedReloadCount;

    public EffectHotReloadService(
        GraphicsDevice graphicsDevice,
        Effect initialEffect,
        string watchedFile,
        Func<CancellationToken, Task<HotReloadCompilation>> compileAsync,
        TimeSpan? debounce = null,
        int retirementFrames = 3)
    {
        if (graphicsDevice == null)
            throw new ArgumentNullException(nameof(graphicsDevice));
        _service = new HotReloadService<Effect>(
            initialEffect,
            watchedFile,
            compileAsync,
            effectCode => new Effect(graphicsDevice, effectCode),
            debounce,
            retirementFrames);
    }

    public void NotifyChanged() => _service.NotifyChanged();
    public void Update() => _service.Update();
    public void Dispose() => _service.Dispose();
}