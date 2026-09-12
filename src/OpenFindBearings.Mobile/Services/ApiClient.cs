using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpenFindBearings.Mobile.Services;

/// <summary>
/// 调用后端 API 的 HTTP 客户端封装
/// 所有请求走 K8s 内部 Service，不经公网
/// </summary>
public class ApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiClient(IHttpClientFactory httpClientFactory, ILogger<ApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 当前请求的商户上下文ID（修复 B5：一人多商户时透传 X-Merchant-Id 到 API）。
    /// ApiClient 为 Scoped 每请求一实例，端点入口赋值后各方法共享，无并发串扰
    /// </summary>
    public string? MerchantId { get; set; }

    /// <summary>
    /// 创建带认证头与商户上下文头的 API 客户端
    /// </summary>
    private HttpClient CreateApiClient(string? accessToken = null)
    {
        var client = _httpClientFactory.CreateClient("Api");
        if (!string.IsNullOrEmpty(accessToken))
            client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        // API 侧 UserContextMiddleware 依据该头判定当前操作商户
        if (!string.IsNullOrEmpty(MerchantId))
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Merchant-Id", MerchantId);
        return client;
    }

    /// <summary>
    /// GET 请求（公开接口，不带 JWT）
    /// </summary>
    public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default) where T : class
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.GetAsync(path, ct);
            response.EnsureSuccessStatusCode();
            var wrapper = await response.Content.ReadFromJsonAsync<ApiResponseWrapper<T>>(JsonOptions, ct);
            return wrapper?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API GET {Path} 失败", path);
            return null;
        }
    }

    /// <summary>
    /// GET 请求（需认证接口，带 JWT）
    /// </summary>
    public async Task<T?> GetAsync<T>(string path, string? accessToken, CancellationToken ct = default) where T : class
    {
        try
        {
            var client = CreateApiClient(accessToken);
            var response = await client.GetAsync(path, ct);
            response.EnsureSuccessStatusCode();
            var wrapper = await response.Content.ReadFromJsonAsync<ApiResponseWrapper<T>>(JsonOptions, ct);
            return wrapper?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API GET {Path} 失败", path);
            return null;
        }
    }

    /// <summary>
    /// POST 请求
    /// </summary>
    public async Task<T?> PostAsync<T>(string path, object body, string? accessToken = null, CancellationToken ct = default) where T : class
    {
        try
        {
            var client = CreateApiClient(accessToken);
            var response = await client.PostAsJsonAsync(path, body, ct);
            response.EnsureSuccessStatusCode();
            var wrapper = await response.Content.ReadFromJsonAsync<ApiResponseWrapper<T>>(JsonOptions, ct);
            return wrapper?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API POST {Path} 失败", path);
            return null;
        }
    }

    /// <summary>
    /// 获取分页数据（公开接口）
    /// </summary>
    public async Task<PagedResult<T>?> GetPagedAsync<T>(string path, CancellationToken ct = default) where T : class
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.GetAsync(path, ct);
            response.EnsureSuccessStatusCode();
            var wrapper = await response.Content.ReadFromJsonAsync<ApiResponseWrapper<PagedResult<T>>>(JsonOptions, ct);
            return wrapper?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API GET {Path} 分页失败", path);
            return null;
        }
    }

    /// <summary>
    /// 获取分页数据（需认证接口，带 JWT）
    /// </summary>
    public async Task<PagedResult<T>?> GetPagedAsync<T>(string path, string? accessToken, CancellationToken ct = default) where T : class
    {
        try
        {
            var client = CreateApiClient(accessToken);
            var response = await client.GetAsync(path, ct);
            response.EnsureSuccessStatusCode();
            var wrapper = await response.Content.ReadFromJsonAsync<ApiResponseWrapper<PagedResult<T>>>(JsonOptions, ct);
            return wrapper?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API GET {Path} 分页失败", path);
            return null;
        }
    }

    /// <summary>
    /// PUT 请求（需认证，带 JWT），返回响应 data。
    /// 改动说明：个人信息编辑代理需要（API PUT /api/me/profile），照抄 Post 模式。
    /// </summary>
    public async Task<T?> PutAsync<T>(string path, object body, string? accessToken = null, CancellationToken ct = default) where T : class
    {
        try
        {
            var client = CreateApiClient(accessToken);
            var response = await client.PutAsJsonAsync(path, body, ct);
            response.EnsureSuccessStatusCode();
            var wrapper = await response.Content.ReadFromJsonAsync<ApiResponseWrapper<T>>(JsonOptions, ct);
            return wrapper?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API PUT {Path} 失败", path);
            return null;
        }
    }

    /// <summary>
    /// 拉取原始响应（媒体代理用：API 无公网 ingress，图片流经 BFF 转发）。
    /// 失败或非 2xx 返回 null。
    /// </summary>
    public async Task<HttpResponseMessage?> GetRawAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Api");
            var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);
            return response.IsSuccessStatusCode ? response : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API GET(raw) {Path} 失败", path);
            return null;
        }
    }

    /// <summary>
    /// 上传文件（multipart/form-data 转发到 API，带用户 JWT）。
    /// 改动说明：头像上传需要透传 IFormFile，普通 JSON 方法不适用；
    /// 返回 API 的 data（{url} 相对路径），由调用方拼公网主机名。
    /// </summary>
    public async Task<T?> UploadAsync<T>(string path, Stream fileStream, string fileName, string contentType, string accessToken, CancellationToken ct = default) where T : class
    {
        try
        {
            var client = CreateApiClient(accessToken);
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(streamContent, "file", fileName);
            var response = await client.PostAsync(path, content, ct);
            response.EnsureSuccessStatusCode();
            var wrapper = await response.Content.ReadFromJsonAsync<ApiResponseWrapper<T>>(JsonOptions, ct);
            return wrapper?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API UPLOAD {Path} 失败", path);
            return null;
        }
    }

    /// <summary>
    /// PUT 请求（响应 data 恒为 null 的更新操作，以 HTTP 状态码判定成败）。
    /// 改动说明：API 的 ApiResponseHelper.Ok(message) 重载 data=null，
    /// 用 PutAsync&lt;string&gt; 会把"成功但无数据"误判为失败，故补 void 版。
    /// </summary>
    public async Task<bool> PutVoidAsync(string path, object body, string? accessToken, CancellationToken ct = default)
    {
        try
        {
            var client = CreateApiClient(accessToken);
            var response = await client.PutAsJsonAsync(path, body, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API PUT {Path} 失败", path);
            return false;
        }
    }

    /// <summary>
    /// POST 请求（无返回体，仅判成功与否，如收藏/关注/浏览上报）
    /// </summary>
    public async Task<bool> PostVoidAsync(string path, string? accessToken, CancellationToken ct = default)
    {
        try
        {
            var client = CreateApiClient(accessToken);
            var response = await client.PostAsync(path, null, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API POST {Path} 失败", path);
            return false;
        }
    }

    /// <summary>
    /// DELETE 写操作（取消收藏/取关/删历史），返回是否成功。
    /// </summary>
    public async Task<bool> DeleteVoidAsync(string path, string? accessToken, CancellationToken ct = default)
    {
        try
        {
            var client = CreateApiClient(accessToken);
            var response = await client.DeleteAsync(path, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API DELETE {Path} 失败", path);
            return false;
        }
    }

    /// <summary>
    /// 标准 API 响应包装结构
    /// </summary>
    private record ApiResponseWrapper<T>(bool Success, int Code, T? Data, string? Message) where T : class;

    /// <summary>
    /// 分页数据结构
    /// </summary>
    public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize) where T : class;
}
