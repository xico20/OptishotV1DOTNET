using OptishotV1DOTNET.Core.Camera;
using OptishotV1DOTNET.Models;
using OptishotV1DOTNET.Utilities;

namespace OptishotV1DOTNET.Core.Vision;

/// <summary>
/// Central pipeline dispatcher — mirrors Swift FrameAnalyzer.
/// Receives raw frames from ICameraService, throttles to ~6 fps,
/// runs LuminosityAnalyzer, computes shake score, and publishes FrameAnalysisResult.
/// Future: will also dispatch to ObjectDetectionPipeline.
/// </summary>
public class FrameAnalyzer
{
    private readonly ICameraService _cameraService;
    private readonly LuminosityAnalyzer _luminosityAnalyzer = new();
    private readonly FrameThrottler _throttler = new();

    // Previous frame's 8x8 grid means — used to detect camera shake
    private float[]? _previousGridMeans;

    // Latest accelerometer reading — updated via event, read on frame thread
    private AccelerometerData _latestAccelerometer;

    public event EventHandler<FrameAnalysisResult>? ResultReady;

    public FrameAnalysisResult? LatestResult { get; private set; }

    public FrameAnalyzer(ICameraService cameraService)
    {
        _cameraService = cameraService;
        _cameraService.FrameReady += OnFrameReady;

        if (Accelerometer.Default.IsSupported)
        {
            Accelerometer.Default.ReadingChanged += (_, e) => _latestAccelerometer = e.Reading;
            if (!Accelerometer.Default.IsMonitoring)
                Accelerometer.Default.Start(SensorSpeed.UI);
        }
    }

    private void OnFrameReady(object? sender, CameraFrameEventArgs e)
    {
        if (!_throttler.ShouldProcessFrame()) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var (stats, condition) = _luminosityAnalyzer.Analyze(
            e.PixelData, e.Width, e.Height, e.BytesPerRow);

        sw.Stop();
        _throttler.ReportAnalysisDuration(sw.Elapsed.TotalMilliseconds);

        // Shake score: mean absolute difference between current and previous grid means
        float shakeScore = 0f;
        if (_previousGridMeans != null)
        {
            float diff = 0;
            for (int i = 0; i < 64; i++)
                diff += MathF.Abs(stats.GridMeans[i] - _previousGridMeans[i]);
            // Normalize: average cell diff of 0.1 = fully shaking (score 1.0)
            shakeScore = Math.Min(1f, (diff / 64f) / 0.1f);
        }
        _previousGridMeans = stats.GridMeans;

        var result = new FrameAnalysisResult
        {
            LightingCondition = condition,
            LuminanceStats = stats,
            ShakeScore = shakeScore,
            TiltDegrees = ComputeTiltDegrees(),
            DetectedObjects = Array.Empty<DetectedObject>() // Phase 2: ObjectDetectionPipeline
        };

        LatestResult = result;
        ResultReady?.Invoke(this, result);
    }

    // Roll angle from accelerometer gravity vector (portrait mode).
    // Returns 0 if accelerometer unavailable or phone is nearly flat.
    private float ComputeTiltDegrees()
    {
        if (!Accelerometer.Default.IsSupported || !Accelerometer.Default.IsMonitoring)
            return 0f;

        var a = _latestAccelerometer.Acceleration;

        // If XY magnitude is too small, phone is nearly horizontal — tilt undefined
        float xyMag = MathF.Sqrt(a.X * a.X + a.Y * a.Y);
        if (xyMag < 0.5f)
            return 0f;

        // atan2(X, Y): 0° when upright, positive when tilted clockwise (right side down)
        return (float)(Math.Atan2(a.X, a.Y) * 180.0 / Math.PI);
    }

    public void Reset()
    {
        _throttler.Reset();
        _previousGridMeans = null;
    }
}
