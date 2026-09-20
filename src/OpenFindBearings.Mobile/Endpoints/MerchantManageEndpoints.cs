using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 商户管理代理端点（/mobile/merchant/*）
/// 代理 API /api/merchant/* 的自家轴承管理（列表/添加/上下架/Excel 导入/证照材料上传 v1.6.0）
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

            // 改动说明（v1.6.2）：上游失败原因透传（UploadAsync 非 2xx 抛 UpstreamUploadException）
            try
            {
                await api.UploadAsync<object>(
                    "/api/merchant/inventory/import", file.OpenReadStream(), file.FileName, file.ContentType, token, ct);
                return Results.Ok(new { success = true, message = "导入处理完成" });
            }
            catch (ApiClient.UpstreamUploadException ex)
            {
                return Results.Ok(new { success = false, message = ex.Message });
            }
        })
        .WithName("ImportMerchantInventory")
        .WithSummary("Excel 批量导入在售商品")
        .WithDescription("上传 Excel 批量导入在售商品（需商户管理员权限）")
        // 改动说明（v1.6.2）：IFormFile 绑定自动附加 anti-forgery 元数据，BFF 无 UseAntiforgery 中间件即 500——纯 Bearer API 显式关闭
        .DisableAntiforgery()
        .RequireAuthorization();

        /// <summary>
        /// 上传证照材料（v1.6.0 由"上传营业执照"泛化：type 1 执照 / 2 授权书 / 3 厂房照；
        /// 入驻后换证/补材料通道，绑定 X-Merchant-Id 当前商户，需登录且为商户成员）
        /// </summary>
        group.MapPost("/documents", async (
            IFormFile file,
            [FromForm] int type,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { success = false, message = "请选择文件" });

            // 改动说明（v1.6.2）：上游 4xx 的 detail 经 UpstreamUploadException 透传，不再固定文案
            try
            {
                var result = await api.UploadAsync<object>(
                    "/api/merchant/documents", file.OpenReadStream(), file.FileName, file.ContentType, token, ct,
                    new Dictionary<string, string> { ["type"] = type.ToString() });
                return result is null
                    ? Results.Json(new { success = false, message = "材料提交失败（类型无效或已有待审执照）" }, statusCode: 502)
                    : Results.Ok(new { success = true, message = "材料已提交，等待审核" });
            }
            catch (ApiClient.UpstreamUploadException ex)
            {
                return Results.Json(new { success = false, message = ex.Message }, statusCode: 502);
            }
        })
        .WithName("UploadMerchantDocument")
        .WithSummary("上传证照材料")
        .WithDescription("商户按类型上传证照材料（执照/授权书/厂房照）进入审核队列，需登录且为商户成员")
        .DisableAntiforgery()
        .RequireAuthorization();

        /// <summary>
        /// 当前商户证照材料列表（信息维护页材料区，v1.6.0 新增，X-Merchant-Id 上下文）
        /// </summary>
        group.MapGet("/documents", async (
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var result = await api.GetAsync<List<JsonElement>>("/api/merchant/documents", token, ct);
            return Results.Ok(result ?? []);
        })
        .WithName("GetMyMerchantDocuments")
        .WithSummary("获取当前商户证照材料")
        .WithDescription("返回当前商户上下文的证照材料与审核状态")
        .RequireAuthorization();

        /// <summary>
        /// 材料文件预上传（v1.6.0 新增）：只传文件返回 URL，供入驻申请"随单材料"先传后提交
        /// </summary>
        group.MapPost("/documents/upload", async (
            IFormFile file,
            ApiClient api,
            HttpContext http,
            CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            if (file == null || file.Length == 0)
                return Results.BadRequest(new { success = false, message = "请选择文件" });

            using var ms = new MemoryStream();
            await file.OpenReadStream().CopyToAsync(ms, ct);
            ms.Position = 0;
            // 改动说明（v1.6.2）：透传上游真实失败原因（扩展名/MIME/大小限制等 400 文案）
            try
            {
                var data = await api.UploadAsync<DocumentUrlResult>(
                    "/api/merchant/documents/upload", ms, file.FileName, file.ContentType ?? "image/jpeg", token, ct);
                return data?.Url is null
                    ? Results.Ok(new { success = false, message = "上传失败" })
                    : Results.Ok(new { success = true, url = data.Url });
            }
            catch (ApiClient.UpstreamUploadException ex)
            {
                return Results.Ok(new { success = false, message = ex.Message });
            }
        })
        .WithName("UploadDocumentFile")
        .WithSummary("材料文件预上传")
        .WithDescription("上传材料文件返回 URL（不建审核记录），需登录")
        .DisableAntiforgery()
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
            // 改动说明（v1.6.2）：透传上游真实失败原因
            try
            {
                var data = await api.UploadAsync<LogoUrlResult>(
                    "/api/merchant/logo", stream, file.FileName, file.ContentType ?? "image/jpeg", token, ct);
                if (data?.Url == null)
                    return Results.Ok(new { success = false, message = "上传失败（可能无管理员权限）" });

                return Results.Ok(new { success = true, url = data.Url });
            }
            catch (ApiClient.UpstreamUploadException ex)
            {
                return Results.Ok(new { success = false, message = ex.Message });
            }
        })
        .WithName("UploadMerchantLogo")
        .WithSummary("上传商户Logo")
        .WithDescription("上传商户 Logo 图片，返回可访问 URL（需商户管理员权限，保存资料时落库）")
        .DisableAntiforgery()
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

    /// <summary>
    /// 材料预上传返回（v1.6.0 对齐 API /api/merchant/documents/upload 的 {url}）
    /// </summary>
    public record DocumentUrlResult(string? Url);
}
