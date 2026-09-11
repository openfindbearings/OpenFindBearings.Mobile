using System.Security.Claims;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 用户资料端点
/// 聚合 Identity 用户信息 + API 业务数据（收藏/关注等）
/// </summary>
public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this RouteGroupBuilder group)
    {

        /// <summary>
        /// 获取用户资料（聚合 Identity 基本信息 + API 业务资料）。
        /// 改动说明：原实现只取 Identity 且因包装未解包恒空；现聚合 API /api/me/profile
        /// 的 nickname/avatar/职业/公司/行业/商家绑定与收藏关注计数，供"我的/个人信息"页消费。
        /// </summary>
        group.MapGet("/profile", async (
            HttpContext http,
            AuthClient authClient,
            ApiClient api,
            CancellationToken ct) =>
        {
            var accessToken = GetAccessToken(http);
            if (string.IsNullOrEmpty(accessToken))
                return Results.Unauthorized();

            // 改动说明：核心身份字段（用户名/手机号/sub）直接读 access token claims——
            // BFF 已启用 JwtBearer 验证，http.User 即有；不再依赖 Identity /me 那一跳，
            // 上游 401/竞态不会再产生"已登录用户"空资料（此前双 null 回 401 的补丁一并撤销）。
            // 扩展字段（昵称/isActive/注册时间/业务资料）best-effort 合并，失败留空由
            // 页面下次进入时懒加载补齐（Taro my 页 didShow 会重拉 profile）
            string Claim(params string[] types) => types
                .Select(t => http.User.FindFirst(t)?.Value)
                .FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";
            var claimUserName = Claim(ClaimTypes.Name, "preferred_username", "name");
            var claimPhone = Claim("phone_number");
            var claimSub = Claim(ClaimTypes.NameIdentifier, "sub");
            // claims 为空 = 令牌缺失/已过期（JwtBearer 未通过验证），回真 401 触发客户端刷新重放，
            // 不能再返回 200 空资料（否则客户端永不刷新，登录态被误判丢失）
            if (string.IsNullOrEmpty(claimSub))
                return Results.Unauthorized();

            // Identity：登录账号信息（用户名/手机号/昵称）
            var userInfo = await authClient.GetUserInfoAsync(accessToken, ct);
            // API 业务库：资料扩展字段与统计（未 JIT 建号时可能为 null）
            var biz = await api.GetAsync<BizProfile>("/api/me/profile", accessToken, ct);

            return Results.Ok(new UserProfile
            {
                Id = claimSub.Length > 0 ? claimSub : (userInfo?.Id ?? biz?.Id.ToString() ?? ""),
                UserName = claimUserName.Length > 0 ? claimUserName : (userInfo?.UserName ?? ""),
                PhoneNumber = claimPhone.Length > 0 ? claimPhone : (userInfo?.PhoneNumber ?? ""),
                Nickname = userInfo?.Nickname ?? biz?.Nickname,
                Avatar = biz?.Avatar,
                Occupation = biz?.Occupation,
                CompanyName = biz?.CompanyName,
                Industry = biz?.Industry,
                MerchantId = biz?.MerchantId,
                MerchantName = biz?.MerchantName,
                FavoriteCount = biz?.FavoriteCount ?? 0,
                FollowCount = biz?.FollowCount ?? 0,
                IsActive = userInfo?.IsActive ?? true,
                CreatedAt = userInfo?.CreatedAt ?? "",
                LastLoginAt = userInfo?.LastLoginAt ?? "",
            });
        })
        .WithName("GetProfile")
        .WithSummary("获取用户资料");

        /// <summary>
        /// 我的收藏轴承。
        /// 改动说明：①补 401 门槛（原缺 token 静默回空页，登录态误判）；
        /// ②DTO 改嵌套形状对齐 API FavoriteBearingDto {id,createdAt,bearing:{...}}
        /// （原平铺定义反序列化必抛被吞→列表恒空）。
        /// </summary>
        group.MapGet("/favorites", async (
            HttpContext http,
            ApiClient api,
            [AsParameters] PageQuery query,
            CancellationToken ct) =>
        {
            var accessToken = GetAccessToken(http);
            if (string.IsNullOrEmpty(accessToken))
                return Results.Unauthorized();

            var path = $"/api/me/favorites/bearings?page={query.Page}&pageSize={query.PageSize}";
            var result = await api.GetPagedAsync<FavoriteBearing>(path, accessToken, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<FavoriteBearing>([], 0, 1, 20));
        })
        .WithName("GetFavorites")
        .WithSummary("我的收藏轴承");

        /// <summary>
        /// 我的关注商家（401 门槛与嵌套 DTO 修复同上）
        /// </summary>
        group.MapGet("/followed", async (
            HttpContext http,
            ApiClient api,
            [AsParameters] PageQuery query,
            CancellationToken ct) =>
        {
            var accessToken = GetAccessToken(http);
            if (string.IsNullOrEmpty(accessToken))
                return Results.Unauthorized();

            var path = $"/api/me/follows/merchants?page={query.Page}&pageSize={query.PageSize}";
            var result = await api.GetPagedAsync<FollowedMerchant>(path, accessToken, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<FollowedMerchant>([], 0, 1, 20));
        })
        .WithName("GetFollows")
        .WithSummary("我的关注商家");
    }

    // ============ 工具 ============

    private static string? GetAccessToken(HttpContext http)
    {
        // 改动说明：与 MeEndpoints.GetToken 同规则——JwtBearer 未通过验证（无 NameIdentifier
        // claim，即令牌缺失/过期/非法）一律视为无令牌返回 null，让端点回 401 触发客户端
        // 刷新重放，杜绝过期令牌透传后上游 401 被吞成 200 空数据
        if (string.IsNullOrEmpty(http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value))
            return null;
        return http.Request.Headers.Authorization
            .FirstOrDefault()?.Replace("Bearer ", "");
    }

    // ============ 参数 ============

    public class PageQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // ============ DTO ============

    /// <summary>BFF 聚合后的用户资料（Identity 账号信息 + API 业务资料）</summary>
    public class UserProfile
    {
        public string Id { get; set; } = "";
        public string UserName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string? Nickname { get; set; }
        public string? Avatar { get; set; }
        public int? Occupation { get; set; }
        public string? CompanyName { get; set; }
        public string? Industry { get; set; }
        public Guid? MerchantId { get; set; }
        public string? MerchantName { get; set; }
        public int FavoriteCount { get; set; }
        public int FollowCount { get; set; }
        public bool IsActive { get; set; }
        public string CreatedAt { get; set; } = "";
        public string LastLoginAt { get; set; } = "";
    }

    /// <summary>API GET /api/me/profile 的 UserDto 中本代理需要的字段子集（大小写不敏感映射）。
    /// Occupation 为枚举且 API 未配置 JsonStringEnumConverter，按数字序列化，故用 int?。</summary>
    public record BizProfile(
        Guid Id,
        string? Nickname,
        string? Avatar,
        int? Occupation,
        string? CompanyName,
        string? Industry,
        Guid? MerchantId,
        string? MerchantName,
        int FavoriteCount,
        int FollowCount);

    /// <summary>轴承摘要（收藏列表内嵌）</summary>
    public record BearingBrief(Guid Id, string PartNumber, string? BrandName, string? BearingType);

    /// <summary>商家摘要（关注列表内嵌）</summary>
    public record MerchantBrief(Guid Id, string Name, string? CompanyName, bool IsVerified);

    // 改动说明：收藏/关注项改为与 API FavoriteBearingDto/FollowedMerchantDto 一致的嵌套形状
    // {id, createdAt, bearing|merchant:{...}}，原平铺定义与响应不匹配导致列表恒空。
    public record FavoriteBearing(Guid Id, DateTime CreatedAt, BearingBrief Bearing);
    public record FollowedMerchant(Guid Id, DateTime CreatedAt, MerchantBrief Merchant);
}
