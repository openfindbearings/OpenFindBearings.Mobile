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

            // Identity：登录账号信息（用户名/手机号/昵称）
            var userInfo = await authClient.GetUserInfoAsync(accessToken, ct);
            // API 业务库：资料扩展字段与统计（未 JIT 建号时可能为 null）
            var biz = await api.GetAsync<BizProfile>("/api/me/profile", accessToken, ct);

            // 改动说明：access 过期(10分钟)时两个上游都 401 → 双双 null，原实现仍回 200 空资料，
            // 客户端表现为"已登录用户/手机号消失"且不触发 401→refresh 自愈链（登录态被误判丢失）。
            // 双 null 极大概率是令牌失效（单 JIT 缺号只会 biz null），改回真 401 让客户端刷新重试
            if (userInfo is null && biz is null)
                return Results.Unauthorized();

            return Results.Ok(new UserProfile
            {
                Id = userInfo?.Id ?? biz?.Id.ToString() ?? "",
                UserName = userInfo?.UserName ?? "",
                PhoneNumber = userInfo?.PhoneNumber ?? "",
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
