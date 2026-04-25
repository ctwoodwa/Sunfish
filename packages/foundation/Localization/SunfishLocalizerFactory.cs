using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Localization;

namespace Sunfish.Foundation.Localization;

/// <summary>
/// <see cref="IStringLocalizerFactory"/> that adds Debug-only hot-reload of
/// <c>.resx</c> files: in <c>DEBUG</c> builds, a <see cref="FileSystemWatcher"/>
/// invalidates the cached <see cref="IStringLocalizer"/> entries when any
/// resource file under the configured directory changes, so edits land in the
/// running UI within a few seconds without app restart. In <c>RELEASE</c> builds
/// the wrapper compiles to a thin pass-through with zero watcher overhead.
/// </summary>
/// <remarks>
/// Wraps an inner <see cref="IStringLocalizerFactory"/> (typically
/// <c>ResourceManagerStringLocalizerFactory</c>) and caches per-type localizers.
/// Cache invalidation is a single semaphore-guarded swap so concurrent calls
/// don't see a torn cache.
///
/// Per Plan 2 Task 4.1 — spec §3A 3-second hot-reload binding.
/// </remarks>
public sealed class SunfishLocalizerFactory : IStringLocalizerFactory, IDisposable
{
    private readonly IStringLocalizerFactory _inner;
    private readonly ConcurrentDictionary<CacheKey, IStringLocalizer> _cache = new();
    private readonly SemaphoreSlim _invalidationLock = new(1, 1);

#if DEBUG
    private readonly FileSystemWatcher? _watcher;
#endif

    /// <summary>
    /// Construct a new factory wrapping <paramref name="inner"/>. When
    /// <paramref name="resourcesRoot"/> is non-null and the build is
    /// <c>DEBUG</c>, a <see cref="FileSystemWatcher"/> watches it (recursively)
    /// for <c>*.resx</c> changes; each change clears the cache.
    /// </summary>
    public SunfishLocalizerFactory(IStringLocalizerFactory inner, string? resourcesRoot = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

#if DEBUG
        if (!string.IsNullOrEmpty(resourcesRoot) && Directory.Exists(resourcesRoot))
        {
            _watcher = new FileSystemWatcher(resourcesRoot, "*.resx")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnResourceFileEvent;
            _watcher.Created += OnResourceFileEvent;
            _watcher.Renamed += OnResourceFileEvent;
            _watcher.Deleted += OnResourceFileEvent;
        }
#endif
    }

    public IStringLocalizer Create(Type resourceSource)
    {
        if (resourceSource is null) throw new ArgumentNullException(nameof(resourceSource));
        var key = new CacheKey(resourceSource, BaseName: null, Location: null);
        return _cache.GetOrAdd(key, k => _inner.Create(k.Type!));
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        if (baseName is null) throw new ArgumentNullException(nameof(baseName));
        if (location is null) throw new ArgumentNullException(nameof(location));
        var key = new CacheKey(Type: null, baseName, location);
        return _cache.GetOrAdd(key, k => _inner.Create(k.BaseName!, k.Location!));
    }

    /// <summary>
    /// Drop every cached localizer. Next <see cref="Create(Type)"/> /
    /// <see cref="Create(string,string)"/> call re-asks the inner factory.
    /// Public so tests + extensions can drive invalidation deterministically.
    /// </summary>
    public void InvalidateAll()
    {
        // SemaphoreSlim guard ensures concurrent invalidations don't double-evict
        // mid-Create — Create's GetOrAdd remains correct under contention.
        _invalidationLock.Wait();
        try { _cache.Clear(); }
        finally { _invalidationLock.Release(); }
    }

    /// <summary>Invalidate just one type's cached localizer.</summary>
    public void Invalidate(Type resourceSource)
    {
        if (resourceSource is null) throw new ArgumentNullException(nameof(resourceSource));
        _invalidationLock.Wait();
        try
        {
            // Remove every entry whose Type matches; baseName/location entries are
            // not invalidated by type alone (callers use InvalidateAll for that).
            foreach (var key in _cache.Keys)
            {
                if (key.Type == resourceSource)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }
        finally { _invalidationLock.Release(); }
    }

#if DEBUG
    private void OnResourceFileEvent(object sender, FileSystemEventArgs e) => InvalidateAll();
#endif

    public void Dispose()
    {
#if DEBUG
        _watcher?.Dispose();
#endif
        _invalidationLock.Dispose();
    }

    /// <summary>Cache key — distinguishes the two <c>IStringLocalizerFactory.Create</c> overloads.</summary>
    private readonly record struct CacheKey(Type? Type, string? BaseName, string? Location);
}
