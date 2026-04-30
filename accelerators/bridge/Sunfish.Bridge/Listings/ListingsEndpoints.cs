using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Defense;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Bridge.Listings;

/// <summary>
/// Bridge route family for the W#28 public-listings surface (ADR 0059).
/// Phase 5c-1 shipped <c>/robots.txt</c> + <c>/sitemap.xml</c>;
/// Phase 5c-2 adds the human-facing <c>/listings</c> + <c>/listings/{slug}</c>
/// SSR pages with schema.org JSON-LD + OpenGraph; the inquiry POST path
/// + capability-tier routes follow in subsequent slices.
/// </summary>
public static class ListingsEndpoints
{
    /// <summary>Wires the W#28 public-listings route family onto the Bridge.</summary>
    public static IEndpointRouteBuilder MapListingsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapGet("/robots.txt", HandleRobotsAsync);
        app.MapGet("/sitemap.xml", HandleSitemapAsync);
        app.MapGet("/listings", HandleIndexAsync);
        app.MapGet("/listings/{slug}", HandleDetailAsync);
        app.MapPost("/listings/{slug}/inquiry", HandleInquiryPostAsync);
        app.MapGet("/listings/criteria/{token}", HandleCriteriaAsync);
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

    internal static async Task<IResult> HandleIndexAsync(HttpRequest request, IListingRepository repository, IListingRenderer renderer, CancellationToken ct)
    {
        var tenant = ResolveTenantFromHost(request.Host.Host);
        var baseUrl = $"{request.Scheme}://{request.Host}";

        var rendered = new List<RenderedListing>();
        await foreach (var listing in repository.ListAsync(tenant, ct))
        {
            if (listing.Status != PublicListingStatus.Published)
            {
                continue;
            }
            var projection = await renderer.RenderForTierAsync(tenant, listing.Id, RedactionTier.Anonymous, ct).ConfigureAwait(false);
            if (projection is not null)
            {
                rendered.Add(projection);
            }
        }

        // Match the rendered list back to the underlying slug (the projection
        // doesn't carry the slug because slug is a routing concern, not a
        // viewer-facing data field).
        var slugs = new Dictionary<PublicListingId, string>();
        await foreach (var listing in repository.ListAsync(tenant, ct))
        {
            slugs[listing.Id] = listing.Slug;
        }

        var html = RenderIndexHtml(rendered, slugs, tenant.Value, baseUrl);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    internal static async Task<IResult> HandleDetailAsync(HttpRequest request, string slug, IListingRepository repository, IListingRenderer renderer, CancellationToken ct)
    {
        var tenant = ResolveTenantFromHost(request.Host.Host);
        var baseUrl = $"{request.Scheme}://{request.Host}";

        var listing = await repository.GetBySlugAsync(tenant, slug, ct).ConfigureAwait(false);
        if (listing is null || listing.Status != PublicListingStatus.Published)
        {
            return Results.NotFound();
        }

        var projection = await renderer.RenderForTierAsync(tenant, listing.Id, RedactionTier.Anonymous, ct).ConfigureAwait(false);
        if (projection is null)
        {
            return Results.NotFound();
        }

        var pageUrl = $"{baseUrl}/listings/{slug}";
        var html = RenderDetailHtml(projection, listing.Slug, pageUrl);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    internal static async Task<IResult> HandleInquiryPostAsync(
        HttpRequest request,
        string slug,
        InquiryFormPost body,
        IListingRepository repository,
        IInquiryFormDefense defense,
        IPublicInquiryService inquiryService,
        CancellationToken ct)
    {
        if (body is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["body"] = new[] { "Request body is required." },
            });
        }

        var tenant = ResolveTenantFromHost(request.Host.Host);
        var listing = await repository.GetBySlugAsync(tenant, slug, ct).ConfigureAwait(false);
        if (listing is null || listing.Status != PublicListingStatus.Published)
        {
            return Results.NotFound();
        }

        var clientIp = ResolveClientIp(request);
        var userAgent = request.Headers.UserAgent.ToString();
        var receivedAt = DateTimeOffset.UtcNow;

        var submission = new InquiryFormSubmission
        {
            Tenant = tenant,
            CaptchaToken = body.CaptchaToken ?? string.Empty,
            ClientIp = clientIp,
            ProspectEmail = body.Email ?? string.Empty,
            MessageBody = body.MessageBody ?? string.Empty,
            ReceivedAt = receivedAt,
            InquirerName = body.Name,
            ListingSlug = slug,
            UserAgent = userAgent,
        };

        var verdict = await defense.EvaluateAsync(submission, ct).ConfigureAwait(false);
        if (!verdict.Passed)
        {
            return Results.UnprocessableEntity(new
            {
                rejectedAt = verdict.RejectedAt?.ToString(),
                reason = verdict.Reason,
            });
        }

        var capability = new AnonymousCapability
        {
            Token = Guid.NewGuid().ToString("N"),
            IssuedAt = receivedAt,
            ExpiresAt = receivedAt.AddMinutes(30),
        };

        var inquiryRequest = new PublicInquiryRequest
        {
            Tenant = tenant,
            Listing = listing.Id,
            ProspectName = body.Name ?? string.Empty,
            ProspectEmail = body.Email ?? string.Empty,
            ProspectPhone = body.Phone,
            MessageBody = body.MessageBody ?? string.Empty,
            ClientIp = clientIp,
            UserAgent = userAgent,
        };

        var inquiry = await inquiryService.SubmitInquiryAsync(inquiryRequest, capability, ct).ConfigureAwait(false);

        return Results.Accepted(value: new
        {
            inquiryId = inquiry.Id.Value,
            status = inquiry.Status.ToString(),
        });
    }

    internal static async Task<IResult> HandleCriteriaAsync(
        HttpRequest request,
        string token,
        IListingRepository repository,
        IListingRenderer renderer,
        IProspectCapabilityVerifier verifier,
        CancellationToken ct)
    {
        var tenant = ResolveTenantFromHost(request.Host.Host);
        var baseUrl = $"{request.Scheme}://{request.Host}";
        var now = DateTimeOffset.UtcNow;

        // Verify the macaroon token. The verifier requires a specific
        // listing id, but the criteria page is a multi-listing surface —
        // verify against the FIRST allowed listing the prospect has, then
        // (if successful) project the remaining allowed listings against
        // the same capability.
        VerifiedProspectCapability verified;
        try
        {
            // Decode the token's caveats lazily by attempting verification
            // against any listing the route can resolve. We don't know the
            // allowed-listing set ahead of verification, so we use the
            // sentinel pattern: try with a probe listing-id, expect either
            // success (caveat matched a real listing — unlikely with a
            // random Guid) or `listing-not-in-allowed-set` from which we
            // can recover by inspecting the caveat-parsing path.
            //
            // Cleaner approach: enumerate listings for the tenant + try
            // each; the first that matches is the "primary" listing. The
            // criteria surface naturally targets the prospect's full
            // allowed-listings set so this enumeration is the right
            // semantics — anonymous viewers can't reach this route, only
            // capability holders can.
            verified = await ResolveAndVerifyAsync(token, tenant, repository, verifier, now, ct).ConfigureAwait(false);
        }
        catch (ProspectCapabilityDeniedException ex)
        {
            return Results.Problem(
                detail: ex.Reason,
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Prospect capability denied");
        }

        var rendered = new List<RenderedListing>();
        var slugs = new Dictionary<PublicListingId, string>();
        foreach (var listingId in verified.AllowedListings)
        {
            var listing = await repository.GetAsync(tenant, listingId, ct).ConfigureAwait(false);
            if (listing is null || listing.Status != PublicListingStatus.Published)
            {
                continue;
            }
            slugs[listingId] = listing.Slug;
            var projection = await renderer.RenderForTierAsync(tenant, listingId, RedactionTier.Prospect, ct).ConfigureAwait(false);
            if (projection is not null)
            {
                rendered.Add(projection);
            }
        }

        var html = RenderCriteriaHtml(rendered, slugs, verified, baseUrl);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static async Task<VerifiedProspectCapability> ResolveAndVerifyAsync(
        string token,
        TenantId tenant,
        IListingRepository repository,
        IProspectCapabilityVerifier verifier,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Iterate the tenant's listings; the first that the verifier
        // accepts is a probe match — the verifier will then project the
        // full allowed-listings set. If no listing matches, the verifier's
        // last-thrown denial is what we surface (typically wrong-tenant /
        // signature-mismatch / decode-failed; or listing-not-in-allowed-set
        // when the caveats are sound but no Published listing exists at
        // this tenant).
        ProspectCapabilityDeniedException? lastDenial = null;
        await foreach (var listing in repository.ListAsync(tenant, ct))
        {
            if (listing.Status != PublicListingStatus.Published)
            {
                continue;
            }
            try
            {
                return await verifier.VerifyAsync(token, tenant, listing.Id, now, ct).ConfigureAwait(false);
            }
            catch (ProspectCapabilityDeniedException ex)
            {
                lastDenial = ex;
                if (ex.Reason.StartsWith("listing-not-in-allowed-set", StringComparison.Ordinal))
                {
                    continue;
                }
                throw;
            }
        }
        throw lastDenial ?? new ProspectCapabilityDeniedException(
            token.Length <= 8 ? token : token[..8] + "...",
            "no-published-listings-for-tenant");
    }

    private static string RenderCriteriaHtml(
        IReadOnlyList<RenderedListing> listings,
        IReadOnlyDictionary<PublicListingId, string> slugs,
        VerifiedProspectCapability verified,
        string baseUrl)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html lang=\"en\"><head>");
        html.Append("<meta charset=\"utf-8\">");
        html.Append("<title>Application criteria</title>");
        html.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.Append("<meta name=\"robots\" content=\"noindex, nofollow\">");
        html.Append("</head><body>");
        html.Append("<main>");
        html.Append("<h1>Application criteria</h1>");
        html.Append($"<p class=\"prospect-info\">Signed in as {HtmlEncode(verified.Email)}.</p>");
        if (listings.Count == 0)
        {
            html.Append("<p>No listings are currently available for application.</p>");
        }
        else
        {
            html.Append("<ul class=\"criteria-listings\">");
            foreach (var l in listings)
            {
                if (!slugs.TryGetValue(l.Id, out var slug))
                {
                    continue;
                }
                html.Append("<li class=\"criteria-listing\">");
                html.Append($"<h2><a href=\"/listings/{HtmlEncode(slug)}\">{HtmlEncode(l.Headline)}</a></h2>");
                html.Append($"<p class=\"address\">{HtmlEncode(l.DisplayAddress)}</p>");
                if (l.AskingRent is { } rent)
                {
                    html.Append($"<p class=\"rent\">{HtmlEncode(FormatMoney(rent))}</p>");
                }
                html.Append($"<section class=\"description\">{HtmlEncode(l.DescriptionMarkdown)}</section>");
                html.Append("</li>");
            }
            html.Append("</ul>");
        }
        html.Append("</main></body></html>");
        return html.ToString();
    }

    private static IPAddress ResolveClientIp(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && forwarded.Count > 0)
        {
            var first = forwarded[0]!.Split(',', 2)[0].Trim();
            if (IPAddress.TryParse(first, out var parsed))
            {
                return parsed;
            }
        }
        return request.HttpContext.Connection.RemoteIpAddress ?? IPAddress.Loopback;
    }

    private static string RenderIndexHtml(IReadOnlyList<RenderedListing> listings, IReadOnlyDictionary<PublicListingId, string> slugs, string tenantValue, string baseUrl)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html lang=\"en\"><head>");
        html.Append("<meta charset=\"utf-8\">");
        html.Append($"<title>Listings · {HtmlEncode(tenantValue)}</title>");
        html.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.Append($"<link rel=\"canonical\" href=\"{HtmlEncode(baseUrl)}/listings\">");
        html.Append($"<meta property=\"og:title\" content=\"Listings · {HtmlEncode(tenantValue)}\">");
        html.Append("<meta property=\"og:type\" content=\"website\">");
        html.Append($"<meta property=\"og:url\" content=\"{HtmlEncode(baseUrl)}/listings\">");
        html.Append("</head><body>");
        html.Append("<main>");
        html.Append("<h1>Listings</h1>");
        if (listings.Count == 0)
        {
            html.Append("<p>No listings are currently published.</p>");
        }
        else
        {
            html.Append("<ul class=\"listings\">");
            foreach (var l in listings)
            {
                if (!slugs.TryGetValue(l.Id, out var slug))
                {
                    continue;
                }
                html.Append("<li class=\"listing\">");
                html.Append($"<h2><a href=\"/listings/{HtmlEncode(slug)}\">{HtmlEncode(l.Headline)}</a></h2>");
                html.Append($"<p class=\"address\">{HtmlEncode(l.DisplayAddress)}</p>");
                if (l.AskingRent is { } rent)
                {
                    html.Append($"<p class=\"rent\">{HtmlEncode(FormatMoney(rent))}</p>");
                }
                html.Append("</li>");
            }
            html.Append("</ul>");
        }
        html.Append("</main></body></html>");
        return html.ToString();
    }

    private static string RenderDetailHtml(RenderedListing listing, string slug, string pageUrl)
    {
        var jsonLd = BuildAccommodationJsonLd(listing, pageUrl);

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html lang=\"en\"><head>");
        html.Append("<meta charset=\"utf-8\">");
        html.Append($"<title>{HtmlEncode(listing.Headline)}</title>");
        html.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.Append($"<link rel=\"canonical\" href=\"{HtmlEncode(pageUrl)}\">");
        html.Append($"<meta property=\"og:title\" content=\"{HtmlEncode(listing.Headline)}\">");
        html.Append("<meta property=\"og:type\" content=\"website\">");
        html.Append($"<meta property=\"og:url\" content=\"{HtmlEncode(pageUrl)}\">");
        html.Append($"<meta property=\"og:description\" content=\"{HtmlEncode(TruncateForOg(listing.DescriptionMarkdown))}\">");
        html.Append("<script type=\"application/ld+json\">");
        html.Append(jsonLd);
        html.Append("</script>");
        html.Append("</head><body>");
        html.Append("<main>");
        html.Append($"<h1>{HtmlEncode(listing.Headline)}</h1>");
        html.Append($"<p class=\"address\">{HtmlEncode(listing.DisplayAddress)}</p>");
        if (listing.AskingRent is { } detailRent)
        {
            html.Append($"<p class=\"rent\">{HtmlEncode(FormatMoney(detailRent))}</p>");
        }
        html.Append($"<section class=\"description\">{HtmlEncode(listing.DescriptionMarkdown)}</section>");
        html.Append("</main></body></html>");
        return html.ToString();
    }

    private static string BuildAccommodationJsonLd(RenderedListing listing, string pageUrl)
    {
        // schema.org Accommodation type — generic enough for apartment / house /
        // room / etc. The W#28 hand-off references "Apartment" specifically,
        // but blocks-public-listings doesn't yet differentiate dwelling types,
        // so Accommodation is the safe parent type.
        var doc = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Accommodation",
            ["name"] = listing.Headline,
            ["description"] = listing.DescriptionMarkdown,
            ["url"] = pageUrl,
        };
        if (!string.IsNullOrWhiteSpace(listing.DisplayAddress))
        {
            doc["address"] = new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = listing.DisplayAddress,
            };
        }
        if (listing.AskingRent is { } rent)
        {
            doc["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["priceCurrency"] = rent.Currency,
                ["price"] = rent.Amount.ToString(CultureInfo.InvariantCulture),
            };
        }
        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        // Escape `</` inside the JSON-LD payload so a script-closer in user-
        // controlled fields can't break out of the surrounding <script> tag
        // (XSS defense; standard practice for inline JSON-LD).
        return json.Replace("</", "<\\/", StringComparison.Ordinal);
    }

    private static string FormatMoney(Sunfish.Foundation.Integrations.Payments.Money money)
        => $"{money.Amount.ToString("0.##", CultureInfo.InvariantCulture)} {money.Currency}";

    private static string TruncateForOg(string text, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        if (text.Length <= maxLength)
        {
            return text;
        }
        return text[..maxLength].TrimEnd() + "…";
    }

    private static string HtmlEncode(string text) => HtmlEncoder.Default.Encode(text ?? string.Empty);

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
