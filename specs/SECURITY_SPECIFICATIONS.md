# Security Specifications

**Parent Document:** [TECHNICAL_SPECIFICATION.md](../TECHNICAL_SPECIFICATION.md)

---

## 1. Security Architecture Overview

### 1.1 Security Principles

| Principle | Implementation |
|-----------|----------------|
| **Defense in Depth** | Multiple security layers (auth, policy, sanitization, audit) |
| **Least Privilege** | Users get minimum required access level |
| **Fail Secure** | On error, block request rather than allow unsanitized |
| **Zero Trust** | Verify every request, don't trust network location |
| **Data Minimization** | Only collect/log necessary information |
| **Separation of Duties** | Admin actions require different role than user access |

### 1.2 Security Zones

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          UNTRUSTED ZONE                                  │
│                     (External LLM Providers)                             │
│        OpenAI, Anthropic, Azure OpenAI, Google AI                       │
└────────────────────────────────┬────────────────────────────────────────┘
                                 │ TLS 1.3 (Outbound)
                                 │ Certificate Pinning
┌────────────────────────────────▼────────────────────────────────────────┐
│                            DMZ ZONE                                      │
│                      (LLM Gateway Proxy)                                │
│    ┌────────────────────────────────────────────────────────────────┐  │
│    │  • Request sanitization                                        │  │
│    │  • Response desanitization                                     │  │
│    │  • Policy enforcement                                          │  │
│    │  • Audit logging                                               │  │
│    └────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────┬────────────────────────────────────────┘
                                 │ TLS 1.3 (Inbound)
                                 │ mTLS optional
┌────────────────────────────────▼────────────────────────────────────────┐
│                          TRUSTED ZONE                                    │
│                    (Corporate Network)                                   │
│         VSCode, JetBrains IDEs, Custom Applications                     │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Authentication & Authorization

### 2.1 Authentication Methods

#### 2.1.1 OAuth 2.0 / OIDC (Recommended)

```
┌──────────┐       ┌──────────────┐       ┌─────────────┐
│  Client  │──────▶│  Identity    │──────▶│ LLM Gateway │
│          │       │  Provider    │       │             │
│          │◀──────│ (Azure AD)   │◀──────│             │
│          │       │              │       │             │
└──────────┘       └──────────────┘       └─────────────┘
     │                   │                      │
     │ 1. Auth request   │                      │
     │──────────────────▶│                      │
     │                   │                      │
     │ 2. Login + MFA    │                      │
     │◀──────────────────│                      │
     │                   │                      │
     │ 3. ID Token +     │                      │
     │    Access Token   │                      │
     │◀──────────────────│                      │
     │                   │                      │
     │ 4. Request with   │                      │
     │    Bearer token   │                      │
     │─────────────────────────────────────────▶│
     │                   │                      │
     │                   │ 5. Validate token    │
     │                   │◀─────────────────────│
     │                   │                      │
     │                   │ 6. Token valid +     │
     │                   │    user claims       │
     │                   │─────────────────────▶│
```

**Token Validation:**
```csharp
public class TokenValidator
{
    public async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _config.Issuer,
            ValidateAudience = true,
            ValidAudience = _config.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            IssuerSigningKeys = await GetSigningKeysAsync(),
            ValidateIssuerSigningKey = true
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, parameters, out _);
    }
}
```

#### 2.1.2 API Key Authentication

For service-to-service communication:

```csharp
public class ApiKeyValidator
{
    public async Task<ApiKeyValidationResult> ValidateAsync(string apiKey)
    {
        // Keys stored hashed (SHA-256)
        var hash = ComputeHash(apiKey);
        
        var record = await _store.GetByHashAsync(hash);
        if (record == null)
            return ApiKeyValidationResult.Invalid();
        
        if (record.ExpiresAt < DateTime.UtcNow)
            return ApiKeyValidationResult.Expired();
        
        if (!record.IsEnabled)
            return ApiKeyValidationResult.Disabled();
        
        return ApiKeyValidationResult.Valid(record.ServiceId, record.Scopes);
    }
}
```

**API Key Format:**
```
sk_{environment}_{random32chars}
Examples:
  sk_prod_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6
  sk_dev_x9y8z7w6v5u4t3s2r1q0p9o8n7m6l5k4
```

#### 2.1.3 Windows Integrated Authentication

For intranet deployments:

```csharp
// Startup.cs
services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

// Extracts user from Kerberos ticket
// Claims include: SID, Groups, Department (from AD attributes)
```

### 2.2 Authorization Model (RBAC)

#### 2.2.1 Roles

| Role | Permissions |
|------|-------------|
| `User` | Proxy requests, view own sessions, view own stats |
| `PowerUser` | User + extended rate limits, more providers |
| `PolicyAdmin` | User + manage user policies, view all user stats |
| `RuleAdmin` | User + manage sanitization rules |
| `Auditor` | Read-only access to all audit logs |
| `Admin` | All permissions + system operations |

#### 2.2.2 Permission Matrix

| Permission | User | PowerUser | PolicyAdmin | RuleAdmin | Auditor | Admin |
|------------|------|-----------|-------------|-----------|---------|-------|
| Proxy requests | ✓ | ✓ | ✓ | ✓ | ✗ | ✓ |
| View own sessions | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| View all sessions | ✗ | ✗ | ✓ | ✗ | ✓ | ✓ |
| Manage policies | ✗ | ✗ | ✓ | ✗ | ✗ | ✓ |
| Manage rules | ✗ | ✗ | ✗ | ✓ | ✗ | ✓ |
| View audit logs | Own | Own | Dept | ✗ | All | All |
| Export audit logs | ✗ | ✗ | ✗ | ✗ | ✓ | ✓ |
| System operations | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ |

#### 2.2.3 Role Assignment

```json
// Via Azure AD Groups
{
  "roleMapping": {
    "SG-LLMGateway-Users": "User",
    "SG-LLMGateway-PowerUsers": "PowerUser",
    "SG-LLMGateway-PolicyAdmins": "PolicyAdmin",
    "SG-LLMGateway-RuleAdmins": "RuleAdmin",
    "SG-LLMGateway-Auditors": "Auditor",
    "SG-LLMGateway-Admins": "Admin"
  }
}
```

### 2.3 Multi-Factor Authentication

**Trigger Conditions:**
- Admin operations always require MFA
- User has `requiresMfa: true` in policy
- Access from unknown IP/device
- After 7 days since last MFA

**Implementation:**
```csharp
public class MfaEnforcement
{
    public bool RequiresMfa(ClaimsPrincipal user, string operation)
    {
        // Admin ops always need MFA
        if (IsAdminOperation(operation))
            return true;
        
        // Check user policy
        var policy = GetUserPolicy(user);
        if (policy.RequiresMfa)
            return true;
        
        // Check last MFA time
        var lastMfa = GetLastMfaTime(user);
        if (lastMfa < DateTime.UtcNow.AddDays(-7))
            return true;
        
        // Check device/IP trust
        if (!IsTrustedContext(user))
            return true;
        
        return false;
    }
}
```

---

## 3. Encryption

### 3.1 Data at Rest Encryption

#### 3.1.1 Session Mappings

```
Algorithm: AES-256-GCM
Key Size: 256 bits
Nonce: 12 bytes (random per encryption)
Tag: 16 bytes (authentication tag)

Hierarchy:
┌─────────────────────────────────────┐
│         Master Key (KEK)            │
│   (Azure Key Vault / HashiCorp)     │
│   Rotation: 365 days                │
└─────────────────┬───────────────────┘
                  │
┌─────────────────▼───────────────────┐
│      Data Encryption Key (DEK)       │
│   (Per-environment: prod/staging)    │
│   Rotation: 90 days                  │
└─────────────────┬───────────────────┘
                  │
┌─────────────────▼───────────────────┐
│       Session Encryption Key         │
│   (Per-session, derived from DEK)   │
│   Lifetime: Session duration        │
└─────────────────────────────────────┘
```

#### 3.1.2 Audit Logs

```
Sensitive fields encrypted:
- IP addresses (searchable encryption)
- User agents
- Violation details

Non-encrypted (for querying):
- Timestamp
- User ID
- Action taken
- Severity level
```

#### 3.1.3 Configuration Secrets

```json
// appsettings.json - secrets referenced via Key Vault
{
  "Encryption": {
    "KeyVaultUri": "https://company-keyvault.vault.azure.net",
    "MasterKeyName": "llm-gateway-master-key",
    "DataKeyName": "llm-gateway-data-key"
  },
  "Database": {
    "ConnectionString": "@Microsoft.KeyVault(SecretUri=https://...)"
  }
}
```

### 3.2 Data in Transit Encryption

#### 3.2.1 TLS Configuration

```csharp
public class TlsConfiguration
{
    public void ConfigureKestrel(KestrelServerOptions options)
    {
        options.ConfigureHttpsDefaults(https =>
        {
            // TLS 1.3 preferred, 1.2 minimum
            https.SslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12;
            
            // Strong cipher suites only
            // TLS 1.3: TLS_AES_256_GCM_SHA384, TLS_CHACHA20_POLY1305_SHA256
            // TLS 1.2: ECDHE+AESGCM, DHE+AESGCM
            
            // Certificate
            https.ServerCertificate = LoadCertificate();
            
            // Optional: mTLS for client verification
            https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
        });
    }
}
```

#### 3.2.2 Certificate Pinning (Outbound)

```csharp
public class CertificatePinningHandler : HttpClientHandler
{
    private readonly Dictionary<string, string[]> _pins = new()
    {
        ["api.openai.com"] = new[] { 
            "sha256/AAAA...", // Primary
            "sha256/BBBB..."  // Backup
        },
        ["api.anthropic.com"] = new[] { 
            "sha256/CCCC..." 
        }
    };
    
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            var host = message.RequestUri.Host;
            if (!_pins.TryGetValue(host, out var expectedPins))
                return true; // No pin configured
            
            var certHash = ComputeHash(cert.RawData);
            return expectedPins.Any(pin => pin == certHash);
        };
        
        return await base.SendAsync(request, cancellationToken);
    }
}
```

### 3.3 Key Rotation

```csharp
public class KeyRotationService
{
    public async Task RotateDataKeyAsync()
    {
        // 1. Generate new key version
        var newKey = await _keyVault.CreateKeyVersionAsync(_dataKeyName);
        
        // 2. Mark rotation in progress
        await _state.SetRotationStatusAsync("IN_PROGRESS", newKey.Version);
        
        // 3. Re-encrypt active sessions with new key
        await ReencryptSessionsAsync(newKey);
        
        // 4. Re-encrypt recent audit logs
        await ReencryptAuditLogsAsync(newKey, TimeSpan.FromDays(30));
        
        // 5. Mark rotation complete
        await _state.SetRotationStatusAsync("COMPLETE", newKey.Version);
        
        // 6. Schedule old key deletion (after grace period)
        await _scheduler.ScheduleKeyDeletionAsync(
            oldKey.Version, 
            TimeSpan.FromDays(7)
        );
    }
}
```

---

## 4. Threat Model

### 4.1 STRIDE Analysis

| Threat | Category | Mitigation |
|--------|----------|------------|
| User impersonation | Spoofing | OAuth 2.0/OIDC with MFA |
| Audit log modification | Tampering | Cryptographic signatures, append-only |
| Unauthorized data access | Information Disclosure | Encryption, RBAC |
| Bypass sanitization | Information Disclosure | Multiple encoding detection, strict parsing |
| Session hijacking | Information Disclosure | Session encryption, IP binding |
| Service disruption | Denial of Service | Rate limiting, resource quotas |
| Privilege escalation | Elevation of Privilege | Strict RBAC, audit admin actions |

### 4.2 Attack Vectors & Mitigations

#### 4.2.1 Encoding Bypass

**Attack:** Send sensitive data in Base64/URL encoding to bypass regex.

**Mitigation:**
```csharp
public class ContentNormalizer
{
    public string Normalize(string content)
    {
        var decoded = content;
        
        // Multiple decode passes
        for (int i = 0; i < 3; i++)
        {
            var previous = decoded;
            
            // URL decode
            decoded = WebUtility.UrlDecode(decoded);
            
            // Base64 decode (if looks like Base64)
            if (IsLikelyBase64(decoded))
                decoded = TryBase64Decode(decoded) ?? decoded;
            
            // Unicode normalization
            decoded = decoded.Normalize(NormalizationForm.FormC);
            
            // Stop if no changes
            if (decoded == previous)
                break;
        }
        
        return decoded;
    }
}
```

#### 4.2.2 Session Fixation

**Attack:** Attacker creates session, tricks user into using it.

**Mitigation:**
```csharp
public class SessionManager
{
    public async Task<Session> CreateSessionAsync(string userId)
    {
        // Generate unpredictable session ID
        var sessionId = GenerateSecureSessionId();
        
        // Bind to user identity (cannot be transferred)
        var session = new Session
        {
            SessionId = sessionId,
            UserId = userId,
            BoundIpAddress = GetClientIp(), // Optional IP binding
            CreatedFromToken = GetTokenJti(), // Bind to auth token
            EncryptionKey = GenerateSessionKey()
        };
        
        return session;
    }
    
    public bool ValidateSession(Session session, ClaimsPrincipal user)
    {
        // Verify session belongs to authenticated user
        if (session.UserId != user.GetUserId())
            return false;
        
        // Verify IP if binding enabled
        if (session.BoundIpAddress != null && 
            session.BoundIpAddress != GetClientIp())
            return false;
        
        return true;
    }
}
```

#### 4.2.3 Regex DoS (ReDoS)

**Attack:** Craft input that causes regex backtracking.

**Mitigation:**
```csharp
public class SafeRegexExecutor
{
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(50);
    
    public MatchCollection Execute(Regex regex, string input)
    {
        // Use regex with timeout
        var safeRegex = new Regex(
            regex.ToString(),
            regex.Options,
            _timeout
        );
        
        try
        {
            return safeRegex.Matches(input);
        }
        catch (RegexMatchTimeoutException)
        {
            _logger.LogWarning("Regex timeout for pattern: {Pattern}", 
                regex.ToString());
            _metrics.IncrementReDoSAttempts();
            throw;
        }
    }
}
```

#### 4.2.4 Audit Log Injection

**Attack:** Include control characters or escape sequences in logged data.

**Mitigation:**
```csharp
public class AuditSanitizer
{
    public string SanitizeForLog(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        
        // Remove control characters
        var sanitized = Regex.Replace(value, @"[\x00-\x1F\x7F]", "");
        
        // Escape for JSON (if logging as JSON)
        sanitized = JsonEncodedText.Encode(sanitized).ToString();
        
        // Truncate to max length
        if (sanitized.Length > MaxLogFieldLength)
            sanitized = sanitized[..MaxLogFieldLength] + "...";
        
        return sanitized;
    }
}
```

---

## 5. Compliance Controls

### 5.1 SOC 2 Type II

| Control | Implementation |
|---------|----------------|
| CC6.1 - Logical Access | RBAC, authentication required |
| CC6.2 - Authentication | OAuth 2.0 + MFA |
| CC6.3 - Authorization | Policy engine, least privilege |
| CC7.1 - Detection | Real-time monitoring, alerting |
| CC7.2 - Monitoring | Audit logs, SIEM integration |
| CC7.3 - Evaluation | Automated compliance checks |

### 5.2 HIPAA

| Requirement | Implementation |
|-------------|----------------|
| Access Control (164.312(a)(1)) | RBAC, unique user IDs |
| Audit Controls (164.312(b)) | Comprehensive audit logging |
| Integrity Controls (164.312(c)(1)) | Signed audit entries |
| Transmission Security (164.312(e)(1)) | TLS 1.3 encryption |
| Encryption (164.312(a)(2)(iv)) | AES-256-GCM |

### 5.3 GDPR

| Requirement | Implementation |
|-------------|----------------|
| Article 25 - Data Protection by Design | Sanitization by default |
| Article 30 - Records of Processing | Audit log retention |
| Article 32 - Security | Encryption, access controls |
| Article 33 - Breach Notification | Alerting, incident response |
| Article 17 - Right to Erasure | Session/mapping deletion |

### 5.4 PCI-DSS v4.0

| Requirement | Implementation |
|-------------|----------------|
| 3.4 - Render PAN unreadable | Credit card detection + blocking |
| 7.1 - Limit access | RBAC |
| 8.2 - Unique IDs | OAuth identity |
| 10.2 - Audit trails | Comprehensive logging |
| 12.3 - Cryptography | TLS 1.3, AES-256 |

---

## 6. Security Monitoring

### 6.1 Security Events

| Event | Severity | Action |
|-------|----------|--------|
| CRITICAL_PII_DETECTED | Critical | Block, alert security team |
| AUTH_FAILURE_THRESHOLD | High | Temporary lockout, alert |
| UNUSUAL_ACCESS_PATTERN | High | Alert, review required |
| POLICY_VIOLATION | Medium | Log, optional alert |
| RATE_LIMIT_EXCEEDED | Medium | Log, throttle |
| SESSION_ANOMALY | Medium | Force re-authentication |

### 6.2 SIEM Integration

```json
// Splunk HEC Event Format
{
  "time": 1705142400,
  "source": "llm-gateway",
  "sourcetype": "security:llm-gateway",
  "event": {
    "eventType": "CRITICAL_PII_DETECTED",
    "severity": "CRITICAL",
    "userId": "john.smith@company.com",
    "department": "Engineering",
    "violationType": "API_KEY",
    "ipAddress": "192.168.1.100",
    "userAgent": "VSCode/1.85",
    "action": "BLOCKED"
  }
}
```

### 6.3 Security Alerts

```yaml
# Alert Rules
alerts:
  - name: critical_pii_detected
    condition: eventType == "CRITICAL_PII_DETECTED"
    severity: critical
    channels: [pagerduty, slack-security]
    
  - name: auth_failures_spike
    condition: count(eventType == "AUTH_FAILURE") > 100 in 5m
    severity: high
    channels: [pagerduty, email-security]
    
  - name: unusual_volume
    condition: requests_per_user > baseline * 5
    severity: medium
    channels: [slack-security]
```

---

## 7. Incident Response

### 7.1 Security Incident Playbook

```
┌─────────────────────────────────────────────────────────────────┐
│                    SECURITY INCIDENT DETECTED                    │
└─────────────────────────────┬───────────────────────────────────┘
                              │
              ┌───────────────▼───────────────┐
              │   1. TRIAGE (0-15 minutes)    │
              │   • Assess severity           │
              │   • Identify affected scope   │
              │   • Page security team        │
              └───────────────┬───────────────┘
                              │
              ┌───────────────▼───────────────┐
              │   2. CONTAIN (15-60 minutes)  │
              │   • Block affected user(s)    │
              │   • Invalidate sessions       │
              │   • Preserve evidence         │
              └───────────────┬───────────────┘
                              │
              ┌───────────────▼───────────────┐
              │   3. INVESTIGATE (1-24 hours) │
              │   • Review audit logs         │
              │   • Identify root cause       │
              │   • Assess data exposure      │
              └───────────────┬───────────────┘
                              │
              ┌───────────────▼───────────────┐
              │   4. REMEDIATE (24-72 hours)  │
              │   • Fix vulnerability         │
              │   • Update detection rules    │
              │   • Rotate affected keys      │
              └───────────────┬───────────────┘
                              │
              ┌───────────────▼───────────────┐
              │   5. REPORT (72+ hours)       │
              │   • Document timeline         │
              │   • Notify stakeholders       │
              │   • Regulatory notification   │
              └─────────────────────────────  ┘
```

### 7.2 Emergency Actions

```csharp
public class EmergencyControls
{
    // Immediately block all requests (kill switch)
    public async Task ActivateKillSwitchAsync()
    {
        await _config.SetAsync("Gateway.Enabled", false);
        await _alerting.SendCriticalAsync("Kill switch activated");
    }
    
    // Block specific user
    public async Task BlockUserAsync(string userId)
    {
        await _policyStore.UpdateAsync(userId, p => p.AccessLevel = AccessLevel.Blocked);
        await _sessionStore.InvalidateAllSessionsAsync(userId);
    }
    
    // Force rotate all keys
    public async Task EmergencyKeyRotationAsync()
    {
        await _keyRotation.RotateMasterKeyAsync();
        await _keyRotation.RotateDataKeyAsync();
        await _sessionStore.InvalidateAllSessionsAsync();
    }
}
```

---

## 8. Security Testing

### 8.1 Security Test Cases

| Category | Test Case | Expected Result |
|----------|-----------|-----------------|
| Auth | Missing token | 401 Unauthorized |
| Auth | Expired token | 401 Unauthorized |
| Auth | Invalid signature | 401 Unauthorized |
| Auth | Revoked token | 401 Unauthorized |
| AuthZ | Access other user's session | 403 Forbidden |
| AuthZ | User accessing admin endpoint | 403 Forbidden |
| Injection | SQL injection in query params | Sanitized/blocked |
| Injection | XSS in user input | Sanitized |
| Encryption | Decrypt with wrong key | Failure |
| Encryption | Tampered ciphertext | Integrity check failure |
| DoS | ReDoS pattern | Timeout, blocked |
| DoS | Exceed rate limit | 429 response |

### 8.2 Penetration Testing Requirements

- **Frequency:** Annual + after major changes
- **Scope:** Full application, API, infrastructure
- **Types:** Black box, gray box, white box
- **Standards:** OWASP Testing Guide v4.2
- **Remediation SLA:** Critical 7 days, High 30 days, Medium 90 days

### 8.3 Vulnerability Scanning

```yaml
# Scheduled scans
scans:
  dependency_scan:
    tool: snyk
    frequency: daily
    targets: [*.csproj, packages.json]
    
  container_scan:
    tool: trivy
    frequency: on_build
    targets: [Dockerfile]
    
  sast_scan:
    tool: semgrep
    frequency: on_commit
    rules: [p/owasp-top-ten, p/csharp]
    
  dast_scan:
    tool: zap
    frequency: weekly
    targets: [https://gateway-staging.company.com]
```

