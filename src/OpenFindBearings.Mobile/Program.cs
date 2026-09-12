using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using OpenFindBearings.Mobile.Endpoints;
using OpenFindBearings.Mobile.Services;

var builder = WebApplication.CreateBuilder(args);

// ============ 服务注册 ============

// HttpClient：调用后端 API
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]!);
    client.DefaultRequestHeaders.Accept.Add(new("application/json"));
    // 改动说明：BFF→API 为内网服务间调用，附带内部令牌头，供 API 限流中间件识别并豁免按 IP 的用户级限流，
    // 避免所有 App 流量经此单一 Pod IP 转发时触发 guest 限流（429）。令牌取自配置 Internal:ApiToken，须与 API 侧一致。
    var internalToken = builder.Configuration["Internal:ApiToken"];
    if (!string.IsNullOrEmpty(internalToken))
        client.DefaultRequestHeaders.Add("X-Internal-Token", internalToken);
});

// HttpClient：调用 Identity
builder.Services.AddHttpClient("Identity", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Identity:Authority"]!);
    client.DefaultRequestHeaders.Accept.Add(new("application/json"));
});

// 业务服务
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AuthClient>();

// JWT 认证（可选，仅需登录的端点使用）
var identityAuthority = builder.Configuration["Identity:Authority"];
// 改动说明：校验受众应为资源名 openfindbearings-api（api:mobile scope 签发令牌的 aud），
// 此前误用 api:mobile 导致带用户令牌访问 profile 端点恒 401；加代码默认值防部署配置缺项。
var apiAudience = builder.Configuration["Identity:Audience"] ?? "openfindbearings-api";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = identityAuthority;
        options.RequireHttpsMetadata = false; // K8s 内部 HTTP
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = apiAudience,
            ValidateLifetime = true,
        };
    });
builder.Services.AddAuthorization();

// 认证端点限流（单副本 → 内存版分区限流即可，无需 Redis）
// 改动说明：BFF 是手机号+密码/验证码的唯一直面者且公网可达，此前无任何限流，
// 可被暴力破解。按来源 IP 对 /mobile/auth/* 固定窗口限流；被拒返回 429。
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { success = false, code = "RATE_LIMITED", message = "操作过于频繁，请稍后再试" }, ct);
    };
    options.AddPolicy("auth", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// 健康检查
builder.Services.AddHealthChecks();

// CORS（允许 Taro H5 跨域）
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowTaro", policy =>
    {
        policy.WithOrigins(
                "http://localhost:10087",
                "http://172.26.32.1:10087",
                "https://mobile.515813.xyz"
              )
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ============ 中间件管道 ============

app.UseCors("AllowTaro");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// 修复 B5：把入站 X-Merchant-Id 商户上下文头复制到 Scoped ApiClient，
// 所有代理到 API 的请求自动透传（归属校验在 API UserContextMiddleware 完成）
app.Use(async (context, next) =>
{
    var mid = context.Request.Headers["X-Merchant-Id"].FirstOrDefault();
    if (!string.IsNullOrEmpty(mid))
        context.RequestServices.GetRequiredService<OpenFindBearings.Mobile.Services.ApiClient>().MerchantId = mid;
    await next();
});

// 健康检查
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// ============ API 端点（统一前缀 /mobile） ============
// 改动说明：原实现把 bearings/merchants 都直接挂在 /mobile 组下，
// 导致两者都注册 /mobile/search、/mobile/{id}（路由冲突），且与前端约定的
// /mobile/bearings/*、/mobile/merchants/* 命名空间不符（前端请求 404）。
// 现按资源加子组：/mobile/bearings、/mobile/merchants、/mobile/auth，
// 既消除歧义又对齐 Taro config.ts 的路径契约。home/profile 保持在 /mobile 根。
var mobile = app.MapGroup("/mobile");
mobile.MapHomeEndpoints();
mobile.MapGroup("/bearings").MapBearingEndpoints();
mobile.MapGroup("/merchants").MapMerchantEndpoints();
mobile.MapGroup("/merchant").MapMerchantManageEndpoints();
mobile.MapProfileEndpoints();
// /mobile/me/*：收藏/关注/历史/资料编辑写操作代理（用户 token 透传）
mobile.MapGroup("/me").MapMeEndpoints();
// 改动说明：媒体代理（图片经 BFF 转发给无公网 ingress 的 API），注册在 /mobile/media 下，匿名可访问
mobile.MapGroup("/media").MapMediaEndpoints();
// 认证组附加 IP 限流策略（防暴力破解登录/注册/刷新）
var authGroup = mobile.MapGroup("/auth");
authGroup.MapAuthEndpoints();
authGroup.RequireRateLimiting("auth");

app.Run();
