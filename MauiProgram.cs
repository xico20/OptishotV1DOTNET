using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using OptishotV1DOTNET.Controls;
using OptishotV1DOTNET.Services;
using OptishotV1DOTNET.ViewModels;
using OptishotV1DOTNET.Views;

namespace OptishotV1DOTNET;

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
            })
            .ConfigureMauiHandlers(handlers =>
            {
#if IOS
                handlers.AddHandler<CameraPreviewView, OptishotV1DOTNET.Platforms.iOS.CameraPreviewHandler>();
#endif
            });

        // Core services
        builder.Services.AddSingleton<AppState>();
        builder.Services.AddSingleton<FrameAnalyzer>();
        builder.Services.AddSingleton<CoachingEngine>();
        builder.Services.AddSingleton<LightingAssistant>();

#if IOS
        builder.Services.AddSingleton<ICameraService, OptishotV1DOTNET.Platforms.iOS.CameraService>();
#endif

        // ViewModels
        builder.Services.AddTransient<ModeSelectionViewModel>();
        builder.Services.AddTransient<CameraViewModel>();

        // Pages
        builder.Services.AddTransient<ModeSelectionPage>();
        builder.Services.AddTransient<CameraPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
