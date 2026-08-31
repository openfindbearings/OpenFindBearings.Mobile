using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 首页聚合端点
/// 一次请求返回热门轴承 + 推荐商家，减少 Taro 前端多次请求
/// </summary>
public static class HomeEndpoints
{
    public static void MapHomeEndpoints(this RouteGroupBuilder group)
    {

        /// <summary>
        /// 首页聚合数据
        /// 一次返回热门轴承、推荐商家、品牌列表、类型列表
        /// </summary>
        group.MapGet("/home", async (
            ApiClient api,
            CancellationToken ct) =>
        {
            // 并行请求（无状态，可安全并发）
            var hotBearingsTask = api.GetAsync<List<BearingDto>>("/api/bearings/hot?count=10", ct);
            var merchantsTask = api.GetPagedAsync<MerchantDto>("/api/merchants/search?verifiedOnly=true&page=1&pageSize=6", ct);
            var brandsTask = api.GetAsync<List<BrandDto>>("/api/brands", ct);
            var typesTask = api.GetAsync<List<BearingTypeDto>>("/api/bearing-types", ct);

            await Task.WhenAll(hotBearingsTask, merchantsTask, brandsTask, typesTask);

            return Results.Ok(new
            {
                hotBearings = hotBearingsTask.Result ?? [],
                merchants = merchantsTask.Result?.Items ?? [],
                brands = brandsTask.Result ?? [],
                bearingTypes = typesTask.Result ?? [],
            });
        })
        .WithName("GetHome")
        .WithSummary("首页聚合数据")
        .AllowAnonymous();
    }

    // ============ DTO 定义 ============

    public record BearingDto(
        Guid Id,
        string PartNumber,
        string? OldNumber,
        string BearingType,
        decimal InnerDiameter,
        decimal OuterDiameter,
        decimal Width,
        string BrandName,
        string? Image3DUrl,
        string? Image2DUrl);

    public record MerchantDto(
        Guid Id,
        string Name,
        string? Description,
        bool IsVerified);

    public record BrandDto(Guid Id, string Name, string? Country);

    public record BearingTypeDto(Guid Id, string Name);
}
