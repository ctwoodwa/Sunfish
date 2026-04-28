namespace Sunfish.Kernel.Audit;

/// <summary>
/// Default in-process <see cref="IAuditEventStream"/> backed by a list and a
/// list of subscribers. Direct parallel to the corresponding stream in
/// <c>Sunfish.Kernel.Ledger</c>.
/// </summary>
internal sealed class InMemoryAuditEventStream : IAuditEventStream
{
    private readonly object _gate = new();
    private readonly List<AuditRecord> _records = new();
    private readonly List<Action<AuditRecord>> _subscribers = new();

    public IReadOnlyList<AuditRecord> ReplayAll()
    {
        lock (_gate)
        {
            return _records.ToArray();
        }
    }

    public IDisposable Subscribe(Action<AuditRecord> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            _subscribers.Add(handler);
        }
        return new Subscription(this, handler);
    }

    internal void Publish(AuditRecord record)
    {
        Action<AuditRecord>[] snapshot;
        lock (_gate)
        {
            _records.Add(record);
            snapshot = _subscribers.ToArray();
        }
        foreach (var handler in snapshot)
        {
            handler(record);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly InMemoryAuditEventStream _owner;
        private readonly Action<AuditRecord> _handler;
        private bool _disposed;

        public Subscription(InMemoryAuditEventStream owner, Action<AuditRecord> handler)
        {
            _owner = owner;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_owner._gate)
            {
                _owner._subscribers.Remove(_handler);
            }
        }
    }
}
