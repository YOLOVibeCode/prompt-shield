using LLMGateway.Core.Entities;

namespace LLMGateway.Core.Interfaces;

/// <summary>
/// Detects PII and regulated data (SSN, credit cards, secrets)
/// </summary>
public interface IComplianceDetector
{
    /// <summary>
    /// Scans content for compliance violations
    /// </summary>
    ComplianceResult Scan(string content);
}

public record ComplianceResult(
    bool HasViolations,
    bool ShouldBlock,
    List<ComplianceViolation> Violations);

public record ComplianceViolation(
    string Type,
    ViolationSeverity Severity,
    string RedactedValue,
    int Position);

