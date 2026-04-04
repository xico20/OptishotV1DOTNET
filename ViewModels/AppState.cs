using CommunityToolkit.Mvvm.ComponentModel;
using OptishotV1DOTNET.Models;

namespace OptishotV1DOTNET.ViewModels;

/// <summary>
/// Global state shared across the app. Mirrors Swift AppState (@MainActor ObservableObject).
/// </summary>
public partial class AppState : ObservableObject
{
    [ObservableProperty]
    private ShootingMode? _selectedMode;

    [ObservableProperty]
    private bool _lightingFeatureEnabled = true;

    [ObservableProperty]
    private bool _compositionFeatureEnabled = true;

    [ObservableProperty]
    private bool _isSessionRunning;

    [ObservableProperty]
    private IReadOnlyList<CoachingTip> _currentTips = Array.Empty<CoachingTip>();

    [ObservableProperty]
    private LightingCondition _lightingCondition = LightingCondition.Unknown;

    [ObservableProperty]
    private IReadOnlyList<DetectedObject> _detectedClutter = Array.Empty<DetectedObject>();

    [ObservableProperty]
    private byte[]? _lastCapturedPhoto;

    [ObservableProperty]
    private bool _isCapturing;
}
