using ObjectDetectionClient.ViewModels;
using System.Diagnostics;

namespace ObjectDetectionClient.Views;

[QueryProperty(nameof(VideoId), "VideoId")]
public partial class VideoDetailPage : ContentPage
{
    private readonly VideoDetailViewModel _viewModel;
    private int _videoId;

    public int VideoId
    {
        get => _videoId;
        set
        {
            _videoId = value;
            Debug.WriteLine($"设置VideoId: {_videoId}");
            OnVideoIdChanged();
        }
    }

    public VideoDetailPage(VideoDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    private async void OnVideoIdChanged()
    {
        if (_videoId > 0)
        {
            Debug.WriteLine($"VideoId改变，开始初始化: {_videoId}");
            await _viewModel.Initialize(_videoId);
        }
        else
        {
            Debug.WriteLine("VideoId无效");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Debug.WriteLine("VideoDetailPage OnAppearing");
    }
}