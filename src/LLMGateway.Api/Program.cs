using LLMGateway.Core.Entities;
using LLMGateway.Core.Interfaces;
using LLMGateway.Core.Models;
using LLMGateway.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<IAliasGenerator, SimpleAliasGenerator>();
builder.Services.AddSingleton<IMappingManager>(sp => 
    new InMemoryMappingManager(TimeSpan.FromHours(8)));
builder.Services.AddSingleton<IAuditLogger, InMemoryAuditLogger>();
builder.Services.AddScoped<ISanitizationEngine, SanitizationEngine>();
builder.Services.AddScoped<IDesanitizationEngine, DesanitizationEngine>();
builder.Services.AddScoped<IComplianceDetector, ComplianceDetector>();
builder.Services.AddScoped<ILlmClient, MockLlmClient>();

// Add sanitization rules
var defaultRules = new List<SanitizationRule>
{
    new()
    {
        Name = "SERVER_NAMES",
        Pattern = @"(?i)(ServerDB\d+|ProductionDB\d+|\w+db\d+)",
        Prefix = "SERVER",
        Severity = ViolationSeverity.Medium
    },
    new()
    {
        Name = "TABLE_NAMES",
        Pattern = @"(?i)(users_prod|orders_prod|customers_prod|transactions_prod)",
        Prefix = "TABLE",
        Severity = ViolationSeverity.Medium
    },
    new()
    {
        Name = "IP_ADDRESSES",
        Pattern = @"\b(192\.168|10\.|172\.(1[6-9]|2[0-9]|3[0-1]))\.\d{1,3}\.\d{1,3}\b",
        Prefix = "IP",
        Severity = ViolationSeverity.High
    }
};
builder.Services.AddSingleton<IEnumerable<SanitizationRule>>(defaultRules);

// Add default user policies
var defaultPolicies = new List<UserPolicy>
{
    new()
    {
        UserId = "developer@company.com",
        Department = "Engineering",
        AccessLevel = AccessLevel.SanitizedOnly,
        DailyRequestLimit = 500,
        HourlyRequestLimit = 50
    },
    new()
    {
        UserId = "security@company.com",
        Department = "Security",
        AccessLevel = AccessLevel.Unrestricted,
        DailyRequestLimit = 1000,
        HourlyRequestLimit = 100
    }
};
builder.Services.AddSingleton<IEnumerable<UserPolicy>>(defaultPolicies);
builder.Services.AddSingleton<IPolicyEngine>(sp => 
    new SimplePolicyEngine(sp.GetRequiredService<IEnumerable<UserPolicy>>()));

builder.Services.AddScoped<IProxyService, ProxyService>();

var app = builder.Build();

// Health endpoints
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Version = "1.0.0",
    Timestamp = DateTime.UtcNow
}))
.WithName("HealthCheck");

app.MapGet("/ready", () => Results.Ok(new
{
    Ready = true,
    Timestamp = DateTime.UtcNow
}))
.WithName("ReadyCheck");

app.MapGet("/live", () => Results.Ok(new
{
    Alive = true,
    Timestamp = DateTime.UtcNow
}))
.WithName("LiveCheck");

// Proxy endpoint
app.MapPost("/api/v1/proxy", async (ProxyRequest request, IProxyService proxyService) =>
{
    var response = await proxyService.ProcessRequestAsync(request);
    return Results.Ok(response);
})
.WithName("ProxyRequest");

// Session endpoints
app.MapGet("/api/v1/sessions/{sessionId}", (string sessionId, IMappingManager mappingManager) =>
{
    var session = mappingManager.GetSession(sessionId);
    if (session == null)
        return Results.NotFound(new { Error = "Session not found" });

    return Results.Ok(new
    {
        session.SessionId,
        session.UserId,
        session.Department,
        session.Status,
        session.CreatedAt,
        session.ExpiresAt,
        session.MappingCount,
        session.RequestCount
    });
})
.WithName("GetSession");

// Audit endpoints
app.MapGet("/api/v1/audit/logs", (IAuditLogger auditLogger, int limit = 50) =>
{
    var logs = auditLogger.GetRecentEntries(limit);
    return Results.Ok(new { Logs = logs, Count = logs.Count });
})
.WithName("GetAuditLogs");

// Policy endpoints
app.MapGet("/api/v1/policies/{userId}", (string userId, IPolicyEngine policyEngine) =>
{
    var decision = policyEngine.Evaluate(userId);
    var policy = policyEngine.GetPolicy(userId);
    
    return Results.Ok(new
    {
        UserId = userId,
        Decision = decision,
        Policy = policy
    });
})
.WithName("GetUserPolicy");

// Compliance test endpoint
app.MapPost("/api/v1/compliance/scan", (string content, IComplianceDetector detector) =>
{
    var result = detector.Scan(content);
    return Results.Ok(result);
})
.WithName("ScanCompliance");

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
