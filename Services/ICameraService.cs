namespace OptishotV1DOTNET.Services;

/// <summary>
/// Raw frame data delivered per camera frame at ~6 fps for analysis.
/// Pixels are in BGRA format, width x height.
/// </summary>
public class CameraFrameEventArgs : EventArgs
{
    public byte[] PixelData { get; init; } = Array.Empty<byte>();
    public int Width { get; init; }
    public int Height { get; init; }
    public int BytesPerRow { get; init; }
}

public interface ICameraService
{
    bool IsRunning { get; }
    bool IsFrontCamera { get; }
    byte[]? LastCapturedPhoto { get; }

    /// <summary>Fired on a background thread for each analysis frame (~6 fps).</summary>
    event EventHandler<CameraFrameEventArgs>? FrameReady;

    Task<bool> RequestPermissionsAsync();
    Task StartAsync();
    Task StopAsync();
    Task SwitchCameraAsync();
    Task<byte[]?> CapturePhotoAsync();

    /// <summary>Attach the native preview layer to the given platform view.</summary>
    void AttachPreview(object nativeView);
    void DetachPreview();
}
