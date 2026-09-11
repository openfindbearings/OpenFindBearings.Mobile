# Mobile BFF API 端点说明 v1.2.0

## 概述

OpenFindBearings.Mobile 作为移动端 BFF，所有端点统一挂载在 `/mobile` 前缀下。BFF 代理后端 API（`http://openfindbearings-api:80`）和 Identity（`http://openfindbearings-identity:80`）的调用，前端无需直接访问这两个服务。

路由按资源分子组注册（Program.cs）：`/mobile/bearings/*`、`/mobile/merchants/*`、`/mobile/me/*`、`/mobile/media/*`、`/mobile/auth/*`；`/mobile/home` 与 `/mobile/profile` 挂在 `/mobile` 根下。

## 变更日志

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.2.0 | 2026-09-11 | 代码审查对齐：① 补充 `/me` 端点组（收藏/关注/历史/资料编辑/头像上传，18 个端点）；② 补充 `/media` 媒体代理；③ 补充认证端点 register/logout；④ 补充手机号格式校验；⑤ 轴承搜索补充 8 个缺失参数（BrandId/BearingTypeId/SortBy/SortOrder/6 维度范围）；⑥ 商家搜索补充 SortBy/SortOrder；⑦ 修正 favorites/followed DTO 为嵌套结构（含 CreatedAt）；⑧ 修正端点计数；⑨ 修正 PagedResult 无 totalPages 字段 |
| v1.1.0 | 2026-09-08 | 路由按资源子组注册；BFF→API 附带 X-Internal-Token；用户资料路径修正；商家 DTO 字段对齐 |
| v1.0.0 | 2026-09-01 | 初始版本 |

## 端点总览

| 分组 | 端点数 | 认证要求 |
|------|--------|----------|
| 首页聚合 | 1 | 无需登录 |
| 轴承 | 5 | 无需登录 |
| 商家 | 5 | 公开端点无需登录，入驻申请需登录 |
| 用户资料（读） | 3 | 需登录（Bearer JWT） |
| 个人中心（写） | 18 | 需登录（Bearer JWT） |
| 媒体代理 | 1 | 无需登录 |
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

**响应**：数组，每项包含 `id`、`partNumber`、`brandName`、`bearingType`、`confidence`（可信度）。

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

## 媒体代理（匿名）

### GET /mobile/media/{**path}

图片流式代理，转发 API 静态文件（images/uploads/avatars）。白名单路径前缀：`images/`、`uploads/`、`avatars/`。

BFF 将 API 内部地址的相对路径转为公网绝对 URL（`bff.515813.xyz/mobile/media/...`），前端统一从 BFF 拉取图片。

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
| `/mobile/media/{path}` | GET | 无 | 无 | 媒体代理 |
| `/mobile/auth/register` | POST | 无 | 10/min/IP | 注册 |
| `/mobile/auth/login` | POST | 无 | 10/min/IP | 密码登录 |
| `/mobile/auth/login-sms` | POST | 无 | 10/min/IP | 短信登录 |
| `/mobile/auth/send-code` | POST | 无 | 10/min/IP | 发送验证码 |
| `/mobile/auth/refresh` | POST | 无 | 10/min/IP | 刷新令牌 |
| `/mobile/auth/logout` | POST | 无 | 10/min/IP | 登出 |

## BFF→API 内部令牌

BFF 调用 API 时统一附带请求头 `X-Internal-Token`（值取自配置 `Internal:ApiToken`），API 限流中间件识别匹配后豁免按 IP 的用户级限流。原因：API 无公网 ingress，全部 App 流量经单一 BFF Pod IP 转发，若按 IP 限流会误伤聚合流量（guest 30/min 触发 429 → 前端空结果）。令牌经 K3s ConfigMap 注入 API 与 BFF 两侧，必须同值。详见《缓存与限流策略 v1.1.0》。
