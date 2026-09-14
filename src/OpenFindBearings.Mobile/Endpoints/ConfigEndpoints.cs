using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 站点配置与版本检查端点
/// 代理 API 的 /api/mobile/config 与 /api/mobile/version/check，供 Taro 前端匿名访问。
/// 改动说明：此前 BFF 未注册 /mobile/config，Taro getSiteConfig 一直 404；
/// 版本更新功能上线时一并补齐两条匿名代理路由
/// </summary>
public static class ConfigEndpoints
{
    public static void MapConfigEndpoints(this RouteGroupBuilder group)
    {
        /// <summary>
        /// 站点配置（站名/备案/客服 + 版本相关字段）
        /// </summary>
        group.MapGet("/config", async (
            ApiClient api,
            CancellationToken ct) =>
        {
            var config = await api.GetAsync<MobileConfigDto>("/api/mobile/config", ct);
            return config is null
                ? Results.Problem("站点配置获取失败", statusCode: 502)
                : Results.Ok(config);
        })
        .WithName("GetSiteConfig")
        .WithSummary("站点配置")
        .AllowAnonymous();

        /// <summary>
        /// 检查版本更新：透传客户端当前版本与平台，返回是否有新版及下载地址
        /// </summary>
        group.MapGet("/version/check", async (
            ApiClient api,
            string currentVersion,
            string platform,
            CancellationToken ct) =>
        {
            // 改动说明：版本串含 "+"（build 元数据）等 query 保留字符，必须转义后再拼接
            var path = $"/api/mobile/version/check?currentVersion={Uri.EscapeDataString(currentVersion)}"
                + $"&platform={Uri.EscapeDataString(platform)}";
            var result = await api.GetAsync<VersionCheckDto>(path, ct);
            return result is null
                ? Results.Problem("版本检查失败", statusCode: 502)
                : Results.Ok(result);
        })
        .WithName("CheckVersion")
        .WithSummary("检查版本更新")
        .AllowAnonymous();
    }

    // ============ DTO 定义（与 API 侧 JSON 字段对齐，反序列化大小写不敏感） ============

    /// <summary>移动端站点配置（与 API MobileConfigDto 对应的展示子集）</summary>
    public record MobileConfigDto(
        string SiteName,
        string SiteDescription,
        string SiteBeiAn,
        string CustomerService,
        string AppVersion,
        string DownloadUrl,
        bool ForceUpdate);

    /// <summary>版本检查结果（与 API VersionCheckResult 对应）</summary>
    public record VersionCheckDto(
        bool HasUpdate,
        string LatestVersion,
        bool IsForceUpdate,
        string? UpdateMessage,
        string? DownloadUrl);
}
