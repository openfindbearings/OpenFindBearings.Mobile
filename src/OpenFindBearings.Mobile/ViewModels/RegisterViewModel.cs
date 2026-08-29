using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.ViewModels;

/// <summary>注册页视图模型：手机号+密码注册</summary>
public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private bool _isBusy;

    public RegisterViewModel(IAuthService auth) => _auth = auth;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Phone) || string.IsNullOrWhiteSpace(Password))
        {
            await Toast.Make("请输入手机号和密码", ToastDuration.Short).Show();
            return;
        }
        if (Password != ConfirmPassword)
        {
            await Toast.Make("两次输入的密码不一致", ToastDuration.Short).Show();
            return;
        }
        IsBusy = true;
        var result = await _auth.RegisterAsync(Phone, Password);
        IsBusy = false;
        if (result.Ok)
        {
            await Toast.Make("注册成功，请登录", ToastDuration.Short).Show();
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Toast.Make(result.Error ?? "注册失败", ToastDuration.Long).Show();
        }
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}
