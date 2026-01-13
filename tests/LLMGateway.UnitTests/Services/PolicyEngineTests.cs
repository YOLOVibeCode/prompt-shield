using FluentAssertions;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Services;

namespace LLMGateway.UnitTests.Services;

public class PolicyEngineTests
{
    [Fact]
    public void Evaluate_UnrestrictedUser_ReturnsAllow()
    {
        // Arrange
        var policies = new List<UserPolicy>
        {
            new()
            {
                UserId = "executive@company.com",
                AccessLevel = AccessLevel.Unrestricted,
                Enabled = true
            }
        };
        var engine = new SimplePolicyEngine(policies);

        // Act
        var decision = engine.Evaluate("executive@company.com");

        // Assert
        decision.Action.Should().Be(PolicyAction.Allow);
        decision.AccessLevel.Should().Be(AccessLevel.Unrestricted);
    }

    [Fact]
    public void Evaluate_SanitizedOnlyUser_ReturnsAllow()
    {
        // Arrange
        var policies = new List<UserPolicy>
        {
            new()
            {
                UserId = "developer@company.com",
                AccessLevel = AccessLevel.SanitizedOnly,
                Enabled = true
            }
        };
        var engine = new SimplePolicyEngine(policies);

        // Act
        var decision = engine.Evaluate("developer@company.com");

        // Assert
        decision.Action.Should().Be(PolicyAction.Allow);
        decision.AccessLevel.Should().Be(AccessLevel.SanitizedOnly);
    }

    [Fact]
    public void Evaluate_BlockedUser_ReturnsBlock()
    {
        // Arrange
        var policies = new List<UserPolicy>
        {
            new()
            {
                UserId = "contractor@company.com",
                AccessLevel = AccessLevel.Blocked,
                Enabled = true
            }
        };
        var engine = new SimplePolicyEngine(policies);

        // Act
        var decision = engine.Evaluate("contractor@company.com");

        // Assert
        decision.Action.Should().Be(PolicyAction.Block);
        decision.Reason.Should().Contain("blocked");
    }

    [Fact]
    public void Evaluate_DisabledUser_ReturnsBlock()
    {
        // Arrange
        var policies = new List<UserPolicy>
        {
            new()
            {
                UserId = "user@company.com",
                AccessLevel = AccessLevel.SanitizedOnly,
                Enabled = false
            }
        };
        var engine = new SimplePolicyEngine(policies);

        // Act
        var decision = engine.Evaluate("user@company.com");

        // Assert
        decision.Action.Should().Be(PolicyAction.Block);
        decision.Reason.Should().Contain("disabled");
    }

    [Fact]
    public void Evaluate_UnknownUser_ReturnsDefaultPolicy()
    {
        // Arrange
        var policies = new List<UserPolicy>();
        var engine = new SimplePolicyEngine(policies);

        // Act
        var decision = engine.Evaluate("unknown@company.com");

        // Assert
        decision.Action.Should().Be(PolicyAction.Allow);
        decision.AccessLevel.Should().Be(AccessLevel.SanitizedOnly); // Default
    }

    [Fact]
    public void GetPolicy_ExistingUser_ReturnsPolicy()
    {
        // Arrange
        var policies = new List<UserPolicy>
        {
            new()
            {
                UserId = "user@company.com",
                Department = "Engineering",
                AccessLevel = AccessLevel.SanitizedOnly,
                DailyRequestLimit = 750
            }
        };
        var engine = new SimplePolicyEngine(policies);

        // Act
        var policy = engine.GetPolicy("user@company.com");

        // Assert
        policy.Should().NotBeNull();
        policy!.Department.Should().Be("Engineering");
        policy.DailyRequestLimit.Should().Be(750);
    }

    [Fact]
    public void GetPolicy_NonExistentUser_ReturnsNull()
    {
        // Arrange
        var engine = new SimplePolicyEngine(new List<UserPolicy>());

        // Act
        var policy = engine.GetPolicy("unknown@company.com");

        // Assert
        policy.Should().BeNull();
    }
}

