using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OptishotV1DOTNET.Core.Camera;
using OptishotV1DOTNET.Core.Vision;
using OptishotV1DOTNET.Features.Coaching;
using OptishotV1DOTNET.Models;
using OptishotV1DOTNET.Utilities;

namespace OptishotV1DOTNET.ViewModels;

public partial class CameraViewModel : ObservableObject
{
    private readonly AppState _appState;
    private readonly ICameraService _cameraService;
    private readonly FrameAnalyzer _frameAnalyzer;
    private readonly CoachingEngine _coachingEngine;

    public AppState AppState => _appState;

    // Keeps tips visible for at least TipMinVisibleMs before clearing
    private CancellationTokenSource? _tipsClearCts;
    private const int TipMinVisibleMs = 2000;

    [ObservableProperty]
    private bool _showPermissionAlert;

    public CameraViewModel(
        AppState appState,
        ICameraService cameraService,
        FrameAnalyzer frameAnalyzer,
        CoachingEngine coachingEngine)
    {
        _appState = appState;
        _cameraService = cameraService;
        _frameAnalyzer = frameAnalyzer;
        _coachingEngine = coachingEngine;

        // Subscribe to coaching tips — keep each tip visible for at least TipMinVisibleMs
        _coachingEngine.TipsUpdated += (_, tips) =>
        {
            if (tips.Count > 0)
            {
                // New tips arrived: show immediately and reset the clear timer
                _appState.CurrentTips = tips;

                _tipsClearCts?.Cancel();
                _tipsClearCts = new CancellationTokenSource();
                var token = _tipsClearCts.Token;

                Task.Delay(TipMinVisibleMs, token).ContinueWith(_ =>
                {
                    if (!token.IsCancellationRequested)
                        MainThread.BeginInvokeOnMainThread(() =>
                            _appState.CurrentTips = Array.Empty<CoachingTip>());
                }, TaskScheduler.Default);
            }
        };

        // Subscribe to frame analysis results to update lighting condition
        _frameAnalyzer.ResultReady += (_, result) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _appState.LightingCondition = result.LightingCondition;
                _appState.DetectedClutter = result.ClutterObjects.ToList();
            });
        };
    }

    public async Task InitializeAsync()
    {
        bool granted = await _cameraService.RequestPermissionsAsync();
        if (!granted)
        {
            ShowPermissionAlert = true;
            return;
        }
        await _cameraService.StartAsync();
        _appState.IsSessionRunning = true;
        CoachingLogger.SessionStarted();
    }

    public async Task CleanupAsync()
    {
        _tipsClearCts?.Cancel();
        await _cameraService.StopAsync();
        _appState.IsSessionRunning = false;
        _appState.CurrentTips = Array.Empty<CoachingTip>();
        _appState.LightingCondition = LightingCondition.Unknown;
        CoachingLogger.SessionEnded();
    }

    [RelayCommand]
    private async Task CapturePhotoAsync()
    {
        if (_appState.IsCapturing) return;
        _appState.IsCapturing = true;
        try
        {
            var photo = await _cameraService.CapturePhotoAsync();
            if (photo is not null)
                _appState.LastCapturedPhoto = photo;
        }
        finally
        {
            _appState.IsCapturing = false;
        }
    }

    [RelayCommand]
    private async Task SwitchCameraAsync()
    {
        await _cameraService.SwitchCameraAsync();
    }

    [RelayCommand]
    private void ToggleLighting()
    {
        _appState.LightingFeatureEnabled = !_appState.LightingFeatureEnabled;
    }

    [RelayCommand]
    private void ToggleComposition()
    {
        _appState.CompositionFeatureEnabled = !_appState.CompositionFeatureEnabled;
    }

    [RelayCommand]
    private void GoBack()
    {
        _appState.SelectedMode = null;
    }
}
