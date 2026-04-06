using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using OptishotV1DOTNET.Core.Camera;
using OptishotV1DOTNET.Core.Vision;
using OptishotV1DOTNET.Features.Coaching;
using OptishotV1DOTNET.Features.Lighting;
using OptishotV1DOTNET.UI.Screens;
using OptishotV1DOTNET.ViewModels;

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
#if ANDROID
                handlers.AddHandler<CameraPreviewView, OptishotV1DOTNET.Platforms.Android.CameraPreviewHandler>();
#endif
            });

        // ── Core ────────────────────────────────────────
        builder.Services.AddSingleton<AppState>();
        builder.Services.AddSingleton<FrameAnalyzer>();

        // ── Platform camera service ─────────────────────
#if IOS
        builder.Services.AddSingleton<ICameraService, OptishotV1DOTNET.Platforms.iOS.CameraService>();
#endif
#if ANDROID
        builder.Services.AddSingleton<ICameraService, OptishotV1DOTNET.Platforms.Android.CameraService>();
#endif

        // ── Features ────────────────────────────────────
        builder.Services.AddSingleton<CoachingEngine>();
        builder.Services.AddSingleton<LightingAssistant>();

        // ── ViewModels ──────────────────────────────────
        builder.Services.AddTransient<ModeSelectionViewModel>();
        builder.Services.AddTransient<CameraViewModel>();

        // ── Pages ───────────────────────────────────────
        builder.Services.AddTransient<ModeSelectionPage>();
        builder.Services.AddTransient<CameraPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
