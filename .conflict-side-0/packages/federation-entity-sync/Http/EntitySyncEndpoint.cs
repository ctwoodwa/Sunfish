using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sunfish.Federation.Common;

namespace Sunfish.Federation.EntitySync.Http;

/// <summary>
/// ASP.NET Core endpoint-mapping helpers for the HTTP+JSON sync transport. Mounts a single
/// POST endpoint at <see cref="HttpSyncTransport.EndpointPath"/> which deserializes incoming
/// envelopes, dispatches them to the locally-registered handler via
/// <see cref="ILocalHandlerDispatcher"/>, and returns the signed reply as JSON.
/// </summary>
public static class EntitySyncEndpoint
{
    /// <summary>
    /// Maps <c>POST /.well-known/sunfish/federation/entity-sync</c> onto <paramref name="endpoints"/>.
    /// </summary>
    /// <returns><paramref name="endpoints"/>, so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapEntitySyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(HttpSyncTransport.EndpointPath, HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(HttpContext ctx, ILocalHandlerDispatcher dispatcher)
    {
        SyncEnvelopeDto? dto;
        try
        {
            dto = await ctx.Request
                .ReadFromJsonAsync<SyncEnvelopeDto>(ctx.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            return Results.BadRequest($"Malformed envelope JSON: {ex.Message}");
        }

        if (dto is null)
            return Results.BadRequest("Empty envelope body.");

        SyncEnvelope incoming;
        try
        {
            incoming = dto.ToEnvelope();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return Results.BadRequest($"Invalid envelope fields: {ex.Message}");
        }

        SyncEnvelope reply;
        try
        {
            reply = await dispatcher.DispatchAsync(incoming, ctx.RequestAborted).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            // No handler for target peer → this process does not host that peer.
            return Results.Problem(
                detail: ex.Message,
                statusCode: (int)HttpStatusCode.NotFound,
                title: "No handler for target peer");
        }

        return Results.Json(SyncEnvelopeDto.From(reply));
    }
}
