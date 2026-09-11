using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// "我"的写操作代理端点（/mobile/me/*）：收藏、关注、浏览历史、资料编辑。
/// 统一模式：从入站 Authorization 头取用户 access token，透传给业务 API 的 /api/me/*；
/// 缺 token 一律 401（不静默回空数据）。写操作返回 {success}，查询返回 API 原数据结构。
/// </summary>
public static class MeEndpoints
{
    public static void MapMeEndpoints(this RouteGroupBuilder group)
    {
        // ============ 收藏轴承 ============

        /// <summary>收藏某轴承（已收藏时 API 返回 400，代理如实透出 success:false）</summary>
        group.MapPost("/favorites/{bearingId}", async (
            string bearingId, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.PostVoidAsync($"/api/me/favorites/bearings/{bearingId}", token, ct);
            return ok
                ? Results.Ok(new { success = true, message = "收藏成功" })
                : Results.Ok(new { success = false, message = "收藏失败或已收藏过" });
        })
        .WithName("FavoriteBearing")
        .WithSummary("收藏轴承");

        /// <summary>取消收藏</summary>
        group.MapDelete("/favorites/{bearingId}", async (
            string bearingId, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.DeleteVoidAsync($"/api/me/favorites/bearings/{bearingId}", token, ct);
            return Results.Ok(new { success = ok, message = ok ? "已取消收藏" : "取消失败" });
        })
        .WithName("UnfavoriteBearing")
        .WithSummary("取消收藏轴承");

        /// <summary>查询是否已收藏（详情页红心状态回显）</summary>
        group.MapGet("/favorites/{bearingId}/check", async (
            string bearingId, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var data = await api.GetAsync<CheckFavorited>($"/api/me/favorites/bearings/{bearingId}/check", token, ct);
            return Results.Ok(new { isFavorited = data?.IsFavorited ?? false });
        })
        .WithName("CheckFavorite")
        .WithSummary("查询轴承收藏状态");

        // ============ 关注商家 ============

        /// <summary>关注某商家</summary>
        group.MapPost("/follows/{merchantId}", async (
            string merchantId, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.PostVoidAsync($"/api/me/follows/merchants/{merchantId}", token, ct);
            return ok
                ? Results.Ok(new { success = true, message = "关注成功" })
                : Results.Ok(new { success = false, message = "关注失败或已关注过" });
        })
        .WithName("FollowMerchant")
        .WithSummary("关注商家");

        /// <summary>取消关注</summary>
        group.MapDelete("/follows/{merchantId}", async (
            string merchantId, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.DeleteVoidAsync($"/api/me/follows/merchants/{merchantId}", token, ct);
            return Results.Ok(new { success = ok, message = ok ? "已取消关注" : "取消失败" });
        })
        .WithName("UnfollowMerchant")
        .WithSummary("取消关注商家");

        /// <summary>查询是否已关注</summary>
        group.MapGet("/follows/{merchantId}/check", async (
            string merchantId, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var data = await api.GetAsync<CheckFollowed>($"/api/me/follows/merchants/{merchantId}/check", token, ct);
            return Results.Ok(new { isFollowed = data?.IsFollowed ?? false });
        })
        .WithName("CheckFollow")
        .WithSummary("查询商家关注状态");

        // ============ 浏览历史 ============

        /// <summary>轴承浏览历史（分页）</summary>
    group.MapGet("/history/bearings", async (
        HttpContext http, ApiClient api, [AsParameters] ProfileEndpoints.PageQuery query, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var path = $"/api/me/history/bearings?page={query.Page}&pageSize={query.PageSize}";
            var result = await api.GetPagedAsync<BearingHistoryItem>(path, token, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<BearingHistoryItem>([], 0, 1, 20));
        })
        .WithName("GetBearingHistory")
        .WithSummary("轴承浏览历史");

        /// <summary>商家浏览历史（分页）</summary>
    group.MapGet("/history/merchants", async (
        HttpContext http, ApiClient api, [AsParameters] ProfileEndpoints.PageQuery query, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var path = $"/api/me/history/merchants?page={query.Page}&pageSize={query.PageSize}";
            var result = await api.GetPagedAsync<MerchantHistoryItem>(path, token, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<MerchantHistoryItem>([], 0, 1, 20));
        })
        .WithName("GetMerchantHistory")
        .WithSummary("商家浏览历史");

        /// <summary>上报轴承浏览（详情页进入时前端自动调用）</summary>
        group.MapPost("/history/bearings/{bearingId}", async (
            string bearingId, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            await api.PostVoidAsync($"/api/me/history/bearings/{bearingId}", token, ct);
            // 上报属尽力而为，不向前端报错
            return Results.Ok(new { success = true });
        })
        .WithName("RecordBearingView")
        .WithSummary("上报轴承浏览");

        /// <summary>上报商家浏览</summary>
        group.MapPost("/history/merchants/{merchantId}", async (
            string merchantId, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            await api.PostVoidAsync($"/api/me/history/merchants/{merchantId}", token, ct);
            return Results.Ok(new { success = true });
        })
        .WithName("RecordMerchantView")
        .WithSummary("上报商家浏览");

        /// <summary>删除单条轴承浏览历史（API 按 userId+bearingId 收敛归属）</summary>
        group.MapDelete("/history/bearings/{bearingId}", async (
            string bearingId, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.DeleteVoidAsync($"/api/me/history/bearings/{bearingId}", token, ct);
            return Results.Ok(new { success = ok });
        })
        .WithName("DeleteBearingHistory")
        .WithSummary("删除单条轴承历史");

        /// <summary>删除单条商家浏览历史</summary>
        group.MapDelete("/history/merchants/{merchantId}", async (
            string merchantId, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.DeleteVoidAsync($"/api/me/history/merchants/{merchantId}", token, ct);
            return Results.Ok(new { success = ok });
        })
        .WithName("DeleteMerchantHistory")
        .WithSummary("删除单条商家历史");

        /// <summary>清空全部浏览历史</summary>
        group.MapDelete("/history/clear", async (
            HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.DeleteVoidAsync("/api/me/history/clear", token, ct);
            return Results.Ok(new { success = ok, message = ok ? "历史记录已清空" : "清空失败" });
        })
        .WithName("ClearHistory")
        .WithSummary("清空浏览历史");

        // ============ 资料编辑 ============

        /// <summary>
        /// 更新个人信息：双写 Identity（nickname/pictureUrl）与业务 API
        /// （nickname/avatar/occupation/companyName/industry），保证两侧一致；
        /// 以 API 侧结果为主返回值（业务库是展示事实源）。
        /// </summary>
        group.MapPut("/profile", async (
            UpdateProfileBody body, HttpContext http, ApiClient api, AuthClient authClient, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            // 改动说明：空串归一为 null——Identity 的 PictureUrl 带 [Url] 校验，
            // 前端未设置头像时传 "" 会被判"URL格式不正确"整请求 400（昵称保存失败根因之一）
            var nickname = string.IsNullOrWhiteSpace(body.Nickname) ? null : body.Nickname.Trim();
            var avatar = string.IsNullOrWhiteSpace(body.Avatar) ? null : body.Avatar.Trim();

            // Identity 侧仅在带 nickname/avatar 时同步（部分更新语义，null 不动）
            if (nickname != null || avatar != null)
            {
                await authClient.UpdateProfileAsync(token, nickname, avatar, ct);
            }

            var bizBody = new
            {
                nickname,
                avatar,
                occupation = body.Occupation,
                companyName = body.CompanyName,
                industry = body.Industry
            };
            // 改动说明：API 更新成功时 data=null（Ok(message) 重载），原 PutAsync<string>
            // 判 null 误报失败；改 PutVoidAsync 以 HTTP 状态码判定
            var ok = await api.PutVoidAsync("/api/me/profile", bizBody, token, ct);
            return ok
                ? Results.Ok(new { success = true, message = "保存成功" })
                : Results.Ok(new { success = false, message = "保存失败" });
        })
        .WithName("UpdateProfile")
        .WithSummary("更新个人信息");

        /// <summary>
        /// 上传头像：透传 multipart 到 API /api/me/avatar 落盘，
        /// 返回可公网访问的绝对 URL（经 BFF 媒体代理 /mobile/media/ 前缀，
        /// API 无公网 ingress，图片必须走 BFF 转发；主机名从 X-Forwarded 头推导）。
        /// </summary>
        group.MapPost("/avatar", async (
            IFormFile file, HttpContext http, ApiClient api, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            if (file == null || file.Length == 0)
                return Results.Ok(new { success = false, message = "请选择图片" });

            using var stream = file.OpenReadStream();
            var data = await api.UploadAsync<AvatarUrlResult>(
                "/api/me/avatar", stream, file.FileName, file.ContentType ?? "image/jpeg", token, ct);
            if (data?.Url == null)
                return Results.Ok(new { success = false, message = "上传失败" });

            return Results.Ok(new { success = true, url = PublicUrl(http, data.Url) });
        })
        .WithName("UploadAvatar")
        .WithSummary("上传头像");
    }

    // ============ 工具 ============

    /// <summary>从入站请求提取用户 access token（与 ProfileEndpoints.GetAccessToken 同逻辑）</summary>
        // 改动说明：仅当 JwtBearer 验证通过（主体含 NameIdentifier claim）才返回令牌。
        // 原实现把过期/非法令牌原样透传给上游，401 被吞成"200 空数据"，客户端 401→刷新
        // 自愈链永不触发（收藏列表空、资料空白的根因）。返回 null 后各端点既有的
        // IsNullOrEmpty→Unauthorized 门槛自然生效，客户端刷新后重放即恢复
        private static string? GetToken(HttpContext http)
        {
            if (string.IsNullOrEmpty(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value))
                return null;
            return http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
        }

    /// <summary>
    /// 把 API 相对路径拼成 BFF 公网绝对 URL。
    /// 改动说明：Ingress 终结 TLS 后转发到容器是 http + 集群内 Host，
    /// 必须优先取 traefik 注入的 X-Forwarded-Proto/Host 才能得到 https://bff.515813.xyz。
    /// </summary>
    internal static string PublicUrl(HttpContext http, string relativePath)
    {
        var proto = http.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? http.Request.Scheme;
        var host = http.Request.Headers["X-Forwarded-Host"].FirstOrDefault()
                   ?? http.Request.Host.Host
                   + (http.Request.Host.Port.HasValue ? $":{http.Request.Host.Port}" : "");
        return $"{proto}://{host}{relativePath}";
    }

    /// <summary>API 头像上传响应 {url}</summary>
    public record AvatarUrlResult(string? Url);

    // ============ DTO ============

    /// <summary>API 收藏 check 端点响应 {isFavorited}</summary>
    public record CheckFavorited(bool IsFavorited);

    /// <summary>API 关注 check 端点响应 {isFollowed}</summary>
    public record CheckFollowed(bool IsFollowed);

    /// <summary>轴承历史条目（对齐 API BearingHistoryDto）</summary>
    public record BearingHistoryItem(
        Guid Id, Guid BearingId, string BearingPartNumber, string? BrandName,
        DateTime ViewedAt, int ViewCount);

    /// <summary>商家历史条目（对齐 API MerchantHistoryDto）</summary>
    public record MerchantHistoryItem(
        Guid Id, Guid MerchantId, string MerchantName, string? CompanyName,
        DateTime ViewedAt, int ViewCount);

    /// <summary>资料编辑请求体（全部可选，部分更新）</summary>
    public record UpdateProfileBody(
        string? Nickname,
        string? Avatar,
        int? Occupation,
        string? CompanyName,
        string? Industry);
}
