using OptishotV1DOTNET.Core.Vision;
using OptishotV1DOTNET.Models;

namespace OptishotV1DOTNET.Features.Composition;

/// <summary>
/// Phase 2 — Background clutter detection and coaching.
/// Mirrors Swift CompositionCleanup.
/// Receives detected objects from ObjectDetectionPipeline and filters
/// for clutter items defined in Constants.ClutterLabels.
/// Not yet active — ObjectDetectionPipeline returns empty results in Phase 1.
/// </summary>
public class CompositionCleanup
{
    private readonly FrameAnalyzer _frameAnalyzer;

    public event EventHandler<IReadOnlyList<DetectedObject>>? ClutterDetected;

    public CompositionCleanup(FrameAnalyzer frameAnalyzer)
    {
        _frameAnalyzer = frameAnalyzer;
        _frameAnalyzer.ResultReady += OnResultReady;
    }

    private void OnResultReady(object? sender, FrameAnalysisResult result)
    {
        var clutter = result.ClutterObjects.ToList();
        if (clutter.Count > 0)
            ClutterDetected?.Invoke(this, clutter);
    }
}
