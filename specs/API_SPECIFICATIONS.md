# API Specifications

**Parent Document:** [TECHNICAL_SPECIFICATION.md](../TECHNICAL_SPECIFICATION.md)

---

## 1. Overview

### 1.1 Base URL

```
Production:  https://llm-gateway.company.com/api/v1
Staging:     https://llm-gateway-staging.company.com/api/v1
Development: http://localhost:8888/api/v1
```

### 1.2 Authentication

All API requests require authentication via one of:

1. **Bearer Token** (OAuth 2.0 / JWT)
   ```http
   Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
   ```

2. **API Key** (for service-to-service)
   ```http
   X-API-Key: sk_live_abc123...
   ```

3. **Windows Authentication** (Intranet)
   ```http
   Authorization: Negotiate YIIBhg...
   ```

### 1.3 Common Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Authorization` | Yes | Authentication token |
| `X-Session-Id` | No | Existing session ID (optional for new sessions) |
| `X-Request-Id` | No | Client correlation ID for tracing |
| `Content-Type` | Yes | `application/json` for JSON bodies |

### 1.4 Error Response Format

```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable error message",
    "details": {
      "field": "Additional context"
    },
    "requestId": "req_abc123",
    "timestamp": "2026-01-13T10:30:00Z"
  }
}
```

### 1.5 Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `UNAUTHORIZED` | 401 | Missing or invalid authentication |
| `FORBIDDEN` | 403 | User lacks permission |
| `NOT_FOUND` | 404 | Resource not found |
| `RATE_LIMITED` | 429 | Too many requests |
| `VALIDATION_ERROR` | 400 | Invalid request parameters |
| `POLICY_VIOLATION` | 403 | Request blocked by policy |
| `CRITICAL_PII_DETECTED` | 403 | Critical sensitive data found |
| `SESSION_EXPIRED` | 410 | Session no longer valid |
| `PROVIDER_ERROR` | 502 | LLM provider returned error |
| `INTERNAL_ERROR` | 500 | Unexpected server error |

---

## 2. Proxy Endpoints

### 2.1 Proxy LLM Request

**Primary endpoint for proxying requests to LLM providers.**

```http
POST /api/v1/proxy
```

#### Request

```json
{
  "sessionId": "sess_abc123",          // Optional: existing session
  "targetProvider": "openai",          // Required: openai|anthropic|azure|google
  "targetUrl": "https://api.openai.com/v1/chat/completions",
  "method": "POST",
  "headers": {
    "Authorization": "Bearer sk-...",
    "Content-Type": "application/json"
  },
  "body": {
    "model": "gpt-4",
    "messages": [
      {
        "role": "user", 
        "content": "Help me optimize the query on ServerDB01.users_prod"
      }
    ]
  }
}
```

#### Response (200 OK)

```json
{
  "success": true,
  "sessionId": "sess_abc123",
  "requestId": "req_xyz789",
  "sanitization": {
    "wasSanitized": true,
    "patternsApplied": ["SERVER_NAMES", "TABLE_NAMES"],
    "mappingsCreated": 2,
    "violationsDetected": [
      {
        "type": "SERVER_NAMES",
        "severity": "MEDIUM",
        "count": 1
      },
      {
        "type": "TABLE_NAMES", 
        "severity": "MEDIUM",
        "count": 1
      }
    ]
  },
  "llmResponse": {
    "statusCode": 200,
    "headers": {
      "x-request-id": "abc123"
    },
    "body": {
      "id": "chatcmpl-123",
      "choices": [
        {
          "message": {
            "role": "assistant",
            "content": "To optimize the query on ServerDB01.users_prod, I recommend..."
          }
        }
      ]
    }
  },
  "desanitization": {
    "wasDesanitized": true,
    "replacementsCount": 2
  },
  "processingTimeMs": 45
}
```

#### Response (403 Forbidden - Policy Violation)

```json
{
  "success": false,
  "error": {
    "code": "CRITICAL_PII_DETECTED",
    "message": "Request blocked: Critical sensitive data detected",
    "details": {
      "violations": [
        {
          "type": "API_KEY",
          "severity": "CRITICAL",
          "pattern": "API_KEY",
          "redactedContent": "sk-proj-abc***xyz"
        }
      ],
      "action": "Request logged and blocked. Security team notified."
    }
  }
}
```

#### Response (429 Rate Limited)

```json
{
  "success": false,
  "error": {
    "code": "RATE_LIMITED",
    "message": "Daily request limit exceeded",
    "details": {
      "limitType": "DAILY",
      "limit": 500,
      "used": 500,
      "retryAfterSeconds": 3600
    }
  }
}
```

**Response Headers:**
```http
Retry-After: 3600
X-RateLimit-Limit: 500
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1705190400
```

---

### 2.2 Transparent Proxy Mode

For IDE/tool integrations that configure the gateway as an HTTP proxy.

```http
CONNECT api.openai.com:443 HTTP/1.1
Host: api.openai.com:443
Proxy-Authorization: Bearer eyJhbGci...
```

The gateway:
1. Establishes TLS tunnel
2. Intercepts requests/responses
3. Applies sanitization/desanitization
4. Forwards to actual LLM provider

**Supported Proxy Modes:**
- HTTP Proxy (port 8888)
- HTTPS Proxy with MITM (port 8443)
- SOCKS5 (port 1080) - optional

---

## 3. Session Endpoints

### 3.1 Get Session

```http
GET /api/v1/sessions/{sessionId}
```

#### Response (200 OK)

```json
{
  "sessionId": "sess_abc123",
  "userId": "john.smith@company.com",
  "department": "Engineering",
  "status": "ACTIVE",
  "createdAt": "2026-01-13T08:00:00Z",
  "expiresAt": "2026-01-13T16:00:00Z",
  "lastAccessedAt": "2026-01-13T10:30:00Z",
  "statistics": {
    "mappingCount": 15,
    "requestCount": 25,
    "totalSanitizations": 42,
    "totalDesanitizations": 38
  }
}
```

### 3.2 List User Sessions

```http
GET /api/v1/sessions?userId={userId}&status={status}
```

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `userId` | string | Filter by user (admin only) |
| `status` | string | ACTIVE, EXPIRED, ALL |
| `limit` | int | Results per page (default: 50, max: 100) |
| `offset` | int | Pagination offset |

#### Response (200 OK)

```json
{
  "sessions": [
    {
      "sessionId": "sess_abc123",
      "userId": "john.smith@company.com",
      "status": "ACTIVE",
      "createdAt": "2026-01-13T08:00:00Z",
      "mappingCount": 15
    }
  ],
  "pagination": {
    "total": 45,
    "limit": 50,
    "offset": 0,
    "hasMore": false
  }
}
```

### 3.3 Export Session Mappings

```http
GET /api/v1/sessions/{sessionId}/mappings
```

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `format` | string | json, csv (default: json) |
| `encrypted` | bool | Return encrypted (default: true) |

#### Response (200 OK - JSON)

```json
{
  "sessionId": "sess_abc123",
  "exportedAt": "2026-01-13T10:30:00Z",
  "mappingCount": 15,
  "mappings": [
    {
      "original": "ServerDB01",
      "alias": "SERVER_0",
      "category": "SERVER_NAMES",
      "createdAt": "2026-01-13T08:15:00Z"
    },
    {
      "original": "users_prod",
      "alias": "TABLE_0",
      "category": "TABLE_NAMES",
      "createdAt": "2026-01-13T08:15:00Z"
    }
  ]
}
```

### 3.4 Clear Session

```http
DELETE /api/v1/sessions/{sessionId}
```

#### Response (200 OK)

```json
{
  "success": true,
  "message": "Session cleared successfully",
  "sessionId": "sess_abc123",
  "mappingsCleared": 15,
  "clearedAt": "2026-01-13T10:30:00Z"
}
```

### 3.5 Extend Session

```http
POST /api/v1/sessions/{sessionId}/extend
```

#### Request

```json
{
  "extensionMinutes": 60
}
```

#### Response (200 OK)

```json
{
  "sessionId": "sess_abc123",
  "previousExpiry": "2026-01-13T16:00:00Z",
  "newExpiry": "2026-01-13T17:00:00Z"
}
```

---

## 4. Policy Endpoints

### 4.1 Get User Policy

```http
GET /api/v1/policies/users/{userId}
```

#### Response (200 OK)

```json
{
  "userId": "john.smith@company.com",
  "effectivePolicy": {
    "accessLevel": "SANITIZED_ONLY",
    "allowedProviders": ["openai", "anthropic"],
    "dailyRequestLimit": 500,
    "hourlyRequestLimit": 50,
    "burstLimit": 10,
    "requiredSanitization": ["SERVER_NAMES", "TABLE_NAMES", "IP_ADDRESSES"],
    "blockedPatterns": [],
    "requiresMfa": false
  },
  "source": {
    "userPolicy": "USER_SPECIFIC",
    "departmentPolicy": "ENGINEERING_DEFAULT"
  }
}
```

### 4.2 Update User Policy

```http
PUT /api/v1/policies/users/{userId}
```

**Required Role:** `PolicyAdmin`

#### Request

```json
{
  "accessLevel": "SANITIZED_ONLY",
  "allowedProviders": ["openai"],
  "dailyRequestLimit": 750,
  "hourlyRequestLimit": 75,
  "burstLimit": 15,
  "requiredSanitization": ["SERVER_NAMES", "TABLE_NAMES"],
  "enabled": true
}
```

#### Response (200 OK)

```json
{
  "success": true,
  "userId": "john.smith@company.com",
  "updatedAt": "2026-01-13T10:30:00Z",
  "updatedBy": "admin@company.com"
}
```

### 4.3 Get Department Policy

```http
GET /api/v1/policies/departments/{department}
```

### 4.4 List All Policies

```http
GET /api/v1/policies
```

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `type` | string | user, department, all |
| `department` | string | Filter by department |
| `limit` | int | Results per page |
| `offset` | int | Pagination offset |

---

## 5. Sanitization Rules Endpoints

### 5.1 List Rules

```http
GET /api/v1/rules
```

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `enabled` | bool | Filter by enabled status |
| `severity` | string | CRITICAL, HIGH, MEDIUM, LOW |
| `category` | string | Filter by category |

#### Response (200 OK)

```json
{
  "rules": [
    {
      "ruleId": "rule_server_names",
      "name": "SERVER_NAMES",
      "description": "Database server instances",
      "pattern": "(?i)(ServerDB|ProductionDB|\\w+db\\d+)",
      "prefix": "SERVER",
      "severity": "MEDIUM",
      "enabled": true,
      "exceptions": ["server_error"],
      "order": 10,
      "matchCount": 1250,
      "lastMatch": "2026-01-13T10:15:00Z"
    }
  ],
  "total": 25
}
```

### 5.2 Get Rule

```http
GET /api/v1/rules/{ruleId}
```

### 5.3 Create Rule

```http
POST /api/v1/rules
```

**Required Role:** `RuleAdmin`

#### Request

```json
{
  "name": "CUSTOM_PATTERN",
  "description": "Custom pattern for internal project names",
  "pattern": "(?i)(Project[A-Z]{3}\\d{4})",
  "prefix": "PROJECT",
  "severity": "MEDIUM",
  "enabled": true,
  "exceptions": [],
  "applicableDepartments": ["Engineering", "Product"]
}
```

#### Response (201 Created)

```json
{
  "ruleId": "rule_custom_abc123",
  "name": "CUSTOM_PATTERN",
  "createdAt": "2026-01-13T10:30:00Z",
  "createdBy": "admin@company.com"
}
```

### 5.4 Update Rule

```http
PUT /api/v1/rules/{ruleId}
```

### 5.5 Delete Rule

```http
DELETE /api/v1/rules/{ruleId}
```

### 5.6 Test Rule

Dry-run a pattern against sample content.

```http
POST /api/v1/rules/test
```

#### Request

```json
{
  "pattern": "(?i)(ServerDB\\d+)",
  "testContent": "Connect to ServerDB01 and query ServerDB02",
  "prefix": "SERVER"
}
```

#### Response (200 OK)

```json
{
  "matches": [
    {
      "matched": "ServerDB01",
      "position": 11,
      "length": 10,
      "wouldBecome": "SERVER_0"
    },
    {
      "matched": "ServerDB02",
      "position": 32,
      "length": 10,
      "wouldBecome": "SERVER_1"
    }
  ],
  "sanitizedPreview": "Connect to SERVER_0 and query SERVER_1",
  "processingTimeMs": 2
}
```

---

## 6. Audit Endpoints

### 6.1 Query Audit Logs

```http
GET /api/v1/audit/logs
```

**Required Role:** `Auditor` or `Admin`

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `startDate` | ISO8601 | Start of date range |
| `endDate` | ISO8601 | End of date range |
| `userId` | string | Filter by user |
| `department` | string | Filter by department |
| `action` | string | ALLOW, BLOCK, RATE_LIMITED |
| `severity` | string | Min severity level |
| `provider` | string | LLM provider |
| `limit` | int | Results per page (max: 1000) |
| `offset` | int | Pagination offset |

#### Response (200 OK)

```json
{
  "logs": [
    {
      "entryId": "aud_abc123",
      "timestamp": "2026-01-13T10:30:00Z",
      "userId": "john.smith@company.com",
      "sessionId": "sess_xyz789",
      "department": "Engineering",
      "llmProvider": "openai",
      "requestHash": "sha256:4a8f5c9e...",
      "wasSanitized": true,
      "violationsDetected": [
        {
          "type": "SERVER_NAMES",
          "severity": "MEDIUM",
          "count": 1
        }
      ],
      "ipAddress": "192.168.1.100",
      "userAgent": "VSCode/1.85",
      "policyApplied": "ENGINEERING_DEFAULT",
      "actionTaken": "ALLOW_WITH_SANITIZATION",
      "processingTimeMs": 45
    }
  ],
  "pagination": {
    "total": 15000,
    "limit": 50,
    "offset": 0,
    "hasMore": true
  },
  "aggregations": {
    "byAction": {
      "ALLOW_WITH_SANITIZATION": 14800,
      "BLOCK": 200
    },
    "byDepartment": {
      "Engineering": 10000,
      "Marketing": 3000,
      "Finance": 2000
    }
  }
}
```

### 6.2 Get Audit Entry

```http
GET /api/v1/audit/logs/{entryId}
```

### 6.3 Export Audit Logs

```http
POST /api/v1/audit/export
```

#### Request

```json
{
  "startDate": "2026-01-01T00:00:00Z",
  "endDate": "2026-01-31T23:59:59Z",
  "format": "csv",
  "filters": {
    "department": "Engineering"
  },
  "includeFields": [
    "timestamp",
    "userId",
    "department",
    "actionTaken",
    "violations"
  ]
}
```

#### Response (202 Accepted)

```json
{
  "exportId": "exp_abc123",
  "status": "PROCESSING",
  "estimatedRecords": 15000,
  "estimatedCompletionTime": "2026-01-13T10:35:00Z",
  "downloadUrl": null
}
```

### 6.4 Get Export Status

```http
GET /api/v1/audit/export/{exportId}
```

#### Response (200 OK - Complete)

```json
{
  "exportId": "exp_abc123",
  "status": "COMPLETE",
  "recordCount": 15000,
  "fileSizeBytes": 2500000,
  "downloadUrl": "https://storage.company.com/exports/exp_abc123.csv.gz",
  "expiresAt": "2026-01-14T10:35:00Z"
}
```

### 6.5 Verify Audit Integrity

```http
POST /api/v1/audit/verify
```

#### Request

```json
{
  "startDate": "2026-01-01T00:00:00Z",
  "endDate": "2026-01-13T23:59:59Z"
}
```

#### Response (200 OK)

```json
{
  "verificationId": "ver_abc123",
  "status": "PASS",
  "entriesVerified": 450000,
  "chainIntegrity": "VALID",
  "signatureValidation": "PASS",
  "issues": [],
  "verifiedAt": "2026-01-13T10:30:00Z"
}
```

---

## 7. Dashboard & Statistics Endpoints

### 7.1 Get Dashboard Statistics

```http
GET /api/v1/dashboard/stats
```

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `period` | string | today, week, month, custom |
| `startDate` | ISO8601 | Start (for custom period) |
| `endDate` | ISO8601 | End (for custom period) |

#### Response (200 OK)

```json
{
  "period": {
    "start": "2026-01-01T00:00:00Z",
    "end": "2026-01-13T23:59:59Z"
  },
  "summary": {
    "totalRequests": 150000,
    "sanitizedRequests": 148500,
    "blockedRequests": 1500,
    "sanitizationRate": 99.0,
    "avgProcessingTimeMs": 42
  },
  "violations": {
    "total": 75000,
    "bySeverity": {
      "CRITICAL": 150,
      "HIGH": 5000,
      "MEDIUM": 50000,
      "LOW": 19850
    },
    "byType": {
      "SERVER_NAMES": 25000,
      "TABLE_NAMES": 20000,
      "IP_ADDRESSES": 15000,
      "API_KEYS": 150,
      "OTHER": 14850
    }
  },
  "users": {
    "activeUsers": 250,
    "topUsers": [
      {
        "userId": "john.smith@company.com",
        "requestCount": 5000,
        "blockedCount": 10
      }
    ]
  },
  "providers": {
    "byProvider": {
      "openai": 120000,
      "anthropic": 25000,
      "azure_openai": 5000
    }
  }
}
```

### 7.2 Get User Statistics

```http
GET /api/v1/dashboard/users/{userId}/stats
```

### 7.3 Get Real-time Metrics

```http
GET /api/v1/dashboard/realtime
```

#### Response (200 OK)

```json
{
  "timestamp": "2026-01-13T10:30:00Z",
  "last5Minutes": {
    "requestCount": 250,
    "avgLatencyMs": 38,
    "errorRate": 0.02,
    "activeUsers": 45
  },
  "systemHealth": {
    "cpu": 35.2,
    "memory": 62.5,
    "sessionStoreStatus": "HEALTHY",
    "auditStoreStatus": "HEALTHY"
  }
}
```

---

## 8. Health & Operations Endpoints

### 8.1 Health Check

```http
GET /health
```

No authentication required.

#### Response (200 OK)

```json
{
  "status": "healthy",
  "version": "1.0.0",
  "uptime": "12d 5h 30m",
  "checks": {
    "sessionStore": "healthy",
    "auditStore": "healthy",
    "keyVault": "healthy",
    "outboundConnectivity": "healthy"
  }
}
```

### 8.2 Readiness Check

```http
GET /ready
```

#### Response (200 OK)

```json
{
  "ready": true,
  "checks": {
    "configLoaded": true,
    "rulesLoaded": true,
    "policiesLoaded": true,
    "certificatesValid": true
  }
}
```

### 8.3 Liveness Check

```http
GET /live
```

#### Response (200 OK)

```json
{
  "alive": true,
  "timestamp": "2026-01-13T10:30:00Z"
}
```

### 8.4 Metrics (Prometheus)

```http
GET /metrics
```

Returns Prometheus-format metrics:

```
# HELP llm_gateway_requests_total Total number of requests processed
# TYPE llm_gateway_requests_total counter
llm_gateway_requests_total{action="allow",provider="openai"} 148500
llm_gateway_requests_total{action="block",provider="openai"} 1500

# HELP llm_gateway_request_duration_seconds Request processing duration
# TYPE llm_gateway_request_duration_seconds histogram
llm_gateway_request_duration_seconds_bucket{le="0.01"} 50000
llm_gateway_request_duration_seconds_bucket{le="0.05"} 140000
llm_gateway_request_duration_seconds_bucket{le="0.1"} 149000
llm_gateway_request_duration_seconds_bucket{le="+Inf"} 150000

# HELP llm_gateway_active_sessions Current number of active sessions
# TYPE llm_gateway_active_sessions gauge
llm_gateway_active_sessions 2500
```

---

## 9. Admin Endpoints

### 9.1 Reload Configuration

```http
POST /api/v1/admin/reload-config
```

**Required Role:** `Admin`

#### Response (200 OK)

```json
{
  "success": true,
  "reloadedAt": "2026-01-13T10:30:00Z",
  "changes": {
    "rulesReloaded": 25,
    "policiesReloaded": 50
  }
}
```

### 9.2 Clear All Sessions

```http
POST /api/v1/admin/clear-sessions
```

**Required Role:** `Admin`

**Confirmation Required:** `X-Confirm: clear-all-sessions`

#### Response (200 OK)

```json
{
  "success": true,
  "sessionsCleared": 2500,
  "clearedAt": "2026-01-13T10:30:00Z"
}
```

### 9.3 Rotate Encryption Keys

```http
POST /api/v1/admin/rotate-keys
```

**Required Role:** `Admin`

#### Response (202 Accepted)

```json
{
  "rotationId": "rot_abc123",
  "status": "IN_PROGRESS",
  "startedAt": "2026-01-13T10:30:00Z",
  "estimatedCompletionTime": "2026-01-13T10:35:00Z"
}
```

---

## 10. OpenAPI Specification

Full OpenAPI 3.0 specification available at:

```http
GET /api/v1/openapi.json
GET /api/v1/openapi.yaml
```

Interactive documentation (Swagger UI):

```http
GET /api/v1/docs
```

---

## 11. Rate Limiting

### 11.1 API Rate Limits

| Endpoint Category | Limit | Window |
|-------------------|-------|--------|
| Proxy requests | Policy-based | Per user |
| Session endpoints | 100/min | Per user |
| Policy endpoints | 50/min | Per user |
| Audit endpoints | 20/min | Per user |
| Admin endpoints | 10/min | Per user |

### 11.2 Rate Limit Headers

All responses include:

```http
X-RateLimit-Limit: 500
X-RateLimit-Remaining: 450
X-RateLimit-Reset: 1705190400
```

---

## 12. Webhooks

### 12.1 Configure Webhook

```http
POST /api/v1/webhooks
```

#### Request

```json
{
  "url": "https://your-server.com/webhook",
  "events": ["POLICY_VIOLATION", "RATE_LIMIT_EXCEEDED", "CRITICAL_PII_DETECTED"],
  "secret": "whsec_abc123..."
}
```

### 12.2 Webhook Payload

```json
{
  "eventId": "evt_abc123",
  "eventType": "CRITICAL_PII_DETECTED",
  "timestamp": "2026-01-13T10:30:00Z",
  "data": {
    "userId": "john.smith@company.com",
    "sessionId": "sess_xyz789",
    "violationType": "API_KEY",
    "severity": "CRITICAL"
  }
}
```

### 12.3 Webhook Signature

Webhooks are signed using HMAC-SHA256:

```http
X-Webhook-Signature: sha256=abc123...
```

Verify with:
```python
import hmac
import hashlib

expected = hmac.new(
    secret.encode(),
    request.body,
    hashlib.sha256
).hexdigest()

is_valid = hmac.compare_digest(f"sha256={expected}", signature_header)
```

