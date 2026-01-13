using FluentAssertions;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Services;

namespace LLMGateway.UnitTests.Services;

public class DesanitizationEngineTests
{
    private readonly IDesanitizationEngine _engine;

    public DesanitizationEngineTests()
    {
        _engine = new DesanitizationEngine();
    }

    [Fact]
    public void Desanitize_WithSingleAlias_ReplacesWithOriginal()
    {
        // Arrange
        var session = CreateSession();
        session.Mappings.Add("ServerDB01", "SERVER_0");
        session.ReverseMappings.Add("SERVER_0", "ServerDB01");
        var content = "Query SERVER_0 for data";

        // Act
        var result = _engine.Desanitize(content, session);

        // Assert
        result.DesanitizedContent.Should().Be("Query ServerDB01 for data");
        result.ReplacementsCount.Should().Be(1);
        result.UnmatchedAliases.Should().BeEmpty();
    }

    [Fact]
    public void Desanitize_WithMultipleAliases_ReplacesAll()
    {
        // Arrange
        var session = CreateSession();
        session.Mappings.Add("ServerDB01", "SERVER_0");
        session.ReverseMappings.Add("SERVER_0", "ServerDB01");
        session.Mappings.Add("users_prod", "TABLE_0");
        session.ReverseMappings.Add("TABLE_0", "users_prod");
        
        var content = "Query SERVER_0.TABLE_0 for data";

        // Act
        var result = _engine.Desanitize(content, session);

        // Assert
        result.DesanitizedContent.Should().Be("Query ServerDB01.users_prod for data");
        result.ReplacementsCount.Should().Be(2);
    }

    [Fact]
    public void Desanitize_WithSameAliasTwice_ReplacesAllOccurrences()
    {
        // Arrange
        var session = CreateSession();
        session.Mappings.Add("ServerDB01", "SERVER_0");
        session.ReverseMappings.Add("SERVER_0", "ServerDB01");
        
        var content = "Connect to SERVER_0 and query SERVER_0";

        // Act
        var result = _engine.Desanitize(content, session);

        // Assert
        result.DesanitizedContent.Should().Be("Connect to ServerDB01 and query ServerDB01");
        result.ReplacementsCount.Should().Be(2);
    }

    [Fact]
    public void Desanitize_WithNoAliases_ReturnsUnchanged()
    {
        // Arrange
        var session = CreateSession();
        var content = "Just a normal response";

        // Act
        var result = _engine.Desanitize(content, session);

        // Assert
        result.DesanitizedContent.Should().Be(content);
        result.ReplacementsCount.Should().Be(0);
        result.UnmatchedAliases.Should().BeEmpty();
    }

    [Fact]
    public void Desanitize_WithUnknownAlias_LeavesUnchangedAndReports()
    {
        // Arrange
        var session = CreateSession();
        session.Mappings.Add("ServerDB01", "SERVER_0");
        session.ReverseMappings.Add("SERVER_0", "ServerDB01");
        
        var content = "Query SERVER_0 and SERVER_999";

        // Act
        var result = _engine.Desanitize(content, session);

        // Assert
        result.DesanitizedContent.Should().Be("Query ServerDB01 and SERVER_999");
        result.ReplacementsCount.Should().Be(1);
        result.UnmatchedAliases.Should().Contain("SERVER_999");
    }

    [Fact]
    public void Desanitize_WithJsonResponse_DesanitizesValues()
    {
        // Arrange
        var session = CreateSession();
        session.Mappings.Add("ServerDB01", "SERVER_0");
        session.ReverseMappings.Add("SERVER_0", "ServerDB01");
        session.Mappings.Add("users_prod", "TABLE_0");
        session.ReverseMappings.Add("TABLE_0", "users_prod");
        
        var content = @"{""query"": ""SELECT * FROM SERVER_0.TABLE_0"", ""server"": ""SERVER_0""}";

        // Act
        var result = _engine.Desanitize(content, session);

        // Assert
        result.DesanitizedContent.Should().Contain("ServerDB01");
        result.DesanitizedContent.Should().Contain("users_prod");
        result.DesanitizedContent.Should().NotContain("SERVER_0");
        result.DesanitizedContent.Should().NotContain("TABLE_0");
    }

    [Fact]
    public void Desanitize_WithIPAlias_Restores()
    {
        // Arrange
        var session = CreateSession();
        session.Mappings.Add("192.168.1.100", "IP_0");
        session.ReverseMappings.Add("IP_0", "192.168.1.100");
        
        var content = "Connect to IP_0";

        // Act
        var result = _engine.Desanitize(content, session);

        // Assert
        result.DesanitizedContent.Should().Be("Connect to 192.168.1.100");
        result.ReplacementsCount.Should().Be(1);
    }

    [Fact]
    public void Desanitize_EmptySession_ReturnsContentUnchanged()
    {
        // Arrange
        var session = CreateSession();
        var content = "Query SERVER_0";

        // Act
        var result = _engine.Desanitize(content, session);

        // Assert
        result.DesanitizedContent.Should().Be(content);
        result.UnmatchedAliases.Should().Contain("SERVER_0");
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
