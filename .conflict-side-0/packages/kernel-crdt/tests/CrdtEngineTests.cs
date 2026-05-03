using Microsoft.Extensions.DependencyInjection;

using Sunfish.Kernel.Crdt.DependencyInjection;

namespace Sunfish.Kernel.Crdt.Tests;

/// <summary>
/// Coverage for <see cref="ICrdtEngine"/> + DI extension + document identity.
/// </summary>
public class CrdtEngineTests
{
    [Fact]
    public async Task CreateDocument_AssignsRequestedId()
    {
        ICrdtEngine engine = new StubCrdtEngine();
        await using var doc = engine.CreateDocument("my-doc-id");
        Assert.Equal("my-doc-id", doc.DocumentId);
    }

    [Fact]
    public void CreateDocument_RejectsEmptyId()
    {
        ICrdtEngine engine = new StubCrdtEngine();
        Assert.Throws<ArgumentException>(() => engine.CreateDocument(string.Empty));
    }

    [Fact]
    public void EngineMetadata_IsPopulated()
    {
        ICrdtEngine engine = new StubCrdtEngine();
        Assert.False(string.IsNullOrWhiteSpace(engine.EngineName));
        Assert.False(string.IsNullOrWhiteSpace(engine.EngineVersion));
    }

    [Fact]
    public void AddSunfishCrdtEngine_RegistersSingleton()
    {
        var services = new ServiceCollection();
        services.AddSunfishCrdtEngine();

        using var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<ICrdtEngine>();
        var b = sp.GetRequiredService<ICrdtEngine>();
        Assert.Same(a, b);
    }

    [Fact]
    public void AddSunfishCrdtEngine_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddSunfishCrdtEngine();
        services.AddSunfishCrdtEngine();

        var count = services.Count(d => d.ServiceType == typeof(ICrdtEngine));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task OpenDocument_WithEmptySnapshot_ReturnsEmptyDoc()
    {
        ICrdtEngine engine = new StubCrdtEngine();
        await using var doc = engine.OpenDocument("doc-1", ReadOnlyMemory<byte>.Empty);
        Assert.Equal(0, doc.GetText("body").Length);
        Assert.Equal(0, doc.GetMap("meta").Count);
        Assert.Equal(0, doc.GetList("items").Count);
    }
}
