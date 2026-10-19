# Mobile BFF API 端点说明 v1.8.5

## 概述

OpenFindBearings.Mobile 作为移动端 BFF，所有端点统一挂载在 `/mobile` 前缀下。BFF 代理后端 API（`http://openfindbearings-api:80`）和 Identity（`http://openfindbearings-identity:80`）的调用，前端无需直接访问这两个服务。

路由按资源子组注册（Program.cs）：`/mobile/bearings/*`、`/mobile/merchants/*`、`/mobile/me/*`、`/mobile/auth/*`；`/mobile/home` 与 `/mobile/profile` 挂在 `/mobile` 根下。

> 媒体图片**不再经 BFF 代理**：v1.4.0 删除 `/mobile/media/**` 转发端点（应用逐字节代理图片是反模式）。改由独立 nginx 媒体服务在 `<your-bff-domain>/media/**` 直出（见 `OpenFindBearings.Api/deploy/k3s/media-server.yml`），前端 `getMediaBase()` 拼 base，媒体源 base 经 `GET /mobile/config` 的 `mediaBaseUrl` 下发。

## 变更日志

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.8.5 | 2026-10-19 | 随 API 02 v1.57.0（商家卡级 + 成交回执 + T1 会话 + 电话单向）：寻货组新增 5 个代理——`POST /mobile/sourcing/responses/{id}/feedback`（成交回执）、`GET /mobile/sourcing/conversations`（会话列表，含双侧视角与未读）、`GET /mobile/sourcing/conversations/unread-count`、`GET|POST /mobile/sourcing/conversations/{demandId}/{merchantId}/messages`（分页拉取 / 发送）、`POST /mobile/sourcing/conversations/{demandId}/{merchantId}/read`（标记已读）。契约变化：`GET /mobile/sourcing/demands/{id}` 不再下发 `publisherContact`（电话单向，商家侧永不返回需求方号码），改下发 `contactUnlockedAt`/`selectedResponseId`/`canFeedback`/`myFeedback`；`GET /mobile/sourcing/opportunities` 商家可见商机不含需求方号码；`GET /mobile/points/merchant-grade` 透传 `dealCount`/`windowDays`/`onSaleMin`/`silverDealMin`/`goldDealMin`/`nextTier`。详见文末"本版增量" || v1.8.4 | 2026-10-19 | 随 API 02 v1.56.0 的寻货边界收口（**BFF 无新增路由**）：① `POST /mobile/sourcing/demands` 透传客户端 `Idempotency-Key` 请求头（`SourcingEndpoints` 读头 → `ApiClient.PostWithResultAsync` 新增可选 `headers` 参数转发；缺省不带头）——上游据此去重，网络重试/连续点击重放返回原单、不重复扣分；② `DELETE /mobile/sourcing/demands/{id}/respond` 撤销语义由"删行"改"置 Cancelled"，BFF 无感知（原样透传结果）；③ 治理输出透传 `isDeleted`（Admin 侧使用）。详见文末"本版增量" |
| v1.8.3 | 2026-10-19 | 随 API 02 v1.55.0 的邀标边界补全（**BFF 无路由/字段改动**）：寻货发布响应字段由 `notifiedMerchantCount` 更名 `recipientMerchantCount`——BFF 以 `PostWithResultAsync<object>` 原样透传，前端读取端已同步更名；`GET /mobile/sourcing/matched` 的邀标聚合口径、`POST /mobile/sourcing/demands` 的 `visibility`/`invitedMerchantIds` 透传与 `publish-public` 代理行为均不变；分享落地页描述文案「查看在售商家并询价」改「查看在售商家并邀请报价」（单据类型只有公开招标/邀标，`/s/bearing/{id}` 与 `/s/merchant/{id}` 不变） |
| v1.8.2 | 2026-10-19 | 寻货公开招标/邀标（对齐 API《02 v1.54.0》《搜索寻货与数据可信联动设计 v1.2.0》）：新增 `POST /mobile/sourcing/demands/{id}/publish-public` 代理（转为公开招标）；`POST /mobile/sourcing/demands` 请求体 `targetMerchantId` → `visibility`+`invitedMerchantIds` 透传、响应 `matchedMerchantCount` → `notifiedMerchantCount` 透传；寻货详情/我的寻货/商户寻货/`matched`/`opportunities` 透传 `isPrivate`。详见文末"本版增量" |
| v1.8.1 | 2026-10-18 | 替代品字典化（对齐 API《02 v1.52.0》《16-替代品治理设计 v2.0.0》）：删除 `POST/GET /mobile/merchant/interchanges` 两个代理（商家替代候选流取消，上游端点已删）；`GET /mobile/bearings/{id}/interchanges` 语义改为"该轴承全部生效替代关系"（审核态过滤随状态机删除，行为等价）。详见文末"本版增量" |
| v1.8.0 | 2026-10-16 | 分享落地页：新增匿名端点 `GET /s/bearing/{id}`、`GET /s/merchant/{id}`——服务端渲染 HTML（注入 Open Graph og 元信息供微信/社交抓取成图文卡片）并自动跳转 H5 对应页；由 H5 镜像 nginx 将 `/s/` 反代到 BFF，故分享链接落在 H5 域名下。BFF 新增 `Share` 配置节（`H5BaseUrl`/`MediaBaseUrl`）。详见文末"本版增量" |
| v1.7.0 | 2026-10-15 | 搜索—寻货—认证商家与数据可信联动（对齐 API《02 v1.49.0》《搜索寻货与数据可信联动设计 v1.0.0》）：新增 `GET/PUT /mobile/notifications/preferences`（通知偏好回显/保存）、`GET /mobile/sourcing/matched`（为你匹配的寻货，商家侧聚合 tab）；变更 `POST /mobile/sourcing/demands`（请求体加 `targetMerchantId` 定向询价透传）、`GET /mobile/sourcing/demands/{id}`（输出加 `isTargeted`）、`GET /mobile/bearings/{id}`（加 `dataSource`）、`GET /mobile/bearings/{id}/merchants`（加 `isVerified`）、`GET /mobile/merchants/{id}`（公共面移除 phone/mobile/email，contact 仅本商户成员自看下发）。明细见文末"本版增量" |
| v1.6.0 | 2026-10-13 | 商家商品审核/删除 + 替代品三级治理：新增 `DELETE /mobile/merchant/bearings/{id}`（删除代理，失败原因透传）、`POST/GET /mobile/merchant/interchanges`（商家提交替代候选 / 我的提交）；变更 `GET /mobile/bearings/{id}/interchanges`（只返回已生效、富 DTO 含 typeLabel/方向）、`GET /mobile/merchant/bearings`（契约改 `saleState`+`reviewStatus`）、`GET /mobile/mall/feed`（卡片含 saleState）。明细见文末"本版增量" |
| v1.5.0 | 2026-09-30 | 新增 `GET /mobile/brands`（品牌字典，匿名，发现页寻货筛选面板数据源，代理 API `/api/brands` 不复用 `/home` 聚合）；寻货 feed 代理契约变更：`GET /mobile/sourcing/demands` 删 `mineOnly`、增 `brand`/`region`/`sort`（对齐 API《02 v1.44.0》《BFF 设计 v1.12.0》；feed 端点此前未入本表明细，此行仅记契约变更） |
| v1.5.0（同版追加） | 2026-10-01 | 新增 `DELETE /mobile/sourcing/demands/{id}/respond` 撤销应答代理（对齐 API《02 v1.45.0》同版追加 +《BFF 设计 v1.13.0》同版追加）：登录+当前商户，透传 API 云端错误（PROXY_FAIL/上游错误码），`ApiClient` 新增 `DeleteWithResultAsync<T>`；寻货组端点数 +1 |
| v1.4.0 | 2026-09-16 | 媒体代理退役：删除 `MediaEndpoints.cs` 及 `/mobile/media/**` 注册、`ApiClient.GetRawAsync`、`MeEndpoints.PublicUrl`。图片改由独立 nginx 媒体服务 `/media/**` 直出；头像/商户 Logo 上传端点原样返回 API 落库的**相对媒体键**（不再拼绝对 URL，host 由前端拼媒体源）。`GET /mobile/config` 响应新增 `mediaBaseUrl` 字段供前端覆盖媒体源 |
| v1.3.0 | 2026-09-14 | 配置与版本端点：① 新增 `GET /mobile/config`（代理 API `/api/mobile/config`，匿名）——修复 Taro `getSiteConfig()` 因 BFF 缺该路由恒 404 的问题；② 新增 `GET /mobile/version/check`（代理 API `/api/mobile/version/check`，匿名，透传 currentVersion/platform）配合移动端版本更新功能 |
| v1.2.0 | 2026-09-11 | 代码审查对齐：① 补充 `/me` 端点组（收藏/关注/历史/资料编辑/头像上传，18 个端点）；② 补充 `/media` 媒体代理；③ 补充认证端点 register/logout；④ 补充手机号格式校验；⑤ 轴承搜索补充 8 个缺失参数（BrandId/BearingTypeId/SortBy/SortOrder/6 维度范围）；⑥ 商家搜索补充 SortBy/SortOrder；⑦ 修正 favorites/followed DTO 为嵌套结构（含 CreatedAt）；⑧ 修正端点计数；⑨ 修正 PagedResult 无 totalPages 字段 |
| v1.1.0 | 2026-09-08 | 路由按资源子组注册；BFF→API 附带 X-Internal-Token；用户资料路径修正；商家 DTO 字段对齐 |
| v1.0.0 | 2026-09-01 | 初始版本 |

## 端点总览

| 分组 | 端点数 | 认证要求 |
|------|--------|----------|
| 首页聚合与字典 | 2 | 无需登录 |
| 轴承 | 5 | 无需登录 |
| 商家 | 5 | 公开端点无需登录，入驻申请需登录 |
| 商家管理 | 23 | 需登录（Bearer JWT）+ X-Merchant-Id 成员/管理员校验 |
| 用户资料（读） | 3 | 需登录（Bearer JWT） |
| 个人中心（写） | 18 | 需登录（Bearer JWT） |
| 配置与版本 | 2 | 无需登录 |
| 认证 | 6 | 无需登录（受 IP 限流） |

## 公开端点（无需登录）

### GET /mobile/home

首页聚合数据，一次请求返回热门轴承、推荐商家、品牌列表、类型列表。

**响应示例**：

```json
{
  "hotBearings": [
    {
      "id": "uuid",
      "partNumber": "6205-2RS",
      "oldNumber": "180205",
      "bearingType": "深沟球轴承",
      "innerDiameter": 25,
      "outerDiameter": 52,
      "width": 15,
      "brandName": "SKF",
      "image3DUrl": "/images/bearings/3d/....gif",
      "image2DUrl": "/images/bearings/2d/....jpg"
    }
  ],
  "merchants": [
    {
      "id": "uuid",
      "name": "洛阳轴承",
      "companyName": "洛阳某某有限公司",
      "isVerified": true,
      "productCount": 123,
      "logoUrl": "/images/merchants/....jpg"
    }
  ],
  "brands": [
    { "id": "uuid", "name": "SKF" }
  ],
  "bearingTypes": [
    { "id": "uuid", "name": "深沟球轴承" }
  ]
}
```

### GET /mobile/brands

品牌字典（v1.5.0 新增，匿名）：发现页寻货筛选面板数据源，代理 API `/api/brands`。独立轻量端点、不复用 `/home` 聚合，避免为品牌列表拉整页数据。

**响应示例**：

```json
[
  { "id": "uuid", "name": "SKF", "country": "瑞典" }
]
```

### GET /mobile/bearings/search

搜索轴承，支持关键字、品牌、类型、ID 精确匹配、排序、尺寸范围筛选。

**查询参数**：

| 参数 | 类型 | 必填 | 默认 | 说明 |
|------|------|------|------|------|
| keyword | string | 否 | - | 型号/品牌/旧代号关键词 |
| brandName | string | 否 | - | 品牌名精确筛选 |
| brandId | Guid | 否 | - | 品牌 ID 精确筛选 |
| bearingType | string | 否 | - | 类型名精确筛选 |
| bearingTypeId | Guid | 否 | - | 类型 ID 精确筛选 |
| sortBy | string | 否 | - | 排序字段（partNumber/brandName 等） |
| sortOrder | string | 否 | asc | 排序方向（asc/desc） |
| minInnerDiameter | decimal | 否 | - | 最小内径（mm） |
| maxInnerDiameter | decimal | 否 | - | 最大内径（mm） |
| minOuterDiameter | decimal | 否 | - | 最小外径（mm） |
| maxOuterDiameter | decimal | 否 | - | 最大外径（mm） |
| minWidth | decimal | 否 | - | 最小宽度（mm） |
| maxWidth | decimal | 否 | - | 最大宽度（mm） |
| page | int | 否 | 1 | 页码 |
| pageSize | int | 否 | 20 | 每页条数 |

**响应**：分页结构 `PagedResult<BearingItem>`

```json
{
  "items": [
    {
      "id": "uuid",
      "partNumber": "6205-2RS",
      "oldNumber": "180205",
      "bearingType": "深沟球轴承",
      "innerDiameter": 25,
      "outerDiameter": 52,
      "width": 15,
      "brandName": "SKF",
      "image3DUrl": null,
      "image2DUrl": "/images/bearings/2d/....jpg"
    }
  ],
  "totalCount": 1234,
  "page": 1,
  "pageSize": 20
}
```

### GET /mobile/bearings/{id}

获取轴承详情，包含完整参数（尺寸、重量、英文名）和浏览/收藏计数。

**路径参数**：`id` — 轴承 GUID

**响应**：

```json
{
  "id": "uuid",
  "partNumber": "6205-2RS",
  "oldNumber": "180205",
  "englishName": "Deep Groove Ball Bearing",
  "bearingType": "深沟球轴承",
  "innerDiameter": 25,
  "outerDiameter": 52,
  "width": 15,
  "weight": 0.13,
  "brandName": "SKF",
  "brandCountry": "瑞典",
  "image3DUrl": "/images/...",
  "image2DUrl": "/images/...",
  "viewCount": 1234,
  "favoriteCount": 56
}
```

### GET /mobile/bearings/{id}/merchants

获取某轴承的在售商家列表。

**路径参数**：`id` — 轴承 GUID

**查询参数**：`page`（默认 1）、`pageSize`（默认 20）

**响应**：分页结构，每项包含 `merchantId`、`merchantName`、`price`（价格描述文本）、`isOnSale`。

### GET /mobile/bearings/{id}/interchanges

获取轴承替代品列表。

**路径参数**：`id` — 轴承 GUID

**响应**：数组，每项包含 `id`、`partNumber`、`brandName`、`bearingType`、`interchangeType`、`typeLabel`（标准等效替代 / 条件替代（需评估）/ 仅供参考）、`confidence`、`isBidirectional`、`remarks` 及方向标注。仅返回**已生效**（审核通过）的替代关系；双向关系按"源 ∪ 目标"合并展开。

---

## 商家端点

### GET /mobile/merchants/search

搜索商家，支持关键字、认证状态筛选、排序。

**查询参数**：

| 参数 | 类型 | 必填 | 默认 | 说明 |
|------|------|------|------|------|
| keyword | string | 否 | - | 商家名关键词 |
| verifiedOnly | bool | 否 | - | 仅已认证商家 |
| sortBy | string | 否 | - | 排序字段 |
| sortOrder | string | 否 | asc | 排序方向 |
| page | int | 否 | 1 | 页码 |
| pageSize | int | 否 | 20 | 每页条数 |

**响应**：分页结构 `PagedResult<MerchantItem>`

```json
{
  "items": [
    {
      "id": "uuid",
      "name": "洛阳轴承",
      "companyName": "洛阳某某有限公司",
      "type": "经销商",
      "isVerified": true,
      "status": "Active",
      "productCount": 123,
      "logoUrl": "/images/merchants/....jpg"
    }
  ],
  "totalCount": 690,
  "page": 1,
  "pageSize": 20
}
```

### GET /mobile/merchants/{id}

获取商家详情。

**路径参数**：`id` — 商家 GUID

**响应**：

```json
{
  "id": "uuid",
  "name": "洛阳轴承",
  "companyName": "洛阳某某有限公司",
  "type": "经销商",
  "contactPerson": "张经理",
  "phone": "0379-...",
  "mobile": "13800138000",
  "email": "...",
  "address": "...",
  "isVerified": true,
  "status": "Active",
  "grade": "...",
  "followerCount": 0,
  "productCount": 123,
  "logoUrl": "/images/merchants/....jpg"
}
```

### GET /mobile/merchants/{id}/bearings

获取商家在售商品列表。

**路径参数**：`id` — 商家 GUID

**查询参数**：`page`（默认 1）、`pageSize`（默认 20）

**响应**：分页结构，每项包含 `bearingId`、`bearingPartNumber`、`oldNumber`、`bearingTypeName`、`brandName`、尺寸三字段、`price`、`isOnSale`。

### POST /mobile/merchants/apply

商家入驻申请。**需要登录**。

**请求体**：

```json
{
  "contactName": "张经理",
  "phone": "13800138000",
  "description": "主营轴承销售",
  "licenseUrl": "https://..."
}
```

**响应**：

```json
{
  "message": "申请已提交，等待审核"
}
```

---

## 用户资料端点（需登录，读）

所有端点需要 `Authorization: Bearer {token}` 头。

### GET /mobile/profile

获取当前用户资料（聚合 Identity 用户信息 + 业务扩展字段）。

**响应**：

```json
{
  "id": "uuid",
  "userName": "13800138000",
  "phoneNumber": "13800138000",
  "isActive": true,
  "createdAt": "2026-08-31T12:00:00Z",
  "lastLoginAt": "2026-09-01T10:00:00Z"
}
```

### GET /mobile/favorites

获取当前用户的收藏轴承列表。

**查询参数**：`page`（默认 1）、`pageSize`（默认 20）

**响应**：分页结构，每项为嵌套对象：

```json
{
  "items": [
    {
      "id": "favorite-uuid",
      "createdAt": "2026-09-01T10:00:00Z",
      "bearing": {
        "id": "bearing-uuid",
        "partNumber": "6205-2RS",
        "brandName": "SKF",
        "image3DUrl": "/images/..."
      }
    }
  ],
  "totalCount": 5,
  "page": 1,
  "pageSize": 20
}
```

### GET /mobile/followed

获取当前用户的关注商家列表。

**查询参数**：`page`（默认 1）、`pageSize`（默认 20）

**响应**：分页结构，每项为嵌套对象：

```json
{
  "items": [
    {
      "id": "follow-uuid",
      "createdAt": "2026-09-01T10:00:00Z",
      "merchant": {
        "id": "merchant-uuid",
        "name": "洛阳轴承",
        "isVerified": true
      }
    }
  ],
  "totalCount": 3,
  "page": 1,
  "pageSize": 20
}
```

---

## 个人中心端点（需登录，写）

所有端点需要 `Authorization: Bearer {token}` 头，路径挂在 `/mobile/me/*`。

### 收藏轴承

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/mobile/me/favorites/{bearingId}` | 收藏轴承 |
| DELETE | `/mobile/me/favorites/{bearingId}` | 取消收藏 |
| GET | `/mobile/me/favorites/{bearingId}/check` | 检查是否已收藏 |

### 关注商家

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/mobile/me/follows/{merchantId}` | 关注商家 |
| DELETE | `/mobile/me/follows/{merchantId}` | 取消关注 |
| GET | `/mobile/me/follows/{merchantId}/check` | 检查是否已关注 |

### 浏览历史

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/mobile/me/history/bearings` | 轴承浏览历史（分页） |
| GET | `/mobile/me/history/merchants` | 商家浏览历史（分页） |
| POST | `/mobile/me/history/bearings/{bearingId}` | 上报浏览记录 |
| POST | `/mobile/me/history/merchants/{merchantId}` | 上报浏览记录 |
| DELETE | `/mobile/me/history/bearings/{bearingId}` | 删除单条历史 |
| DELETE | `/mobile/me/history/merchants/{merchantId}` | 删除单条历史 |
| DELETE | `/mobile/me/history/clear` | 清空全部历史 |

### 个人资料编辑

| 方法 | 路径 | 说明 |
|------|------|------|
| PUT | `/mobile/me/profile` | 编辑资料（双写 Identity + API） |
| POST | `/mobile/me/avatar` | 上传头像（jpg/png/webp，≤2MB） |

**PUT /mobile/me/profile 请求体**：

```json
{
  "nickname": "张三",
  "occupation": "工程师",
  "companyName": "某某公司",
  "industry": "机械制造"
}
```

> BFF 双写：Identity（nickname/pictureUrl）+ API（nickname/avatar/occupation/companyName/industry）。

---

## 配置与版本端点（匿名）

改动说明：版本更新功能上线，新增两条匿名只读代理，注册于 `mobile.MapConfigEndpoints()`（ConfigEndpoints.cs）。

### GET /mobile/config

代理 API `GET /api/mobile/config`，返回站点配置与版本相关字段（siteName/siteDescription/siteBeiAn/customerService/appVersion/downloadUrl/forceUpdate）。修复原 Taro `getSiteConfig()` 因 BFF 缺该路由恒 404。

### GET /mobile/version/check?currentVersion=&platform=

代理 API `GET /api/mobile/version/check`，透传 `currentVersion`/`platform`（`Uri.EscapeDataString` 转义，版本串含 `+` 等 query 保留字符）。返回 `{hasUpdate, latestVersion, isForceUpdate, updateMessage, downloadUrl}`，版本比较（SemVer 2.0）在 API 侧完成，BFF 不判定。

---

## 认证端点（代理 Identity，限流保护）

所有认证端点受 IP 限流保护（10 次/分钟/固定窗口）。手机号格式校验：`^1[3-9]\d{9}$`，无效返回 400 PHONE_INVALID。

### POST /mobile/auth/register

注册新用户并自动登录。

**请求体**：

```json
{
  "username": "13800138000",
  "password": "your-password",
  "deviceId": "随机GUID"
}
```

**成功响应（200）**：

```json
{
  "success": true,
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "...",
  "expiresIn": 600
}
```

**失败响应**：
- 400：手机号格式不正确（PHONE_INVALID）
- 409：用户已存在

### POST /mobile/auth/login

密码登录。BFF 代理 Identity 的 OAuth password grant。

**请求体**：

```json
{
  "username": "13800138000",
  "password": "your-password",
  "deviceId": "随机GUID"
}
```

**成功响应（200）**：同 `/register`。

**失败响应（401）**：

```json
{ "success": false, "message": "手机号或密码错误" }
```

### POST /mobile/auth/login-sms

短信验证码登录/注册。BFF 代理 Identity 的 SMS grant。

**请求体**：

```json
{ "phone": "13800138000", "code": "123456", "deviceId": "随机GUID" }
```

**响应**：同 `/login`。

### POST /mobile/auth/send-code

发送短信验证码。

**请求体**：

```json
{ "phone": "13800138000" }
```

**成功响应（200）**：

```json
{ "success": true, "message": "验证码已发送" }
```

### POST /mobile/auth/refresh

刷新令牌。需要 `refreshToken` 和 `deviceId`（两者必须与签发时一致）。

**请求体**：

```json
{ "refreshToken": "旧refresh_token", "deviceId": "登录时的同一个GUID" }
```

**响应**：同 `/login`，返回新的 access_token 和 refresh_token。

### POST /mobile/auth/logout

登出（吊销 refresh_token）。

**请求体**：

```json
{ "refreshToken": "当前refresh_token" }
```

**响应**：

```json
{ "success": true, "message": "已退出登录" }
```

---

## 通用响应结构

### 分页响应

```json
{ "items": [...], "totalCount": 1234, "page": 1, "pageSize": 20 }
```

> `PagedResult<T>` 定义：`record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize)`。不含 `totalPages` 字段。

### 错误响应

BFF 返回 HTTP 状态码 + JSON body：

```json
{ "success": false, "message": "错误描述" }
```

## 认证要求汇总

| 端点 | 方法 | 认证 | 限流 | 说明 |
|------|------|------|------|------|
| `/mobile/home` | GET | 无 | 无 | 首页聚合 |
| `/mobile/brands` | GET | 无 | 无 | 品牌字典（筛选面板） |
| `/mobile/bearings/search` | GET | 无 | 无 | 轴承搜索 |
| `/mobile/bearings/{id}` | GET | 无 | 无 | 轴承详情 |
| `/mobile/bearings/{id}/merchants` | GET | 无 | 无 | 在售商家 |
| `/mobile/bearings/{id}/interchanges` | GET | 无 | 无 | 替代品 |
| `/mobile/merchants/search` | GET | 无 | 无 | 商家搜索 |
| `/mobile/merchants/{id}` | GET | 无 | 无 | 商家详情 |
| `/mobile/merchants/{id}/bearings` | GET | 无 | 无 | 在售商品 |
| `/mobile/merchants/apply` | POST | Bearer | 无 | 入驻申请 |
| `/mobile/profile` | GET | Bearer | 无 | 用户资料 |
| `/mobile/favorites` | GET | Bearer | 无 | 收藏列表 |
| `/mobile/followed` | GET | Bearer | 无 | 关注列表 |
| `/mobile/me/favorites/{id}` | POST/DELETE | Bearer | 无 | 收藏/取消 |
| `/mobile/me/favorites/{id}/check` | GET | Bearer | 无 | 检查收藏 |
| `/mobile/me/follows/{id}` | POST/DELETE | Bearer | 无 | 关注/取关 |
| `/mobile/me/follows/{id}/check` | GET | Bearer | 无 | 检查关注 |
| `/mobile/me/history/*` | GET/POST/DELETE | Bearer | 无 | 浏览历史 CRUD |
| `/mobile/me/profile` | PUT | Bearer | 无 | 编辑资料 |
| `/mobile/me/avatar` | POST | Bearer | 无 | 上传头像 |
| `/mobile/auth/register` | POST | 无 | 10/min/IP | 注册 |
| `/mobile/auth/login` | POST | 无 | 10/min/IP | 密码登录 |
| `/mobile/auth/login-sms` | POST | 无 | 10/min/IP | 短信登录 |
| `/mobile/auth/send-code` | POST | 无 | 10/min/IP | 发送验证码 |
| `/mobile/auth/refresh` | POST | 无 | 10/min/IP | 刷新令牌 |
| `/mobile/auth/logout` | POST | 无 | 10/min/IP | 登出 |

## BFF→API 内部令牌

BFF 调用 API 时统一附带请求头 `X-Internal-Token`（值取自配置 `Internal:ApiToken`），API 限流中间件识别匹配后豁免按 IP 的用户级限流。原因：API 无公网 ingress，全部 App 流量经单一 BFF Pod IP 转发，若按 IP 限流会误伤聚合流量（guest 30/min 触发 429 → 前端空结果）。令牌经 K3s ConfigMap 注入 API 与 BFF 两侧，必须同值。详见《缓存与限流策略 v1.1.0》。

---

## 本版增量（v1.6.0）：商家商品审核/删除 + 替代品三级治理

### 新增端点（商家管理组 /mobile/merchant）
| 方法 | 路径 | 说明 |
|---|---|---|
| DELETE | /mobile/merchant/bearings/{id} | 商家删除商品（代理 API DELETE；出售中/补货中禁删，失败原因透传，其余物理删除） |
| ~~POST / GET~~ `/mobile/merchant/interchanges` | **已于 v1.8.1 移除**（替代品字典化）：商家不再报替代候选，改走轴承「纠错」的 `interchange` 字段（由平台核实后在替代品管理页落地） |

### 变更端点 / 字段
| 端点 | 变更 |
|---|---|
| GET /mobile/mall/feed | 货源导购卡片契约，含 `saleState` |
| GET /mobile/merchant/bearings | `MyMerchantBearingItem` 由 `isOnSale`/`isRestocking`/`isPendingApproval` 收敛为 `saleState` + `reviewStatus`；上架语义改为未通过→提交审核、已通过→出售中 |
| GET /mobile/bearings/{id}/interchanges | 只返回已生效替代；DTO 增 `interchangeType`/`typeLabel`/`isBidirectional`/`remarks` 等 |

> **文档欠账**：商家管理组（/mobile/merchant/* 商品/成员/材料/金库/挂礼）与积分/寻货/成就/商城/站内信/游戏等组此前未逐端点入本表，待专批补录。

---

## 本版增量（v1.7.0）：搜索—寻货—认证商家与数据可信联动

### 新增端点
| 方法 | 路径 | 说明 |
|---|---|---|
| GET | /mobile/notifications/preferences | 读取通知偏好（渠道开关 + `smsGatewayReady`，登录透传） |
| PUT | /mobile/notifications/preferences | 保存通知偏好（sms/weChat/email 三项，null=不改） |
| GET | /mobile/sourcing/matched | 为你匹配的寻货（透传 X-Merchant-Id，定向给我的 + 匹配我在售型号的进行中广播） |

### 变更端点 / 字段
| 端点 | 变更 |
|---|---|
| POST /mobile/sourcing/demands | 请求体加 `targetMerchantId`（定向询价透传；null=广播） |
| GET /mobile/sourcing/demands/{id} | 输出加 `isTargeted`（定向单标记） |
| GET /mobile/bearings/{id} | `BearingDetail` 加 `dataSource`（参数来源标"仅供参考"依据） |
| GET /mobile/bearings/{id}/merchants | `BearingMerchantItem` 加 `isVerified`（询价行动门"仅认证商家可询价"依据） |
| GET /mobile/merchants/{id} | `MerchantDetail` 移除 `phone/mobile/email`（电话单轨：号码只在寻货撮合达成后互给）；`contactPerson/address` 保留 |

> 配套文档：《搜索寻货与数据可信联动设计 v1.0.0》（开源 Api 仓 doc/17）。

---

## 本版增量（v1.8.0）：分享落地页 /s/{id}

**动机**：App 分享出去的链接原为内部路由 `/pages/...`（对接收方无意义、且暴露一串地址）；改为服务端落地页后，微信/社交可抓取 og 成"图文卡片"，点开跳 H5 对应页。

### 新增端点（公开组 /s，匿名）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | /s/bearing/{id} | 轴承货源分享落地页：og=型号/品牌/在售认证商家数，跳 H5 `/pages/home/bearingDetail?id={id}[&view=supply]` |
| GET | /s/merchant/{id} | 商家主页分享落地页：og=商家名 +（认证商家/轴承商家），跳 H5 `/pages/merchant/merchantDetail?id={id}` |

- 返回 `text/html; charset=utf-8`：含 `og:*`、`twitter:card` 元信息 + 自动跳转脚本与兜底链接；数据经 `ApiClient` 取 API（`/api/bearings/{id}`、`/api/merchants/{id}`、`/api/bearings/{id}/merchants`）。
- 未取到数据时 302 回 `/`（H5 首页）。
- **部署**：由 H5 镜像 `nginx.conf` 的 `location /s/` 反代到 BFF，故对外分享链接形如 `https://<h5域名>/s/bearing/{id}`。

### 新增配置（appsettings.json）

| 键 | 说明（开源占位） |
|---|---|
| `Share:H5BaseUrl` | H5 站点公网地址（跳转目标与链接域名），如 `https://<your-h5-domain>` |
| `Share:MediaBaseUrl` | 媒体源 base（og:image 绝对化），如 `https://<your-media-domain>/media` |

> 配套文档：《移动端登录与商户入驻设计 v1.7.35》。

---

## 本版增量（v1.8.1）：替代品字典化（-2 端点）

| 变更 | 说明 |
|---|---|
| 移除 `POST /mobile/merchant/interchanges` | 商家替代候选提交代理（上游 API 端点已删） |
| 移除 `GET /mobile/merchant/interchanges` | 我的替代提交代理（同上） |
| `GET /mobile/bearings/{id}/interchanges` | 审核态过滤随状态机删除，语义为"该轴承全部生效替代关系"（行为等价）；仍返回 `interchangeType`/`typeLabel`/`isBidirectional`/`remarks` 与方向标注 |

> 口径：替代关系回归平台维护的主数据（字典）——数据来自权威网站抓取（Sync staging 审核后入库）或平台人工维护；
> 商家/用户参与通道为**纠错**（对轴承提交 `interchange` 字段，Taro 轴承详情「替代品」区块提供"点此报告"入口），审批不自动应用、由平台在替代品管理页落地。
> 配套文档：《16-替代品治理设计 v2.0.0》《API 端点说明 v1.52.0》。

---

## 本版增量（v1.8.2）：寻货公开招标 / 邀标（+1 端点）

| 端点 | 变更 |
|---|---|
| `POST /mobile/sourcing/demands/{id}/publish-public` | **新增代理**：转为公开招标（邀标单进行中时由发布人放开为公开）；`PostWithResultAsync<object>` 原样透传上游结果与失败原因 |
| `POST /mobile/sourcing/demands` | 请求体透传字段由 `targetMerchantId` 改为 `visibility`（0公开招标/1邀标）+ `invitedMerchantIds`；响应 `matchedMerchantCount` 更名 `notifiedMerchantCount` 原样随行（`PostWithResultAsync<object>` 无需改代码） |
| `GET /mobile/sourcing/demands/{id}`、`/my/demands`、`/merchant/demands`、`/matched` | 透传 `isPrivate`（邀标单标记；`isTargeted` 已弃用）；`/matched` 增列"邀标给我的进行中单" |

> 口径：BFF 只做透传，不承载"谁能看邀标单"的判定——可见性由 API 侧统一裁决（邀标单非收件商家取详情返回 404）。
> 配套文档：《搜索寻货与数据可信联动设计 v1.2.0》《API 端点说明 v1.54.0》《移动端登录与商户入驻设计 v1.7.38》。

---

## 本版增量（v1.8.4）：寻货边界收口（无新增路由）

| 端点 | 变更 |
|---|---|
| `POST /mobile/sourcing/demands` | **透传 `Idempotency-Key`**：`SourcingEndpoints` 读入站请求头，非空时以自定义头转发上游（`ApiClient.PostWithResultAsync` 新增可选 `headers` 参数——带头时走显式 `HttpRequestMessage` + `JsonContent.Create`，与 `PostAsJsonAsync` 同用默认 JSON 选项）；缺省不带头，行为与旧版一致 |
| `DELETE /mobile/sourcing/demands/{id}/respond` | 上游撤销由"物理删行"改"置 `Cancelled` 留行"（当日额度不回退、可重新应答），BFF 无需改动（原样透传成功/失败消息） |
| 治理侧 | `isDeleted` 由 API 直接下发（Admin 服务→API），不经 BFF |

> 说明：幂等键由前端生成（Taro `genPublishIdempotencyKey`，页面会话内稳定复用、发布成功后轮换），BFF 只透传不生成；同一 key 的重放由上游返回原单（不重复建单/扣分）。
> 配套文档：《API 端点说明 v1.56.0》《寻货功能设计 v1.8.2》《移动端登录与商户入驻设计 v1.7.40》。
