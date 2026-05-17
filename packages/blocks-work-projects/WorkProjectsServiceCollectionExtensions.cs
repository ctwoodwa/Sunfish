using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.WorkProjects.Migration;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.WorkProjects;

/// <summary>
/// Composition root for <c>blocks-work-projects</c>. Registers every
/// in-memory service the cluster ships; cross-cluster contract
/// dependencies (<see cref="IDomainEventPublisher"/>,
/// <see cref="IPartyReadModel"/>, future <c>IPeriodResolver</c> wiring)
/// use <c>TryAddSingleton</c> so downstream sweeps from
/// <c>foundation-events</c> + sibling clusters cleanly override
/// without throwing.
/// </summary>
public static class WorkProjectsServiceCollectionExtensions
{
    public static IServiceCollection AddBlocksWorkProjects(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Repositories
        services.AddSingleton<InMemoryProjectRepository>();
        services.AddSingleton<InMemoryProjectMilestoneRepository>();
        services.AddSingleton<IProjectBudgetRepository, InMemoryProjectBudgetRepository>();
        services.AddSingleton<InMemoryTimeEntryRepository>();
        services.AddSingleton<InMemoryProjectActualRepository>();
        services.AddSingleton<IProjectActualReader>(sp => sp.GetRequiredService<InMemoryProjectActualRepository>());
        services.AddSingleton<IProjectActualWriter>(sp => sp.GetRequiredService<InMemoryProjectActualRepository>());
        services.AddSingleton<IProjectActualRepository>(sp => sp.GetRequiredService<InMemoryProjectActualRepository>());

        // Code generator
        services.AddSingleton<IProjectCodeGenerator, InMemoryProjectCodeGenerator>();

        // Read models
        services.AddSingleton<IProjectReadModel, InMemoryProjectReadModel>();

        // Cross-cluster contract stubs (TryAddSingleton — sweep-friendly)
        services.TryAddSingleton<IDomainEventPublisher, NoopDomainEventPublisher>();
        services.TryAddSingleton<IPartyReadModel, InMemoryPartyReadModel>();

        // Services
        services.AddSingleton<IProjectService, InMemoryProjectService>();
        services.AddSingleton<ITimeEntryService, InMemoryTimeEntryService>();
        services.AddSingleton<ITimeApprovalService, InMemoryTimeApprovalService>();
        services.AddSingleton<IRemodelProjectService, InMemoryRemodelProjectService>();
        services.AddSingleton(sp => new Events.JournalEntryPostedHandler(
            sp.GetRequiredService<IProjectActualReader>(),
            sp.GetRequiredService<IProjectActualWriter>(),
            sp.GetService<IGlAccountCategoryResolver>()));
        services.AddSingleton<IProjectActualProjector, InMemoryProjectActualProjector>();

        // Importer
        services.AddSingleton<IErpnextProjectImporter, ErpnextProjectImporter>();

        return services;
    }
}
