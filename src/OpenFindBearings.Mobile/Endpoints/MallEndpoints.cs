using System.Text.Json.Serialization;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>商城目录条目（BFF 透传 API /api/mall/items.items）</summary>
public record MallItemResponse(
    Guid Id, string Key, string Name, string Description, string Icon, int Category,
    int Price, int? OriginalPrice, bool Flashing, DateTime? FlashEnd,
    int? DurationHours, int Stock, int SoldCount, bool SoldOut, string? OwnerMerchantName,
    // 改动说明（v2.10.0 寻货置顶）：置顶对象类型透传（1=商品/2=需求），前端分节与定价单位据此区分
    int TargetKind = 1);

/// <summary>商城目录（含余额，供前端三态按钮）</summary>
public record MallCatalogResponse(
    List<MallItemResponse> Items,
    [property: JsonPropertyName("balance")] int Balance);

/// <summary>兑换结果（BFF 透传 API /api/mall/redeem）</summary>
public record MallRedeemResponse(Guid? OrderId, int PointsSpent, DateTime? PinnedUntil);

/// <summary>兑换订单条目（BFF 透传 API /api/mall/orders items）</summary>
public record MallOrderResponse(
    Guid Id, string ItemKey, string ItemName, int PointsSpent, int Status,
    string? Remark, DateTime CreatedAt, DateTime? FulfilledAt,
    // v2.4.0 实物礼品物流态（虚拟权益恒 0）
    int ShipStatus = 0, string? ShipTracking = null, DateTime? ShippedAt = null, DateTime? ReceivedAt = null);

/// <summary>兑换请求体（BFF → API）</summary>
public record MallRedeemRequest(Guid ItemId, Guid? TargetRef, string? RequestId, bool UseTreasury = false);

/// <summary>礼品兑换请求体（收货三件套必填，v2.4.0 托管扣款）</summary>
public record MallRedeemGiftRequest(Guid ItemId, string ReceiverName, string ReceiverPhone, string ReceiverAddress, string? RequestId);

/// <summary>确认收货响应（结算入商家金库的分值）</summary>
public record MallConfirmResponse(int Settled);

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
                "/api/mall/redeem", new { itemId = req.ItemId, targetRef = req.TargetRef, requestId = req.RequestId, useTreasury = req.UseTreasury }, token, ct);
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

        /// <summary>实物礼品兑换（v2.4.0 托管扣款；失败原因透传）</summary>
        group.MapPost("/redeem-gift", async (MallRedeemGiftRequest req, ApiClient api, HttpContext http, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var call = await api.PostWithResultAsync<MallRedeemResponse>("/api/mall/redeem-gift",
                new { itemId = req.ItemId, receiverName = req.ReceiverName, receiverPhone = req.ReceiverPhone, receiverAddress = req.ReceiverAddress, requestId = req.RequestId }, token, ct);
            if (!call.Success || call.Data == null)
                return Results.Json(new { success = false, message = call.ErrorText ?? "兑换失败" }, statusCode: 400);
            return Results.Ok(call.Data);
        })
        .WithName("RedeemMallGiftProxy")
        .WithSummary("礼品兑换")
        .RequireAuthorization();

        /// <summary>确认收货（买家；触发商家金库结算）</summary>
        group.MapPost("/orders/{id}/confirm-receipt", async (Guid id, ApiClient api, HttpContext http, CancellationToken ct) =>
        {
            var token = GetToken(http);
            if (string.IsNullOrEmpty(token)) return Results.Unauthorized();
            var call = await api.PostWithResultAsync<MallConfirmResponse>($"/api/mall/orders/{id}/confirm-receipt", new { }, token, ct);
            return call.Success
                ? Results.Ok(new { success = true, settled = call.Data?.Settled ?? 0 })
                : Results.Json(new { success = false, message = call.ErrorText ?? "操作失败" }, statusCode: 400);
        })
        .WithName("ConfirmMallReceiptProxy")
        .WithSummary("确认收货")
        .RequireAuthorization();
    }

    /// <summary>从入站 Authorization 头取用户 access token（缺省 null）</summary>
    private static string? GetToken(HttpContext http)
    {
        var auth = http.Request.Headers.Authorization.ToString();
        return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth["Bearer ".Length..] : null;
    }
}
