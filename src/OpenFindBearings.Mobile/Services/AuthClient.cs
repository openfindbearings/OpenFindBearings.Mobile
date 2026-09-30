using System.Net.Http.Json;
using System.Text.Json;

namespace OpenFindBearings.Mobile.Services;

/// <summary>
/// 调用 Identity 认证服务的 HTTP 客户端封装。
/// 处理密码/短信登录、刷新令牌、发送验证码、修改密码；
/// 统一附加 OAuth 公共参数（client_id/realm/scope），并把 Identity 的失败响应解析为结构化结果，
/// 不再简单吞成 null，便于端点层映射明确错误码给移动端。
/// 改动说明（短信登录上线）：/register 端点下线，SignUpAsync 随之删除，全面走"验证码登录即注册"。
/// </summary>
public class AuthClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthClient> _logger;
    private readonly IConfiguration _configuration;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>mobile-client 为公开客户端（无 secret），仅需 client_id 自报</summary>
    private string ClientId => _configuration["Identity:ClientId"] ?? "mobile-client";
    /// <summary>租户标识，Identity 的 TenantContextMiddleware 从 token 请求表单体读取 realm</summary>
    private string Realm => _configuration["Identity:Realm"] ?? "openfindbearings";
    /// <summary>
    /// 请求的 scope，api:mobile 映射到资源 openfindbearings-api，使签发令牌带正确 aud。
    /// 改动说明：追加 offline_access——OpenIddict 仅在请求含该 scope 且客户端有 scp:offline_access
    /// 权限时才签发 refresh_token，缺失导致移动端登录响应无 refresh、冷启动登录态丢失。
    /// </summary>
    private string Scope => _configuration["Identity:Scope"] ?? "api:mobile offline_access";

    public AuthClient(
        IHttpClientFactory httpClientFactory,
        ILogger<AuthClient> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// 密码登录（grant_type=password）。携带 device_id 供 Identity 做设备绑定。
    /// </summary>
    public Task<AuthResult> LoginAsync(string username, string password, string deviceId, CancellationToken ct = default)
    {
        return RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password,
            ["device_id"] = deviceId,
        }, "登录", ct);
    }

    /// <summary>
    /// 短信验证码登录/注册（grant_type=sms）。
    /// 改动说明：参数名由 username/sms_code 修正为 phone/code，与 Identity HandleSmsAsync 的
    /// GetParameter("phone")/GetParameter("code") 对齐，否则取不到值恒判失败。
    /// </summary>
    public Task<AuthResult> LoginWithSmsAsync(string phone, string code, string deviceId, CancellationToken ct = default)
    {
        return RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "sms",
            ["phone"] = phone,
            ["code"] = code,
            ["device_id"] = deviceId,
        }, "短信登录", ct);
    }

    /// <summary>
    /// 刷新令牌（grant_type=refresh_token）。携带 device_id 与原始签发值比对。
    /// </summary>
    public Task<AuthResult> RefreshAsync(string refreshToken, string deviceId, CancellationToken ct = default)
    {
        return RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["device_id"] = deviceId,
        }, "刷新令牌", ct);
    }

    /// <summary>
    /// 统一构造 /connect/token 请求：附加 client_id/realm/scope 公共参数并解析成功/失败响应。
    /// 改动说明：原实现漏发 client_id/realm/scope，mobile-client 为公开客户端必须自报 client_id，
    /// 且 password/sms handler 会做租户校验（缺 realm 直接拒绝）、令牌需 scope 才有正确 aud。
    /// </summary>
    private async Task<AuthResult> RequestTokenAsync(Dictionary<string, string> form, string scene, CancellationToken ct)
    {
        try
        {
            form["client_id"] = ClientId;
            form["realm"] = Realm;
            form["scope"] = Scope;

            var client = _httpClientFactory.CreateClient("Identity");
            var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form), ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var token = await JsonSerializer.DeserializeAsync<TokenResult>(
                    new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body)), JsonOptions, ct);
                return token is not null ? AuthResult.Ok(token) : AuthResult.Failure("UPSTREAM_ERROR", null, 502);
            }

            // OpenIddict 失败响应形如 {error, error_description}
            var (error, errorDescription) = ParseOAuthError(body);
            _logger.LogWarning("{Scene}失败: {Error} {Desc}", scene, error, errorDescription);
            return AuthResult.Failure(error, errorDescription, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Scene}请求异常", scene);
            return AuthResult.Failure("UPSTREAM_ERROR", null, 0);
        }
    }

    /// <summary>
    /// 吊销刷新令牌（登出用）：调用 OpenIddict 的 /connect/revocation 作废 refresh_token，
    /// 使该设备后续无法再静默续期。公开客户端需自报 client_id；mobile-client 已具 Revocation 端点权限。
    /// </summary>
    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Identity");
            var response = await client.PostAsync("/connect/revocation", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = refreshToken,
                ["token_type_hint"] = "refresh_token",
                ["client_id"] = ClientId,
            }), ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            // 吊销失败不阻断登出（本地令牌仍会清除）；仅记日志
            _logger.LogWarning(ex, "吊销刷新令牌失败");
            return false;
        }
    }

    /// <summary>
    /// 发送短信验证码（P2 使用，接口先保留）。
    /// </summary>
    public async Task<bool> SendSmsCodeAsync(string phone, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Identity");
            var response = await client.PostAsJsonAsync("/api/sms/send-code", new { phone }, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送验证码异常");
            return false;
        }
    }

    /// <summary>
    /// 修改密码（个人信息页"设置/修改密码"代理）。
    /// 改动说明（短信登录上线）：验证码登录自动注册的用户没有密码，首次设置时
    /// currentPassword 允许为空（由 Identity 按"是否已设密码"分支判定）。
    /// Identity 返回 {success,code,message} 包装，失败时把 message 透传给移动端。
    /// </summary>
    public async Task<(bool Success, string? Message)> ChangePasswordAsync(
        string accessToken,
        string currentPassword,
        string newPassword,
        string confirmNewPassword,
        CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Identity");
            client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
            var response = await client.PostAsJsonAsync("/api/account/me/change-password", new
            {
                currentPassword,
                newPassword,
                confirmNewPassword
            }, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                // Identity 2xx 也可能是业务失败（success=false），以包装体为准
                var ok = TryGetSuccess(body);
                return (ok, ok ? null : ExtractMessage(body) ?? "修改密码失败");
            }
            _logger.LogWarning("修改密码失败: status={Status} body={Body}", (int)response.StatusCode, body);
            return (false, ExtractMessage(body) ?? "修改密码失败");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修改密码请求异常");
            return (false, "上游服务异常，请稍后再试");
        }
    }

    /// <summary>
    /// 获取用户信息（供 profile 代理使用）。
    /// 改动说明：Identity 统一返回 {success,code,data} 包装，原实现直接反序列化包装体
    /// 导致所有字段恒 null（profile 空白 bug）；现先解包装再取 data，并补齐 nickname/email/pictureUrl
    /// （Identity UserResponse 本就有，此前记录未定义被丢弃，前端昵称只能拿 userName 兜底）。
    /// </summary>
    public async Task<UserInfo?> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Identity");
            client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
            var response = await client.GetAsync("/api/account/me", ct);
            if (!response.IsSuccessStatusCode) return null;
            var wrapper = await response.Content.ReadFromJsonAsync<IdentityResponse<UserInfo>>(JsonOptions, ct);
            return wrapper?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取用户信息失败");
            return null;
        }
    }

    /// <summary>
    /// 更新 Identity 侧自助资料（昵称/头像）。个人信息编辑保存时与 API 业务资料双写，
    /// 保证 Identity（登录账号信息）与业务库（展示信息）两侧一致。
    /// </summary>
    public async Task<bool> UpdateProfileAsync(string accessToken, string? nickname, string? pictureUrl, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Identity");
            client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
            // Identity UpdateProfileRequest 为部分更新语义：null 字段不动
            var payload = new { nickname, pictureUrl };
            var response = await client.PutAsJsonAsync("/api/account/me/profile", payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新 Identity 资料失败");
            return false;
        }
    }

    /// <summary>解析 OpenIddict 的 {error, error_description} 失败响应</summary>
    private static (string error, string? description) ParseOAuthError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var error = root.TryGetProperty("error", out var e) ? e.GetString() ?? "unknown" : "unknown";
            var desc = root.TryGetProperty("error_description", out var d) ? d.GetString() : null;
            return (error, desc);
        }
        catch
        {
            return ("unknown", null);
        }
    }

    /// <summary>从 Identity ApiResponse 成败体里读取 success 布尔（缺失视为 false）</summary>
    private static bool TryGetSuccess(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("success", out var s)
                && s.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>从 Identity ApiResponse 失败体里尽力提取 message</summary>
    private static string? ExtractMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                return m.GetString();
        }
        catch { /* 非 JSON 忽略 */ }
        return null;
    }

    /// <summary>
    /// 令牌响应结构（OpenIddict 返回 snake_case，靠大小写不敏感反序列化映射）。
    /// </summary>
    public record TokenResult(
        string Access_Token,
        string? Refresh_Token,
        int Expires_In,
        string? Token_Type);

    /// <summary>
    /// Identity ApiResponse 通用包装（{success,code,data}），供解包用户信息用。
    /// </summary>
    private sealed record IdentityResponse<T>(bool Success, T? Data);

    /// <summary>
    /// 用户信息结构。
    /// 改动说明：补 Nickname/Email/PictureUrl——Identity UserResponse 已有这些字段，
    /// 此前记录未定义导致反序列化丢弃、前端昵称显示成用户名。
    /// </summary>
    public record UserInfo(
        string? Id,
        string? UserName,
        string? PhoneNumber,
        string? Nickname,
        string? Email,
        string? PictureUrl,
        bool IsActive,
        string? CreatedAt,
        string? LastLoginAt);
}

/// <summary>
/// 认证调用统一结果：成功携带令牌，失败携带错误码/描述/上游状态码，
/// 供端点层映射为明确的移动端错误码，避免把不同失败原因一律吞成 null。
/// </summary>
public sealed record AuthResult
{
    public bool Success { get; }
    public AuthClient.TokenResult? Token { get; }
    public string? Error { get; }
    public string? ErrorDescription { get; }
    public int StatusCode { get; }

    private AuthResult(bool success, AuthClient.TokenResult? token, string? error, string? description, int statusCode)
    {
        Success = success;
        Token = token;
        Error = error;
        ErrorDescription = description;
        StatusCode = statusCode;
    }

    /// <summary>令牌换取成功</summary>
    public static AuthResult Ok(AuthClient.TokenResult token) => new(true, token, null, null, 200);

    /// <summary>失败</summary>
    public static AuthResult Failure(string error, string? description, int statusCode)
        => new(false, null, error, description, statusCode);
}
