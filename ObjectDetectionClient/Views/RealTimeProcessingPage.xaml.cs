using ObjectDetectionClient.ViewModels;
using System.Diagnostics;
using System.Web;

namespace ObjectDetectionClient.Views;

public partial class RealTimeProcessingPage : ContentPage
{
    private readonly RealTimeProcessingViewModel _viewModel;

    public RealTimeProcessingPage(RealTimeProcessingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        try
        {
            var location = Shell.Current.CurrentState.Location;
            Debug.WriteLine($"导航位置: {location}");

            // 尝试从路由参数中获取 videoId
            var route = location.OriginalString;
            Debug.WriteLine($"导航路径: {route}");

            // 检查是否包含查询参数
            if (route.Contains('?'))
            {
                var queryPart = route.Split('?')[1];
                var videoIdParam = queryPart.Split('&')
                    .FirstOrDefault(p => p.StartsWith("videoId=", StringComparison.OrdinalIgnoreCase));
                if (videoIdParam != null)
                {
                    var videoIdValue = videoIdParam.Substring(8); // 移除 "videoId=" 前缀
                    if (int.TryParse(videoIdValue, out int videoId))
                    {
                        Debug.WriteLine($"从查询参数中提取到视频ID: {videoId}");
                        _viewModel.Initialize(videoId);
                    }
                }
            }
            // 兼容旧的路径参数格式
            else if (route.Contains("realtime/"))
            {
                var parts = route.Split('/');
                if (parts.Length > 2 && int.TryParse(parts[2], out int videoId))
                {
                    Debug.WriteLine($"从路由路径中提取到视频ID: {videoId}");
                    _viewModel.Initialize(videoId);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"导航参数解析失败: {ex.Message}");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Cleanup();
    }
}
