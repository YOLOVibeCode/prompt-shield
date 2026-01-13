using LLMGateway.Core.Entities;

namespace LLMGateway.Core.Interfaces;

/// <summary>
/// Logs audit entries for compliance
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs an audit entry
    /// </summary>
    void Log(AuditEntry entry);
    
    /// <summary>
    /// Gets recent audit entries
    /// </summary>
    List<AuditEntry> GetRecentEntries(int count = 100);
}

