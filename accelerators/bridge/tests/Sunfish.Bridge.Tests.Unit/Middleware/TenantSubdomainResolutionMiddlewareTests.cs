using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Data;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Middleware;
using Sunfish.Bridge.Orchestration;
using Sunfish.Bridge.Services;
using Sunfish.Foundation.Authorization;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Middleware;

/// <summary>
/// Covers <see cref="TenantSubdomainResolutionMiddleware"/> (Wave 5.3.A,
/// see <c>_shared/product/wave-5.3-decomposition.md</c> §5.3.A). Uses the EF
/// Core in-memory provider so the middleware's <see cref="ITenantRegistry"/>
/// lookup exercises the real registry + DbContext path without needing
/// Postgres.
/// </summary>
public class TenantSubdomainResolutionMiddlewareTests
{
    private sealed class TestTenant : ITenantContext
    {
        public string TenantId => "unit-tenant";
        public string UserId => "unit-user";
        public IReadOnlyList<string> Roles { get; } = ["Admin"];
        public bool HasPermission(string permission) => true;
    }

    private sealed class MiddlewareHarness
    {
        public IServiceProvider Services { get; }
        public Guid TenantId { get; }
        public byte[] AuthSalt { get; }

        private MiddlewareHarness(IServiceProvider services, Guid tenantId, byte[] salt)
        {
            Services = services;
            TenantId = tenantId;
            AuthSalt = salt;
        }

        public static async Task<MiddlewareHarness> CreateAsync(
            string slug,
            TenantStatus status,
            TenantResolutionOptions? options = null,
            [System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
        {
            var root = new InMemoryDatabaseRoot();
            var services = new ServiceCollection();
            services.AddSingleton<ITenantContext, TestTenant>();
            services.AddDbContext<SunfishBridgeDbContext>(o => o.UseInMemoryDatabase(dbName, root));
            services.AddSingleton<ITenantRegistryEventBus, InMemoryTenantRegistryEventBus>();
            services.AddScoped<ITenantRegistry, TenantRegistry>();
            services.AddScoped<IBrowserTenantContext, BrowserTenantContext>();
            services.AddSingleton<IOptions<TenantResolutionOptions>>(
                Options.Create(options ?? new TenantResolutionOptions()));

            var sp = services.BuildServiceProvider();
            var salt = RandomNumberGenerator.GetBytes(16);

            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SunfishBridgeDbContext>();
                var registration = new TenantRegistration
                {
                    TenantId = Guid.NewGuid(),
                    Slug = slug,
                    DisplayName = slug,
                    Plan = "Team",
                    Status = status,
                    TrustLevel = TrustLevel.RelayOnly,
                    TeamPublicKey = null,
                    AuthSalt = salt,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                db.TenantRegistrations.Add(registration);
                await db.SaveChangesAsync();
                return new MiddlewareHarness(sp, registration.TenantId, salt);
            }
        }

        public async Task<(HttpContext ctx, IBrowserTenantContext tenantCtx, bool nextCalled)> InvokeAsync(
            string hostHeader,
            string? xForwardedHost = null)
        {
            using var scope = Services.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
            var tenantCtx = scope.ServiceProvider.GetRequiredService<IBrowserTenantContext>();
            var opts = scope.ServiceProvider.GetRequiredService<IOptions<TenantResolutionOptions>>();

            var ctx = new DefaultHttpContext
            {
                Request =
                {
                    Host = new HostString(hostHeader),
                },
            };
            if (xForwardedHost is not null)
            {
                ctx.Request.Headers["X-Forwarded-Host"] = xForwardedHost;
            }

            var nextCalled = false;
            var middleware = new TenantSubdomainResolutionMiddleware(_ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(ctx, registry, tenantCtx, opts);
            return (ctx, tenantCtx, nextCalled);
        }
    }

    [Fact]
    public async Task Happy_path_binds_context()
    {
        var harness = await MiddlewareHarness.CreateAsync("acme", TenantStatus.Active);

        var (ctx, tenantCtx, nextCalled) = await harness.InvokeAsync("acme.localhost");

        Assert.True(nextCalled, "next(ctx) must be invoked on the happy path");
        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.True(tenantCtx.IsResolved);
        Assert.Equal(harness.TenantId, tenantCtx.TenantId);
        Assert.Equal("acme", tenantCtx.Slug);
        Assert.Equal(TrustLevel.RelayOnly, tenantCtx.TrustLevel);
        Assert.Equal(harness.AuthSalt, tenantCtx.AuthSalt);
    }

    [Theory]
    [InlineData("admin.localhost")]
    [InlineData("www.localhost")]
    [InlineData("auth.localhost")]
    [InlineData("api.localhost")]
    public async Task Reserved_slug_returns_404(string host)
    {
        // The registry is irrelevant here — reserved slugs are short-circuited
        // before the DB lookup. Still provision a non-matching tenant row so the
        // harness services compose.
        var harness = await MiddlewareHarness.CreateAsync("acme", TenantStatus.Active);

        var (ctx, tenantCtx, nextCalled) = await harness.InvokeAsync(host);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.False(tenantCtx.IsResolved);
    }

    [Fact]
    public async Task Missing_slug_returns_404()
    {
        var harness = await MiddlewareHarness.CreateAsync("acme", TenantStatus.Active);

        var (ctx, tenantCtx, nextCalled) = await harness.InvokeAsync("unknown-tenant.localhost");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.False(tenantCtx.IsResolved);
    }

    [Fact]
    public async Task Pending_tenant_returns_404()
    {
        var harness = await MiddlewareHarness.CreateAsync("pendingco", TenantStatus.Pending);

        var (ctx, tenantCtx, nextCalled) = await harness.InvokeAsync("pendingco.localhost");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.False(tenantCtx.IsResolved);
    }

    [Fact]
    public async Task Cancelled_tenant_returns_410()
    {
        var harness = await MiddlewareHarness.CreateAsync("deadco", TenantStatus.Cancelled);

        var (ctx, tenantCtx, nextCalled) = await harness.InvokeAsync("deadco.localhost");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status410Gone, ctx.Response.StatusCode);
        Assert.False(tenantCtx.IsResolved);
    }

    [Fact]
    public async Task Suspended_tenant_returns_503_with_retry_after()
    {
        var harness = await MiddlewareHarness.CreateAsync("pausedco", TenantStatus.Suspended);

        var (ctx, tenantCtx, nextCalled) = await harness.InvokeAsync("pausedco.localhost");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, ctx.Response.StatusCode);
        Assert.Equal("300", ctx.Response.Headers.RetryAfter.ToString());
        Assert.False(tenantCtx.IsResolved);
    }

    [Fact]
    public async Task X_Forwarded_Host_is_honoured_when_trusted()
    {
        var harness = await MiddlewareHarness.CreateAsync(
            "acme",
            TenantStatus.Active,
            new TenantResolutionOptions
            {
                RootHost = "example.com",
                TrustForwardedHost = true,
            });

        // Host is the LB's internal hostname; the trusted proxy forwards the
        // original tenant-scoped hostname in X-Forwarded-Host.
        var (ctx, tenantCtx, nextCalled) = await harness.InvokeAsync(
            hostHeader: "internal.lb",
            xForwardedHost: "acme.example.com");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.True(tenantCtx.IsResolved);
        Assert.Equal("acme", tenantCtx.Slug);
    }

    [Fact]
    public async Task X_Forwarded_Host_is_ignored_when_untrusted()
    {
        var harness = await MiddlewareHarness.CreateAsync(
            "acme",
            TenantStatus.Active,
            new TenantResolutionOptions
            {
                // RootHost omitted so the Host header 'acme.localhost' is the
                // authoritative slug source (left-most label = "acme").
                TrustForwardedHost = false,
            });

        // Untrusted spoof — X-Forwarded-Host claims a different tenant; the
        // middleware must ignore it and resolve from Host.
        var (ctx, tenantCtx, nextCalled) = await harness.InvokeAsync(
            hostHeader: "acme.localhost",
            xForwardedHost: "admin.example.com");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.True(tenantCtx.IsResolved);
        Assert.Equal("acme", tenantCtx.Slug);
    }
}
