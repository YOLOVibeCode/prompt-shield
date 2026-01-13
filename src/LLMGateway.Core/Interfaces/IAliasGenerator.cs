namespace LLMGateway.Core.Interfaces;

/// <summary>
/// Generates unique aliases for sensitive values
/// </summary>
public interface IAliasGenerator
{
    /// <summary>
    /// Generates a unique alias with the given prefix
    /// </summary>
    string GenerateAlias(string prefix, int counter);
}

