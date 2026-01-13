using System.Text.RegularExpressions;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;

namespace LLMGateway.Core.Services;

public class DesanitizationEngine : IDesanitizationEngine
{
    public DesanitizationResult Desanitize(string content, Session session)
    {
        if (session.ReverseMappings.Count == 0)
        {
            // Check if there are any alias patterns in the content
            var unmatched = FindUnmatchedAliases(content);
            return new DesanitizationResult(content, 0, unmatched);
        }

        var workingContent = content;
        var replacementsCount = 0;
        var unmatchedAliases = new List<string>();

        // Build a pattern that matches any of our known alias formats
        var aliasPattern = @"\b([A-Z]+_\d+)\b";
        var regex = new Regex(aliasPattern);
        var matches = regex.Matches(content);

        // Track which aliases we've already replaced to avoid duplicate processing
        var processedAliases = new HashSet<string>();

        foreach (Match match in matches)
        {
            var alias = match.Value;
            
            if (processedAliases.Contains(alias))
                continue;

            processedAliases.Add(alias);

            if (session.ReverseMappings.TryGetValue(alias, out var original))
            {
                // Replace all occurrences of this alias
                var occurrences = Regex.Matches(workingContent, $@"\b{Regex.Escape(alias)}\b").Count;
                workingContent = Regex.Replace(workingContent, $@"\b{Regex.Escape(alias)}\b", original);
                replacementsCount += occurrences;
            }
            else
            {
                // Unknown alias
                unmatchedAliases.Add(alias);
            }
        }

        return new DesanitizationResult(workingContent, replacementsCount, unmatchedAliases);
    }

    private static List<string> FindUnmatchedAliases(string content)
    {
        var aliasPattern = @"\b([A-Z]+_\d+)\b";
        var regex = new Regex(aliasPattern);
        var matches = regex.Matches(content);
        
        return matches.Select(m => m.Value).Distinct().ToList();
    }
}
