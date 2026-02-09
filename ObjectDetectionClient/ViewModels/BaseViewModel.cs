using CommunityToolkit.Mvvm.ComponentModel;

namespace ObjectDetectionClient.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title;
}