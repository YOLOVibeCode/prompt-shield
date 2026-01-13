using FluentAssertions;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Services;

namespace LLMGateway.UnitTests.Services;

public class AuditLoggerTests
{
    private readonly IAuditLogger _logger;

    public AuditLoggerTests()
    {
        _logger = new InMemoryAuditLogger();
    }

    [Fact]
    public void Log_CreatesEntry()
    {
        // Arrange
        var entry = new AuditEntry
        {
            EntryId = "aud_123",
            Timestamp = DateTime.UtcNow,
            UserId = "user@example.com",
            SessionId = "sess_123",
            WasSanitized = true,
            WasDesanitized = true,
            ActionTaken = "ALLOW_WITH_SANITIZATION",
            ProcessingTimeMs = 45
        };

        // Act
        _logger.Log(entry);

        // Assert
        var entries = _logger.GetRecentEntries();
        entries.Should().HaveCount(1);
        entries[0].EntryId.Should().Be("aud_123");
    }

    [Fact]
    public void GetRecentEntries_ReturnsInReverseChronologicalOrder()
    {
        // Arrange
        var entry1 = CreateAuditEntry("aud_1", DateTime.UtcNow.AddMinutes(-2));
        var entry2 = CreateAuditEntry("aud_2", DateTime.UtcNow.AddMinutes(-1));
        var entry3 = CreateAuditEntry("aud_3", DateTime.UtcNow);

        _logger.Log(entry1);
        _logger.Log(entry2);
        _logger.Log(entry3);

        // Act
        var entries = _logger.GetRecentEntries();

        // Assert
        entries.Should().HaveCount(3);
        entries[0].EntryId.Should().Be("aud_3"); // Most recent first
        entries[1].EntryId.Should().Be("aud_2");
        entries[2].EntryId.Should().Be("aud_1");
    }

    [Fact]
    public void GetRecentEntries_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _logger.Log(CreateAuditEntry($"aud_{i}", DateTime.UtcNow.AddMinutes(-i)));
        }

        // Act
        var entries = _logger.GetRecentEntries(5);

        // Assert
        entries.Should().HaveCount(5);
    }

    [Fact]
    public void Log_StoresViolationTypes()
    {
        // Arrange
        var entry = new AuditEntry
        {
            EntryId = "aud_123",
            Timestamp = DateTime.UtcNow,
            UserId = "user@example.com",
            WasSanitized = true,
            WasDesanitized = false,
            ActionTaken = "ALLOW",
            ViolationTypes = new List<string> { "SERVER_NAMES", "TABLE_NAMES" }
        };

        // Act
        _logger.Log(entry);

        // Assert
        var entries = _logger.GetRecentEntries();
        entries[0].ViolationTypes.Should().Contain("SERVER_NAMES");
        entries[0].ViolationTypes.Should().Contain("TABLE_NAMES");
    }

    private static AuditEntry CreateAuditEntry(string entryId, DateTime timestamp)
    {
        return new AuditEntry
        {
            EntryId = entryId,
            Timestamp = timestamp,
            UserId = "test@example.com",
            WasSanitized = false,
            WasDesanitized = false,
            ActionTaken = "ALLOW"
        };
    }
}

