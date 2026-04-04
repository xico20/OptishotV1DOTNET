namespace OptishotV1DOTNET.Utilities;

public static class Constants
{
    // Camera
    public const int PreferredFrameRate = 60;

    // Analysis
    public const int AnalysisTargetFps = 6;
    public const int AnalysisFrameWidth = 640;
    public const int AnalysisFrameHeight = 480;

    // Lighting thresholds
    public const float DarkThreshold = 0.18f;
    public const float BrightThreshold = 0.82f;
    public const float HarshContrastThreshold = 0.35f;
    public const float ExcellentLowMean = 0.35f;
    public const float ExcellentHighMean = 0.65f;
    public const float ExcellentMaxStdDev = 0.15f;

    // Detection
    public const float ConfidenceThreshold = 0.55f;

    // Coaching
    public const double CoachingDebounceSecs = 0.2;
    public const double CoachingCooldownSecs = 1.5;
    public const int MaxVisibleTips = 3;

    // Composition
    public const int ClutterPersistenceFrames = 3;
    public const float CenterZoneMin = 0.25f;
    public const float CenterZoneMax = 0.75f;

    // Objects considered clutter
    public static readonly HashSet<string> ClutterLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "bottle", "cup", "remote", "phone", "book", "backpack", "handbag",
        "suitcase", "umbrella", "vase", "scissors", "toothbrush", "hair drier",
        "teddy bear", "laptop", "mouse", "keyboard"
    };
}
