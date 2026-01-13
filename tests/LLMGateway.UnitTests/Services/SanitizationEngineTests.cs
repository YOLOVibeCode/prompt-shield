using FluentAssertions;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Services;

namespace LLMGateway.UnitTests.Services;

public class SanitizationEngineTests
{
    private readonly ISanitizationEngine _engine;
    private readonly IAliasGenerator _aliasGenerator;

    public SanitizationEngineTests()
    {
        _aliasGenerator = new SimpleAliasGenerator();
        _engine = new SanitizationEngine(_aliasGenerator);
    }

    [Fact]
    public void Sanitize_WithServerName_ReturnsAlias()
    {
        // Arrange
        var content = "Connect to ServerDB01";
        var session = CreateSession();
        var rules = new[]
        {
            new SanitizationRule
            {
                Name = "SERVER_NAMES",
                Pattern = @"(?i)(ServerDB\d+)",
                Prefix = "SERVER",
                Severity = ViolationSeverity.Medium
            }
        };

        // Act
        var result = _engine.Sanitize(content, session, rules);

        // Assert
        result.WasSanitized.Should().BeTrue();
        result.SanitizedContent.Should().Be("Connect to SERVER_0");
        result.MappingsCreated.Should().ContainKey("ServerDB01");
        result.MappingsCreated["ServerDB01"].Should().Be("SERVER_0");
        result.Violations.Should().HaveCount(1);
        result.Violations[0].RuleName.Should().Be("SERVER_NAMES");
    }

    [Fact]
    public void Sanitize_WithoutSensitiveData_ReturnsUnchanged()
    {
        // Arrange
        var content = "Just a normal query";
        var session = CreateSession();
        var rules = new[]
        {
            new SanitizationRule
            {
                Name = "SERVER_NAMES",
                Pattern = @"(?i)(ServerDB\d+)",
                Prefix = "SERVER",
                Severity = ViolationSeverity.Medium
            }
        };

        // Act
        var result = _engine.Sanitize(content, session, rules);

        // Assert
        result.WasSanitized.Should().BeFalse();
        result.SanitizedContent.Should().Be(content);
        result.MappingsCreated.Should().BeEmpty();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_WithMultipleMatches_CreatesMultipleAliases()
    {
        // Arrange
        var content = "Join ServerDB01 and ServerDB02";
        var session = CreateSession();
        var rules = new[]
        {
            new SanitizationRule
            {
                Name = "SERVER_NAMES",
                Pattern = @"(?i)(ServerDB\d+)",
                Prefix = "SERVER",
                Severity = ViolationSeverity.Medium
            }
        };

        // Act
        var result = _engine.Sanitize(content, session, rules);

        // Assert
        result.WasSanitized.Should().BeTrue();
        result.SanitizedContent.Should().Be("Join SERVER_0 and SERVER_1");
        result.MappingsCreated.Should().HaveCount(2);
        result.MappingsCreated["ServerDB01"].Should().Be("SERVER_0");
        result.MappingsCreated["ServerDB02"].Should().Be("SERVER_1");
    }

    [Fact]
    public void Sanitize_WithSameValueTwice_UsesSameAlias()
    {
        // Arrange
        var content = "Query ServerDB01 and check ServerDB01 again";
        var session = CreateSession();
        var rules = new[]
        {
            new SanitizationRule
            {
                Name = "SERVER_NAMES",
                Pattern = @"(?i)(ServerDB\d+)",
                Prefix = "SERVER",
                Severity = ViolationSeverity.Medium
            }
        };

        // Act
        var result = _engine.Sanitize(content, session, rules);

        // Assert
        result.WasSanitized.Should().BeTrue();
        result.SanitizedContent.Should().Be("Query SERVER_0 and check SERVER_0 again");
        result.MappingsCreated.Should().HaveCount(1);
    }

    [Fact]
    public void Sanitize_WithExistingMapping_ReusesAlias()
    {
        // Arrange
        var content = "Query ServerDB01";
        var session = CreateSession();
        session.Mappings.Add("ServerDB01", "SERVER_0");
        session.ReverseMappings.Add("SERVER_0", "ServerDB01");

        var rules = new[]
        {
            new SanitizationRule
            {
                Name = "SERVER_NAMES",
                Pattern = @"(?i)(ServerDB\d+)",
                Prefix = "SERVER",
                Severity = ViolationSeverity.Medium
            }
        };

        // Act
        var result = _engine.Sanitize(content, session, rules);

        // Assert
        result.SanitizedContent.Should().Be("Query SERVER_0");
        result.MappingsCreated.Should().BeEmpty(); // No new mappings
    }

    [Fact]
    public void Sanitize_WithException_DoesNotSanitize()
    {
        // Arrange
        var content = "Check server_error logs";
        var session = CreateSession();
        var rules = new[]
        {
            new SanitizationRule
            {
                Name = "SERVER_NAMES",
                Pattern = @"(?i)(server_\w+)",
                Prefix = "SERVER",
                Severity = ViolationSeverity.Medium,
                Exceptions = new[] { "server_error", "server_logs" }
            }
        };

        // Act
        var result = _engine.Sanitize(content, session, rules);

        // Assert
        result.WasSanitized.Should().BeFalse();
        result.SanitizedContent.Should().Be(content);
        result.MappingsCreated.Should().BeEmpty();
    }

    private static Session CreateSession()
    {
        return new Session
        {
            SessionId = "test-session",
            UserId = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        };
    }
}

