using System.Text.Json.Serialization;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>成就墙视图项（BFF 透传 API AchievementProgressView）</summary>
public record AchievementItemResponse(
    string Key, string Name, string Description, string Icon, string Category,
    int Scope, int Target, int Progress, bool Unlocked, DateTime? UnlockedAt,
    bool Rare, bool Hidden, int MetaPoints, string? TitleReward, string? ImageKey);

/// <summary>成就墙视图（BFF 透传 API AchievementWallView）</summary>
public record AchievementWallResponse(
    List<AchievementItemResponse> Items,
    [property: JsonPropertyName("totalMetaPoints")] int TotalMetaPoints,
    [property: JsonPropertyName("currentTitle")] string? CurrentTitle,
    [property: JsonPropertyName("unlockedCount")] int UnlockedCount);

/// <summary>
/// 成就代理端点（/mobile/achievements/*，v2.1.0 成就子系统）
/// 代理 API /api/achievements/wall、/api/me/achievements、/api/merchants/{id}/achievements，
/// 供 Taro 成就墙页、个人资料徽章排、商户详情徽章排消费
/// </summary>
public static class AchievementEndpoints
{
    /// <summary>
    /// 注册成就代理路由（挂在 /mobile/achievements 组下）
    /// </summary>
    public static void MapAchievementEndpoints(this RouteGroupBuilder group)
    {
        /// <summary>个人成就墙（全目录+本人进度+成就点+称号）</summary>
        group.MapGet("/wall", async (ApiClient api, HttpContext http, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var result = await api.GetAsync<AchievementWallResponse>("/api/achievements/wall", token, ct);
            return result == null
                ? Results.Json(new { success = false, message = "成就墙加载失败" }, statusCode: 502)
                : Results.Ok(result);
        })
        .WithName("GetAchievementWall")
        .WithSummary("个人成就墙")
        .RequireAuthorization();

        /// <summary>我的已解锁徽章排（资料页徽章条）</summary>
        group.MapGet("/me", async (ApiClient api, HttpContext http, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var result = await api.GetAsync<AchievementWallResponse>("/api/me/achievements", token, ct);
            return result == null
                ? Results.Json(new { success = false, message = "徽章加载失败" }, statusCode: 502)
                : Results.Ok(result);
        })
        .WithName("GetMyAchievements")
        .WithSummary("我的已解锁徽章")
        .RequireAuthorization();

        /// <summary>商户徽章排（商户详情/卡片信任信号，可未登录浏览）</summary>
        group.MapGet("/merchants/{id:guid}", async (Guid id, ApiClient api, HttpContext http, CancellationToken ct) =>
        {
            var token = GetToken(http); // 可空：未登录也允许看商户徽章
            var result = await api.GetAsync<AchievementWallResponse>($"/api/merchants/{id}/achievements", token, ct);
            return result == null
                ? Results.Json(new { success = false, message = "商户徽章加载失败" }, statusCode: 502)
                : Results.Ok(result);
        })
        .WithName("GetMerchantAchievements")
        .WithSummary("商户徽章排");
    }

    /// <summary>从入站 Authorization 头取用户 access token（缺省 null）</summary>
    private static string? GetToken(HttpContext http)
    {
        var auth = http.Request.Headers.Authorization.ToString();
        return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth["Bearer ".Length..] : null;
    }
}
