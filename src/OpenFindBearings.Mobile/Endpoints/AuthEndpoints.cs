using System.Text.RegularExpressions;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Endpoints;

/// <summary>
/// 认证端点：代理 Identity 的注册/登录/刷新/验证码。
/// BFF 层做薄封装、不存储凭证；把 Identity 的失败原因映射为明确的错误码返回移动端，
/// 成功时返回扁平结构 { success, accessToken, refreshToken, expiresIn }。
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder group)
    {
        /// <summary>
        /// 密码登录。
        /// </summary>
        group.MapPost("/login", async (
            LoginRequest body,
            AuthClient authClient,
            CancellationToken ct) =>
        {
            if (!IsChineseMobile(body.Username)) return PhoneInvalid();

            var result = await authClient.LoginAsync(body.Username, body.Password, body.DeviceId, ct);
            return result.Success ? Success(result.Token!) : MapFailure(result, "INVALID_CREDENTIALS");
        })
        .WithName("Login")
        .WithSummary("密码登录")
        .AllowAnonymous();

        /// <summary>
        /// 短信验证码登录/注册（登录即注册：Identity sms grant 对未注册手机号自动建号）。
        /// </summary>
        group.MapPost("/login-sms", async (
            SmsLoginRequest body,
            AuthClient authClient,
            CancellationToken ct) =>
        {
            if (!IsChineseMobile(body.Phone)) return PhoneInvalid();

            var result = await authClient.LoginWithSmsAsync(body.Phone, body.Code, body.DeviceId, ct);
            return result.Success ? Success(result.Token!) : MapFailure(result, "SMS_INVALID");
        })
        .WithName("LoginSms")
        .WithSummary("短信验证码登录")
        .AllowAnonymous();

        /// <summary>
        /// 发送短信验证码。
        /// </summary>
        group.MapPost("/send-code", async (
            SendCodeRequest body,
            AuthClient authClient,
            CancellationToken ct) =>
        {
            if (!IsChineseMobile(body.Phone)) return PhoneInvalid();

            // 改动说明（验证码改密）：type 透传给 Identity（login 默认；改密发码用 reset_password），
            // 隔离不同用途的验证码，防止登录码被挪用于改密
            var ok = await authClient.SendSmsCodeAsync(body.Phone, body.Type, ct);
            return ok
                ? Results.Ok(new { success = true, message = "验证码已发送" })
                : Results.Json(new { success = false, code = "UPSTREAM_ERROR", message = "发送失败" }, statusCode: 502);
        })
        .WithName("SendSmsCode")
        .WithSummary("发送短信验证码")
        .AllowAnonymous();

        /// <summary>
        /// 刷新令牌。
        /// </summary>
        group.MapPost("/refresh", async (
            RefreshRequest body,
            AuthClient authClient,
            CancellationToken ct) =>
        {
            var result = await authClient.RefreshAsync(body.RefreshToken, body.DeviceId, ct);
            return result.Success ? Success(result.Token!) : MapFailure(result, "REFRESH_INVALID");
        })
        .WithName("RefreshToken")
        .WithSummary("刷新令牌")
        .AllowAnonymous();

        /// <summary>
        /// 登出：吊销该会话的刷新令牌（access 短时效自然过期）。尽力而为，即便上游失败也返回成功，
        /// 因移动端本地令牌无论如何都会清除。
        /// </summary>
        group.MapPost("/logout", async (
            LogoutRequest body,
            AuthClient authClient,
            CancellationToken ct) =>
        {
            if (!string.IsNullOrEmpty(body.RefreshToken))
            {
                await authClient.RevokeRefreshTokenAsync(body.RefreshToken, ct);
            }
            return Results.Ok(new { success = true });
        })
        .WithName("Logout")
        .WithSummary("登出（吊销刷新令牌）")
        .AllowAnonymous();
    }

    /// <summary>
    /// 中国大陆手机号格式校验：11 位、1 开头、第二位 3-9。
    /// 移动端以手机号为唯一账号，注册/登录/验证码入口统一在 BFF 层强制。
    /// </summary>
    private static bool IsChineseMobile(string? phone) =>
        !string.IsNullOrEmpty(phone) && Regex.IsMatch(phone, @"^1[3-9]\d{9}$");

    /// <summary>手机号格式不合法的统一失败响应（400 + PHONE_INVALID，移动端直接展示 message）</summary>
    private static IResult PhoneInvalid() =>
        Results.Json(new { success = false, code = "PHONE_INVALID", message = "手机号格式不正确" }, statusCode: 400);

    /// <summary>构造成功响应（扁平结构，供移动端 request.ts 直接读 accessToken）</summary>
    private static IResult Success(AuthClient.TokenResult token) => Results.Ok(new
    {
        success = true,
        accessToken = token.Access_Token,
        refreshToken = token.Refresh_Token,
        expiresIn = token.Expires_In,
    });

    /// <summary>
    /// 把 Identity 失败映射为移动端错误码 + HTTP 状态。
    /// 改动说明：invalid_grant 可能来自密码错误、账号不可用、验证码错误等，据 error_description 细分，
    /// 避免一律 401 "手机号或密码错误" 掩盖真实原因（如禁用/上游故障）。
    /// </summary>
    private static IResult MapFailure(AuthResult result, string fallbackCode)
    {
        var (code, status) = result.Error switch
        {
            // 改动说明（账户不存在提示）：Identity 对"用户不存在"返回可区分的 error_description，
            // 映射为 USER_NOT_FOUND 404，供移动端提示未注册手机号去走验证码即注册
            "invalid_grant" when (result.ErrorDescription ?? "").Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                => ("USER_NOT_FOUND", 404),
            // 改动说明（短信登录上线）：/register 端点已删除，USER_EXISTS/REGISTER_INVALID 两条
            // 注册专属映射随之清掉（僵尸分支），登录链路只剩凭据/禁用/上游错误三类
            "invalid_grant" when (result.ErrorDescription ?? "").Contains("not available", StringComparison.OrdinalIgnoreCase)
                => ("ACCOUNT_DISABLED", 403),
            "invalid_grant" => (fallbackCode, 401),
            "UPSTREAM_ERROR" => ("UPSTREAM_ERROR", 502),
            _ => (fallbackCode, 401)
        };
        // 改动说明：把 Identity 英文 error_description 换成移动端可读中文——
        // 账户不存在引导先注册；密码错误类给出统一模糊文案（不泄露具体原因）
        var message = code switch
        {
            "USER_NOT_FOUND" => "账户不存在，请先验证码方式登录注册",
            "INVALID_CREDENTIALS" when result.Error == "invalid_grant" => "手机号或密码错误",
            _ => result.ErrorDescription ?? "请求失败"
        };
        return Results.Json(new { success = false, code, message }, statusCode: status);
    }

    // ============ 参数 ============

    public record LoginRequest(string Username, string Password, string DeviceId);
    public record SmsLoginRequest(string Phone, string Code, string DeviceId);
    public record SendCodeRequest(string Phone, string? Type);
    public record RefreshRequest(string RefreshToken, string DeviceId);
    public record LogoutRequest(string? RefreshToken);
}
