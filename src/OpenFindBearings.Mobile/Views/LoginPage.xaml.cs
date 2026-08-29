using Microsoft.Extensions.DependencyInjection;
using OpenFindBearings.Mobile.ViewModels;

namespace OpenFindBearings.Mobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
        BindingContext = App.Current!.Handler!.MauiContext!.Services.GetRequiredService<LoginViewModel>();
    }
}
