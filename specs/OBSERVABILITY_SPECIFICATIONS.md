# Observability Specifications

**Parent Document:** [TECHNICAL_SPECIFICATION.md](../TECHNICAL_SPECIFICATION.md)

---

## 1. Observability Strategy

### 1.1 Three Pillars of Observability

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         OBSERVABILITY                                    │
├─────────────────────┬─────────────────────┬─────────────────────────────┤
│       METRICS       │       LOGS          │         TRACES              │
│                     │                     │                             │
│ • Request rates     │ • Structured JSON   │ • Distributed tracing       │
│ • Latencies         │ • Correlation IDs   │ • Request flow              │
│ • Error rates       │ • Context enrichment│ • Dependency mapping        │
│ • Resource usage    │ • Log levels        │ • Performance bottlenecks   │
│                     │                     │                             │
│ Prometheus/Grafana  │ ELK/Splunk          │ Jaeger/Zipkin               │
└─────────────────────┴─────────────────────┴─────────────────────────────┘
```

### 1.2 Observability Goals

| Goal | Target | Measurement |
|------|--------|-------------|
| Mean Time to Detect (MTTD) | < 5 minutes | Alert latency |
| Mean Time to Understand | < 10 minutes | Investigation time |
| Mean Time to Resolve (MTTR) | < 30 minutes | Incident duration |
| Dashboard load time | < 3 seconds | User experience |

---

## 2. Metrics

### 2.1 Application Metrics

#### Request Metrics

```csharp
public class RequestMetrics
{
    // Counter: Total requests
    private readonly Counter<long> _requestsTotal = Meter.CreateCounter<long>(
        "llm_gateway_requests_total",
        description: "Total number of requests processed");
    
    // Histogram: Request duration
    private readonly Histogram<double> _requestDuration = Meter.CreateHistogram<double>(
        "llm_gateway_request_duration_seconds",
        unit: "seconds",
        description: "Request processing duration");
    
    // Gauge: Active requests
    private readonly UpDownCounter<int> _activeRequests = Meter.CreateUpDownCounter<int>(
        "llm_gateway_active_requests",
        description: "Currently processing requests");
    
    public void RecordRequest(ProxyResult result, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "provider", result.Provider },
            { "action", result.Action.ToString() },
            { "department", result.Department },
            { "status", result.StatusCode.ToString() }
        };
        
        _requestsTotal.Add(1, tags);
        _requestDuration.Record(duration.TotalSeconds, tags);
    }
}
```

#### Sanitization Metrics

```prometheus
# Counter: Sanitization operations
llm_gateway_sanitizations_total{type="SERVER_NAMES",severity="MEDIUM"} 25000
llm_gateway_sanitizations_total{type="TABLE_NAMES",severity="MEDIUM"} 20000
llm_gateway_sanitizations_total{type="API_KEYS",severity="CRITICAL"} 150

# Counter: Blocked requests by reason
llm_gateway_blocked_total{reason="CRITICAL_PII"} 150
llm_gateway_blocked_total{reason="POLICY_VIOLATION"} 500
llm_gateway_blocked_total{reason="RATE_LIMIT"} 850

# Histogram: Sanitization processing time
llm_gateway_sanitization_duration_seconds_bucket{le="0.001"} 50000
llm_gateway_sanitization_duration_seconds_bucket{le="0.005"} 140000
llm_gateway_sanitization_duration_seconds_bucket{le="0.01"} 148000
llm_gateway_sanitization_duration_seconds_bucket{le="0.05"} 149500
llm_gateway_sanitization_duration_seconds_bucket{le="+Inf"} 150000
```

#### Session Metrics

```prometheus
# Gauge: Active sessions
llm_gateway_sessions_active 2500

# Counter: Session operations
llm_gateway_sessions_created_total 15000
llm_gateway_sessions_expired_total 12500
llm_gateway_sessions_cleared_total 500

# Histogram: Session size (mappings count)
llm_gateway_session_mappings_bucket{le="10"} 10000
llm_gateway_session_mappings_bucket{le="50"} 14000
llm_gateway_session_mappings_bucket{le="100"} 14900
llm_gateway_session_mappings_bucket{le="+Inf"} 15000
```

### 2.2 Infrastructure Metrics

```prometheus
# CPU and Memory
process_cpu_seconds_total
process_resident_memory_bytes
process_virtual_memory_bytes

# .NET Runtime
dotnet_gc_collections_total{generation="0|1|2"}
dotnet_gc_heap_size_bytes
dotnet_threadpool_threads_count
dotnet_threadpool_queue_length

# HTTP connections
http_client_active_requests{host="api.openai.com"}
http_client_request_duration_seconds{host="api.openai.com"}
```

### 2.3 Business Metrics

```prometheus
# Usage by department
llm_gateway_requests_by_department{department="Engineering"} 50000
llm_gateway_requests_by_department{department="Marketing"} 30000
llm_gateway_requests_by_department{department="Finance"} 10000

# Top users
llm_gateway_requests_by_user{user="john.smith@company.com"} 5000

# Provider usage
llm_gateway_requests_by_provider{provider="openai"} 80000
llm_gateway_requests_by_provider{provider="anthropic"} 10000

# Token consumption (estimated)
llm_gateway_tokens_total{direction="input"} 50000000
llm_gateway_tokens_total{direction="output"} 25000000
```

---

## 3. Logging

### 3.1 Log Format

```json
{
  "timestamp": "2026-01-13T10:30:00.123Z",
  "level": "Information",
  "messageTemplate": "Request {RequestId} processed in {Duration}ms",
  "message": "Request req_abc123 processed in 45ms",
  "properties": {
    "requestId": "req_abc123",
    "userId": "john.smith@company.com",
    "sessionId": "sess_xyz789",
    "department": "Engineering",
    "provider": "openai",
    "duration": 45,
    "wasSanitized": true,
    "action": "ALLOW_WITH_SANITIZATION"
  },
  "traceId": "abc123def456",
  "spanId": "span789",
  "source": "LLMGateway.SanitizationEngine"
}
```

### 3.2 Log Levels

| Level | Usage | Examples |
|-------|-------|----------|
| `Trace` | Very detailed debugging | Regex match details |
| `Debug` | Debugging information | Request/response bodies (sanitized) |
| `Information` | Normal operations | Request processed, session created |
| `Warning` | Potential issues | High-severity violation, rate limit near |
| `Error` | Errors that allow recovery | LLM provider timeout, retrying |
| `Critical` | System-threatening errors | Database unreachable, key vault down |

### 3.3 Structured Logging Implementation

```csharp
public class ProxyHandler
{
    private readonly ILogger<ProxyHandler> _logger;
    
    public async Task<ProxyResult> HandleAsync(ProxyRequest request)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["requestId"] = request.RequestId,
            ["userId"] = request.UserId,
            ["sessionId"] = request.SessionId,
            ["provider"] = request.Provider
        });
        
        _logger.LogInformation(
            "Processing proxy request {RequestId} for user {UserId}",
            request.RequestId,
            request.UserId);
        
        try
        {
            var result = await ProcessAsync(request);
            
            _logger.LogInformation(
                "Request {RequestId} completed with action {Action} in {Duration}ms",
                request.RequestId,
                result.Action,
                result.Duration.TotalMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Request {RequestId} failed with error: {ErrorMessage}",
                request.RequestId,
                ex.Message);
            throw;
        }
    }
}
```

### 3.4 Log Redaction

```csharp
public class LogRedactor
{
    private readonly Regex[] _redactionPatterns = new[]
    {
        new Regex(@"Authorization:\s*Bearer\s+\S+", RegexOptions.IgnoreCase),
        new Regex(@"api[_-]?key[=:]\s*\S+", RegexOptions.IgnoreCase),
        new Regex(@"\b\d{3}-\d{2}-\d{4}\b"), // SSN
        new Regex(@"\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b") // Credit card
    };
    
    public string Redact(string content)
    {
        foreach (var pattern in _redactionPatterns)
        {
            content = pattern.Replace(content, "[REDACTED]");
        }
        return content;
    }
}
```

---

## 4. Distributed Tracing

### 4.1 Trace Context

```csharp
public class TracingMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Extract or create trace context
        var traceParent = context.Request.Headers["traceparent"];
        using var activity = ActivitySource.StartActivity(
            "ProxyRequest",
            ActivityKind.Server,
            traceParent);
        
        activity?.SetTag("user.id", context.User.GetUserId());
        activity?.SetTag("request.id", context.TraceIdentifier);
        activity?.SetTag("provider", GetTargetProvider(context));
        
        try
        {
            await next(context);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

### 4.2 Trace Spans

```
┌────────────────────────────────────────────────────────────────────────────┐
│ ProxyRequest (45ms)                                                        │
├──────┬─────────────────────────────────────────────────────────────────────┤
│      │ AuthenticateUser (2ms)                                              │
├──────┼──────┬──────────────────────────────────────────────────────────────┤
│      │      │ EvaluatePolicy (3ms)                                         │
├──────┼──────┼──────┬───────────────────────────────────────────────────────┤
│      │      │      │ ScanCompliance (5ms)                                  │
├──────┼──────┼──────┼──────┬────────────────────────────────────────────────┤
│      │      │      │      │ SanitizeContent (8ms)                          │
│      │      │      │      │   └── GetOrCreateSession (2ms)                 │
│      │      │      │      │   └── ApplyPatterns (5ms)                      │
├──────┼──────┼──────┼──────┼────────────────────────────────────────────────┤
│      │      │      │      │ ForwardToLLM (20ms) ────────────────────────── │
│      │      │      │      │   └── HTTP POST api.openai.com                 │
├──────┼──────┼──────┼──────┼────────────────────────────────────────────────┤
│      │      │      │      │ DesanitizeResponse (4ms)                       │
├──────┼──────┼──────┼──────┼────────────────────────────────────────────────┤
│      │      │      │      │ AuditLog (3ms)                                 │
└──────┴──────┴──────┴──────┴────────────────────────────────────────────────┘
```

### 4.3 OpenTelemetry Configuration

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("LLMGateway")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRedisInstrumentation()
        .AddSqlClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://jaeger:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());
```

---

## 5. Dashboards

### 5.1 Operations Dashboard

```yaml
# Grafana dashboard configuration
dashboard:
  title: "LLM Gateway - Operations"
  rows:
    - title: "Request Overview"
      panels:
        - type: stat
          title: "Requests/sec"
          query: rate(llm_gateway_requests_total[5m])
          
        - type: stat
          title: "P99 Latency"
          query: histogram_quantile(0.99, rate(llm_gateway_request_duration_seconds_bucket[5m]))
          
        - type: stat
          title: "Error Rate"
          query: |
            sum(rate(llm_gateway_requests_total{status=~"5.."}[5m])) /
            sum(rate(llm_gateway_requests_total[5m])) * 100
            
    - title: "Sanitization"
      panels:
        - type: timeseries
          title: "Sanitization by Type"
          query: rate(llm_gateway_sanitizations_total[5m])
          
        - type: piechart
          title: "Violations by Severity"
          query: sum(llm_gateway_sanitizations_total) by (severity)
          
    - title: "System Health"
      panels:
        - type: gauge
          title: "CPU Usage"
          query: process_cpu_seconds_total
          
        - type: gauge
          title: "Memory Usage"
          query: process_resident_memory_bytes
          
        - type: timeseries
          title: "Active Sessions"
          query: llm_gateway_sessions_active
```

### 5.2 Security Dashboard

```yaml
dashboard:
  title: "LLM Gateway - Security"
  rows:
    - title: "Blocked Requests"
      panels:
        - type: stat
          title: "Blocked (24h)"
          query: sum(increase(llm_gateway_blocked_total[24h]))
          color: red
          
        - type: timeseries
          title: "Blocks by Reason"
          query: rate(llm_gateway_blocked_total[5m])
          
    - title: "High-Risk Activity"
      panels:
        - type: table
          title: "Top Violators"
          query: topk(10, sum(llm_gateway_sanitizations_total{severity="CRITICAL"}) by (user))
          
        - type: timeseries
          title: "Critical PII Detections"
          query: rate(llm_gateway_sanitizations_total{severity="CRITICAL"}[5m])
          
    - title: "Rate Limiting"
      panels:
        - type: stat
          title: "Rate Limited (24h)"
          query: sum(increase(llm_gateway_blocked_total{reason="RATE_LIMIT"}[24h]))
```

### 5.3 Business Dashboard

```yaml
dashboard:
  title: "LLM Gateway - Business Intelligence"
  rows:
    - title: "Usage Overview"
      panels:
        - type: stat
          title: "Total Requests (Today)"
          query: sum(increase(llm_gateway_requests_total[24h]))
          
        - type: stat
          title: "Active Users"
          query: count(count by (user) (llm_gateway_requests_total))
          
    - title: "Department Usage"
      panels:
        - type: piechart
          title: "Requests by Department"
          query: sum(llm_gateway_requests_total) by (department)
          
        - type: table
          title: "Department Details"
          query: |
            sum(llm_gateway_requests_total) by (department)
            + sum(llm_gateway_blocked_total) by (department)
            
    - title: "Provider Usage"
      panels:
        - type: piechart
          title: "Requests by Provider"
          query: sum(llm_gateway_requests_total) by (provider)
          
        - type: timeseries
          title: "Provider Trends"
          query: sum(rate(llm_gateway_requests_total[1h])) by (provider)
```

---

## 6. Alerting

### 6.1 Alert Rules

```yaml
# Prometheus alerting rules
groups:
  - name: llm-gateway-critical
    rules:
      - alert: HighErrorRate
        expr: |
          sum(rate(llm_gateway_requests_total{status=~"5.."}[5m])) /
          sum(rate(llm_gateway_requests_total[5m])) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High error rate ({{ $value | humanizePercentage }})"
          
      - alert: HighLatency
        expr: |
          histogram_quantile(0.99, rate(llm_gateway_request_duration_seconds_bucket[5m])) > 0.2
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "P99 latency above 200ms ({{ $value | humanizeDuration }})"
          
      - alert: CriticalPIISpike
        expr: |
          sum(increase(llm_gateway_sanitizations_total{severity="CRITICAL"}[5m])) > 10
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Spike in critical PII detections"
          
  - name: llm-gateway-warning
    rules:
      - alert: HighSanitizationRate
        expr: |
          sum(rate(llm_gateway_sanitizations_total[5m])) /
          sum(rate(llm_gateway_requests_total[5m])) > 0.95
        for: 15m
        labels:
          severity: warning
        annotations:
          summary: "Very high sanitization rate - check for mass data leakage attempt"
          
      - alert: SessionStoreHighLatency
        expr: |
          histogram_quantile(0.99, rate(redis_command_duration_seconds_bucket[5m])) > 0.05
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Redis latency high"
```

### 6.2 Alert Channels

```yaml
# Alertmanager configuration
receivers:
  - name: 'critical-pagerduty'
    pagerduty_configs:
      - service_key: 'xxx'
        severity: critical
        
  - name: 'warning-slack'
    slack_configs:
      - api_url: 'https://hooks.slack.com/services/xxx'
        channel: '#llm-gateway-alerts'
        
  - name: 'security-team'
    email_configs:
      - to: 'security-team@company.com'

route:
  group_by: ['alertname', 'severity']
  routes:
    - match:
        severity: critical
      receiver: 'critical-pagerduty'
      continue: true
    - match:
        alertname: CriticalPIISpike
      receiver: 'security-team'
    - match:
        severity: warning
      receiver: 'warning-slack'
```

---

## 7. Health Checks

### 7.1 Liveness & Readiness

```csharp
// Health check implementation
public class GatewayHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, object>();
        
        // Session store
        try
        {
            await _redis.PingAsync();
            checks["sessionStore"] = "healthy";
        }
        catch (Exception ex)
        {
            checks["sessionStore"] = $"unhealthy: {ex.Message}";
            return HealthCheckResult.Unhealthy("Session store unavailable", ex, checks);
        }
        
        // Audit store
        try
        {
            await _auditStore.HealthCheckAsync();
            checks["auditStore"] = "healthy";
        }
        catch (Exception ex)
        {
            checks["auditStore"] = $"unhealthy: {ex.Message}";
            return HealthCheckResult.Degraded("Audit store unavailable", ex, checks);
        }
        
        // LLM provider connectivity
        foreach (var provider in _providers)
        {
            try
            {
                await _httpClient.GetAsync($"https://api.{provider}.com/health");
                checks[$"provider_{provider}"] = "reachable";
            }
            catch
            {
                checks[$"provider_{provider}"] = "unreachable";
            }
        }
        
        return HealthCheckResult.Healthy("All systems operational", checks);
    }
}
```

### 7.2 Health Endpoints

```http
GET /health
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "sessionStore": {
      "status": "Healthy",
      "duration": "00:00:00.0012345"
    },
    "auditStore": {
      "status": "Healthy", 
      "duration": "00:00:00.0045678"
    },
    "keyVault": {
      "status": "Healthy",
      "duration": "00:00:00.0123456"
    }
  }
}

GET /ready
{
  "ready": true,
  "rulesLoaded": 25,
  "policiesLoaded": 150,
  "certificatesValid": true
}

GET /live
{
  "alive": true,
  "timestamp": "2026-01-13T10:30:00Z"
}
```

---

## 8. Log Aggregation

### 8.1 Splunk Integration

```csharp
public class SplunkSink : ILogEventSink
{
    private readonly HttpClient _client;
    private readonly string _hecEndpoint;
    private readonly string _hecToken;
    
    public void Emit(LogEvent logEvent)
    {
        var splunkEvent = new
        {
            time = logEvent.Timestamp.ToUnixTimeSeconds(),
            source = "llm-gateway",
            sourcetype = "llm-gateway:logs",
            @event = new
            {
                level = logEvent.Level.ToString(),
                message = logEvent.RenderMessage(),
                properties = logEvent.Properties
                    .ToDictionary(p => p.Key, p => p.Value.ToString())
            }
        };
        
        _client.PostAsync(_hecEndpoint, 
            JsonContent.Create(splunkEvent),
            new Dictionary<string, string>
            {
                ["Authorization"] = $"Splunk {_hecToken}"
            });
    }
}
```

### 8.2 ELK Stack Integration

```yaml
# Filebeat configuration
filebeat.inputs:
  - type: log
    paths:
      - /var/log/llm-gateway/*.log
    json.keys_under_root: true
    json.add_error_key: true
    
processors:
  - add_fields:
      target: ''
      fields:
        environment: production
        service: llm-gateway
        
output.elasticsearch:
  hosts: ["elasticsearch:9200"]
  index: "llm-gateway-%{+yyyy.MM.dd}"
```

