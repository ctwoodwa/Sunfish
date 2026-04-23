using System.Diagnostics;

namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Thin abstraction over <see cref="Process.Start(ProcessStartInfo)"/> used by
/// <see cref="TenantProcessSupervisor"/> (Wave 5.2.C.1). Extracted as an
/// interface so unit tests can inject a fake starter that never spawns a real
/// OS process — the default
/// <see cref="SystemDiagnosticsProcessStarter"/> delegates straight to
/// <see cref="Process.Start(ProcessStartInfo)"/> and is exercised in Wave 5.2.E
/// integration tests instead.
/// </summary>
/// <remarks>
/// The surface is deliberately minimal: a single <see cref="Start"/> method
/// that returns an <see cref="IProcessHandle"/>. The handle wraps the operations
/// the supervisor actually needs (kill, exit notification, exit code) so tests
/// can implement it without holding a real <see cref="Process"/>.
/// </remarks>
public interface IProcessStarter
{
    /// <summary>
    /// Start a child process with the supplied <see cref="ProcessStartInfo"/>
    /// and return an <see cref="IProcessHandle"/> wrapping it. Throws on
    /// spawn failure (same semantics as <see cref="Process.Start(ProcessStartInfo)"/>).
    /// </summary>
    IProcessHandle Start(ProcessStartInfo startInfo);
}

/// <summary>
/// Test-friendly handle over a started process. Only exposes the operations
/// <see cref="TenantProcessSupervisor"/> needs.
/// </summary>
public interface IProcessHandle : IDisposable
{
    /// <summary>OS-assigned process id.</summary>
    int Id { get; }

    /// <summary>True if the underlying process has exited.</summary>
    bool HasExited { get; }

    /// <summary>Exit code once <see cref="HasExited"/> is true. Undefined while the process is still running.</summary>
    int ExitCode { get; }

    /// <summary>
    /// Raised when the process exits. Implementations SHOULD set
    /// <c>EnableRaisingEvents = true</c> so handlers fire on the OS-reported
    /// exit, not only on polling.
    /// </summary>
    event EventHandler? Exited;

    /// <summary>
    /// Kill the process and its entire tree. Best-effort; swallows
    /// <see cref="InvalidOperationException"/> on already-exited processes so
    /// callers don't have to race-guard.
    /// </summary>
    void Kill(bool entireProcessTree);
}

/// <summary>
/// Production <see cref="IProcessStarter"/> that delegates straight to
/// <see cref="Process.Start(ProcessStartInfo)"/>. Registered as a singleton by
/// <see cref="ServiceCollectionExtensions.AddBridgeOrchestrationSupervisor"/>.
/// </summary>
public sealed class SystemDiagnosticsProcessStarter : IProcessStarter
{
    /// <inheritdoc />
    public IProcessHandle Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"Process.Start returned null for '{startInfo.FileName}'.");
        return new SystemProcessHandle(process);
    }

    private sealed class SystemProcessHandle : IProcessHandle
    {
        private readonly Process _process;
        private bool _disposed;

        public SystemProcessHandle(Process process)
        {
            _process = process;
            // Enable OS-reported exit events; Process.Exited fires once when
            // the OS reaps the child. Wired before any consumer subscribes,
            // so late subscribers still fire via the underlying Process event.
            _process.EnableRaisingEvents = true;
        }

        public int Id => _process.Id;

        public bool HasExited
        {
            get
            {
                try
                {
                    return _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    // Process.HasExited throws if no process is associated
                    // (e.g. after Dispose); treat as exited so callers don't
                    // loop forever.
                    return true;
                }
            }
        }

        public int ExitCode => _process.ExitCode;

        public event EventHandler? Exited
        {
            add => _process.Exited += value;
            remove => _process.Exited -= value;
        }

        public void Kill(bool entireProcessTree)
        {
            try
            {
                _process.Kill(entireProcessTree);
            }
            catch (InvalidOperationException)
            {
                // Already exited; caller's post-conditions are satisfied.
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Access denied / process already gone on Windows.
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _process.Dispose();
        }
    }
}
