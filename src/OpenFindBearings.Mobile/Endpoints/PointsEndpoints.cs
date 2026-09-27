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
            return Results.Ok(result ?? new PointAccountResponse(0, 0, 0, false, 0, null, 1, null));
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

        /// <summary>
        /// 赚分任务清单（v1.7.7 任务中心：规则+完成态）
        /// </summary>
        group.MapGet("/tasks", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<List<PointTaskItem>>("/api/points/tasks", token, ct);
            return Results.Ok(result ?? new List<PointTaskItem>());
        })
        .WithName("GetPointTasks")
        .WithSummary("赚分任务清单")
        .WithDescription("任务中心数据源：启用规则与本人完成态")
        .RequireAuthorization();

        /// <summary>
        /// 商家福利卡（v2.5.0 商家经济）：最佳商家等级/福利清单/升级提示，散人为空；
        /// 改动说明（v2.6.0 任务中心拆分）：merchantId 可选透传（商家管理页"本店视角"）
        /// </summary>
        group.MapGet("/merchant-buff", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct,
            Guid? merchantId) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var path = merchantId.HasValue ? $"/api/points/merchant-buff?merchantId={merchantId}" : "/api/points/merchant-buff";
            var result = await api.GetAsync<MerchantBuffResponse>(path, token, ct);
            return Results.Ok(result ?? new MerchantBuffResponse(null, null, 0, 0, new List<string>(), ""));
        })
        .WithName("GetMerchantBuff")
        .WithSummary("商家福利卡")
        .RequireAuthorization();

        /// <summary>
        /// 商家集体任务板（v2.6.0 M3）：周期任务进度与完成态（透传，散人为空清单）；
        /// 改动说明（v2.6.0 商家主页）：merchantId 可选透传——商家主页成员区按所属商家查询，
        /// 缺省仍走 API 最佳商户口径（任务中心卡）
        /// </summary>
        group.MapGet("/merchant-tasks", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct,
            Guid? merchantId) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var path = merchantId.HasValue ? $"/api/points/merchant-tasks?merchantId={merchantId}" : "/api/points/merchant-tasks";
            var result = await api.GetAsync<MerchantTasksResponse>(path, token, ct);
            return Results.Ok(result ?? new MerchantTasksResponse(null, null, new List<MerchantTaskItem>(), 0));
        })
        .WithName("GetMerchantTasks")
        .WithSummary("商家集体任务板")
        .RequireAuthorization();

        /// <summary>
        /// 商家实力月榜（v2.6.0 M3）：TOP 榜 + 我的商家回显（透传）；
        /// 改动说明（v2.6.0 任务中心拆分）：merchantId 可选透传（商家管理页"本店名次"视角）
        /// </summary>
        group.MapGet("/merchant-ranking", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct,
            Guid? merchantId) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var path = merchantId.HasValue ? $"/api/points/merchant-ranking?merchantId={merchantId}" : "/api/points/merchant-ranking";
            var result = await api.GetAsync<MerchantRankingResponse>(path, token, ct);
            return Results.Ok(result ?? new MerchantRankingResponse("", new List<MerchantRankItem>(), null));
        })
        .WithName("GetMerchantRanking")
        .WithSummary("商家实力月榜")
        .RequireAuthorization();
    }

    private static string? GetToken(HttpContext http) =>
        http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");

    /// <summary>账户概览响应（对齐 API /api/points/account）</summary>
    // 时间治理批次：透传 API 下发的业务日界偏移（可空=旧 API 无此字段，前端按 +8 兜底），
    //   防管理员调整 BusinessClock 配置后前端硬编码 +8 与后端日界漂移
    // v2.7.0 G7：透传用户积分等级（level 数字 + levelName 名称）
    public record PointAccountResponse(int Balance, int TotalEarned, int TotalSpent, bool TodayCheckedIn, int ConsecutiveDays, int? TzOffsetHours, int Level = 1, string? LevelName = null);

    /// <summary>签到结果（对齐 API CheckinResult）</summary>
    // v2.1.0 成就子系统：透传本次签到新点亮的成就键（供 Taro toast）
    public record CheckinResponse(int Amount, int ConsecutiveDays, bool AlreadyCheckedIn, string[]? UnlockedAchievements = null);

    /// <summary>流水项（对齐 API /api/points/transactions items）</summary>
    public record PointTransactionItem(
        Guid Id,
        int Direction,
        string GrantType,
        int Amount,
        int BalanceAfter,
        string? Remark,
        DateTime CreatedAt);

    /// <summary>任务项（对齐 API /api/points/tasks；daily=每日刷新任务，done 按今日/历史口径）</summary>
    public record PointTaskItem(
        string GrantType,
        string DisplayName,
        int Amount,
        string? Description,
        List<int>? Ladder,
        bool Daily,
        bool Done);

    /// <summary>商家福利卡（透传 API /api/points/merchant-buff，v2.5.0）</summary>
    public record MerchantBuffResponse(Guid? MerchantId, string? MerchantName, int Grade, int Rank, List<string> Labels, string NextHint);

    /// <summary>集体任务板响应（透传 API /api/points/merchant-tasks，v2.6.0）</summary>
    public record MerchantTasksResponse(Guid? MerchantId, string? MerchantName, List<MerchantTaskItem> Tasks, int CompletedTotal);

    /// <summary>集体任务项（period 1 周/2 月；rewardType 1 成员/2 金库；done=本周期已达成）</summary>
    public record MerchantTaskItem(string TaskKey, string Name, string Description,
        int Target, int Current, int Period, int RewardType, int RewardAmount, bool Done);

    /// <summary>商家实力月榜响应（透传 API /api/points/merchant-ranking，v2.6.0）</summary>
    public record MerchantRankingResponse(string PeriodKey, List<MerchantRankItem> Top, MerchantRankItem? Mine);

    /// <summary>榜单行（rank=0 表示未进前 100 单独回显）</summary>
    public record MerchantRankItem(int Rank, Guid MerchantId, string MerchantName, string GradeDisplay, int Total);
}
