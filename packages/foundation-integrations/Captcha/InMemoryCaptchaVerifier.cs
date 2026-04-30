using System.Collections.Concurrent;
using System.Net;

namespace Sunfish.Foundation.Integrations.Captcha;

/// <summary>
/// W#28 Phase 3 stub <see cref="ICaptchaVerifier"/>. Drives test scenarios
/// without contacting any provider. Tokens are pre-seeded with a verdict
/// via <see cref="Seed(string, double)"/>; unseeded tokens fail by
/// default.
/// </summary>
/// <remarks>
/// Records every call in an in-memory journal for assertion in tests.
/// The journal exposes the verified token + client IP. **NOT for
/// production** — production deployments wire the
/// <c>providers-recaptcha</c> (or similar) adapter from Phase 3.1.
/// </remarks>
public sealed class InMemoryCaptchaVerifier : ICaptchaVerifier
{
    private readonly ConcurrentDictionary<string, double> _seeds = new();
    private readonly ConcurrentBag<(string Token, IPAddress ClientIp)> _calls = new();
    private readonly double _minPassingScore;

    /// <summary>Snapshot of every verify call (token + client IP) for test assertions.</summary>
    public IReadOnlyCollection<(string Token, IPAddress ClientIp)> Calls => _calls;

    /// <summary>Initialises a verifier with the standard 0.3 minimum passing score.</summary>
    public InMemoryCaptchaVerifier() : this(minPassingScore: 0.3) { }

    /// <summary>Initialises a verifier with a custom minimum passing score.</summary>
    public InMemoryCaptchaVerifier(double minPassingScore)
    {
        if (minPassingScore < 0 || minPassingScore > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minPassingScore), "Score must be in [0.0, 1.0].");
        }
        _minPassingScore = minPassingScore;
    }

    /// <summary>
    /// Pre-seeds a token with a score; subsequent <see cref="VerifyAsync"/>
    /// calls with this token return <see cref="CaptchaVerifyResult.Passed"/>
    /// based on whether the seeded score meets the minimum.
    /// </summary>
    public void Seed(string token, double score)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        if (score < 0 || score > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be in [0.0, 1.0].");
        }
        _seeds[token] = score;
    }

    /// <inheritdoc />
    public Task<CaptchaVerifyResult> VerifyAsync(string token, IPAddress clientIp, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentNullException.ThrowIfNull(clientIp);
        ct.ThrowIfCancellationRequested();

        _calls.Add((token, clientIp));

        if (!_seeds.TryGetValue(token, out var score))
        {
            return Task.FromResult(new CaptchaVerifyResult(Passed: false, Score: 0.0, Provider: "in-memory"));
        }

        return Task.FromResult(new CaptchaVerifyResult(
            Passed: score >= _minPassingScore,
            Score: score,
            Provider: "in-memory"));
    }
}
