using System.Threading;
using System.Threading.Tasks;

namespace OpenFindBearings.Mobile.Services;

/// <summary>业务 API 客户端接口：自动附带 Bearer 令牌</summary>
public interface IApiClient
{
    Task<T?> GetAsync<T>(string path, CancellationToken ct = default);
    Task<HttpResponseMessage> PostAsync<T>(string path, T body, CancellationToken ct = default);
}
