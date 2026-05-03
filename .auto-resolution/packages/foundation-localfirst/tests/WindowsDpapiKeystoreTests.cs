using Sunfish.Foundation.LocalFirst.Encryption;

// DPAPI APIs are Windows-only. Each test guards at runtime with OperatingSystem.IsWindows();
// suppressing CA1416 here avoids decorating every xUnit [Fact] with [SupportedOSPlatform].
#pragma warning disable CA1416

namespace Sunfish.Foundation.LocalFirst.Tests;

/// <summary>
/// DPAPI keystore tests. These only run on Windows — on other platforms DPAPI
/// is unavailable and the tests short-circuit to a no-op pass.
/// </summary>
public class WindowsDpapiKeystoreTests : IDisposable
{
    private readonly string _storageDir = Path.Combine(Path.GetTempPath(), "sunfish-dpapi-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_storageDir))
            {
                Directory.Delete(_storageDir, recursive: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private static bool IsWindows => OperatingSystem.IsWindows();

    [Fact]
    public async Task SetKey_then_GetKey_roundtrips()
    {
        if (!IsWindows) return;

        var keystore = new WindowsDpapiKeystore(_storageDir);
        var key = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        await keystore.SetKeyAsync("test-key", key, CancellationToken.None);
        var actual = await keystore.GetKeyAsync("test-key", CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(key, actual!.Value.ToArray());
    }

    [Fact]
    public async Task DeleteKey_removes_the_stored_key()
    {
        if (!IsWindows) return;

        var keystore = new WindowsDpapiKeystore(_storageDir);
        await keystore.SetKeyAsync("doomed", new byte[] { 9, 9, 9 }, CancellationToken.None);

        await keystore.DeleteKeyAsync("doomed", CancellationToken.None);

        Assert.Null(await keystore.GetKeyAsync("doomed", CancellationToken.None));
    }

    [Fact]
    public async Task GetKey_for_unknown_name_returns_null()
    {
        if (!IsWindows) return;

        var keystore = new WindowsDpapiKeystore(_storageDir);
        Assert.Null(await keystore.GetKeyAsync("does-not-exist", CancellationToken.None));
    }
}
