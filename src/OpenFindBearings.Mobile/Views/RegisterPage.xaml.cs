using Microsoft.Extensions.DependencyInjection;
using OpenFindBearings.Mobile.ViewModels;

namespace OpenFindBearings.Mobile.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
        BindingContext = App.Current!.Handler!.MauiContext!.Services.GetRequiredService<RegisterViewModel>();
    }
}
