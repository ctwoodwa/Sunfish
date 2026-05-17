using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.Docs.Services;

namespace Sunfish.Blocks.Docs.DependencyInjection;

/// <summary>
/// DI helpers for the documents cluster.
/// </summary>
public static class DocsServiceCollectionExtensions
{
    /// <summary>
    /// Register the blocks-docs substrate. PR 2 wires
    /// <see cref="IAttachmentRepository"/> → <see cref="InMemoryAttachmentRepository"/>
    /// and <see cref="IAttachmentService"/> → <see cref="AttachmentService"/>.
    /// PR 3 will wire <c>IBlobStore</c> + <c>BlocksDocsOptions</c>
    /// (MIME / size policy). PR 4 adds <c>IDocumentRefService</c>.
    /// </summary>
    public static IServiceCollection AddBlocksDocs(this IServiceCollection services)
    {
        services.TryAddSingleton<IAttachmentRepository, InMemoryAttachmentRepository>();
        services.TryAddSingleton<IAttachmentService, AttachmentService>();
        return services;
    }
}
