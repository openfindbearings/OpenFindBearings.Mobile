using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 积分代理端点（/mobile/points/*，v1.7.6 积分底座）
/// 代理 API /api/points/*（账户概览/每日签到/流水），供 Taro 我的页积分卡、签到按钮、明细页消费。
/// 统一模式：从入站 Authorization 头取用户 access token 透传；缺 token 一律 401
/// </summary>
public static class PointsEndpoints
{
    /// <summary>
    /// 注册积分代理路由（挂在 /mobile/points 组下）
    /// </summary>
    public static void MapPointsEndpoints(this RouteGroupBuilder group)
    {
        /// <summary>
        /// 积分账户概览（余额/累计/今日签到状态/连续天数）
        /// </summary>
        group.MapGet("/account", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<PointAccountResponse>("/api/points/account", token, ct);
            return Results.Ok(result ?? new PointAccountResponse(0, 0, 0, false, 0));
        })
        .WithName("GetPointAccount")
        .WithSummary("积分账户概览")
        .WithDescription("当前用户积分余额、累计与今日签到状态")
        .RequireAuthorization();

        /// <summary>
        /// 每日签到（透传阶梯结果：本次分值/连续天数/是否已签）
        /// </summary>
        group.MapPost("/checkin", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.PostAsync<CheckinResponse>("/api/points/checkin", new { }, token, ct);
            return result == null
                ? Results.Json(new { success = false, message = "签到失败，请稍后重试" }, statusCode: 502)
                : Results.Ok(result);
        })
        .WithName("DailyCheckin")
        .WithSummary("每日签到")
        .WithDescription("主动签到得阶梯积分，同日重复返回已签状态")
        .RequireAuthorization();

        /// <summary>
        /// 积分流水分页（明细页）
        /// </summary>
        group.MapGet("/transactions", async (
            int page,
            int pageSize,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetPagedAsync<PointTransactionItem>(
                $"/api/points/transactions?page={page}&pageSize={pageSize}", token, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<PointTransactionItem>([], 0, 1, 20));
        })
        .WithName("GetPointTransactions")
        .WithSummary("积分流水")
        .WithDescription("当前用户积分收支明细分页")
        .RequireAuthorization();
    }

    private static string? GetToken(HttpContext http) =>
        http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");

    /// <summary>账户概览响应（对齐 API /api/points/account）</summary>
    public record PointAccountResponse(int Balance, int TotalEarned, int TotalSpent, bool TodayCheckedIn, int ConsecutiveDays);

    /// <summary>签到结果（对齐 API CheckinResult）</summary>
    public record CheckinResponse(int Amount, int ConsecutiveDays, bool AlreadyCheckedIn);

    /// <summary>流水项（对齐 API /api/points/transactions items）</summary>
    public record PointTransactionItem(
        Guid Id,
        int Direction,
        string GrantType,
        int Amount,
        int BalanceAfter,
        string? Remark,
        DateTime CreatedAt);
}
