using LLMGateway.Core.Entities;

namespace LLMGateway.Core.Interfaces;

/// <summary>
/// Evaluates user access policies
/// </summary>
public interface IPolicyEngine
{
    /// <summary>
    /// Evaluates if a request should be allowed based on user policy
    /// </summary>
    PolicyDecision Evaluate(string userId, string? department = null);
    
    /// <summary>
    /// Gets the policy for a user
    /// </summary>
    UserPolicy? GetPolicy(string userId);
}

public record PolicyDecision(
    PolicyAction Action,
    string Reason,
    AccessLevel AccessLevel);

public enum PolicyAction
{
    Allow,
    AllowWithWarning,
    Block
}

