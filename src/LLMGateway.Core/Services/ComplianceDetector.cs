using System.Text.RegularExpressions;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;

namespace LLMGateway.Core.Services;

public class ComplianceDetector : IComplianceDetector
{
    private readonly List<CompliancePattern> _patterns = new()
    {
        new CompliancePattern("SSN", @"\b\d{3}-\d{2}-\d{4}\b", ViolationSeverity.Critical),
        new CompliancePattern("CREDIT_CARD", @"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", ViolationSeverity.Critical, ValidateCreditCard),
        new CompliancePattern("API_KEY", @"(?i)(api[_-]?key|apikey|access[_-]?token)\s*[=:]\s*\S+", ViolationSeverity.Critical),
        new CompliancePattern("PASSWORD", @"(?i)password\s*[=:]\s*\S+", ViolationSeverity.High),
        new CompliancePattern("EMAIL", @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", ViolationSeverity.Low)
    };

    public ComplianceResult Scan(string content)
    {
        var violations = new List<ComplianceViolation>();

        foreach (var pattern in _patterns)
        {
            var regex = new Regex(pattern.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50));
            var matches = regex.Matches(content);

            foreach (Match match in matches)
            {
                // Additional validation if provided
                if (pattern.Validator != null && !pattern.Validator(match.Value))
                {
                    continue;
                }

                var redacted = RedactValue(match.Value);
                violations.Add(new ComplianceViolation(
                    Type: pattern.Type,
                    Severity: pattern.Severity,
                    RedactedValue: redacted,
                    Position: match.Index));
            }
        }

        var hasViolations = violations.Count > 0;
        var shouldBlock = violations.Any(v => v.Severity == ViolationSeverity.Critical);

        return new ComplianceResult(hasViolations, shouldBlock, violations);
    }

    private static string RedactValue(string value)
    {
        if (value.Length <= 10)
            return "***";
        
        return value[..3] + "***" + value[^3..];
    }

    private static bool ValidateCreditCard(string cardNumber)
    {
        // Simple Luhn algorithm
        var digits = cardNumber.Where(char.IsDigit).Select(c => c - '0').ToArray();
        
        if (digits.Length < 13 || digits.Length > 19)
            return false;

        var sum = 0;
        var alternate = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i];

            if (alternate)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }

            sum += digit;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    private record CompliancePattern(
        string Type, 
        string Pattern, 
        ViolationSeverity Severity,
        Func<string, bool>? Validator = null);
}

