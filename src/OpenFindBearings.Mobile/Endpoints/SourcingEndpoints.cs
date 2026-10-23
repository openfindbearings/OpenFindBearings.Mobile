using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 寻货代理端点（/mobile/sourcing/*，v1.7.8）：代理 API /api/sourcing/*。
/// 浏览类（feed/详情）匿名可访问、带 token 则透传（isMine/我的应答判定）；
/// 写操作强制登录透传；发布/应答用 PostWithResult 透传"NEED_POINTS:X"积分确认文案
/// </summary>
public static class SourcingEndpoints
{
    /// <summary>
    /// 注册寻货代理路由（挂在 /mobile/sourcing 组下）
    /// </summary>
    public static void MapSourcingEndpoints(this RouteGroupBuilder group)
    {
        /// <summary>
        /// 寻货 feed（公开浏览；keyword 型号搜索、brand/region 大厅筛选、sort 发布时间升降序、onlyOpen 过滤、分页）
        /// </summary>
        group.MapGet("/demands", async (
            string? keyword,
            // 改动说明：onlyOpen 与分页改可空——非空值类型会被最小 API 判为必填查询参数，
            //   缺省直接 400 空体；onlyOpen 缺省按 true（只列开放中的寻货）兜底
            bool? onlyOpen,
            string? brand,
            string? region,
            string? sort,
            int? page,
            int? pageSize,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            // 改动说明：可空分页先落本地变量再校正，缺省按第 1 页、20 条，上限 50 条
            var p = page ?? 1;
            if (p <= 0) p = 1;
            var ps = pageSize ?? 20;
            if (ps <= 0 || ps > 50) ps = 20;
            // 改动说明：v1.12.0 消息架构调整后删除 mineOnly 透传，列表页回纯公开流；
            //   这里是"我的寻货"（/my/demands）独立端点，保留 brand/region/sort 筛选透传
            var qs = $"?keyword={Uri.EscapeDataString(keyword ?? "")}&onlyOpen={onlyOpen ?? true}" +
                $"&brand={Uri.EscapeDataString(brand ?? "")}&region={Uri.EscapeDataString(region ?? "")}" +
                $"&sort={Uri.EscapeDataString(sort ?? "desc")}" +
                $"&page={p}&pageSize={ps}";
            var result = await api.GetAsync<SourcingFeedResponse>($"/api/sourcing/demands{qs}", token, ct);
            return Results.Ok(result ?? new SourcingFeedResponse(Array.Empty<SourcingFeedItem>(), 0));
        })
        .WithName("GetSourcingFeed")
        .WithSummary("寻货列表")
        .WithDescription("寻货 feed（型号关键词 + brand/region 筛选 + sort 升降序 + 分页）");

        /// <summary>
        /// 寻货详情（公开浏览；登录带当前商户时返回 myResponse，选定后返回解锁联系方式）
        /// </summary>
        group.MapGet("/demands/{id}", async (
            string id,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            var result = await api.GetAsync<SourcingDetailResponse>($"/api/sourcing/demands/{id}", token, ct);
            return result == null
                ? Results.Json(new { success = false, message = "寻货不存在" }, statusCode: 404)
                : Results.Ok(result);
        })
        .WithName("GetSourcingDetail")
        .WithSummary("寻货详情")
        .WithDescription("需求全文+分级应答视图");

        /// <summary>
        /// 发布寻货（登录；免费额度内直接成功，超限未确认返回 NEED_POINTS 文案）
        /// </summary>
        group.MapPost("/demands", async (
            SourcingPublishRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            // 改动说明（v1.8.4 发布幂等）：透传客户端 Idempotency-Key（缺省则原样不传）——
            //   上游按"同发布人同 key"去重，网络重试/双击不会重复建单或重复扣分
            var idemKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            var headers = string.IsNullOrWhiteSpace(idemKey)
                ? null
                : new Dictionary<string, string> { ["Idempotency-Key"] = idemKey! };

            var result = await api.PostWithResultAsync<object>("/api/sourcing/demands", body, token, ct, headers);
            return ProxyResult(result, "发布失败");
        })
        .WithName("PublishSourcingDemand")
        .WithSummary("发布寻货")
        .RequireAuthorization();

        /// <summary>
        /// 转为公开（v1.8.0 公开/邀请）：邀请单进行中时由发布人放开为公开（进大厅），
        /// 免去取消重建的额度损耗
        /// </summary>
        group.MapPost("/demands/{id}/publish-public", async (
            string id,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.PostWithResultAsync<object>($"/api/sourcing/demands/{id}/publish-public", new { }, token, ct);
            return ProxyResult(result, "转为公开失败");
        })
        .WithName("PublishSourcingDemandPublic")
        .WithSummary("转为公开")
        .RequireAuthorization();

        /// <summary>
        /// 应答寻货（登录+当前商户；重复应答=更新）
        /// </summary>
        group.MapPost("/demands/{id}/respond", async (
            string id,
            SourcingRespondRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.PostWithResultAsync<object>($"/api/sourcing/demands/{id}/respond", body, token, ct);
            return ProxyResult(result, "应答失败");
        })
        .WithName("RespondSourcingDemand")
        .WithSummary("应答寻货")
        .RequireAuthorization();

        /// <summary>
        /// 撤销应答（当前商户；仅待处理可撤；撤后需求回到未应答，额度不回退）
        /// </summary>
        group.MapDelete("/demands/{id}/respond", async (
            string id,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.DeleteWithResultAsync<object>($"/api/sourcing/demands/{id}/respond", token, ct);
            return ProxyResult(result, "撤销失败");
        })
        .WithName("CancelSourcingResponse")
        .WithSummary("撤销应答")
        .RequireAuthorization();

        /// <summary>
        /// 选定应答（发布人；选定后解锁商家联系方式——v2.18.0 单向，需求方号码不下发商家）
        /// </summary>
        group.MapPost("/demands/{id}/select", async (
            string id,
            SourcingSelectRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.PostWithResultAsync<object>($"/api/sourcing/demands/{id}/select", body, token, ct);
            return ProxyResult(result, "选定失败");
        })
        .WithName("SelectSourcingResponse")
        .WithSummary("选定应答")
        .RequireAuthorization();

        /// <summary>
        /// 提交/更新成交回执（v2.18.0 撮合成交链）：需求方对被选定合作一键回填洽谈结果，
        /// 返回是否计入商家卡级与未计入原因
        /// </summary>
        group.MapPost("/responses/{id}/feedback", async (
            string id,
            SourcingFeedbackRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.PostWithResultAsync<object>($"/api/sourcing/responses/{id}/feedback", body, token, ct);
            return ProxyResult(result, "回执提交失败");
        })
        .WithName("SubmitSourcingDealFeedback")
        .WithSummary("提交成交回执")
        .RequireAuthorization();

        /// <summary>
        /// 取消寻货（发布人）
        /// </summary>
        group.MapPost("/demands/{id}/cancel", async (
            string id,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.PostWithResultAsync<object>($"/api/sourcing/demands/{id}/cancel", new { }, token, ct);
            return ProxyResult(result, "取消失败");
        })
        .WithName("CancelSourcingDemand")
        .WithSummary("取消寻货")
        .RequireAuthorization();

        /// <summary>
        /// 寻货额度聚合（v1.7.8 额度可见化）：发布/页额度条与按钮三态的数据源，透传 API 口径
        /// </summary>
        group.MapGet("/quota", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<SourcingQuotaResponse>("/api/sourcing/quota", token, ct);
            return Results.Ok(result);
        })
        .WithName("GetSourcingQuota")
        .WithSummary("寻货额度聚合")
        .RequireAuthorization();

        /// <summary>
        /// 我发布的寻货（个人维度列表）
        /// </summary>
        group.MapGet("/my/demands", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<List<SourcingMyDemandItem>>("/api/sourcing/my/demands", token, ct);
            return Results.Ok(result ?? new List<SourcingMyDemandItem>());
        })
        .WithName("GetMySourcingDemands")
        .WithSummary("我发布的寻货")
        .RequireAuthorization();

        /// <summary>
        /// 商户名义发布的寻货列表（v2.12.0 商户工作台"寻货管理-我发布的"，透传 X-Merchant-Id）
        /// </summary>
        group.MapGet("/merchant/demands", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<List<SourcingMerchantDemandItem>>("/api/sourcing/merchant/demands", token, ct);
            return Results.Ok(result ?? new List<SourcingMerchantDemandItem>());
        })
        .WithName("GetMerchantSourcingDemands")
        .WithSummary("商户名义发布的寻货列表")
        .RequireAuthorization();

        /// <summary>
        /// 批量删除寻货需求（v2.12.0 列表删除：软删终态单，左滑单删=ids 传一个）
        /// </summary>
        group.MapPost("/demands/batch-delete", async (
            BatchIdsRequest body, ApiClient api, HttpContext http, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var data = await api.PostAsync<BatchDeleteSourcingResult>("/api/sourcing/demands/batch-delete", new { ids = body.Ids }, token, ct);
            return Results.Ok(new { success = data != null, deleted = data?.Deleted ?? 0, skipped = data?.Skipped ?? 0 });
        })
        .WithName("BatchDeleteSourcingDemands")
        .WithSummary("批量删除寻货")
        .RequireAuthorization();

        /// <summary>
        /// 当前商户的应答记录（商家维度列表）
        /// </summary>
        group.MapGet("/my/responses", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<List<SourcingMerchantResponseItem>>("/api/sourcing/my/responses", token, ct);
            return Results.Ok(result ?? new List<SourcingMerchantResponseItem>());
        })
        .WithName("GetMySourcingResponses")
        .WithSummary("商户寻货应答记录")
        .RequireAuthorization();

        /// <summary>
        /// 我的在售同款（v1.36.0 应答预填）：透传 X-Merchant-Id 当前商户，API 定位在售/补货中条目。
        /// 改动说明（v2.13.2 漏接线修复）：API 早有 /my-offering，BFF 此前漏代理致 RN 端应答预填恒空
        /// </summary>
        group.MapGet("/my-offering", async (
            string? bearingId,
            string? partNumber,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            // 空 bearingId 不拼进上游串（API 的 Guid? 绑定遇空串会 400），缺省即 null 走型号文本匹配
            var qs = $"?partNumber={Uri.EscapeDataString(partNumber ?? "")}";
            if (!string.IsNullOrWhiteSpace(bearingId))
                qs += $"&bearingId={Uri.EscapeDataString(bearingId)}";
            var result = await api.GetAsync<MyOfferingResponse>($"/api/sourcing/my-offering{qs}", token, ct);
            return Results.Ok(result ?? new MyOfferingResponse(false));
        })
        .WithName("GetSourcingMyOffering")
        .WithSummary("我的在售同款（应答预填）")
        .RequireAuthorization();

        /// <summary>
        /// 需求信号（v1.36.0 反向导购）：透传 X-Merchant-Id 当前商户。
        /// 改动说明（v2.13.2 漏接线修复）：API 早有 /opportunities，BFF 此前漏代理致商家横幅恒不显示
        /// </summary>
        group.MapGet("/opportunities", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var result = await api.GetAsync<List<OpportunityItem>>("/api/sourcing/opportunities", token, ct);
            return Results.Ok(result ?? new List<OpportunityItem>());
        })
        .WithName("GetSourcingOpportunities")
        .WithSummary("需求信号（反向导购）")
        .RequireAuthorization();

        /// <summary>
        /// 为你匹配的寻货（v1.6.0 搜索寻货联动）：透传 X-Merchant-Id 当前商户，
        /// 返回"点名邀请我的 + 匹配我在售型号的进行中寻货"，供商户工作台聚合 tab
        /// 改动说明（v1.8.2）："定向给我的"旧措辞统一为"点名邀请我的"（邀请单进收件商家的聚合面是硬承诺）
        /// </summary>
        group.MapGet("/matched", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var result = await api.GetAsync<List<MatchedSourcingItem>>("/api/sourcing/matched", token, ct);
            return Results.Ok(result ?? new List<MatchedSourcingItem>());
        })
        .WithName("GetMatchedSourcingDemands")
        .WithSummary("为你匹配的寻货")
        .WithDescription("点名邀请我的 + 匹配我在售型号的进行中公开单（商家侧聚合面）")
        .RequireAuthorization();

        // ============ 撮合会话代理（v2.18.0 T1 会话） ============

        /// <summary>
        /// 会话列表（需求方与商家方两视角合并，含未读数与单据状态）
        /// </summary>
        group.MapGet("/conversations", async (
            // 改动说明：分页改可空，避免缺省参数被当作必填而返回 400 空体
            int? page,
            int? pageSize,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            // 改动说明：可空分页统一兜底第 1 页、20 条，上限 50 条
            var p = page ?? 1;
            if (p <= 0) p = 1;
            var ps = pageSize ?? 20;
            if (ps <= 0 || ps > 50) ps = 20;
            var qs = $"?page={p}&pageSize={ps}";
            var result = await api.GetAsync<SourcingConversationListResponse>($"/api/sourcing/conversations{qs}", token, ct);
            return Results.Ok(result ?? new SourcingConversationListResponse(new List<SourcingConversationItem>(), 0));
        })
        .WithName("GetSourcingConversations")
        .WithSummary("撮合会话列表")
        .RequireAuthorization();

        /// <summary>
        /// 会话总未读数（首页消息角标数据源）
        /// </summary>
        group.MapGet("/conversations/unread-count", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<UnreadCountResponse>("/api/sourcing/conversations/unread-count", token, ct);
            return Results.Ok(result ?? new UnreadCountResponse(0));
        })
        .WithName("GetSourcingConversationUnreadCount")
        .WithSummary("会话总未读数")
        .RequireAuthorization();

        /// <summary>
        /// 会话消息（时间正序；before 游标翻历史）
        /// </summary>
        group.MapGet("/conversations/{demandId}/{merchantId}/messages", async (
            string demandId,
            string merchantId,
            string? before,
            // 改动说明：limit 改可空——非空 int 会被最小 API 判为必填查询参数，缺省即 400 空体
            int? limit,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            // 改动说明：缺省条数按 30 兜底，上限 50
            var qs = $"?limit={((limit ?? 30) is > 0 and <= 50 ? limit ?? 30 : 30)}";
            if (!string.IsNullOrWhiteSpace(before))
                qs += $"&before={Uri.EscapeDataString(before)}";
            var result = await api.GetAsync<SourcingMessagesResponse>(
                $"/api/sourcing/conversations/{demandId}/{merchantId}/messages{qs}", token, ct);
            return result == null
                ? Results.Json(new { success = false, message = "会话不存在" }, statusCode: 404)
                : Results.Ok(result);
        })
        .WithName("GetSourcingConversationMessages")
        .WithSummary("会话消息")
        .RequireAuthorization();

        /// <summary>
        /// 发送会话消息（会话懒创建；单据结束后拒发）
        /// </summary>
        group.MapPost("/conversations/{demandId}/{merchantId}/messages", async (
            string demandId,
            string merchantId,
            SendSourcingMessageRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.PostWithResultAsync<object>(
                $"/api/sourcing/conversations/{demandId}/{merchantId}/messages", body, token, ct);
            return ProxyResult(result, "发送失败");
        })
        .WithName("SendSourcingMessage")
        .WithSummary("发送会话消息")
        .RequireAuthorization();

        /// <summary>
        /// 标记会话已读
        /// </summary>
        group.MapPost("/conversations/{demandId}/{merchantId}/read", async (
            string demandId,
            string merchantId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.PostWithResultAsync<object>(
                $"/api/sourcing/conversations/{demandId}/{merchantId}/read", new { }, token, ct);
            return ProxyResult(result, "标记已读失败");
        })
        .WithName("MarkSourcingConversationRead")
        .WithSummary("标记会话已读")
        .RequireAuthorization();
    }

    /// <summary>
    /// 透传上游结果：成功原样返回；失败把 API 的 message（含 NEED_POINTS:X 协议文案）透传给前端
    /// </summary>
    private static IResult ProxyResult(ApiClient.ApiCallResult<object> result, string fallbackMessage)
    {
        if (result.Success)
            return Results.Ok(result.Data ?? new { });
        var message = string.IsNullOrWhiteSpace(result.ErrorText) ? fallbackMessage : result.ErrorText;
        return Results.Json(new { success = false, message }, statusCode: result.StatusCode == 0 ? 502 : result.StatusCode);
    }

    /// <summary>从入站 Authorization 头取 Bearer token</summary>
    // 改动说明（v2.13.2 令牌提取统一）：委托 BffProxy.Token（含 claim 校验）
    private static string? GetToken(HttpContext http) => BffProxy.Token(http);
}

/// <summary>feed 单项（对齐 API /api/sourcing/demands items）</summary>
public record SourcingFeedItem(
    Guid Id,
    string PartNumber,
    string? Brand,
    string? Quantity,
    string? Region,
    int Status,
    int ResponseCount,
    DateTime CreatedAt,
    DateTime ExpiryAt,
    bool IsMine,
    // 改动说明（v2.10.0 寻货置顶）：置顶标记透传（大厅角标/排序展示）
    bool IsPinned = false,
    DateTime? PinnedUntil = null,
    // 改动说明（v2.12.0 商户名义发布）：发布方身份透传（徽章/跳转）
    Guid? PublisherMerchantId = null,
    string? PublisherMerchantName = null,
    string? PublisherType = null);

/// <summary>feed 分页响应</summary>
public record SourcingFeedResponse(SourcingFeedItem[] Items, int Total);

/// <summary>额度条单项（免费额度/今日已用/硬上限/积分单价）</summary>
public record SourcingQuotaItem(int FreeLimit, int TodayUsed, int HardLimit, int PointsPrice);

/// <summary>寻货额度聚合响应（v1.7.8 额度可见化）</summary>
public record SourcingQuotaResponse(SourcingQuotaItem Publish, SourcingQuotaItem Respond, int Balance);

/// <summary>应答型号行（v1.5.0 多行标书：每条应答含多行型号，可引用在售商品）</summary>
public record SourcingResponseItemDto(
    Guid Id,
    string PartNumber,
    Guid? BearingId,
    decimal? Price,
    string? Stock,
    string? LeadTime);

/// <summary>应答明细（发布人可见全量；v1.5.0 多行标书 + 实力摘要）</summary>
public record SourcingResponseDetail(
    Guid Id,
    Guid MerchantId,
    string? MerchantName,
    bool IsVerified,
    // v1.5.0 证据力 P1：商家实力摘要（发布人选定的结构化判断依据）
    string? CompanyName,
    int OnSaleCount,
    int CompletedTaskCount,
    // v1.5.0 多行标书：应答型号行
    List<SourcingResponseItemDto> Items,
    string Remark,
    int Status,
    DateTime CreatedAt,
    // v2.12.0 等级玩法铭牌曝光：经办人段位（号+名，透传自 API）
    int ResponderLevel = 1,
    string? ResponderLevelName = null,
    // 改动说明（v1.57.1 双徽章并列）：透传商家卡级（与"已认证"并列；0=未知档）
    int MerchantGrade = 0,
    string? MerchantGradeDisplay = null);

/// <summary>查看者自己商户的应答（其余人不可见他人报价）</summary>
public record SourcingMyResponse(
    Guid Id,
    List<SourcingResponseItemDto>? Items,
    string Remark,
    int Status,
    DateTime CreatedAt);

/// <summary>寻货详情（对齐 API /api/sourcing/demands/{id}）</summary>
public record SourcingDetailResponse(
    Guid Id,
    string PartNumber,
    // v1.7.9：透传 bearingId（应答预填"我的在售同款"精确匹配）
    Guid? BearingId,
    string? Brand,
    string? Quantity,
    string? ExpectedDelivery,
    string? Region,
    string? Description,
    int Status,
    int ResponseCount,
    DateTime CreatedAt,
    DateTime ExpiryAt,
    DateTime? ClosedAt,
    bool IsPublisher,
    List<SourcingResponseDetail>? Responses,
    SourcingMyResponse? MyResponse,
    string? SelectedMerchantContact,
    /// <summary>是否邀请单（v1.8.0；原 IsTargeted，前端出"邀请"徽章）</summary>
    bool IsPrivate = false,
    /// <summary>邀请收件人家数（v1.8.0；前端出"邀请(N家)"文案）</summary>
    int InvitedCount = 0,
    // 改动说明（v2.12.0 商户名义发布）：发布方身份透传
    Guid? PublisherMerchantId = null,
    string? PublisherMerchantName = null,
    string? PublisherType = null,
    // 改动说明（v2.18.0 电话单向）：删除 PublisherContact（商家永不下发需求方号码）；
    // 新增解锁时刻（商家可见"买家已获取你的联系方式"）与成交回执状态（仅发布方可回填）
    DateTime? ContactUnlockedAt = null,
    Guid? SelectedResponseId = null,
    bool CanFeedback = false,
    SourcingMyFeedback? MyFeedback = null);

/// <summary>成交回执请求（v2.18.0 透传体）</summary>
/// <param name="Outcome">洽谈结果（0=谈成；1=没货；2=加价；3=拖延；4=其他）</param>
/// <param name="Reason">补充说明（可选）</param>
public record SourcingFeedbackRequest(int Outcome, string? Reason);

/// <summary>我的成交回执（详情回显，v2.18.0）</summary>
    public record SourcingMyFeedback(int Outcome, string? Reason, DateTime? At, bool Counted,
        // 改动说明（v1.57.1）：未计分原因（反刷留痕，如"疑似关联方"），前端提示用
        string? Note = null);

/// <summary>会话列表项（v2.18.0，对齐 API /api/sourcing/conversations items）</summary>
public record SourcingConversationItem(
    Guid ConversationId,
    Guid DemandId,
    Guid MerchantId,
    string Role,
    string? CounterpartyName,
    string? PartNumber,
    int? DemandStatus,
    bool DemandReadOnly,
    DateTime? LastMessageAt,
    string? LastMessagePreview,
    int UnreadCount);

/// <summary>会话列表响应</summary>
public record SourcingConversationListResponse(List<SourcingConversationItem> Items, int Total);

/// <summary>会话消息项</summary>
public record SourcingMessageItem(Guid Id, int SenderSide, bool Mine, string Content, DateTime CreatedAt);

/// <summary>会话消息响应（ReadOnly=true 表示单据已结束，会话只读）</summary>
public record SourcingMessagesResponse(
    Guid? ConversationId,
    Guid DemandId,
    Guid MerchantId,
    string Role,
    bool ReadOnly,
    bool HasMore,
    List<SourcingMessageItem> Messages);

/// <summary>发送会话消息请求</summary>
public record SendSourcingMessageRequest(string Content);

/// <summary>未读计数响应（会话与系统消息各一，v2.18.0 消息中心两类）</summary>
public record UnreadCountResponse(int UnreadCount);

/// <summary>发布请求（透传体；v2.12.0 加 MerchantId=商户名义发布；v1.8.0 以 Visibility+InvitedMerchantIds 取代 TargetMerchantId）</summary>
public record SourcingPublishRequest(string PartNumber, Guid? BearingId, string? Brand, string? Quantity,
    string? ExpectedDelivery, string? Region, string? Description, bool UsePoints, Guid? MerchantId = null,
    /// <summary>曝光范围：0=公开（默认，进大厅）/ 1=邀请（不进大厅，只推收件人）</summary>
    int Visibility = 0,
    /// <summary>邀请收件人勾选（空=全部匹配的认证在售商家；仅 Visibility=1 时有意义）</summary>
    List<Guid>? InvitedMerchantIds = null);

/// <summary>应答请求（v1.5.0 多行标书透传体：报价/库存/交期按行携带）</summary>
public record SourcingRespondItem(
    string PartNumber,
    Guid? BearingId,
    decimal? Price,
    string? Stock,
    string? LeadTime);

/// <summary>应答请求（透传体）</summary>
public record SourcingRespondRequest(List<SourcingRespondItem> Items, string Remark, bool UsePoints);

/// <summary>选定请求（透传体）</summary>
public record SourcingSelectRequest(Guid ResponseId);

/// <summary>我发布的寻货项</summary>
public record SourcingMyDemandItem(
    Guid Id,
    string PartNumber,
    string? Brand,
    string? Quantity,
    int Status,
    int ResponseCount,
    DateTime CreatedAt,
    DateTime ExpiryAt,
    // 改动说明（v2.10.0 寻货置顶）："我的"页置顶按钮态透传
    bool IsPinned = false,
    DateTime? PinnedUntil = null,
    // 改动说明（v2.12.0 商户名义发布）：发布身份徽章透传
    Guid? PublisherMerchantId = null,
    string? PublisherMerchantName = null,
    string? PublisherType = null,
    // 改动说明（v1.8.0 公开/邀请）：曝光范围透传（"我的寻货"卡片出"邀请(N家)/转为公开"）
    bool IsPrivate = false,
    int InvitedCount = 0);

/// <summary>商户名义发布的寻货项（v2.12.0 商户工作台"寻货管理-我发布的"，含经办人）</summary>
public record SourcingMerchantDemandItem(
    Guid Id,
    string PartNumber,
    string? Brand,
    string? Quantity,
    int Status,
    int ResponseCount,
    DateTime CreatedAt,
    DateTime ExpiryAt,
    bool IsPinned = false,
    DateTime? PinnedUntil = null,
    /// <summary>经办人昵称（管理操作仅经办人本人，其他成员只读）</summary>
    string? PublisherName = null,
    /// <summary>当前用户是否经办人（前端出/藏管理按钮）</summary>
    bool IsMine = false,
    // 改动说明（v1.8.0 公开/邀请）：曝光范围透传（本店寻货卡片出"邀请(N家)"）
    bool IsPrivate = false,
    int InvitedCount = 0);

/// <summary>商户应答记录项（含需求快照；v1.5.0 型号行随应答透出）</summary>
public record SourcingMerchantResponseItem(
    Guid Id,
    Guid DemandId,
    string? PartNumber,
    int? DemandStatus,
    List<SourcingResponseItemDto>? Items,
    string Remark,
    int Status,
    DateTime CreatedAt);

/// <summary>寻货批量删除返回 {deleted,skipped}（进行中拒删计 skipped，v2.12.0 列表删除）</summary>
public record BatchDeleteSourcingResult(int Deleted, int Skipped);

/// <summary>我的在售同款（v2.13.2 补代理；对齐 API /api/sourcing/my-offering，应答预填数据源）</summary>
public record MyOfferingResponse(
    bool Found,
    bool IsOnSale = false,
    bool IsRestocking = false,
    decimal? Price = null,
    string? PriceDescription = null,
    string? Stock = null,
    string? MinOrder = null,
    string? RestockEta = null,
    string? Remarks = null);

/// <summary>需求信号项（v2.13.2 补代理；对齐 API /api/sourcing/opportunities 反向导购横幅）</summary>
public record OpportunityItem(string PartNumber, int DemandCount, DateTime LatestAt);

/// <summary>为你匹配的寻货项（v1.6.0；对齐 API /api/sourcing/matched，商家侧聚合面）</summary>
public record MatchedSourcingItem(
    Guid Id,
    string PartNumber,
    string? Brand,
    string? Quantity,
    string? Region,
    int Status,
    int ResponseCount,
    DateTime CreatedAt,
    DateTime ExpiryAt,
    /// <summary>是否点名邀请我的单（v1.8.0；原 IsTargeted，前端出"邀请"徽章）</summary>
    bool IsPrivate);
