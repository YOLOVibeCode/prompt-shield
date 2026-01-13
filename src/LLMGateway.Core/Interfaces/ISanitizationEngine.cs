using LLMGateway.Core.Entities;

namespace LLMGateway.Core.Interfaces;

/// <summary>
/// Sanitizes content by replacing sensitive data with aliases
/// </summary>
public interface ISanitizationEngine
{
    /// <summary>
    /// Sanitizes the content and returns the result with created mappings
    /// </summary>
    SanitizationResult Sanitize(string content, Session session, IEnumerable<SanitizationRule> rules);
}

public record SanitizationResult(
    string SanitizedContent,
    bool WasSanitized,
    Dictionary<string, string> MappingsCreated,
    List<Violation> Violations);

public record Violation(
    string RuleName,
    ViolationSeverity Severity,
    string MatchedValue);

