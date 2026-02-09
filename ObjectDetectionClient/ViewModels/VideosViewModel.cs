using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObjectDetectionClient.Models;
using ObjectDetectionClient.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace ObjectDetectionClient.ViewModels;

public partial class VideosViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<Video> _videos = new();

    public VideosViewModel(ApiService apiService)
    {
        _apiService = apiService;
        Title = "视频列表";
    }

    [RelayCommand]
    private async Task LoadVideos()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            Debug.WriteLine("开始加载视频列表...");

            var videos = await _apiService.GetVideosAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Videos.Clear();
                foreach (var video in videos)
                {
                    Videos.Add(video);
                    Debug.WriteLine($"添加视频: {video.Filename} (ID: {video.Id}, Status: {video.Status})");
                }

                Debug.WriteLine($"视频列表加载完成，共 {Videos.Count} 个视频");
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载视频失败: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlert("错误", $"加载视频失败: {ex.Message}", "确定");
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToVideoDetail(Video video)
    {
        if (video == null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlert("提示", "请选择视频", "确定");
            });
            return;
        }

        try
        {
            Debug.WriteLine($"导航到视频详情，ID: {video.Id}, Filename: {video.Filename}");

            // 使用相对路由导航，并传递参数
            await Shell.Current.GoToAsync($"VideoDetailPage?VideoId={video.Id}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"导航失败: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlert("错误", $"导航失败: {ex.Message}", "确定");
            });
        }
    }


    // 页面显示时调用的方法
    public async Task OnAppearing()
    {
        Debug.WriteLine("VideosPage OnAppearing");
        await LoadVideos();
    }
}