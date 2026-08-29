using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.ViewModels;

/// <summary>我的页视图模型：展示登录态、退出登录</summary>
public partial class ProfileViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    [ObservableProperty] private string _status = "未登录";
    [ObservableProperty] private bool _isAuthenticated;

    public ProfileViewModel(IAuthService auth) => _auth = auth;

    /// <summary>进入页面时刷新登录态</summary>
    public async Task RefreshAsync()
    {
        IsAuthenticated = await _auth.IsAuthenticatedAsync();
        Status = IsAuthenticated ? "已登录" : "未登录";
    }

    [RelayCommand]
    private async Task LoginAsync() => await Shell.Current.GoToAsync("LoginPage");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        await RefreshAsync();
        await Toast.Make("已退出登录", ToastDuration.Short).Show();
    }
}
