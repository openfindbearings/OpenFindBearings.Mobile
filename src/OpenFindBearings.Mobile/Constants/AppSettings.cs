namespace OpenFindBearings.Mobile.Constants;

/// <summary>
/// 全局配置：后端服务地址与 OAuth 客户端参数。
/// 开发环境使用本地地址；部署到 K3s 时改为对应公网域名（auth.abcsxl.com / api.515813.xyz）。
/// </summary>
public static class AppSettings
{
    /// <summary>Identity（OIDC 认证中心）基址</summary>
    public const string IdentityAuthority = "https://localhost:7201";

    /// <summary>业务 API 基址</summary>
    public const string ApiBaseUrl = "https://localhost:7183";

    /// <summary>移动端 OAuth 客户端 ID</summary>
    public const string ClientId = "maui-client";

    /// <summary>申请的资源范围</summary>
    public const string Scope = "api:maui";

    /// <summary>租户 realm（对应 Identity 的 openfindbearings 租户）</summary>
    public const string Realm = "openfindbearings";

    /// <summary>令牌端点</summary>
    public static string TokenEndpoint => $"{IdentityAuthority}/connect/token";

    /// <summary>注册端点</summary>
    public static string SignUpEndpoint => $"{IdentityAuthority}/api/account/signup";
}
