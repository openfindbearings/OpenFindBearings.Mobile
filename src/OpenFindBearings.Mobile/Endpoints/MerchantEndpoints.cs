using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 商家相关端点
/// 代理 API 的商家搜索、详情、在售商品、入驻申请与状态查询等接口
/// </summary>
public static class MerchantEndpoints
{
    public static void MapMerchantEndpoints(this RouteGroupBuilder group)
    {

        /// <summary>
        /// 搜索商家
        /// </summary>
        group.MapGet("/search", async (
            ApiClient api,
            [AsParameters] SearchParams query,
            CancellationToken ct) =>
        {
            var path = BuildQueryString("/api/merchants/search", query);
            var result = await api.GetPagedAsync<MerchantItem>(path, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<MerchantItem>([], 0, 1, 20));
        })
        .WithName("SearchMerchants")
        .WithSummary("搜索商家")
        .AllowAnonymous();

        /// <summary>
        /// 商家详情
        /// </summary>
        group.MapGet("/{id:guid}", async (
            Guid id,
            ApiClient api,
            CancellationToken ct) =>
        {
            var result = await api.GetAsync<MerchantDetail>($"/api/merchants/{id}", ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetMerchantDetail")
        .WithSummary("获取商家详情")
        .AllowAnonymous();

        /// <summary>
        /// 商家在售商品
        /// </summary>
        group.MapGet("/{id:guid}/bearings", async (
            Guid id,
            ApiClient api,
            [AsParameters] BearingQuery query,
            CancellationToken ct) =>
        {
            var path = $"/api/merchants/{id}/bearings?page={query.Page}&pageSize={query.PageSize}";
            var result = await api.GetPagedAsync<MerchantBearingItem>(path, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<MerchantBearingItem>([], 0, 1, 20));
        })
        .WithName("GetMerchantBearings")
        .WithSummary("获取商家在售商品")
        .AllowAnonymous();

        /// <summary>
        /// 入驻申请（需登录）
        /// </summary>
        group.MapPost("/apply", async (
            ApplyRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            // 改动说明：透传用户 access token（原实现 PostAsync 不带 token 且取 sub 判断登录，API 侧拿不到身份会 401）
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.PostAsync<object>("/api/merchant/apply", body, token, ct);
            return Results.Ok(new { message = "申请已提交，等待审核" });
        })
        .WithName("ApplyMerchant")
        .WithSummary("商家入驻申请")
        .WithDescription("提交商家入驻申请（self 新建或 claim 认领爬虫商家），需登录")
        .RequireAuthorization();

        /// <summary>
        /// 查询入驻状态（需登录）
        /// </summary>
        group.MapGet("/application", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.GetAsync<List<MerchantApplicationItem>>("/api/merchant/application", token, ct);
            return Results.Ok(result ?? []);
        })
        .WithName("GetMerchantApplication")
        .WithSummary("查询入驻状态")
        .WithDescription("查询当前用户在各商户的入驻进度，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 认领搜索爬虫商家（需登录）
        /// </summary>
        group.MapGet("/claimable", async (
            string? keyword,
            int page,
            int pageSize,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var path = $"/api/merchant/claimable?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(keyword))
                path += $"&keyword={Uri.EscapeDataString(keyword)}";

            var result = await api.GetAsync<ApiClient.PagedResult<ClaimableMerchantItem>>(path, token, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<ClaimableMerchantItem>([], 0, 1, 20));
        })
        .WithName("GetClaimableMerchants")
        .WithSummary("认领搜索爬虫商家")
        .WithDescription("搜索可认领的爬虫来源商家，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 提名他人为管理员（入驻模式 B，需登录）
        /// </summary>
        group.MapPost("/nominate", async (
            NominateRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.PostAsync<object>("/api/merchant/nominate", body, token, ct);
            return Results.Ok(new { message = "提名邀请已创建，等待对方接受" });
        })
        .WithName("NominateMerchant")
        .WithSummary("提名他人为管理员")
        .WithDescription("提名另一手机号/邮箱作为商户管理员，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 接受管理员提名（被提名人补资料并提交审核，需登录）
        /// </summary>
        group.MapPost("/nominate/{code}/accept", async (
            string code,
            AcceptNominationRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.PostAsync<object>($"/api/merchant/nominate/{code}/accept", body, token, ct);
            return Results.Ok(new { message = "已接受提名，申请已提交，等待审核" });
        })
        .WithName("AcceptNomination")
        .WithSummary("接受管理员提名")
        .WithDescription("被提名人接受提名并补全商户资料，提交后台审核，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 待我接受的管理员提名列表（需登录；API 按 JWT 手机号匹配）
        /// </summary>
        group.MapGet("/nominations/pending", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.GetAsync<List<PendingNominationItem>>("/api/merchant/nominations/pending", token, ct);
            return Results.Ok(result ?? []);
        })
        .WithName("GetPendingNominations")
        .WithSummary("待我接受的提名")
        .WithDescription("查询发给当前登录用户手机号的待接受管理员提名，需登录")
        .RequireAuthorization();

        /// <summary>
        /// 获取当前商户成员列表（需登录且为商户成员）
        /// </summary>
        group.MapGet("/staff", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();

            var result = await api.GetAsync<ApiClient.PagedResult<MerchantStaffItem>>("/api/merchant/staff?page=1&pageSize=100", token, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<MerchantStaffItem>([], 0, 1, 100));
        })
        .WithName("GetMerchantStaff")
        .WithSummary("获取商户成员列表")
        .WithDescription("获取当前商户成员列表（含角色与状态），需登录且为商户成员")
        .RequireAuthorization();

        /// <summary>
        /// 停用成员（管理员）
        /// </summary>
        group.MapPost("/members/{userId}/suspend", async (
            string userId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();
            var ok = await api.PostVoidAsync($"/api/merchant/members/{userId}/suspend", token, ct);
            return Results.Ok(new { success = ok, message = ok ? "成员已停用" : "停用失败" });
        })
        .WithName("SuspendMerchantMember")
        .WithSummary("停用成员")
        .WithDescription("停用商户成员（需商家管理员权限）")
        .RequireAuthorization();

        /// <summary>
        /// 恢复被停用成员（管理员）
        /// </summary>
        group.MapPost("/members/{userId}/activate", async (
            string userId,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();
            var ok = await api.PostVoidAsync($"/api/merchant/members/{userId}/activate", token, ct);
            return Results.Ok(new { success = ok, message = ok ? "成员已恢复" : "恢复失败" });
        })
        .WithName("ActivateMerchantMember")
        .WithSummary("恢复成员")
        .WithDescription("恢复被停用的商户成员（需商家管理员权限）")
        .RequireAuthorization();

        /// <summary>
        /// 变更成员角色（管理员）
        /// </summary>
        group.MapPut("/members/{userId}/role", async (
            string userId,
            ChangeMemberRoleRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token))
                return Results.Unauthorized();
            var ok = await api.PutVoidAsync($"/api/merchant/members/{userId}/role", body, token, ct);
            return Results.Ok(new { success = ok, message = ok ? "角色已变更" : "变更失败" });
        })
        .WithName("ChangeMerchantMemberRole")
        .WithSummary("变更成员角色")
        .WithDescription("变更商户成员角色（需商家管理员权限）")
        .RequireAuthorization();
    }

    /// <summary>
    /// 从入站请求提取用户 access token（与 MeEndpoints 同逻辑）
    /// </summary>
    private static string? GetToken(HttpContext http)
    {
        if (string.IsNullOrEmpty(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value))
            return null;
        return http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
    }

    // ============ 参数 ============

    public class SearchParams
    {
        public string? Keyword { get; set; }
        public bool? VerifiedOnly { get; set; }
        // 改动说明：新增排序透传（name/productcount），与 API 商家搜索对齐
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class BearingQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// 入驻申请请求体（对齐 API ApplyMerchantRequest）
    /// </summary>
    public record ApplyRequest(
        string? Mode = "self",
        Guid? ClaimMerchantId = null,
        string? Name = null,
        int? Type = null,
        string? ContactPerson = null,
        string? Phone = null,
        string? Mobile = null,
        string? Email = null,
        string? Address = null,
        string? CompanyName = null,
        string? UnifiedSocialCreditCode = null,
        string? Description = null,
        string? LicenseUrl = null);

    /// <summary>
    /// 提名他人为管理员请求体（对齐 API NominateMerchantRequest）
    /// </summary>
    public record NominateRequest(
        string? NomineePhone = null,
        string? NomineeEmail = null,
        string? Name = null,
        int? Type = null,
        string? CompanyName = null,
        string? ContactPerson = null,
        string? Phone = null,
        string? Mobile = null,
        string? Email = null,
        string? Address = null,
        bool InitiatorJoins = true);

    /// <summary>
    /// 接受提名请求体（对齐 API AcceptNominationRequest）
    /// </summary>
    public record AcceptNominationRequest(
        string? ContactPerson = null,
        string? Phone = null,
        string? Mobile = null,
        string? Email = null,
        string? Address = null,
        string? CompanyName = null,
        string? UnifiedSocialCreditCode = null,
        string? Description = null,
        string? LicenseUrl = null);

    // ============ DTO ============

    public record MerchantItem(
        Guid Id, string Name, string? CompanyName, string? Type,
        bool IsVerified, string? Status, int? ProductCount, string? LogoUrl);

    // 对齐 API MerchantDto 字段（补 LogoUrl/ContactPerson/Mobile/Email/Address/Grade/FollowerCount/ProductCount）
    public record MerchantDetail(
        Guid Id, string Name, string? CompanyName, string? Type,
        string? ContactPerson, string? Phone, string? Mobile, string? Email, string? Address,
        bool IsVerified, string? Status, string? Grade,
        int FollowerCount, int ProductCount, string? LogoUrl);

    public record MerchantBearingItem(
        Guid BearingId, string BearingPartNumber, string? OldNumber,
        string? BearingTypeName, string? BrandName,
        decimal? InnerDiameter, decimal? OuterDiameter, decimal? Width,
        string? Price, bool IsOnSale);

    /// <summary>
    /// 入驻状态项（对齐 API MerchantApplicationDto）
    /// </summary>
    public record MerchantApplicationItem(
        Guid MerchantId, string MerchantName, string Status,
        string? RejectReason, string Role, bool IsVerified);

    /// <summary>
    /// 可认领爬虫商家项（对齐 API ClaimableMerchantDto）
    /// </summary>
    public record ClaimableMerchantItem(Guid Id, string Name, string? CompanyName, string Type);

    /// <summary>
    /// 待接受提名项（对齐 API PendingNominationDto）
    /// </summary>
    public record PendingNominationItem(
        string InvitationCode,
        Guid MerchantId,
        string MerchantName,
        string? CompanyName,
        DateTime CreatedAt);

    /// <summary>
    /// 商户成员项（对齐 API MerchantStaffDto）
    /// </summary>
    public record MerchantStaffItem(Guid Id, string Nickname, string? Avatar, string? Role, string Status);

    /// <summary>
    /// 变更成员角色请求体
    /// </summary>
    public record ChangeMemberRoleRequest(string Role);

    // ============ 工具 ============

    private static string BuildQueryString(string path, SearchParams p)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(p.Keyword)) parts.Add($"keyword={Uri.EscapeDataString(p.Keyword)}");
        if (p.VerifiedOnly.HasValue) parts.Add($"verifiedOnly={p.VerifiedOnly.Value.ToString().ToLower()}");
        if (!string.IsNullOrEmpty(p.SortBy)) parts.Add($"sortBy={Uri.EscapeDataString(p.SortBy)}");
        if (!string.IsNullOrEmpty(p.SortOrder)) parts.Add($"sortOrder={Uri.EscapeDataString(p.SortOrder)}");
        parts.Add($"page={p.Page}");
        parts.Add($"pageSize={p.PageSize}");
        return path + "?" + string.Join("&", parts);
    }
}
