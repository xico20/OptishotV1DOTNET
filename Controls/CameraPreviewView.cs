namespace OptishotV1DOTNET.Controls;

/// <summary>
/// Virtual MAUI view representing the live camera preview.
/// The platform-specific handler (Platforms/iOS/CameraPreviewHandler.cs)
/// renders it as an AVCaptureVideoPreviewLayer on iOS.
/// </summary>
public class CameraPreviewView : View
{
    public static readonly BindableProperty CameraServiceProperty =
        BindableProperty.Create(
            nameof(CameraService),
            typeof(Services.ICameraService),
            typeof(CameraPreviewView));

    public Services.ICameraService? CameraService
    {
        get => (Services.ICameraService?)GetValue(CameraServiceProperty);
        set => SetValue(CameraServiceProperty, value);
    }
}
