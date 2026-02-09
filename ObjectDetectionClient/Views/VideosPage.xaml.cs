using ObjectDetectionClient.ViewModels;

namespace ObjectDetectionClient.Views;

public partial class VideosPage : ContentPage
{
    private readonly VideosViewModel _viewModel;

    public VideosPage(VideosViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearing();
    }
}