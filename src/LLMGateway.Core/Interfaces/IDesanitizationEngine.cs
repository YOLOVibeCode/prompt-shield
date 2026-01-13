using LLMGateway.Core.Entities;

namespace LLMGateway.Core.Interfaces;

/// <summary>
/// Reverses aliases back to original values using session mappings
/// </summary>
public interface IDesanitizationEngine
{
    /// <summary>
    /// Replaces all aliases in content with their original values from the session
    /// </summary>
    DesanitizationResult Desanitize(string content, Session session);
}

public record DesanitizationResult(
    string DesanitizedContent,
    int ReplacementsCount,
    List<string> UnmatchedAliases);
