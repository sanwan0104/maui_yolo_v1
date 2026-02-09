using System;
using Newtonsoft.Json;

namespace ObjectDetectionClient.Models;

public class Video
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("filename")]
    public string Filename { get; set; } = "未命名视频";

    [JsonProperty("filepath")]
    public string Filepath { get; set; } = "";

    [JsonProperty("status")]
    public string Status { get; set; } = "pending";

    [JsonProperty("upload_time")]
    public DateTime? UploadTime { get; set; }
}

public class UploadVideoResponse
{
    [JsonProperty("id")]
    public int VideoId { get; set; }

    [JsonProperty("filename")]
    public string Filename { get; set; } = "";

    [JsonProperty("message")]
    public string Message { get; set; } = "";
}

public class ProcessingStatusResponse
{
    [JsonProperty("processing")]
    public bool Processing { get; set; }

    [JsonProperty("progress")]
    public double Progress { get; set; }

    [JsonProperty("current_frame")]
    public int CurrentFrame { get; set; }

    [JsonProperty("total_frames")]
    public int TotalFrames { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = "";

    [JsonProperty("fps")]
    public double Fps { get; set; }
}