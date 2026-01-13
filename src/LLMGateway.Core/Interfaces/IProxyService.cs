using LLMGateway.Core.Models;

namespace LLMGateway.Core.Interfaces;

/// <summary>
/// Handles proxy requests - sanitize, forward to LLM, desanitize
/// </summary>
public interface IProxyService
{
    /// <summary>
    /// Process a proxy request: sanitize → forward to LLM → desanitize
    /// </summary>
    Task<ProxyResponse> ProcessRequestAsync(ProxyRequest request, CancellationToken cancellationToken = default);
}

