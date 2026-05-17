using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.WorkProjects;
using Sunfish.Blocks.WorkProjects.Migration;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Events;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>W#60 P4 — coverage for <see cref="WorkProjectsServiceCollectionExtensions"/>.</summary>
public sealed class AddBlocksWorkProjectsTests
{
    [Fact]
    public void AddBlocksWorkProjects_ResolvesAllPublicSurfaces()
    {
        var services = new ServiceCollection().AddBlocksWorkProjects();
        using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IProjectService>());
        Assert.NotNull(sp.GetRequiredService<IProjectReadModel>());
        Assert.NotNull(sp.GetRequiredService<ITimeEntryService>());
        Assert.NotNull(sp.GetRequiredService<ITimeApprovalService>());
        Assert.NotNull(sp.GetRequiredService<IRemodelProjectService>());
        Assert.NotNull(sp.GetRequiredService<IProjectActualProjector>());
        Assert.NotNull(sp.GetRequiredService<IProjectActualReader>());
        Assert.NotNull(sp.GetRequiredService<IProjectActualWriter>());
        Assert.NotNull(sp.GetRequiredService<IProjectBudgetRepository>());
        Assert.NotNull(sp.GetRequiredService<IProjectCodeGenerator>());
        Assert.NotNull(sp.GetRequiredService<IErpnextProjectImporter>());
        Assert.NotNull(sp.GetRequiredService<IDomainEventPublisher>());
        Assert.NotNull(sp.GetRequiredService<IPartyReadModel>());
    }

    [Fact]
    public void AddBlocksWorkProjects_DomainEventPublisher_TryAddPattern_AllowsOverride()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventPublisher, CustomPub>();
        services.AddBlocksWorkProjects();
        using var sp = services.BuildServiceProvider();
        Assert.IsType<CustomPub>(sp.GetRequiredService<IDomainEventPublisher>());
    }

    private sealed class CustomPub : IDomainEventPublisher
    {
        public Task PublishAsync<T>(DomainEventEnvelope<T> envelope, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
