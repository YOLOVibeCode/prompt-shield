namespace LLMGateway.Core.Entities;

public class SanitizationRule
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Pattern { get; init; }
    public required string Prefix { get; init; }
    public required ViolationSeverity Severity { get; init; }
    public bool Enabled { get; init; } = true;
    public string[] Exceptions { get; init; } = Array.Empty<string>();
    public int Order { get; init; }
}

public enum ViolationSeverity
{
    Low,
    Medium,
    High,
    Critical
}

