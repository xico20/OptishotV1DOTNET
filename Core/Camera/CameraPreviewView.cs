namespace OptishotV1DOTNET.Core.Camera;

/// <summary>
/// Virtual MAUI view representing the live camera preview.
/// The platform-specific handler (Platforms/Android or iOS/CameraPreviewHandler)
/// renders it as a SurfaceView (Android) or AVCaptureVideoPreviewLayer (iOS).
/// </summary>
public class CameraPreviewView : View
{
    public static readonly BindableProperty CameraServiceProperty =
        BindableProperty.Create(
            nameof(CameraService),
            typeof(ICameraService),
            typeof(CameraPreviewView));

    public ICameraService? CameraService
    {
        get => (ICameraService?)GetValue(CameraServiceProperty);
        set => SetValue(CameraServiceProperty, value);
    }
}
