using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Bridge.Middleware;
using Sunfish.UICore.Wayfinder;

namespace Sunfish.Bridge.Features.Identity;

/// <summary>
/// Bridge route family for the W#58 Phase 3 Identity Atlas JSON surface (ADR 0066 §Phase 3).
/// Exposes <see cref="IIdentityAtlasSurface"/> over HTTP/JSON for React adapter consumers.
/// TenantId is resolved from <see cref="IBrowserTenantContext"/>; ActorId from the
/// <c>sub</c> / <see cref="ClaimTypes.NameIdentifier"/> claim. All routes require
/// <c>RequireAuthorization()</c>.
/// </summary>
public static class IdentityEndpoints
{
    /// <summary>Wires the W#58 Phase 3 Identity Atlas route family onto the Bridge.</summary>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/identity/profile",      HandleProfileAsync).RequireAuthorization();
        app.MapGet("/api/v1/identity/keys",          HandleKeyRotationAsync).RequireAuthorization();
        app.MapGet("/api/v1/identity/recovery",      HandleRecoveryContactsAsync).RequireAuthorization();
        app.MapGet("/api/v1/identity/keys/history",  HandleHistoricalKeysAsync).RequireAuthorization();
        app.MapGet("/api/v1/identity/teams",         HandleActiveTeamOverviewAsync).RequireAuthorization();

        return app;
    }

    // -----------------------------------------------------------------------
    //  Handlers
    // -----------------------------------------------------------------------

    internal static async Task<IResult> HandleProfileAsync(
        HttpContext ctx,
        IBrowserTenantContext tenantCtx,
        IIdentityAtlasSurface surface,
        CancellationToken ct)
    {
        if (!TryResolve(ctx, tenantCtx, out var tenantId, out var actorId))
            return Results.Unauthorized();

        var vm = await surface.GetProfileEditAsync(tenantId, actorId, ct).ConfigureAwait(false);
        return Results.Ok(new IdentityProfileResponse(
            Actor:        actorId.Value,
            DisplayName:  vm.DisplayName,
            ContactEmail: vm.ContactEmail,
            PhoneNumber:  vm.PhoneNumber));
    }

    internal static async Task<IResult> HandleKeyRotationAsync(
        HttpContext ctx,
        IBrowserTenantContext tenantCtx,
        IIdentityAtlasSurface surface,
        CancellationToken ct)
    {
        if (!TryResolve(ctx, tenantCtx, out var tenantId, out var actorId))
            return Results.Unauthorized();

        var vm = await surface.GetKeyRotationAsync(tenantId, actorId, ct).ConfigureAwait(false);
        return Results.Ok(new KeyRotationResponse(
            Actor:                actorId.Value,
            CurrentFingerprint:   vm.CurrentFingerprint.Value,
            HistoricalKeyCount:   vm.HistoricalKeyCount,
            RotationInProgress:   vm.RotationInProgress,
            RotationWindowExpiry: vm.RotationWindowExpiry?.ToString("O")));
    }

    internal static async Task<IResult> HandleRecoveryContactsAsync(
        HttpContext ctx,
        IBrowserTenantContext tenantCtx,
        IIdentityAtlasSurface surface,
        CancellationToken ct)
    {
        if (!TryResolve(ctx, tenantCtx, out var tenantId, out var actorId))
            return Results.Unauthorized();

        var vm = await surface.GetRecoveryContactsAsync(tenantId, actorId, ct).ConfigureAwait(false);
        var contacts = vm.Contacts
            .Select(c => new RecoveryContactResponse(
                ContactActorId:     c.ContactActorId.Value,
                DisplayName:        c.DisplayName,
                VerificationStatus: c.VerificationStatus.ToString().ToLowerInvariant(),
                EnrolledAt:         c.EnrolledAt.ToString("O")))
            .ToList();
        return Results.Ok(new RecoveryContactsResponse(
            Actor:       actorId.Value,
            Contacts:    contacts,
            MaxContacts: vm.MaxContacts));
    }

    internal static async Task<IResult> HandleHistoricalKeysAsync(
        HttpContext ctx,
        IBrowserTenantContext tenantCtx,
        IIdentityAtlasSurface surface,
        CancellationToken ct)
    {
        if (!TryResolve(ctx, tenantCtx, out var tenantId, out var actorId))
            return Results.Unauthorized();

        var vm = await surface.GetHistoricalKeysAsync(tenantId, actorId, ct).ConfigureAwait(false);
        var keys = vm.Keys
            .Select(k => new HistoricalKeyEntryResponse(
                Fingerprint:            k.Fingerprint.Value,
                ActivatedAt:            k.ActivatedAt.ToString("O"),
                RetiredAt:              k.RetiredAt?.ToString("O"),
                RotationReason:         k.RotationReason,
                SignatureSurvivalCount: k.SignatureSurvivalCount))
            .ToList();
        return Results.Ok(new HistoricalKeysResponse(
            Actor: actorId.Value,
            Keys:  keys));
    }

    internal static async Task<IResult> HandleActiveTeamOverviewAsync(
        HttpContext ctx,
        IBrowserTenantContext tenantCtx,
        IIdentityAtlasSurface surface,
        CancellationToken ct)
    {
        if (!TryResolve(ctx, tenantCtx, out var tenantId, out var actorId))
            return Results.Unauthorized();

        var vm = await surface.GetActiveTeamOverviewAsync(tenantId, actorId, ct).ConfigureAwait(false);
        var teams = vm.Teams
            .Select(t => new TeamMembershipResponse(
                TeamId:            t.TeamId.ToString(),
                DisplayName:       t.DisplayName,
                RoleDisplayName:   t.RoleDisplayName,
                SubkeyFingerprint: t.SubkeyFingerprint.Value))
            .ToList();
        return Results.Ok(new ActiveTeamOverviewResponse(
            Actor:        actorId.Value,
            Teams:        teams,
            ActiveTeamId: vm.ActiveTeamId?.ToString()));
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    private static bool TryResolve(
        HttpContext ctx,
        IBrowserTenantContext tenantCtx,
        out TenantId tenantId,
        out ActorId actorId)
    {
        tenantId = default;
        actorId  = default;

        if (!tenantCtx.IsResolved) return false;

        var sub = ctx.User.FindFirst("sub")?.Value
            ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (sub is null) return false;

        tenantId = new TenantId(tenantCtx.TenantId.ToString());
        actorId  = new ActorId(sub);
        return true;
    }

    // -----------------------------------------------------------------------
    //  Response DTOs (internal wire shape; camelCase via JsonSerializerOptions.Web)
    // -----------------------------------------------------------------------

    internal sealed record IdentityProfileResponse(
        string  Actor,
        string  DisplayName,
        string  ContactEmail,
        string? PhoneNumber);

    internal sealed record KeyRotationResponse(
        string  Actor,
        string? CurrentFingerprint,
        int     HistoricalKeyCount,
        bool    RotationInProgress,
        string? RotationWindowExpiry);

    internal sealed record RecoveryContactResponse(
        string ContactActorId,
        string DisplayName,
        string VerificationStatus,
        string EnrolledAt);

    internal sealed record RecoveryContactsResponse(
        string                        Actor,
        List<RecoveryContactResponse> Contacts,
        int                           MaxContacts);

    internal sealed record HistoricalKeyEntryResponse(
        string  Fingerprint,
        string  ActivatedAt,
        string? RetiredAt,
        string  RotationReason,
        int     SignatureSurvivalCount);

    internal sealed record HistoricalKeysResponse(
        string                           Actor,
        List<HistoricalKeyEntryResponse> Keys);

    internal sealed record TeamMembershipResponse(
        string TeamId,
        string DisplayName,
        string RoleDisplayName,
        string SubkeyFingerprint);

    internal sealed record ActiveTeamOverviewResponse(
        string                      Actor,
        List<TeamMembershipResponse> Teams,
        string?                     ActiveTeamId);
}
