using System.Runtime.InteropServices;

namespace Sunfish.Kernel.Events;

/// <summary>
/// Options for <see cref="FileBackedEventLog"/>. Wired through the standard
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> pipeline via
/// <c>AddSunfishEventLog</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Default directory:</b> Windows uses <c>%LOCALAPPDATA%\Sunfish\event-log\</c>; other platforms
/// use <c>$XDG_DATA_HOME/sunfish/event-log/</c> when <c>XDG_DATA_HOME</c> is set, falling back to
/// <c>~/.local/share/sunfish/event-log/</c>. The directory is created on first append.
/// </para>
/// <para>
/// <b>Flush policy:</b> <see cref="FlushIntervalMilliseconds"/> at zero makes every <c>AppendAsync</c>
/// fsync before returning — the strongest durability guarantee. Non-zero values defer fsyncs to a
/// background flush; useful for high-throughput scenarios where a small risk of losing the last
/// millisecond of appends is acceptable (e.g. derived read models).
/// </para>
/// </remarks>
public sealed class EventLogOptions
{
    /// <summary>
    /// Root directory for event-log files. Each epoch gets its own
    /// <c>events-{epochId}[-{part}].log</c> files plus <c>snapshot-*.cbor</c> files.
    /// </summary>
    public string Directory { get; set; } = DefaultDirectory();

    /// <summary>
    /// The epoch this log writes to. Events are appended to <c>events-{EpochId}.log</c>
    /// (or rolled-over parts). See paper §7 for the epoch concept.
    /// </summary>
    public string EpochId { get; set; } = "epoch-0";

    /// <summary>
    /// How often to fsync the log file. <c>0</c> (default) means synchronous fsync on every
    /// <c>AppendAsync</c> — the safest option. Non-zero values defer fsyncs to a background flush.
    /// </summary>
    public int FlushIntervalMilliseconds { get; set; }

    /// <summary>
    /// Maximum size (in bytes) of a single event-log file before rolling over to a new part.
    /// Default 1 GiB. Rollover produces <c>events-{EpochId}-{part}.log</c> with part numbers
    /// starting at 1 (the initial file is un-parted).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 1_073_741_824;

    /// <summary>Returns the platform-conventional default directory for event-log files.</summary>
    public static string DefaultDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
            {
                localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local");
            }
            return Path.Combine(localAppData, "Sunfish", "event-log");
        }

        // POSIX — XDG-aware fallback to ~/.local/share/sunfish/event-log/
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(xdg))
        {
            return Path.Combine(xdg, "sunfish", "event-log");
        }
        var home = Environment.GetEnvironmentVariable("HOME")
                   ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "sunfish", "event-log");
    }
}
