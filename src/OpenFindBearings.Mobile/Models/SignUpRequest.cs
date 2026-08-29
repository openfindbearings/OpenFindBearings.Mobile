using OpenFindBearings.Mobile.Constants;

namespace OpenFindBearings.Mobile.Models;

/// <summary>手机号+密码注册请求</summary>
public class SignUpRequest
{
    public string Account { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
    public string Realm { get; set; } = AppSettings.Realm;
    public bool AgreeTerms { get; set; } = true;
    public string? InviteCode { get; set; }
}
