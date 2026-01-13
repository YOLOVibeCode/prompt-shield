using LLMGateway.Core.Entities;

namespace LLMGateway.Core.Interfaces;

/// <summary>
/// Manages user sessions and their mappings
/// </summary>
public interface IMappingManager
{
    /// <summary>
    /// Gets an existing session or creates a new one
    /// </summary>
    Session GetOrCreateSession(string userId, string? department = null);
    
    /// <summary>
    /// Gets a session by ID
    /// </summary>
    Session? GetSession(string sessionId);
    
    /// <summary>
    /// Clears a session and all its mappings
    /// </summary>
    void ClearSession(string sessionId);
    
    /// <summary>
    /// Removes expired sessions
    /// </summary>
    int CleanupExpiredSessions();
}

