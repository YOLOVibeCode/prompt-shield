namespace LLMGateway.Core.Entities;

public class AuditEntry
{
    public required string EntryId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string UserId { get; init; }
    public string? SessionId { get; init; }
    public string? Department { get; init; }
    public required bool WasSanitized { get; init; }
    public required bool WasDesanitized { get; init; }
    public required string ActionTaken { get; init; }
    public int ProcessingTimeMs { get; init; }
    public List<string> ViolationTypes { get; init; } = new();
}

