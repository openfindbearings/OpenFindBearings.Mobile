using CommunityToolkit.Maui;
using MauiIcons.Core;
using MauiIcons.Fluent;
using MauiIcons.Fluent.Filled;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenFindBearings.Mobile.Constants;
using OpenFindBearings.Mobile.Services;
using OpenFindBearings.Mobile.ViewModels;
using Syncfusion.Maui.Toolkit.Hosting;

namespace OpenFindBearings.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
        var builder = MauiApp.CreateBuilder();
        builder.ConfigureSyncfusionToolkit();
        builder.UseMauiCommunityToolkit();
        builder
			.UseMauiApp<App>()
            .UseFluentMauiIcons()
            .UseFluentFilledMauiIcons()
            .ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// 依赖注入：鉴权与会话、业务 API 客户端、视图模型
		builder.Services.AddHttpClient<AuthService>(c => c.BaseAddress = new Uri(AppSettings.IdentityAuthority));
		builder.Services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<AuthService>());
		builder.Services.AddHttpClient<ApiClient>(c => c.BaseAddress = new Uri(AppSettings.ApiBaseUrl));
		builder.Services.AddSingleton<IApiClient>(sp => sp.GetRequiredService<ApiClient>());
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<RegisterViewModel>();
		builder.Services.AddTransient<ProfileViewModel>();

		return builder.Build();
	}
}
