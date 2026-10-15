using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// BFF 代理公共助手（v2.13.2 一致性收敛）：统一"用户令牌提取"与"上游写操作失败透传"两件事，
/// 消除各端点文件里 8 份 GetToken 私有副本与多套失败封装的分叉（有校验 claim 版、header-only 版、
/// 502 包裹版、RFC7807 版混杂）。
/// </summary>
public static class BffProxy
{
    /// <summary>
    /// 从入站请求提取用户 access token：要求 JWT 具名用户标识 claim 存在，缺失即视为未登录（回 null → 端点 401）。
    /// 统一采用此校验版——原 header-only 变体会把过期/无效令牌前传给上游，上游 401 被静默吞掉。
    /// </summary>
    public static string? Token(HttpContext http)
    {
        if (string.IsNullOrEmpty(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value))
            return null;
        return http.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
    }

    /// <summary>
    /// 透传上游调用结果：成功原样返回 data（无体回空对象）；失败把 API 的 message/detail/title 透传给前端，
    /// 状态码优先用上游（0=建连失败归一为 502）。统一失败封装为 <c>{ success:false, message }</c>。
    /// </summary>
    public static IResult Result<T>(ApiClient.ApiCallResult<T> result, string fallbackMessage) where T : class
    {
        if (result.Success)
            return Results.Ok((object?)result.Data ?? new { });
        var message = string.IsNullOrWhiteSpace(result.ErrorText) ? fallbackMessage : result.ErrorText;
        return Results.Json(new { success = false, message }, statusCode: result.StatusCode == 0 ? 502 : result.StatusCode);
    }
}

/// <summary>
/// 上游上传类接口统一返回体 { url }（v2.13.2 收敛四处同型记录：头像 AvatarUrlResult / Logo LogoUrlResult /
/// 材料 DocumentUrlResult / 礼品图 GiftImageResult，字段完全一致，合并唯一来源）
/// </summary>
public record UrlResult(string? Url);