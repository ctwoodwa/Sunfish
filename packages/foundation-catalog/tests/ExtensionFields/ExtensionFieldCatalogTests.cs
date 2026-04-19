using Sunfish.Foundation.Catalog.ExtensionFields;
using Sunfish.Foundation.Extensibility;

namespace Sunfish.Foundation.Catalog.Tests.ExtensionFields;

public class ExtensionFieldCatalogTests
{
    private sealed class SampleLease : IHasExtensionData
    {
        public ExtensionDataBag Extensions { get; } = new();
    }

    private static ExtensionFieldSpec Spec(string key, ExtensionStorage storage = ExtensionStorage.Json)
        => new(ExtensionFieldKey.Of(key), typeof(string), ExtensionFieldScope.Bundle, storage);

    [Fact]
    public void Register_and_GetFields_roundtrip()
    {
        var catalog = new ExtensionFieldCatalog();
        catalog.Register<SampleLease>(Spec("moveInChecklist"));
        catalog.Register<SampleLease>(Spec("petPolicy"));

        var fields = catalog.GetFields<SampleLease>();

        Assert.Equal(2, fields.Count);
        Assert.Equal("moveInChecklist", fields[0].Key.Value);
        Assert.Equal("petPolicy", fields[1].Key.Value);
    }

    [Fact]
    public void Duplicate_registration_throws()
    {
        var catalog = new ExtensionFieldCatalog();
        catalog.Register<SampleLease>(Spec("moveInChecklist"));

        Assert.Throws<InvalidOperationException>(
            () => catalog.Register<SampleLease>(Spec("moveInChecklist")));
    }

    [Fact]
    public void TryGetField_returns_registered_spec()
    {
        var catalog = new ExtensionFieldCatalog();
        catalog.Register<SampleLease>(Spec("petPolicy", ExtensionStorage.PromotedColumn));

        var found = catalog.TryGetField(
            typeof(SampleLease),
            ExtensionFieldKey.Of("petPolicy"),
            out var spec);

        Assert.True(found);
        Assert.NotNull(spec);
        Assert.Equal(ExtensionStorage.PromotedColumn, spec!.Storage);
    }

    [Fact]
    public void TryGetField_returns_false_when_absent()
    {
        var catalog = new ExtensionFieldCatalog();

        var found = catalog.TryGetField(
            typeof(SampleLease),
            ExtensionFieldKey.Of("missing"),
            out var spec);

        Assert.False(found);
        Assert.Null(spec);
    }

    [Fact]
    public void GetFields_returns_empty_for_unregistered_entity()
    {
        var catalog = new ExtensionFieldCatalog();
        Assert.Empty(catalog.GetFields(typeof(SampleLease)));
    }
}
