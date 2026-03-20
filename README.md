# MAUI 视频目标识别系统 - 完整项目解析

## 一、项目整体架构

这是一个基于 **.NET MAUI + Flask + YOLOv11** 的视频目标识别系统，采用 **MVVM 架构模式**。

### 技术栈

| 层级     | 技术                         | 职责               |
| -------- | ---------------------------- | ------------------ |
| 前端     | .NET MAUI                    | 跨平台应用框架     |
| 前端架构 | MVVM + CommunityToolkit.Mvvm | 视图模型双向绑定   |
| 后端     | Flask (Python)               | RESTful API 服务   |
| 数据库   | MySQL                        | 视频和检测结果存储 |
| AI模型   | YOLOv11 (Ultralytics)        | 目标检测推理       |
| 视频处理 | OpenCV                       | 视频编解码和帧处理 |

### 项目结构

```
maui_yolo_v1/
├── ObjectDetectionClient/          # MAUI 前端应用
│   ├── Models/                    # 数据模型
│   │   ├── Video.cs              # 视频数据模型
│   │   └── DetectionResult.cs     # 检测结果模型
│   ├── Services/                  # 服务层
│   │   └── ApiService.cs         # HTTP API 客户端
│   ├── Converters/                # 数据转换器
│   │   ├── Converters.cs        # 状态转颜色、边界框格式化
│   │   └── BoolConverters.cs     # 布尔值转换器
│   ├── ViewModels/                # 视图模型层
│   │   ├── BaseViewModel.cs      # ViewModel 基类
│   │   ├── VideosViewModel.cs    # 视频列表
│   │   ├── UploadViewModel.cs    # 文件上传
│   │   ├── RealTimeProcessingViewModel.cs  # 实时处理
│   │   └── VideoDetailViewModel.cs         # 视频详情
│   ├── Views/                     # 视图层（XAML 界面）
│   │   ├── VideosPage.xaml(.cs)  # 视频列表页
│   │   ├── UploadPage.xaml(.cs)  # 上传页
│   │   ├── RealTimeProcessingPage.xaml(.cs)  # 实时处理页
│   │   └── VideoDetailPage.xaml(.cs)         # 视频详情页
│   ├── App.xaml(.cs)             # 应用入口
│   ├── AppShell.xaml(.cs)        # 导航容器
│   └── MauiProgram.cs            # 依赖注入配置
│
└── ObjectDetection_API/           # Flask 后端
    └── app.py                    # API 服务主文件
```

---

## 二、应用入口与配置

### 1. App.xaml.cs - 应用主类

```csharp
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();  // 设置主页面为 Shell 导航容器
    }
}
```

**作用**: 应用的启动入口，创建 Shell 作为根容器。

---

### 2. MauiProgram.cs - 依赖注入配置中心

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()  // 启用 MVVM 工具包
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // 单例服务（整个应用生命周期只创建一次）
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<StatusToColorConverter>();
        
        // 瞬态服务（每次请求创建新实例）
        builder.Services.AddTransient<VideosViewModel>();
        builder.Services.AddTransient<UploadViewModel>();
        builder.Services.AddTransient<RealTimeProcessingViewModel>();
        
        // 瞬态视图（每次导航创建新页面）
        builder.Services.AddTransient<VideosPage>();
        builder.Services.AddTransient<UploadPage>();

        return builder.Build();
    }
}
```

**三种注册模式**:

- **Singleton（单例）**: `ApiService` 和转换器在整个应用中只创建一个实例
- **Transient（瞬态）**: 每次请求时创建新的 ViewModel 和 View 实例
- **Scoped（ scoped）**: 在特定范围内只创建一个实例

---

### 3. AppShell.xaml - 导航容器

```xml
<Shell>
    <!-- 底部 Tab 栏 -->
    <TabBar>
        <Tab Title="视频列表" Icon="home.png">
            <ShellContent Route="VideosPage" />
        </Tab>
        <Tab Title="上传" Icon="upload.png">
            <ShellContent Route="UploadPage" />
        </Tab>
    </TabBar>

    <!-- 独立路由（不显示在 TabBar） -->
    <ShellContent Route="VideoDetailPage" />
    <ShellContent Route="realtime" />
</Shell>
```

**AppShell.xaml.cs**:

```csharp
public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        // 注册路由
        Routing.RegisterRoute("VideoDetailPage", typeof(VideoDetailPage));
        Routing.RegisterRoute("realtime", typeof(RealTimeProcessingPage));
    }
}
```

**导航结构**:

- **TabBar**: 底部标签栏，包含"视频列表"和"上传"两个主要页面
- **独立路由**: VideoDetailPage 和 realtime 通过编程导航，不在 TabBar 显示

---

## 三、数据模型层（Models）

### 1. Video.cs - 视频数据模型

```csharp
public class Video
{
    [JsonProperty("id")]
    public int Id { get; set; }              // 视频唯一 ID
    
    [JsonProperty("filename")]
    public string Filename { get; set; }     // 文件名
    
    [JsonProperty("filepath")]
    public string Filepath { get; set; }     // 存储路径
    
    [JsonProperty("status")]
    public string Status { get; set; }       // 状态：pending/processing/completed/failed
    
    [JsonProperty("upload_time")]
    public DateTime? UploadTime { get; set; } // 上传时间
}

public class ProcessingStatusResponse
{
    [JsonProperty("processing")]
    public bool Processing { get; set; }
    
    [JsonProperty("progress")]
    public double Progress { get; set; }     // 0-100%
    
    [JsonProperty("current_frame")]
    public int CurrentFrame { get; set; }    // 当前帧
    
    [JsonProperty("fps")]
    public double Fps { get; set; }         // 处理速度
}
```

**[JsonProperty]** 特性用于指定 JSON 键名，实现前后端数据映射。

---

### 2. DetectionResult.cs - 检测结果模型

```csharp
public class DetectionResult
{
    [JsonProperty("object_class")]
    public string ObjectClass { get; set; }  // 检测类别（person/car/bus）
    
    [JsonProperty("confidence")]
    public float Confidence { get; set; }    // 置信度（0-1）
    
    [JsonProperty("bbox_x1")]
    public int BboxX1 { get; set; }          // 边界框坐标
    [JsonProperty("bbox_y1")]
    public int BboxY1 { get; set; }
    [JsonProperty("bbox_x2")]
    public int BboxX2 { get; set; }
    [JsonProperty("bbox_y2")]
    public int BboxY2 { get; set; }
    
    [JsonProperty("frame_number")]
    public int FrameNumber { get; set; }     // 帧号
}
```

---

## 四、服务层（Services）

### ApiService.cs - HTTP API 客户端

```csharp
public class ApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:5000";  // Flask 后端地址

    public ApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    // 获取视频列表
    public async Task<List<Video>> GetVideosAsync()
    {
        var response = await _httpClient.GetAsync("/api/videos");
        var jsonString = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<Video>>>(jsonString);
        return apiResponse?.Data ?? new List<Video>();
    }

    // 上传视频（实时处理）
    public async Task<UploadVideoResponse> UploadVideoRealtimeAsync(string filePath, string fileName)
    {
        using var form = new MultipartFormDataContent();
        using var fileStream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(fileStream);
        form.Add(fileContent, "file", fileName);  // 保留原始文件名
        
        var response = await _httpClient.PostAsync("/api/upload_realtime", form);
        var apiResponse = JsonConvert.DeserializeObject<ApiResponse<UploadVideoResponse>>(
            await response.Content.ReadAsStringAsync());
        return apiResponse?.Data;
    }

    // 获取处理状态
    public async Task<ProcessingStatusResponse> GetProcessingStatusAsync(int videoId)
    {
        var response = await _httpClient.GetAsync($"/api/videos/{videoId}/process_status");
        var apiResponse = JsonConvert.DeserializeObject<ApiResponse<ProcessingStatusResponse>>(
            await response.Content.ReadAsStringAsync());
        return apiResponse?.Data;
    }

    // 下载处理后的视频
    public async Task<bool> DownloadProcessedVideoAsync(int videoId)
    {
        var response = await _httpClient.GetAsync($"/api/videos/{videoId}/processed_video_file");
        if (response.IsSuccessStatusCode)
        {
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                                        $"processed_{videoId}.mp4");
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream);
            return true;
        }
        return false;
    }
}
```

**核心职责**:

- 封装所有 HTTP 请求（GET/POST）
- 处理文件上传（MultipartFormDataContent）
- JSON 序列化/反序列化
- 统一错误处理和超时管理

---

## 五、数据转换器（Converters）

### Converters.cs

```csharp
// 状态 → 颜色 转换器
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status?.ToLower() switch
            {
                "pending" => Color.FromArgb("#FF9800"),    // 橙色
                "processing" => Color.FromArgb("#2196F3"), // 蓝色
                "completed" => Color.FromArgb("#4CAF50"),  // 绿色
                "failed" => Color.FromArgb("#F44336"),     // 红色
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }
}

// 边界框格式化转换器
public class BboxConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DetectionResult result)
        {
            return $"({result.BboxX1}, {result.BboxY1}) - ({result.BboxX2}, {result.BboxY2})";
        }
        return string.Empty;
    }
}
```

### BoolConverters.cs

```csharp
// 非空 → 布尔值 转换器
public class NotNullConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }
}

// 布尔值取反转换器
public class BoolInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return true;
    }
}
```

**使用示例**:

让我检查一下 Converters 是否真的被使用：


​            
toolName: search_by_regex
​            
status: success
​          
query: StatusToColorConverter|NotNullConverter|IntToBoolConverter|BoolInverseConverter|BboxConverter
​          

## Converters 使用情况分析

从搜索结果来看，Converters 的使用情况如下：

### ✅ 正在使用的（3个）

| 转换器                     | 注册位置       | 使用位置                              | 功能                                         |
| -------------------------- | -------------- | ------------------------------------- | -------------------------------------------- |
| **StatusToColorConverter** | MauiProgram.cs | VideosPage.xaml, VideoDetailPage.xaml | 状态→颜色（如 pending 橙色，completed 绿色） |
| **NotNullConverter**       | MauiProgram.cs | UploadPage.xaml                       | 文件选择后显示"已选择文件"提示框             |
| **BboxConverter**          | MauiProgram.cs | VideoDetailPage.xaml                  | 边界框坐标格式化显示                         |

### ❌ 未使用的（2个）

| 转换器                   | 注册位置       | 使用位置 | 状态                         |
| ------------------------ | -------------- | -------- | ---------------------------- |
| **IntToBoolConverter**   | MauiProgram.cs | ❌ 无     | 已注册但未在任何 XAML 中使用 |
| **BoolInverseConverter** | MauiProgram.cs | ❌ 无     | 已注册但未在任何 XAML 中使用 |

---

### 证据

**VideosPage.xaml** 中的使用：

```xml
<Label.TextColor>
    <Binding Path="Status" Converter="{StaticResource StatusToColorConverter}" />
</Label.TextColor>
```

**UploadPage.xaml** 中的使用：

```xml
<Frame IsVisible="{Binding SelectedFile, Converter={x:StaticResource NotNullConverter}}"
       ...>
```

---

### 结论

**IntToBoolConverter** 和 **BoolInverseConverter** 确实是多余的：

- 在 `MauiProgram.cs` 中注册了
- 但从未在任何 XAML 文件中使用

如果不需要这两个功能，可以从 `MauiProgram.cs` 中删除对应的注册代码。不过它们存在也不会影响功能，只是多余的代码而已。

---

## 六、视图模型层（ViewModels）

### 1. VideosViewModel.cs - 视频列表

```csharp
public partial class VideosViewModel : BaseViewModel
{
    private readonly ApiService _apiService;

    [ObservableProperty]
    private ObservableCollection<Video> _videos = new();  // 自动生成属性

    public VideosViewModel(ApiService apiService)
    {
        _apiService = apiService;
    }

    [RelayCommand]  // 自动生成 LoadVideosCommand 命令
    private async Task LoadVideos()
    {
        if (IsBusy) return;
        
        try
        {
            IsBusy = true;
            var videos = await _apiService.GetVideosAsync();
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Videos.Clear();
                foreach (var video in videos)
                    Videos.Add(video);
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
        if (video == null) return;
        // 使用查询参数传递视频 ID
        await Shell.Current.GoToAsync($"VideoDetailPage?VideoId={video.Id}");
    }
}
```

**[ObservableProperty]** 特性会在编译时自动生成 `Videos` 属性和 `VideosChanged` 事件，实现数据变更自动通知 UI。

**[RelayCommand]** 特性会自动生成可绑定的命令属性，支持异步执行。

---

### 2. UploadViewModel.cs - 文件上传

```csharp
public partial class UploadViewModel : BaseViewModel
{
    [ObservableProperty]
    private FileResult _selectedFile;

    [ObservableProperty]
    private string _uploadStatus = "请选择视频文件";

    [RelayCommand]
    private async Task SelectFile()
    {
        var options = new PickOptions
        {
            PickerTitle = "请选择视频文件",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".mp4", ".avi", ".mov", ".mkv" } }
            })
        };

        SelectedFile = await FilePicker.Default.PickAsync(options);
        if (SelectedFile != null)
        {
            UploadStatus = $"已选择：{SelectedFile.FileName}";
        }
    }

    [RelayCommand]
    private async Task UploadFile()
    {
        if (SelectedFile == null) return;

        try
        {
            IsUploading = true;
            // 使用 FullPath 获取完整文件名（解决中文截断问题）
            var fileName = Path.GetFileName(SelectedFile.FullPath);
            var tempPath = Path.Combine(FileSystem.CacheDirectory, fileName);
            
            // 保存到临时目录
            using (var stream = await SelectedFile.OpenReadAsync())
            using (var fileStream = File.Create(tempPath))
            {
                await stream.CopyToAsync(fileStream);
            }

            // 上传到服务器
            var result = await _apiService.UploadVideoRealtimeAsync(tempPath, fileName);

            if (result != null)
            {
                // 导航到实时处理页面
                await Shell.Current.GoToAsync($"realtime?videoId={result.VideoId}");
            }
        }
        finally
        {
            IsUploading = false;
        }
    }
}
```

**关键问题解决**: 使用 `FullPath` 而非 `FileName` 解决中文文件名被截断的问题。

---

### 3. RealTimeProcessingViewModel.cs - 实时处理（核心）

```csharp
public partial class RealTimeProcessingViewModel : BaseViewModel
{
    private readonly ApiService _apiService;
    private int _videoId;
    private Timer _statusTimer;  // 定时器：每 2 秒轮询状态

    [ObservableProperty]
    private string _processingStatus;

    [ObservableProperty]
    private double _processingProgress;

    [ObservableProperty]
    private double _processingFps;

    [ObservableProperty]
    private string _videoStreamUrl;  // Data URL 格式

    // 初始化（接收 videoId）
    public void Initialize(int videoId)
    {
        _videoId = videoId;
        
        // 生成 HTML 内容用于 WebView 显示 MJPEG 流
        var streamUrl = $"http://localhost:5000/api/videos/{videoId}/stream";
        var htmlContent = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ margin: 0; background-color: black; }}
                    #videoStream {{ width: 100%; height: 100%; }}
                </style>
            </head>
            <body>
                <img src=""{streamUrl}"" />
            </body>
            </html>";
        
        // Data URL 格式：解决 WebView 跨域问题
        VideoStreamUrl = $"data:text/html;charset=utf-8,{Uri.EscapeDataString(htmlContent)}";
    }

    public async Task LoadDataAsync()
    {
        // 回退逻辑：如果 videoId 为 0，获取最新视频
        if (_videoId == 0)
        {
            var videos = await _apiService.GetVideosAsync();
            if (videos?.Count > 0)
            {
                _videoId = videos.Max(v => v.Id);
                // 重新生成 VideoStreamUrl...
            }
        }

        VideoInfo = await _apiService.GetVideoDetailAsync(_videoId);
        StartStatusTimer();  // 启动定时器
    }

    private void StartStatusTimer()
    {
        _statusTimer = new Timer(2000);  // 每 2 秒
        _statusTimer.Elapsed += async (sender, e) => await UpdateProcessingStatusAsync();
        _statusTimer.Start();
    }

    private async Task UpdateProcessingStatusAsync()
    {
        var status = await _apiService.GetProcessingStatusAsync(_videoId);
        if (status != null)
        {
            ProcessingStatus = status.Status;
            ProcessingProgress = status.Progress;
            ProcessingFps = status.Fps;

            if (status.Status == "completed")
            {
                IsDownloadEnabled = true;
                await LoadDetectionResultsAsync();
                _statusTimer?.Stop();  // 处理完成，停止轮询
            }
        }
    }

    public void Cleanup()
    {
        _statusTimer?.Stop();
        _statusTimer?.Dispose();
    }
}
```

**核心技术**:

- **Timer 定时轮询**: 每 2 秒从后端获取处理状态
- **Data URL 技术**: 将 HTML 嵌入 URL 解决 WebView 跨域限制
- **ObservableProperty**: 属性变更自动通知 UI 更新

---

### 4. RealTimeProcessingPage.xaml.cs - 页面后台代码

```csharp
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

        // 解析导航 URL 中的 videoId 参数
        var route = Shell.Current.CurrentState.Location.OriginalString;
        
        if (route.Contains('?'))
        {
            // 解析查询参数格式：realtime?videoId=5
            var queryPart = route.Split('?')[1];
            var videoIdParam = queryPart.Split('&')
                .FirstOrDefault(p => p.StartsWith("videoId="));
            
            if (videoIdParam != null)
            {
                var videoIdValue = videoIdParam.Substring(8);
                if (int.TryParse(videoIdValue, out int videoId))
                {
                    _viewModel.Initialize(videoId);
                }
            }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadDataAsync();  // 页面显示时加载数据
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Cleanup();  // 页面消失时清理定时器
    }
}
```

**作用**: 

- 解析导航传递的 videoId 参数
- 管理页面生命周期（OnAppearing/OnDisappearing）
- 连接 ViewModel 和 View

---

## 七、视图层（Views）

### 1. VideosPage.xaml - 视频列表页

```xml
<ContentPage Title="视频列表">
    <Grid>
        <!-- 工具栏 -->
        <StackLayout Padding="10" BackgroundColor="#F5F5F5">
            <Grid>
                <Label Text="视频列表" FontSize="24" FontAttributes="Bold" />
                <Button Text="刷新" Command="{Binding LoadVideosCommand}" 
                        BackgroundColor="#4CAF50" HorizontalOptions="End" />
            </Grid>
        </StackLayout>

        <!-- 视频列表 -->
        <CollectionView ItemsSource="{Binding Videos}" Margin="0,60,0,0">
            <CollectionView.ItemTemplate>
                <DataTemplate>
                    <Border StrokeShape="RoundRectangle 8" Margin="10,5" Padding="10">
                        <Grid>
                            <!-- 点击手势：导航到详情页 -->
                            <Grid.GestureRecognizers>
                                <TapGestureRecognizer 
                                    Command="{Binding Source={RelativeSource AncestorType={x:Type ContentPage}},
                                              Path=BindingContext.NavigateToVideoDetailCommand}"
                                    CommandParameter="{Binding .}" />
                            </Grid.GestureRecognizers>

                            <Label Text="{Binding Filename}" FontAttributes="Bold" />
                            
                            <HorizontalStackLayout>
                                <Label Text="{Binding Status}">
                                    <Label.TextColor>
                                        <Binding Path="Status" 
                                                 Converter="{StaticResource StatusToColorConverter}" />
                                    </Label.TextColor>
                                </Label>
                            </HorizontalStackLayout>
                        </Grid>
                    </Border>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>

        <!-- 加载指示器 -->
        <ActivityIndicator IsRunning="{Binding IsBusy}" />
    </Grid>
</ContentPage>
```

**关键技术**:

- **CollectionView**: 高性能列表控件
- **RelativeSource 绑定**: 从 DataTemplate 中访问父级页面的 BindingContext
- **TapGestureRecognizer**: 为列表项添加点击事件

---

### 2. UploadPage.xaml - 上传页

```xml
<ContentPage Title="上传视频">
    <Frame Margin="20" VerticalOptions="Center" Padding="30">
        <StackLayout Spacing="20">
            <!-- 虚线边框选择区域 -->
            <Border Stroke="#2196F3" StrokeThickness="2" StrokeDashArray="5,5"
                    StrokeShape="RoundRectangle 10" HeightRequest="200">
                <Grid>
                    <StackLayout HorizontalOptions="Center" VerticalOptions="Center">
                        <Button Text="选择视频文件" Command="{Binding SelectFileCommand}"
                                BackgroundColor="#2196F3" />
                        <Label Text="{Binding UploadStatus}" TextColor="Gray" />
                    </StackLayout>

                    <!-- 已选择文件（使用转换器控制可见性） -->
                    <Frame IsVisible="{Binding SelectedFile, 
                                     Converter={x:StaticResource NotNullConverter}}"
                           BackgroundColor="#E8F5E9" VerticalOptions="End">
                        <Label Text="{Binding SelectedFile.FileName}" FontAttributes="Bold" />
                    </Frame>
                </Grid>
            </Border>

            <!-- 上传按钮 -->
            <Button Text="上传" Command="{Binding UploadFileCommand}"
                    IsEnabled="{Binding SelectedFile, 
                                Converter={x:StaticResource NotNullConverter}}"
                    BackgroundColor="#4CAF50" />

            <ActivityIndicator IsRunning="{Binding IsUploading}" />
        </StackLayout>
    </Frame>
</ContentPage>
```

---

### 3. RealTimeProcessingPage.xaml - 实时处理页

```xml
<ContentPage Title="实时处理">
    <VerticalStackLayout Padding="16" Spacing="16">
        <!-- 视频播放区域（WebView） -->
        <Frame WidthRequest="320" HeightRequest="240">
            <WebView Source="{Binding VideoStreamUrl}" />
        </Frame>
        
        <!-- 处理状态 -->
        <Frame Padding="12">
            <VerticalStackLayout Spacing="8">
                <HorizontalStackLayout>
                    <Label Text="状态：" FontAttributes="Bold" />
                    <Label Text="{Binding ProcessingStatus}" />
                </HorizontalStackLayout>
                
                <HorizontalStackLayout>
                    <Label Text="处理速度：" FontAttributes="Bold" />
                    <Label Text="{Binding ProcessingFps, StringFormat='{0:F1} 帧/秒'}" />
                </HorizontalStackLayout>
                
                <ProgressBar Progress="{Binding ProcessingProgress}" />
            </VerticalStackLayout>
        </Frame>
        
        <!-- 检测结果 -->
        <CollectionView ItemsSource="{Binding DetectionResults}" HeightRequest="200">
            <CollectionView.ItemTemplate>
                <DataTemplate>
                    <Frame BorderColor="LightGreen" Padding="8">
                        <HorizontalStackLayout>
                            <Label Text="{Binding ObjectClass}" FontAttributes="Bold" WidthRequest="100" />
                            <Label Text="{Binding Confidence, StringFormat='{0:P0}'}" WidthRequest="60" />
                        </HorizontalStackLayout>
                    </Frame>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>
        
        <!-- 按钮 -->
        <HorizontalStackLayout Spacing="12">
            <Button Text="下载" Command="{Binding DownloadCommand}" 
                    IsEnabled="{Binding IsDownloadEnabled}" />
            <Button Text="返回" Command="{Binding BackCommand}" />
        </HorizontalStackLayout>
    </VerticalStackLayout>
</ContentPage>
```

**核心技术**: WebView 通过 Data URL 加载 HTML，HTML 中的 `<img>` 标签加载 MJPEG 视频流。

---

## 八、后端 API（Flask app.py）

### 核心 API 接口

```python
# 1. 上传视频并启动实时处理
@app.route('/api/upload_realtime', methods=['POST'])
def upload_and_process_realtime():
    file = request.files['file']
    filename = file.filename  # 保留原始文件名（支持中文）
    filepath = os.path.join(app.config['UPLOAD_FOLDER'], filename)
    file.save(filepath)

    video_id = save_videos_info(filename, filepath)
    processor = VideoProcessor(filepath, video_id, model)
    processing_tasks[video_id] = processor

    # 异步线程处理（不阻塞 API 响应）
    thread = threading.Thread(target=processor.process_realtime)
    thread.start()

    return success_response({
        'video_id': video_id,
        'filename': filename,
        'message': '视频已上传，开始实时处理'
    })

# 2. 获取处理状态
@app.route('/api/videos/<int:video_id>/process_status', methods=['GET'])
def get_processing_status(video_id):
    if video_id in processing_tasks:
        processor = processing_tasks[video_id]
        return success_response({
            'processing': processor.processing,
            'progress': processor.get_progress(),
            'fps': processor.fps,
            'current_frame': processor.frame_count,
            'status': 'processing' if processor.processing else 'completed'
        })
    # 从数据库获取...

# 3. MJPEG 视频流
@app.route('/api/videos/<int:video_id>/stream')
def video_stream(video_id):
    def generate():
        cap = cv2.VideoCapture(video['filepath'])
        while cap.isOpened():
            ret, frame = cap.read()
            if not ret: break

            results = model(frame, verbose=False, device='cpu')[0]
            # 绘制检测框...

            ret, jpeg = cv2.imencode('.jpg', frame)
            yield (b'--frame\r\n'
                   b'Content-Type: image/jpeg\r\n\r\n' + jpeg.tobytes() + b'\r\n')
            time.sleep(0.033)  # ~30fps

    return Response(generate(), mimetype='multipart/x-mixed-replace; boundary=frame')
```

### VideoProcessor 类（核心优化逻辑）

```python
class VideoProcessor:
    def __init__(self, video_path, video_id, model):
        self.process_every_n_frames = 2  # 跳帧：每2帧处理1次
        self.last_detections = []        # 缓存上一次检测结果

    def process_frame(self, frame, use_last_detections=False):
        if use_last_detections and self.last_detections:
            detections = self.last_detections  # 跳过推理，复用缓存
        else:
            results = self.model(frame, verbose=False, device='cpu', imgsz=640)[0]
            detections = []
            for box in results.boxes:
                if box.conf[0].item() >= 0.5:  # 置信度阈值
                    detections.append({
                        'class': self.model.names[int(box.cls[0].item())],
                        'confidence': box.conf[0].item(),
                        'bbox': box.xyxy[0].tolist()
                    })
            self.last_detections = detections

        # 绘制检测框（优化：线宽1，字体0.4）
        for det in detections:
            x1, y1, x2, y2 = map(int, det['bbox'])
            cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 1)
            cv2.putText(frame, f"{det['class']}: {det['confidence']:.2f}", 
                       (x1, y1-5), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 255, 0), 1)
        return frame, detections
```

**性能优化措施**:

| 优化项     | 说明                         | 效果               |
| ---------- | ---------------------------- | ------------------ |
| 跳帧处理   | 每 2 帧检测 1 次             | 减少 50% 推理计算  |
| 结果缓存   | 跳过的帧复用上一次的检测结果 | 避免重复绘制       |
| CPU 推理   | 强制 device='cpu'            | 避免 CUDA 兼容问题 |
| 输入尺寸   | imgsz=640                    | 平衡精度和速度     |
| 置信度过滤 | 只处理 confidence >= 0.5     | 减少无效计算       |
| 绘制优化   | 线宽 1px，字体 0.4           | 减少绘制时间       |

---

## 九、关键技术总结

### MVVM 架构数据流

```
┌─────────────────────────────────────────────────────────┐
│                        View (XAML)                       │
│  <Label Text="{Binding ProcessingStatus}" />            │
│  <WebView Source="{Binding VideoStreamUrl}" />          │
└─────────────────────┬───────────────────────────────────┘
                      │ 数据绑定
                      ▼
┌─────────────────────────────────────────────────────────┐
│                   ViewModel                              │
│  [ObservableProperty] private string _processingStatus; │
│  ProcessingStatus 自动通知 View 更新                     │
│  [RelayCommand] UploadFileCommand 执行上传逻辑           │
└─────────────────────┬───────────────────────────────────┘
                      │ 依赖注入 (构造函数)
                      ▼
┌─────────────────────────────────────────────────────────┐
│                    Service                               │
│  ApiService - 封装 HTTP 请求                            │
│  GetVideosAsync() / UploadVideoRealtimeAsync()         │
└─────────────────────┬───────────────────────────────────┘
                      │ HTTP 请求
                      ▼
┌─────────────────────────────────────────────────────────┐
│                 Backend (Flask)                          │
│  /api/upload_realtime - 上传并处理                       │
│  /api/videos/{id}/stream - MJPEG 视频流                  │
│  VideoProcessor - YOLO 目标检测                          │
└─────────────────────────────────────────────────────────┘
```

### 项目核心亮点

| 功能           | 实现方式                            |
| -------------- | ----------------------------------- |
| **MVVM 架构**  | CommunityToolkit.Mvvm 简化代码      |
| **依赖注入**   | MauiProgram.cs 集中配置             |
| **导航传参**   | Shell 路由 + 查询参数               |
| **视频流显示** | WebView + Data URL + HTML img       |
| **实时状态**   | Timer 定时轮询 + ObservableProperty |
| **性能优化**   | 跳帧 + 缓存 + CPU 推理              |
| **中文文件名** | 使用 FullPath 替代 FileName         |

---

这就是整个 MAUI 视频目标识别系统的完整代码架构解析。系统通过 MVVM 模式实现了清晰的分层架构，通过依赖注入实现了松耦合，通过定时轮询实现了实时状态更新，通过 WebView + Data URL 解决了跨平台视频流显示问题。
