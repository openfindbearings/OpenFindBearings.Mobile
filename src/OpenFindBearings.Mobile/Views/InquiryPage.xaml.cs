using Microsoft.Extensions.DependencyInjection;
using OpenFindBearings.Mobile.Models;
using OpenFindBearings.Mobile.Services;

namespace OpenFindBearings.Mobile.Views;

public partial class InquiryPage : ContentPage
{
    private readonly IApiClient _api;

    public InquiryPage()
    {
        InitializeComponent();
        _api = App.Current!.Handler!.MauiContext!.Services.GetRequiredService<IApiClient>();
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        var keyword = KeywordEntry?.Text;
        if (string.IsNullOrWhiteSpace(keyword))
            return;
        var result = await _api.GetAsync<PagedBearingResult>($"/api/bearings?keyword={Uri.EscapeDataString(keyword)}");
        Results.ItemsSource = result?.Items ?? new List<BearingItem>();
    }
}
