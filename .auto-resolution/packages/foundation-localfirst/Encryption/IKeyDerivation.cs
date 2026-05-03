namespace Sunfish.Foundation.LocalFirst.Encryption;

/// <summary>
/// Derives a fixed-length symmetric key from a password and salt. Paper §11.2:
/// "Keys are derived from user credentials (Argon2id) and stored in OS-native
/// keystores."
/// </summary>
public interface IKeyDerivation
{
    /// <summary>
    /// Derive a key from <paramref name="password"/> and <paramref name="salt"/>.
    /// The output length is implementation-defined (default 32 bytes / 256 bits,
    /// suitable for AES-256 / SQLCipher).
    /// </summary>
    byte[] DeriveKey(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt);
}

/// <summary>
/// Argon2id parameters. Defaults follow the OWASP Password Storage Cheat Sheet
/// recommendation of <c>m=65536 KiB (64 MB), t=3, p=4</c>. Callers with tighter
/// resource budgets (mobile, embedded) may lower these, but not below
/// OWASP's fallback of <c>m=19456 KiB, t=2, p=1</c>.
/// </summary>
/// <param name="MemoryKiB">Memory cost in KiB. Default 64 MiB.</param>
/// <param name="Iterations">Time cost (iterations). Default 3.</param>
/// <param name="Parallelism">Degree of parallelism. Default 4.</param>
/// <param name="OutputLengthBytes">Derived key length in bytes. Default 32.</param>
public sealed record Argon2idOptions(
    int MemoryKiB = 65_536,
    int Iterations = 3,
    int Parallelism = 4,
    int OutputLengthBytes = 32);
