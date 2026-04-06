namespace OptishotV1DOTNET.Core.ML;

/// <summary>
/// Phase 2 — ML model lifecycle manager.
/// Mirrors Swift ModelManager (CoreML model loading/unloading).
/// On Android will manage ONNX Runtime or ML Kit model sessions.
/// Not yet implemented.
/// </summary>
public class ModelManager
{
    public bool IsLoaded { get; private set; }

    // TODO: Phase 2 — load YOLO or equivalent model
    public Task LoadAsync() => Task.CompletedTask;
    public void Unload() { }
}
