using ObjectDetectionClient.ViewModels;

namespace ObjectDetectionClient.Views;

public partial class UploadPage : ContentPage
{
    public UploadPage(UploadViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}