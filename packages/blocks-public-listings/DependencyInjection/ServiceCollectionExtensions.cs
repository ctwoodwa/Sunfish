using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.PublicListings.Data;
using Sunfish.Blocks.PublicListings.Defense;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Foundation.Integrations.Captcha;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.PublicListings.DependencyInjection;

/// <summary>DI registration for the in-memory public-listings substrate.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the in-memory public-listings substrate (repository + renderer + entity-module contribution + 5-layer defense pipeline).</summary>
    public static IServiceCollection AddInMemoryPublicListings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IListingRepository, InMemoryListingRepository>();
        services.TryAddSingleton<IListingRenderer, DefaultListingRenderer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISunfishEntityModule, PublicListingsEntityModule>());

        // W#28 Phase 5b — defense pipeline (Layers 1–3 always wired; Layers
        // 4–5 activate when an IInboundMessageScorer + IUnroutedTriageQueue
        // are also registered through W#20's AddSunfishMessaging()).
        services.TryAddSingleton<ICaptchaVerifier, InMemoryCaptchaVerifier>();
        services.TryAddSingleton<IInquiryRateLimiter, InMemoryInquiryRateLimiter>();
        services.TryAddSingleton<IEmailMxResolver, StubEmailMxResolver>();
        services.TryAddSingleton<IInquiryFormDefense>(sp =>
        {
            var captcha = sp.GetRequiredService<ICaptchaVerifier>();
            var rateLimiter = sp.GetRequiredService<IInquiryRateLimiter>();
            var mxResolver = sp.GetRequiredService<IEmailMxResolver>();
            var scorer = sp.GetService<Sunfish.Foundation.Integrations.Messaging.IInboundMessageScorer>();
            var triageQueue = sp.GetService<Sunfish.Foundation.Integrations.Messaging.IUnroutedTriageQueue>();
            var options = sp.GetService<InquiryFormDefenseOptions>();
            var audit = sp.GetService<Sunfish.Blocks.PublicListings.Audit.PublicListingAuditEmitter>();
            return new InquiryFormDefense(captcha, rateLimiter, mxResolver, scorer, triageQueue, options, audit);
        });
        return services;
    }
}
