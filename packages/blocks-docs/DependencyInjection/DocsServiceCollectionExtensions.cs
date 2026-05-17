using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.Docs.Models;
using Sunfish.Blocks.Docs.Services;

namespace Sunfish.Blocks.Docs.DependencyInjection;

/// <summary>
/// DI helpers for the documents cluster.
/// </summary>
public static class DocsServiceCollectionExtensions
{
    /// <summary>
    /// Register the blocks-docs substrate. PR 3 wires the upload
    /// pipeline: <see cref="IAttachmentRepository"/>,
    /// <see cref="IAttachmentService"/>, and
    /// <see cref="IMimeTypeAndSizePolicy"/> for defense-in-depth (MIME
    /// whitelist + size cap + tenant quota). PR 4 adds the cross-cluster
    /// join surface: <see cref="IDocumentRefRepository"/>. PR 5 wires
    /// the service layer + parent-delete reconciler:
    /// <see cref="IDocumentRefService"/> + <see cref="DocumentRefReconciler"/>.
    ///
    /// <para>
    /// Optional <paramref name="options"/> lets the host customize
    /// <see cref="BlocksDocsOptions"/> — per-tenant MIME whitelist
    /// overrides, per-attachment cap, cumulative tenant quota. Without
    /// it the defaults from <see cref="DefaultMimeWhitelist.Defaults"/>
    /// + 100 MB cap + unlimited quota apply.
    /// </para>
    /// </summary>
    public static IServiceCollection AddBlocksDocs(
        this IServiceCollection services,
        BlocksDocsOptions? options = null)
    {
        services.TryAddSingleton(options ?? new BlocksDocsOptions());
        services.TryAddSingleton<IAttachmentRepository, InMemoryAttachmentRepository>();
        services.TryAddSingleton<IMimeTypeAndSizePolicy, MimeTypeAndSizePolicy>();
        services.TryAddSingleton<IAttachmentService, AttachmentService>();
        services.TryAddSingleton<IDocumentRefRepository, InMemoryDocumentRefRepository>();
        services.TryAddSingleton<IDocumentRefService, DocumentRefService>();
        services.TryAddSingleton<DocumentRefReconciler>();
        services.TryAddSingleton<AttachmentOrphanReconciler>();
        return services;
    }
}
