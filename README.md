# OpenFindBearings.Mobile

移动端 BFF（Backend-for-Frontend），为 Taro H5/小程序/未来 App 提供统一的后台服务接口。BFF 聚合后端 API 调用、处理移动端认证、简化前端请求复杂度。

> 注意：Taro 前端已独立部署到 `mobile.515813.xyz`，BFF 仅提供 API 代理服务，不再托管静态文件。

## 技术栈

- ASP.NET Core Minimal API（.NET 10.0）
- JWT Bearer 认证（通过 Identity）
- IHttpClientFactory（命名客户端：Api、Identity）
- System.Text.Json（属性名大小写不敏感）

## 核心功能

| 功能 | 说明 |
|------|------|
| 首页聚合 | `/mobile/home` 一次返回热门轴承+推荐商家+品牌+类型 |
| 轴承代理 | 搜索、详情、在售商家、替代品 |
| 商家代理 | 搜索、详情、在售商品 |
| 认证代理 | 密码登录、短信登录、验证码发送、令牌刷新（device_id 绑定） |
| 用户资料 | 聚合 Identity 用户信息 + API 业务数据 |
| 站点配置 | `/mobile/config` 返回站点名称/备案号/客服联系方式 |

## 架构

```
Taro H5 (mobile.515813.xyz)          小程序/未来App
        │                                      │
        │  浏览器直连                            │  走 BFF 公网域名
        ▼                                      ▼
  ┌────────────────────────────────────────────────────┐
  │         Mobile BFF (bff.515813.xyz)                │
  │  ASP.NET Core Minimal API                         │
  │  ├─ /mobile/home       首页聚合                    │
  │  ├─ /mobile/bearings/* 轴承代理                    │
  │  ├─ /mobile/merchants/* 商家代理                   │
  │  ├─ /mobile/profile    用户资料                    │
  │  └─ /mobile/auth/*    认证代理                     │
  └───────────┬──────────────────────┬────────────────┘
              │ K8s 内部 HTTP         │ K8s 内部 HTTP
              ▼                       ▼
  ┌────────────────────┐    ┌────────────────────┐
  │  API (:8080)       │    │  Identity (:8080)  │
  │  openfindbearings- │    │  openfindbearings- │
  │  api:80            │    │  identity:80       │
  └────────────────────┘    └────────────────────┘
```

- API 和 Identity 仅通过 K8s 内部 Service 访问，不暴露公网
- BFF 是唯一面向移动端的公网入口（域名 `bff.515813.xyz`）
- Taro 前端独立部署在 `mobile.515813.xyz`，通过 BFF 访问后端

## 域名规划

| 域名 | 用途 | TLS |
|------|------|-----|
| mobile.515813.xyz | Taro H5 静态文件（nginx） | cert-manager |
| bff.515813.xyz | BFF API 代理 | cert-manager |
| auth.abcsxl.com | Identity OAuth | 已部署 |

## 构建与运行

```bash
# 克隆仓库后
cd OpenFindBearings.Mobile

# 恢复依赖
dotnet restore src/OpenFindBearings.Mobile

# 运行（开发）
dotnet run --project src/OpenFindBearings.Mobile

# 构建发布
dotnet publish src/OpenFindBearings.Mobile -c Release -o ./publish
```

默认端口：`http://localhost:8080`

### 本地开发

1. 确保 API 和 Identity 已运行（默认 K8s 内部地址）
2. 开发时 CORS 允许 `http://localhost:10087` 和 `http://172.26.32.1:10087`
3. Taro 开发服务器 `pnpm run dev:h5` 代理 `/mobile/*` 请求到本 BFF

## 部署

### K3s 部署

```bash
kubectl apply -f deploy/k3s/
```

| 资源 | 名称 | 说明 |
|------|------|------|
| Deployment | openfindbearings-mobile | 1 副本，revisionHistoryLimit=5 |
| Service | openfindbearings-mobile | ClusterIP，port 80 → targetPort 8080 |
| Ingress | openfindbearings-mobile-ingress | host: bff.515813.xyz，TLS 自动签发 |

### 手动更新镜像

```bash
# 版本发布后更新
kubectl set image deployment/openfindbearings-mobile \
  openfindbearings-mobile=ghcr.io/openfindbearings/openfindbearings-mobile:v1.0.0

# 等待滚动更新
kubectl rollout status deployment/openfindbearings-mobile --timeout=10m
```

## 环境变量

| 变量 | 默认值 | 说明 |
|------|--------|------|
| ASPNETCORE_ENVIRONMENT | Production | 运行环境 |
| ASPNETCORE_URLS | http://+:8080 | 监听地址 |
| Api__BaseUrl | http://openfindbearings-api:80 | API K8s 内部地址 |
| Identity__Authority | http://openfindbearings-identity:80 | Identity K8s 内部地址 |
| Identity__Audience | api:mobile | JWT Audience |
| Identity__ClientId | maui-client | OAuth 客户端 ID |
| Identity__ClientSecret | maui-secret | OAuth 客户端密钥 |

## 端点清单

### 公开端点

| 路径 | 方法 | 说明 |
|------|------|------|
| `/mobile/home` | GET | 首页聚合 |
| `/mobile/bearings/search` | GET | 轴承搜索 |
| `/mobile/bearings/{id}` | GET | 轴承详情 |
| `/mobile/bearings/{id}/merchants` | GET | 轴承在售商家 |
| `/mobile/merchants/search` | GET | 商家搜索 |
| `/mobile/merchants/{id}` | GET | 商家详情 |
| `/mobile/merchants/{id}/bearings` | GET | 商家在售商品 |
| `/mobile/config` | GET | 站点配置 |

### 认证端点

| 路径 | 方法 | 说明 |
|------|------|------|
| `/mobile/auth/login` | POST | 密码登录 |
| `/mobile/auth/login-sms` | POST | 短信登录 |
| `/mobile/auth/refresh` | POST | 刷新令牌 |
| `/mobile/auth/send-sms` | POST | 发送验证码 |

### 需登录端点

| 路径 | 方法 | 认证 | 说明 |
|------|------|------|------|
| `/mobile/profile` | GET | Bearer | 用户资料 |

## 健康检查

| 路径 | 类型 |
|------|------|
| `/health` | 基本健康 |
| `/health/live` | 存活探针 |
| `/health/ready` | 就绪探针 |

## CI/CD

GitHub Actions 自动构建推送：

- `build.yml`：push/PR 到 main、dev 时构建验证
- `deploy.yml`：Release 发布或手动 workflow_dispatch 时构建镜像推送到 GHCR，并更新 K3s Deployment

镜像地址：`ghcr.io/openfindbearings/openfindbearings-mobile`

## 目录结构

```
src/OpenFindBearings.Mobile/
├── Program.cs               # 入口，服务注册 + 端点映射
├── appsettings.json         # 配置
├── Endpoints/               # 端点定义
│   ├── HomeEndpoints.cs
│   ├── BearingEndpoints.cs
│   ├── MerchantEndpoints.cs
│   ├── ProfileEndpoints.cs
│   ├── AuthEndpoints.cs
│   └── ConfigEndpoints.cs
├── Services/                # 服务层
│   ├── ApiClient.cs         # API HTTP 客户端
│   └── AuthClient.cs        # Identity HTTP 客户端
└── deploy/k3s/              # K3s 部署配置
    ├── deploy.yml
    └── kustomization.yaml
```

## 相关文档

- [BFF 设计文档](./doc/OpenFindBearings.Mobile-BFF设计-v1.0.0.md)
- [Taro 移动端设计](./OpenFindBearings.Taro/doc/OpenFindBearings.Taro移动端设计-v1.1.0.md)
