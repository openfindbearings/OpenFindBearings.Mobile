using Microsoft.Extensions.DependencyInjection;
using OpenFindBearings.Mobile.ViewModels;

namespace OpenFindBearings.Mobile.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
        var vm = App.Current!.Handler!.MauiContext!.Services.GetRequiredService<ProfileViewModel>();
        BindingContext = vm;
        _ = vm.RefreshAsync();
    }
}
