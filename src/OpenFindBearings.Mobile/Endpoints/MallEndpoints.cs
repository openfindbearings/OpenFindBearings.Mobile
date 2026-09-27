using System.Text.Json.Serialization;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>商城目录条目（BFF 透传 API /api/mall/items.items）</summary>
public record MallItemResponse(
    Guid Id, string Key, string Name, string Description, string Icon, int Category,
    int Price, int? OriginalPrice, bool Flashing, DateTime? FlashEnd,
    int? DurationHours, int Stock, int SoldCount, bool SoldOut);

/// <summary>商城目录（含余额，供前端三态按钮）</summary>
public record MallCatalogResponse(
    List<MallItemResponse> Items,
    [property: JsonPropertyName("balance")] int Balance);

/// <summary>兑换结果（BFF 透传 API /api/mall/redeem）</summary>
public record MallRedeemResponse(Guid? OrderId, int PointsSpent, DateTime? PinnedUntil);

/// <summary>兑换订单条目（BFF 透传 API /api/mall/orders items）</summary>
public record MallOrderResponse(
    Guid Id, string ItemKey, string ItemName, int PointsSpent, int Status,
    string? Remark, DateTime CreatedAt, DateTime? FulfilledAt);

/// <summary>兑换请求体（BFF → API）</summary>
public record MallRedeemRequest(Guid ItemId, Guid? TargetRef, string? RequestId);

/// <summary>
/// 商城代理端点（/mobile/mall/*，v2.3.0 商城虚拟权益）
/// 代理 API /api/mall/*（目录/兑换/我的订单），供 Taro 商城 Tab 与商品管理置顶入口消费。
/// 兑换走 PostWithResultAsync 以透传 API 的业务失败原因（积分不足/越权/非在售）
/// </summary>
public static class MallEndpoints
{
    /// <summary>
    /// 注册商城代理路由（挂在 /mobile/mall 组下）
    /// </summary>
    public static void MapMallEndpoints(this RouteGroupBuilder group)
    {
        /// <summary>商城目录（生效价/闪购态/库存态 + 当前余额）</summary>
        group.MapGet("/items", async (ApiClient api, HttpContext http, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var result = await api.GetAsync<MallCatalogResponse>("/api/mall/items", token, ct);
            return result == null
                ? Results.Json(new { success = false, message = "商城目录加载失败" }, statusCode: 502)
                : Results.Ok(result);
        })
        .WithName("GetMallItems")
        .WithSummary("商城目录")
        .RequireAuthorization();

        /// <summary>积分兑换（失败原因原样透传，前端直接 toast）</summary>
        group.MapPost("/redeem", async (MallRedeemRequest req, ApiClient api, HttpContext http, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var call = await api.PostWithResultAsync<MallRedeemResponse>(
                "/api/mall/redeem", new { itemId = req.ItemId, targetRef = req.TargetRef, requestId = req.RequestId }, token, ct);
            if (!call.Success || call.Data == null)
            {
                var msg = call.ErrorText ?? "兑换失败，请稍后重试";
                return Results.Json(new { success = false, message = msg }, statusCode: 400);
            }
            return Results.Ok(call.Data);
        })
        .WithName("RedeemMallItem")
        .WithSummary("积分兑换")
        .RequireAuthorization();

        /// <summary>我的兑换订单（分页透传）</summary>
        group.MapGet("/orders", async (int? page, int? pageSize, ApiClient api, HttpContext http, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

            var p = page is > 0 ? page.Value : 1;
            var ps = pageSize is > 0 ? pageSize.Value : 20;
            var result = await api.GetPagedAsync<MallOrderResponse>($"/api/mall/orders?page={p}&pageSize={ps}", token, ct);
            return result == null
                ? Results.Json(new { success = false, message = "订单加载失败" }, statusCode: 502)
                : Results.Ok(result);
        })
        .WithName("GetMallOrders")
        .WithSummary("我的兑换订单")
        .RequireAuthorization();
    }

    /// <summary>从入站 Authorization 头取用户 access token（缺省 null）</summary>
    private static string? GetToken(HttpContext http)
    {
        var auth = http.Request.Headers.Authorization.ToString();
        return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth["Bearer ".Length..] : null;
    }
}
