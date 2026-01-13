using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Models;

namespace LLMGateway.Core.Services;

public class ProxyService(
    IMappingManager mappingManager,
    ISanitizationEngine sanitizationEngine,
    IDesanitizationEngine desanitizationEngine,
    ILlmClient llmClient,
    IEnumerable<SanitizationRule> rules) : IProxyService
{
    public async Task<ProxyResponse> ProcessRequestAsync(
        ProxyRequest request, 
        CancellationToken cancellationToken = default)
    {
        // 1. Get or create session
        Session session;
        if (!string.IsNullOrEmpty(request.SessionId))
        {
            session = mappingManager.GetSession(request.SessionId) 
                ?? mappingManager.GetOrCreateSession(request.UserId, request.Department);
        }
        else
        {
            session = mappingManager.GetOrCreateSession(request.UserId, request.Department);
        }

        // 2. Sanitize the content
        var sanitizationResult = sanitizationEngine.Sanitize(request.Content, session, rules);

        // 3. Forward to LLM
        var llmResponse = await llmClient.SendAsync(
            sanitizationResult.SanitizedContent, 
            cancellationToken);

        // 4. Desanitize the LLM response
        var desanitizationResult = desanitizationEngine.Desanitize(llmResponse, session);

        return new ProxyResponse(
            Content: desanitizationResult.DesanitizedContent,
            SessionId: session.SessionId,
            WasSanitized: sanitizationResult.WasSanitized,
            MappingsCreated: sanitizationResult.MappingsCreated,
            LlmRawResponse: llmResponse,
            WasDesanitized: desanitizationResult.ReplacementsCount > 0);
    }
}

