using System.Text.Json.Serialization;

namespace OpenFindBearings.Mobile.Models;

/// <summary>OIDC 令牌响应</summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("scope")] public string? Scope { get; set; }
}
