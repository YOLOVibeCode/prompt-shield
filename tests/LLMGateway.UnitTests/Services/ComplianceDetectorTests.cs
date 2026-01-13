using FluentAssertions;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Services;

namespace LLMGateway.UnitTests.Services;

public class ComplianceDetectorTests
{
    private readonly IComplianceDetector _detector;

    public ComplianceDetectorTests()
    {
        _detector = new ComplianceDetector();
    }

    [Fact]
    public void Scan_WithSSN_DetectsViolation()
    {
        // Arrange
        var content = "My SSN is 123-45-6789";

        // Act
        var result = _detector.Scan(content);

        // Assert
        result.HasViolations.Should().BeTrue();
        result.ShouldBlock.Should().BeTrue(); // SSN is critical
        result.Violations.Should().HaveCount(1);
        result.Violations[0].Type.Should().Be("SSN");
        result.Violations[0].Severity.Should().Be(ViolationSeverity.Critical);
        result.Violations[0].RedactedValue.Should().Contain("***"); // Redacted
    }

    [Fact]
    public void Scan_WithCreditCard_DetectsViolation()
    {
        // Arrange
        var content = "My credit card is 4111111111111111";

        // Act
        var result = _detector.Scan(content);

        // Assert
        result.HasViolations.Should().BeTrue();
        result.ShouldBlock.Should().BeTrue();
        result.Violations.Should().HaveCount(1);
        result.Violations[0].Type.Should().Be("CREDIT_CARD");
        result.Violations[0].Severity.Should().Be(ViolationSeverity.Critical);
    }

    [Fact]
    public void Scan_WithCreditCardSpaces_DetectsViolation()
    {
        // Arrange
        var content = "Card: 4111 1111 1111 1111";

        // Act
        var result = _detector.Scan(content);

        // Assert
        result.HasViolations.Should().BeTrue();
        result.Violations[0].Type.Should().Be("CREDIT_CARD");
    }

    [Fact]
    public void Scan_WithAPIKey_DetectsViolation()
    {
        // Arrange
        var content = "API_KEY=sk_test_4eC39HqLyjWDarjtT1zdp7dc";

        // Act
        var result = _detector.Scan(content);

        // Assert
        result.HasViolations.Should().BeTrue();
        result.ShouldBlock.Should().BeTrue();
        result.Violations[0].Type.Should().Be("API_KEY");
        result.Violations[0].Severity.Should().Be(ViolationSeverity.Critical);
    }

    [Fact]
    public void Scan_WithPassword_DetectsViolation()
    {
        // Arrange
        var content = "password=MySecret123!";

        // Act
        var result = _detector.Scan(content);

        // Assert
        result.HasViolations.Should().BeTrue();
        result.Violations[0].Type.Should().Be("PASSWORD");
        result.Violations[0].Severity.Should().Be(ViolationSeverity.High);
    }

    [Fact]
    public void Scan_WithNoViolations_ReturnsClean()
    {
        // Arrange
        var content = "Just a normal query about optimizing databases";

        // Act
        var result = _detector.Scan(content);

        // Assert
        result.HasViolations.Should().BeFalse();
        result.ShouldBlock.Should().BeFalse();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Scan_WithMultipleViolations_DetectsAll()
    {
        // Arrange
        var content = "SSN: 123-45-6789, Card: 4111111111111111";

        // Act
        var result = _detector.Scan(content);

        // Assert
        result.HasViolations.Should().BeTrue();
        result.ShouldBlock.Should().BeTrue();
        result.Violations.Should().HaveCount(2);
    }

    [Fact]
    public void Scan_WithEmailAddress_DetectsAsLowSeverity()
    {
        // Arrange
        var content = "Contact me at user@example.com";

        // Act
        var result = _detector.Scan(content);

        // Assert
        result.HasViolations.Should().BeTrue();
        result.ShouldBlock.Should().BeFalse(); // Email is low severity
        result.Violations[0].Type.Should().Be("EMAIL");
        result.Violations[0].Severity.Should().Be(ViolationSeverity.Low);
    }
}

