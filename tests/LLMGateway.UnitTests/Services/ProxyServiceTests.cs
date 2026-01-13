using FluentAssertions;
using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Models;
using LLMGateway.Core.Services;

namespace LLMGateway.UnitTests.Services;

public class ProxyServiceTests
{
    private readonly IProxyService _proxyService;
    private readonly IMappingManager _mappingManager;
    private readonly ISanitizationEngine _sanitizationEngine;
    private readonly IDesanitizationEngine _desanitizationEngine;
    private readonly ILlmClient _llmClient;
    private readonly List<SanitizationRule> _rules;

    public ProxyServiceTests()
    {
        _mappingManager = new InMemoryMappingManager(TimeSpan.FromHours(8));
        _sanitizationEngine = new SanitizationEngine(new SimpleAliasGenerator());
        _desanitizationEngine = new DesanitizationEngine();
        _llmClient = new MockLlmClient();
        
        _rules = new List<SanitizationRule>
        {
            new()
            {
                Name = "SERVER_NAMES",
                Pattern = @"(?i)(ServerDB\d+)",
                Prefix = "SERVER",
                Severity = ViolationSeverity.Medium
            },
            new()
            {
                Name = "TABLE_NAMES",
                Pattern = @"(?i)(users_prod|orders_prod)",
                Prefix = "TABLE",
                Severity = ViolationSeverity.Medium
            }
        };

        _proxyService = new ProxyService(
            _mappingManager, 
            _sanitizationEngine, 
            _desanitizationEngine, 
            _llmClient, 
            _rules);
    }

    [Fact]
    public async Task ProcessRequest_WithSensitiveData_SanitizesContent()
    {
        // Arrange
        var request = new ProxyRequest(
            UserId: "user@example.com",
            Content: "Query ServerDB01.users_prod");

        // Act
        var response = await _proxyService.ProcessRequestAsync(request);

        // Assert
        response.WasSanitized.Should().BeTrue();
        response.WasDesanitized.Should().BeTrue();
        
        // Final response should be desanitized (back to original)
        response.Content.Should().Contain("ServerDB01");
        response.Content.Should().Contain("users_prod");
        
        // LLM raw response should contain aliases
        response.LlmRawResponse.Should().Contain("SERVER_0");
        response.LlmRawResponse.Should().Contain("TABLE_0");
    }

    [Fact]
    public async Task ProcessRequest_CreatesSession()
    {
        // Arrange
        var request = new ProxyRequest(
            UserId: "user@example.com",
            Content: "Query ServerDB01");

        // Act
        var response = await _proxyService.ProcessRequestAsync(request);

        // Assert
        response.SessionId.Should().NotBeNullOrEmpty();
        response.SessionId.Should().StartWith("sess_");
    }

    [Fact]
    public async Task ProcessRequest_ReturnsCreatedMappings()
    {
        // Arrange
        var request = new ProxyRequest(
            UserId: "user@example.com",
            Content: "Query ServerDB01");

        // Act
        var response = await _proxyService.ProcessRequestAsync(request);

        // Assert
        response.MappingsCreated.Should().ContainKey("ServerDB01");
        response.MappingsCreated["ServerDB01"].Should().Be("SERVER_0");
    }

    [Fact]
    public async Task ProcessRequest_WithExistingSession_ReusesSession()
    {
        // Arrange
        var request1 = new ProxyRequest(
            UserId: "user@example.com",
            Content: "Query ServerDB01");
        
        var response1 = await _proxyService.ProcessRequestAsync(request1);

        var request2 = new ProxyRequest(
            UserId: "user@example.com",
            Content: "Query ServerDB02",
            SessionId: response1.SessionId);

        // Act
        var response2 = await _proxyService.ProcessRequestAsync(request2);

        // Assert
        response2.SessionId.Should().Be(response1.SessionId);
    }

    [Fact]
    public async Task ProcessRequest_SameValueInSession_ReusesSameAlias()
    {
        // Arrange
        var request1 = new ProxyRequest(
            UserId: "user@example.com",
            Content: "Query ServerDB01");
        
        var response1 = await _proxyService.ProcessRequestAsync(request1);

        var request2 = new ProxyRequest(
            UserId: "user@example.com",
            Content: "Also query ServerDB01",
            SessionId: response1.SessionId);

        // Act
        var response2 = await _proxyService.ProcessRequestAsync(request2);

        // Assert
        response2.Content.Should().Contain("ServerDB01"); // Desanitized
        response2.MappingsCreated.Should().BeEmpty(); // No new mappings
    }

    [Fact]
    public async Task ProcessRequest_WithoutSensitiveData_ReturnsUnchanged()
    {
        // Arrange
        var request = new ProxyRequest(
            UserId: "user@example.com",
            Content: "Just a normal query");

        // Act
        var response = await _proxyService.ProcessRequestAsync(request);

        // Assert
        response.WasSanitized.Should().BeFalse();
        response.Content.Should().Contain("Just a normal query");
        response.MappingsCreated.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessRequest_WithDepartment_StoresDepartmentInSession()
    {
        // Arrange
        var request = new ProxyRequest(
            UserId: "user@example.com",
            Content: "Query data",
            Department: "Engineering");

        // Act
        var response = await _proxyService.ProcessRequestAsync(request);

        // Assert
        var session = _mappingManager.GetSession(response.SessionId);
        session.Should().NotBeNull();
        session!.Department.Should().Be("Engineering");
    }

    [Fact]
    public async Task ProcessRequest_EndToEnd_SanitizesForwardsAndDesanitizes()
    {
        // Arrange - User query with sensitive data
        var request = new ProxyRequest(
            UserId: "developer@company.com",
            Content: "How do I optimize queries on ServerDB01.users_prod?");

        // Act
        var response = await _proxyService.ProcessRequestAsync(request);

        // Assert - Full end-to-end flow
        response.WasSanitized.Should().BeTrue();
        response.WasDesanitized.Should().BeTrue();
        
        // LLM received sanitized version
        response.LlmRawResponse.Should().Contain("SERVER_0");
        response.LlmRawResponse.Should().Contain("TABLE_0");
        response.LlmRawResponse.Should().NotContain("ServerDB01");
        response.LlmRawResponse.Should().NotContain("users_prod");
        
        // User receives desanitized version
        response.Content.Should().Contain("ServerDB01");
        response.Content.Should().Contain("users_prod");
        response.Content.Should().NotContain("SERVER_0");
        response.Content.Should().NotContain("TABLE_0");
    }
}

