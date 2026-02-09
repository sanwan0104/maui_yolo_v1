using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using ObjectDetectionClient.Services;
using ObjectDetectionClient.ViewModels;
using ObjectDetectionClient.Views;
using ObjectDetectionClient.Converters;

namespace ObjectDetectionClient;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // 注册服务
        builder.Services.AddSingleton<ApiService>();

        // 注册转换器
        builder.Services.AddSingleton<StatusToColorConverter>();
        builder.Services.AddSingleton<BboxConverter>();
        builder.Services.AddSingleton<IntToBoolConverter>();
        builder.Services.AddSingleton<BoolInverseConverter>();
        builder.Services.AddSingleton<NotNullConverter>();

        // 注册ViewModels
        builder.Services.AddTransient<VideosViewModel>();
        builder.Services.AddTransient<VideoDetailViewModel>();
        builder.Services.AddTransient<UploadViewModel>();
        builder.Services.AddTransient<RealTimeProcessingViewModel>();

        // 注册Views
        builder.Services.AddTransient<VideosPage>();
        builder.Services.AddTransient<VideoDetailPage>();
        builder.Services.AddTransient<UploadPage>();
        builder.Services.AddTransient<RealTimeProcessingPage>();

        return builder.Build();
    }
}