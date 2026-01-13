namespace LLMGateway.Core.Interfaces;

/// <summary>
/// Client for communicating with LLM providers
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends a prompt to the LLM and returns the response
    /// </summary>
    Task<string> SendAsync(string prompt, CancellationToken cancellationToken = default);
}
