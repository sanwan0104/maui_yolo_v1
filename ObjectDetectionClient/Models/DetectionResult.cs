using Newtonsoft.Json;
using System;

namespace ObjectDetectionClient.Models;

public class DetectionResult
{
    [JsonProperty("detection_id")]
    public int DetectionId { get; set; }

    [JsonProperty("video_id")]
    public int VideoId { get; set; }

    [JsonProperty("object_class")]
    public string ObjectClass { get; set; } = "unknown";

    [JsonProperty("confidence")]
    public float Confidence { get; set; }

    [JsonProperty("bbox_x1")]
    public int BboxX1 { get; set; }

    [JsonProperty("bbox_y1")]
    public int BboxY1 { get; set; }

    [JsonProperty("bbox_x2")]
    public int BboxX2 { get; set; }

    [JsonProperty("bbox_y2")]
    public int BboxY2 { get; set; }

    [JsonProperty("frame_number")]
    public int FrameNumber { get; set; }

    [JsonProperty("detected_at")]
    public DateTime DetectedAt { get; set; } = DateTime.Now;
}