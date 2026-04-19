using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Catalog.Tests.Bundles;

public class BundleCatalogTests
{
    [Fact]
    public void Register_and_TryGet_roundtrip()
    {
        var catalog = new BundleCatalog();
        var manifest = NewManifest("sunfish.bundles.alpha");

        catalog.Register(manifest);

        Assert.True(catalog.TryGet("sunfish.bundles.alpha", out var resolved));
        Assert.Same(manifest, resolved);
    }

    [Fact]
    public void Register_rejects_duplicate_keys()
    {
        var catalog = new BundleCatalog();
        catalog.Register(NewManifest("sunfish.bundles.alpha"));

        Assert.Throws<InvalidOperationException>(
            () => catalog.Register(NewManifest("sunfish.bundles.alpha")));
    }

    [Fact]
    public void GetBundles_preserves_registration_order()
    {
        var catalog = new BundleCatalog();
        catalog.Register(NewManifest("sunfish.bundles.a"));
        catalog.Register(NewManifest("sunfish.bundles.b"));
        catalog.Register(NewManifest("sunfish.bundles.c"));

        var keys = catalog.GetBundles().Select(b => b.Key).ToArray();

        Assert.Equal(new[] { "sunfish.bundles.a", "sunfish.bundles.b", "sunfish.bundles.c" }, keys);
    }

    [Fact]
    public void TryGet_returns_false_when_absent()
    {
        var catalog = new BundleCatalog();

        Assert.False(catalog.TryGet("nope", out var manifest));
        Assert.Null(manifest);
    }

    private static BusinessCaseBundleManifest NewManifest(string key) => new()
    {
        Key = key,
        Name = key,
        Version = "0.1.0",
    };
}
