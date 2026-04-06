using OptishotV1DOTNET.Core.Camera;
using OptishotV1DOTNET.Models;

namespace OptishotV1DOTNET.Core.Vision;

/// <summary>
/// Phase 2 — YOLO-based object detection pipeline.
/// Mirrors Swift ObjectDetectionPipeline (VNImageRequestHandler + CoreML).
/// On Android will use ML Kit or ONNX Runtime.
/// Not yet implemented — FrameAnalyzer returns empty DetectedObjects for now.
/// </summary>
public class ObjectDetectionPipeline
{
    public Task<IReadOnlyList<DetectedObject>> DetectAsync(CameraFrameEventArgs frame)
    {
        // TODO: Phase 2 — run YOLO model on frame
        return Task.FromResult<IReadOnlyList<DetectedObject>>(Array.Empty<DetectedObject>());
    }
}
