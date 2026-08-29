using OpenFindBearings.Mobile.Models;

namespace OpenFindBearings.Mobile.Services;

/// <summary>鉴权与会话服务：管理 token、device_id 与登录态</summary>
public interface IAuthService
{
    /// <summary>是否已登录且令牌未过期</summary>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>获取本机 device_id（首次使用时生成并持久化）</summary>
    Task<string?> GetDeviceIdAsync();

    /// <summary>手机号+密码登录</summary>
    Task<AuthResult> LoginAsync(string phone, string password);

    /// <summary>手机号+密码注册</summary>
    Task<AuthResult> RegisterAsync(string phone, string password);

    /// <summary>获取有效访问令牌，过期时自动刷新</summary>
    Task<string?> GetAccessTokenAsync();

    /// <summary>退出登录并清理本地令牌</summary>
    Task LogoutAsync();
}
