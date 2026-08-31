using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 轴承相关端点
/// 代理 API 的轴承搜索、详情、在售商家等接口
/// </summary>
public static class BearingEndpoints
{
    public static void MapBearingEndpoints(this RouteGroupBuilder group)
    {

        /// <summary>
        /// 搜索轴承
        /// </summary>
        group.MapGet("/search", async (
            ApiClient api,
            [AsParameters] SearchParams query,
            CancellationToken ct) =>
        {
            var path = BuildQueryString("/api/bearings/search", query);
            var result = await api.GetPagedAsync<BearingItem>(path, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<BearingItem>([], 0, 1, 20));
        })
        .WithName("SearchBearings")
        .WithSummary("搜索轴承")
        .AllowAnonymous();

        /// <summary>
        /// 轴承详情
        /// </summary>
        group.MapGet("/{id:guid}", async (
            Guid id,
            ApiClient api,
            CancellationToken ct) =>
        {
            var result = await api.GetAsync<BearingDetail>($"/api/bearings/{id}", ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetBearingDetail")
        .WithSummary("获取轴承详情")
        .AllowAnonymous();

        /// <summary>
        /// 轴承在售商家
        /// </summary>
        group.MapGet("/{id:guid}/merchants", async (
            Guid id,
            ApiClient api,
            [AsParameters] MerchantQuery query,
            CancellationToken ct) =>
        {
            var path = $"/api/bearings/{id}/merchants?onlyOnSale=true&page={query.Page}&pageSize={query.PageSize}";
            var result = await api.GetPagedAsync<BearingMerchantItem>(path, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<BearingMerchantItem>([], 0, 1, 20));
        })
        .WithName("GetBearingMerchants")
        .WithSummary("获取轴承在售商家")
        .AllowAnonymous();

        /// <summary>
        /// 轴承替代品
        /// </summary>
        group.MapGet("/{id:guid}/interchanges", async (
            Guid id,
            ApiClient api,
            CancellationToken ct) =>
        {
            var result = await api.GetAsync<List<InterchangeItem>>($"/api/bearings/{id}/interchanges", ct);
            return Results.Ok(result ?? []);
        })
        .WithName("GetBearingInterchanges")
        .WithSummary("获取轴承替代品")
        .AllowAnonymous();
    }

    // ============ 参数 ============

    public class SearchParams
    {
        public string? Keyword { get; set; }
        public string? BrandName { get; set; }
        public string? BearingType { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class MerchantQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // ============ DTO ============

    public record BearingItem(
        Guid Id, string PartNumber, string? OldNumber,
        string BearingType, decimal InnerDiameter, decimal OuterDiameter, decimal Width,
        string BrandName, string? Image3DUrl, string? Image2DUrl);

    public record BearingDetail(
        Guid Id, string PartNumber, string? OldNumber, string? EnglishName,
        string BearingType, decimal InnerDiameter, decimal OuterDiameter, decimal Width,
        decimal? Weight, string BrandName, string? BrandCountry,
        string? Image3DUrl, string? Image2DUrl,
        int ViewCount, int FavoriteCount);

    public record BearingMerchantItem(
        Guid MerchantId, string MerchantName, string? Price, bool IsOnSale);

    public record InterchangeItem(
        Guid Id, string PartNumber, string BrandName, string BearingType, int Confidence);

    // ============ 工具 ============

    private static string BuildQueryString(string path, SearchParams p)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(p.Keyword)) parts.Add($"keyword={Uri.EscapeDataString(p.Keyword)}");
        if (!string.IsNullOrEmpty(p.BrandName)) parts.Add($"brandName={Uri.EscapeDataString(p.BrandName)}");
        if (!string.IsNullOrEmpty(p.BearingType)) parts.Add($"bearingType={Uri.EscapeDataString(p.BearingType)}");
        parts.Add($"page={p.Page}");
        parts.Add($"pageSize={p.PageSize}");
        return path + "?" + string.Join("&", parts);
    }
}
