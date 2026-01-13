# LLM Gateway: Enterprise-Grade LLM Access Control & Data Sanitization

**Version:** 1.0  
**Last Updated:** January 2026  
**Author:** Enterprise Security Architecture  
**Classification:** Internal Use

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Architecture Overview](#architecture-overview)
3. [Core Components](#core-components)
4. [Security Features](#security-features)
5. [Installation & Setup](#installation--setup)
6. [Configuration](#configuration)
7. [API Reference](#api-reference)
8. [Compliance & Audit](#compliance--audit)
9. [Deployment Guide](#deployment-guide)
10. [Troubleshooting](#troubleshooting)

---

## Executive Summary

**LLM Gateway** is an enterprise-grade proxy service that provides comprehensive security controls for Large Language Model (LLM) access within corporate environments. It ensures that sensitive data—including server names, database schemas, table names, PII, API keys, and confidential information—is never transmitted to external LLM services like ChatGPT, Claude, or Copilot.

### Key Benefits

- **Zero-Knowledge Guarantee**: Sensitive data is masked before leaving your network
- **Policy-Based Control**: Department-specific rules and access levels
- **Complete Audit Trail**: Immutable compliance logging for regulatory requirements
- **Transparent Integration**: Works seamlessly with existing tools (VSCode, IDEs, APIs)
- **No LLM Dependency**: Detection uses rule-based patterns, not AI (eliminates circular dependency)
- **Session-Scoped Mappings**: Different users see different aliases for the same data
- **Real-Time Monitoring**: Dashboard and SIEM integration for immediate alerts

### Compliance Support

- **SOC 2 Type II**: Audit logging and access controls
- **HIPAA**: PII detection and encryption
- **GDPR**: Data minimization and user audit trails
- **PCI-DSS**: Credit card and payment data protection
- **ISO 27001**: Information security management

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     USER/DEVELOPER                          │
│         (VSCode, IDE, Application, API Client)              │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                   LLM GATEWAY PROXY                         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ 1. Request Interceptor                              │  │
│  │    - Buffers incoming request                       │  │
│  │    - Extracts session ID / user context             │  │
│  └──────────────────────────────────────────────────────┘  │
│                         │                                   │
│  ┌──────────────────────▼──────────────────────────────┐  │
│  │ 2. Policy Engine                                    │  │
│  │    - Check user access level (RBAC)                │  │
│  │    - Verify department policies                    │  │
│  │    - Rate limit & quota checks                     │  │
│  └──────────────────────┬──────────────────────────────┘  │
│                         │                                   │
│  ┌──────────────────────▼──────────────────────────────┐  │
│  │ 3. Compliance Detector                              │  │
│  │    - PII detection (SSN, CC, secrets)              │  │
│  │    - Banned pattern matching                       │  │
│  │    - Flag high-risk content                        │  │
│  └──────────────────────┬──────────────────────────────┘  │
│                         │                                   │
│  ┌──────────────────────▼──────────────────────────────┐  │
│  │ 4. Sanitization Engine                              │  │
│  │    - Apply regex patterns                          │  │
│  │    - Generate aliases (TABLE_0, SERVER_1, etc)    │  │
│  │    - Maintain session mappings                     │  │
│  └──────────────────────┬──────────────────────────────┘  │
│                         │                                   │
│  ┌──────────────────────▼──────────────────────────────┐  │
│  │ 5. Audit Logger                                     │  │
│  │    - Log request metadata                          │  │
│  │    - Record detected violations                    │  │
│  │    - Send to SIEM system                          │  │
│  └──────────────────────┬──────────────────────────────┘  │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
         ┌───────────────────────────────────┐
         │   Forward to LLM API              │
         │   (OpenAI, Anthropic, Azure)      │
         │   [SANITIZED REQUEST]             │
         └───────────────────┬───────────────┘
                             │
                             ▼
         ┌───────────────────────────────────┐
         │   LLM Response                    │
         │   [Contains alias references]     │
         └───────────────────┬───────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                   LLM GATEWAY PROXY                         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ 6. Response Desanitizer                             │  │
│  │    - Reverse mapping (TABLE_0 → users_prod)        │  │
│  │    - Restore original names                        │  │
│  │    - Validate response integrity                   │  │
│  └──────────────────────┬──────────────────────────────┘  │
│                         │                                   │
│  ┌──────────────────────▼──────────────────────────────┐  │
│  │ 7. Encryption Service                               │  │
│  │    - Encrypt sensitive session data                │  │
│  │    - Rotate keys periodically                      │  │
│  │    - Secure storage of mappings                    │  │
│  └──────────────────────┬──────────────────────────────┘  │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
        ┌───────────────────────────────────┐
        │   Return to User/Developer        │
        │   [DESANITIZED, SAFE RESPONSE]    │
        └───────────────────────────────────┘
```

---

## Core Components

### 1. Sanitization Engine

**Purpose**: Masks sensitive data using rule-based pattern matching.

**Features**:
- Regex-based pattern matching (no AI dependency)
- Session-scoped alias generation
- Reversible mappings maintained in memory
- Supports custom rule definitions
- Real-time pattern validation

**Detection Categories**:
- Server/Database names
- Schema and table names
- IP addresses
- Email domains (internal)
- File paths and URIs
- Secrets and API keys (basic detection)

### 2. Policy Engine

**Purpose**: Enforces access controls and department-specific rules.

**Policy Types**:
- **Department Policies**: Marketing vs. Engineering vs. Finance rules differ
- **User Policies**: Individual user access levels and limits
- **Provider Policies**: Which LLMs are allowed (ChatGPT OK, but not Claude, etc.)
- **Content Policies**: Forbidden patterns (e.g., no production data queries)

**Policy Actions**:
- `ALLOW`: Request proceeds with sanitization
- `WARN`: Request allowed but logged as warning
- `BLOCK`: Request rejected; returned to user

### 3. Compliance Detector

**Purpose**: Identifies PII and regulated data before it's sent to LLM.

**Detects**:
- Social Security Numbers (XXX-XX-XXXX)
- Credit card numbers (Luhn algorithm validation)
- API keys and tokens
- Database passwords
- Private keys (RSA, certificates)
- Healthcare data patterns (HIPAA)
- Financial account numbers

**Severity Levels**:
- `CRITICAL`: Immediate block (e.g., production credentials)
- `HIGH`: Log and warn (e.g., internal email domains)
- `MEDIUM`: Sanitize and allow (e.g., local server names)
- `LOW`: Log only (e.g., public information)

### 4. Mapping Manager

**Purpose**: Maintains reversible mappings of original → masked data.

**Session Management**:
- Per-user session isolation
- Automatic session creation
- Configurable TTL (time-to-live)
- Optional session persistence to encrypted files
- Clean-up of expired sessions

**Mapping Storage**:
```
Session: user-123-session-abc
├── Mappings (original → alias)
│   ├── "ServerDB01" → "SERVER_0"
│   ├── "users_prod" → "TABLE_0"
│   └── "10.0.0.50" → "IP_0"
├── Reverse Mappings (for response desanitization)
└── Metadata (created_at, user_id, expires_at)
```

### 5. Audit Logger

**Purpose**: Maintains immutable compliance trail.

**Logs**:
- User ID, timestamp, source IP
- Department and role
- LLM provider accessed
- Sanitization status
- Violations detected
- Request/response hashes
- Policy violations

**Destinations**:
- Local file (immutable append-only log)
- SIEM systems (Splunk, ELK, Azure Sentinel)
- Syslog server
- Database (SQL Server, PostgreSQL)

### 6. Rate Limiter & Quota Manager

**Purpose**: Prevents abuse and enforces consumption limits.

**Controls**:
- Daily request limits per user
- Hourly burst limits
- Token-based rate limiting
- Department-wide quotas
- Priority queuing

**Behavior on Limit Exceeded**:
- Return 429 Too Many Requests
- Log violation for compliance
- Optional: escalate to manager

### 7. Encryption Service

**Purpose**: Protects session mappings and sensitive logs.

**Encryption**:
- AES-256-GCM for data at rest
- TLS 1.3 for data in transit
- User-scoped encryption keys
- Automatic key rotation

**Protected Data**:
- Session mapping files
- Sensitive audit logs
- User policy definitions

---

## Security Features

### 1. Role-Based Access Control (RBAC)

```
User Categories:
├── UNRESTRICTED
│   └── Users: Executives, Security team
│   └── Access: Direct LLM access, no sanitization required
│
├── SANITIZED_ONLY
│   └── Users: Developers, Engineers
│   └── Access: Full LLM access but all requests sanitized
│
└── BLOCKED
    └── Users: Contractors, external users
    └── Access: No LLM access allowed
```

### 2. Department-Based Policies

**Engineering Department**:
- Allowed: Database optimization queries
- Blocked: Production credential queries
- Required: Table/server sanitization
- Limit: 500 requests/day

**Finance Department**:
- Allowed: Financial modeling, forecasting
- Blocked: Account numbers, credit cards
- Required: All PII sanitization
- Limit: 100 requests/day

**Marketing Department**:
- Allowed: Content generation, copywriting
- Blocked: Customer data, internal systems
- Required: Email domain masking
- Limit: 1000 requests/day

### 3. Data Minimization

**Principle**: Send only necessary context to LLM.

**Example**:
```
Original Query:
"Help me optimize the query that joins users_prod.accounts, 
orders_prod.transactions, and customers_archive.profiles 
from ServerDB01 where region='US'"

Sanitized Query:
"Help me optimize the query that joins TABLE_0, TABLE_1, and TABLE_2 
from SERVER_0 where region='US'"

Result: 3 tables → 3 aliases. LLM provides optimization advice 
without knowing actual database structure.
```

### 4. Threat Detection & Response

**Automatic Detection**:
- Unusual access patterns (user making 10x normal requests)
- Suspicious content (multiple sanitizations needed)
- Time-based anomalies (access outside normal hours)
- Geo-location mismatches

**Automated Response**:
- Alert security team
- Temporary rate limit increase
- Force re-authentication
- Session termination
- Escalate to manager

### 5. Encryption Standards

**Data at Rest**:
- Algorithm: AES-256-GCM
- Key management: Azure Key Vault / HashiCorp Vault
- Key rotation: 90 days

**Data in Transit**:
- Protocol: TLS 1.3
- Certificate pinning: Enabled
- Forward secrecy: Required

**Session Mappings**:
- Encrypted before storage
- User-specific encryption keys
- Automatic decryption on access

### 6. Audit & Immutability

**Audit Log Properties**:
- Append-only (no modification or deletion)
- Cryptographically signed entries
- Tamper detection via checksums
- Retention: 7 years (configurable)

**Audit Entry Contents**:
```json
{
  "timestamp": "2024-01-13T10:30:00Z",
  "userId": "john.smith@company.com",
  "sessionId": "sess-abc123",
  "department": "Engineering",
  "llmProvider": "openai",
  "requestHash": "sha256-...",
  "wasSanitized": true,
  "violationsDetected": ["SERVER_NAME", "TABLE_NAME"],
  "ipAddress": "192.168.1.100",
  "userAgent": "VSCode/1.85",
  "policyApplied": "ENGINEERING_DEFAULT",
  "actionTaken": "ALLOW_WITH_SANITIZATION",
  "signature": "ed25519-..."
}
```

---

## Installation & Setup

### Prerequisites

- .NET 8.0 or later
- Windows/Linux/macOS
- 2GB RAM minimum
- 500MB disk space
- Network access to LLM providers (outbound HTTPS)

### Step 1: Clone Repository

```bash
git clone https://github.com/company/llm-gateway.git
cd llm-gateway
```

### Step 2: Install Dependencies

```bash
dotnet restore
```

### Step 3: Configure Settings

```bash
cp appsettings.example.json appsettings.json
# Edit appsettings.json with your settings
```

### Step 4: Initialize Database

```bash
dotnet ef database update
```

### Step 5: Load Sanitization Rules

```bash
dotnet run -- --init-rules sanitization-rules.json
```

### Step 6: Start Gateway

```bash
dotnet run
# Gateway running on http://localhost:8888
```

### Step 7: Configure Client Tools

**For VSCode**:
```json
{
  "http.proxy": "http://localhost:8888",
  "http.proxySupport": "override",
  "http.proxyStrictSSL": false
}
```

**For .NET Applications**:
```csharp
var httpClientHandler = new HttpClientHandler
{
    Proxy = new WebProxy("http://localhost:8888"),
    UseProxy = true
};
var client = new HttpClient(httpClientHandler);
```

**For Python**:
```python
import os
os.environ['HTTP_PROXY'] = 'http://localhost:8888'
os.environ['HTTPS_PROXY'] = 'http://localhost:8888'
```

---

## Configuration

### appsettings.json Structure

```json
{
  "Gateway": {
    "Port": 8888,
    "BindAddress": "localhost",
    "EnableTls": false,
    "CertificatePath": null
  },
  
  "Sanitization": {
    "Enabled": true,
    "RulesFile": "sanitization-rules.json",
    "CaseSensitive": false,
    "SessionTimeoutMinutes": 480
  },

  "Compliance": {
    "PiiDetectionEnabled": true,
    "BlockOnHighSeverityViolation": true,
    "AllowedViolationSeverities": ["LOW", "MEDIUM"]
  },

  "Policies": {
    "DefaultAccessLevel": "SANITIZED_ONLY",
    "RequireMfa": false,
    "IpWhitelist": [],
    "IpBlacklist": []
  },

  "RateLimiting": {
    "Enabled": true,
    "DefaultDailyLimit": 500,
    "DefaultHourlyLimit": 50,
    "BurstLimit": 10
  },

  "Audit": {
    "Enabled": true,
    "LogPath": "./audit-logs",
    "RetentionDays": 2555,
    "DestinationType": "file|splunk|elastic|syslog",
    "DestinationUri": "http://splunk:8088"
  },

  "Encryption": {
    "Enabled": true,
    "Algorithm": "AES256GCM",
    "KeyRotationDays": 90,
    "KeyVaultUri": "https://company-keyvault.vault.azure.net"
  },

  "Integration": {
    "ActiveDirectoryDomain": "company.com",
    "SyncIntervalMinutes": 60,
    "DlpSystemUri": null,
    "SiemSystemUri": "http://splunk:8088"
  }
}
```

### sanitization-rules.json

```json
[
  {
    "name": "SERVER_NAMES",
    "description": "Database server instances",
    "pattern": "(?i)(ServerDB|ProductionDB|DevDB|\\w+db\\d+|server_\\w+)",
    "prefix": "SERVER",
    "severity": "MEDIUM",
    "enabled": true,
    "exceptions": ["server_error", "server_logs"]
  },
  
  {
    "name": "TABLE_NAMES",
    "description": "Database table references",
    "pattern": "(?i)(FROM|INTO|UPDATE|DELETE FROM)\\s+(\\w+)\\.(\\w+)",
    "prefix": "TABLE",
    "severity": "MEDIUM",
    "enabled": true,
    "exceptions": []
  },

  {
    "name": "SCHEMA_NAMES",
    "description": "Database schemas",
    "pattern": "(?i)(dbo|public|staging|internal|warehouse)\\.",
    "prefix": "SCHEMA",
    "severity": "MEDIUM",
    "enabled": true,
    "exceptions": []
  },

  {
    "name": "IP_ADDRESSES",
    "description": "Internal IP addresses",
    "pattern": "\\b(192\\.168|10\\.|172\\.(1[6-9]|2[0-9]|3[0-1]))\\.\\d{1,3}\\.\\d{1,3}\\b",
    "prefix": "IP",
    "severity": "HIGH",
    "enabled": true,
    "exceptions": []
  },

  {
    "name": "EMAIL_DOMAINS",
    "description": "Internal email domains",
    "pattern": "@(company|internal|corp|dev)\\.\\w+",
    "prefix": "EMAIL",
    "severity": "MEDIUM",
    "enabled": true,
    "exceptions": []
  },

  {
    "name": "API_KEYS",
    "description": "API keys and tokens",
    "pattern": "(?i)(api[_-]?key|apikey|access[_-]?token|secret[_-]?key)\\s*[:=]\\s*['\\\"]?([a-zA-Z0-9-_]{20,})['\\\"]?",
    "prefix": "TOKEN",
    "severity": "CRITICAL",
    "enabled": true,
    "exceptions": []
  },

  {
    "name": "DATABASE_PASSWORDS",
    "description": "Database connection string passwords",
    "pattern": "(?i)(password|pwd)\\s*=\\s*[^;\\s]+",
    "prefix": "CRED",
    "severity": "CRITICAL",
    "enabled": true,
    "exceptions": []
  },

  {
    "name": "FILE_PATHS",
    "description": "Internal file paths and UNC paths",
    "pattern": "(?i)(c:|d:|e:)\\\\[\\w\\\\]+|\\\\\\\\[a-z0-9-]+\\\\[\\w\\\\]+",
    "prefix": "PATH",
    "severity": "MEDIUM",
    "enabled": true,
    "exceptions": ["c:\\windows", "c:\\program files"]
  }
]
```

### User Policies Configuration

```json
{
  "users": [
    {
      "userId": "john.smith@company.com",
      "displayName": "John Smith",
      "department": "Engineering",
      "accessLevel": "SANITIZED_ONLY",
      "allowedProviders": ["openai", "anthropic"],
      "dailyRequestLimit": 500,
      "hourlyRequestLimit": 50,
      "policyName": "ENGINEERING_DEFAULT",
      "requiresMfa": false,
      "enabled": true,
      "metadata": {
        "team": "Backend",
        "manager": "jane.doe@company.com"
      }
    },
    {
      "userId": "security@company.com",
      "displayName": "Security Team",
      "department": "Security",
      "accessLevel": "UNRESTRICTED",
      "allowedProviders": ["openai", "anthropic", "google"],
      "dailyRequestLimit": 1000,
      "hourlyRequestLimit": 100,
      "policyName": "SECURITY_UNRESTRICTED",
      "requiresMfa": true,
      "enabled": true
    }
  ],

  "departmentDefaults": {
    "Engineering": {
      "accessLevel": "SANITIZED_ONLY",
      "allowedProviders": ["openai"],
      "dailyRequestLimit": 500,
      "requiredSanitization": ["SERVER", "TABLE", "SCHEMA", "IP"]
    },
    "Finance": {
      "accessLevel": "SANITIZED_ONLY",
      "allowedProviders": ["openai"],
      "dailyRequestLimit": 100,
      "requiredSanitization": ["IP", "EMAIL", "API_KEY", "PASSWORD"]
    },
    "Marketing": {
      "accessLevel": "SANITIZED_ONLY",
      "allowedProviders": ["openai"],
      "dailyRequestLimit": 1000,
      "requiredSanitization": ["EMAIL", "IP"]
    }
  }
}
```

---

## API Reference

### Authentication

All requests must include authentication headers:

```http
Authorization: Bearer {token}
X-Session-Id: {sessionId}
X-Target-Url: {originalLlmApiUrl}
```

### Endpoints

#### 1. Proxy LLM Request

**Endpoint**: `POST /api/v1/proxy`

**Request**:
```json
{
  "sessionId": "user-123-session-abc",
  "userId": "john.smith@company.com",
  "llmProvider": "openai",
  "originalUrl": "https://api.openai.com/v1/chat/completions",
  "payload": {
    "model": "gpt-4",
    "messages": [
      {
        "role": "user",
        "content": "Help me optimize this query..."
      }
    ]
  }
}
```

**Response** (Success - 200):
```json
{
  "success": true,
  "sessionId": "user-123-session-abc",
  "wasSanitized": true,
  "sanitizationDetails": {
    "patternsApplied": ["SERVER", "TABLE"],
    "mappingsCreated": {
      "ServerDB01": "SERVER_0",
      "users_prod": "TABLE_0"
    }
  },
  "llmResponse": {
    "choices": [
      {
        "message": {
          "content": "You can optimize the query by..."
        }
      }
    ]
  },
  "desanitizationStatus": "COMPLETE"
}
```

**Response** (Blocked - 403):
```json
{
  "success": false,
  "error": "REQUEST_BLOCKED",
  "reason": "Critical PII detected (API key found)",
  "violations": [
    {
      "type": "API_KEY",
      "severity": "CRITICAL",
      "pattern": "API_KEY",
      "detectedContent": "***REDACTED***"
    }
  ],
  "action": "Escalated to security team for review"
}
```

#### 2. Get Session Status

**Endpoint**: `GET /api/v1/sessions/{sessionId}`

**Response**:
```json
{
  "sessionId": "user-123-session-abc",
  "userId": "john.smith@company.com",
  "createdAt": "2024-01-13T10:00:00Z",
  "expiresAt": "2024-01-13T18:00:00Z",
  "mappingCount": 15,
  "requestsProcessed": 25,
  "status": "ACTIVE"
}
```

#### 3. Export Session Mappings

**Endpoint**: `GET /api/v1/sessions/{sessionId}/mappings`

**Response**:
```json
{
  "sessionId": "user-123-session-abc",
  "mappings": {
    "ServerDB01": "SERVER_0",
    "users_prod": "TABLE_0",
    "192.168.1.100": "IP_0"
  },
  "exportedAt": "2024-01-13T10:30:00Z",
  "encrypted": true
}
```

#### 4. Clear Session

**Endpoint**: `DELETE /api/v1/sessions/{sessionId}`

**Response**:
```json
{
  "success": true,
  "message": "Session cleared successfully",
  "mappingsCleared": 15
}
```

#### 5. Get Dashboard Statistics

**Endpoint**: `GET /api/v1/dashboard/stats`

**Query Parameters**:
- `startDate`: ISO 8601 date
- `endDate`: ISO 8601 date

**Response**:
```json
{
  "period": {
    "startDate": "2024-01-01T00:00:00Z",
    "endDate": "2024-01-13T23:59:59Z"
  },
  "statistics": {
    "totalRequests": 15000,
    "sanitizedRequests": 14800,
    "blockedRequests": 200,
    "avgSanitizationTime": 45,
    "topViolationTypes": {
      "SERVER_NAMES": 1200,
      "TABLE_NAMES": 950,
      "IP_ADDRESSES": 800
    }
  },
  "userActivity": {
    "totalActiveUsers": 125,
    "topUsers": [
      {
        "userId": "john.smith@company.com",
        "requestCount": 500,
        "blockedCount": 10
      }
    ]
  }
}
```

#### 6. Get Audit Logs

**Endpoint**: `GET /api/v1/audit/logs`

**Query Parameters**:
- `userId`: Filter by user
- `department`: Filter by department
- `severity`: CRITICAL, HIGH, MEDIUM, LOW
- `limit`: Results per page (max 1000)
- `offset`: Pagination offset

**Response**:
```json
{
  "total": 15000,
  "limit": 50,
  "offset": 0,
  "logs": [
    {
      "timestamp": "2024-01-13T10:30:00Z",
      "userId": "john.smith@company.com",
      "department": "Engineering",
      "llmProvider": "openai",
      "requestHash": "sha256-abc123...",
      "wasSanitized": true,
      "violationsDetected": ["SERVER_NAME"],
      "ipAddress": "192.168.1.100",
      "action": "ALLOW_WITH_SANITIZATION"
    }
  ]
}
```

#### 7. Update Policy

**Endpoint**: `POST /api/v1/policies/{policyId}`

**Request**:
```json
{
  "dailyRequestLimit": 750,
  "allowedProviders": ["openai", "anthropic"],
  "requiredSanitization": ["SERVER", "TABLE", "SCHEMA"]
}
```

**Response**:
```json
{
  "success": true,
  "policyId": "ENGINEERING_DEFAULT",
  "updatedAt": "2024-01-13T10:30:00Z"
}
```

---

## Compliance & Audit

### Audit Log Structure

Every request through the gateway is logged with the following information:

```
Timestamp        │ 2024-01-13T10:30:00Z
User ID          │ john.smith@company.com
Session ID       │ sess-abc123def456
Department       │ Engineering
LLM Provider     │ openai
Request Hash     │ sha256-4a8f5c9e...
Sanitized        │ Yes
Violations       │ SERVER_NAME (HIGH)
IP Address       │ 192.168.1.100
User Agent       │ VSCode/1.85.0
Policy Applied   │ ENGINEERING_DEFAULT
Action Taken     │ ALLOW_WITH_SANITIZATION
Response Time    │ 45ms
Token Count      │ 250
Signature        │ ed25519-xyz789...
```

### Compliance Reports

#### Monthly Compliance Report

Generate using:
```bash
dotnet run -- --generate-report monthly
```

**Report Contents**:
- Total requests processed
- Sanitization rate
- Violation statistics
- High-risk users (top violators)
- Policy effectiveness
- Rate limiting events
- Encryption key rotations
- Audit log integrity checks

#### Export for Auditors

```bash
dotnet run -- --export-audit --format csv --output audit-2024-01.csv
```

**Exported Fields**:
- Timestamp
- User ID
- Department
- LLM Provider
- Sanitization Status
- Violations Detected
- Action Taken
- Digital Signature

### Retention & Archival

**Audit Log Retention**:
- Active: 90 days (hot storage)
- Archive: 7 years (cold storage)
- Deletion: After 7 years

**Archive Format**:
- Compressed: GZIP
- Encrypted: AES-256
- Location: Cloud storage (Azure Blob, S3)
- Verification: SHA256 checksums

---

## Deployment Guide

### Docker Deployment

**Dockerfile**:
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app
COPY bin/Release/net8.0/publish .

EXPOSE 8888

ENV ASPNETCORE_URLS=http://+:8888
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "LLMSanitizer.Proxy.dll"]
```

**Build & Run**:
```bash
docker build -t llm-gateway:latest .
docker run -d \
  -p 8888:8888 \
  -v /config:/app/config \
  -v /logs:/app/logs \
  -e ASPNETCORE_ENVIRONMENT=Production \
  llm-gateway:latest
```

### Kubernetes Deployment

**deployment.yaml**:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: llm-gateway
  namespace: security
spec:
  replicas: 3
  selector:
    matchLabels:
      app: llm-gateway
  template:
    metadata:
      labels:
        app: llm-gateway
    spec:
      containers:
      - name: gateway
        image: registry.company.com/llm-gateway:latest
        ports:
        - containerPort: 8888
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: KeyVaultUri
          value: "https://company-keyvault.vault.azure.net"
        volumeMounts:
        - name: config
          mountPath: /app/config
        - name: logs
          mountPath: /app/logs
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8888
          initialDelaySeconds: 30
          periodSeconds: 10
      volumes:
      - name: config
        configMap:
          name: llm-gateway-config
      - name: logs
        persistentVolumeClaim:
          claimName: llm-gateway-logs
---
apiVersion: v1
kind: Service
metadata:
  name: llm-gateway
  namespace: security
spec:
  type: ClusterIP
  ports:
  - port: 8888
    targetPort: 8888
  selector:
    app: llm-gateway
```

**Deploy**:
```bash
kubectl apply -f deployment.yaml
kubectl get pods -n security -l app=llm-gateway
```

### Windows Service Installation

```powershell
# Create service
New-Service -Name "LLMGateway" `
  -BinaryPathName "C:\Program Files\LLMGateway\LLMSanitizer.Proxy.exe" `
  -DisplayName "LLM Gateway Service" `
  -StartupType Automatic

# Start service
Start-Service -Name "LLMGateway"

# View status
Get-Service -Name "LLMGateway"
```

### Linux Systemd Service

**/etc/systemd/system/llm-gateway.service**:
```ini
[Unit]
Description=LLM Gateway Service
After=network.target

[Service]
Type=notify
User=www-data
WorkingDirectory=/opt/llm-gateway
ExecStart=/usr/bin/dotnet /opt/llm-gateway/LLMSanitizer.Proxy.dll
Restart=on-failure
RestartSec=10

[Install]
WantedBy=multi-user.target
```

**Enable & Start**:
```bash
sudo systemctl daemon-reload
sudo systemctl enable llm-gateway
sudo systemctl start llm-gateway
sudo systemctl status llm-gateway
```

---

## Troubleshooting

### Common Issues

#### 1. "Proxy Connection Refused"

**Symptom**: VSCode/IDE cannot connect to gateway

**Solutions**:
```bash
# Check if gateway is running
netstat -an | grep 8888

# Check gateway status
curl http://localhost:8888/health

# Restart gateway
dotnet run

# Check logs
tail -f logs/application-*.log
```

#### 2. "Request Blocked - High Severity Violation"

**Symptom**: Legitimate queries are being blocked

**Diagnosis**:
```bash
# Check what was detected
grep "VIOLATION" logs/audit-*.log

# Review blocking rule
cat sanitization-rules.json | grep -A5 "severity.*CRITICAL"
```

**Solution**:
- Add exception to rule pattern
- Adjust severity level
- Update policy for user/department

#### 3. "Session Not Found"

**Symptom**: Desanitization fails because session expired

**Solutions**:
```bash
# Check session TTL
cat appsettings.json | grep SessionTimeoutMinutes

# Increase TTL
# Change "SessionTimeoutMinutes": 480 to 1440 (24 hours)

# Clear expired sessions manually
dotnet run -- --clear-expired-sessions
```

#### 4. "Performance Degradation"

**Symptom**: Gateway processing takes >100ms per request

**Diagnosis**:
```bash
# Check regex performance
dotnet run -- --benchmark-patterns

# Monitor resource usage
docker stats llm-gateway

# Review logs for slow operations
grep "ProcessingTime" logs/application-*.log | sort -r
```

**Solutions**:
- Optimize regex patterns (escape special chars properly)
- Increase thread pool size
- Add caching for mappings
- Distribute across multiple instances

#### 5. "Desanitization Mismatch"

**Symptom**: Response contains aliases instead of original names

**Cause**: Session expired or mappings lost

**Solution**:
```bash
# Export session before expiry
curl http://localhost:8888/api/v1/sessions/{sessionId}/mappings

# Save mappings to file
# Use for manual desanitization if needed
```

#### 6. "Certificate/TLS Errors"

**Symptom**: "SSL certificate verification failed"

**Solution**:
```json
{
  "Gateway": {
    "EnableTls": true,
    "CertificatePath": "/etc/ssl/certs/llm-gateway.pfx",
    "CertificatePassword": "${CERT_PASSWORD}"
  }
}
```

```bash
# Install certificate
cp llm-gateway.pfx /etc/ssl/certs/
chmod 600 /etc/ssl/certs/llm-gateway.pfx

# Restart gateway
systemctl restart llm-gateway
```

### Debug Mode

Enable verbose logging:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "LLMSanitizer": "Debug",
      "Microsoft": "Warning"
    }
  }
}
```

```bash
dotnet run --configuration Debug
```

### Health Check Endpoint

```bash
# Gateway health
curl http://localhost:8888/health

# Response
{
  "status": "healthy",
  "uptime": "12h 34m",
  "requestsProcessed": 15000,
  "sessionsActive": 45,
  "lastError": null
}
```

---

## Appendix

### A. Glossary

| Term | Definition |
|------|-----------|
| **Sanitization** | Replacing sensitive data with aliases before sending to LLM |
| **Desanitization** | Reversing aliases back to original data in LLM response |
| **Session** | User context for one sanitization lifecycle |
| **Mapping** | Original data → Alias correspondence |
| **Policy** | Department/user-specific access rules |
| **Violation** | Detected sensitive data matching a pattern |
| **Audit Trail** | Immutable log of all gateway activities |
| **PII** | Personally Identifiable Information (SSN, credit cards, etc.) |

### B. Regular Expression Patterns

Common patterns already configured:

- **Server Names**: `(?i)(ServerDB|ProductionDB|DevDB|\w+db\d+|server_\w+)`
- **Table Names**: `(?i)(FROM|INTO|UPDATE|DELETE FROM)\s+(\w+)\.(\w+)`
- **IP Addresses**: `\b(192\.168|10\.|172\.(1[6-9]|2[0-9]|3[0-1]))\.\d{1,3}\.\d{1,3}\b`
- **Email Domains**: `@(company|internal|corp|dev)\.\w+`
- **API Keys**: `(?i)(api[_-]?key|secret[_-]?key)\s*[:=]\s*['\"]?([a-zA-Z0-9-_]{20,})['\"]?`

### C. Integration Examples

**GitHub Copilot in VSCode**:
```json
{
  "http.proxy": "http://localhost:8888",
  "http.proxySupport": "override",
  "http.proxyStrictSSL": false
}
```

**Azure OpenAI SDK (.NET)**:
```csharp
var handler = new HttpClientHandler
{
    Proxy = new WebProxy("http://localhost:8888"),
    UseProxy = true
};
var client = new AzureOpenAIClient(
    new Uri("https://api.openai.azure.com/"),
    new AzureKeyCredential(key),
    new AzureOpenAIClientOptions { Transport = new HttpClientTransport(client) }
);
```

**OpenAI Python SDK**:
```python
import os
os.environ['HTTP_PROXY'] = 'http://localhost:8888'
os.environ['HTTPS_PROXY'] = 'http://localhost:8888'

import openai
response = openai.ChatCompletion.create(
    model="gpt-4",
    messages=[{"role": "user", "content": "..."}]
)
```

### D. Security Best Practices

1. **Network Isolation**
   - Run gateway on isolated network segment
   - Firewall rules: only allow internal access to port 8888
   - Disable external access

2. **Key Management**
   - Store encryption keys in Azure Key Vault / HashiCorp Vault
   - Rotate keys every 90 days
   - Use separate keys per environment (Dev/Staging/Prod)

3. **Access Control**
   - Require MFA for sensitive operations
   - Audit all admin actions
   - Principle of least privilege

4. **Monitoring**
   - Set up SIEM alerts for violations
   - Monitor gateway CPU/memory
   - Track request latencies
   - Alert on unusual access patterns

5. **Testing**
   - Regularly test sanitization rules
   - Penetration test the gateway
   - Validate audit log integrity
   - Test disaster recovery procedures

### E. Performance Metrics

Target metrics:

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| P99 Latency | <100ms | >200ms |
| Request Throughput | 1000 req/s | <500 req/s |
| CPU Usage | <40% | >70% |
| Memory Usage | <500MB | >800MB |
| Error Rate | <0.1% | >1% |
| Uptime | 99.9% | <99.5% |

### F. Support & Escalation

**For Issues**:
1. Check logs: `logs/application-*.log`
2. Run diagnostics: `dotnet run -- --diagnose`
3. Contact: security-team@company.com

**For Feature Requests**:
- Open issue in GitHub
- Include use case and priority

**For Security Issues**:
- Email: security-team@company.com (do not open public issue)
- Include: description, affected version, reproduction steps

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2024-01-13 | Initial release |
| | | - Core sanitization engine |
| | | - Policy enforcement |
| | | - Audit logging |
| | | - RBAC and department policies |
| | | - PII detection |
| | | - Rate limiting |

---

## License

LLM Gateway is proprietary software. Unauthorized copying or distribution is prohibited.

---

**Document Version**: 1.0  
**Last Updated**: January 13, 2026  
**Next Review**: April 13, 2026  
**Owner**: Enterprise Security Team

---

### Quick Reference

**Start Gateway**:
```bash
dotnet run
```

**Test Sanitization**:
```bash
curl -X POST http://localhost:8888/api/v1/proxy \
  -H "Content-Type: application/json" \
  -d '{"userId": "test", "payload": "Help me query ServerDB01.users_prod"}'
```

**View Health**:
```bash
curl http://localhost:8888/health
```

**Export Audit Logs**:
```bash
dotnet run -- --export-audit --format csv
```

**Generate Compliance Report**:
```bash
dotnet run -- --generate-report monthly
```
