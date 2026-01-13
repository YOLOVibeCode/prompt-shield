using FluentAssertions;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Services;

namespace LLMGateway.UnitTests.Services;

public class InMemoryMappingManagerTests
{
    private readonly IMappingManager _manager;

    public InMemoryMappingManagerTests()
    {
        _manager = new InMemoryMappingManager(TimeSpan.FromHours(8));
    }

    [Fact]
    public void GetOrCreateSession_NewUser_CreatesSession()
    {
        // Act
        var session = _manager.GetOrCreateSession("user@example.com", "Engineering");

        // Assert
        session.Should().NotBeNull();
        session.SessionId.Should().NotBeNullOrEmpty();
        session.UserId.Should().Be("user@example.com");
        session.Department.Should().Be("Engineering");
        session.Status.Should().Be(Core.Entities.SessionStatus.Active);
        session.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void GetOrCreateSession_ExistingUser_ReturnsSameSession()
    {
        // Arrange
        var session1 = _manager.GetOrCreateSession("user@example.com");

        // Act
        var session2 = _manager.GetOrCreateSession("user@example.com");

        // Assert
        session1.SessionId.Should().Be(session2.SessionId);
    }

    [Fact]
    public void GetSession_ValidSessionId_ReturnsSession()
    {
        // Arrange
        var created = _manager.GetOrCreateSession("user@example.com");

        // Act
        var retrieved = _manager.GetSession(created.SessionId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.SessionId.Should().Be(created.SessionId);
        retrieved.UserId.Should().Be("user@example.com");
    }

    [Fact]
    public void GetSession_InvalidSessionId_ReturnsNull()
    {
        // Act
        var session = _manager.GetSession("non-existent-session");

        // Assert
        session.Should().BeNull();
    }

    [Fact]
    public void ClearSession_ValidSessionId_RemovesSession()
    {
        // Arrange
        var session = _manager.GetOrCreateSession("user@example.com");
        var sessionId = session.SessionId;

        // Act
        _manager.ClearSession(sessionId);

        // Assert
        var retrieved = _manager.GetSession(sessionId);
        retrieved.Should().BeNull();
    }

    [Fact]
    public void ClearSession_AfterClear_CreatesNewSessionForUser()
    {
        // Arrange
        var session1 = _manager.GetOrCreateSession("user@example.com");
        var sessionId1 = session1.SessionId;
        _manager.ClearSession(sessionId1);

        // Act
        var session2 = _manager.GetOrCreateSession("user@example.com");

        // Assert
        session2.SessionId.Should().NotBe(sessionId1);
    }

    [Fact]
    public void GetOrCreateSession_MaintainsMappingsAcrossRequests()
    {
        // Arrange
        var session = _manager.GetOrCreateSession("user@example.com");
        session.Mappings.Add("ServerDB01", "SERVER_0");

        // Act
        var sameSession = _manager.GetOrCreateSession("user@example.com");

        // Assert
        sameSession.Mappings.Should().ContainKey("ServerDB01");
        sameSession.Mappings["ServerDB01"].Should().Be("SERVER_0");
    }

    [Fact]
    public void CleanupExpiredSessions_RemovesExpiredSessions()
    {
        // Arrange
        var manager = new InMemoryMappingManager(TimeSpan.FromMilliseconds(1));
        var session = manager.GetOrCreateSession("user@example.com");
        var sessionId = session.SessionId;
        
        // Wait for expiration
        Thread.Sleep(10);

        // Act
        var removed = manager.CleanupExpiredSessions();

        // Assert
        removed.Should().Be(1);
        manager.GetSession(sessionId).Should().BeNull();
    }

    [Fact]
    public void CleanupExpiredSessions_DoesNotRemoveActiveSessions()
    {
        // Arrange
        var session = _manager.GetOrCreateSession("user@example.com");

        // Act
        var removed = _manager.CleanupExpiredSessions();

        // Assert
        removed.Should().Be(0);
        _manager.GetSession(session.SessionId).Should().NotBeNull();
    }

    [Fact]
    public void GetOrCreateSession_DifferentUsers_CreatesDifferentSessions()
    {
        // Act
        var session1 = _manager.GetOrCreateSession("user1@example.com");
        var session2 = _manager.GetOrCreateSession("user2@example.com");

        // Assert
        session1.SessionId.Should().NotBe(session2.SessionId);
        session1.UserId.Should().Be("user1@example.com");
        session2.UserId.Should().Be("user2@example.com");
    }
}

