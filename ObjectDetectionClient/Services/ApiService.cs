using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ObjectDetectionClient.Models;
using System.Diagnostics;
using System.IO;

namespace ObjectDetectionClient.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:5000";

    public ApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<List<Video>> GetVideosAsync()
    {
        Debug.WriteLine($"API调用: 获取视频列表");

        try
        {
            var response = await _httpClient.GetAsync("/api/videos");
            Debug.WriteLine($"API响应状态: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"API响应内容: {jsonString}");

                try
                {
                    // 直接解析为Video列表，因为API返回的是包含data和message的对象
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<Video>>>(jsonString);
                    if (apiResponse?.Data != null)
                    {
                        Debug.WriteLine($"成功获取到 {apiResponse.Data.Count} 个视频");
                        return apiResponse.Data;
                    }
                    else
                    {
                        Debug.WriteLine("API响应数据为空");
                    }
                }
                catch (JsonException jsonEx)
                {
                    Debug.WriteLine($"JSON解析错误: {jsonEx.Message}");
                    // 尝试直接解析为列表
                    try
                    {
                        var videos = JsonConvert.DeserializeObject<List<Video>>(jsonString);
                        if (videos != null)
                        {
                            Debug.WriteLine($"直接解析到 {videos.Count} 个视频");
                            return videos;
                        }
                    }
                    catch (Exception ex2)
                    {
                        Debug.WriteLine($"直接解析错误: {ex2.Message}");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"API请求失败: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"API调用异常: {ex.Message}");
        }

        // 返回空列表而不是测试数据
        return new List<Video>();
    }

    public async Task<Video> GetVideoDetailAsync(int videoId)
    {
        Debug.WriteLine($"API调用: 获取视频详情 ID={videoId}");

        try
        {
            var response = await _httpClient.GetAsync($"/api/videos/{videoId}");
            Debug.WriteLine($"API响应状态: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"API响应内容: {jsonString}");

                try
                {
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse<Video>>(jsonString);
                    if (apiResponse?.Data != null)
                    {
                        Debug.WriteLine($"获取到视频详情: {apiResponse.Data.Filename}");
                        return apiResponse.Data;
                    }
                }
                catch (JsonException jsonEx)
                {
                    Debug.WriteLine($"JSON解析错误: {jsonEx.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"API请求失败: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"API调用异常: {ex.Message}");
        }

        return null;
    }

    public async Task<List<DetectionResult>> GetVideoResultsAsync(int videoId)
    {
        Debug.WriteLine($"API调用: 获取检测结果 VideoId={videoId}");

        try
        {
            var response = await _httpClient.GetAsync($"/api/videos/{videoId}/results");
            Debug.WriteLine($"API响应状态: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"API响应内容: {jsonString.Substring(0, Math.Min(jsonString.Length, 200))}");

                try
                {
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<DetectionResult>>>(jsonString);
                    return apiResponse?.Data ?? new List<DetectionResult>();
                }
                catch (JsonException jsonEx)
                {
                    Debug.WriteLine($"JSON解析错误: {jsonEx.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"API请求失败: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"API调用异常: {ex.Message}");
        }

        return new List<DetectionResult>();
    }

    public async Task<UploadVideoResponse> UploadVideoAsync(string filePath)
    {
        try
        {
            Debug.WriteLine($"上传文件: {filePath}");
            using var form = new MultipartFormDataContent();
            using var fileStream = System.IO.File.OpenRead(filePath);
            using var fileContent = new StreamContent(fileStream);

            form.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync("/api/upload", form);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<UploadVideoResponse>>(jsonString);
                return apiResponse?.Data;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"上传异常: {ex.Message}");
        }

        return null;
    }

    public async Task<UploadVideoResponse> UploadVideoRealtimeAsync(string filePath, string fileName)
    {
        try
        {
            Debug.WriteLine($"上传文件(实时处理): {filePath}, 文件名: {fileName}");
            using var form = new MultipartFormDataContent();
            using var fileStream = System.IO.File.OpenRead(filePath);
            using var fileContent = new StreamContent(fileStream);

            form.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync("/api/upload_realtime", form);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<UploadVideoResponse>>(jsonString);
                return apiResponse?.Data;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"上传异常: {ex.Message}");
        }

        return null;
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"健康检查异常: {ex.Message}");
            return false;
        }
    }

    public async Task<ProcessingStatusResponse> GetProcessingStatusAsync(int videoId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/videos/{videoId}/process_status");
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<ProcessingStatusResponse>>(jsonString);
                return apiResponse?.Data;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取处理状态异常: {ex.Message}");
        }
        return null;
    }

    public async Task<bool> DownloadProcessedVideoAsync(int videoId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/videos/{videoId}/processed_video_file");
            if (response.IsSuccessStatusCode)
            {
                // 获取文件名
                var fileName = response.Content.Headers.ContentDisposition?.FileName ?? $"processed_{videoId}.mp4";
                fileName = fileName.Trim('"');

                // 保存文件
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(filePath);
                await stream.CopyToAsync(fileStream);

                Debug.WriteLine($"视频下载成功: {filePath}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"下载视频异常: {ex.Message}");
        }
        return false;
    }
}

public class ApiResponse<T>
{
    [JsonProperty("message")]
    public string Message { get; set; } = "";

    [JsonProperty("data")]
    public T Data { get; set; }

    [JsonProperty("error")]
    public string Error { get; set; } = "";
}