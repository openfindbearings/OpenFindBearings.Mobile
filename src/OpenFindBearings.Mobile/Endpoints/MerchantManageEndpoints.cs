using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 商户管理代理端点（/mobile/merchant/*）
/// 代理 API /api/merchant/* 的自家轴承管理（列表/添加/上下架/Excel 导入）
/// 统一模式：从入站 Authorization 头取用户 access token 透传；缺 token 一律 401
/// </summary>
public static class MerchantManageEndpoints
{
    public static void MapMerchantManageEndpoints(this RouteGroupBuilder group)
    {
        /// <summary>
        /// 获取自家在售商品列表
        /// </summary>
        group.MapGet("/bearings", async (
            int page,
            int pageSize,
            bool? onlyOnSale,
            bool? pendingOnly,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var path = $"/api/merchant/bearings?page={page}&pageSize={pageSize}";
            if (onlyOnSale.HasValue) path += $"&onlyOnSale={onlyOnSale.Value.ToString().ToLower()}";
            if (pendingOnly.HasValue) path += $"&pendingOnly={pendingOnly.Value.ToString().ToLower()}";

            var result = await api.GetPagedAsync<MyMerchantBearingItem>(path, token, ct);
            return Results.Ok(result ?? new ApiClient.PagedResult<MyMerchantBearingItem>([], 0, 1, 20));
        })
        .WithName("GetMyMerchantBearings")
        .WithSummary("获取自家在售商品")
        .WithDescription("获取当前商户的在售商品列表，需登录且为商户成员")
        .RequireAuthorization();

        /// <summary>
        /// 添加在售商品
        /// </summary>
        group.MapPost("/bearings", async (
            CreateBearingRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var result = await api.PostAsync<object>("/api/merchant/bearings", body, token, ct);
            return Results.Ok(new { message = "添加成功，等待审核" });
        })
        .WithName("CreateMerchantBearing")
        .WithSummary("添加在售商品")
        .WithDescription("向当前商户添加在售商品，需登录且为商户成员")
        .RequireAuthorization();

        /// <summary>
        /// 上架在售商品
        /// </summary>
        group.MapPost("/bearings/{id}/onshelf", async (
            string id,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.PostVoidAsync($"/api/merchant/bearings/{id}/onshelf", token, ct);
            return Results.Ok(new { success = ok, message = ok ? "已上架" : "上架失败" });
        })
        .WithName("PutOnShelf")
        .WithSummary("上架在售商品")
        .WithDescription("上架当前商户的在售商品，需登录且为商户成员")
        .RequireAuthorization();

        /// <summary>
        /// 下架在售商品
        /// </summary>
        group.MapPost("/bearings/{id}/offshelf", async (
            string id,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.PostVoidAsync($"/api/merchant/bearings/{id}/offshelf", token, ct);
            return Results.Ok(new { success = ok, message = ok ? "已下架" : "下架失败" });
        })
        .WithName("TakeOffShelf")
        .WithSummary("下架在售商品")
        .WithDescription("下架当前商户的在售商品，需登录且为商户成员")
        .RequireAuthorization();

        /// <summary>
        /// Excel 批量导入在售商品（仅商户管理员）
        /// </summary>
        group.MapPost("/inventory/import", async (
            IFormFile file,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { success = false, message = "请选择文件" });

            var result = await api.UploadAsync<object>(
                "/api/merchant/inventory/import", file.OpenReadStream(), file.FileName, file.ContentType, token, ct);
            return Results.Ok(new { success = true, message = "导入处理完成" });
        })
        .WithName("ImportMerchantInventory")
        .WithSummary("Excel 批量导入在售商品")
        .WithDescription("上传 Excel 批量导入在售商品（需商户管理员权限）")
        .RequireAuthorization();

        /// <summary>
        /// 上传营业执照（店铺认证，需登录且为商户成员）
        /// </summary>
        group.MapPost("/license", async (
            IFormFile file,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { success = false, message = "请选择文件" });

            var result = await api.UploadAsync<object>(
                "/api/merchant/license", file.OpenReadStream(), file.FileName, file.ContentType, token, ct);
            return Results.Ok(new { success = true, message = "营业执照已提交，等待审核" });
        })
        .WithName("UploadMerchantLicense")
        .WithSummary("上传营业执照")
        .WithDescription("商户上传/更新营业执照照片用于认证，需登录且为商户成员")
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

    /// <summary>
    /// 添加在售商品请求体
    /// </summary>
    public record CreateBearingRequest(
        string BearingPartNumber,
        string? Price,
        string? Stock,
        string? MinOrder,
        string? Remarks);

    /// <summary>
    /// 自家在售商品项（对齐 API MerchantBearing 列表字段）
    /// </summary>
    public record MyMerchantBearingItem(
        Guid BearingId, string BearingPartNumber, string? OldNumber,
        string? BearingTypeName, string? BrandName,
        decimal? InnerDiameter, decimal? OuterDiameter, decimal? Width,
        string? Price, bool IsOnSale);
}
