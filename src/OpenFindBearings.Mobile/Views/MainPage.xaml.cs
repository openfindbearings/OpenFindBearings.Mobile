namespace OpenFindBearings.Mobile.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnSearchClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("InquiryPage");
    private async void OnProfileClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("AboutPage");
}
