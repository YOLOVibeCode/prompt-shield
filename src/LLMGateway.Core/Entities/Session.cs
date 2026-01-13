namespace LLMGateway.Core.Entities;

public class Session
{
    public required string SessionId { get; init; }
    public required string UserId { get; init; }
    public string? Department { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public DateTime? LastAccessedAt { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    
    public Dictionary<string, string> Mappings { get; init; } = new();
    public Dictionary<string, string> ReverseMappings { get; init; } = new();
    
    public int RequestCount { get; set; }
    
    public int MappingCount => Mappings.Count;
    
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

public enum SessionStatus
{
    Active,
    Expired,
    Terminated
}

