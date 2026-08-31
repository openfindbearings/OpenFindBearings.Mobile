using System.Net.Http.Json;
using System.Text.Json;

namespace OpenFindBearings.Mobile.Services;

/// <summary>
/// 调用 Identity 认证服务的 HTTP 客户端封装
/// 处理登录、刷新令牌、发送验证码等
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
    /// 密码登录
    /// </summary>
    public async Task<TokenResult?> LoginAsync(string username, string password, string deviceId, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Identity");
            var clientId = _configuration["Identity:ClientId"] ?? "maui-client";
            var clientSecret = _configuration["Identity:ClientSecret"] ?? "maui-secret";

            var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password,
                ["device_id"] = deviceId,
            }), ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("登录失败: {Error}", error);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TokenResult>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录请求异常");
            return null;
        }
    }

    /// <summary>
    /// 短信验证码登录/注册
    /// </summary>
    public async Task<TokenResult?> LoginWithSmsAsync(string phone, string code, string deviceId, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Identity");

            var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "sms",
                ["username"] = phone,
                ["sms_code"] = code,
                ["device_id"] = deviceId,
            }), ct);

            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<TokenResult>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "短信登录请求异常");
            return null;
        }
    }

    /// <summary>
    /// 刷新令牌
    /// </summary>
    public async Task<TokenResult?> RefreshAsync(string refreshToken, string deviceId, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Identity");

            var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["device_id"] = deviceId,
            }), ct);

            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<TokenResult>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新令牌请求异常");
            return null;
        }
    }

    /// <summary>
    /// 发送短信验证码
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
    /// 获取用户信息
    /// </summary>
    public async Task<UserInfo?> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Identity");
            client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
            var response = await client.GetAsync("/api/account/me", ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<UserInfo>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取用户信息失败");
            return null;
        }
    }

    /// <summary>
    /// 令牌响应结构
    /// </summary>
    public record TokenResult(
        string Access_Token,
        string Refresh_Token,
        int Expires_In,
        string Token_Type);

    /// <summary>
    /// 用户信息结构
    /// </summary>
    public record UserInfo(
        string? Id,
        string? UserName,
        string? PhoneNumber,
        bool IsActive,
        string? CreatedAt,
        string? LastLoginAt);
}
