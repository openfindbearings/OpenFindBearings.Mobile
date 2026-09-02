# OpenFindBearings.Mobile

移动端 BFF（Backend-for-Frontend），为 Taro H5/小程序/未来 App 提供统一的后台服务接口。BFF 聚合后端 API 调用、处理移动端认证、简化前端请求复杂度。

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
- BFF 是唯一面向移动端的公网入口
- Taro 前端独立部署，通过 BFF 访问后端

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

## 相关文档

### 设计文档

- [BFF 设计文档](./doc/OpenFindBearings.Mobile-BFF设计-v1.0.0.md)

### 接口与规范

- [API 端点说明](./doc/01-API端点说明/API端点说明-v1.0.0.md) — 完整端点清单、请求/响应结构、认证要求
- [认证集成设计](./doc/02-认证集成设计/认证集成设计-v1.0.0.md) — JWT 验证、token 刷新、device_id 绑定
- [缓存与限流策略](./doc/03-缓存与限流策略/缓存与限流策略-v1.0.0.md) — 缓存方案、限流规则、降级策略

### 关联项目

- [Taro 移动端设计](../OpenFindBearings.Taro/doc/OpenFindBearings.Taro移动端设计-v1.1.0.md)
