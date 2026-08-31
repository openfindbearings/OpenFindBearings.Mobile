using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenFindBearings.Mobile.Endpoints;
using OpenFindBearings.Mobile.Services;

var builder = WebApplication.CreateBuilder(args);

// ============ 服务注册 ============

// HttpClient：调用后端 API
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]!);
    client.DefaultRequestHeaders.Accept.Add(new("application/json"));
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
var apiAudience = builder.Configuration["Identity:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = identityAuthority;
        options.Audience = apiAudience;
        options.RequireHttpsMetadata = false; // K8s 内部 HTTP
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
        };
    });
builder.Services.AddAuthorization();

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

// 静态文件（Taro H5 构建产物）
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowTaro");

app.UseAuthentication();
app.UseAuthorization();

// 健康检查
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// ============ API 端点 ============

app.MapHomeEndpoints();
app.MapBearingEndpoints();
app.MapMerchantEndpoints();
app.MapProfileEndpoints();
app.MapAuthEndpoints();

// SPA 回退：非 API 路径全部返回 index.html
app.MapFallbackToFile("index.html");

app.Run();
