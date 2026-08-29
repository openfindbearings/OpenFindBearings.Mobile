using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFindBearings.Mobile.Services;

/// <summary>业务 API 客户端实现：自动附加 Bearer 令牌后调用 OpenFindBearings.Api</summary>
public class ApiClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly IAuthService _auth;

    public ApiClient(HttpClient http, IAuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _http.GetFromJsonAsync<T>(path, ct);
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string path, T body, CancellationToken ct = default)
    {
        var token = await _auth.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _http.PostAsJsonAsync(path, body, ct);
    }
}
