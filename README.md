# OpenFindBearings.Mobile

移动端 BFF（Backend-for-Frontend），为 Taro H5/小程序/未来 App 提供统一的后台服务接口。聚合后端 API 调用、处理移动端认证、提供静态文件托管。

## 技术栈

- ASP.NET Core Minimal API（.NET 10.0）
- JWT Bearer 认证（通过 Identity）
- 静态文件托管（Taro H5 构建产物）

## 核心功能

- **首页聚合**：一次请求返回热门轴承、推荐商家、品牌列表、类型列表
- **轴承代理**：搜索、详情、在售商家、替代品
- **商家代理**：搜索、详情、在售商品、入驻申请
- **认证代理**：密码登录、短信登录、验证码发送、令牌刷新
- **用户资料**：聚合 Identity 用户信息 + API 业务数据
- **静态文件**：托管 Taro H5 构建产物，单域名提供前后端

## 架构

```
Taro H5/小程序/未来App → Mobile.Bff (聚合/缓存/鉴权) → API + Identity
```

- API 和 Identity 仅通过 K8s 内部 Service 访问，不暴露公网
- BFF 是唯一面向移动端的公网入口

## 构建与运行

```bash
cd OpenFindBearings.Mobile
dotnet restore src/OpenFindBearings.Mobile
dotnet run --project src/OpenFindBearings.Mobile
```

默认端口 `http://localhost:8080`。

## 部署

```bash
kubectl apply -f deploy/k3s/
```

- 域名：`mobile.515813.xyz`
- 后端 API：`http://openfindbearings-api:80`（K8s 内部）
- Identity：`http://openfindbearings-identity:80`（K8s 内部）
