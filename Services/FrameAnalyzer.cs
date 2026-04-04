using OptishotV1DOTNET.Models;

namespace OptishotV1DOTNET.Services;

/// <summary>
/// Coordinator for all frame analysis. Receives frames from ICameraService,
/// throttles to ~6 fps, runs LuminosityAnalyzer, and publishes results.
/// Mirrors Swift FrameAnalyzer.
/// </summary>
public class FrameAnalyzer
{
    private readonly ICameraService _cameraService;
    private readonly LuminosityAnalyzer _luminosityAnalyzer = new();
    private readonly FrameThrottler _throttler = new();

    public event EventHandler<FrameAnalysisResult>? ResultReady;

    public FrameAnalysisResult? LatestResult { get; private set; }

    public FrameAnalyzer(ICameraService cameraService)
    {
        _cameraService = cameraService;
        _cameraService.FrameReady += OnFrameReady;
    }

    private void OnFrameReady(object? sender, CameraFrameEventArgs e)
    {
        if (!_throttler.ShouldProcessFrame()) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var (stats, condition) = _luminosityAnalyzer.Analyze(
            e.PixelData, e.Width, e.Height, e.BytesPerRow);

        sw.Stop();
        _throttler.ReportAnalysisDuration(sw.Elapsed.TotalMilliseconds);

        var result = new FrameAnalysisResult
        {
            LightingCondition = condition,
            LuminanceStats = stats,
            DetectedObjects = Array.Empty<DetectedObject>() // Object detection disabled in MVP
        };

        LatestResult = result;
        ResultReady?.Invoke(this, result);
    }

    public void Reset() => _throttler.Reset();
}
