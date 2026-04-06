namespace OptishotV1DOTNET.Utilities;

public static class Constants
{
    // ── Camera ──────────────────────────────────────────────────────────────
    public const int PreferredFrameRate = 60;

    // ── Analysis ────────────────────────────────────────────────────────────
    public const int AnalysisTargetFps = 6;    // how many frames per second we analyze
    public const int AnalysisFrameWidth = 640;  // resolution sent to LuminosityAnalyzer
    public const int AnalysisFrameHeight = 480;

    // ── Lighting thresholds (0.0 = black, 1.0 = white) ──────────────────────
    // Mean brightness below this → "Too Dark"
    public const float DarkThreshold = 0.18f;
    // Mean brightness above this → "Too Bright"
    public const float BrightThreshold = 0.82f;
    // Standard deviation above this → "Harsh Shadows" (big light/dark contrast)
    public const float HarshContrastThreshold = 0.38f;
    // Mean in [ExcellentLow, ExcellentHigh] + low stdDev → "Excellent"
    public const float ExcellentLowMean = 0.35f;
    public const float ExcellentHighMean = 0.65f;
    public const float ExcellentMinStdDev = 0.05f; // below this = flat/obscured scene (e.g. finger on lens)
    public const float ExcellentMaxStdDev = 0.18f;

    // ── Object detection ────────────────────────────────────────────────────
    // Minimum confidence score for a detected object to be considered clutter
    public const float ConfidenceThreshold = 0.55f;

    // ── Coaching timing ─────────────────────────────────────────────────────
    // Wait this long after the last frame change before showing a new tip (avoids flicker)
    public const double CoachingDebounceSecs = 0.2;
    // Don't repeat the same category of tip more often than this
    public const double CoachingCooldownSecs = 1.5;
    // Cooldown for the "Good light" improvement tip
    public const double GoodLightCooldownSecs = 8.0;
    // Cooldown for the "Perfect light!" celebration tip
    public const double ExcellentCooldownSecs = 20.0;
    // Max number of tips shown at once on screen
    public const int MaxVisibleTips = 3;

    // ── Stability (shake + blur) ─────────────────────────────────────────────
    // ShakeScore above this → camera is moving too much
    public const float ShakeThreshold = 0.4f;
    // SharpnessScore below this → image is blurry
    public const float BlurThreshold = 0.2f;
    // Cooldown for shake/blur tips (short — condition changes quickly)
    public const double StabilityCooldownSecs = 2.0;

    // ── Composition ─────────────────────────────────────────────────────────
    // Object must appear in this many consecutive frames before it's reported as clutter
    public const int ClutterPersistenceFrames = 3;
    // Central zone of the frame (25%–75% of width/height) — objects here matter most
    public const float CenterZoneMin = 0.25f;
    public const float CenterZoneMax = 0.75f;

    // Objects that count as background clutter when detected in the frame
    public static readonly HashSet<string> ClutterLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "bottle", "cup", "remote", "phone", "book", "backpack", "handbag",
        "suitcase", "umbrella", "vase", "scissors", "toothbrush", "hair drier",
        "teddy bear", "laptop", "mouse", "keyboard"
    };
}
