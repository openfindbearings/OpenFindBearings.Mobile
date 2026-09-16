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

        /// <summary>
        /// 获取当前商户资料（商户信息维护页读，走 X-Merchant-Id 上下文）
        /// </summary>
        group.MapGet("/profile", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            // API 已按当前商户上下文(X-Merchant-Id)定位，ApiClient 自动透传该头
            var result = await api.GetAsync<MerchantProfile>("/api/merchant/profile", token, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetMerchantProfile")
        .WithSummary("获取商户资料")
        .WithDescription("读取当前商户上下文资料，供商户信息维护页编辑回填")
        .RequireAuthorization();

        /// <summary>
        /// 更新当前商户资料（商户信息维护页写，需商户管理员，由 API 校验）
        /// </summary>
        group.MapPut("/profile", async (
            UpdateMerchantProfileRequest body,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var ok = await api.PutVoidAsync("/api/merchant/profile", body, token, ct);
            return Results.Ok(new { success = ok, message = ok ? "资料已更新" : "更新失败（可能无管理员权限）" });
        })
        .WithName("UpdateMerchantProfile")
        .WithSummary("更新商户资料")
        .WithDescription("更新当前商户上下文资料（需商户管理员权限）")
        .RequireAuthorization();

        /// <summary>
        /// 上传商户 Logo：透传 multipart 到 API 落盘，原样返回相对媒体键 /uploads/merchants/logo/...
        /// 改动说明：与头像一致——相对键入库，host 由前端拼独立媒体源，不再由 BFF 拼绝对 URL。
        /// 仅返回 URL，实际写入 DB 由维护页保存 profile 时带 logoUrl 完成。
        /// </summary>
        group.MapPost("/logo", async (
            IFormFile file,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            if (file == null || file.Length == 0)
                return Results.Ok(new { success = false, message = "请选择图片" });

            using var stream = file.OpenReadStream();
            var data = await api.UploadAsync<LogoUrlResult>(
                "/api/merchant/logo", stream, file.FileName, file.ContentType ?? "image/jpeg", token, ct);
            if (data?.Url == null)
                return Results.Ok(new { success = false, message = "上传失败（可能无管理员权限）" });

            return Results.Ok(new { success = true, url = data.Url });
        })
        .WithName("UploadMerchantLogo")
        .WithSummary("上传商户Logo")
        .WithDescription("上传商户 Logo 图片，返回可访问 URL（需商户管理员权限，保存资料时落库）")
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

    /// <summary>
    /// 商户资料（对齐 API MerchantDetailDto 中维护页所需字段）
    /// </summary>
    public record MerchantProfile(
        Guid Id, string Name, string? CompanyName, string? Type,
        string? ContactPerson, string? Phone, string? Mobile, string? Email, string? Address,
        string? LogoUrl, string? Website, string? UnifiedSocialCreditCode,
        string? Description, string? BusinessScope, bool IsVerified, string? Status);

    /// <summary>
    /// 更新商户资料请求体（对齐 API UpdateMerchantCommand 的可编辑字段，null=保留）
    /// </summary>
    public record UpdateMerchantProfileRequest(
        string? Name, string? CompanyName, string? EnglishName, string? UnifiedSocialCreditCode,
        int? Type, string? Description, string? BusinessScope, string? LogoUrl, string? Website,
        string? ContactPerson, string? Phone, string? Mobile, string? Email, string? Address);

    /// <summary>API Logo 上传响应 {url}</summary>
    public record LogoUrlResult(string? Url);
}
