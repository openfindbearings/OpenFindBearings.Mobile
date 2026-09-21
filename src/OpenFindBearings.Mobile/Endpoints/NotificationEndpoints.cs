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
    }

    private static string? GetToken(HttpContext http) =>
        http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");

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
