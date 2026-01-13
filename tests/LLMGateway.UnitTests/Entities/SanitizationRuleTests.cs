using FluentAssertions;
using LLMGateway.Core.Entities;

namespace LLMGateway.UnitTests.Entities;

public class SanitizationRuleTests
{
    [Fact]
    public void SanitizationRule_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var rule = new SanitizationRule
        {
            Name = "SERVER_NAMES",
            Pattern = @"(?i)(ServerDB|ProductionDB|\w+db\d+)",
            Prefix = "SERVER",
            Severity = ViolationSeverity.Medium
        };

        // Assert
        rule.Name.Should().Be("SERVER_NAMES");
        rule.Pattern.Should().Be(@"(?i)(ServerDB|ProductionDB|\w+db\d+)");
        rule.Prefix.Should().Be("SERVER");
        rule.Severity.Should().Be(ViolationSeverity.Medium);
        rule.Enabled.Should().BeTrue(); // Default
    }

    [Fact]
    public void SanitizationRule_ShouldSupportExceptions()
    {
        // Arrange & Act
        var rule = new SanitizationRule
        {
            Name = "SERVER_NAMES",
            Pattern = @"(?i)(ServerDB|ProductionDB)",
            Prefix = "SERVER",
            Severity = ViolationSeverity.Medium,
            Exceptions = new[] { "server_error", "server_logs" }
        };

        // Assert
        rule.Exceptions.Should().HaveCount(2);
        rule.Exceptions.Should().Contain("server_error");
        rule.Exceptions.Should().Contain("server_logs");
    }

    [Fact]
    public void SanitizationRule_ShouldHaveEmptyExceptionsByDefault()
    {
        // Arrange & Act
        var rule = new SanitizationRule
        {
            Name = "TEST_RULE",
            Pattern = "test",
            Prefix = "TEST",
            Severity = ViolationSeverity.Low
        };

        // Assert
        rule.Exceptions.Should().NotBeNull();
        rule.Exceptions.Should().BeEmpty();
    }
}

