# OpenFindBearings.Mobile BFF 设计 v1.7.0

## 概述

OpenFindBearings.Mobile 是移动端 BFF（Backend-for-Frontend），为 Taro H5/小程序/未来 App 提供统一的后台服务接口。BFF 聚合后端 API 调用、处理移动端认证、简化前端请求复杂度。

原计划用 MAUI 开发移动客户端，已弃用。Mobile 项目转型为 BFF，Taro 项目负责前端界面，两者独立部署、独立扩缩容。

## 变更日志

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0.0 | 2026-08-31 | 初始版本，BFF 架构设计、端点定义、认证对接、部署配置完整落地 |
| v1.1.0 | 2026-09-11 | 代码审查对齐：① 移除不存在的 `/mobile/config` 端点；② 补充 `/me` 端点组（收藏/关注/历史/资料编辑/头像上传，15 个端点）；③ 补充 `/media` 媒体代理端点；④ 中间件管道补充 RateLimiter；⑤ appsettings.json 修正（audience=openfindbearings-api、移除 ClientSecret、补 Realm/Scope/Internal）；⑥ 端点注册代码补充子组嵌套；⑦ AuthClient 方法表补充 3 个方法；⑧ ApiClient 方法表补充 6 个方法；⑨ 目录结构补充 MeEndpoints/MediaEndpoints |
| v1.2.0 | 2026-09-14 | 商户入驻/维护代理落地与错误透传修复：① `/apply`（`/mobile/merchants/apply`）原 `PostAsync<object>` 吞掉上游所有 4xx/409 且固定返回 200（虚假成功），改用新增 `ApiClient.PostWithResultAsync<T>`（读 ProblemDetails status/code/detail/extensions）按状态码与错误码原样转发，支持 409 `MERCHANT_CLAIMABLE_EXISTS`（携 existingMerchantId/existingName）；② 新增商户信息维护代理 `GET/PUT /mobile/merchant/profile`（→ `/api/merchant/profile`，走 X-Merchant-Id 上下文）与 `POST /mobile/merchant/logo`（multipart 上传 + PublicUrl 拼绝对地址）；③ `MerchantApplicationItem` 新增 `LogoUrl`（TabBar/切换器）；④ 补充"商户入驻与管理代理"章节（此前 merchant 相关代理未列入端点清单），中间件管道补 `/merchant`（MapMerchantManageEndpoints）注册。 |
| v1.3.0 | 2026-09-16 | 媒体代理退役（应用不应逐字节代理图片，是反模式）：删除 `MediaEndpoints.cs`、`/mobile/media` 注册与 `ApiClient.GetRawAsync`。图片改由独立 nginx 媒体服务在 `bff.515813.xyz/media/**` 直出（`OpenFindBearings.Api/deploy/k3s/media-server.yml`）。`MeEndpoints/MerchantManageEndpoints` 的 `PublicUrl` 绝对化助手删除，头像/商户 Logo 上传端点改为原样回 API 落库的**相对媒体键**（`/uploads/...`），host 由前端 `getMediaBase()` 拼接，媒体源 base 经 `GET /mobile/config` 的 `mediaBaseUrl` 下发——相对键入库 + 边缘拼 host，是将来切 MinIO/OSS 免改代码的接缝 |
| v1.4.0 | 2026-09-16 | 站内信代理落地：新增 `NotificationEndpoints.cs`，`/mobile/notifications` 组四端点透传 API `/api/notifications/*`（列表 `GET /`、未读数 `GET /unread-count`、单条已读 `POST /{id}/read`、全部已读 `POST /read-all`），统一 GetToken 透传模式 + RequireAuthorization；供 Taro 消息中心页与 TabBar 未读角标消费（事件驱动链路见 API《06 v2.5.0》4.6） |
| v1.5.0 | 2026-09-16 | 被拒申请自助通道代理（对齐 API《06 v2.6.0》4.1.3）：① `GET /mobile/merchants/{merchantId}/application` → API 申请详情（重提表单预填源，上游 404 原样透传）；② `POST /mobile/merchants/{merchantId}/resubmit` → 修改重提（`PostWithResultAsync`，状态已变/名称撞他人/必填缺失等 4xx 按状态码与 detail 文案原样转发）；③ `POST /mobile/merchants/{merchantId}/delete-application` → 删除被拒申请（`PostVoidAsync`）。均需登录透传 token |
| v1.6.0 | 2026-09-19 | 证照材料泛化代理（对齐 API《06 v2.7.0》4.7）：① `POST /mobile/merchant/license`→`POST /mobile/merchant/documents`（multipart form 带 `type` 1/2/3，上游失败不再虚假成功返回 502 文案）；② 新增 `GET /mobile/merchant/documents`（当前商户材料列表）、`POST /mobile/merchant/documents/upload`（随单材料预上传只回 URL）；③ apply/resubmit/accept 代理请求体 `licenseUrl`→`documents[]`（`DocumentInput{type,fileUrl}` 直通序列化），`ApplicationDetailItem` 补 `documents`；④ `ApiClient.UploadAsync` 新增可选 `extraFields`（置于 `ct` 之后保旧调用兼容）。 |
| v1.6.1 | 2026-09-19 | withdraw / delete-application 代理错误透传修复：原 `PostVoidAsync` 把 API 的 400 守卫原因（"仅待审核可撤
| v1.6.2 | 2026-09-20 | （补记，此前文档滞后于代码）上传链错误透传与防伪修复：① `ApiClient` 新增 `UpstreamUploadException` 携带 API 4xx ProblemDetails 的 detail，`UploadAsync` 失败不再压成 null（材料/Logo/预上传三处透传真实原因）；② 五个 `IFormFile` 端点（头像/Logo/材料/预上传/Excel 导入）加 `.DisableAntiforgery()`——.NET 8+ 对 IFormFile 绑定自动附加防伪元数据，BFF 无防伪中间件即 500（纯 Bearer API 无 cookie 面，显式关闭）。 |
| v1.6.3 | 2026-09-20 | 商户管理域对齐（API《06 v2.9.0》）：① `MerchantStaffItem` 加 `isSelf`（后端权威标记，Taro 登录态 id 与成员 id 不同源无法自判）；② 商品断链修复——`CreateBearingRequest` 字段对齐 API `CreateMerchantBearingCommand`（原 BearingPartNumber/Price/Stock 与后端 BearingId/PriceDescription 完全对不上，添加必失败）；`MyMerchantBearingItem` 补 Id/四项描述/isPendingApproval（编辑回填与审核角标）；新增 `PUT /mobile/merchant/bearings/{id}` 编辑代理；③ 成员管理：新增 `POST /mobile/merchants/staff`（手机号/邮箱添加）与 `DELETE /mobile/merchants/staff/{userId}`（移除）代理；④ 新增 `POST /mobile/merchants/{merchantId}/verify-request` 申请认证代理（400 缺项透传）；`MerchantApplicationItem` 加 `verifyRequested`。 |回 / 仅被驳回可删除 / 无权操作 / 渠道不支持自助"）全部吞成固定"撤回失败/删除失败"，误导排障（曾据此误判为部署时差）。改 `PostWithResultAsync<object>` 按状态码与 ProblemDetails detail 透传，前端 toast 显示真实原因。 |
| v1.7.0 | 2026-09-21 | 员工邀请确认制代理（对齐 API《06 v2.10.0》5.3）：① `POST /mobile/merchants/staff` 响应改为透传 API 真实 message（"邀请已发送，对方同意后加入"/"该用户已是…在职成员"，`AddStaffResponse`），原固定"成员已添加"文案随静默拉入语义废弃；② 新增 4 代理：`GET /mobile/merchants/staff/invitations/pending`（待我确认邀请列表）、`POST /mobile/merchants/staff/invitations/{id}/accept|decline|revoke`（`PostWithResultAsync` 按状态码透传 ProblemDetails detail，400 业务原因前端 toast 直显）；③ `MerchantStaffItem` 加 `invitationId`（Invited 行撤销定位），`ClaimableMerchantItem` 加 `isClaimable/isMine/statusText`（全量发现搜索标记透传）。 |

## 专项文档

| 文档 | 说明 |
|------|------|
| [API 端点说明](./01-API端点说明/API端点说明-v1.2.0.md) | 完整端点清单、请求/响应结构、认证要求 |
| [认证集成设计](./02-认证集成设计/认证集成设计-v1.1.0.md) | JWT 验证、token 刷新、device_id 绑定 |
| [缓存与限流策略](./03-缓存与限流策略/缓存与限流策略-v1.1.0.md) | 缓存方案、限流规则、降级策略 |

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
  │  ├─ /mobile/profile     用户资料（读）               │
   │  ├─ /mobile/me/*        收藏/关注/历史/资料（写）     │
   │  ├─ /mobile/auth/*     认证代理（限流保护）           │
   │  └─ /mobile/config      站点配置（含 mediaBaseUrl）    │
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
| 限流 | ASP.NET Core 内置 RateLimiter | 认证端点 IP 固定窗口 10/min |
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
| `/mobile/bearings/{id}/interchanges` | GET | 轴承替代品 | API `/api/bearings/{id}/interchanges` |
| `/mobile/merchants/search` | GET | 商家搜索 | API `/api/merchants/search` |
| `/mobile/merchants/{id}` | GET | 商家详情 | API `/api/merchants/{id}` |
| `/mobile/merchants/{id}/bearings` | GET | 商家在售商品 | API `/api/merchants/{id}/bearings` |

### 认证端点（代理 Identity，限流保护）

| 路径 | 方法 | 说明 | 代理目标 |
|------|------|------|----------|
| `/mobile/auth/register` | POST | 注册 | Identity `/api/account/register` → 自动登录 |
| `/mobile/auth/login` | POST | 密码登录 | Identity `/connect/token` (grant_type=password) |
| `/mobile/auth/login-sms` | POST | 短信登录 | Identity `/connect/token` (grant_type=sms) |
| `/mobile/auth/refresh` | POST | 刷新令牌 | Identity `/connect/token` (grant_type=refresh_token) |
| `/mobile/auth/send-code` | POST | 发送验证码 | Identity `/api/sms/send-code` |
| `/mobile/auth/logout` | POST | 登出（吊销 refresh_token） | Identity `/connect/revocation` |

### 需登录端点（JWT 验证）

| 路径 | 方法 | 说明 | 代理目标 |
|------|------|------|----------|
| `/mobile/profile` | GET | 用户资料聚合 | Identity `/api/account/me` + API 业务数据 |
| `/mobile/favorites` | GET | 收藏轴承列表 | API `/api/me/favorites` |
| `/mobile/followed` | GET | 关注商家列表 | API `/api/me/follows` |
| `/mobile/me/history/bearings` | GET | 轴承浏览历史 | API `/api/me/history/bearings` |
| `/mobile/me/history/merchants` | GET | 商家浏览历史 | API `/api/me/history/merchants` |
| `/mobile/me/favorites/{bearingId}` | POST | 收藏轴承 | API `/api/me/favorites/{bearingId}` |
| `/mobile/me/favorites/{bearingId}` | DELETE | 取消收藏 | API `/api/me/favorites/{bearingId}` |
| `/mobile/me/favorites/{bearingId}/check` | GET | 检查是否已收藏 | API `/api/me/favorites/{bearingId}/check` |
| `/mobile/me/follows/{merchantId}` | POST | 关注商家 | API `/api/me/follows/{merchantId}` |
| `/mobile/me/follows/{merchantId}` | DELETE | 取消关注 | API `/api/me/follows/{merchantId}` |
| `/mobile/me/follows/{merchantId}/check` | GET | 检查是否已关注 | API `/api/me/follows/{merchantId}/check` |
| `/mobile/me/history/bearings/{bearingId}` | POST | 上报浏览记录 | API `/api/me/history/bearings/{bearingId}` |
| `/mobile/me/history/merchants/{merchantId}` | POST | 上报浏览记录 | API `/api/me/history/merchants/{merchantId}` |
| `/mobile/me/history/bearings/{bearingId}` | DELETE | 删除单条历史 | API `/api/me/history/bearings/{bearingId}` |
| `/mobile/me/history/merchants/{merchantId}` | DELETE | 删除单条历史 | API `/api/me/history/merchants/{merchantId}` |
| `/mobile/me/history/clear` | DELETE | 清空全部历史 | API `/api/me/history/clear` |
| `/mobile/me/profile` | PUT | 编辑资料（双写 Identity+API） | Identity + API |
| `/mobile/me/avatar` | POST | 上传头像（返回相对媒体键） | API `/api/me/avatar` |

### 商户入驻与管理代理（需登录，透传 X-Merchant-Id 当前商户上下文）

商家代理分两组：`/mobile/merchants/*`（`MerchantEndpoints`，入驻 + 公开查询）与 `/mobile/merchant/*`（`MerchantManageEndpoints`，当前商户操作）。`ApiClient` 依据入站 `X-Merchant-Id` 自动向 API 透传该头。

| 路径 | 方法 | 说明 | 代理目标 |
|------|------|------|----------|
| `/mobile/merchants/apply` | POST | 提交入驻申请（self/claim）。v1.2.0 起按状态码/错误码原样转发（409 撞名引导 / 4xx 文案透传） | API `/api/merchant/apply` |
| `/mobile/merchants/application` | GET | 各商户入驻状态（含 logoUrl） | API `/api/merchant/application` |
| `/mobile/merchants/{merchantId}/application` | GET | **v1.5.0** 申请详情（重提预填；非成员 404 透传） | API `/api/merchant/{id}/application` |
| `/mobile/merchants/{merchantId}/resubmit` | POST | **v1.5.0** 被拒修改重提（4xx 文案透传） | API `/api/merchant/{id}/resubmit` |
| `/mobile/merchants/{merchantId}/delete-application` | POST | **v1.5.0** 删除被拒申请 | API `/api/merchant/{id}/delete-application` |
| `/mobile/merchants/claimable` | GET | 认领搜索 | API `/api/merchant/claimable` |
| `/mobile/merchants/nominate` | POST | 提名他人（可带 targetMerchantId） | API `/api/merchant/nominate` |
| `/mobile/merchants/nominate/{code}/accept` | POST | 接受提名 | API `/api/merchant/nominate/{code}/accept` |
| `/mobile/merchants/nominations/pending` | GET | 待我接受的提名 | API `/api/merchant/nominations/pending` |
| `/mobile/merchants/staff` + `members/{id}/suspend\|activate` + `members/{id}/role` | GET/POST/PUT | 成员管理 | API `/api/merchant/staff` 等 |
| `/mobile/merchant/bearings` + `onshelf/offshelf` + `inventory/import` | GET/POST | 自家商品与导入 | API `/api/merchant/bearings` 等 |
| `/mobile/merchant/documents` | POST | **v1.6.0** 上传证照材料（multipart 带 type，入驻后换证/补材料即时建待审记录） | API `/api/merchant/documents` |
| `/mobile/merchant/documents` | GET | **v1.6.0** 当前商户材料列表（信息维护页证照区） | API `/api/merchant/documents` |
| `/mobile/merchant/documents/upload` | POST | **v1.6.0** 随单材料预上传（只回 `{url}` 不建记录） | API `/api/merchant/documents/upload` |
| `/mobile/merchant/profile` | GET | **v1.2.0** 读当前商户资料（维护页回填） | API `/api/merchant/profile` |
| `/mobile/merchant/profile` | PUT | **v1.2.0** 更新资料（需 MerchantAdmin） | API `/api/merchant/profile` |
| `/mobile/merchant/logo` | POST | **v1.2.0** 上传 Logo（返回 PublicUrl 绝对地址） | API `/api/merchant/logo` |

> `apply` 的 409/4xx 转发依赖 `ApiClient.PostWithResultAsync`（见下）。`logo`/`avatar` 等上传端点用 `PublicUrl(http, 相对路径)` 拼 BFF 公网绝对地址回传（X-Forwarded-Proto/Host 推导）。

### 站内信代理（v1.4.0 新增，需登录，`NotificationEndpoints`）

| 路径 | 方法 | 说明 | 代理目标 |
|------|------|------|----------|
| `/mobile/notifications` | GET | 收件箱分页（unreadOnly/page/pageSize） | API `/api/notifications` |
| `/mobile/notifications/unread-count` | GET | 未读条数（TabBar 角标） | API `/api/notifications/unread-count` |
| `/mobile/notifications/{id}/read` | POST | 标记单条已读 | API `/api/notifications/{id}/read` |
| `/mobile/notifications/read-all` | POST | 全部已读 | API `/api/notifications/read-all` |

注册：`Program.cs` `mobile.MapGroup("/notifications").MapNotificationEndpoints()`。

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
| `PutAsync<T>(path, body, token)` | PUT 请求 |
| `GetRawAsync(path, token)` | GET 返回原始 HttpResponseMessage |
| `UploadAsync<T>(path, file, token)` | 文件上传（multipart/form-data） |
| `PutVoidAsync(path, body, token)` | PUT 无返回体（按 HTTP 状态码判定成功） |
| `PostVoidAsync(path, body, token)` | POST 无返回体 |
| `PostWithResultAsync<T>(path, body, token)` | **v1.2.0** POST 带错误透传（读 ProblemDetails status/code/detail/extensions，返回 `ApiCallResult<T>`；供 `/apply` 等需感知 4xx/409 的端点用，区别于 `PostAsync<T>` 吞错返 null） |
| `DeleteVoidAsync(path, token)` | DELETE 无返回体 |

API 响应统一包装为 `ApiResponse<T>` 结构，ApiClient 自动拆包返回 `Data` 字段。

### AuthClient

调用 Identity 认证服务的 HTTP 客户端封装，走 K8s 内部 Service（`http://openfindbearings-identity:80`）。

| 方法 | 说明 |
|------|------|
| `SignUpAsync(username, password, deviceId)` | 注册并自动登录获取 token |
| `LoginAsync(username, password, deviceId)` | 密码登录，返回 token |
| `LoginWithSmsAsync(phone, code, deviceId)` | 短信登录/注册 |
| `RefreshAsync(refreshToken, deviceId)` | 刷新令牌（device_id 校验） |
| `RevokeRefreshTokenAsync(refreshToken)` | 吊销 refresh_token（登出） |
| `SendSmsCodeAsync(phone)` | 发送验证码 |
| `GetUserInfoAsync(token)` | 获取用户信息 |
| `UpdateProfileAsync(token, nickname, pictureUrl)` | 更新 Identity 用户资料 |

## 中间件管道

```csharp
// 管道顺序（Program.cs）
app.UseCors("AllowTaro");        // 1. CORS（允许 Taro H5 跨域）
app.UseRateLimiter();            // 2. 限流（auth 组 IP 固定窗口 10/min）
app.UseAuthentication();         // 3. JWT 认证
app.UseAuthorization();          // 4. 授权

// 健康检查
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// API 端点（统一前缀 /mobile）
var mobile = app.MapGroup("/mobile");
mobile.MapHomeEndpoints();
mobile.MapGroup("/bearings").MapBearingEndpoints();
mobile.MapGroup("/merchants").MapMerchantEndpoints();
mobile.MapGroup("/merchant").MapMerchantManageEndpoints();
mobile.MapProfileEndpoints();
mobile.MapGroup("/me").MapMeEndpoints();
var authGroup = mobile.MapGroup("/auth");
authGroup.MapAuthEndpoints();
authGroup.RequireRateLimiting("auth");
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
    "Audience": "openfindbearings-api",
    "ClientId": "mobile-client",
    "Realm": "openfindbearings",
    "Scope": "api:mobile offline_access"
  },
  "Internal": {
    "ApiToken": "ofb-internal-3f9a1c7e5b2d4a86"
  }
}
```

> 注：`mobile-client` 为公开 OAuth 客户端，无需 ClientSecret。文档历史版本曾记载 ClientSecret 配置项，实际代码不使用。

### K3s 环境变量覆盖

| 环境变量 | 值 | 说明 |
|----------|-----|------|
| `ASPNETCORE_ENVIRONMENT` | Production | 生产环境 |
| `ASPNETCORE_URLS` | http://+:8080 | 监听端口 |
| `Api__BaseUrl` | http://openfindbearings-api:80 | API K8s 内部地址 |
| `Identity__Authority` | http://openfindbearings-identity:80 | Identity K8s 内部地址 |
| `Identity__Audience` | openfindbearings-api | JWT Audience（与 Identity 签发一致） |
| `Identity__ClientId` | mobile-client | OAuth 公开客户端 ID |
| `Internal__ApiToken` | REPLACE_ME | BFF→API 内部令牌（须与 API ConfigMap 一致） |

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
| Ingress | openfindbearings-mobile-ingress | host: bff.515813.xyz，TLS 自动签发，nginx.ingress.kubernetes.io/proxy-body-size=5m |

### 健康检查

| 路径 | 类型 | 初始延迟 | 间隔 |
|------|------|----------|------|
| /health/live | Liveness | 15s | 10s |
| /health/ready | Readiness | 5s | 5s |

## CI/CD

### build.yml

触发：push 到 main/dev 分支、PR 到 main 分支。纯构建验证，不推送镜像。

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
├── appsettings.json         # 配置（API/Identity 地址、Internal 令牌）
├── OpenFindBearings.Mobile.csproj
├── Endpoints/               # 端点定义（按业务分组）
│   ├── HomeEndpoints.cs     # 首页聚合
│   ├── BearingEndpoints.cs  # 轴承搜索/详情/在售商家/替代品
│   ├── MerchantEndpoints.cs # 商家搜索/详情/在售商品
│   ├── ProfileEndpoints.cs  # 用户资料（读）+ 收藏/关注列表
│   ├── MeEndpoints.cs       # 收藏/关注/历史/资料编辑/头像上传（写）
│   └── AuthEndpoints.cs     # 登录/注册/刷新/验证码/登出
├── Services/                # 服务层
│   ├── ApiClient.cs         # API HTTP 客户端
│   └── AuthClient.cs        # Identity HTTP 客户端
├── Properties/
└── deploy/
    └── k3s/
        ├── deploy.yml       # Deployment + Service + Ingress
        ├── configmap.yml    # 配置模板
        └── kustomization.yaml
```
