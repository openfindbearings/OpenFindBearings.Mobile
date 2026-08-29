namespace OpenFindBearings.Mobile.Models;

/// <summary>鉴权操作结果</summary>
public class AuthResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }

    public static AuthResult Success() => new() { Ok = true };
    public static AuthResult Failure(string error) => new() { Error = error };
}
