using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;

namespace LLMGateway.Core.Services;

public class SimplePolicyEngine(IEnumerable<UserPolicy> policies) : IPolicyEngine
{
    private readonly Dictionary<string, UserPolicy> _policiesByUser = 
        policies.ToDictionary(p => p.UserId, p => p);

    public PolicyDecision Evaluate(string userId, string? department = null)
    {
        // Get user policy or use default
        var policy = GetPolicy(userId) ?? CreateDefaultPolicy(userId, department);

        // Check if user is enabled
        if (!policy.Enabled)
        {
            return new PolicyDecision(
                Action: PolicyAction.Block,
                Reason: "User account is disabled",
                AccessLevel: policy.AccessLevel);
        }

        // Check access level
        if (policy.AccessLevel == AccessLevel.Blocked)
        {
            return new PolicyDecision(
                Action: PolicyAction.Block,
                Reason: "User access level is blocked",
                AccessLevel: policy.AccessLevel);
        }

        // Allow with appropriate access level
        return new PolicyDecision(
            Action: PolicyAction.Allow,
            Reason: "Access granted",
            AccessLevel: policy.AccessLevel);
    }

    public UserPolicy? GetPolicy(string userId)
    {
        return _policiesByUser.TryGetValue(userId, out var policy) ? policy : null;
    }

    private static UserPolicy CreateDefaultPolicy(string userId, string? department)
    {
        return new UserPolicy
        {
            UserId = userId,
            Department = department,
            AccessLevel = AccessLevel.SanitizedOnly, // Safe default
            DailyRequestLimit = 500,
            HourlyRequestLimit = 50,
            Enabled = true
        };
    }
}

