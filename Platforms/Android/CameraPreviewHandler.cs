using Android.Views;
using Microsoft.Maui.Handlers;
using OptishotV1DOTNET.Core.Camera;

namespace OptishotV1DOTNET.Platforms.Android;

public class CameraPreviewHandler : ViewHandler<CameraPreviewView, AspectSurfaceView>
{
    public static PropertyMapper<CameraPreviewView, CameraPreviewHandler> Mapper =
        new PropertyMapper<CameraPreviewView, CameraPreviewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(CameraPreviewView.CameraService)] = MapCameraService
        };

    public CameraPreviewHandler() : base(Mapper) { }

    protected override AspectSurfaceView CreatePlatformView()
        => new AspectSurfaceView(Context);

    protected override void ConnectHandler(AspectSurfaceView platformView)
    {
        base.ConnectHandler(platformView);
        AttachPreview();
    }

    protected override void DisconnectHandler(AspectSurfaceView platformView)
    {
        VirtualView.CameraService?.DetachPreview();
        base.DisconnectHandler(platformView);
    }

    private static void MapCameraService(CameraPreviewHandler handler, CameraPreviewView view)
        => handler.AttachPreview();

    private void AttachPreview()
    {
        if (VirtualView.CameraService is { } service)
            service.AttachPreview(PlatformView);
    }
}

/// <summary>
/// SurfaceView that preserves the camera preview aspect ratio instead of stretching.
/// </summary>
public class AspectSurfaceView : SurfaceView
{
    private int _previewWidth;
    private int _previewHeight;

    public AspectSurfaceView(global::Android.Content.Context context) : base(context) { }

    public void SetPreviewSize(int width, int height)
    {
        _previewWidth = width;
        _previewHeight = height;
        RequestLayout();
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int width = MeasureSpec.GetSize(widthMeasureSpec);
        int height = MeasureSpec.GetSize(heightMeasureSpec);

        if (_previewWidth > 0 && _previewHeight > 0)
        {
            // Scale to fill width, crop height if needed (AspectFill)
            float ratio = (float)_previewHeight / _previewWidth; // portrait ratio
            int scaledHeight = (int)(width * ratio);
            if (scaledHeight < height)
            {
                // Fill height instead
                scaledHeight = height;
                width = (int)(height / ratio);
            }
            SetMeasuredDimension(width, scaledHeight);
        }
        else
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        }
    }
}
