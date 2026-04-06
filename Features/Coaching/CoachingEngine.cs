using OptishotV1DOTNET.Core.Vision;
using OptishotV1DOTNET.Models;
using OptishotV1DOTNET.Utilities;

namespace OptishotV1DOTNET.Features.Coaching;

/// <summary>
/// Turns raw FrameAnalysisResults into actionable coaching tips shown to the user.
/// Logic: debounce incoming results → filter by cooldown → sort by severity → cap at 3 tips.
/// Mirrors Swift CoachingEngine.
/// </summary>
public class CoachingEngine
{
    private readonly FrameAnalyzer _frameAnalyzer;

    // Tracks when each tip category was last shown, to enforce per-category cooldown
    private readonly Dictionary<TipCategory, DateTime> _lastShownByCategory = new();

    // Debounce: we cancel and restart a delay on every new result.
    // The tip only fires after the scene is stable for CoachingDebounceSecs.
    private CancellationTokenSource? _debounceCts;

    public event EventHandler<IReadOnlyList<CoachingTip>>? TipsUpdated;
    public IReadOnlyList<CoachingTip> ActiveTips { get; private set; } = Array.Empty<CoachingTip>();

    public CoachingEngine(FrameAnalyzer frameAnalyzer)
    {
        _frameAnalyzer = frameAnalyzer;
        _frameAnalyzer.ResultReady += OnResultReady;
    }

private void OnResultReady(object? sender, FrameAnalysisResult result)
    {
        // Cancel the previous pending tip — we only want to coach on stable scenes
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Delay(TimeSpan.FromSeconds(Constants.CoachingDebounceSecs), token)
            .ContinueWith(_ =>
            {
                if (token.IsCancellationRequested) return;
                ProcessResult(result);
            }, TaskScheduler.Default);
    }

    private void ProcessResult(FrameAnalysisResult result)
    {
        var candidates = new List<CoachingTip>();

        // Lighting tip — problems get a warning; Excellent gets a celebration
        if (result.LightingCondition.NeedsCoaching)
        {
            var tip = BuildLightingTip(result);
            if (PassesCooldown(tip.Category))
                candidates.Add(tip);
        }
        else if (result.LightingCondition.Kind == LightingConditionKind.Excellent)
        {
            var tip = new CoachingTip
            {
                Category = TipCategory.Lighting,
                Message = "Perfect light! ✦ Great conditions for a shot.",
                Severity = TipSeverity.Suggestion
            };
            if (PassesCustomCooldown(tip.Category, Constants.ExcellentCooldownSecs))
                candidates.Add(tip);
        }
        else if (result.LightingCondition.Kind == LightingConditionKind.Good)
        {
            var tip = new CoachingTip
            {
                Category = TipCategory.Lighting,
                Message = BuildGoodLightTip(result.LuminanceStats.Mean, result.LuminanceStats.StandardDeviation),
                Severity = TipSeverity.Suggestion
            };
            if (PassesCustomCooldown(tip.Category, Constants.GoodLightCooldownSecs))
                candidates.Add(tip);
        }
        // Shake tip — camera is moving too much (takes priority over blur)
        if (result.ShakeScore > Constants.ShakeThreshold)
        {
            var tip = new CoachingTip
            {
                Category = TipCategory.Stability,
                Message = "Camera is moving — hold still for a sharp shot.",
                Severity = TipSeverity.Warning
            };
            if (PassesCustomCooldown(tip.Category, Constants.StabilityCooldownSecs))
                candidates.Add(tip);
        }
        // Blur tip — image is soft even when not shaking (focus or lens issue)
        else if (result.LuminanceStats.SharpnessScore < Constants.BlurThreshold)
        {
            var tip = new CoachingTip
            {
                Category = TipCategory.Stability,
                Message = "Image is blurry — hold the camera steady and wait for focus.",
                Severity = TipSeverity.Warning
            };
            if (PassesCustomCooldown(tip.Category, Constants.StabilityCooldownSecs))
                candidates.Add(tip);
        }

        // Clutter tip — only if objects were detected in the background (Phase 2)
        var clutterObjects = result.ClutterObjects.ToList();
        if (clutterObjects.Count > 0)
        {
            var tip = BuildClutterTip(clutterObjects);
            if (PassesCooldown(tip.Category))
                candidates.Add(tip);
        }

        // Most severe tips first; cap at MaxVisibleTips (3) to avoid overwhelming the user
        var tips = candidates
            .OrderByDescending(t => (int)t.Severity)
            .Take(Constants.MaxVisibleTips)
            .ToList();

        // Update cooldown timestamps for shown categories
        foreach (var tip in tips)
        {
            _lastShownByCategory[tip.Category] = DateTime.UtcNow;
            CoachingLogger.TipShown(tip.Category.ToString(), tip.Message);
        }

        ActiveTips = tips;

        // Log lighting condition on every result (even when no tip fires)
        CoachingLogger.LightingChanged(
            result.LightingCondition.Kind.ToString(),
            result.LuminanceStats.Mean,
            result.LuminanceStats.StandardDeviation);

        // Tips update must happen on the UI thread (touches MAUI bindings)
        MainThread.BeginInvokeOnMainThread(() =>
            TipsUpdated?.Invoke(this, tips));
    }

    // Returns true if the category hasn't been shown recently (or never shown)
    private bool PassesCooldown(TipCategory category)
    {
        if (!_lastShownByCategory.TryGetValue(category, out var lastShown))
            return true;
        return (DateTime.UtcNow - lastShown).TotalSeconds >= Constants.CoachingCooldownSecs;
    }

    private bool PassesCustomCooldown(TipCategory category, double cooldownSecs)
    {
        if (!_lastShownByCategory.TryGetValue(category, out var lastShown))
            return true;
        return (DateTime.UtcNow - lastShown).TotalSeconds >= cooldownSecs;
    }

    // Tells the user what to fix to go from Good → Excellent
    private static string BuildGoodLightTip(float mean, float stdDev)
    {
        if (stdDev > Constants.ExcellentMaxStdDev)
            return "Try diffusing the light for more even coverage.";
        if (mean < Constants.ExcellentLowMean)
            return "Add a bit more light to reach perfect conditions.";
        return "Reduce the light slightly for perfect conditions.";
    }

    // Very dark (mean < 0.10) is Critical; just dark is Warning
    private static CoachingTip BuildLightingTip(FrameAnalysisResult result)
    {
        var mean = result.LuminanceStats.Mean;
        var severity = mean < 0.10f ? TipSeverity.Critical : TipSeverity.Warning;

        return new CoachingTip
        {
            Category = TipCategory.Lighting,
            Message = result.LightingCondition.CoachingMessage,
            Severity = severity
        };
    }

    // Message adapts based on how many cluttered objects are detected (1, 2, or 3+)
    private static CoachingTip BuildClutterTip(List<DetectedObject> objects)
    {
        string message = objects.Count switch
        {
            1 => $"Remove the {objects[0].DisplayName} from the background.",
            2 => $"Remove the {objects[0].DisplayName} and {objects[1].DisplayName}.",
            _ => $"{objects[0].DisplayName}, {objects[1].DisplayName}, {objects[2].DisplayName} — clean up the background."
        };

        return new CoachingTip
        {
            Category = TipCategory.Composition,
            Message = message,
            Severity = TipSeverity.Suggestion
        };
    }
}
