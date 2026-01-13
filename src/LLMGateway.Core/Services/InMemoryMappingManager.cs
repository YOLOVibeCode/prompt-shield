using System.Collections.Concurrent;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;

namespace LLMGateway.Core.Services;

public class InMemoryMappingManager(TimeSpan sessionTimeout) : IMappingManager
{
    private readonly ConcurrentDictionary<string, Session> _sessionsById = new();
    private readonly ConcurrentDictionary<string, string> _sessionIdByUserId = new();

    public Session GetOrCreateSession(string userId, string? department = null)
    {
        // Check if user already has an active session
        if (_sessionIdByUserId.TryGetValue(userId, out var existingSessionId))
        {
            if (_sessionsById.TryGetValue(existingSessionId, out var existingSession))
            {
                if (!existingSession.IsExpired)
                {
                    existingSession.LastAccessedAt = DateTime.UtcNow;
                    return existingSession;
                }
                
                // Session expired, remove it
                _sessionsById.TryRemove(existingSessionId, out _);
                _sessionIdByUserId.TryRemove(userId, out _);
            }
        }

        // Create new session
        var session = new Session
        {
            SessionId = GenerateSessionId(),
            UserId = userId,
            Department = department,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(sessionTimeout),
            Status = SessionStatus.Active
        };

        _sessionsById[session.SessionId] = session;
        _sessionIdByUserId[userId] = session.SessionId;

        return session;
    }

    public Session? GetSession(string sessionId)
    {
        if (_sessionsById.TryGetValue(sessionId, out var session))
        {
            if (!session.IsExpired)
            {
                session.LastAccessedAt = DateTime.UtcNow;
                return session;
            }
            
            // Session expired, remove it
            _sessionsById.TryRemove(sessionId, out _);
            _sessionIdByUserId.TryRemove(session.UserId, out _);
        }

        return null;
    }

    public void ClearSession(string sessionId)
    {
        if (_sessionsById.TryRemove(sessionId, out var session))
        {
            _sessionIdByUserId.TryRemove(session.UserId, out _);
        }
    }

    public int CleanupExpiredSessions()
    {
        var expiredSessions = _sessionsById.Values
            .Where(s => s.IsExpired)
            .ToList();

        foreach (var session in expiredSessions)
        {
            _sessionsById.TryRemove(session.SessionId, out _);
            _sessionIdByUserId.TryRemove(session.UserId, out _);
        }

        return expiredSessions.Count;
    }

    private static string GenerateSessionId()
    {
        return $"sess_{Guid.NewGuid():N}";
    }
}

