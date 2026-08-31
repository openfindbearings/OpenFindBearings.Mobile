using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 商家相关端点
/// 代理 API 的商家搜索、详情、在售商品等接口
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
            Microsoft.AspNetCore.Http.IHttpContextAccessor httpAccessor,
            CancellationToken ct) =>
        {
            var userId = httpAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var result = await api.PostAsync<object>("/api/merchant/apply", body, null, ct);
            return Results.Ok(new { message = "申请已提交，等待审核" });
        })
        .WithName("ApplyMerchant")
        .WithSummary("商家入驻申请")
        .RequireAuthorization();
    }

    // ============ 参数 ============

    public class SearchParams
    {
        public string? Keyword { get; set; }
        public bool? VerifiedOnly { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class BearingQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public record ApplyRequest(string ContactName, string Phone, string? Description, string? LicenseUrl);

    // ============ DTO ============

    public record MerchantItem(
        Guid Id, string Name, string? Description,
        bool IsVerified, string? Status, int? BearingCount);

    public record MerchantDetail(
        Guid Id, string Name, string? Contact, string? Phone,
        string? Description, bool IsVerified, string? Status);

    public record MerchantBearingItem(
        Guid BearingId, string BearingPartNumber, string? OldNumber,
        string? BearingTypeName, string? BrandName,
        decimal? InnerDiameter, decimal? OuterDiameter, decimal? Width,
        string? Price, bool IsOnSale);

    // ============ 工具 ============

    private static string BuildQueryString(string path, SearchParams p)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(p.Keyword)) parts.Add($"keyword={Uri.EscapeDataString(p.Keyword)}");
        if (p.VerifiedOnly.HasValue) parts.Add($"verifiedOnly={p.VerifiedOnly.Value.ToString().ToLower()}");
        parts.Add($"page={p.Page}");
        parts.Add($"pageSize={p.PageSize}");
        return path + "?" + string.Join("&", parts);
    }
}
