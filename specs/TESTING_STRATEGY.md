# Testing Strategy

**Parent Document:** [TECHNICAL_SPECIFICATION.md](../TECHNICAL_SPECIFICATION.md)

---

## 1. Testing Philosophy

### 1.1 Test Pyramid

```
                    ┌─────────────┐
                   /  E2E Tests   \     5-10%
                  /   (Slow)       \    
                 /─────────────────\
                /  Integration      \   20-30%
               /    Tests            \
              /─────────────────────────\
             /       Unit Tests          \  60-70%
            /         (Fast)              \
           /───────────────────────────────\
```

### 1.2 Testing Principles

| Principle | Description |
|-----------|-------------|
| **Shift Left** | Test early, catch issues before production |
| **Automate Everything** | No manual testing in CI/CD pipeline |
| **Fast Feedback** | Unit tests < 100ms, integration < 5s |
| **Deterministic** | Tests produce same results every run |
| **Isolated** | Tests don't depend on each other |
| **Production-Like** | Test environments mirror production |

---

## 2. Unit Testing

### 2.1 Coverage Requirements

| Component | Target Coverage |
|-----------|-----------------|
| Sanitization Engine | 95% |
| Policy Engine | 95% |
| Compliance Detector | 95% |
| Mapping Manager | 90% |
| Encryption Service | 90% |
| Audit Logger | 85% |
| API Controllers | 80% |
| Overall | 85% minimum |

### 2.2 Unit Test Structure

```csharp
// Follow AAA pattern: Arrange, Act, Assert
[TestClass]
public class SanitizationEngineTests
{
    private ISanitizationEngine _engine;
    private Mock<IMappingManager> _mappingManager;
    private Mock<IRuleRegistry> _ruleRegistry;
    
    [TestInitialize]
    public void Setup()
    {
        _mappingManager = new Mock<IMappingManager>();
        _ruleRegistry = new Mock<IRuleRegistry>();
        _engine = new SanitizationEngine(_mappingManager.Object, _ruleRegistry.Object);
    }
    
    [TestMethod]
    public void Sanitize_WithServerName_ReturnsAlias()
    {
        // Arrange
        var content = "Connect to ServerDB01";
        var session = CreateTestSession();
        var rules = new[] { CreateServerNameRule() };
        _ruleRegistry.Setup(r => r.GetRules()).Returns(rules);
        
        // Act
        var result = _engine.Sanitize(content, session, DefaultPolicy);
        
        // Assert
        Assert.IsTrue(result.WasSanitized);
        Assert.AreEqual("Connect to SERVER_0", result.SanitizedContent);
        Assert.AreEqual(1, result.MappingsCreated.Count);
    }
    
    [TestMethod]
    public void Sanitize_WithCriticalPii_ReturnsBlock()
    {
        // Arrange
        var content = "API key: sk-proj-abc123def456xyz";
        var session = CreateTestSession();
        var rules = new[] { CreateApiKeyRule() };
        _ruleRegistry.Setup(r => r.GetRules()).Returns(rules);
        
        // Act
        var result = _engine.Sanitize(content, session, DefaultPolicy);
        
        // Assert
        Assert.IsTrue(result.ShouldBlock);
        Assert.AreEqual("CRITICAL", result.ViolationsFound[0].Severity);
    }
}
```

### 2.3 Mocking Strategy

```csharp
// External dependencies should always be mocked
public interface ISanitizationEngineDependencies
{
    IMappingManager MappingManager { get; }
    IRuleRegistry RuleRegistry { get; }
    ILogger<SanitizationEngine> Logger { get; }
    IMetricsCollector Metrics { get; }
}

// Use strict mocks to catch unexpected calls
var mockMappingManager = new Mock<IMappingManager>(MockBehavior.Strict);

// Use loose mocks for logging (don't care about exact calls)
var mockLogger = new Mock<ILogger<SanitizationEngine>>(MockBehavior.Loose);
```

### 2.4 Parameterized Tests

```csharp
[TestClass]
public class RegexPatternTests
{
    [DataTestMethod]
    [DataRow("ServerDB01", true, "SERVER_0")]
    [DataRow("ProductionDB", true, "SERVER_0")]
    [DataRow("mydb123", true, "SERVER_0")]
    [DataRow("server_main", true, "SERVER_0")]
    [DataRow("server_error", false, null)]  // Exception
    [DataRow("localhost", false, null)]     // Not matched
    public void ServerNamePattern_MatchesCorrectly(
        string input, 
        bool shouldMatch, 
        string expectedAlias)
    {
        var rule = LoadRule("SERVER_NAMES");
        var match = Regex.Match(input, rule.Pattern);
        
        Assert.AreEqual(shouldMatch, match.Success);
        if (shouldMatch)
        {
            // Verify alias generation
        }
    }
}
```

---

## 3. Integration Testing

### 3.1 Integration Test Scope

| Test Scope | Components Tested |
|------------|-------------------|
| Request Pipeline | Proxy → Policy → Compliance → Sanitizer → Forward |
| Session Management | SessionManager → Redis → Encryption |
| Audit Pipeline | AuditLogger → Queue → Destinations |
| Policy Enforcement | PolicyEngine → PolicyStore → UserLookup |
| End-to-End Proxy | Full request/response cycle |

### 3.2 Test Infrastructure

```yaml
# docker-compose.test.yml
version: '3.8'
services:
  gateway:
    build: .
    environment:
      - ASPNETCORE_ENVIRONMENT=Test
      - Redis__ConnectionString=redis:6379
      - Database__ConnectionString=Server=sqlserver;Database=GatewayTest;
    depends_on:
      - redis
      - sqlserver
      - mock-llm
      
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
      
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=TestPassword123!
      
  mock-llm:
    image: gateway-mock-llm:latest
    ports:
      - "8080:8080"
```

### 3.3 Integration Test Examples

```csharp
[TestClass]
public class ProxyIntegrationTests : IntegrationTestBase
{
    [TestMethod]
    public async Task ProxyRequest_WithSensitiveData_SanitizesAndForwards()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var request = new ProxyRequest
        {
            TargetProvider = "openai",
            TargetUrl = "https://api.openai.com/v1/chat/completions",
            Body = new
            {
                model = "gpt-4",
                messages = new[]
                {
                    new { role = "user", content = "Query ServerDB01.users_prod" }
                }
            }
        };
        
        // Act
        var response = await client.PostAsync("/api/v1/proxy", 
            JsonContent.Create(request));
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProxyResponse>();
        
        Assert.IsTrue(result.Sanitization.WasSanitized);
        Assert.Contains("SERVER_0", result.Sanitization.MappingsCreated.Values);
        Assert.Contains("TABLE_0", result.Sanitization.MappingsCreated.Values);
        
        // Verify mock LLM received sanitized content
        var mockRequest = await MockLlm.GetLastRequestAsync();
        Assert.DoesNotContain("ServerDB01", mockRequest.Content);
        Assert.Contains("SERVER_0", mockRequest.Content);
    }
    
    [TestMethod]
    public async Task ProxyRequest_WithCriticalPii_BlocksRequest()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var request = new ProxyRequest
        {
            TargetProvider = "openai",
            Body = new
            {
                model = "gpt-4",
                messages = new[]
                {
                    new { role = "user", content = "Use API key: sk-proj-real123key456" }
                }
            }
        };
        
        // Act
        var response = await client.PostAsync("/api/v1/proxy", 
            JsonContent.Create(request));
        
        // Assert
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.AreEqual("CRITICAL_PII_DETECTED", error.Code);
        
        // Verify request was NOT forwarded
        Assert.IsNull(await MockLlm.GetLastRequestAsync());
        
        // Verify audit log was created
        var auditEntry = await GetLastAuditEntryAsync();
        Assert.AreEqual("BLOCK", auditEntry.ActionTaken);
    }
}
```

### 3.4 Session Integration Tests

```csharp
[TestClass]
public class SessionIntegrationTests : IntegrationTestBase
{
    [TestMethod]
    public async Task Session_MappingsPersistedAcrossRequests()
    {
        // First request - creates mappings
        var response1 = await ProxyRequestAsync("Query ServerDB01");
        var sessionId = response1.SessionId;
        
        // Second request - uses existing session
        var response2 = await ProxyRequestAsync("Also check ServerDB01", sessionId);
        
        // Same alias should be used
        Assert.AreEqual(
            response1.Sanitization.MappingsCreated["ServerDB01"],
            response2.Sanitization.MappingsCreated["ServerDB01"]
        );
    }
    
    [TestMethod]
    public async Task Session_ExpiredSession_CreatesNew()
    {
        // Create session
        var response1 = await ProxyRequestAsync("Query ServerDB01");
        var sessionId = response1.SessionId;
        
        // Force expire session
        await ExpireSessionAsync(sessionId);
        
        // New request with expired session ID
        var response2 = await ProxyRequestAsync("Query ServerDB01", sessionId);
        
        // Should create new session
        Assert.AreNotEqual(sessionId, response2.SessionId);
    }
}
```

---

## 4. End-to-End Testing

### 4.1 E2E Test Scenarios

| Scenario | Description |
|----------|-------------|
| IDE Integration | VSCode with proxy configured sends real LLM request |
| Multi-User | Multiple users with different policies simultaneously |
| Session Lifecycle | Create → Use → Extend → Clear → Recreate |
| Rate Limiting | Exceed limits, verify throttling works |
| Failover | Kill session store, verify graceful degradation |

### 4.2 E2E Test Implementation

```csharp
[TestClass]
public class EndToEndTests
{
    private TestServer _gateway;
    private WireMockServer _mockOpenAi;
    
    [TestMethod]
    public async Task FullWorkflow_DeveloperScenario()
    {
        // 1. Authenticate
        var token = await GetTestTokenAsync("developer@company.com");
        var client = CreateHttpClient(token);
        
        // 2. First query with sensitive data
        var query1 = @"How do I optimize this SQL?
            SELECT * FROM users_prod.accounts 
            WHERE created_at > '2024-01-01'
            ON ServerDB01";
        
        var response1 = await ProxyRequestAsync(client, query1);
        Assert.IsTrue(response1.Success);
        Assert.IsTrue(response1.Sanitization.WasSanitized);
        
        // 3. Verify LLM received sanitized content
        var llmRequest = _mockOpenAi.GetLastRequest();
        Assert.DoesNotContain("users_prod", llmRequest.Body);
        Assert.DoesNotContain("ServerDB01", llmRequest.Body);
        Assert.Contains("TABLE_0", llmRequest.Body);
        Assert.Contains("SERVER_0", llmRequest.Body);
        
        // 4. Verify response was desanitized
        Assert.Contains("users_prod", response1.LlmResponse.Content);
        Assert.Contains("ServerDB01", response1.LlmResponse.Content);
        
        // 5. Second query in same session
        var query2 = "Now add an index on ServerDB01.users_prod.created_at";
        var response2 = await ProxyRequestAsync(client, query2, response1.SessionId);
        
        // Same aliases should be used
        var llmRequest2 = _mockOpenAi.GetLastRequest();
        Assert.Contains("SERVER_0", llmRequest2.Body);
        Assert.Contains("TABLE_0", llmRequest2.Body);
        
        // 6. View audit logs
        var auditLogs = await GetAuditLogsAsync(token);
        Assert.AreEqual(2, auditLogs.Count);
        Assert.IsTrue(auditLogs.All(l => l.ActionTaken == "ALLOW_WITH_SANITIZATION"));
    }
}
```

### 4.3 Load Testing

```csharp
// Using NBomber for load testing
public class LoadTests
{
    [Test]
    public void SustainedLoad_1000RequestsPerSecond()
    {
        var scenario = Scenario.Create("proxy_requests", async context =>
        {
            var client = context.ScenarioInfo.Data["client"] as HttpClient;
            var response = await client.PostAsync("/api/v1/proxy", 
                CreateRandomRequest());
            
            return response.IsSuccessStatusCode 
                ? Response.Ok() 
                : Response.Fail();
        })
        .WithInit(context =>
        {
            context.Data["client"] = CreateAuthenticatedClient();
            return Task.CompletedTask;
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 1000, interval: TimeSpan.FromSeconds(1), 
                during: TimeSpan.FromMinutes(5))
        );
        
        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
        
        Assert.IsTrue(stats.ScenarioStats[0].Ok.Request.RPS >= 950);
        Assert.IsTrue(stats.ScenarioStats[0].Ok.Latency.Percent99 < 200);
        Assert.IsTrue(stats.ScenarioStats[0].Fail.Request.Percent < 1);
    }
}
```

---

## 5. Contract Testing

### 5.1 API Contract Tests

```csharp
[TestClass]
public class ApiContractTests
{
    [TestMethod]
    public async Task ProxyResponse_MatchesOpenApiSchema()
    {
        // Load OpenAPI spec
        var openApiDoc = await OpenApiDocument.FromFileAsync("openapi.yaml");
        
        // Make request
        var response = await Client.PostAsync("/api/v1/proxy", 
            CreateValidRequest());
        var json = await response.Content.ReadAsStringAsync();
        
        // Validate against schema
        var schema = openApiDoc.Paths["/api/v1/proxy"]
            .Operations[OperationType.Post]
            .Responses["200"]
            .Content["application/json"]
            .Schema;
        
        var validator = new JsonSchemaValidator();
        var errors = validator.Validate(json, schema);
        
        Assert.IsTrue(errors.Count == 0, 
            $"Schema violations: {string.Join(", ", errors)}");
    }
}
```

### 5.2 Provider Compatibility Tests

```csharp
[TestClass]
public class ProviderCompatibilityTests
{
    [DataTestMethod]
    [DataRow("openai", "https://api.openai.com/v1/chat/completions")]
    [DataRow("anthropic", "https://api.anthropic.com/v1/messages")]
    [DataRow("azure_openai", "https://company.openai.azure.com/openai/deployments/gpt-4/chat/completions")]
    public async Task Provider_RequestFormatPreserved(
        string provider, 
        string targetUrl)
    {
        var request = CreateProviderRequest(provider);
        var response = await ProxyRequestAsync(request);
        
        // Verify provider received correct format
        var mockRequest = await GetMockRequest(provider);
        Assert.AreEqual(targetUrl, mockRequest.Url);
        Assert.IsTrue(mockRequest.Headers.ContainsKey("Authorization"));
        
        // Verify response format preserved
        Assert.AreEqual("application/json", response.ContentType);
    }
}
```

---

## 6. Security Testing

### 6.1 Security Test Cases

```csharp
[TestClass]
public class SecurityTests
{
    [TestMethod]
    public async Task MissingAuthentication_Returns401()
    {
        var client = CreateUnauthenticatedClient();
        var response = await client.PostAsync("/api/v1/proxy", 
            CreateValidRequest());
        
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [TestMethod]
    public async Task ExpiredToken_Returns401()
    {
        var expiredToken = CreateExpiredToken();
        var client = CreateClient(expiredToken);
        var response = await client.PostAsync("/api/v1/proxy", 
            CreateValidRequest());
        
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [TestMethod]
    public async Task CrossUserSessionAccess_Returns403()
    {
        // User A creates session
        var clientA = CreateClient(GetToken("userA@company.com"));
        var responseA = await clientA.PostAsync("/api/v1/proxy", 
            CreateValidRequest());
        var sessionIdA = GetSessionId(responseA);
        
        // User B tries to access User A's session
        var clientB = CreateClient(GetToken("userB@company.com"));
        var responseB = await clientB.GetAsync($"/api/v1/sessions/{sessionIdA}");
        
        Assert.AreEqual(HttpStatusCode.Forbidden, responseB.StatusCode);
    }
    
    [TestMethod]
    public async Task SqlInjection_Sanitized()
    {
        var maliciousInput = "'; DROP TABLE users; --";
        var response = await ProxyRequestAsync(maliciousInput);
        
        // Should not cause error, should be sanitized
        Assert.IsTrue(response.Success);
    }
    
    [TestMethod]
    public async Task EncodedBypass_Detected()
    {
        // Try to bypass with Base64 encoding
        var sensitiveData = "ServerDB01";
        var encoded = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(sensitiveData));
        
        var response = await ProxyRequestAsync($"Check server {encoded}");
        
        // Should detect and sanitize
        Assert.IsTrue(response.Sanitization.WasSanitized);
    }
}
```

### 6.2 Fuzzing Tests

```csharp
[TestClass]
public class FuzzingTests
{
    [TestMethod]
    public void RegexFuzzing_NoReDoS()
    {
        var rules = LoadAllSanitizationRules();
        var fuzzer = new PatternFuzzer();
        
        foreach (var rule in rules)
        {
            var evilInputs = fuzzer.GenerateReDoSAttempts(rule.Pattern);
            
            foreach (var input in evilInputs)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var regex = new Regex(rule.Pattern, 
                        RegexOptions.None, 
                        TimeSpan.FromMilliseconds(100));
                    regex.Match(input);
                }
                catch (RegexMatchTimeoutException)
                {
                    // Expected for some evil inputs
                }
                sw.Stop();
                
                // Should never take more than 100ms
                Assert.IsTrue(sw.ElapsedMilliseconds < 100,
                    $"Rule {rule.Name} took {sw.ElapsedMilliseconds}ms on input: {input[..50]}...");
            }
        }
    }
}
```

---

## 7. Performance Testing

### 7.1 Performance Benchmarks

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class SanitizationBenchmarks
{
    private ISanitizationEngine _engine;
    private Session _session;
    private string _smallContent;
    private string _mediumContent;
    private string _largeContent;
    
    [GlobalSetup]
    public void Setup()
    {
        _engine = CreateEngine();
        _session = CreateSession();
        _smallContent = "Query ServerDB01"; // ~20 chars
        _mediumContent = GenerateContent(1000); // 1KB
        _largeContent = GenerateContent(100000); // 100KB
    }
    
    [Benchmark]
    public SanitizationResult SmallContent() 
        => _engine.Sanitize(_smallContent, _session, DefaultPolicy);
    
    [Benchmark]
    public SanitizationResult MediumContent() 
        => _engine.Sanitize(_mediumContent, _session, DefaultPolicy);
    
    [Benchmark]
    public SanitizationResult LargeContent() 
        => _engine.Sanitize(_largeContent, _session, DefaultPolicy);
}

// Expected results:
// | Method        | Mean      | Gen0   | Allocated |
// |-------------- |----------:|-------:|----------:|
// | SmallContent  |   0.8 ms  | 0.0010 |      1 KB |
// | MediumContent |   5.2 ms  | 0.0156 |     12 KB |
// | LargeContent  |  45.0 ms  | 0.3125 |    250 KB |
```

### 7.2 Performance Acceptance Criteria

| Metric | Target | Failure Threshold |
|--------|--------|-------------------|
| P50 Latency | < 20ms | > 50ms |
| P99 Latency | < 100ms | > 200ms |
| Throughput | 1000 req/s | < 500 req/s |
| Memory per request | < 1MB | > 5MB |
| GC pause time | < 10ms | > 50ms |

---

## 8. Test Automation

### 8.1 CI/CD Pipeline

```yaml
# .github/workflows/test.yml
name: Test Pipeline

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'
      - run: dotnet test --filter Category=Unit --collect:"XPlat Code Coverage"
      - uses: codecov/codecov-action@v3
        
  integration-tests:
    runs-on: ubuntu-latest
    services:
      redis:
        image: redis:7-alpine
        ports:
          - 6379:6379
    steps:
      - uses: actions/checkout@v4
      - run: docker-compose -f docker-compose.test.yml up -d
      - run: dotnet test --filter Category=Integration
      - run: docker-compose -f docker-compose.test.yml down
        
  security-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet test --filter Category=Security
      - uses: snyk/actions/dotnet@master
        env:
          SNYK_TOKEN: ${{ secrets.SNYK_TOKEN }}
          
  performance-tests:
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4
      - run: dotnet run --project tests/Performance --configuration Release
      - uses: benchmark-action/github-action-benchmark@v1
        with:
          tool: 'benchmarkdotnet'
          output-file-path: BenchmarkDotNet.Artifacts/results.json
```

### 8.2 Test Reporting

```csharp
// Generate detailed test reports
[TestClass]
public class TestReporting
{
    [AssemblyCleanup]
    public static void GenerateReports()
    {
        // Coverage report
        ReportGenerator.Generate(
            coverageFiles: "**/*.cobertura.xml",
            outputDir: "TestResults/Coverage",
            reportTypes: "Html,Cobertura"
        );
        
        // Test results
        TestResultsPublisher.Publish(
            resultsFile: "TestResults/results.trx",
            format: "JUnit",
            outputFile: "TestResults/junit.xml"
        );
    }
}
```

---

## 9. Test Data Management

### 9.1 Test Data Factory

```csharp
public class TestDataFactory
{
    public static Session CreateSession(
        string userId = "test@company.com",
        string department = "Engineering")
    {
        return new Session
        {
            SessionId = $"test-{Guid.NewGuid():N}",
            UserId = userId,
            Department = department,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            Mappings = new Dictionary<string, string>(),
            ReverseMappings = new Dictionary<string, string>()
        };
    }
    
    public static SanitizationRule CreateRule(
        string name = "TEST_RULE",
        string pattern = @"TestPattern\d+",
        ViolationSeverity severity = ViolationSeverity.Medium)
    {
        return new SanitizationRule
        {
            RuleId = $"rule-{Guid.NewGuid():N}",
            Name = name,
            Pattern = pattern,
            Prefix = "TEST",
            Severity = severity,
            Enabled = true
        };
    }
    
    public static ProxyRequest CreateProxyRequest(
        string content = "Test query",
        string provider = "openai")
    {
        return new ProxyRequest
        {
            TargetProvider = provider,
            TargetUrl = $"https://api.{provider}.com/v1/chat/completions",
            Body = new
            {
                model = "gpt-4",
                messages = new[] { new { role = "user", content } }
            }
        };
    }
}
```

### 9.2 Sensitive Test Data

```csharp
// Never use real sensitive data in tests
// Use obviously fake patterns
public static class TestSecrets
{
    public const string FakeApiKey = "sk-test-FAKE1234567890ABCDEF1234";
    public const string FakeSsn = "000-00-0000";
    public const string FakeCreditCard = "4111111111111111"; // Test card number
    public const string FakePassword = "TestPassword123!NotReal";
    public const string FakeServerName = "TestServerDB01";
    public const string FakeTableName = "test_users_table";
}
```

