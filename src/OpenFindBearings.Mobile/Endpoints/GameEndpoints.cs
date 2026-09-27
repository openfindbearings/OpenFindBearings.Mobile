using System.Text.Json.Nodes;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 游戏中心通用代理（v2.10.1 方案 A）：/mobile/games/{key}/board|result 原样透传主 API 插件端点。
/// 通用转发设计——将来新增游戏（数独/2048）或游戏拆成独立微服务，BFF 这层零改动。
/// </summary>
public static class GameEndpoints
{
    /// <summary>
    /// 映射游戏代理端点（挂到 /mobile/games 组下；未知游戏键由主 API 返回 404）
    /// </summary>
    public static void MapGameEndpoints(this RouteGroupBuilder group)
    {

        /// <summary>出题板（透传 GET /api/games/{key}/board，size 可选规模参数）</summary>
        group.MapGet("/{key}/board", async (
            string key,
            ApiClient api,
            HttpContext http,
            CancellationToken ct,
            int? size) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var path = size.HasValue
                ? $"/api/games/{key}/board?size={size.Value}"
                : $"/api/games/{key}/board";
            var result = await api.GetAsync<JsonNode>(path, token, ct);
            return Results.Ok(result);
        })
        .WithName("GetGameBoardProxy")
        .WithSummary("游戏出题板代理");

        /// <summary>结算发分（透传 POST /api/games/{key}/result，payload 原样转发）</summary>
        group.MapPost("/{key}/result", async (
            string key,
            ApiClient api,
            HttpContext http,
            CancellationToken ct,
            JsonNode payload) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.PostAsync<JsonNode>(
                $"/api/games/{key}/result", payload, token, ct);
            return Results.Ok(result);
        })
        .WithName("ReportGameResultProxy")
        .WithSummary("游戏结算代理");
    }

    private static string? GetToken(HttpContext http) =>
        http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
}
