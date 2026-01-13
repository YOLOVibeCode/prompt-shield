using System.Collections.Concurrent;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;

namespace LLMGateway.Core.Services;

public class InMemoryAuditLogger : IAuditLogger
{
    private readonly ConcurrentBag<AuditEntry> _entries = new();

    public void Log(AuditEntry entry)
    {
        _entries.Add(entry);
    }

    public List<AuditEntry> GetRecentEntries(int count = 100)
    {
        return _entries
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();
    }
}

