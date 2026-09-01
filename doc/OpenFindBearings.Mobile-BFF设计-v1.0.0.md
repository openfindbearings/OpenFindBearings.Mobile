# OpenFindBearings.Mobile BFF 设计 v1.0.0

## 概述

OpenFindBearings.Mobile 是移动端 BFF（Backend-for-Frontend），为 Taro H5/小程序/未来 App 提供统一的后台服务接口。BFF 聚合后端 API 调用、处理移动端认证、简化前端请求复杂度。

原计划用 MAUI 开发移动客户端，已弃用。Mobile 项目转型为 BFF，Taro 项目负责前端界面，两者独立部署、独立扩缩容。

## 变更日志

### v1.0.0 (2026-08-31)

- 初始版本，BFF 架构设计、端点定义、认证对接、部署配置完整落地。

## 架构定位

```
Taro H5 (mobile.515813.xyz)          Taro 小程序/未来 App
        │                                      │
        │  浏览器直连                              │  走 BFF 公网域名
        ▼                                      ▼
  ┌──────────────────────────────────────────────────┐
  │           Mobile BFF (bff.515813.xyz)              │
  │  ASP.NET Core Minimal API (.NET 10)                │
  │  ├─ /mobile/home       首页聚合                    │
  │  ├─ /mobile/bearings/*  轴承代理                    │
  │  ├─ /mobile/merchants/* 商家代理                    │
  │  ├─ /mobile/profile     用户资料                    │
  │  ├─ /mobile/auth/*     认证代理                    │
  │  └─ /mobile/config      站点配置                    │
  └───────────┬──────────────────────┬─────────────────┘
              │ K8s 内部 HTTP         │ K8s 内部 HTTP
              ▼                      ▼
  ┌────────────────────┐  ┌────────────────────────┐
  │  API (:8080)        │  │  Identity (:8080)      │
  │  openfindbearings-  │  │  openfindbearings-     │
  │  api:80             │  │  identity:80           │
  └────────────────────┘  └────────────────────────┘
```

### 与 Taro 的关系

| 项目 | 职责 | 域名 | 容器 |
|------|------|------|------|
| OpenFindBearings.Taro | 前端界面（H5 静态文件） | mobile.515813.xyz | nginx:alpine |
| OpenFindBearings.Mobile | BFF 后端 API 代理 | bff.515813.xyz | aspnet:10.0 |

Taro H5 构建产物独立部署到 nginx 容器，不打包进 BFF 镜像。两者通过域名分离：
- `mobile.515813.xyz` → Taro H5 静态文件（nginx）
- `bff.515813.xyz/mobile/*` → BFF API 代理（ASP.NET Core）

### 为什么需要 BFF

| 问题 | 无 BFF | 有 BFF |
|------|--------|--------|
| 前端请求次数 | 首页需 3-4 次请求（轴承+商家+配置） | 1 次 `/mobile/home` 聚合返回 |
| 认证复杂度 | 前端直连 Identity，处理 OAuth 流程 | BFF 代理认证，前端只管 token 存取 |
| 跨域问题 | 前端直连多个不同域名服务 | 统一走 BFF 域名，无跨域 |
| 安全暴露 | API/Identity 需要公网 Ingress | API 无公网 Ingress，仅 BFF 暴露 |
| 移动端优化 | 通用 API 响应可能含冗余字段 | BFF 裁剪字段，减少传输量 |

## 技术栈

| 层面 | 选型 | 说明 |
|------|------|------|
| 框架 | ASP.NET Core Minimal API (.NET 10) | 轻量，端点式组织代码 |
| 认证 | JWT Bearer | 转发 Identity 签发的 JWT，可选验证 |
| HTTP 客户端 | IHttpClientFactory + 命名客户端 | "Api" 和 "Identity" 两个命名客户端 |
| 序列化 | System.Text.Json | PropertyNameCaseInsensitive |
| 部署 | Docker + K3s | 单 Pod，ClusterIP Service + Ingress |

## 端点清单

### 公开端点（无需登录）

| 路径 | 方法 | 说明 | 代理目标 |
|------|------|------|----------|
| `/mobile/home` | GET | 首页聚合（热门轴承+推荐商家+品牌+类型） | API 多端点聚合 |
| `/mobile/bearings/search` | GET | 轴承搜索 | API `/api/bearings/search` |
| `/mobile/bearings/{id}` | GET | 轴承详情 | API `/api/bearings/{id}` |
| `/mobile/bearings/{id}/merchants` | GET | 轴承在售商家 | API `/api/bearings/{id}/merchants` |
| `/mobile/merchants/search` | GET | 商家搜索 | API `/api/merchants/search` |
| `/mobile/merchants/{id}` | GET | 商家详情 | API `/api/merchants/{id}` |
| `/mobile/merchants/{id}/bearings` | GET | 商家在售商品 | API `/api/merchants/{id}/bearings` |
| `/mobile/config` | GET | 站点配置（名称/备案/客服） | API `/api/mobile/config` |

### 认证端点（代理 Identity）

| 路径 | 方法 | 说明 | 代理目标 |
|------|------|------|----------|
| `/mobile/auth/login` | POST | 密码登录 | Identity `/connect/token` (grant_type=password) |
| `/mobile/auth/login-sms` | POST | 短信登录 | Identity `/connect/token` (grant_type=sms) |
| `/mobile/auth/refresh` | POST | 刷新令牌 | Identity `/connect/token` (grant_type=refresh_token) |
| `/mobile/auth/send-sms` | POST | 发送验证码 | Identity `/api/sms/send-code` |

### 需登录端点（JWT 验证）

| 路径 | 方法 | 认证 | 说明 | 代理目标 |
|------|------|------|------|----------|
| `/mobile/profile` | GET | Bearer | 用户资料聚合 | Identity `/api/account/me` + API 业务数据 |

## 服务层

### ApiClient

调用后端 API 的 HTTP 客户端封装，所有请求走 K8s 内部 Service（`http://openfindbearings-api:80`），不经公网。

| 方法 | 说明 |
|------|------|
| `GetAsync<T>(path)` | GET 公开接口 |
| `GetAsync<T>(path, token)` | GET 需认证接口（附加 Bearer） |
| `PostAsync<T>(path, body, token)` | POST 请求 |
| `GetPagedAsync<T>(path)` | 分页 GET 公开接口 |
| `GetPagedAsync<T>(path, token)` | 分页 GET 需认证接口 |

API 响应统一包装为 `ApiResponse<T>` 结构，ApiClient 自动拆包返回 `Data` 字段。

### AuthClient

调用 Identity 认证服务的 HTTP 客户端封装，走 K8s 内部 Service（`http://openfindbearings-identity:80`）。

| 方法 | 说明 |
|------|------|
| `LoginAsync(username, password, deviceId)` | 密码登录，返回 token |
| `LoginWithSmsAsync(phone, code, deviceId)` | 短信登录/注册 |
| `RefreshAsync(refreshToken, deviceId)` | 刷新令牌（device_id 校验） |
| `SendSmsCodeAsync(phone)` | 发送验证码 |
| `GetUserInfoAsync(token)` | 获取用户信息 |

## 认证流程

### 登录流程

```
Taro 前端                 Mobile BFF              Identity
─────────                 ──────────              ─────────
POST /mobile/auth/login
  {username, password,    ──────────────────►   POST /connect/token
   deviceId}                                        grant_type=password
                                                    device_id=xxx
                          ◄─────────────────   {access_token, refresh_token}
  ◄── {access_token,
       refresh_token}
  存储 token + deviceId
```

### 令牌刷新流程

```
Taro 前端                 Mobile BFF              Identity
─────────                 ──────────              ─────────
access_token 过期 → 401
POST /mobile/auth/refresh
  {refreshToken, deviceId  ──────────────────►   POST /connect/token
   }                                                 grant_type=refresh_token
                                                    device_id=xxx
                                                    校验 device_id 一致
                          ◄─────────────────   {new access_token, new refresh_token}
  ◄── {新 token}
  重放原请求
```

### device_id 设备绑定

- 登录时前端生成随机 GUID 作为 device_id，存入本地存储
- device_id 随登录请求发送给 Identity，写入 JWT claim
- 刷新令牌时 Identity 校验 device_id 与签发时是否一致，不一致返回 `invalid_grant`
- 各设备不共享登录状态

## 中间件管道

```csharp
// 管道顺序（Program.cs）
app.UseCors("AllowTaro");        // 1. CORS（允许 Taro H5 跨域）
app.UseAuthentication();         // 2. JWT 认证
app.UseAuthorization();          // 3. 授权

// 健康检查
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// API 端点（统一前缀 /mobile）
var mobile = app.MapGroup("/mobile");
mobile.MapHomeEndpoints();
mobile.MapBearingEndpoints();
mobile.MapMerchantEndpoints();
mobile.MapProfileEndpoints();
mobile.MapAuthEndpoints();
```

### CORS 配置

允许的源：
- `http://localhost:10087`（本地开发）
- `http://172.26.32.1:10087`（局域网调试）
- `https://mobile.515813.xyz`（生产 Taro H5）

## 配置

### appsettings.json

```json
{
  "Api": {
    "BaseUrl": "http://openfindbearings-api:80"
  },
  "Identity": {
    "Authority": "http://openfindbearings-identity:80",
    "Audience": "api:mobile",
    "ClientId": "mobile-client",
    "ClientSecret": "mobile-secret"
  }
}
```

### K3s 环境变量覆盖

| 环境变量 | 值 | 说明 |
|----------|-----|------|
| `ASPNETCORE_ENVIRONMENT` | Production | 生产环境 |
| `ASPNETCORE_URLS` | http://+:8080 | 监听端口 |
| `Api__BaseUrl` | http://openfindbearings-api:80 | API K8s 内部地址 |
| `Identity__Authority` | http://openfindbearings-identity:80 | Identity K8s 内部地址 |
| `Identity__Audience` | api:mobile | JWT Audience |
| `Identity__ClientId` | mobile-client | OAuth 客户端 ID |
| `Identity__ClientSecret` | mobile-secret | OAuth 客户端密钥 |

## 部署

### Dockerfile

纯 .NET 构建，不包含 Taro（Taro 独立部署）：

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# ... 编译 BFF

FROM base AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenFindBearings.Mobile.dll"]
```

### K3s 部署

| 资源 | 名称 | 说明 |
|------|------|------|
| Deployment | openfindbearings-mobile | 1 副本，revisionHistoryLimit=5 |
| Service | openfindbearings-mobile | ClusterIP，port 80 → targetPort 8080 |
| Ingress | openfindbearings-mobile-ingress | host: bff.515813.xyz，TLS 自动签发 |

### 健康检查

| 路径 | 类型 | 初始延迟 | 间隔 |
|------|------|----------|------|
| /health/live | Liveness | 15s | 10s |
| /health/ready | Readiness | 5s | 5s |

## CI/CD

### build.yml

触发：push/PR 到 main、dev 分支。纯构建验证，不推送镜像。

### deploy.yml

触发：Release 发布 或 workflow_dispatch。构建 Docker 镜像推送 GHCR，kubectl set image 更新 K3s Deployment。

| 环境变量 | 值 |
|----------|-----|
| IMAGE_NAME | openfindbearings/openfindbearings-mobile |
| DEPLOYMENT | openfindbearings-mobile |
| CONTAINER | openfindbearings-mobile |

## 目录结构

```
src/OpenFindBearings.Mobile/
├── Program.cs               # 入口，服务注册 + 中间件管道 + 端点映射
├── appsettings.json         # 配置（API/Identity 地址）
├── OpenFindBearings.Mobile.csproj
├── Endpoints/               # 端点定义（按业务分组）
│   ├── HomeEndpoints.cs     # 首页聚合
│   ├── BearingEndpoints.cs  # 轴承搜索/详情/在售商家
│   ├── MerchantEndpoints.cs # 商家搜索/详情/在售商品
│   ├── ProfileEndpoints.cs  # 用户资料
│   ├── AuthEndpoints.cs     # 登录/刷新/验证码
│   └── ConfigEndpoints.cs   # 站点配置
├── Services/                # 服务层
│   ├── ApiClient.cs         # API HTTP 客户端
│   └── AuthClient.cs        # Identity HTTP 客户端
├── Properties/
└── deploy/
    └── k3s/
        ├── deploy.yml       # Deployment + Service + Ingress
        └── kustomization.yaml
```
