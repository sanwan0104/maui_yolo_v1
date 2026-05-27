using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObjectDetectionClient.Models;
using ObjectDetectionClient.Services;
using Microsoft.Maui.Storage;
using System.IO;

namespace ObjectDetectionClient.ViewModels;

public partial class UploadViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private FileResult _selectedFile;

    [ObservableProperty]
    private string _uploadStatus;

    [ObservableProperty]
    private bool _isUploading;

    [ObservableProperty]
    private bool _showSuccessMessage;

    [ObservableProperty]
    private string _successMessage;

    public UploadViewModel(ApiService apiService)
    {
        _apiService = apiService;
        Title = "上传视频";
        UploadStatus = "请选择视频文件";
    }

    [RelayCommand]
    private async Task SelectFile()
    {
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv" } }
                });

            var options = new PickOptions
            {
                PickerTitle = "请选择视频文件",
                FileTypes = customFileType,
            };

            SelectedFile = await FilePicker.Default.PickAsync(options);
            if (SelectedFile != null)
            {
                UploadStatus = $"已选择: {SelectedFile.FileName}";
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("错误", $"选择文件失败: {ex.Message}", "确定");
        }
    }

    [RelayCommand]
    private async Task UploadFile()
    {
        if (SelectedFile == null)
        {
            await Application.Current.MainPage.DisplayAlert("提示", "请先选择文件", "确定");
            return;
        }

        try
        {
            IsUploading = true;
            UploadStatus = "上传中...";

            // 将文件保存到临时位置，使用FullPath获取正确的文件名
            var fileName = Path.GetFileName(SelectedFile.FullPath);
            var tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            using (var stream = await SelectedFile.OpenReadAsync())
            using (var fileStream = File.Create(tempPath))
            {
                await stream.CopyToAsync(fileStream);
            }

            // 上传到服务器（使用实时处理接口）
            var result = await _apiService.UploadVideoRealtimeAsync(tempPath, fileName);

            if (result != null)
            {
                ShowSuccessMessage = true;
                SuccessMessage = $"{result.Filename} 上传成功！\n视频ID: {result.VideoId}\n{result.Message}";
                UploadStatus = "上传完成，正在跳转到处理页面...";

                // 清空选择
                SelectedFile = null;

                // 延迟后跳转到实时处理页面
                await Task.Delay(1000);
                ShowSuccessMessage = false;

                // 导航到实时处理页面 - 使用查询参数传递 videoId
                await Shell.Current.GoToAsync($"realtime?videoId={result.VideoId}");
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("错误", "上传失败", "确定");
                UploadStatus = "上传失败";
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("错误", $"上传失败: {ex.Message}", "确定");
            UploadStatus = "上传失败";
        }
        finally
        {
            IsUploading = false;
        }
    }

   
}