namespace Proyecto_HobbyHub.Security;

// Process-wide bounded sampling; never allocate buckets using attacker supplied values.
public sealed class AuditEventBudget(TimeProvider time)
{
    private readonly object gate = new();
    private DateTimeOffset window;
    private int count;
    public bool Allow()
    {
        lock (gate)
        {
            var now = time.GetUtcNow();
            if (now - window >= TimeSpan.FromMinutes(1)) { window = now; count = 0; }
            return ++count <= 30;
        }
    }
}
