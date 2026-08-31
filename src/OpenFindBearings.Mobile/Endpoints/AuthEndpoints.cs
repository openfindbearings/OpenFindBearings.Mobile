using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 认证端点
/// 代理 Identity 的登录、刷新、验证码等接口
/// BFF 层做薄封装，不存储凭证
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/mobile/auth")
            .WithTags("认证");

        /// <summary>
        /// 密码登录
        /// </summary>
        group.MapPost("/login", async (
            LoginRequest body,
            AuthClient authClient,
            CancellationToken ct) =>
        {
            var result = await authClient.LoginAsync(body.Username, body.Password, body.DeviceId, ct);
            if (result is null)
                return Results.Json(new { success = false, message = "手机号或密码错误" }, statusCode: 401);

            return Results.Ok(new
            {
                success = true,
                accessToken = result.Access_Token,
                refreshToken = result.Refresh_Token,
                expiresIn = result.Expires_In,
            });
        })
        .WithName("Login")
        .WithSummary("密码登录")
        .AllowAnonymous();

        /// <summary>
        /// 短信验证码登录/注册
        /// </summary>
        group.MapPost("/login-sms", async (
            SmsLoginRequest body,
            AuthClient authClient,
            CancellationToken ct) =>
        {
            var result = await authClient.LoginWithSmsAsync(body.Phone, body.Code, body.DeviceId, ct);
            if (result is null)
                return Results.Json(new { success = false, message = "验证码错误或已过期" }, statusCode: 401);

            return Results.Ok(new
            {
                success = true,
                accessToken = result.Access_Token,
                refreshToken = result.Refresh_Token,
                expiresIn = result.Expires_In,
            });
        })
        .WithName("LoginSms")
        .WithSummary("短信验证码登录")
        .AllowAnonymous();

        /// <summary>
        /// 发送短信验证码
        /// </summary>
        group.MapPost("/send-code", async (
            SendCodeRequest body,
            AuthClient authClient,
            CancellationToken ct) =>
        {
            var ok = await authClient.SendSmsCodeAsync(body.Phone, ct);
            return ok
                ? Results.Ok(new { success = true, message = "验证码已发送" })
                : Results.Json(new { success = false, message = "发送失败" }, statusCode: 500);
        })
        .WithName("SendSmsCode")
        .WithSummary("发送短信验证码")
        .AllowAnonymous();

        /// <summary>
        /// 刷新令牌
        /// </summary>
        group.MapPost("/refresh", async (
            RefreshRequest body,
            AuthClient authClient,
            CancellationToken ct) =>
        {
            var result = await authClient.RefreshAsync(body.RefreshToken, body.DeviceId, ct);
            if (result is null)
                return Results.Json(new { success = false, message = "刷新失败" }, statusCode: 401);

            return Results.Ok(new
            {
                success = true,
                accessToken = result.Access_Token,
                refreshToken = result.Refresh_Token,
                expiresIn = result.Expires_In,
            });
        })
        .WithName("RefreshToken")
        .WithSummary("刷新令牌")
        .AllowAnonymous();
    }

    // ============ 参数 ============

    public record LoginRequest(string Username, string Password, string DeviceId);
    public record SmsLoginRequest(string Phone, string Code, string DeviceId);
    public record SendCodeRequest(string Phone);
    public record RefreshRequest(string RefreshToken, string DeviceId);
}
