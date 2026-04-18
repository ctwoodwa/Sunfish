using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sunfish.Federation.Common.Kubo;

namespace Sunfish.Federation.Common.Extensions;

/// <summary>
/// Hosted service that runs federation-related startup posture checks before the application is
/// ready to serve. In production, verifies the presence of a swarm key and (when a Kubo health
/// probe is registered) that the Kubo daemon is in private-network mode.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="kuboHealth"/> is optional — the blob-replication package (Task D-5) registers
/// the probe. When the probe is null, startup logs a warning and continues. Full private-network
/// enforcement requires the complete wiring that lands with Task D-5.
/// </para>
/// </remarks>
public sealed class FederationStartupChecks(
    IOptions<FederationOptions> options,
    ILogger<FederationStartupChecks> logger,
    IKuboHealthProbe? kuboHealth = null) : IHostedService
{
    private readonly IOptions<FederationOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<FederationStartupChecks> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IKuboHealthProbe? _kuboHealth = kuboHealth;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken ct)
    {
        var opts = _options.Value;

        if (opts.Environment != FederationEnvironment.Production)
        {
            _logger.LogInformation(
                "Federation environment is {Env}; skipping private-network enforcement. Acceptable for dev/test only.",
                opts.Environment);
            return;
        }

        if (string.IsNullOrEmpty(opts.SwarmKeyPath))
        {
            _logger.LogCritical(
                "FATAL: Production federation requires a swarm key. Set Sunfish:Federation:SwarmKeyPath to a file " +
                "containing a 32-byte hex-encoded IPFS swarm key (see docs/federation/kubo-sidecar-dependency.md).");
            throw new InvalidOperationException("Swarm key required in production federation environment.");
        }

        if (_kuboHealth is null)
        {
            _logger.LogWarning(
                "Kubo health probe is not registered. Blob-replication infrastructure (Task D-5 / IpfsBlobStore) has " +
                "not yet been wired. Private-network enforcement is partial until that ships.");
            return;
        }

        var cfg = await _kuboHealth.GetConfigAsync(ct).ConfigureAwait(false);
        if (!string.Equals(cfg.NetworkProfile, "private", StringComparison.Ordinal))
        {
            _logger.LogCritical(
                "FATAL: Kubo daemon at {Address} reports NetworkProfile={Profile}; expected 'private'. Refusing to start.",
                opts.KuboRpcAddress,
                cfg.NetworkProfile);
            throw new InvalidOperationException("Kubo daemon is not running in private-network mode.");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
