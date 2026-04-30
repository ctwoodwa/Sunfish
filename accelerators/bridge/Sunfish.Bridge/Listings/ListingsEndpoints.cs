using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Bridge.Listings;

/// <summary>
/// Bridge route family for the W#28 public-listings surface
/// (ADR 0059). Phase 5c-1 ships <c>/robots.txt</c> +
/// <c>/sitemap.xml</c>; the human-facing
/// <c>/listings</c> + <c>/listings/{slug}</c> pages and the
/// inquiry POST path follow in subsequent phases.
/// </summary>
public static class ListingsEndpoints
{
    /// <summary>Wires the W#28 public-listings route family onto the Bridge.</summary>
    public static IEndpointRouteBuilder MapListingsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/robots.txt", HandleRobotsAsync);
        app.MapGet("/sitemap.xml", HandleSitemapAsync);
        return app;
    }

    internal static IResult HandleRobotsAsync(HttpRequest request)
    {
        var baseUrl = $"{request.Scheme}://{request.Host}";
        var body = new StringBuilder();
        body.Append("User-agent: *\n");
        body.Append("Allow: /listings\n");
        body.Append("Allow: /listings/\n");
        body.Append("Disallow: /listings/criteria/\n");
        body.Append("\n");
        body.Append($"Sitemap: {baseUrl}/sitemap.xml\n");
        return Results.Text(body.ToString(), "text/plain; charset=utf-8");
    }

    internal static async Task<IResult> HandleSitemapAsync(HttpRequest request, IListingRepository repository, CancellationToken ct)
    {
        var tenant = ResolveTenantFromHost(request.Host.Host);
        var baseUrl = $"{request.Scheme}://{request.Host}";

        var stream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Async = true,
            Encoding = Encoding.UTF8,
            Indent = false,
            OmitXmlDeclaration = false,
        };

        await using (var writer = XmlWriter.Create(stream, settings))
        {
            await writer.WriteStartDocumentAsync().ConfigureAwait(false);
            await writer.WriteStartElementAsync(prefix: null, localName: "urlset", ns: "http://www.sitemaps.org/schemas/sitemap/0.9").ConfigureAwait(false);

            await foreach (var listing in repository.ListAsync(tenant, ct))
            {
                if (listing.Status != PublicListingStatus.Published)
                {
                    continue;
                }
                await WriteUrlEntryAsync(writer, $"{baseUrl}/listings/{listing.Slug}", listing.PublishedAt).ConfigureAwait(false);
            }

            await writer.WriteEndElementAsync().ConfigureAwait(false);
            await writer.WriteEndDocumentAsync().ConfigureAwait(false);
        }

        var bytes = stream.ToArray();
        var xml = Encoding.UTF8.GetString(bytes);
        return Results.Content(xml, "application/xml; charset=utf-8");
    }

    private static async Task WriteUrlEntryAsync(XmlWriter writer, string loc, DateTimeOffset? lastmod)
    {
        await writer.WriteStartElementAsync(null, "url", null).ConfigureAwait(false);
        await writer.WriteElementStringAsync(null, "loc", null, loc).ConfigureAwait(false);
        if (lastmod is not null)
        {
            await writer.WriteElementStringAsync(null, "lastmod", null, lastmod.Value.UtcDateTime.ToString("yyyy-MM-dd")).ConfigureAwait(false);
        }
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the tenant for a public-listings request from the host
    /// header. Phase 5c-1 strips the leading subdomain label; the
    /// tenant-subdomain middleware will replace this with the actual
    /// resolved <see cref="ITenantContext"/> in a follow-up.
    /// </summary>
    internal static TenantId ResolveTenantFromHost(string host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return TenantId.Default;
        }
        var firstDot = host.IndexOf('.');
        if (firstDot <= 0)
        {
            return TenantId.Default;
        }
        return new TenantId(host[..firstDot]);
    }
}
