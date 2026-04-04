using OptishotV1DOTNET.Models;
using OptishotV1DOTNET.Utilities;

namespace OptishotV1DOTNET.Services;

/// <summary>
/// Generates contextual coaching tips from frame analysis results.
/// Mirrors Swift CoachingEngine: debounce, per-category cooldown, severity sorting, max 3 tips.
/// </summary>
public class CoachingEngine
{
    private readonly FrameAnalyzer _frameAnalyzer;
    private readonly Dictionary<TipCategory, DateTime> _lastShownByCategory = new();
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
        // Cancel previous debounce
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

        // Lighting tip
        if (result.LightingCondition.NeedsCoaching)
        {
            var tip = BuildLightingTip(result);
            if (PassesCooldown(tip.Category))
                candidates.Add(tip);
        }

        // Clutter tip
        var clutterObjects = result.ClutterObjects.ToList();
        if (clutterObjects.Count > 0)
        {
            var tip = BuildClutterTip(clutterObjects);
            if (PassesCooldown(tip.Category))
                candidates.Add(tip);
        }

        // Sort by severity descending, limit to max 3
        var tips = candidates
            .OrderByDescending(t => (int)t.Severity)
            .Take(Constants.MaxVisibleTips)
            .ToList();

        // Update cooldowns
        foreach (var tip in tips)
            _lastShownByCategory[tip.Category] = DateTime.UtcNow;

        ActiveTips = tips;

        MainThread.BeginInvokeOnMainThread(() =>
            TipsUpdated?.Invoke(this, tips));
    }

    private bool PassesCooldown(TipCategory category)
    {
        if (!_lastShownByCategory.TryGetValue(category, out var lastShown))
            return true;
        return (DateTime.UtcNow - lastShown).TotalSeconds >= Constants.CoachingCooldownSecs;
    }

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
