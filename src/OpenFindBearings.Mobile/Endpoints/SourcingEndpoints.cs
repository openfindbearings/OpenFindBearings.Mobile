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
        /// 寻货 feed（公开浏览；keyword 型号搜索、onlyOpen 进行中过滤、分页）
        /// </summary>
        group.MapGet("/demands", async (
            string? keyword,
            bool onlyOpen,
            int page,
            int pageSize,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            var qs = $"?keyword={Uri.EscapeDataString(keyword ?? "")}&onlyOpen={onlyOpen}&page={(page <= 0 ? 1 : page)}&pageSize={(pageSize is > 0 and <= 50 ? pageSize : 20)}";
            var result = await api.GetAsync<SourcingFeedResponse>($"/api/sourcing/demands{qs}", token, ct);
            return Results.Ok(result ?? new SourcingFeedResponse(Array.Empty<SourcingFeedItem>(), 0));
        })
        .WithName("GetSourcingFeed")
        .WithSummary("寻货列表")
        .WithDescription("进行中寻货 feed（公开，型号搜索）");

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

            var result = await api.PostWithResultAsync<object>("/api/sourcing/demands", body, token, ct);
            return ProxyResult(result, "发布失败");
        })
        .WithName("PublishSourcingDemand")
        .WithSummary("发布寻货")
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
        /// 选定应答（发布人；成功后双方联系方式解锁）
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
    private static string? GetToken(HttpContext http)
        => http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
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
    bool IsMine);

/// <summary>feed 分页响应</summary>
public record SourcingFeedResponse(SourcingFeedItem[] Items, int Total);

/// <summary>应答明细（发布人可见全量）</summary>
public record SourcingResponseDetail(
    Guid Id,
    Guid MerchantId,
    string? MerchantName,
    bool IsVerified,
    decimal? Price,
    string? Stock,
    string? LeadTime,
    string Remark,
    int Status,
    DateTime CreatedAt);

/// <summary>查看者自己商户的应答（其余人不可见他人报价）</summary>
public record SourcingMyResponse(
    Guid Id,
    decimal? Price,
    string? Stock,
    string? LeadTime,
    string Remark,
    int Status,
    DateTime CreatedAt);

/// <summary>寻货详情（对齐 API /api/sourcing/demands/{id}）</summary>
public record SourcingDetailResponse(
    Guid Id,
    string PartNumber,
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
    string? PublisherContact);

/// <summary>发布请求（透传体）</summary>
public record SourcingPublishRequest(string PartNumber, Guid? BearingId, string? Brand, string? Quantity,
    string? ExpectedDelivery, string? Region, string? Description, bool UsePoints);

/// <summary>应答请求（透传体）</summary>
public record SourcingRespondRequest(decimal? Price, string? Stock, string? LeadTime, string Remark, bool UsePoints);

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
    DateTime ExpiryAt);

/// <summary>商户应答记录项（含需求快照）</summary>
public record SourcingMerchantResponseItem(
    Guid Id,
    Guid DemandId,
    string? PartNumber,
    int? DemandStatus,
    decimal? Price,
    string? Stock,
    string? LeadTime,
    string Remark,
    int Status,
    DateTime CreatedAt);
