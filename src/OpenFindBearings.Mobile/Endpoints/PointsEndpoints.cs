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
            // 改动说明：分页改可空——非空值类型会被最小 API 判为必填查询参数，缺省即 400 空体
            int? page,
            int? pageSize,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            // 改动说明：缺省分页按第 1 页、20 条兜底
            var result = await api.GetPagedAsync<PointTransactionItem>(
                $"/api/points/transactions?page={page ?? 1}&pageSize={pageSize ?? 20}", token, ct);
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
        /// 段位阶梯表（v2.13.0 段位详情页）：全档位+本人落档与升档礼已领标记（透传）
        /// </summary>
        group.MapGet("/levels", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<PointLadderResponse>("/api/points/levels", token, ct);
            return Results.Ok(result ?? new PointLadderResponse(0, 1, "倔强青铜", new List<PointLadderItem>()));
        })
        .WithName("GetMyPointLevels")
        .WithSummary("段位阶梯表")
        .RequireAuthorization();

        /// <summary>
        /// 商家等级详情（v2.13.0 商家等级页）：双指标进度/四档阶梯/升档礼已领/保级钟（透传，API 校验在职成员）
        /// </summary>
        group.MapGet("/merchant-grade", async (
            Guid merchantId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<MerchantGradeResponse>(
                $"/api/points/merchant-grade?merchantId={merchantId}", token, ct);
            return result == null
                ? Results.Json(new { success = false, message = "等级详情获取失败，请稍后重试" }, statusCode: 502)
                : Results.Ok(result);
        })
        .WithName("GetMerchantGrade")
        .WithSummary("商家等级详情")
        .RequireAuthorization();

        /// <summary>
        /// 商家牌级规则（v2.19.0 有效期制）：公开只读，规则页与未登录访客可用；
        /// 阈值/有效期/保级天数全部由 API 从 SystemConfig 下发，前端不硬编码
        /// </summary>
        group.MapGet("/merchant-grade/rules", async (ApiClient api, CancellationToken ct) =>
        {
            var result = await api.GetAsync<MerchantGradeRulesResponse>("/api/points/merchant-grade/rules", ct);
            return result == null
                ? Results.Json(new { success = false, message = "等级规则获取失败，请稍后重试" }, statusCode: 502)
                : Results.Ok(result);
        })
        .WithName("GetMerchantGradeRules")
        .WithSummary("商家牌级规则")
        .WithDescription("公开规则页数据源：有效期、保级天数、在售门槛、成交阈值与四档升档礼")
        .AllowAnonymous();

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

        /// <summary>
        /// 轴承币与商家金规则数据源（v2.17.0 规则数据源）：匿名代理 API /api/points/rules，
        /// 规则页渲染用；运营改配置后前端自动跟随，消除页面硬编码数值漂移
        /// </summary>
        group.MapGet("/rules", async (
            ApiClient api,
            CancellationToken ct) =>
        {
            var result = await api.GetAsync<PointRulesResponse>("/api/points/rules", ct);
            return result is null
                ? Results.Json(new { success = false, message = "规则数据获取失败" }, statusCode: 502)
                : Results.Ok(result);
        })
        .WithName("GetPointRules")
        .WithSummary("轴承币与商家金规则")
        .WithDescription("规则页数据源：轴承币/商家金各项数值，匿名访问")
        .AllowAnonymous();
    }

    // 改动说明（v2.13.2 令牌提取统一）：委托 BffProxy.Token（含 claim 校验）
    private static string? GetToken(HttpContext http) => BffProxy.Token(http);

    /// <summary>账户概览响应（对齐 API /api/points/account）</summary>
    // 时间治理批次：透传 API 下发的业务日界偏移（可空=旧 API 无此字段，前端按 +8 兜底），
    //   防管理员调整 BusinessClock 配置后前端硬编码 +8 与后端日界漂移
    // v2.7.0 G7：透传用户积分等级（level 数字 + levelName 名称）
    // v2.12.0 等级玩法：透传下一档三字段（进度条"距升 X 还差 Y 币"数据源；已达最高档为 null）
    public record PointAccountResponse(int Balance, int TotalEarned, int TotalSpent, bool TodayCheckedIn, int ConsecutiveDays, int? TzOffsetHours, int Level = 1, string? LevelName = null, int? NextLevelMin = null, string? NextLevelName = null, int? NextLevelBonus = null);

    /// <summary>签到结果（对齐 API CheckinResult）</summary>
    // v2.1.0 成就子系统：透传本次签到新点亮的成就键（供 Taro toast）；v2.8.0 G1 暴击倍数
    // v2.12.0 等级玩法：透传签到后最终段位与是否跨档（leveledUp=true 前端播升级 toast）
    public record CheckinResponse(int Amount, int ConsecutiveDays, bool AlreadyCheckedIn, string[]? UnlockedAchievements = null, int CritMultiplier = 1, int? Level = null, string? LevelName = null, bool LeveledUp = false);

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
        bool Done,
        // 改动说明（v2.13.2 字段缺失修复）：补 count/limit（API 早已下发，BFF 漏声明致任务中心"今日 n/上限"恒空）
        int Count = 0,
        int Limit = 0);

    /// <summary>段位阶梯表（透传 API /api/points/levels，v2.13.0 段位详情页）</summary>
    public record PointLadderResponse(int TotalEarned, int CurrentLevel, string CurrentLevelName, List<PointLadderItem> Levels);

    /// <summary>段位档（reached=已达档；bonusClaimed=该档升档礼终身已领）</summary>
    public record PointLadderItem(int Level, string Name, int MinTotalEarned, int LevelUpBonus, bool Reached, bool BonusClaimed);

    /// <summary>商家牌级详情（透传 API /api/points/merchant-grade，v2.13.0 商家等级页）</summary>
    /// 改动说明（v1.57.1 修 bug）：补 dealCount/windowDays/onSaleMin/silverDealMin/goldDealMin/nextTier，
    ///   并删除随金库退出定级而失效的 TreasuryEarned/Lv3OnSaleMin/Lv4OnSaleMin/Lv4TreasuryMin 死字段——
    ///   原契约缺字段致 Taro 卡级页把阈值渲染成 undefined
    /// 改动说明（v1.8.8 有效期制）：windowDays 改为 periodDays，新增有效期起止、本期成交要求与到期降档预告
    public record MerchantGradeResponse(Guid MerchantId, string? MerchantName, int Grade, int Rank, string GradeDisplay,
        bool IsVerified, int OnSaleCount, int DealCount, int RequiredDeals, int PeriodDays, int GraceDays,
        DateTime? PeriodStartAt, DateTime? PeriodEndAt,
        int OnSaleMin, int SilverDealMin, int GoldDealMin,
        int BonusLv2, int BonusLv3, int BonusLv4, List<int> ClaimedRanks,
        MerchantGradeNextTier? NextTier, int? DowngradeTo,
        DateTime? GraceUntil, List<string> Labels);

    /// <summary>卡级"距下一档"（API nextTier；金牌为 null）</summary>
    public record MerchantGradeNextTier(int Grade, string Label, int RequiredOnSale, int RequiredDeals,
        bool MetOnSale, bool MetDeals);

    /// <summary>商家牌级规则（透传 API /api/points/merchant-grade/rules，v2.19.0 规则页；匿名可读）</summary>
    public record MerchantGradeRulesResponse(int PeriodDays, int GraceDays, int OnSaleMin,
        int SilverDealMin, int GoldDealMin, List<MerchantGradeRuleTier> Tiers);

    /// <summary>规则阶梯行（rank 1 普通 / 2 铜牌 / 3 银牌 / 4 金牌）</summary>
    public record MerchantGradeRuleTier(int Rank, string Name, int RequiredOnSale, int RequiredDeals,
        int UpBonus, string Note);

    /// <summary>商家福利卡（透传 API /api/points/merchant-buff，v2.5.0）</summary>
    // v2.12.0 等级玩法：透传下一档升档礼金额（gradeUpBonus，商家金终身一次）与本店保级截止钟
    //（graceUntil 仅 merchantId 本店视角返回，掉级倒计时卡数据源）
    public record MerchantBuffResponse(Guid? MerchantId, string? MerchantName, int Grade, int Rank, List<string> Labels, string NextHint, int? GradeUpBonus = null, DateTime? GraceUntil = null);

    /// <summary>集体任务板响应（透传 API /api/points/merchant-tasks，v2.6.0）</summary>
    public record MerchantTasksResponse(Guid? MerchantId, string? MerchantName, List<MerchantTaskItem> Tasks, int CompletedTotal);

    /// <summary>集体任务项（period 1 周/2 月；rewardType 1 成员/2 金库；done=本周期已达成）</summary>
    public record MerchantTaskItem(string TaskKey, string Name, string Description,
        int Target, int Current, int Period, int RewardType, int RewardAmount, bool Done);

    /// <summary>商家实力月榜响应（透传 API /api/points/merchant-ranking，v2.6.0）</summary>
    public record MerchantRankingResponse(string PeriodKey, List<MerchantRankItem> Top, MerchantRankItem? Mine);

    /// <summary>榜单行（rank=0 表示未进前 100 单独回显）</summary>
    public record MerchantRankItem(int Rank, Guid MerchantId, string MerchantName, string GradeDisplay, int Total);

    /// <summary>规则数据响应（透传 API /api/points/rules，v2.17.0 规则数据源；
    /// 字段与 API PointRulesDto 对齐，字段缺失由前端各自兜底默认）</summary>
    public record PointRulesResponse(
        int[] CheckinLadder, int CheckinDoubleChance, int CheckinLegendChance,
        int LoginAmount, int CorrectionAmount, int CorrectionDailyLimit, int ComboAmount,
        int MinigameAmount, int MinigameDailyLimit,
        int MilestoneProfile, int MilestoneFirstProduct, int MilestoneApproved,
        int SourcingBonusAmount, int SourcingPublishDailyLimit, int SourcingRespondDailyLimit,
        int FreePublishPerDay, int FreeRespondPerDay,
        int LevelBonusMin, int LevelBonusMax, int TzOffsetHours,
        int BuffCheckinMax, int BuffLogin, double BuffCorrectionMax,
        int TricklePercent, int TrickleDailyCap, int TrickleMonthlyCap, int GiftSettleMonthlyCap,
        int MerchantGradeBonusLv2, int MerchantGradeBonusLv3, int MerchantGradeBonusLv4,
        int MerchantGradeGraceDays, int MerchantGoldPayRate,
        int MerchantTaskWeeklyTreasury, int MerchantTaskMemberMin, int MerchantTaskMemberMax);
}
