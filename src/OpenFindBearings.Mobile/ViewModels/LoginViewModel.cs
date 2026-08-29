using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.ViewModels;

/// <summary>登录页视图模型：手机号+密码登录</summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _isBusy;

    public LoginViewModel(IAuthService auth) => _auth = auth;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Phone) || string.IsNullOrWhiteSpace(Password))
        {
            await Toast.Make("请输入手机号和密码", ToastDuration.Short).Show();
            return;
        }
        IsBusy = true;
        var result = await _auth.LoginAsync(Phone, Password);
        IsBusy = false;
        if (result.Ok)
            await Shell.Current.GoToAsync("//MainPage");
        else
            await Toast.Make(result.Error ?? "登录失败", ToastDuration.Long).Show();
    }

    [RelayCommand]
    private async Task GoRegisterAsync() => await Shell.Current.GoToAsync("RegisterPage");
}
