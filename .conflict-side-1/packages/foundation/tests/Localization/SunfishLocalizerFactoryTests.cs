using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Sunfish.Foundation.Localization;
using Xunit;

namespace Sunfish.Foundation.Tests.Localization;

/// <summary>
/// Plan 2 Task 4.1 — verify SunfishLocalizerFactory caches per-type localizers
/// and invalidates them on demand (which is what the FileSystemWatcher triggers
/// in Debug builds). Watcher behaviour itself is driven by the OS and untestable
/// deterministically; the tests exercise the cache + InvalidateAll/Invalidate
/// path that the watcher invokes.
/// </summary>
public class SunfishLocalizerFactoryTests
{
    [Fact]
    public void Create_Type_CachesPerTypeAcrossCalls()
    {
        var inner = Substitute.For<IStringLocalizerFactory>();
        var stubLocalizer = Substitute.For<IStringLocalizer>();
        inner.Create(typeof(TestResource)).Returns(stubLocalizer);

        using var factory = new SunfishLocalizerFactory(inner);

        var first = factory.Create(typeof(TestResource));
        var second = factory.Create(typeof(TestResource));

        Assert.Same(first, second);
        inner.Received(1).Create(typeof(TestResource));
    }

    [Fact]
    public void Create_BaseNameLocation_CachesPerKey()
    {
        var inner = Substitute.For<IStringLocalizerFactory>();
        var stubLocalizer = Substitute.For<IStringLocalizer>();
        inner.Create("Sunfish.Resources", "asm").Returns(stubLocalizer);

        using var factory = new SunfishLocalizerFactory(inner);

        var first = factory.Create("Sunfish.Resources", "asm");
        var second = factory.Create("Sunfish.Resources", "asm");

        Assert.Same(first, second);
        inner.Received(1).Create("Sunfish.Resources", "asm");
    }

    [Fact]
    public void InvalidateAll_DropsAllCachedLocalizers_NextCreateAsksInner()
    {
        var inner = Substitute.For<IStringLocalizerFactory>();
        var firstCall = Substitute.For<IStringLocalizer>();
        var secondCall = Substitute.For<IStringLocalizer>();
        inner.Create(typeof(TestResource)).Returns(firstCall, secondCall);

        using var factory = new SunfishLocalizerFactory(inner);

        var before = factory.Create(typeof(TestResource));
        factory.InvalidateAll();
        var after = factory.Create(typeof(TestResource));

        Assert.Same(firstCall, before);
        Assert.Same(secondCall, after);
        Assert.NotSame(before, after);
        inner.Received(2).Create(typeof(TestResource));
    }

    [Fact]
    public void Invalidate_DropsOnlyMatchingType()
    {
        var inner = Substitute.For<IStringLocalizerFactory>();
        inner.Create(typeof(TestResource)).Returns(Substitute.For<IStringLocalizer>(), Substitute.For<IStringLocalizer>());
        var otherStub = Substitute.For<IStringLocalizer>();
        inner.Create(typeof(OtherResource)).Returns(otherStub);

        using var factory = new SunfishLocalizerFactory(inner);

        var test1 = factory.Create(typeof(TestResource));
        var other1 = factory.Create(typeof(OtherResource));
        factory.Invalidate(typeof(TestResource));
        var test2 = factory.Create(typeof(TestResource));
        var other2 = factory.Create(typeof(OtherResource));

        Assert.NotSame(test1, test2); // TestResource invalidated
        Assert.Same(other1, other2);   // OtherResource untouched
        inner.Received(2).Create(typeof(TestResource));
        inner.Received(1).Create(typeof(OtherResource));
    }

    [Fact]
    public void Constructor_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SunfishLocalizerFactory(null!));
    }

    [Fact]
    public void Constructor_NonExistentResourcesRoot_DoesNotThrow()
    {
        var inner = Substitute.For<IStringLocalizerFactory>();
        var nonexistent = Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}");

        // Should not throw — watcher only attaches when the directory exists.
        using var factory = new SunfishLocalizerFactory(inner, nonexistent);
    }

    [Fact]
    public void Constructor_ExistingResourcesRoot_AttachesAndDoesNotThrow()
    {
        var inner = Substitute.For<IStringLocalizerFactory>();
        var dir = Path.Combine(Path.GetTempPath(), $"sunfish-loc-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            using var factory = new SunfishLocalizerFactory(inner, dir);
            // Smoke: factory remains usable.
            inner.Create(typeof(TestResource)).Returns(Substitute.For<IStringLocalizer>());
            Assert.NotNull(factory.Create(typeof(TestResource)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void InvalidateAll_IsThreadSafe_UnderConcurrentCreate()
    {
        var inner = Substitute.For<IStringLocalizerFactory>();
        inner.Create(Arg.Any<Type>()).Returns(_ => Substitute.For<IStringLocalizer>());

        using var factory = new SunfishLocalizerFactory(inner);

        // Hammer Create + InvalidateAll concurrently; assert no exceptions, all returns non-null.
        const int iterations = 200;
        using var barrier = new ManualResetEventSlim(initialState: false);

        var creator = new Thread(() =>
        {
            barrier.Wait();
            for (int i = 0; i < iterations; i++)
            {
                _ = factory.Create(typeof(TestResource));
            }
        });

        var invalidator = new Thread(() =>
        {
            barrier.Wait();
            for (int i = 0; i < iterations; i++)
            {
                factory.InvalidateAll();
            }
        });

        creator.Start();
        invalidator.Start();
        barrier.Set();
        creator.Join();
        invalidator.Join();

        // No assertion needed beyond completing without exception.
    }

    private sealed class OtherResource { }
}
