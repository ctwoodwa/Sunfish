using Sunfish.Federation.BlobReplication.Kubo;
using Sunfish.Federation.Common.Kubo;

namespace Sunfish.Federation.BlobReplication;

/// <summary>
/// <see cref="IKuboHealthProbe"/> implementation backed by the Kubo HTTP RPC. Reports
/// <c>"private"</c> when the daemon has a populated <c>Swarm.SwarmKey</c> config field and
/// <c>"public"</c> otherwise.
/// </summary>
/// <remarks>
/// <para>
/// This is the probe that <c>FederationStartupChecks</c> consumes: when a production federation
/// node boots, the check asserts <c>NetworkProfile == "private"</c> and refuses to start otherwise.
/// The probe issues two cheap calls — <c>/api/v0/id</c> for the daemon version and
/// <c>/api/v0/config/show</c> for the swarm key state — on every invocation; callers that need
/// to probe on a hot path should cache the result.
/// </para>
/// </remarks>
public sealed class KuboHealthProbe : IKuboHealthProbe
{
    private readonly IKuboHttpClient _kubo;

    /// <summary>Constructs a health probe that issues HTTP calls via <paramref name="kubo"/>.</summary>
    public KuboHealthProbe(IKuboHttpClient kubo)
    {
        ArgumentNullException.ThrowIfNull(kubo);
        _kubo = kubo;
    }

    /// <inheritdoc />
    public async ValueTask<KuboNetworkInfo> GetConfigAsync(CancellationToken ct)
    {
        var idTask = _kubo.IdAsync(ct);
        var configTask = _kubo.GetConfigAsync(ct);
        await Task.WhenAll(idTask, configTask).ConfigureAwait(false);

        var id = await idTask.ConfigureAwait(false);
        var config = await configTask.ConfigureAwait(false);

        var profile = string.IsNullOrEmpty(config.Swarm?.SwarmKey) ? "public" : "private";
        return new KuboNetworkInfo(NetworkProfile: profile, Version: id.AgentVersion);
    }
}
