using FluentAssertions;
using LLMGateway.Core.Entities;

namespace LLMGateway.UnitTests.Entities;

public class SessionTests
{
    [Fact]
    public void Session_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var session = new Session
        {
            SessionId = "sess_123",
            UserId = "user@example.com",
            Department = "Engineering",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        };

        // Assert
        session.SessionId.Should().Be("sess_123");
        session.UserId.Should().Be("user@example.com");
        session.Department.Should().Be("Engineering");
        session.Status.Should().Be(SessionStatus.Active); // Default
    }

    [Fact]
    public void Session_ShouldHaveEmptyMappingsByDefault()
    {
        // Arrange & Act
        var session = new Session
        {
            SessionId = "sess_123",
            UserId = "user@example.com",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        };

        // Assert
        session.Mappings.Should().NotBeNull();
        session.Mappings.Should().BeEmpty();
        session.ReverseMappings.Should().NotBeNull();
        session.ReverseMappings.Should().BeEmpty();
    }

    [Fact]
    public void Session_ShouldBeExpiredWhenPastExpiryTime()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "sess_123",
            UserId = "user@example.com",
            CreatedAt = DateTime.UtcNow.AddHours(-9),
            ExpiresAt = DateTime.UtcNow.AddHours(-1) // Expired 1 hour ago
        };

        // Act
        var isExpired = session.IsExpired;

        // Assert
        isExpired.Should().BeTrue();
    }

    [Fact]
    public void Session_ShouldNotBeExpiredWhenBeforeExpiryTime()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "sess_123",
            UserId = "user@example.com",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(8) // Expires in 8 hours
        };

        // Act
        var isExpired = session.IsExpired;

        // Assert
        isExpired.Should().BeFalse();
    }

    [Fact]
    public void Session_ShouldCalculateMappingCount()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "sess_123",
            UserId = "user@example.com",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        };
        
        session.Mappings.Add("ServerDB01", "SERVER_0");
        session.Mappings.Add("users_prod", "TABLE_0");

        // Act
        var count = session.MappingCount;

        // Assert
        count.Should().Be(2);
    }
}

