using Sunfish.Foundation.LocalFirst.Encryption;

namespace Sunfish.Foundation.LocalFirst.Tests;

public class Argon2idKeyDerivationTests
{
    // Keep parameters low so the test is fast (~50 ms) but still exercises Argon2id's
    // memory-hard primitive. Production defaults are validated by construction.
    private static readonly Argon2idOptions s_fast = new(
        MemoryKiB: 1024,
        Iterations: 1,
        Parallelism: 1,
        OutputLengthBytes: 32);

    [Fact]
    public void Same_password_and_salt_yield_identical_keys()
    {
        var kdf = new Argon2idKeyDerivation(s_fast);
        var password = Encoding.UTF8.GetBytes("correct horse battery staple");
        var salt = Encoding.UTF8.GetBytes("sunfish-salt-0123456789ab");

        var a = kdf.DeriveKey(password, salt);
        var b = kdf.DeriveKey(password, salt);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Different_salt_produces_different_key()
    {
        var kdf = new Argon2idKeyDerivation(s_fast);
        var password = Encoding.UTF8.GetBytes("correct horse battery staple");

        var a = kdf.DeriveKey(password, Encoding.UTF8.GetBytes("sunfish-salt-A0123456789a"));
        var b = kdf.DeriveKey(password, Encoding.UTF8.GetBytes("sunfish-salt-B0123456789a"));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Output_length_matches_option()
    {
        var kdf16 = new Argon2idKeyDerivation(s_fast with { OutputLengthBytes = 16 });
        var kdf64 = new Argon2idKeyDerivation(s_fast with { OutputLengthBytes = 64 });

        var pw = Encoding.UTF8.GetBytes("pw");
        var salt = Encoding.UTF8.GetBytes("sunfish-salt-0123456789ab");

        Assert.Equal(16, kdf16.DeriveKey(pw, salt).Length);
        Assert.Equal(64, kdf64.DeriveKey(pw, salt).Length);
    }

    [Fact]
    public void Different_iteration_count_produces_different_key()
    {
        var pw = Encoding.UTF8.GetBytes("pw");
        var salt = Encoding.UTF8.GetBytes("sunfish-salt-0123456789ab");

        var low = new Argon2idKeyDerivation(s_fast with { Iterations = 1 }).DeriveKey(pw, salt);
        var high = new Argon2idKeyDerivation(s_fast with { Iterations = 3 }).DeriveKey(pw, salt);

        Assert.NotEqual(low, high);
    }
}
