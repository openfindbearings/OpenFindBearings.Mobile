# deploy（OpenFindBearings.Mobile BFF 部署模板）

本目录是移动端 BFF 的 K8s 部署清单模板。部署时请将占位符替换为真实域名。

## 步骤

1. **创建 Secret**：`secrets/mobile-secret-template.yml`（Identity ClientSecret + 与 API 一致的内部令牌）后 apply
2. **替换占位符**：`<your-bff-domain>` → 你的 BFF 域名（deploy.yml Ingress，TLS 由 cert-manager 签发）
3. **ConfigMap**：`configmap.yml`（含依赖服务名，如服务名不同需调整）
4. **依赖**：API、Identity；镜像 `ghcr.io/openfindbearings/openfindbearings-mobile`（公开）

## apply

```
secrets → configmap.yml → deploy.yml
```

> 完整运维清单（真实域名/密钥）在私有运维库，本目录只提供模板，占位符请在部署时替换为真实值。
