using System.Net.Http.Json;
using Microsoft.Maui.Storage;
using OpenFindBearings.Mobile.Constants;
using OpenFindBearings.Mobile.Models;

namespace OpenFindBearings.Mobile.Services;

/// <summary>
/// 鉴权服务实现：基于 OpenIddict 密码授权/刷新授权。
/// token 与 device_id 持久化于 SecureStorage，刷新时校验 device_id 实现各设备登录态隔离。
/// </summary>
public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private const string DeviceIdKey = "device_id";
    private const string AccessKey = "access_token";
    private const string RefreshKey = "refresh_token";
    private const string ExpiryKey = "token_expiry";

    public AuthService(HttpClient http) => _http = http;

    public async Task<string?> GetDeviceIdAsync()
    {
        var id = await SecureStorage.Default.GetAsync(DeviceIdKey);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N");
            await SecureStorage.Default.SetAsync(DeviceIdKey, id);
        }
        return id;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await SecureStorage.Default.GetAsync(AccessKey);
        var exp = await SecureStorage.Default.GetAsync(ExpiryKey);
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(exp))
            return false;
        return DateTime.TryParse(exp, out var expiry) && expiry > DateTime.UtcNow;
    }

    public async Task<AuthResult> LoginAsync(string phone, string password)
    {
        var deviceId = await GetDeviceIdAsync();
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = phone,
            ["password"] = password,
            ["client_id"] = AppSettings.ClientId,
            ["scope"] = AppSettings.Scope,
            ["device_id"] = deviceId
        };
        try
        {
            var resp = await _http.PostAsync(AppSettings.TokenEndpoint, new FormUrlEncodedContent(form));
            if (!resp.IsSuccessStatusCode)
                return AuthResult.Failure(await resp.Content.ReadAsStringAsync());
            var token = await resp.Content.ReadFromJsonAsync<TokenResponse>();
            if (token is null)
                return AuthResult.Failure("令牌响应解析失败");
            await StoreTokensAsync(token);
            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            return AuthResult.Failure(ex.Message);
        }
    }

    public async Task<AuthResult> RegisterAsync(string phone, string password)
    {
        var dto = new SignUpRequest
        {
            Account = phone,
            Password = password,
            ConfirmPassword = password,
            Realm = AppSettings.Realm,
            AgreeTerms = true
        };
        try
        {
            var resp = await _http.PostAsJsonAsync(AppSettings.SignUpEndpoint, dto);
            if (!resp.IsSuccessStatusCode)
                return AuthResult.Failure(await resp.Content.ReadAsStringAsync());
            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            return AuthResult.Failure(ex.Message);
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (await IsAuthenticatedAsync())
            return await SecureStorage.Default.GetAsync(AccessKey);
        return await RefreshAsync();
    }

    public async Task<string?> RefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            var refresh = await SecureStorage.Default.GetAsync(RefreshKey);
            if (string.IsNullOrEmpty(refresh))
                return null;
            var deviceId = await GetDeviceIdAsync();
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refresh,
                ["client_id"] = AppSettings.ClientId,
                ["device_id"] = deviceId
            };
            var resp = await _http.PostAsync(AppSettings.TokenEndpoint, new FormUrlEncodedContent(form));
            if (!resp.IsSuccessStatusCode)
            {
                await ClearAsync();
                return null;
            }
            var token = await resp.Content.ReadFromJsonAsync<TokenResponse>();
            if (token is null)
                return null;
            await StoreTokensAsync(token);
            return token.AccessToken;
        }
        catch
        {
            await ClearAsync();
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task LogoutAsync() => await ClearAsync();

    private async Task StoreTokensAsync(TokenResponse token)
    {
        await SecureStorage.Default.SetAsync(AccessKey, token.AccessToken);
        if (!string.IsNullOrEmpty(token.RefreshToken))
            await SecureStorage.Default.SetAsync(RefreshKey, token.RefreshToken);
        var expiry = DateTime.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn - 60));
        await SecureStorage.Default.SetAsync(ExpiryKey, expiry.ToString("o"));
    }

    private async Task ClearAsync()
    {
        SecureStorage.Default.Remove(AccessKey);
        SecureStorage.Default.Remove(RefreshKey);
        SecureStorage.Default.Remove(ExpiryKey);
    }
}
