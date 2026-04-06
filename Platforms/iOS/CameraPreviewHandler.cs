using Microsoft.Maui.Handlers;
using OptishotV1DOTNET.Core.Camera;
using UIKit;

namespace OptishotV1DOTNET.Platforms.iOS;

/// <summary>
/// iOS handler for the CameraPreviewView virtual control.
/// Creates a plain UIView and asks ICameraService to attach its AVCaptureVideoPreviewLayer.
/// </summary>
public class CameraPreviewHandler : ViewHandler<CameraPreviewView, UIView>
{
    public static PropertyMapper<CameraPreviewView, CameraPreviewHandler> Mapper =
        new PropertyMapper<CameraPreviewView, CameraPreviewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(CameraPreviewView.CameraService)] = MapCameraService
        };

    public CameraPreviewHandler() : base(Mapper) { }

    protected override UIView CreatePlatformView()
    {
        var view = new UIView
        {
            BackgroundColor = UIColor.Black,
            ClipsToBounds = true
        };
        return view;
    }

    protected override void ConnectHandler(UIView platformView)
    {
        base.ConnectHandler(platformView);
        AttachPreview();
    }

    protected override void DisconnectHandler(UIView platformView)
    {
        VirtualView.CameraService?.DetachPreview();
        base.DisconnectHandler(platformView);
    }

    private static void MapCameraService(CameraPreviewHandler handler, CameraPreviewView view)
    {
        handler.AttachPreview();
    }

    private void AttachPreview()
    {
        if (VirtualView.CameraService is { } service)
            service.AttachPreview(PlatformView);
    }

    // Re-layout the preview layer when the view bounds change
    public override void SetVirtualView(IView view)
    {
        base.SetVirtualView(view);
    }

    protected override void SetupContainer()
    {
        base.SetupContainer();
    }
}
