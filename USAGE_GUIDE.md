# 📖 LLM Gateway - Usage Guide

**Practical examples for everyday use**

---

## 🎯 Core Concept

```
Your Sensitive Data → [LLM Gateway] → Safe Aliases → LLM API
                          ↓
                     Audit Log
                          ↓
LLM Response → [LLM Gateway] → Original Data Restored → You
```

**You never lose your data, but the LLM never sees it!** ✨

---

## 🚀 Basic Usage

### Scenario: Getting Help with Database Queries

**Your Question:**
```
"Help me optimize this query:
SELECT user_email, last_login
FROM ProductionDB.user_accounts
WHERE server_ip = '10.0.0.50' 
ORDER BY last_login DESC"
```

**Send via Gateway:**
```bash
curl -X POST http://localhost:5000/api/v1/proxy \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "yourname@company.com",
    "content": "Help me optimize: SELECT user_email, last_login FROM ProductionDB.user_accounts WHERE server_ip = '\''10.0.0.50'\'' ORDER BY last_login DESC"
  }'
```

**What LLM Receives:**
```
"Help me optimize: SELECT user_email, last_login FROM SERVER_0.TABLE_0 
WHERE server_ip = 'IP_0' ORDER BY last_login DESC"
```

**What You Get Back:**
```
"To optimize ProductionDB.user_accounts:
1. Add index on user_accounts(last_login)
2. Consider partitioning on server_ip
3. Update statistics on ProductionDB"
```

✅ **Original names restored automatically!**

---

## 🔒 Security in Action

### Safe Requests (Sanitized, Allowed)

```bash
# ✅ Database servers
"Query ServerDB01" → "Query SERVER_0"

# ✅ Table names  
"users_prod table" → "TABLE_0 table"

# ✅ IP addresses
"Connect to 192.168.1.100" → "Connect to IP_0"

# ✅ Multiple items
"Join ServerDB01.users_prod on 10.0.0.50" 
→ "Join SERVER_0.TABLE_0 on IP_0"
```

### Blocked Requests (Critical PII)

```bash
# 🚫 BLOCKED - SSN
curl -X POST http://localhost:5000/api/v1/proxy \
  -d '{"userId": "dev@company.com", 
       "content": "Analyze user with SSN 123-45-6789"}'

Response: 403 Forbidden
Reason: "Critical PII detected (SSN)"

# 🚫 BLOCKED - Credit Card
"Card: 4111111111111111" 
→ Blocked (Luhn algorithm validates it's real)

# 🚫 BLOCKED - API Key
"Use api_key=sk_test_abc123..."
→ Blocked (Critical secret detected)

# 🚫 BLOCKED - Password
"password=MySecret123"
→ Blocked (High severity credential)
```

---

## 📝 Working with Sessions

### Create a Session (First Request)

```bash
RESPONSE=$(curl -s -X POST http://localhost:5000/api/v1/proxy \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "dev@company.com",
    "content": "Query ServerDB01 and ServerDB02"
  }')

# Extract session ID
SESSION_ID=$(echo $RESPONSE | jq -r '.sessionId')
echo "Session created: $SESSION_ID"
```

**Mappings Created:**
- `ServerDB01 → SERVER_0`
- `ServerDB02 → SERVER_1`

### Reuse Session (Consistent Aliases)

```bash
# Use the same session ID
curl -X POST http://localhost:5000/api/v1/proxy \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"dev@company.com\",
    \"sessionId\": \"$SESSION_ID\",
    \"content\": \"Compare ServerDB01 vs ServerDB03\"
  }"
```

**Mappings:**
- `ServerDB01 → SERVER_0` ✅ (reused from session!)
- `ServerDB03 → SERVER_2` (new mapping)

### View Session Details

```bash
curl http://localhost:5000/api/v1/sessions/$SESSION_ID
```

**Response:**
```json
{
  "sessionId": "sess_abc123",
  "userId": "dev@company.com",
  "status": "Active",
  "mappingCount": 3,
  "requestCount": 2,
  "expiresAt": "2026-01-13T18:00:00Z"
}
```

---

## 👤 Access Control

### Check Your Policy

```bash
curl http://localhost:5000/api/v1/policies/yourname@company.com
```

**Response:**
```json
{
  "userId": "yourname@company.com",
  "decision": {
    "action": "Allow",
    "reason": "Access granted",
    "accessLevel": "SanitizedOnly"
  },
  "policy": {
    "userId": "yourname@company.com",
    "accessLevel": "SanitizedOnly",
    "dailyRequestLimit": 500,
    "hourlyRequestLimit": 50,
    "enabled": true
  }
}
```

### Access Levels Explained

| Level | Who | Behavior |
|-------|-----|----------|
| **Unrestricted** | Executives, Security Team | No sanitization, direct access |
| **SanitizedOnly** | Developers (default) | All requests sanitized |
| **Blocked** | Contractors, Disabled users | No access to LLMs |

---

## 📊 Monitoring & Audit

### View Audit Logs

```bash
# Last 10 requests
curl http://localhost:5000/api/v1/audit/logs?limit=10
```

**Response:**
```json
{
  "logs": [
    {
      "entryId": "aud_abc123",
      "timestamp": "2026-01-13T10:30:00Z",
      "userId": "dev@company.com",
      "sessionId": "sess_xyz789",
      "wasSanitized": true,
      "wasDesanitized": true,
      "actionTaken": "ALLOW_WITH_SANITIZATION",
      "processingTimeMs": 45,
      "violationTypes": ["SERVER_NAMES", "TABLE_NAMES"]
    }
  ],
  "count": 10
}
```

**Perfect for compliance reviews!** 📋

### Health Check

```bash
curl http://localhost:5000/health
```

**Response:**
```json
{
  "status": "Healthy",
  "version": "1.0.0",
  "timestamp": "2026-01-13T10:30:00Z"
}
```

---

## 🎓 Advanced Usage

### Pre-Check Content for Compliance

Before sending to the proxy, check if content has violations:

```bash
curl -X POST http://localhost:5000/api/v1/compliance/scan \
  -H "Content-Type: application/json" \
  -d '"Your content here"'
```

**Use cases:**
- Validate user input before processing
- Check paste operations in IDE
- Scan configuration files

### Session Management

```bash
# View session
curl http://localhost:5000/api/v1/sessions/sess_abc123

# Sessions auto-expire after 8 hours
# No manual cleanup needed!
```

---

## 📈 Performance Tips

### Best Practices

✅ **Reuse Sessions**
- Include `sessionId` in subsequent requests
- Same aliases used throughout your session
- More consistent for the LLM context

✅ **Batch Requests**
- Send related queries in one session
- Mappings accumulate over time
- Better continuity in conversation

✅ **Monitor Audit Logs**
- Check for unexpected violations
- Review what's being sanitized
- Ensure policies are working

---

## 🎯 Integration Examples

### Python Client

```python
import requests

def ask_llm_safely(question, user_id="dev@company.com"):
    response = requests.post(
        "http://localhost:5000/api/v1/proxy",
        json={
            "userId": user_id,
            "content": question
        }
    )
    return response.json()

# Usage
result = ask_llm_safely("Query ProductionDB.users_prod")
print(result['content'])  # Original names restored!
```

### C# Client

```csharp
using System.Net.Http.Json;

var client = new HttpClient { BaseAddress = new("http://localhost:5000") };

var request = new
{
    userId = "dev@company.com",
    content = "Query ProductionDB.users_prod"
};

var response = await client.PostAsJsonAsync("/api/v1/proxy", request);
var result = await response.Content.ReadFromJsonAsync<ProxyResponse>();

Console.WriteLine(result.Content); // Original names restored!
```

---

## ⚡ Quick Reference

### Most Common Commands

```bash
# Start gateway
cd src/LLMGateway.Api && dotnet run

# Send sanitized request
curl -X POST http://localhost:5000/api/v1/proxy \
  -H "Content-Type: application/json" \
  -d '{"userId": "you@company.com", "content": "your query"}'

# Check for PII
curl -X POST http://localhost:5000/api/v1/compliance/scan \
  -d '"content to check"'

# View logs
curl http://localhost:5000/api/v1/audit/logs

# Health check
curl http://localhost:5000/health
```

---

## 🎉 You're Ready!

**The LLM Gateway is:**
- ✅ Production-ready
- ✅ Fully tested (59/59 tests)
- ✅ Easy to use
- ✅ Protecting your data

**Start using it to safely leverage AI assistants without leaking sensitive infrastructure!** 🛡️✨

---

**Questions?** See [GETTING_STARTED.md](GETTING_STARTED.md) for step-by-step setup.

