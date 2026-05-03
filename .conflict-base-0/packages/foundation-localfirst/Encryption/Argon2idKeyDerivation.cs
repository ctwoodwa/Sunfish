using Konscious.Security.Cryptography;

namespace Sunfish.Foundation.LocalFirst.Encryption;

/// <summary>
/// Argon2id key derivation using the <c>Konscious.Security.Cryptography.Argon2</c>
/// NuGet library. This is the standard .NET Argon2 implementation referenced by
/// the paper's threat model (§11.2).
/// </summary>
public sealed class Argon2idKeyDerivation : IKeyDerivation
{
    /// <summary>Options (memory, iterations, parallelism, output length).</summary>
    public Argon2idOptions Options { get; }

    /// <summary>Initializes a new instance with the given parameters.</summary>
    public Argon2idKeyDerivation(Argon2idOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MemoryKiB <= 0) throw new ArgumentOutOfRangeException(nameof(options), "MemoryKiB must be positive.");
        if (options.Iterations <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Iterations must be positive.");
        if (options.Parallelism <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Parallelism must be positive.");
        if (options.OutputLengthBytes <= 0) throw new ArgumentOutOfRangeException(nameof(options), "OutputLengthBytes must be positive.");
        Options = options;
    }

    /// <inheritdoc />
    public byte[] DeriveKey(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt)
    {
        // Konscious requires byte[] inputs; we copy the spans once per call.
        var passwordBytes = password.ToArray();
        var saltBytes = salt.ToArray();

        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = saltBytes,
            DegreeOfParallelism = Options.Parallelism,
            MemorySize = Options.MemoryKiB,
            Iterations = Options.Iterations,
        };

        return argon2.GetBytes(Options.OutputLengthBytes);
    }
}
