using System.Net;
using System.Text;
using System.Text.Json;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 分享落地页端点（搜索寻货联动）：为"轴承真实货源"与"商家主页"生成服务端渲染的极简 HTML，
/// 注入 Open Graph（og:*）标签供微信/社交抓取为图文卡片，并自动跳转到 H5 对应页面。
/// 匿名可访问；分享链接形如 https://{h5域名}/s/bearing/{id}——由 H5 镜像 nginx 将 /s/ 反代到本 BFF，
/// 故分享出的链接落在 H5 域名下（只有一个小域名，不暴露技术域名与长串路径）。
/// </summary>
public static class ShareEndpoints
{
    /// <summary>
    /// 注册分享落地页端点：GET /s/bearing/{id} 与 GET /s/merchant/{id}
    /// </summary>
    public static void MapShareEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/s");

        // 轴承货源分享落地页：og=型号/品牌/在售商家数，跳 H5 轴承详情（货源视角）
        group.MapGet("/bearing/{id:guid}", async (
            Guid id,
            string? view,
            ApiClient api,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var detail = await api.GetAsync<BearingEndpoints.BearingDetail>($"/api/bearings/{id}", ct);
            if (detail is null) return Results.Redirect("/");

            // 在售商家数（认证）用于卡片标题，与 App 内分享文案同口径
            var merchants = await api.GetPagedAsync<BearingEndpoints.BearingMerchantItem>(
                $"/api/bearings/{id}/merchants?onlyOnSale=true&page=1&pageSize=100", ct);
            var verified = merchants?.Items.Count(m => m.IsVerified) ?? 0;

            var brandPart = string.IsNullOrWhiteSpace(detail.BrandName) ? string.Empty : " · " + detail.BrandName;
            var countPart = verified > 0 ? $" · {verified}家认证商家真实有货" : string.Empty;
            var title = $"{detail.PartNumber}{brandPart}{countPart}";
            // 改动说明（v1.8.2 口径统一）：文案由"并询价"改为"并邀请报价"——单据类型只有公开招标/邀标，"询价"不再作为入口名
            var desc = $"{detail.BearingType}轴承｜内径 {detail.InnerDiameter} 外径 {detail.OuterDiameter} 宽度 {detail.Width} mm｜查看在售商家并邀请报价";
            var image = AbsoluteImage(config, detail.Image3DUrl ?? detail.Image2DUrl);
            // view=supply 透传到 H5（货源视角：在售商家置顶）
            var target = $"{H5Base(config)}/#/pages/home/bearingDetail?id={id}{(view == "supply" ? "&view=supply" : string.Empty)}";
            return Html(title, desc, image, target);
        })
        .WithName("ShareBearingLanding")
        .WithSummary("轴承货源分享落地页")
        .AllowAnonymous();

        // 商家主页分享落地页：og=商家名/认证标识，跳 H5 商家主页
        group.MapGet("/merchant/{id:guid}", async (
            Guid id,
            ApiClient api,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var detail = await api.GetAsync<MerchantEndpoints.MerchantDetail>($"/api/merchants/{id}", ct);
            if (detail is null) return Results.Redirect("/");

            // 认证口径与商家主页分享标题一致：认证→"认证商家"，否则→"轴承商家"
            var title = $"{detail.Name} · {(detail.IsVerified ? "认证商家" : "轴承商家")}";
            var desc = string.IsNullOrWhiteSpace(detail.CompanyName)
                ? "轴承商家主页｜查看在售轴承"
                : $"{detail.CompanyName}｜查看在售轴承";
            var image = AbsoluteImage(config, detail.LogoUrl);
            var target = $"{H5Base(config)}/#/pages/merchant/merchantDetail?id={id}";
            return Html(title, desc, image, target);
        })
        .WithName("ShareMerchantLanding")
        .WithSummary("商家主页分享落地页")
        .AllowAnonymous();
    }

    /// <summary>H5 站点 base（配置 Share:H5BaseUrl，去尾斜杠）</summary>
    private static string H5Base(IConfiguration config) => (config["Share:H5BaseUrl"] ?? string.Empty).TrimEnd('/');

    /// <summary>媒体源 base（配置 Share:MediaBaseUrl，去尾斜杠）</summary>
    private static string MediaBase(IConfiguration config) => (config["Share:MediaBaseUrl"] ?? string.Empty).TrimEnd('/');

    /// <summary>
    /// 把库内相对媒体键解析为社交可抓取的绝对图片地址；无图片时回退 H5 站点图标
    /// </summary>
    private static string AbsoluteImage(IConfiguration config, string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return $"{H5Base(config)}/static/favicon.png";
        if (key.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || key.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return key;
        return $"{MediaBase(config)}{key}";
    }

    /// <summary>
    /// 构造分享落地 HTML：og/twitter 元信息（供社交抓取成卡片）+ 自动跳转脚本与兜底链接。
    /// 标题/描述/图片按 HTML 上下文转义，跳转地址按 JS 字符串转义，防注入。
    /// </summary>
    private static IResult Html(string title, string desc, string image, string target)
    {
        var html = $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>{WebUtility.HtmlEncode(title)}</title>
<meta property=""og:type"" content=""website"">
<meta property=""og:title"" content=""{WebUtility.HtmlEncode(title)}"">
<meta property=""og:description"" content=""{WebUtility.HtmlEncode(desc)}"">
<meta property=""og:image"" content=""{WebUtility.HtmlEncode(image)}"">
<meta name=""twitter:card"" content=""summary_large_image"">
<script>location.replace({JsonSerializer.Serialize(target)});</script>
</head>
<body>
<p><a href=""{WebUtility.HtmlEncode(target)}"">{WebUtility.HtmlEncode(title)}</a></p>
</body>
</html>";
        return Results.Content(html, "text/html", Encoding.UTF8);
    }
}