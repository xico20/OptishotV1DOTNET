using OptishotV1DOTNET.Utilities;

namespace OptishotV1DOTNET.Services;

/// <summary>
/// Throttles frame analysis to ~6 fps with adaptive backoff.
/// Mirrors the Swift FrameThrottler logic.
/// </summary>
public class FrameThrottler
{
    private readonly double _baseIntervalMs = 1000.0 / Constants.AnalysisTargetFps; // ~166.7 ms
    private double _currentIntervalMs;
    private DateTime _lastProcessedTime = DateTime.MinValue;
    private int _consecutiveHeavyFrames = 0;
    private const int HeavyFrameThreshold = 3;

    public FrameThrottler()
    {
        _currentIntervalMs = _baseIntervalMs;
    }

    /// <returns>True if enough time has elapsed since the last processed frame.</returns>
    public bool ShouldProcessFrame()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastProcessedTime).TotalMilliseconds >= _currentIntervalMs)
        {
            _lastProcessedTime = now;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Report how long the last analysis took.
    /// If heavy frames pile up, doubles the interval (halves analysis rate).
    /// </summary>
    public void ReportAnalysisDuration(double durationMs)
    {
        if (durationMs > _baseIntervalMs)
        {
            _consecutiveHeavyFrames++;
            if (_consecutiveHeavyFrames >= HeavyFrameThreshold)
                _currentIntervalMs = _baseIntervalMs * 2; // drop to ~3 fps
        }
        else
        {
            _consecutiveHeavyFrames = 0;
            _currentIntervalMs = _baseIntervalMs;
        }
    }

    public void Reset()
    {
        _lastProcessedTime = DateTime.MinValue;
        _consecutiveHeavyFrames = 0;
        _currentIntervalMs = _baseIntervalMs;
    }
}
