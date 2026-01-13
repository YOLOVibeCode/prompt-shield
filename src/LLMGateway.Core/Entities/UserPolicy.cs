namespace LLMGateway.Core.Entities;

public class UserPolicy
{
    public required string UserId { get; init; }
    public string? Department { get; init; }
    public AccessLevel AccessLevel { get; init; } = AccessLevel.SanitizedOnly;
    public int DailyRequestLimit { get; init; } = 500;
    public int HourlyRequestLimit { get; init; } = 50;
    public bool Enabled { get; init; } = true;
}

public enum AccessLevel
{
    Blocked,         // No LLM access
    SanitizedOnly,   // Must pass through sanitization
    Unrestricted     // Direct access (for executives/security)
}

