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
        /// 获取用户资料（聚合 Identity + API）
        /// </summary>
        group.MapGet("/", async (
            HttpContext http,
            AuthClient authClient,
            ApiClient api,
            CancellationToken ct) =>
        {
            var accessToken = GetAccessToken(http);
            if (string.IsNullOrEmpty(accessToken))
                return Results.Unauthorized();

            // 从 Identity 获取用户基本信息
            var userInfo = await authClient.GetUserInfoAsync(accessToken, ct);

            return Results.Ok(new UserProfile
            {
                Id = userInfo?.Id ?? "",
                UserName = userInfo?.UserName ?? "",
                PhoneNumber = userInfo?.PhoneNumber ?? "",
                IsActive = userInfo?.IsActive ?? true,
                CreatedAt = userInfo?.CreatedAt ?? "",
                LastLoginAt = userInfo?.LastLoginAt ?? "",
            });
        })
        .WithName("GetProfile")
        .WithSummary("获取用户资料");

        /// <summary>
        /// 我的收藏轴承
        /// </summary>
        group.MapGet("/favorites", async (
            HttpContext http,
            ApiClient api,
            [AsParameters] PageQuery query,
            CancellationToken ct) =>
        {
            var accessToken = GetAccessToken(http);
            var path = $"/api/favorites/bearings?page={query.Page}&pageSize={query.PageSize}";
            var result = await api.GetPagedAsync<FavoriteBearing>(path, accessToken, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<FavoriteBearing>([], 0, 1, 20));
        })
        .WithName("GetFavorites")
        .WithSummary("我的收藏轴承");

        /// <summary>
        /// 我的关注商家
        /// </summary>
        group.MapGet("/follows", async (
            HttpContext http,
            ApiClient api,
            [AsParameters] PageQuery query,
            CancellationToken ct) =>
        {
            var accessToken = GetAccessToken(http);
            var path = $"/api/follows/merchants?page={query.Page}&pageSize={query.PageSize}";
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

    public class UserProfile
    {
        public string Id { get; set; } = "";
        public string UserName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public bool IsActive { get; set; }
        public string CreatedAt { get; set; } = "";
        public string LastLoginAt { get; set; } = "";
    }

    public record FavoriteBearing(Guid Id, string PartNumber, string BrandName, string? Image3DUrl);
    public record FollowedMerchant(Guid Id, string Name, bool IsVerified);
}
