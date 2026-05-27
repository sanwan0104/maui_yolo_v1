using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObjectDetectionClient.Models;
using ObjectDetectionClient.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ObjectDetectionClient.ViewModels;

public partial class VideoDetailViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private Video _video = new();

    [ObservableProperty]
    private ObservableCollection<DetectionResult> _detectionResults = new();

    private int _videoId = 0;

    public VideoDetailViewModel(ApiService apiService)
    {
        _apiService = apiService;
        Title = "视频详情";
    }

    public async Task Initialize(int videoId)
    {
        try
        {
            if (videoId <= 0)
            {
                Debug.WriteLine($"无效的VideoId: {videoId}");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.DisplayAlert("错误", "无效的视频ID", "确定");
                    await GoBack();
                });
                return;
            }

            _videoId = videoId;
            Debug.WriteLine($"初始化视频详情页，VideoId: {_videoId}");

            await LoadVideoDetail();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"初始化失败: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlert("错误", $"初始化失败: {ex.Message}", "确定");
            });
        }
    }

    [RelayCommand]
    private async Task LoadVideoDetail()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Debug.WriteLine($"开始加载视频详情，ID: {_videoId}");

            // 获取视频信息
            var videoDetail = await _apiService.GetVideoDetailAsync(_videoId);
            if (videoDetail != null)
            {
                Video = videoDetail;
                Debug.WriteLine($"已加载视频详情: ID={Video.Id}, Filename={Video.Filename}, Status={Video.Status}");
            }
            else
            {
                Debug.WriteLine("获取视频详情失败，返回null");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.DisplayAlert("错误", "获取视频详情失败", "确定");
                    await GoBack();
                });
                return;
            }

            // 加载检测结果
            var results = await _apiService.GetVideoResultsAsync(_videoId);
            Debug.WriteLine($"获取到 {results.Count} 个检测结果");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DetectionResults.Clear();
                foreach (var result in results)
                {
                    DetectionResults.Add(result);
                }

                Debug.WriteLine($"检测结果加载完成，共 {DetectionResults.Count} 个");
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载视频详情失败: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlert("错误", $"加载视频详情失败: {ex.Message}", "确定");
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshVideoDetail()
    {
        Debug.WriteLine("手动刷新视频详情");
        await LoadVideoDetail();
    }

    [RelayCommand]
    private async Task GoBack()
    {
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"返回失败: {ex.Message}");
            await Shell.Current.GoToAsync("//VideosPage");
        }
    }
}