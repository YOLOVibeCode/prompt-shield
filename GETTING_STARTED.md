# 🚀 Getting Started with LLM Gateway

**5-minute guide to protecting your sensitive data**

---

## What is LLM Gateway?

A security proxy that sits between you and LLM APIs (ChatGPT, Claude, etc.) to:

1. 🔒 **Automatically mask** sensitive data (servers, databases, IPs)
2. 🤖 **Forward** safe requests to LLMs
3. 🔓 **Restore** original values in the response
4. 📊 **Log** everything for compliance

**Result:** You get AI help without leaking infrastructure details! ✨

---

## ⚡ Quick Start

### Prerequisites

- .NET 10 SDK installed
- Terminal/Command Prompt
- curl or Postman (for testing)

### Step 1: Get the Code

```bash
cd /Users/admin/Dev/YOLOProjects/prompt-shield
dotnet build
```

Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Step 2: Verify with Tests

```bash
dotnet test
```

Expected output:
```
Passed!  - Failed:     0, Passed:    59, Skipped:     0
```

✅ If you see **59/59 passing**, you're good to go!

### Step 3: Start the Gateway

```bash
cd src/LLMGateway.Api
dotnet run
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

🎉 **Gateway is running!**

---

## 🎯 Your First Request

Open a new terminal and try this:

```bash
curl -X POST http://localhost:5000/api/v1/proxy \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "developer@company.com",
    "content": "How do I query ServerDB01.users_prod table?"
  }'
```

**You should see:**

```json
{
  "content": "Based on your query about SERVER_0.TABLE_0...",
  "sessionId": "sess_abc123...",
  "wasSanitized": true,
  "wasDesanitized": true,
  "mappingsCreated": {
    "ServerDB01": "SERVER_0",
    "users_prod": "TABLE_0"
  }
}
```

✅ **Success!** Notice:
- `ServerDB01` was replaced with `SERVER_0`
- `users_prod` was replaced with `TABLE_0`
- You got a session ID for future requests
- The response shows what was sanitized

---

## 🧪 Test the Security Features

### Test 1: PII Detection (Should Block)

```bash
curl -X POST http://localhost:5000/api/v1/compliance/scan \
  -H "Content-Type: application/json" \
  -d '"My SSN is 123-45-6789"'
```

**Response:**
```json
{
  "hasViolations": true,
  "shouldBlock": true,
  "violations": [{
    "type": "SSN",
    "severity": "Critical",
    "redactedValue": "123***789"
  }]
}
```

🚫 **This request would be BLOCKED** - protecting you from accidental PII leakage!

### Test 2: Credit Card Detection

```bash
curl -X POST http://localhost:5000/api/v1/compliance/scan \
  -H "Content-Type: application/json" \
  -d '"Card: 4111111111111111"'
```

**Response:**
```json
{
  "hasViolations": true,
  "shouldBlock": true,
  "violations": [{
    "type": "CREDIT_CARD",
    "severity": "Critical"
  }]
}
```

🚫 **BLOCKED** - The Luhn algorithm validated this is a real credit card format!

### Test 3: Safe Content (Should Allow)

```bash
curl -X POST http://localhost:5000/api/v1/proxy \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "developer@company.com",
    "content": "What are best practices for SQL optimization?"
  }'
```

**Response:**
```json
{
  "content": "Based on your query about best practices...",
  "sessionId": "sess_xyz...",
  "wasSanitized": false,
  "mappingsCreated": {}
}
```

✅ **ALLOWED** - No sensitive data detected, request processed normally.

---

## 🔑 Understanding Sessions

Sessions keep your mappings consistent across requests.

### First Request

```bash
curl -X POST http://localhost:5000/api/v1/proxy \
  -d '{
    "userId": "dev@company.com",
    "content": "Query ServerDB01"
  }'
```

**Returns:** `sessionId: "sess_abc123"`, `ServerDB01 → SERVER_0`

### Second Request (Same Session)

```bash
curl -X POST http://localhost:5000/api/v1/proxy \
  -d '{
    "userId": "dev@company.com",
    "sessionId": "sess_abc123",
    "content": "Also query ServerDB01 and ServerDB02"
  }'
```

**Returns:**
- `ServerDB01 → SERVER_0` (same alias as before! ✅)
- `ServerDB02 → SERVER_1` (new alias)

### Check Your Session

```bash
curl http://localhost:5000/api/v1/sessions/sess_abc123
```

**Response:**
```json
{
  "sessionId": "sess_abc123",
  "userId": "dev@company.com",
  "status": "Active",
  "mappingCount": 2,
  "requestCount": 2,
  "expiresAt": "2026-01-13T18:00:00Z"
}
```

---

## 📋 Common Use Cases

### Use Case 1: IDE Integration (Future)

Configure your IDE to proxy through LLM Gateway:

```json
// VSCode settings.json (future integration)
{
  "http.proxy": "http://localhost:5000",
  "github.copilot.proxy": "http://localhost:5000"
}
```

**Benefit:** All Copilot/AI requests automatically protected!

### Use Case 2: Code Review Help

```bash
curl -X POST http://localhost:5000/api/v1/proxy \
  -d '{
    "userId": "developer@company.com",
    "content": "Review this SQL: SELECT * FROM ProductionDB.sensitive_data WHERE ip='\''172.16.0.100'\''"
  }'
```

✅ LLM sees: `"SELECT * FROM SERVER_0.TABLE_0 WHERE ip='IP_0'"`  
✅ You get: Advice with real names restored

### Use Case 3: Audit Review

```bash
# View recent activity
curl http://localhost:5000/api/v1/audit/logs?limit=10
```

**Response shows:**
- Who made requests
- What was sanitized
- What violations were detected
- When it happened

Perfect for compliance audits! 📊

---

## 🛡️ Protection Levels

### What Gets Sanitized (Masked but Allowed)

✅ Database servers: `ServerDB01` → `SERVER_0`  
✅ Database tables: `users_prod` → `TABLE_0`  
✅ Private IPs: `192.168.1.100` → `IP_0`  
✅ Internal hostnames: `prod-server-01` → `SERVER_0`

### What Gets Blocked (Critical PII)

🚫 Social Security Numbers: `123-45-6789`  
🚫 Credit Cards: `4111111111111111` (validated with Luhn)  
🚫 API Keys: `sk_test_abc123def456`  
🚫 Passwords: `password=MySecret123`

### What Gets Logged

📊 Every request is audited with:
- User ID
- Timestamp
- Sanitization status
- Violations detected
- Processing time

---

## ⚙️ Configuration

### Default Policies

| User | Access Level | Daily Limit |
|------|--------------|-------------|
| `developer@company.com` | SanitizedOnly | 500 |
| `security@company.com` | Unrestricted | 1000 |
| **Anyone else** | SanitizedOnly | 500 (default) |

### Access Levels

| Level | Behavior |
|-------|----------|
| **Unrestricted** | Direct LLM access, no sanitization (executives) |
| **SanitizedOnly** | All requests sanitized (developers) |
| **Blocked** | No LLM access (contractors, disabled users) |

### Session Settings

- **Timeout:** 8 hours (auto-expires after inactivity)
- **Storage:** In-memory (lost on restart, but fast)
- **Isolation:** Each user has their own session
- **Cleanup:** Expired sessions auto-removed

---

## 🐛 Troubleshooting

### Issue: "Connection refused"

**Problem:** Gateway not running

**Solution:**
```bash
cd src/LLMGateway.Api
dotnet run
# Wait for "Now listening on: http://localhost:5000"
```

### Issue: "Tests failing"

**Problem:** Code changed without tests

**Solution:**
```bash
dotnet test --logger "console;verbosity=detailed"
# Check which test failed and fix the code
```

### Issue: "Session not found"

**Problem:** Session expired (8 hours TTL)

**Solution:**
```bash
# Just send a new request without sessionId
# A new session will be created automatically
```

---

## 📞 Support

### Questions?

1. Check `/health` endpoint: `curl http://localhost:5000/health`
2. View audit logs: `curl http://localhost:5000/api/v1/audit/logs`
3. Read specs: `specs/TEST_COVERAGE_CHECKLISTS.md`

### Need Help?

- 📧 Internal: Contact development team
- 📚 Docs: See `specs/` folder for detailed specs
- 🐛 Issues: Check test output for debugging

---

## ✅ Ready to Use!

You now have a **fully functional LLM Gateway** that:

✅ Protects your infrastructure names  
✅ Blocks accidental PII leakage  
✅ Maintains session continuity  
✅ Provides complete audit trail  
✅ Works with any LLM API  

**Start protecting your data today!** 🛡️✨

---

**Next:** See [README.md](README.md) for API reference and advanced usage.

