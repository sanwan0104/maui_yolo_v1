using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObjectDetectionClient.Models;
using ObjectDetectionClient.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Timer = System.Timers.Timer;

namespace ObjectDetectionClient.ViewModels;

public partial class RealTimeProcessingViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private int _videoId;
    private Timer _statusTimer;

    [ObservableProperty]
    private string _processingStatus = "准备中";

    [ObservableProperty]
    private double _processingProgress;

    [ObservableProperty]
    private int _currentFrame;

    [ObservableProperty]
    private int _totalFrames;

    [ObservableProperty]
    private double _processingFps;

    [ObservableProperty]
    private bool _isDownloadEnabled = false;

    [ObservableProperty]
    private ObservableCollection<DetectionResult> _detectionResults = new();

    [ObservableProperty]
    private Video _videoInfo;

    [ObservableProperty]
    private string _videoStreamUrl;

    public RealTimeProcessingViewModel(ApiService apiService)
    {
        _apiService = apiService;
        Title = "实时处理";
    }

    public void Initialize(int videoId)
    {
        _videoId = videoId;
        Debug.WriteLine($"初始化实时处理页面，视频ID: {videoId}");
        
        // 直接生成HTML内容
        var streamUrl = $"http://localhost:5000/api/videos/{videoId}/stream";
        var htmlContent = $"<!DOCTYPE html>\n<html>\n<head>\n    <title>视频流</title>\n    <style>\n        body {{ margin: 0; padding: 0; background-color: black; }}\n        #videoStream {{ width: 100%; height: 100%; object-fit: contain; }}\n    </style>\n</head>\n<body>\n    <img id=\"videoStream\" src=\"{streamUrl}\" alt=\"视频流\">\n</body>\n</html>";
        
        // 使用data URL来加载HTML
        VideoStreamUrl = $"data:text/html;charset=utf-8,{Uri.EscapeDataString(htmlContent)}";
        Debug.WriteLine($"视频流 URL: Data URL (包含HTML内容)");
    }

    public async Task LoadDataAsync()
    {
        try
        {
            // 检查 _videoId 是否为 0，如果是 0，尝试获取最新的视频 ID
            if (_videoId == 0)
            {
                Debug.WriteLine("视频 ID 为 0，尝试获取最新的视频 ID");
                var videos = await _apiService.GetVideosAsync();
                if (videos != null && videos.Count > 0)
                {
                    // 获取最新的视频 ID
                    _videoId = videos.Max(v => v.Id);
                    Debug.WriteLine($"获取到最新的视频 ID: {_videoId}");
                    
                    // 直接生成HTML内容
                    var streamUrl = $"http://localhost:5000/api/videos/{_videoId}/stream";
                    var htmlContent = $"<!DOCTYPE html>\n<html>\n<head>\n    <title>视频流</title>\n    <style>\n        body {{ margin: 0; padding: 0; background-color: black; }}\n        #videoStream {{ width: 100%; height: 100%; object-fit: contain; }}\n    </style>\n</head>\n<body>\n    <img id=\"videoStream\" src=\"{streamUrl}\" alt=\"视频流\">\n</body>\n</html>";
                    
                    // 使用data URL来加载HTML
                    VideoStreamUrl = $"data:text/html;charset=utf-8,{Uri.EscapeDataString(htmlContent)}";
                    Debug.WriteLine($"更新视频流 URL: Data URL (包含HTML内容)");
                }
            }

            // 获取视频信息
            VideoInfo = await _apiService.GetVideoDetailAsync(_videoId);
            if (VideoInfo != null)
            {
                Title = $"处理: {VideoInfo.Filename}";
            }

            // 启动状态更新定时器
            StartStatusTimer();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载数据失败: {ex.Message}");
        }
    }

    private void StartStatusTimer()
    {
        _statusTimer = new Timer(2000); // 每2秒更新一次状态
        _statusTimer.Elapsed += async (sender, e) => await UpdateProcessingStatusAsync();
        _statusTimer.Start();
    }

    private async Task UpdateProcessingStatusAsync()
    {
        try
        {
            // 获取处理状态
            var status = await _apiService.GetProcessingStatusAsync(_videoId);
            if (status != null)
            {
                ProcessingStatus = status.Status;
                ProcessingProgress = status.Progress;
                CurrentFrame = status.CurrentFrame;
                TotalFrames = status.TotalFrames;
                ProcessingFps = status.Fps;

                // 检查是否处理完成
                if (status.Status == "completed")
                {
                    IsDownloadEnabled = true;
                    await LoadDetectionResultsAsync();
                    _statusTimer?.Stop();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新状态失败: {ex.Message}");
        }
    }

    private async Task LoadDetectionResultsAsync()
    {
        try
        {
            var results = await _apiService.GetVideoResultsAsync(_videoId);
            if (results != null && results.Count > 0)
            {
                DetectionResults.Clear();
                foreach (var result in results)
                {
                    DetectionResults.Add(result);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载检测结果失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DownloadProcessedVideo()
    {
        try
        {
            // 实现下载处理后视频的逻辑
            await _apiService.DownloadProcessedVideoAsync(_videoId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"下载视频失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task Back()
    {
        await Shell.Current.GoToAsync("..");
    }

    public void Cleanup()
    {
        _statusTimer?.Stop();
        _statusTimer?.Dispose();
    }
}
