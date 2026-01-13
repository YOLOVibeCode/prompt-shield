# Deployment Specifications

**Parent Document:** [TECHNICAL_SPECIFICATION.md](../TECHNICAL_SPECIFICATION.md)

---

## 1. Deployment Architecture

### 1.1 Reference Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              LOAD BALANCER                                   │
│                    (Azure App Gateway / AWS ALB / nginx)                     │
│                         Health checks, SSL termination                       │
└─────────────────────────────────┬───────────────────────────────────────────┘
                                  │
         ┌────────────────────────┼────────────────────────────────┐
         │                        │                                │
         ▼                        ▼                                ▼
┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
│  LLM Gateway    │      │  LLM Gateway    │      │  LLM Gateway    │
│   Instance 1    │      │   Instance 2    │      │   Instance 3    │
│                 │      │                 │      │                 │
│  - CPU: 2 cores │      │  - CPU: 2 cores │      │  - CPU: 2 cores │
│  - RAM: 4 GB    │      │  - RAM: 4 GB    │      │  - RAM: 4 GB    │
└────────┬────────┘      └────────┬────────┘      └────────┬────────┘
         │                        │                        │
         └────────────────────────┼────────────────────────┘
                                  │
         ┌────────────────────────┼────────────────────────┐
         │                        │                        │
         ▼                        ▼                        ▼
┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
│   Redis Cluster │      │   PostgreSQL    │      │   Azure Key     │
│  (Session Store)│      │  (Audit Logs)   │      │     Vault       │
│                 │      │                 │      │                 │
│  3 nodes HA     │      │  Primary +      │      │  HSM-backed     │
│                 │      │  2 replicas     │      │                 │
└─────────────────┘      └─────────────────┘      └─────────────────┘
```

### 1.2 Environment Tiers

| Environment | Purpose | Scale | SLA |
|-------------|---------|-------|-----|
| Development | Dev testing | 1 instance | None |
| Staging | Pre-prod validation | 2 instances | 99% |
| Production | Live traffic | 3+ instances | 99.9% |
| DR | Disaster recovery | Mirror of prod | 99.9% |

---

## 2. Infrastructure Requirements

### 2.1 Compute Requirements

#### Per Instance (Production)

| Resource | Minimum | Recommended | Maximum |
|----------|---------|-------------|---------|
| CPU | 2 cores | 4 cores | 8 cores |
| Memory | 2 GB | 4 GB | 8 GB |
| Disk | 20 GB SSD | 50 GB SSD | 100 GB SSD |
| Network | 1 Gbps | 10 Gbps | 10 Gbps |

#### Scaling Guidelines

```yaml
# Horizontal Pod Autoscaler (Kubernetes)
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: llm-gateway-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: llm-gateway
  minReplicas: 3
  maxReplicas: 10
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
    - type: Pods
      pods:
        metric:
          name: llm_gateway_requests_per_second
        target:
          type: AverageValue
          averageValue: "500"
```

### 2.2 Storage Requirements

#### Session Store (Redis)

| Metric | Sizing |
|--------|--------|
| Memory per session | ~10 KB average |
| Peak sessions | 10,000 |
| Total memory | 100 MB + overhead |
| Recommended | 1 GB per node |
| Cluster size | 3 nodes (HA) |

#### Audit Log Store

| Metric | Sizing |
|--------|--------|
| Log entry size | ~2 KB average |
| Daily volume | ~300 MB (150K entries) |
| Retention | 7 years |
| Total storage | ~750 GB |
| Growth rate | ~100 GB/year |

### 2.3 Network Requirements

```
┌─────────────────────────────────────────────────────────────────┐
│                        NETWORK TOPOLOGY                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Inbound (Corporate Network → Gateway)                          │
│  ├── Port 8888 (HTTP Proxy)                                     │
│  ├── Port 8443 (HTTPS Proxy with MITM)                         │
│  └── Port 443 (API endpoints)                                   │
│                                                                  │
│  Outbound (Gateway → LLM Providers)                             │
│  ├── api.openai.com:443                                         │
│  ├── api.anthropic.com:443                                      │
│  ├── *.openai.azure.com:443                                     │
│  └── generativelanguage.googleapis.com:443                      │
│                                                                  │
│  Internal (Gateway → Dependencies)                               │
│  ├── Redis: 6379                                                │
│  ├── PostgreSQL: 5432                                           │
│  ├── Key Vault: 443                                             │
│  └── SIEM (Splunk): 8088                                        │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Kubernetes Deployment

### 3.1 Deployment Manifest

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: llm-gateway
  namespace: security
  labels:
    app: llm-gateway
    version: v1.0.0
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      app: llm-gateway
  template:
    metadata:
      labels:
        app: llm-gateway
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8888"
        prometheus.io/path: "/metrics"
    spec:
      serviceAccountName: llm-gateway
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        fsGroup: 1000
      containers:
        - name: gateway
          image: registry.company.com/llm-gateway:v1.0.0
          imagePullPolicy: Always
          ports:
            - name: http
              containerPort: 8888
              protocol: TCP
            - name: https
              containerPort: 8443
              protocol: TCP
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: ASPNETCORE_URLS
              value: "http://+:8888;https://+:8443"
            - name: Redis__ConnectionString
              valueFrom:
                secretKeyRef:
                  name: llm-gateway-secrets
                  key: redis-connection
            - name: KeyVault__Uri
              value: "https://company-keyvault.vault.azure.net"
          volumeMounts:
            - name: config
              mountPath: /app/config
              readOnly: true
            - name: certs
              mountPath: /app/certs
              readOnly: true
            - name: logs
              mountPath: /app/logs
          resources:
            requests:
              cpu: "500m"
              memory: "1Gi"
            limits:
              cpu: "2000m"
              memory: "4Gi"
          livenessProbe:
            httpGet:
              path: /live
              port: 8888
            initialDelaySeconds: 10
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /ready
              port: 8888
            initialDelaySeconds: 5
            periodSeconds: 5
            timeoutSeconds: 3
            failureThreshold: 3
          startupProbe:
            httpGet:
              path: /health
              port: 8888
            initialDelaySeconds: 5
            periodSeconds: 5
            failureThreshold: 30
      volumes:
        - name: config
          configMap:
            name: llm-gateway-config
        - name: certs
          secret:
            secretName: llm-gateway-certs
        - name: logs
          emptyDir: {}
      affinity:
        podAntiAffinity:
          preferredDuringSchedulingIgnoredDuringExecution:
            - weight: 100
              podAffinityTerm:
                labelSelector:
                  matchLabels:
                    app: llm-gateway
                topologyKey: kubernetes.io/hostname
```

### 3.2 Service & Ingress

```yaml
---
apiVersion: v1
kind: Service
metadata:
  name: llm-gateway
  namespace: security
spec:
  type: ClusterIP
  ports:
    - name: http
      port: 8888
      targetPort: 8888
    - name: https
      port: 8443
      targetPort: 8443
  selector:
    app: llm-gateway
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: llm-gateway-ingress
  namespace: security
  annotations:
    nginx.ingress.kubernetes.io/proxy-body-size: "10m"
    nginx.ingress.kubernetes.io/proxy-read-timeout: "300"
    nginx.ingress.kubernetes.io/proxy-send-timeout: "300"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  ingressClassName: nginx
  tls:
    - hosts:
        - llm-gateway.company.com
      secretName: llm-gateway-tls
  rules:
    - host: llm-gateway.company.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: llm-gateway
                port:
                  number: 8888
```

### 3.3 ConfigMap & Secrets

```yaml
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: llm-gateway-config
  namespace: security
data:
  appsettings.Production.json: |
    {
      "Gateway": {
        "Port": 8888,
        "EnableTls": true
      },
      "Sanitization": {
        "Enabled": true,
        "SessionTimeoutMinutes": 480
      },
      "RateLimiting": {
        "Enabled": true,
        "DefaultDailyLimit": 500
      },
      "Audit": {
        "Enabled": true,
        "DestinationType": "splunk"
      }
    }
  
  sanitization-rules.json: |
    [
      {
        "name": "SERVER_NAMES",
        "pattern": "(?i)(ServerDB|ProductionDB|\\w+db\\d+)",
        "prefix": "SERVER",
        "severity": "MEDIUM",
        "enabled": true
      }
    ]
---
apiVersion: v1
kind: Secret
metadata:
  name: llm-gateway-secrets
  namespace: security
type: Opaque
stringData:
  redis-connection: "redis-cluster.security.svc.cluster.local:6379,password=${REDIS_PASSWORD}"
```

---

## 4. Docker Deployment

### 4.1 Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["src/LLMGateway.Proxy/LLMGateway.Proxy.csproj", "LLMGateway.Proxy/"]
COPY ["src/LLMGateway.Core/LLMGateway.Core.csproj", "LLMGateway.Core/"]
RUN dotnet restore "LLMGateway.Proxy/LLMGateway.Proxy.csproj"

# Copy source and build
COPY src/ .
RUN dotnet build "LLMGateway.Proxy/LLMGateway.Proxy.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "LLMGateway.Proxy/LLMGateway.Proxy.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r gateway && useradd -r -g gateway gateway

# Copy published app
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R gateway:gateway /app

# Switch to non-root user
USER gateway

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8888/health || exit 1

# Expose ports
EXPOSE 8888 8443

# Entry point
ENTRYPOINT ["dotnet", "LLMGateway.Proxy.dll"]
```

### 4.2 Docker Compose (Development)

```yaml
version: '3.8'

services:
  gateway:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8888:8888"
      - "8443:8443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - Redis__ConnectionString=redis:6379
      - Database__ConnectionString=Server=postgres;Database=llmgateway;User Id=gateway;Password=devpassword;
    volumes:
      - ./config:/app/config:ro
      - ./logs:/app/logs
    depends_on:
      - redis
      - postgres
    networks:
      - gateway-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8888/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    networks:
      - gateway-network
    command: redis-server --appendonly yes

  postgres:
    image: postgres:15-alpine
    environment:
      - POSTGRES_USER=gateway
      - POSTGRES_PASSWORD=devpassword
      - POSTGRES_DB=llmgateway
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql
    networks:
      - gateway-network

volumes:
  redis-data:
  postgres-data:

networks:
  gateway-network:
    driver: bridge
```

---

## 5. Cloud Deployments

### 5.1 Azure Deployment

```bicep
// main.bicep
param location string = resourceGroup().location
param environment string = 'prod'

// Container Apps Environment
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: 'llm-gateway-env-${environment}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// LLM Gateway Container App
resource gatewayApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'llm-gateway-${environment}'
  location: location
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8888
        transport: 'http'
      }
      secrets: [
        {
          name: 'redis-password'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/redis-password'
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'gateway'
          image: 'crllmgateway.azurecr.io/llm-gateway:v1.0.0'
          resources: {
            cpu: json('2')
            memory: '4Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'KeyVault__Uri'
              value: keyVault.properties.vaultUri
            }
          ]
        }
      ]
      scale: {
        minReplicas: 3
        maxReplicas: 10
        rules: [
          {
            name: 'cpu-scaling'
            custom: {
              type: 'cpu'
              metadata: {
                type: 'Utilization'
                value: '70'
              }
            }
          }
        ]
      }
    }
  }
}

// Redis Cache
resource redis 'Microsoft.Cache/redis@2023-04-01' = {
  name: 'llm-gateway-redis-${environment}'
  location: location
  properties: {
    sku: {
      name: 'Premium'
      family: 'P'
      capacity: 1
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: 'llm-gateway-kv-${environment}'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'premium' // HSM-backed
    }
    tenantId: subscription().tenantId
    enableSoftDelete: true
    enablePurgeProtection: true
  }
}
```

### 5.2 AWS Deployment (Terraform)

```hcl
# main.tf

# ECS Cluster
resource "aws_ecs_cluster" "llm_gateway" {
  name = "llm-gateway-${var.environment}"
  
  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

# ECS Service
resource "aws_ecs_service" "gateway" {
  name            = "llm-gateway"
  cluster         = aws_ecs_cluster.llm_gateway.id
  task_definition = aws_ecs_task_definition.gateway.arn
  desired_count   = 3
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [aws_security_group.gateway.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.gateway.arn
    container_name   = "llm-gateway"
    container_port   = 8888
  }

  deployment_configuration {
    maximum_percent         = 200
    minimum_healthy_percent = 100
  }
}

# Task Definition
resource "aws_ecs_task_definition" "gateway" {
  family                   = "llm-gateway"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "2048"
  memory                   = "4096"
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn           = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([
    {
      name      = "llm-gateway"
      image     = "${aws_ecr_repository.gateway.repository_url}:v1.0.0"
      essential = true

      portMappings = [
        {
          containerPort = 8888
          protocol      = "tcp"
        }
      ]

      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = "Production"
        }
      ]

      secrets = [
        {
          name      = "Redis__ConnectionString"
          valueFrom = aws_secretsmanager_secret.redis.arn
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.gateway.name
          awslogs-region        = var.region
          awslogs-stream-prefix = "llm-gateway"
        }
      }

      healthCheck = {
        command     = ["CMD-SHELL", "curl -f http://localhost:8888/health || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }
    }
  ])
}

# ElastiCache Redis
resource "aws_elasticache_replication_group" "redis" {
  replication_group_id       = "llm-gateway-redis"
  description               = "Redis for LLM Gateway sessions"
  node_type                 = "cache.r6g.large"
  num_cache_clusters        = 3
  automatic_failover_enabled = true
  multi_az_enabled          = true
  at_rest_encryption_enabled = true
  transit_encryption_enabled = true
  
  subnet_group_name         = aws_elasticache_subnet_group.redis.name
  security_group_ids        = [aws_security_group.redis.id]
}
```

---

## 6. CI/CD Pipeline

### 6.1 GitHub Actions

```yaml
# .github/workflows/deploy.yml
name: Deploy LLM Gateway

on:
  push:
    branches: [main]
    tags: ['v*']
  pull_request:
    branches: [main]

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  build:
    runs-on: ubuntu-latest
    outputs:
      version: ${{ steps.version.outputs.version }}
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'
          
      - name: Restore
        run: dotnet restore
        
      - name: Build
        run: dotnet build --configuration Release --no-restore
        
      - name: Test
        run: dotnet test --configuration Release --no-build --collect:"XPlat Code Coverage"
        
      - name: Publish coverage
        uses: codecov/codecov-action@v3
        
      - name: Get version
        id: version
        run: echo "version=${GITHUB_REF_NAME:-commit-${GITHUB_SHA::8}}" >> $GITHUB_OUTPUT

  docker:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Login to Container Registry
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
          
      - name: Build and push
        uses: docker/build-push-action@v5
        with:
          context: .
          push: true
          tags: |
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ needs.build.outputs.version }}
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:latest

  deploy-staging:
    needs: [build, docker]
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - uses: azure/setup-kubectl@v3
      
      - name: Deploy to staging
        run: |
          kubectl set image deployment/llm-gateway \
            gateway=${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ needs.build.outputs.version }} \
            -n security
          kubectl rollout status deployment/llm-gateway -n security

  deploy-production:
    needs: [build, docker, deploy-staging]
    runs-on: ubuntu-latest
    environment: production
    if: startsWith(github.ref, 'refs/tags/v')
    steps:
      - uses: azure/setup-kubectl@v3
      
      - name: Deploy to production
        run: |
          kubectl set image deployment/llm-gateway \
            gateway=${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ needs.build.outputs.version }} \
            -n security
          kubectl rollout status deployment/llm-gateway -n security
```

---

## 7. Disaster Recovery

### 7.1 Backup Strategy

| Component | Backup Frequency | Retention | Method |
|-----------|------------------|-----------|--------|
| Configuration | On change | 90 days | Git |
| Audit Logs | Daily | 7 years | SQL backup + archive |
| Session Data | Real-time | N/A | Redis replication |
| Encryption Keys | On rotation | Forever | Key Vault backup |

### 7.2 Recovery Procedures

```bash
#!/bin/bash
# dr-recovery.sh

# 1. Verify DR environment is ready
kubectl get nodes -n security

# 2. Update DNS to point to DR region
# (Manual or automated via DNS failover)

# 3. Scale up DR instances
kubectl scale deployment llm-gateway --replicas=3 -n security

# 4. Verify health
kubectl rollout status deployment/llm-gateway -n security

# 5. Restore audit logs (if needed)
./restore-audit-logs.sh --source $BACKUP_LOCATION --target $DR_DATABASE

# 6. Verify functionality
curl https://llm-gateway-dr.company.com/health
```

### 7.3 RTO/RPO Targets

| Scenario | RTO | RPO |
|----------|-----|-----|
| Instance failure | 1 minute | 0 |
| Zone failure | 5 minutes | 0 |
| Region failure | 30 minutes | 5 minutes |
| Database corruption | 1 hour | 24 hours |

---

## 8. Operational Runbooks

### 8.1 Common Operations

```bash
# Scale up/down
kubectl scale deployment llm-gateway --replicas=5 -n security

# Rolling restart
kubectl rollout restart deployment/llm-gateway -n security

# View logs
kubectl logs -l app=llm-gateway -n security --tail=100 -f

# Check pod status
kubectl get pods -l app=llm-gateway -n security -o wide

# View metrics
kubectl top pods -l app=llm-gateway -n security

# Execute shell in pod
kubectl exec -it $(kubectl get pod -l app=llm-gateway -n security -o jsonpath='{.items[0].metadata.name}') -n security -- /bin/sh
```

### 8.2 Emergency Procedures

```bash
#!/bin/bash
# emergency-shutdown.sh
# Use when critical security incident detected

# 1. Block all traffic
kubectl patch ingress llm-gateway-ingress -n security \
  -p '{"spec":{"rules":[]}}'

# 2. Scale to zero
kubectl scale deployment llm-gateway --replicas=0 -n security

# 3. Preserve logs
kubectl logs -l app=llm-gateway -n security --all-containers > incident-logs.txt

# 4. Notify security team
./notify-security.sh "Emergency shutdown initiated"
```

### 8.3 Health Check Script

```bash
#!/bin/bash
# healthcheck.sh

GATEWAY_URL="${GATEWAY_URL:-https://llm-gateway.company.com}"

# Health endpoint
health=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY_URL/health")
if [ "$health" != "200" ]; then
  echo "CRITICAL: Health check failed with status $health"
  exit 2
fi

# Ready endpoint
ready=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY_URL/ready")
if [ "$ready" != "200" ]; then
  echo "WARNING: Ready check failed with status $ready"
  exit 1
fi

# Test proxy (with test user)
proxy=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $TEST_TOKEN" \
  -X POST "$GATEWAY_URL/api/v1/proxy" \
  -d '{"targetProvider":"openai","body":{"model":"gpt-4","messages":[]}}')
if [ "$proxy" != "200" ]; then
  echo "WARNING: Proxy test failed with status $proxy"
  exit 1
fi

echo "OK: All health checks passed"
exit 0
```

