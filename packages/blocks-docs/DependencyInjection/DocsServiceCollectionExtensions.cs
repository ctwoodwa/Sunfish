using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Blocks.Docs.DependencyInjection;

/// <summary>
/// DI helpers for the documents cluster. PR 1 ships a no-op stub —
/// services, IBlobStore wiring, and cross-cluster DocumentRef
/// registrations land in PRs 2–5.
/// </summary>
public static class DocsServiceCollectionExtensions
{
    /// <summary>
    /// Register the blocks-docs substrate. PR 1 is a no-op (returns
    /// <paramref name="services"/> unchanged); call sites can wire it
    /// today and pick up the real registrations transparently once
    /// PRs 2–5 ship.
    /// </summary>
    public static IServiceCollection AddBlocksDocs(this IServiceCollection services)
    {
        // Intentionally empty in PR 1. PR 2 will register
        // IAttachmentRepository → InMemoryAttachmentRepository and the
        // IAttachmentService for content-hash dedup. PR 3 wires IBlobStore
        // + MimeTypeAndSizePolicy. PR 4 adds IDocumentRefService.
        return services;
    }
}
