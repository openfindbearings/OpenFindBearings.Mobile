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
            // 改动说明：分页参数改为可空，缺省时按第 1 页、20 条兜底（与 DTO 原默认值一致）
            var path = $"/api/bearings/{id}/merchants?onlyOnSale=true&page={query.Page ?? 1}&pageSize={query.PageSize ?? 20}";
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
        // 改动说明：新增按 ID 的品牌/类型筛选 + 排序 + 尺寸范围，透传给 API（API 早已支持，此前 BFF 未转发）。
        public Guid? BrandId { get; set; }
        public Guid? BearingTypeId { get; set; }
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; }
        public decimal? MinInnerDiameter { get; set; }
        public decimal? MaxInnerDiameter { get; set; }
        public decimal? MinOuterDiameter { get; set; }
        public decimal? MaxOuterDiameter { get; set; }
        public decimal? MinWidth { get; set; }
        public decimal? MaxWidth { get; set; }
        // 改动说明：分页字段改可空——[AsParameters] 把非空值类型当作必填，调用方不带分页时
        //   会直接返回 400 空体；可空后由 BuildQueryString 兜底 1/20 页默认值
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }

    public class MerchantQuery
    {
        // 改动说明：同上，缺省分页不再触发 400
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }

    // ============ DTO ============

    public record BearingItem(
        Guid Id, string PartNumber, string? OldNumber,
        string BearingType, decimal InnerDiameter, decimal OuterDiameter, decimal Width,
        string BrandName, string? Image3DUrl, string? Image2DUrl,
        // v1.6.0 数据可信联动：参数来源（Internet/OcrExtract/SeedData=仅供参考）
        string? DataSource = null,
        // 改动说明（v1.8.19 搜索结果分流）：该型号认证商家有货家数——大于 0 时搜索结果提供「寻货」
        //   入口定向邀请认证商家，为 0 时仅按互联网数据公布，不提供寻货入口
        int VerifiedMerchantCount = 0);

    // 改动说明（v2.13.2 幻影字段修复）：EnglishName 上游（BearingDto/BearingDetailDto）并不存在，
//   反序列化恒 null，故移除；类型走 BearingType（API 详情已补填）
    public record BearingDetail(
        Guid Id, string PartNumber, string? OldNumber,
        string BearingType, decimal InnerDiameter, decimal OuterDiameter, decimal Width,
        decimal? Weight, string BrandName, string? BrandCountry,
        string? Image3DUrl, string? Image2DUrl,
        int ViewCount, int FavoriteCount,
        // v1.6.0 数据可信联动：参数来源标注依据
        string? DataSource = null);

    // v2.3.0 商城置顶卡：透传置顶态（买家侧角标 + 排序已由 API 完成）
    // v2.15.0 状态统一：IsOnSale/IsRestocking → SaleState(0=已下架/1=出售中/2=补货中)
    // v1.6.0 数据可信联动：IsVerified=询价行动门判定（仅认证商家可发起询价）
    // 改动说明（v1.57.1 双徽章并列）：补商家卡级（Grade/GradeDisplay）——与"已认证"徽章并列展示，
    //   认证是类型、卡级是等级，前端不得用认证代替卡级也不得覆盖
    // 改动说明（v1.8.14 邀请候选口径）：补 Invitable（认证 + 生效 + 未软删）——Taro 邀请候选默认全选
    //   与详情页邀请入口据此过滤，避免把停用/软删商家勾进邀请单被服务端整单拒绝；
    //   缺省 true 是滚动发布兼容：旧版 API 不下发该字段时不应把候选全部过滤掉（服务端仍会逐家校验）
    public record BearingMerchantItem(
        Guid MerchantId, string MerchantName, string? Price, int SaleState, string? RestockEta,
        bool IsPinned = false, DateTime? PinnedUntil = null, bool IsVerified = false,
        int Grade = 0, string? GradeDisplay = null, bool Invitable = true,
        // 改动说明（v1.8.15 商家商品图）：商家此型号的实拍首图，未上传时为空，前端据此显示缩略图
        string? CoverImageUrl = null,
        // 改动说明（v1.8.19 互联网数据展示）：互联网来源标记 + 可公开联系电话（手机已脱敏、固话原样），
        //   前端在"其他在售"段据此标注「互联网数据」并展示电话
        bool IsInternetSource = false, string? ContactPhone = null);

    // v2.16.0 替代品治理：透传三级替代类型与责任提示字段（BearingId=替代品对方轴承，供导航）
    public record InterchangeItem(
        Guid Id, Guid BearingId, string PartNumber, string? BrandName, string? BearingType,
        decimal InnerDiameter, decimal OuterDiameter, decimal Width,
        string? InterchangeType, string TypeLabel, int Confidence, string? Remarks,
        bool IsBidirectional, bool IsAutoRecommendable);

    // ============ 工具 ============

    private static string BuildQueryString(string path, SearchParams p)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(p.Keyword)) parts.Add($"keyword={Uri.EscapeDataString(p.Keyword)}");
        if (!string.IsNullOrEmpty(p.BrandName)) parts.Add($"brandName={Uri.EscapeDataString(p.BrandName)}");
        if (!string.IsNullOrEmpty(p.BearingType)) parts.Add($"bearingType={Uri.EscapeDataString(p.BearingType)}");
        // 改动说明：品牌/类型按 ID 筛选（API 用 BrandId/BearingTypeId），排序与尺寸范围一并透传
        if (p.BrandId.HasValue) parts.Add($"brandId={p.BrandId.Value}");
        if (p.BearingTypeId.HasValue) parts.Add($"bearingTypeId={p.BearingTypeId.Value}");
        if (!string.IsNullOrEmpty(p.SortBy)) parts.Add($"sortBy={Uri.EscapeDataString(p.SortBy)}");
        if (!string.IsNullOrEmpty(p.SortOrder)) parts.Add($"sortOrder={Uri.EscapeDataString(p.SortOrder)}");
        if (p.MinInnerDiameter.HasValue) parts.Add($"minInnerDiameter={p.MinInnerDiameter.Value}");
        if (p.MaxInnerDiameter.HasValue) parts.Add($"maxInnerDiameter={p.MaxInnerDiameter.Value}");
        if (p.MinOuterDiameter.HasValue) parts.Add($"minOuterDiameter={p.MinOuterDiameter.Value}");
        if (p.MaxOuterDiameter.HasValue) parts.Add($"maxOuterDiameter={p.MaxOuterDiameter.Value}");
        if (p.MinWidth.HasValue) parts.Add($"minWidth={p.MinWidth.Value}");
        if (p.MaxWidth.HasValue) parts.Add($"maxWidth={p.MaxWidth.Value}");
        parts.Add($"page={p.Page ?? 1}");
        parts.Add($"pageSize={p.PageSize ?? 20}");
        return path + "?" + string.Join("&", parts);
    }
}
