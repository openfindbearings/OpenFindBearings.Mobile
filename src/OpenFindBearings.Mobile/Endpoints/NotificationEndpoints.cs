using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 站内信代理端点（/mobile/notifications/*）
/// 代理 API /api/notifications/*（收件箱列表/未读数/已读标记），供 Taro 消息中心与 TabBar 角标消费。
/// 统一模式：从入站 Authorization 头取用户 access token 透传；缺 token 一律 401
/// </summary>
public static class NotificationEndpoints
{
    /// <summary>
    /// 注册站内信代理路由（挂在 /mobile/notifications 组下）
    /// </summary>
    public static void MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        /// <summary>
        /// 收件箱分页列表
        /// </summary>
        group.MapGet("/", async (
            bool? unreadOnly,
            int page,
            int pageSize,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var path = $"/api/notifications?page={page}&pageSize={pageSize}";
            if (unreadOnly == true) path += "&unreadOnly=true";

            var result = await api.GetPagedAsync<NotificationItem>(path, token, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<NotificationItem>([], 0, 1, 20));
        })
        .WithName("GetNotifications")
        .WithSummary("站内信列表")
        .WithDescription("当前用户收件箱分页列表，支持仅未读筛选")
        .RequireAuthorization();

        /// <summary>
        /// 未读数（TabBar 角标轮询）
        /// </summary>
        group.MapGet("/unread-count", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<UnreadCountResponse>("/api/notifications/unread-count", token, ct);
            return Results.Ok(new { count = result?.Count ?? 0 });
        })
        .WithName("GetUnreadNotificationCount")
        .WithSummary("站内信未读数")
        .WithDescription("当前用户未读通知条数，供角标展示")
        .RequireAuthorization();

        /// <summary>
        /// 标记单条已读
        /// </summary>
        group.MapPost("/{id}/read", async (
            string id,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.PostVoidAsync($"/api/notifications/{id}/read", token, ct);
            return Results.Ok(new { success = ok });
        })
        .WithName("MarkNotificationRead")
        .WithSummary("标记单条站内信已读")
        .WithDescription("仅能标记本人收件箱内的通知")
        .RequireAuthorization();

        /// <summary>
        /// 全部标记已读
        /// </summary>
        group.MapPost("/read-all", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.PostVoidAsync("/api/notifications/read-all", token, ct);
            return Results.Ok(new { success = ok });
        })
        .WithName("MarkAllNotificationsRead")
        .WithSummary("全部站内信标记已读")
        .WithDescription("批量将当前用户未读通知置为已读")
        .RequireAuthorization();

        /// <summary>
        /// 删除单条站内信（v1.7.2 消息中心左滑删除代理）
        /// </summary>
        group.MapDelete("/{id}", async (
            string id,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.DeleteVoidAsync($"/api/notifications/{id}", token, ct);
            return Results.Ok(new { success = ok });
        })
        .WithName("DeleteNotification")
        .WithSummary("删除单条站内信")
        .WithDescription("硬删本人收件箱内的一条消息")
        .RequireAuthorization();

        /// <summary>
        /// 清空已读站内信（v1.7.2 消息中心"清空已读"代理，未读保留）
        /// </summary>
        group.MapDelete("/read", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.DeleteVoidAsync("/api/notifications/read", token, ct);
            return Results.Ok(new { success = ok });
        })
        .WithName("ClearReadNotifications")
        .WithSummary("清空已读站内信")
        .WithDescription("批量删除当前用户所有已读消息，未读不受影响")
        .RequireAuthorization();

        /// <summary>
        /// 批量标记已读（v2.12.0 列表多选：长按多选后批量操作）
        /// </summary>
        group.MapPost("/batch-read", async (
            BatchIdsRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var data = await api.PostAsync<AffectedResult>("/api/notifications/batch-read", new { ids = body.Ids }, token, ct);
            return Results.Ok(new { success = data != null, affected = data?.Affected ?? 0 });
        })
        .WithName("BatchMarkNotificationsRead")
        .WithSummary("批量标记站内信已读")
        .RequireAuthorization();

        /// <summary>
        /// 批量删除站内信（v2.12.0 列表多选）
        /// </summary>
        group.MapPost("/batch-delete", async (
            BatchIdsRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var data = await api.PostAsync<AffectedResult>("/api/notifications/batch-delete", new { ids = body.Ids }, token, ct);
            return Results.Ok(new { success = data != null, affected = data?.Affected ?? 0 });
        })
.WithName("BatchDeleteNotifications")
        .WithSummary("批量删除站内信")
        .RequireAuthorization();

        /// <summary>
        /// 读取通知偏好（v1.6.0 通知设置）：渠道开关 + 短信网关就绪标记，供"通知设置"子页回显
        /// </summary>
        group.MapGet("/preferences", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var result = await api.GetAsync<NotificationPreferenceResponse>("/api/notifications/preferences", token, ct);
            return Results.Ok(result ?? new NotificationPreferenceResponse(false, true, true, false));
        })
        .WithName("GetNotificationPreferences")
        .WithSummary("读取通知偏好")
        .WithDescription("当前用户通知渠道开关 + 短信网关就绪标记")
        .RequireAuthorization();

        /// <summary>
        /// 更新通知偏好（v1.6.0 用户自勾选接收渠道；站内信恒开不在本接口范围）
        /// </summary>
        group.MapPut("/preferences", async (
            NotificationPreferenceRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.PutVoidAsync("/api/notifications/preferences", body, token, ct);
            return Results.Ok(new { success = ok });
        })
        .WithName("UpdateNotificationPreferences")
        .WithSummary("更新通知偏好")
        .WithDescription("保存用户勾选的短信/微信/邮件通知渠道开关")
        .RequireAuthorization();
    }

// 改动说明（v2.13.2 重复定义清理）：删除类内重复的 BatchIdsRequest/AffectedResult
    //   （与文件末尾命名空间级同型记录重复），统一取命名空间级那份

    // 改动说明（v2.13.2 令牌提取统一）：委托 BffProxy.Token（含 claim 校验）
    private static string? GetToken(HttpContext http) => BffProxy.Token(http);

    /// <summary>收件箱列表项（对齐 API NotificationDto）</summary>
    public record NotificationItem(
        Guid Id,
        string Type,
        string Title,
        string Body,
        string? BizType,
        Guid? BizId,
        bool IsRead,
        DateTime CreatedAt);

    /// <summary>未读数响应（API 返回 { count }）</summary>
    public record UnreadCountResponse(int Count);
}

/// <summary>批量 ID 请求（通知/收藏/关注批量操作共用体，v2.12.0 列表多选）</summary>
public record BatchIdsRequest(List<Guid> Ids);

/// <summary>批量操作结果（透传 API {affected}）</summary>
public record AffectedResult(int Affected);

/// <summary>通知偏好响应（v1.6.0 对齐 API /api/notifications/preferences）</summary>
public record NotificationPreferenceResponse(
    bool SmsNotificationEnabled,
    bool WeChatNotificationEnabled,
    bool EmailNotificationEnabled,
    /// <summary>短信通道是否就绪（未接网关时前端隐藏短信开关）</summary>
    bool SmsGatewayReady);

/// <summary>通知偏好更新请求（v1.6.0 透传体；null=不改该项）</summary>
public record NotificationPreferenceRequest(
    bool? SmsNotificationEnabled,
    bool? WeChatNotificationEnabled,
    bool? EmailNotificationEnabled);
