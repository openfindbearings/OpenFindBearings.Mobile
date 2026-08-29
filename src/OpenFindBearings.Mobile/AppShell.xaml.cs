using Microsoft.Extensions.DependencyInjection;
using OpenFindBearings.Mobile.Views;

namespace OpenFindBearings.Mobile;

/// <summary>应用外壳：底部 TabBar（首页/查询/我的），登录与注册作为路由页面覆盖其上</summary>
public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute("LoginPage", typeof(LoginPage));
		Routing.RegisterRoute("RegisterPage", typeof(RegisterPage));
	}
}
