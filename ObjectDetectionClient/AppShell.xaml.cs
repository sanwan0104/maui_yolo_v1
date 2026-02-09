﻿namespace ObjectDetectionClient;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // 注册路由
        Routing.RegisterRoute("VideoDetailPage", typeof(Views.VideoDetailPage));
        Routing.RegisterRoute("realtime", typeof(Views.RealTimeProcessingPage));
    }
}