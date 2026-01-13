# Component Specifications

**Parent Document:** [TECHNICAL_SPECIFICATION.md](../TECHNICAL_SPECIFICATION.md)

---

## 1. Sanitization Engine

### 1.1 Overview

The Sanitization Engine is responsible for detecting and masking sensitive data in outbound requests using configurable regex patterns.

### 1.2 Interface Definition

```csharp
public interface ISanitizationEngine
{
    /// <summary>
    /// Sanitizes content using loaded rules and returns sanitized content + mappings created.
    /// </summary>
    SanitizationResult Sanitize(string content, Session session, UserPolicy policy);
    
    /// <summary>
    /// Validates content without modifying (dry-run).
    /// </summary>
    ValidationResult Validate(string content, UserPolicy policy);
    
    /// <summary>
    /// Reloads rules from configuration source.
    /// </summary>
    Task ReloadRulesAsync();
}

public class SanitizationResult
{
    public string SanitizedContent { get; set; }
    public bool WasSanitized { get; set; }
    public Dictionary<string, string> MappingsCreated { get; set; }  // Original → Alias
    public Violation[] ViolationsFound { get; set; }
    public int ProcessingTimeMs { get; set; }
    public bool ShouldBlock { get; set; }
    public string BlockReason { get; set; }
}
```

### 1.3 Processing Algorithm

```
FUNCTION Sanitize(content, session, policy):
    results = []
    workingContent = content
    
    // Load applicable rules
    rules = GetRulesForPolicy(policy)
    rules = SortByPriority(rules)
    
    FOR EACH rule IN rules:
        IF rule.Enabled AND IsApplicable(rule, policy):
            // Pre-compile regex (cached)
            regex = GetCompiledRegex(rule.Pattern)
            
            // Set timeout for regex execution
            matches = ExecuteWithTimeout(regex, workingContent, 50ms)
            
            FOR EACH match IN matches:
                // Check exceptions
                IF NOT IsException(match.Value, rule.Exceptions):
                    // Check severity action
                    IF rule.Severity == CRITICAL:
                        RETURN BlockResult(rule, match)
                    
                    // Generate or retrieve alias
                    alias = GetOrCreateAlias(session, match.Value, rule.Prefix)
                    
                    // Replace in working content
                    workingContent = Replace(workingContent, match, alias)
                    
                    // Record violation
                    results.Add(CreateViolation(rule, match, alias))
    
    RETURN SanitizationResult(workingContent, results)
```

### 1.4 Alias Generation

```csharp
public class AliasGenerator
{
    // Format: {PREFIX}_{COUNTER}
    // Examples: SERVER_0, TABLE_0, IP_1
    
    public string GenerateAlias(Session session, string prefix)
    {
        // Thread-safe counter per prefix per session
        var counter = session.GetNextCounter(prefix);
        return $"{prefix}_{counter}";
    }
}
```

### 1.5 Performance Requirements

| Metric | Target |
|--------|--------|
| Single rule evaluation | < 5ms |
| Full pipeline (50 rules) | < 100ms |
| Memory per rule | < 1KB (compiled regex) |
| Cache hit rate | > 95% |

### 1.6 Configuration

```json
{
  "SanitizationEngine": {
    "MaxRules": 500,
    "RegexTimeoutMs": 50,
    "EnableParallelProcessing": true,
    "MaxParallelism": 4,
    "CacheCompiledPatterns": true,
    "MaxContentLength": 1048576
  }
}
```

---

## 2. Desanitization Engine

### 2.1 Overview

The Desanitization Engine reverses alias mappings in LLM responses, restoring original values.

### 2.2 Interface Definition

```csharp
public interface IDesanitizationEngine
{
    /// <summary>
    /// Reverses aliases in response content back to original values.
    /// </summary>
    DesanitizationResult Desanitize(string content, Session session);
}

public class DesanitizationResult
{
    public string DesanitizedContent { get; set; }
    public int ReplacementsCount { get; set; }
    public string[] UnmatchedAliases { get; set; }  // Aliases not in session
    public int ProcessingTimeMs { get; set; }
}
```

### 2.3 Processing Algorithm

```
FUNCTION Desanitize(content, session):
    workingContent = content
    replacements = 0
    unmatched = []
    
    // Build regex from all known aliases
    aliases = session.ReverseMappings.Keys
    aliasPattern = BuildAlternationPattern(aliases)  // (SERVER_0|TABLE_0|IP_0|...)
    
    regex = Compile(aliasPattern)
    matches = regex.FindAll(workingContent)
    
    FOR EACH match IN matches:
        alias = match.Value
        IF session.ReverseMappings.Contains(alias):
            original = session.ReverseMappings[alias]
            workingContent = Replace(workingContent, match, original)
            replacements++
        ELSE:
            unmatched.Add(alias)
    
    RETURN DesanitizationResult(workingContent, replacements, unmatched)
```

### 2.4 Edge Cases

| Case | Handling |
|------|----------|
| Alias not in session | Log warning, leave alias in response |
| Session expired | Return error, suggest re-submitting |
| Alias appears multiple times | Replace all occurrences |
| Nested JSON response | Parse JSON, process string values recursively |

---

## 3. Policy Engine

### 3.1 Overview

The Policy Engine evaluates user/department access permissions and determines allowed actions.

### 3.2 Interface Definition

```csharp
public interface IPolicyEngine
{
    /// <summary>
    /// Evaluates request against applicable policies.
    /// </summary>
    PolicyDecision Evaluate(PolicyContext context);
    
    /// <summary>
    /// Gets effective policy for user (merges user + department defaults).
    /// </summary>
    UserPolicy GetEffectivePolicy(string userId, string department);
    
    /// <summary>
    /// Updates policy at runtime.
    /// </summary>
    Task UpdatePolicyAsync(UserPolicy policy);
}

public class PolicyContext
{
    public string UserId { get; set; }
    public string Department { get; set; }
    public string LlmProvider { get; set; }
    public string Content { get; set; }
    public string SourceIp { get; set; }
    public DateTime RequestTime { get; set; }
}

public class PolicyDecision
{
    public PolicyAction Action { get; set; }        // ALLOW, WARN, BLOCK
    public string Reason { get; set; }
    public string PolicyApplied { get; set; }
    public string[] RequiredSanitization { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public enum PolicyAction
{
    Allow,
    AllowWithWarning,
    Block
}
```

### 3.3 Policy Evaluation Order

```
1. Check if user exists in system
   └── If not: Apply default guest policy (usually BLOCK)

2. Check user-specific policy
   └── If exists: Use as base

3. Merge with department defaults
   └── User settings override department defaults

4. Evaluate access level
   ├── UNRESTRICTED: Skip sanitization
   ├── SANITIZED_ONLY: Require sanitization
   └── BLOCKED: Reject request

5. Check provider allowlist
   └── Is requested LLM provider in allowed list?

6. Check rate limits
   └── Has user exceeded daily/hourly/burst limits?

7. Check time-based rules (optional)
   └── Is request during allowed hours?

8. Check IP-based rules (optional)
   └── Is source IP in whitelist/blacklist?

9. Return decision with applicable sanitization rules
```

### 3.4 Policy Merge Logic

```csharp
public UserPolicy MergePolicy(UserPolicy userPolicy, DepartmentPolicy deptPolicy)
{
    return new UserPolicy
    {
        // User settings take precedence
        AccessLevel = userPolicy?.AccessLevel ?? deptPolicy.AccessLevel,
        
        // More restrictive limit wins
        DailyRequestLimit = Math.Min(
            userPolicy?.DailyRequestLimit ?? int.MaxValue,
            deptPolicy.DailyRequestLimit
        ),
        
        // Intersection of allowed providers
        AllowedProviders = Intersect(
            userPolicy?.AllowedProviders,
            deptPolicy.AllowedProviders
        ),
        
        // Union of required sanitization
        RequiredSanitizationRules = Union(
            userPolicy?.RequiredSanitizationRules,
            deptPolicy.RequiredSanitizationRules
        )
    };
}
```

---

## 4. Compliance Detector

### 4.1 Overview

The Compliance Detector identifies regulated data (PII, secrets, credentials) requiring special handling.

### 4.2 Interface Definition

```csharp
public interface IComplianceDetector
{
    /// <summary>
    /// Scans content for compliance-relevant data.
    /// </summary>
    ComplianceResult Scan(string content);
}

public class ComplianceResult
{
    public bool HasViolations { get; set; }
    public bool ShouldBlock { get; set; }           // Any CRITICAL findings
    public ComplianceViolation[] Violations { get; set; }
    public ComplianceSummary Summary { get; set; }
}

public class ComplianceViolation
{
    public string Type { get; set; }                // SSN, CREDIT_CARD, API_KEY, etc.
    public ViolationSeverity Severity { get; set; }
    public string Pattern { get; set; }
    public string RedactedMatch { get; set; }       // "4111****1111"
    public int Position { get; set; }
    public int Length { get; set; }
    public string Recommendation { get; set; }      // What to do
}
```

### 4.3 Detection Patterns

#### PII Detection

| Type | Pattern | Validation | Severity |
|------|---------|------------|----------|
| SSN | `\b\d{3}-\d{2}-\d{4}\b` | Format check | CRITICAL |
| Credit Card | `\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b` | Luhn algorithm | CRITICAL |
| Phone (US) | `\b\d{3}[-.]\d{3}[-.]\d{4}\b` | Format check | MEDIUM |
| Email | `\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z]{2,}\b` | Domain check | LOW-HIGH* |

*Internal email domains = HIGH, external = LOW

#### Secrets Detection

| Type | Pattern | Validation | Severity |
|------|---------|------------|----------|
| AWS Key | `AKIA[0-9A-Z]{16}` | Prefix check | CRITICAL |
| GitHub Token | `ghp_[a-zA-Z0-9]{36}` | Prefix check | CRITICAL |
| Generic API Key | `(?i)api[_-]?key\s*[:=]\s*['"]?[a-zA-Z0-9]{20,}` | Length check | HIGH |
| Private Key | `-----BEGIN (RSA )?PRIVATE KEY-----` | Header check | CRITICAL |
| Password in string | `(?i)password\s*[:=]\s*['"]?[^\s'"]{8,}` | Context check | HIGH |

#### Infrastructure Detection

| Type | Pattern | Validation | Severity |
|------|---------|------------|----------|
| Private IP | `\b(10\.\d{1,3}|172\.(1[6-9]|2\d|3[01])|192\.168)\.\d{1,3}\.\d{1,3}\b` | RFC 1918 | MEDIUM |
| Connection String | `(?i)(server|data source)=[^;]+;` | Format check | HIGH |
| File Path (Windows) | `[A-Za-z]:\\[^*?"<>|:\n]+` | Drive letter | MEDIUM |
| UNC Path | `\\\\[a-zA-Z0-9.-]+\\` | Format check | MEDIUM |

### 4.4 Luhn Algorithm Implementation

```csharp
public static bool IsValidCreditCard(string number)
{
    // Remove spaces and dashes
    var digits = number.Where(char.IsDigit).ToArray();
    if (digits.Length < 13 || digits.Length > 19)
        return false;
    
    int sum = 0;
    bool alternate = false;
    
    for (int i = digits.Length - 1; i >= 0; i--)
    {
        int digit = digits[i] - '0';
        
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
```

---

## 5. Mapping Manager

### 5.1 Overview

The Mapping Manager maintains bidirectional mappings between original values and aliases, scoped to user sessions.

### 5.2 Interface Definition

```csharp
public interface IMappingManager
{
    /// <summary>
    /// Gets or creates a session for the user.
    /// </summary>
    Task<Session> GetOrCreateSessionAsync(string userId, string department);
    
    /// <summary>
    /// Adds a mapping to the session.
    /// </summary>
    void AddMapping(Session session, string original, string alias);
    
    /// <summary>
    /// Gets alias for original value (null if not mapped).
    /// </summary>
    string GetAlias(Session session, string original);
    
    /// <summary>
    /// Gets original value for alias (null if not mapped).
    /// </summary>
    string GetOriginal(Session session, string alias);
    
    /// <summary>
    /// Exports session mappings (encrypted).
    /// </summary>
    Task<byte[]> ExportSessionAsync(string sessionId);
    
    /// <summary>
    /// Clears session and all mappings.
    /// </summary>
    Task ClearSessionAsync(string sessionId);
}
```

### 5.3 Session Storage Options

#### Option A: In-Memory (Single Instance)

```csharp
public class InMemoryMappingManager : IMappingManager
{
    private readonly ConcurrentDictionary<string, Session> _sessions;
    private readonly IEncryptionService _encryption;
    
    // Sessions stored in memory with periodic cleanup
}
```

**Pros:** Fast, simple
**Cons:** Lost on restart, single-instance only

#### Option B: Redis (Multi-Instance)

```csharp
public class RedisMappingManager : IMappingManager
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IEncryptionService _encryption;
    
    // Sessions stored in Redis with encryption
    // Key format: "session:{sessionId}"
    // TTL set on keys for auto-expiry
}
```

**Pros:** Shared across instances, survives restarts
**Cons:** Network latency, additional infrastructure

#### Option C: Hybrid (Recommended)

```csharp
public class HybridMappingManager : IMappingManager
{
    private readonly IMemoryCache _localCache;
    private readonly RedisMappingManager _redis;
    
    // L1: Local memory cache (fast reads)
    // L2: Redis (persistence, sharing)
    // Write-through on add, read-through on miss
}
```

### 5.4 Session Data Structure

```
Redis Key: session:{sessionId}
Redis Type: Hash

Fields:
  - user_id: string
  - department: string
  - created_at: ISO8601
  - expires_at: ISO8601
  - mappings: encrypted JSON
  - request_count: int
  - encryption_key: encrypted (with master key)

TTL: Matches expires_at
```

### 5.5 Encryption

```csharp
public class SessionEncryption
{
    // Each session has its own encryption key
    // Session key is encrypted with master key from Key Vault
    
    public byte[] EncryptMappings(Dictionary<string, string> mappings, byte[] sessionKey)
    {
        var json = JsonSerializer.Serialize(mappings);
        return AesGcm.Encrypt(json, sessionKey);
    }
    
    public Dictionary<string, string> DecryptMappings(byte[] encrypted, byte[] sessionKey)
    {
        var json = AesGcm.Decrypt(encrypted, sessionKey);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
    }
}
```

---

## 6. Audit Logger

### 6.1 Overview

The Audit Logger creates immutable, cryptographically signed records of all gateway activity.

### 6.2 Interface Definition

```csharp
public interface IAuditLogger
{
    /// <summary>
    /// Logs an audit entry (fire-and-forget for performance).
    /// </summary>
    void Log(AuditEntry entry);
    
    /// <summary>
    /// Logs and waits for confirmation (for critical events).
    /// </summary>
    Task LogAndConfirmAsync(AuditEntry entry);
    
    /// <summary>
    /// Queries audit logs.
    /// </summary>
    Task<AuditQueryResult> QueryAsync(AuditQuery query);
    
    /// <summary>
    /// Verifies integrity of audit chain.
    /// </summary>
    Task<IntegrityCheckResult> VerifyIntegrityAsync(DateTime from, DateTime to);
}
```

### 6.3 Entry Signing

```csharp
public class AuditEntrySigner
{
    private readonly Ed25519Signer _signer;
    
    public string SignEntry(AuditEntry entry)
    {
        // Create canonical representation
        var canonical = CreateCanonical(entry);
        
        // Sign with Ed25519
        var signature = _signer.Sign(canonical);
        
        return Convert.ToBase64String(signature);
    }
    
    private byte[] CreateCanonical(AuditEntry entry)
    {
        // Deterministic JSON serialization
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            entry.Timestamp,
            entry.UserId,
            entry.SessionId,
            entry.RequestHash,
            entry.ActionTaken,
            entry.PreviousEntryHash
        }, new JsonSerializerOptions { WriteIndented = false });
    }
}
```

### 6.4 Chain Integrity

```
Entry N:
{
  "entryId": "abc123",
  "previousEntryHash": "sha256(Entry N-1)",
  "signature": "ed25519(canonical)",
  ...
}

Entry N+1:
{
  "entryId": "def456",
  "previousEntryHash": "sha256(Entry N)",
  "signature": "ed25519(canonical)",
  ...
}

Verification:
1. Recompute hash of Entry N
2. Compare with previousEntryHash in Entry N+1
3. If mismatch: tampering detected
```

### 6.5 Destinations

```csharp
public interface IAuditDestination
{
    Task WriteAsync(AuditEntry entry);
    Task<bool> IsHealthyAsync();
}

// Implementations:
public class FileAuditDestination : IAuditDestination { }
public class SplunkAuditDestination : IAuditDestination { }
public class ElasticAuditDestination : IAuditDestination { }
public class SyslogAuditDestination : IAuditDestination { }
public class SqlAuditDestination : IAuditDestination { }
```

### 6.6 Buffering & Reliability

```csharp
public class BufferedAuditLogger : IAuditLogger
{
    private readonly Channel<AuditEntry> _buffer;
    private readonly IAuditDestination[] _destinations;
    
    // Buffer entries in memory
    // Background worker processes buffer
    // On destination failure: retry with exponential backoff
    // Dead letter queue for undeliverable entries
}
```

---

## 7. Rate Limiter

### 7.1 Overview

The Rate Limiter prevents abuse by enforcing request quotas at multiple time windows.

### 7.2 Interface Definition

```csharp
public interface IRateLimiter
{
    /// <summary>
    /// Checks if request is allowed under rate limits.
    /// </summary>
    RateLimitResult CheckLimit(string userId, string department);
    
    /// <summary>
    /// Records a request (call after successful processing).
    /// </summary>
    void RecordRequest(string userId);
    
    /// <summary>
    /// Gets current usage for user.
    /// </summary>
    RateLimitUsage GetUsage(string userId);
}

public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public int RemainingRequests { get; set; }
    public TimeSpan RetryAfter { get; set; }
    public string LimitType { get; set; }           // DAILY, HOURLY, BURST
}
```

### 7.3 Algorithm: Sliding Window

```csharp
public class SlidingWindowRateLimiter : IRateLimiter
{
    // Uses Redis sorted sets for distributed rate limiting
    
    public RateLimitResult CheckLimit(string userId, string department)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var policy = GetPolicy(userId, department);
        
        // Check burst limit (last minute)
        var burstWindow = now - TimeSpan.FromMinutes(1).TotalMilliseconds;
        var burstCount = CountRequestsInWindow(userId, burstWindow, now);
        if (burstCount >= policy.BurstLimit)
            return Blocked("BURST", CalculateRetryAfter(userId, burstWindow));
        
        // Check hourly limit
        var hourlyWindow = now - TimeSpan.FromHours(1).TotalMilliseconds;
        var hourlyCount = CountRequestsInWindow(userId, hourlyWindow, now);
        if (hourlyCount >= policy.HourlyRequestLimit)
            return Blocked("HOURLY", CalculateRetryAfter(userId, hourlyWindow));
        
        // Check daily limit
        var dailyWindow = GetStartOfDay(now);
        var dailyCount = CountRequestsInWindow(userId, dailyWindow, now);
        if (dailyCount >= policy.DailyRequestLimit)
            return Blocked("DAILY", GetTimeUntilMidnight());
        
        return Allowed(policy.DailyRequestLimit - dailyCount);
    }
}
```

### 7.4 Redis Implementation

```
Key: ratelimit:{userId}
Type: Sorted Set
Score: Unix timestamp (milliseconds)
Member: Request ID (UUID)

Operations:
- ZADD ratelimit:user@example.com {timestamp} {requestId}
- ZREMRANGEBYSCORE ratelimit:user@example.com -inf {windowStart}
- ZCOUNT ratelimit:user@example.com {windowStart} {windowEnd}

TTL: 24 hours (auto-cleanup)
```

---

## 8. Encryption Service

### 8.1 Overview

The Encryption Service provides cryptographic operations for protecting sensitive data.

### 8.2 Interface Definition

```csharp
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts data using AES-256-GCM.
    /// </summary>
    EncryptedData Encrypt(byte[] plaintext, byte[] key = null);
    
    /// <summary>
    /// Decrypts data using AES-256-GCM.
    /// </summary>
    byte[] Decrypt(EncryptedData encrypted, byte[] key = null);
    
    /// <summary>
    /// Generates a new encryption key.
    /// </summary>
    byte[] GenerateKey();
    
    /// <summary>
    /// Retrieves master key from Key Vault.
    /// </summary>
    Task<byte[]> GetMasterKeyAsync();
    
    /// <summary>
    /// Rotates master key.
    /// </summary>
    Task RotateKeyAsync();
}

public class EncryptedData
{
    public byte[] Ciphertext { get; set; }
    public byte[] Nonce { get; set; }              // 12 bytes for AES-GCM
    public byte[] Tag { get; set; }                // 16 bytes authentication tag
    public string KeyId { get; set; }              // Which key version was used
}
```

### 8.3 Key Hierarchy

```
                    ┌─────────────────────┐
                    │   Azure Key Vault   │
                    │    (Master Key)     │
                    └──────────┬──────────┘
                               │
                    ┌──────────▼──────────┐
                    │   Key Encryption    │
                    │   Key (KEK)         │
                    └──────────┬──────────┘
                               │
          ┌────────────────────┼────────────────────┐
          │                    │                    │
┌─────────▼─────────┐ ┌───────▼───────┐ ┌─────────▼─────────┐
│ Session Key 1     │ │ Session Key 2 │ │ Session Key N     │
│ (Per-User)        │ │               │ │                   │
└─────────┬─────────┘ └───────┬───────┘ └─────────┬─────────┘
          │                   │                   │
          ▼                   ▼                   ▼
    Session Data         Session Data        Session Data
```

### 8.4 AES-256-GCM Implementation

```csharp
public class AesGcmEncryption
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;  // 256 bits
    
    public EncryptedData Encrypt(byte[] plaintext, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];
        
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        
        return new EncryptedData
        {
            Ciphertext = ciphertext,
            Nonce = nonce,
            Tag = tag
        };
    }
    
    public byte[] Decrypt(EncryptedData encrypted, byte[] key)
    {
        var plaintext = new byte[encrypted.Ciphertext.Length];
        
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(encrypted.Nonce, encrypted.Ciphertext, encrypted.Tag, plaintext);
        
        return plaintext;
    }
}
```

---

## 9. HTTP Proxy Component

### 9.1 Overview

The HTTP Proxy intercepts and forwards requests to LLM providers.

### 9.2 Interface Definition

```csharp
public interface IProxyHandler
{
    /// <summary>
    /// Handles incoming proxy request.
    /// </summary>
    Task<ProxyResponse> HandleRequestAsync(ProxyRequest request);
}

public class ProxyRequest
{
    public string Method { get; set; }
    public Uri TargetUrl { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public byte[] Body { get; set; }
    public string UserId { get; set; }
    public string SessionId { get; set; }
}

public class ProxyResponse
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public byte[] Body { get; set; }
    public ProxyMetadata Metadata { get; set; }
}
```

### 9.3 HTTPS Interception (MITM)

```
Client                    Gateway                     LLM Provider
  │                          │                              │
  │ CONNECT api.openai.com   │                              │
  │─────────────────────────▶│                              │
  │                          │                              │
  │◀──────────────────────── │                              │
  │ 200 Connection Established                              │
  │                          │                              │
  │ TLS Handshake           │                              │
  │─────────────────────────▶│                              │
  │  (Gateway certificate)   │                              │
  │                          │                              │
  │◀──────────────────────── │                              │
  │ TLS Established          │                              │
  │                          │                              │
  │ POST /v1/chat/completions│                              │
  │─────────────────────────▶│ TLS Handshake               │
  │                          │─────────────────────────────▶│
  │                          │  (Real certificate)          │
  │                          │                              │
  │                          │ Sanitized Request            │
  │                          │─────────────────────────────▶│
  │                          │                              │
  │                          │◀─────────────────────────────│
  │                          │ Response                     │
  │                          │                              │
  │◀─────────────────────────│                              │
  │ Desanitized Response     │                              │
```

### 9.4 Certificate Management

```csharp
public class CertificateManager
{
    /// <summary>
    /// Generates a certificate for the target host, signed by our CA.
    /// </summary>
    public X509Certificate2 GetOrCreateCertificate(string hostname)
    {
        // Check cache
        if (_cache.TryGetValue(hostname, out var cert))
            return cert;
        
        // Generate new certificate
        cert = GenerateCertificate(hostname);
        _cache[hostname] = cert;
        
        return cert;
    }
    
    private X509Certificate2 GenerateCertificate(string hostname)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={hostname}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        
        // Add SAN
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(hostname);
        request.CertificateExtensions.Add(sanBuilder.Build());
        
        // Sign with CA
        return request.Create(
            _caCertificate,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddYears(1),
            Guid.NewGuid().ToByteArray()
        );
    }
}
```

### 9.5 Provider Routing

```csharp
public class ProviderRouter
{
    private readonly Dictionary<string, ProviderConfig> _providers = new()
    {
        ["api.openai.com"] = new ProviderConfig { Name = "openai", RequiresSanitization = true },
        ["api.anthropic.com"] = new ProviderConfig { Name = "anthropic", RequiresSanitization = true },
        ["*.openai.azure.com"] = new ProviderConfig { Name = "azure_openai", RequiresSanitization = true },
        ["generativelanguage.googleapis.com"] = new ProviderConfig { Name = "google", RequiresSanitization = true }
    };
    
    public ProviderConfig GetProvider(string hostname)
    {
        // Direct match
        if (_providers.TryGetValue(hostname, out var config))
            return config;
        
        // Wildcard match
        foreach (var (pattern, cfg) in _providers.Where(p => p.Key.Contains('*')))
        {
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            if (Regex.IsMatch(hostname, regex))
                return cfg;
        }
        
        // Unknown provider - passthrough without sanitization
        return new ProviderConfig { Name = "unknown", RequiresSanitization = false };
    }
}
```

---

## 10. Configuration Manager

### 10.1 Overview

The Configuration Manager loads, validates, and provides access to runtime configuration.

### 10.2 Configuration Sources (Priority Order)

1. Environment variables (highest)
2. User secrets (development only)
3. appsettings.{Environment}.json
4. appsettings.json
5. Default values (lowest)

### 10.3 Hot Reload Support

```csharp
public class ConfigurationManager : IDisposable
{
    private readonly IOptionsMonitor<GatewayConfig> _monitor;
    
    public GatewayConfig Current => _monitor.CurrentValue;
    
    public ConfigurationManager(IOptionsMonitor<GatewayConfig> monitor)
    {
        _monitor = monitor;
        _monitor.OnChange(OnConfigurationChanged);
    }
    
    private void OnConfigurationChanged(GatewayConfig newConfig)
    {
        // Validate new configuration
        var validation = Validate(newConfig);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Invalid configuration change rejected: {Errors}", 
                validation.Errors);
            return;
        }
        
        // Notify components
        ConfigurationChanged?.Invoke(this, newConfig);
    }
}
```

### 10.4 Configuration Validation

```csharp
public class GatewayConfigValidator : IValidateOptions<GatewayConfig>
{
    public ValidateOptionsResult Validate(string name, GatewayConfig config)
    {
        var errors = new List<string>();
        
        if (config.Port < 1 || config.Port > 65535)
            errors.Add("Port must be between 1 and 65535");
        
        if (config.SessionTimeoutMinutes < 1)
            errors.Add("Session timeout must be at least 1 minute");
        
        if (config.RateLimiting.DailyLimit < config.RateLimiting.HourlyLimit)
            errors.Add("Daily limit must be >= hourly limit");
        
        // Validate regex patterns compile
        foreach (var rule in config.SanitizationRules)
        {
            try { _ = new Regex(rule.Pattern); }
            catch (RegexParseException ex)
            {
                errors.Add($"Invalid regex in rule '{rule.Name}': {ex.Message}");
            }
        }
        
        return errors.Any()
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
```

