#!/bin/bash

# LLM Gateway API Test Script
# Make this file executable: chmod +x test-api.sh

API_URL="http://localhost:5000"

echo "🚀 LLM Gateway API Tests"
echo "========================"
echo ""

# Test 1: Health Check
echo "1️⃣  Testing /health endpoint..."
curl -s "$API_URL/health" | jq '.'
echo ""
echo ""

# Test 2: Ready Check
echo "2️⃣  Testing /ready endpoint..."
curl -s "$API_URL/ready" | jq '.'
echo ""
echo ""

# Test 3: Proxy Request with Sensitive Data
echo "3️⃣  Testing /api/v1/proxy with sensitive data..."
curl -s -X POST "$API_URL/api/v1/proxy" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "developer@company.com",
    "content": "Query ServerDB01.users_prod for data from 192.168.1.100",
    "department": "Engineering"
  }' | jq '.'
echo ""
echo ""

# Test 4: Proxy Request without Sensitive Data
echo "4️⃣  Testing /api/v1/proxy without sensitive data..."
curl -s -X POST "$API_URL/api/v1/proxy" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "developer@company.com",
    "content": "How do I optimize SQL queries?",
    "department": "Engineering"
  }' | jq '.'
echo ""
echo ""

# Test 5: Reusing Session
echo "5️⃣  Testing session reuse..."
RESPONSE=$(curl -s -X POST "$API_URL/api/v1/proxy" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "developer@company.com",
    "content": "Query ServerDB01 again",
    "department": "Engineering"
  }')

SESSION_ID=$(echo $RESPONSE | jq -r '.sessionId')
echo "First request created session: $SESSION_ID"
echo $RESPONSE | jq '.'
echo ""

echo "Second request with same session ID..."
curl -s -X POST "$API_URL/api/v1/proxy" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"developer@company.com\",
    \"content\": \"Also check ServerDB02\",
    \"sessionId\": \"$SESSION_ID\",
    \"department\": \"Engineering\"
  }" | jq '.'
echo ""
echo ""

echo "✅ All tests complete!"

