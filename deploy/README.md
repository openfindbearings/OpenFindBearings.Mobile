# deploy

本服务的集群部署清单统一收拢在私有仓库 **FindBearings.Infra**：

- K3s 清单：`apps/<服务名>/`
- 密钥模板：`secrets/templates/`（真实值在 `secrets/real/`，不入库）
- 部署手册：`runbooks/`

本仓库只保留代码、Dockerfile 与镜像构建/推送 CI（`.github/workflows/deploy.yml` 经 `kubectl set image` 滚动更新，不读本目录清单）。