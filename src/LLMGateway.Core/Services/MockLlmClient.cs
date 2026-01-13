using LLMGateway.Core.Interfaces;

namespace LLMGateway.Core.Services;

/// <summary>
/// Mock LLM client that echoes back the prompt for testing
/// </summary>
public class MockLlmClient : ILlmClient
{
    public Task<string> SendAsync(string prompt, CancellationToken cancellationToken = default)
    {
        // Mock response that uses the aliases from the prompt
        var response = $"Based on your query about {prompt}, here's my response: You should optimize the query by adding indexes.";
        return Task.FromResult(response);
    }
}

