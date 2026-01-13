using System.Text.RegularExpressions;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;

namespace LLMGateway.Core.Services;

public class SanitizationEngine(IAliasGenerator aliasGenerator) : ISanitizationEngine
{
    public SanitizationResult Sanitize(
        string content, 
        Session session, 
        IEnumerable<SanitizationRule> rules)
    {
        var workingContent = content;
        var mappingsCreated = new Dictionary<string, string>();
        var violations = new List<Violation>();
        var wasSanitized = false;

        foreach (var rule in rules.Where(r => r.Enabled))
        {
            var regex = new Regex(rule.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50));
            var matches = regex.Matches(workingContent);

            foreach (Match match in matches)
            {
                var originalValue = match.Groups[1].Value;

                // Check if it's in the exception list
                if (rule.Exceptions.Contains(originalValue, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Get or create alias
                string alias;
                if (session.Mappings.TryGetValue(originalValue, out var existingAlias))
                {
                    alias = existingAlias;
                }
                else
                {
                    // Count how many aliases we have for this prefix
                    var counter = session.Mappings.Values.Count(a => a.StartsWith(rule.Prefix + "_"));
                    alias = aliasGenerator.GenerateAlias(rule.Prefix, counter);
                    
                    session.Mappings[originalValue] = alias;
                    session.ReverseMappings[alias] = originalValue;
                    mappingsCreated[originalValue] = alias;
                }

                // Replace in content
                workingContent = workingContent.Replace(originalValue, alias);
                wasSanitized = true;

                // Record violation
                violations.Add(new Violation(rule.Name, rule.Severity, originalValue));
            }
        }

        return new SanitizationResult(workingContent, wasSanitized, mappingsCreated, violations);
    }
}

