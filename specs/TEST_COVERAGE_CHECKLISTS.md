# Test Coverage Checklists

**Purpose:** Ensure 100% end-to-end test coverage for all LLM Gateway specifications.

---

## Table of Contents

1. [Sanitization Engine Tests](#1-sanitization-engine-tests)
2. [Desanitization Engine Tests](#2-desanitization-engine-tests)
3. [Policy Engine Tests](#3-policy-engine-tests)
4. [Compliance Detector Tests](#4-compliance-detector-tests)
5. [Session Management Tests](#5-session-management-tests)
6. [Audit Logging Tests](#6-audit-logging-tests)
7. [Rate Limiting Tests](#7-rate-limiting-tests)
8. [Encryption Service Tests](#8-encryption-service-tests)
9. [HTTP Proxy Tests](#9-http-proxy-tests)
10. [API Endpoint Tests](#10-api-endpoint-tests)
11. [Integration Tests](#11-integration-tests)
12. [End-to-End Scenario Tests](#12-end-to-end-scenario-tests)
13. [Security Tests](#13-security-tests)
14. [Performance Tests](#14-performance-tests)

---

## 1. Sanitization Engine Tests

### 1.1 Pattern Matching

| Test ID | Test Case | Input | Expected Output | Status |
|---------|-----------|-------|-----------------|--------|
| SAN-001 | Server name detection (basic) | `"Query ServerDB01"` | `"Query SERVER_0"` | ☐ |
| SAN-002 | Server name detection (multiple) | `"Join ServerDB01 and ServerDB02"` | `"Join SERVER_0 and SERVER_1"` | ☐ |
| SAN-003 | Server name detection (case insensitive) | `"Query serverdb01"` | `"Query SERVER_0"` | ☐ |
| SAN-004 | Server name exception list | `"server_error occurred"` | `"server_error occurred"` (unchanged) | ☐ |
| SAN-005 | Table name detection (schema.table) | `"FROM users_prod.accounts"` | `"FROM TABLE_0"` | ☐ |
| SAN-006 | Table name detection (SELECT) | `"SELECT * FROM orders"` | `"SELECT * FROM TABLE_0"` | ☐ |
| SAN-007 | Table name detection (INSERT) | `"INSERT INTO logs"` | `"INSERT INTO TABLE_0"` | ☐ |
| SAN-008 | Table name detection (UPDATE) | `"UPDATE customers SET"` | `"UPDATE TABLE_0 SET"` | ☐ |
| SAN-009 | Table name detection (DELETE) | `"DELETE FROM sessions"` | `"DELETE FROM TABLE_0"` | ☐ |
| SAN-010 | IP address detection (10.x.x.x) | `"Connect to 10.0.0.50"` | `"Connect to IP_0"` | ☐ |
| SAN-011 | IP address detection (192.168.x.x) | `"Server at 192.168.1.100"` | `"Server at IP_0"` | ☐ |
| SAN-012 | IP address detection (172.16-31.x.x) | `"Host 172.20.5.10"` | `"Host IP_0"` | ☐ |
| SAN-013 | Public IP not masked | `"Google DNS 8.8.8.8"` | `"Google DNS 8.8.8.8"` (unchanged) | ☐ |
| SAN-014 | Email domain detection | `"user@company.internal"` | `"user@EMAIL_0"` | ☐ |
| SAN-015 | Email domain exception | `"user@gmail.com"` | `"user@gmail.com"` (unchanged) | ☐ |
| SAN-016 | File path detection (Windows) | `"C:\Projects\secret"` | `"PATH_0"` | ☐ |
| SAN-017 | File path detection (UNC) | `"\\\\server\\share\\file"` | `"PATH_0"` | ☐ |
| SAN-018 | File path exception | `"C:\Windows\System32"` | `"C:\Windows\System32"` (unchanged) | ☐ |

### 1.2 Alias Generation

| Test ID | Test Case | Scenario | Expected Behavior | Status |
|---------|-----------|----------|-------------------|--------|
| SAN-020 | Unique alias per original | Same value twice in one request | Same alias used both times | ☐ |
| SAN-021 | Sequential alias numbering | Multiple different servers | SERVER_0, SERVER_1, SERVER_2 | ☐ |
| SAN-022 | Cross-category numbering | Server + Table + IP | SERVER_0, TABLE_0, IP_0 (independent) | ☐ |
| SAN-023 | Session persistence | Same value in different requests | Same alias if same session | ☐ |
| SAN-024 | New session new aliases | Same value, different session | Different alias (starts from 0) | ☐ |

### 1.3 Edge Cases

| Test ID | Test Case | Input | Expected Behavior | Status |
|---------|-----------|-------|-------------------|--------|
| SAN-030 | Empty string | `""` | Empty string returned | ☐ |
| SAN-031 | Null input | `null` | ArgumentNullException or empty | ☐ |
| SAN-032 | Very long string | 1MB of text | Processes within timeout | ☐ |
| SAN-033 | Unicode content | `"サーバー ServerDB01"` | Only ServerDB01 masked | ☐ |
| SAN-034 | Nested JSON | `{"query": "ServerDB01"}` | Sanitizes within JSON | ☐ |
| SAN-035 | URL encoded | `"Server%44B01"` | Decodes then sanitizes | ☐ |
| SAN-036 | Base64 encoded | `U2VydmVyREIwMQ==` | Detects and sanitizes | ☐ |
| SAN-037 | Multiple encodings | Double URL encoded | Handles recursive decode | ☐ |
| SAN-038 | HTML entities | `"Server&#68;B01"` | Decodes and sanitizes | ☐ |
| SAN-039 | Regex special chars | `"Server.DB01"` | Handles literal dots | ☐ |

### 1.4 Performance

| Test ID | Test Case | Constraint | Pass Criteria | Status |
|---------|-----------|------------|---------------|--------|
| SAN-040 | Small content latency | 100 bytes | < 5ms | ☐ |
| SAN-041 | Medium content latency | 10KB | < 20ms | ☐ |
| SAN-042 | Large content latency | 100KB | < 100ms | ☐ |
| SAN-043 | Pattern timeout | Evil regex input | Timeout at 50ms, not hang | ☐ |
| SAN-044 | Memory allocation | 1000 requests | < 1KB per request | ☐ |
| SAN-045 | Concurrent processing | 100 parallel | No race conditions | ☐ |

---

## 2. Desanitization Engine Tests

### 2.1 Basic Desanitization

| Test ID | Test Case | Input | Expected Output | Status |
|---------|-----------|-------|-----------------|--------|
| DES-001 | Single alias replacement | `"Query SERVER_0"` | `"Query ServerDB01"` | ☐ |
| DES-002 | Multiple same alias | `"SERVER_0 and SERVER_0"` | `"ServerDB01 and ServerDB01"` | ☐ |
| DES-003 | Multiple different aliases | `"SERVER_0 TABLE_0"` | `"ServerDB01 users_prod"` | ☐ |
| DES-004 | Alias in JSON response | `{"server": "SERVER_0"}` | `{"server": "ServerDB01"}` | ☐ |
| DES-005 | Alias in code block | `\`\`\`sql SELECT * FROM TABLE_0\`\`\`` | Original table name restored | ☐ |
| DES-006 | Partial alias match | `"SERVER_0_extra"` | Not replaced (exact match only) | ☐ |
| DES-007 | Case sensitivity | `"server_0"` | Not replaced (case sensitive) | ☐ |

### 2.2 Session Handling

| Test ID | Test Case | Scenario | Expected Behavior | Status |
|---------|-----------|----------|-------------------|--------|
| DES-010 | Valid session | Alias exists in session | Replaced with original | ☐ |
| DES-011 | Expired session | Session TTL exceeded | Error or alias left as-is | ☐ |
| DES-012 | Unknown alias | Alias not in session | Left unchanged, logged | ☐ |
| DES-013 | Wrong session | Alias from different session | Not replaced | ☐ |
| DES-014 | Session cleared | Session was manually cleared | Error returned | ☐ |

### 2.3 Edge Cases

| Test ID | Test Case | Input | Expected Behavior | Status |
|---------|-----------|-------|-------------------|--------|
| DES-020 | Empty response | `""` | Empty string returned | ☐ |
| DES-021 | No aliases in response | `"Just some text"` | Unchanged | ☐ |
| DES-022 | Large response | 1MB LLM response | Processes efficiently | ☐ |
| DES-023 | Streaming response | Chunked response | Each chunk desanitized | ☐ |

---

## 3. Policy Engine Tests

### 3.1 Access Level Evaluation

| Test ID | Test Case | User Config | Expected Action | Status |
|---------|-----------|-------------|-----------------|--------|
| POL-001 | Unrestricted user | AccessLevel: UNRESTRICTED | ALLOW (no sanitization) | ☐ |
| POL-002 | Sanitized user | AccessLevel: SANITIZED_ONLY | ALLOW with sanitization | ☐ |
| POL-003 | Blocked user | AccessLevel: BLOCKED | BLOCK | ☐ |
| POL-004 | Unknown user | User not in system | Apply default policy | ☐ |
| POL-005 | Disabled user | enabled: false | BLOCK | ☐ |

### 3.2 Provider Restrictions

| Test ID | Test Case | Allowed Providers | Request Provider | Expected | Status |
|---------|-----------|-------------------|------------------|----------|--------|
| POL-010 | Allowed provider | ["openai"] | openai | ALLOW | ☐ |
| POL-011 | Disallowed provider | ["openai"] | anthropic | BLOCK | ☐ |
| POL-012 | Multiple allowed | ["openai", "anthropic"] | anthropic | ALLOW | ☐ |
| POL-013 | Empty allowed list | [] | any | BLOCK all | ☐ |
| POL-014 | Unknown provider | ["openai"] | unknown | BLOCK | ☐ |

### 3.3 Department Policies

| Test ID | Test Case | User Dept | Dept Policy | Expected | Status |
|---------|-----------|-----------|-------------|----------|--------|
| POL-020 | Engineering defaults | Engineering | 500 req/day | Limit applied | ☐ |
| POL-021 | Finance defaults | Finance | 100 req/day | Limit applied | ☐ |
| POL-022 | User overrides dept | User: 750, Dept: 500 | 750 (user wins) | ☐ |
| POL-023 | Dept more restrictive | User: null, Dept: 100 | 100 (dept applied) | ☐ |
| POL-024 | Unknown department | Unknown | Default policy | ☐ |

### 3.4 Policy Merging

| Test ID | Test Case | User Policy | Dept Policy | Expected Result | Status |
|---------|-----------|-------------|-------------|-----------------|--------|
| POL-030 | User limit wins | limit: 750 | limit: 500 | 750 | ☐ |
| POL-031 | Provider intersection | ["openai","anthropic"] | ["openai"] | ["openai"] | ☐ |
| POL-032 | Sanitization union | ["SERVER"] | ["TABLE"] | ["SERVER","TABLE"] | ☐ |
| POL-033 | MFA requirement | mfa: false | mfa: true | true (more secure) | ☐ |

---

## 4. Compliance Detector Tests

### 4.1 PII Detection

| Test ID | Test Case | Input | Detection | Severity | Status |
|---------|-----------|-------|-----------|----------|--------|
| CMP-001 | SSN format | `"SSN: 123-45-6789"` | Detected | CRITICAL | ☐ |
| CMP-002 | SSN no dashes | `"SSN: 123456789"` | Not detected (invalid format) | N/A | ☐ |
| CMP-003 | Credit card (Visa) | `"4111111111111111"` | Detected + Luhn valid | CRITICAL | ☐ |
| CMP-004 | Credit card (MC) | `"5500000000000004"` | Detected + Luhn valid | CRITICAL | ☐ |
| CMP-005 | Credit card (Amex) | `"340000000000009"` | Detected + Luhn valid | CRITICAL | ☐ |
| CMP-006 | Credit card invalid | `"4111111111111112"` | Detected but Luhn invalid | HIGH | ☐ |
| CMP-007 | Credit card with spaces | `"4111 1111 1111 1111"` | Detected | CRITICAL | ☐ |
| CMP-008 | Credit card with dashes | `"4111-1111-1111-1111"` | Detected | CRITICAL | ☐ |
| CMP-009 | Phone number (US) | `"Call 555-123-4567"` | Detected | MEDIUM | ☐ |
| CMP-010 | Email (internal) | `"user@company.com"` | Detected | HIGH | ☐ |
| CMP-011 | Email (external) | `"user@gmail.com"` | Detected | LOW | ☐ |

### 4.2 Secrets Detection

| Test ID | Test Case | Input | Detection | Severity | Status |
|---------|-----------|-------|-----------|----------|--------|
| CMP-020 | AWS access key | `"AKIAIOSFODNN7EXAMPLE"` | Detected | CRITICAL | ☐ |
| CMP-021 | AWS secret key | `"wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"` | Detected | CRITICAL | ☐ |
| CMP-022 | GitHub token | `"ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"` | Detected | CRITICAL | ☐ |
| CMP-023 | Generic API key | `"api_key=abc123def456ghi789"` | Detected | HIGH | ☐ |
| CMP-024 | API key variations | `"apiKey: xyz...", "API-KEY=..."` | Detected | HIGH | ☐ |
| CMP-025 | Private key header | `"-----BEGIN RSA PRIVATE KEY-----"` | Detected | CRITICAL | ☐ |
| CMP-026 | Password in string | `"password=MySecret123"` | Detected | HIGH | ☐ |
| CMP-027 | Connection string | `"Server=db;Password=secret"` | Detected | CRITICAL | ☐ |
| CMP-028 | JWT token | `"eyJhbGciOiJIUzI1NiIs..."` | Detected | HIGH | ☐ |

### 4.3 Severity Actions

| Test ID | Test Case | Severity | Policy Setting | Expected Action | Status |
|---------|-----------|----------|----------------|-----------------|--------|
| CMP-030 | Critical blocks | CRITICAL | BlockOnCritical: true | BLOCK | ☐ |
| CMP-031 | Critical allowed | CRITICAL | BlockOnCritical: false | ALLOW + warn | ☐ |
| CMP-032 | High severity | HIGH | AllowedSeverities: [HIGH] | ALLOW | ☐ |
| CMP-033 | High not allowed | HIGH | AllowedSeverities: [LOW,MED] | BLOCK | ☐ |
| CMP-034 | Multiple violations | CRITICAL + HIGH | BlockOnCritical: true | BLOCK (critical) | ☐ |

---

## 5. Session Management Tests

### 5.1 Session Lifecycle

| Test ID | Test Case | Action | Expected Result | Status |
|---------|-----------|--------|-----------------|--------|
| SES-001 | Create new session | First request from user | Session created with ID | ☐ |
| SES-002 | Reuse existing session | Request with session ID | Same session returned | ☐ |
| SES-003 | Session expiration | TTL exceeded | Session marked expired | ☐ |
| SES-004 | Session extension | Extend by 1 hour | New expiry time set | ☐ |
| SES-005 | Session clear | DELETE session | Session removed | ☐ |
| SES-006 | Auto cleanup | Background job | Expired sessions removed | ☐ |

### 5.2 Mapping Storage

| Test ID | Test Case | Scenario | Expected Result | Status |
|---------|-----------|----------|-----------------|--------|
| SES-010 | Add mapping | New original value | Mapping stored | ☐ |
| SES-011 | Get existing mapping | Same value again | Same alias returned | ☐ |
| SES-012 | Reverse lookup | Alias to original | Original returned | ☐ |
| SES-013 | Large mapping count | 10,000 mappings | All stored correctly | ☐ |
| SES-014 | Mapping persistence | Server restart | Mappings restored (Redis) | ☐ |

### 5.3 Session Isolation

| Test ID | Test Case | Scenario | Expected Result | Status |
|---------|-----------|----------|-----------------|--------|
| SES-020 | User isolation | User A vs User B | Different sessions | ☐ |
| SES-021 | Cross-user access denied | User A accesses B's session | FORBIDDEN | ☐ |
| SES-022 | Same user different sessions | User creates new session | Independent mappings | ☐ |

### 5.4 Encryption

| Test ID | Test Case | Scenario | Expected Result | Status |
|---------|-----------|----------|-----------------|--------|
| SES-030 | Mappings encrypted at rest | Check Redis storage | Data encrypted | ☐ |
| SES-031 | Decrypt on read | Load session | Mappings decrypted correctly | ☐ |
| SES-032 | Wrong key fails | Tampered key | Decryption fails | ☐ |
| SES-033 | Key rotation | New key version | Re-encryption successful | ☐ |

---

## 6. Audit Logging Tests

### 6.1 Entry Creation

| Test ID | Test Case | Request Type | Expected Log Fields | Status |
|---------|-----------|--------------|---------------------|--------|
| AUD-001 | Successful request | Normal proxy | All fields populated | ☐ |
| AUD-002 | Blocked request | Policy violation | Action: BLOCK, reason logged | ☐ |
| AUD-003 | Rate limited | Quota exceeded | Action: RATE_LIMITED | ☐ |
| AUD-004 | Sanitized request | Contains sensitive | WasSanitized: true | ☐ |
| AUD-005 | Unsanitized request | No sensitive data | WasSanitized: false | ☐ |

### 6.2 Required Fields

| Test ID | Test Case | Field | Validation | Status |
|---------|-----------|-------|------------|--------|
| AUD-010 | Timestamp present | timestamp | ISO8601 format | ☐ |
| AUD-011 | User ID present | userId | Non-empty | ☐ |
| AUD-012 | Session ID present | sessionId | Valid format | ☐ |
| AUD-013 | Request hash | requestHash | SHA256 format | ☐ |
| AUD-014 | Signature present | signature | Ed25519 valid | ☐ |
| AUD-015 | Chain link | previousEntryHash | Matches previous | ☐ |

### 6.3 Log Integrity

| Test ID | Test Case | Scenario | Expected Result | Status |
|---------|-----------|----------|-----------------|--------|
| AUD-020 | Signature verification | Valid entry | Signature valid | ☐ |
| AUD-021 | Tampered content | Modified field | Signature invalid | ☐ |
| AUD-022 | Chain integrity | Sequential entries | Chain valid | ☐ |
| AUD-023 | Chain broken | Missing entry | Chain invalid detected | ☐ |
| AUD-024 | Verify date range | 1 month of logs | All entries valid | ☐ |

### 6.4 Destinations

| Test ID | Test Case | Destination | Expected Result | Status |
|---------|-----------|-------------|-----------------|--------|
| AUD-030 | File destination | Local file | Written successfully | ☐ |
| AUD-031 | Splunk destination | Splunk HEC | Received by Splunk | ☐ |
| AUD-032 | Elastic destination | Elasticsearch | Indexed correctly | ☐ |
| AUD-033 | SQL destination | PostgreSQL | Inserted into table | ☐ |
| AUD-034 | Multi-destination | File + Splunk | Both receive entry | ☐ |
| AUD-035 | Destination failure | Splunk down | Retry + dead letter | ☐ |

---

## 7. Rate Limiting Tests

### 7.1 Limit Enforcement

| Test ID | Test Case | Limit | Requests | Expected Result | Status |
|---------|-----------|-------|----------|-----------------|--------|
| RAT-001 | Under daily limit | 500/day | 499 | All allowed | ☐ |
| RAT-002 | At daily limit | 500/day | 500 | Last allowed | ☐ |
| RAT-003 | Over daily limit | 500/day | 501 | 501st blocked | ☐ |
| RAT-004 | Under hourly limit | 50/hour | 49 | All allowed | ☐ |
| RAT-005 | Over hourly limit | 50/hour | 51 | 51st blocked | ☐ |
| RAT-006 | Burst limit | 10/min | 11 rapid | 11th blocked | ☐ |
| RAT-007 | Limit reset | After 24h | New day | Counter reset | ☐ |

### 7.2 Response Headers

| Test ID | Test Case | Scenario | Expected Headers | Status |
|---------|-----------|----------|------------------|--------|
| RAT-010 | Rate limit headers | Any request | X-RateLimit-* present | ☐ |
| RAT-011 | Remaining count | 450/500 used | Remaining: 50 | ☐ |
| RAT-012 | Reset time | Limit exceeded | Reset: Unix timestamp | ☐ |
| RAT-013 | Retry-After | 429 response | Retry-After: seconds | ☐ |

### 7.3 Per-User Limits

| Test ID | Test Case | User Config | Expected Limit | Status |
|---------|-----------|-------------|----------------|--------|
| RAT-020 | User custom limit | dailyLimit: 750 | 750 enforced | ☐ |
| RAT-021 | Department default | No user config | Dept limit used | ☐ |
| RAT-022 | Global default | No user/dept config | Global default | ☐ |
| RAT-023 | Power user | Higher limits | Extended limits work | ☐ |

---

## 8. Encryption Service Tests

### 8.1 AES-256-GCM

| Test ID | Test Case | Operation | Expected Result | Status |
|---------|-----------|-----------|-----------------|--------|
| ENC-001 | Encrypt plaintext | 1KB data | Ciphertext returned | ☐ |
| ENC-002 | Decrypt ciphertext | Valid ciphertext | Original plaintext | ☐ |
| ENC-003 | Wrong key decrypt | Different key | Decryption fails | ☐ |
| ENC-004 | Tampered ciphertext | Modified bytes | Auth tag fails | ☐ |
| ENC-005 | Tampered tag | Modified tag | Verification fails | ☐ |
| ENC-006 | Nonce uniqueness | 1000 encryptions | All nonces unique | ☐ |

### 8.2 Key Management

| Test ID | Test Case | Operation | Expected Result | Status |
|---------|-----------|-----------|-----------------|--------|
| ENC-010 | Generate key | New key | 256-bit key returned | ☐ |
| ENC-011 | Load master key | From Key Vault | Key retrieved | ☐ |
| ENC-012 | Key rotation | Rotate master | New version created | ☐ |
| ENC-013 | Key hierarchy | Derive session key | Unique per session | ☐ |
| ENC-014 | Key vault unavailable | Connection lost | Graceful degradation | ☐ |

---

## 9. HTTP Proxy Tests

### 9.1 Request Forwarding

| Test ID | Test Case | Request | Expected Behavior | Status |
|---------|-----------|---------|-------------------|--------|
| PRX-001 | Forward GET | GET /v1/models | Forwarded to provider | ☐ |
| PRX-002 | Forward POST | POST /v1/chat | Body forwarded | ☐ |
| PRX-003 | Preserve headers | Authorization header | Header forwarded | ☐ |
| PRX-004 | Add gateway headers | X-Request-ID | Header added | ☐ |
| PRX-005 | Remove hop headers | Connection: keep-alive | Header removed | ☐ |

### 9.2 Provider Routing

| Test ID | Test Case | Target URL | Expected Provider | Status |
|---------|-----------|------------|-------------------|--------|
| PRX-010 | OpenAI routing | api.openai.com | openai | ☐ |
| PRX-011 | Anthropic routing | api.anthropic.com | anthropic | ☐ |
| PRX-012 | Azure OpenAI | *.openai.azure.com | azure_openai | ☐ |
| PRX-013 | Google AI | generativelanguage.googleapis.com | google | ☐ |
| PRX-014 | Unknown provider | unknown.example.com | Passthrough | ☐ |

### 9.3 TLS Handling

| Test ID | Test Case | Scenario | Expected Behavior | Status |
|---------|-----------|----------|-------------------|--------|
| PRX-020 | HTTPS interception | CONNECT request | MITM established | ☐ |
| PRX-021 | Certificate generation | New host | Cert generated | ☐ |
| PRX-022 | Certificate caching | Same host again | Cached cert used | ☐ |
| PRX-023 | Certificate pinning | Outbound to provider | Pin verified | ☐ |
| PRX-024 | Invalid cert (outbound) | Bad provider cert | Connection refused | ☐ |

### 9.4 Error Handling

| Test ID | Test Case | Scenario | Expected Response | Status |
|---------|-----------|----------|-------------------|--------|
| PRX-030 | Provider timeout | 30s timeout | 504 Gateway Timeout | ☐ |
| PRX-031 | Provider 5xx | Provider returns 500 | 502 Bad Gateway | ☐ |
| PRX-032 | Provider unreachable | DNS failure | 503 Service Unavailable | ☐ |
| PRX-033 | Request too large | > 10MB body | 413 Payload Too Large | ☐ |

---

## 10. API Endpoint Tests

### 10.1 Proxy Endpoint

| Test ID | Test Case | Request | Expected Response | Status |
|---------|-----------|---------|-------------------|--------|
| API-001 | Valid proxy request | POST /api/v1/proxy | 200 + response | ☐ |
| API-002 | Missing auth | No Authorization | 401 | ☐ |
| API-003 | Invalid provider | provider: "invalid" | 400 | ☐ |
| API-004 | Missing body | No body | 400 | ☐ |
| API-005 | Blocked content | Critical PII | 403 | ☐ |

### 10.2 Session Endpoints

| Test ID | Test Case | Request | Expected Response | Status |
|---------|-----------|---------|-------------------|--------|
| API-010 | Get session | GET /sessions/{id} | 200 + session | ☐ |
| API-011 | Get unknown session | GET /sessions/invalid | 404 | ☐ |
| API-012 | Get other user's session | GET /sessions/{other} | 403 | ☐ |
| API-013 | List sessions | GET /sessions | 200 + list | ☐ |
| API-014 | Delete session | DELETE /sessions/{id} | 200 | ☐ |
| API-015 | Export mappings | GET /sessions/{id}/mappings | 200 + mappings | ☐ |

### 10.3 Policy Endpoints

| Test ID | Test Case | Request | Expected Response | Status |
|---------|-----------|---------|-------------------|--------|
| API-020 | Get user policy | GET /policies/users/{id} | 200 + policy | ☐ |
| API-021 | Update policy (admin) | PUT /policies/users/{id} | 200 | ☐ |
| API-022 | Update policy (non-admin) | PUT /policies/users/{id} | 403 | ☐ |
| API-023 | Get dept policy | GET /policies/departments/{d} | 200 + policy | ☐ |

### 10.4 Audit Endpoints

| Test ID | Test Case | Request | Expected Response | Status |
|---------|-----------|---------|-------------------|--------|
| API-030 | Query logs (auditor) | GET /audit/logs | 200 + logs | ☐ |
| API-031 | Query logs (user) | GET /audit/logs | Own logs only | ☐ |
| API-032 | Export logs | POST /audit/export | 202 + export ID | ☐ |
| API-033 | Verify integrity | POST /audit/verify | 200 + result | ☐ |

### 10.5 Health Endpoints

| Test ID | Test Case | Request | Expected Response | Status |
|---------|-----------|---------|-------------------|--------|
| API-040 | Health check | GET /health | 200 + status | ☐ |
| API-041 | Ready check | GET /ready | 200 if ready | ☐ |
| API-042 | Live check | GET /live | 200 if alive | ☐ |
| API-043 | Metrics | GET /metrics | Prometheus format | ☐ |

---

## 11. Integration Tests

### 11.1 Full Pipeline

| Test ID | Test Case | Flow | Verification | Status |
|---------|-----------|------|--------------|--------|
| INT-001 | Request → Response | Client → Gateway → LLM → Client | Complete cycle | ☐ |
| INT-002 | Sanitization pipeline | Policy → Compliance → Sanitize → Forward | All steps executed | ☐ |
| INT-003 | Desanitization pipeline | Response → Desanitize → Return | Aliases replaced | ☐ |
| INT-004 | Audit pipeline | Request → Audit → Destination | Log created | ☐ |

### 11.2 Component Integration

| Test ID | Test Case | Components | Verification | Status |
|---------|-----------|------------|--------------|--------|
| INT-010 | Policy + Session | PolicyEngine + SessionManager | Policy uses session | ☐ |
| INT-011 | Sanitizer + Mapping | SanitizationEngine + MappingManager | Mappings stored | ☐ |
| INT-012 | Audit + Encryption | AuditLogger + EncryptionService | Logs encrypted | ☐ |
| INT-013 | Proxy + Rate Limit | ProxyHandler + RateLimiter | Limits enforced | ☐ |

### 11.3 Infrastructure Integration

| Test ID | Test Case | Infrastructure | Verification | Status |
|---------|-----------|----------------|--------------|--------|
| INT-020 | Redis connectivity | SessionManager → Redis | Session stored | ☐ |
| INT-021 | PostgreSQL connectivity | AuditLogger → PostgreSQL | Log inserted | ☐ |
| INT-022 | Key Vault connectivity | EncryptionService → KV | Key retrieved | ☐ |
| INT-023 | LLM provider connectivity | Proxy → OpenAI | Response received | ☐ |

---

## 12. End-to-End Scenario Tests

### 12.1 Developer Workflow

| Test ID | Test Case | Scenario | Steps | Status |
|---------|-----------|----------|-------|--------|
| E2E-001 | Basic query sanitization | Dev asks about query | 1. Send request with ServerDB01<br>2. Verify sanitized to SERVER_0<br>3. Verify response desanitized | ☐ |
| E2E-002 | Multi-request session | Dev has conversation | 1. First request creates session<br>2. Second request uses same session<br>3. Same aliases used | ☐ |
| E2E-003 | Code completion | Copilot-style request | 1. IDE sends completion request<br>2. Gateway sanitizes<br>3. Completion returned | ☐ |

### 12.2 Compliance Workflow

| Test ID | Test Case | Scenario | Steps | Status |
|---------|-----------|----------|-------|--------|
| E2E-010 | PII blocking | User sends SSN | 1. Request with SSN<br>2. Request blocked<br>3. Security alerted<br>4. Audit logged | ☐ |
| E2E-011 | Policy violation | Blocked provider | 1. Request to disallowed provider<br>2. Request blocked<br>3. Audit logged | ☐ |
| E2E-012 | Audit export | Compliance review | 1. Admin exports logs<br>2. CSV generated<br>3. All fields present | ☐ |

### 12.3 Admin Workflow

| Test ID | Test Case | Scenario | Steps | Status |
|---------|-----------|----------|-------|--------|
| E2E-020 | Update user policy | Grant more access | 1. Admin updates policy<br>2. User gets new limits<br>3. Audit logged | ☐ |
| E2E-021 | Add new rule | New pattern needed | 1. Admin creates rule<br>2. Rule hot-reloaded<br>3. Pattern active | ☐ |
| E2E-022 | Block user | Security incident | 1. Admin blocks user<br>2. User requests fail<br>3. Sessions invalidated | ☐ |

### 12.4 Failure Scenarios

| Test ID | Test Case | Scenario | Steps | Status |
|---------|-----------|----------|-------|--------|
| E2E-030 | Redis down | Session store unavailable | 1. Redis goes down<br>2. Requests continue (degraded)<br>3. Alert triggered | ☐ |
| E2E-031 | Provider timeout | LLM slow response | 1. Provider times out<br>2. Client gets 504<br>3. Retry possible | ☐ |
| E2E-032 | Key Vault unavailable | Cannot get keys | 1. KV unreachable<br>2. Cached keys used<br>3. Alert triggered | ☐ |

---

## 13. Security Tests

### 13.1 Authentication

| Test ID | Test Case | Attack | Expected Defense | Status |
|---------|-----------|--------|------------------|--------|
| SEC-001 | Missing token | No Authorization header | 401 Unauthorized | ☐ |
| SEC-002 | Invalid token | Malformed JWT | 401 Unauthorized | ☐ |
| SEC-003 | Expired token | Token past expiry | 401 Unauthorized | ☐ |
| SEC-004 | Wrong audience | Token for different app | 401 Unauthorized | ☐ |
| SEC-005 | Revoked token | Token in revocation list | 401 Unauthorized | ☐ |

### 13.2 Authorization

| Test ID | Test Case | Attack | Expected Defense | Status |
|---------|-----------|--------|------------------|--------|
| SEC-010 | Access other session | User A → Session B | 403 Forbidden | ☐ |
| SEC-011 | User → Admin endpoint | Non-admin calls admin API | 403 Forbidden | ☐ |
| SEC-012 | Privilege escalation | Modify own policy | 403 Forbidden | ☐ |
| SEC-013 | Audit log tampering | Modify audit entry | Signature invalid | ☐ |

### 13.3 Injection Attacks

| Test ID | Test Case | Attack | Expected Defense | Status |
|---------|-----------|--------|------------------|--------|
| SEC-020 | SQL injection | `'; DROP TABLE--` | Parameterized queries | ☐ |
| SEC-021 | Log injection | Control characters | Sanitized output | ☐ |
| SEC-022 | Header injection | `\r\n` in headers | Headers validated | ☐ |
| SEC-023 | Path traversal | `../../../etc/passwd` | Path validated | ☐ |

### 13.4 Bypass Attempts

| Test ID | Test Case | Attack | Expected Defense | Status |
|---------|-----------|--------|------------------|--------|
| SEC-030 | Base64 encoding | Encode sensitive data | Decode + detect | ☐ |
| SEC-031 | URL encoding | `%53erverDB01` | Decode + detect | ☐ |
| SEC-032 | Unicode tricks | Homoglyph characters | Normalize + detect | ☐ |
| SEC-033 | Double encoding | `%2553erverDB01` | Multi-pass decode | ☐ |
| SEC-034 | Case variation | `SERVERDB01` | Case-insensitive | ☐ |

### 13.5 DoS Protection

| Test ID | Test Case | Attack | Expected Defense | Status |
|---------|-----------|--------|------------------|--------|
| SEC-040 | ReDoS pattern | Evil regex input | Timeout at 50ms | ☐ |
| SEC-041 | Large payload | 100MB request | Size limit enforced | ☐ |
| SEC-042 | Connection flood | 10K connections | Rate limiting | ☐ |
| SEC-043 | Slowloris | Slow headers | Timeout | ☐ |

---

## 14. Performance Tests

### 14.1 Latency

| Test ID | Test Case | Scenario | Target | Status |
|---------|-----------|----------|--------|--------|
| PRF-001 | P50 latency | Normal load | < 20ms | ☐ |
| PRF-002 | P95 latency | Normal load | < 50ms | ☐ |
| PRF-003 | P99 latency | Normal load | < 100ms | ☐ |
| PRF-004 | P99 under stress | 80% capacity | < 200ms | ☐ |

### 14.2 Throughput

| Test ID | Test Case | Scenario | Target | Status |
|---------|-----------|----------|--------|--------|
| PRF-010 | Sustained throughput | 5 minutes | 1000 req/s | ☐ |
| PRF-011 | Burst throughput | 30 seconds | 2000 req/s | ☐ |
| PRF-012 | Multi-instance | 3 instances | 3000 req/s | ☐ |

### 14.3 Resource Usage

| Test ID | Test Case | Scenario | Target | Status |
|---------|-----------|----------|--------|--------|
| PRF-020 | CPU usage | Sustained load | < 70% | ☐ |
| PRF-021 | Memory usage | 1 hour | < 1GB | ☐ |
| PRF-022 | Memory growth | 24 hours | < 10% growth | ☐ |
| PRF-023 | GC pauses | During load | < 50ms | ☐ |

### 14.4 Scalability

| Test ID | Test Case | Scenario | Target | Status |
|---------|-----------|----------|--------|--------|
| PRF-030 | Horizontal scaling | 1 → 3 instances | Linear throughput | ☐ |
| PRF-031 | Session scaling | 10K sessions | No degradation | ☐ |
| PRF-032 | Rule scaling | 500 rules | < 2x latency | ☐ |

---

## Summary

### Coverage Statistics

| Category | Total Tests | Unit | Integration | E2E |
|----------|-------------|------|-------------|-----|
| Sanitization | 45 | 35 | 7 | 3 |
| Desanitization | 23 | 18 | 3 | 2 |
| Policy | 33 | 25 | 5 | 3 |
| Compliance | 34 | 28 | 4 | 2 |
| Session | 22 | 15 | 5 | 2 |
| Audit | 35 | 20 | 10 | 5 |
| Rate Limiting | 23 | 18 | 3 | 2 |
| Encryption | 14 | 12 | 2 | 0 |
| HTTP Proxy | 24 | 12 | 8 | 4 |
| API Endpoints | 43 | 0 | 43 | 0 |
| Integration | 23 | 0 | 23 | 0 |
| E2E Scenarios | 32 | 0 | 0 | 32 |
| Security | 43 | 20 | 15 | 8 |
| Performance | 23 | 0 | 8 | 15 |
| **TOTAL** | **417** | **203** | **136** | **78** |

### Test Execution Order

1. **Unit Tests** (203 tests) - Run on every commit
2. **Integration Tests** (136 tests) - Run on PR
3. **E2E Tests** (78 tests) - Run before release
4. **Performance Tests** - Run weekly + before release
5. **Security Tests** - Run on PR + before release

---

**Document Version:** 1.0  
**Last Updated:** January 13, 2026  
**Next Review:** Before each release

