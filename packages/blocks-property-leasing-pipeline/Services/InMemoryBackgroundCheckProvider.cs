using System.Collections.Concurrent;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Services;

/// <summary>
/// Test/demo <see cref="IBackgroundCheckProvider"/>. By default returns
/// <see cref="BackgroundCheckOutcome.Clear"/> for unseeded SSN
/// identifiers; tests seed canned outcomes via <see cref="SeedFindings"/>
/// or <see cref="SeedError"/> to exercise the FCRA-decline path.
/// **NOT for production**.
/// </summary>
public sealed class InMemoryBackgroundCheckProvider : IBackgroundCheckProvider
{
    private readonly ConcurrentDictionary<string, BackgroundCheckResult> _byVendorRef = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<AdverseFinding>> _seedFindings = new();
    private readonly HashSet<string> _seedErrors = new();
    private readonly TimeProvider _time;

    /// <summary>Creates a provider using the system clock.</summary>
    public InMemoryBackgroundCheckProvider() : this(time: null) { }

    /// <summary>Creates a provider with a custom time source (useful for deterministic tests).</summary>
    public InMemoryBackgroundCheckProvider(TimeProvider? time)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Pre-seeds findings for an SSN identifier — the next <see cref="KickOffAsync"/> with that identifier returns these findings.</summary>
    public void SeedFindings(string ssnIdentifier, IReadOnlyList<AdverseFinding> findings)
    {
        ArgumentException.ThrowIfNullOrEmpty(ssnIdentifier);
        ArgumentNullException.ThrowIfNull(findings);
        _seedFindings[ssnIdentifier] = findings;
    }

    /// <summary>Pre-seeds an error response for an SSN identifier.</summary>
    public void SeedError(string ssnIdentifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(ssnIdentifier);
        _seedErrors.Add(ssnIdentifier);
    }

    /// <inheritdoc />
    public Task<BackgroundCheckResult> KickOffAsync(BackgroundCheckRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var vendorRef = $"in-memory-bg:{Guid.NewGuid():N}";
        var now = _time.GetUtcNow();

        BackgroundCheckResult result;
        if (_seedErrors.Contains(request.SocialSecurityIdentifier))
        {
            result = new BackgroundCheckResult
            {
                VendorRef = vendorRef,
                Application = request.Application,
                Outcome = BackgroundCheckOutcome.Error,
                Findings = Array.Empty<AdverseFinding>(),
                CompletedAt = now,
            };
        }
        else if (_seedFindings.TryGetValue(request.SocialSecurityIdentifier, out var findings))
        {
            result = new BackgroundCheckResult
            {
                VendorRef = vendorRef,
                Application = request.Application,
                Outcome = BackgroundCheckOutcome.HasFindings,
                Findings = findings,
                CompletedAt = now,
            };
        }
        else
        {
            result = new BackgroundCheckResult
            {
                VendorRef = vendorRef,
                Application = request.Application,
                Outcome = BackgroundCheckOutcome.Clear,
                Findings = Array.Empty<AdverseFinding>(),
                CompletedAt = now,
            };
        }

        _byVendorRef[vendorRef] = result;
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<BackgroundCheckResult> GetStatusAsync(string vendorRef, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(vendorRef);
        ct.ThrowIfCancellationRequested();
        if (!_byVendorRef.TryGetValue(vendorRef, out var result))
        {
            throw new InvalidOperationException($"Unknown vendor ref '{vendorRef}'.");
        }
        return Task.FromResult(result);
    }
}
