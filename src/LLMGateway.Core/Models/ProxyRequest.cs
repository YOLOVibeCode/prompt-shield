namespace LLMGateway.Core.Models;

public record ProxyRequest(
    string UserId,
    string Content,
    string? SessionId = null,
    string? Department = null);

public record ProxyResponse(
    string Content,
    string SessionId,
    bool WasSanitized,
    Dictionary<string, string> MappingsCreated,
    string? LlmRawResponse = null,
    bool WasDesanitized = false);

