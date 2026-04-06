using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Media;
using Android.OS;
using Android.Views;
using Java.Lang;
using OptishotV1DOTNET.Core.Camera;

namespace OptishotV1DOTNET.Platforms.Android;

// Android implementation of ICameraService using the Camera2 API.
// Manages three parallel surfaces: preview (user sees), analysis (coaching), photo (capture).
public class CameraService : Java.Lang.Object, ICameraService, ISurfaceHolderCallback
{
    private CameraManager? _cameraManager;
    private CameraDevice? _cameraDevice;
    private CameraCaptureSession? _captureSession;
    private ImageReader? _analysisReader; // feeds frames to LuminosityAnalyzer (~6 fps)
    private ImageReader? _photoReader;    // used only when the shutter button is pressed
    private AspectSurfaceView? _surfaceView;
    private HandlerThread? _backgroundThread; // all camera work runs off the main thread
    private Handler? _backgroundHandler;

    private string? _frontCameraId;
    private string? _backCameraId;
    private bool _isFrontCamera = false;
    private bool _isRunning = false;

    // Used to hand the photo bytes back to CapturePhotoAsync() callers
    private TaskCompletionSource<byte[]?>? _photoCaptureTcs;

    public bool IsRunning => _isRunning;
    public bool IsFrontCamera => _isFrontCamera;
    public byte[]? LastCapturedPhoto { get; private set; }

    // Fired on background thread for each analysis frame (~6 fps)
    public event EventHandler<CameraFrameEventArgs>? FrameReady;

    public CameraService()
    {
        _cameraManager = Platform.CurrentActivity?.GetSystemService(Context.CameraService) as CameraManager;
        FindCameraIds();
    }

    // Scans all camera IDs and stores the first front and back camera found.
    // Wrapped in try/catch because Redmi and other devices expose non-standard cameras
    // (depth, macro, etc.) that throw when queried for characteristics.
    private void FindCameraIds()
    {
        if (_cameraManager == null) return;
        foreach (var id in _cameraManager.GetCameraIdList() ?? [])
        {
            try
            {
                var chars = _cameraManager.GetCameraCharacteristics(id);
                var facing = chars.Get(CameraCharacteristics.LensFacing) as Integer;
                if (facing == null) continue;
                if (facing.IntValue() == (int)LensFacing.Back && _backCameraId == null)
                    _backCameraId = id;
                else if (facing.IntValue() == (int)LensFacing.Front && _frontCameraId == null)
                    _frontCameraId = id;
            }
            catch
            {
                // Skip cameras that don't support characteristics (depth, macro, etc.)
            }
        }
    }

    public async Task<bool> RequestPermissionsAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        return status == PermissionStatus.Granted;
    }

    public Task StartAsync()
    {
        StartBackgroundThread();
        _isRunning = true;
        // Surface might already be ready (page was visible before StartAsync was called)
        if (_surfaceView?.Holder?.Surface?.IsValid == true)
            OpenCamera();
        // If surface isn't ready yet, SurfaceCreated callback will call OpenCamera()
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _isRunning = false;
        CloseCamera();
        StopBackgroundThread();
        return Task.CompletedTask;
    }

    public Task SwitchCameraAsync()
    {
        _isFrontCamera = !_isFrontCamera;
        CloseCamera();
        if (_surfaceView?.Holder?.Surface?.IsValid == true)
            OpenCamera();
        return Task.CompletedTask;
    }

    // Returns a Task that completes when the photo bytes are available.
    // TriggerPhotoCapture fires a single capture; PhotoImageListener resolves the TCS.
    public Task<byte[]?> CapturePhotoAsync()
    {
        _photoCaptureTcs = new TaskCompletionSource<byte[]?>();
        TriggerPhotoCapture();
        return _photoCaptureTcs.Task;
    }

    public void AttachPreview(object nativeView)
    {
        if (nativeView is AspectSurfaceView surfaceView)
        {
            _surfaceView = surfaceView;
            ApplyPreviewAspectRatio(surfaceView);
            _surfaceView.Holder?.AddCallback(this);
            if (_isRunning && surfaceView.Holder?.Surface?.IsValid == true)
                OpenCamera();
        }
    }

    private void ApplyPreviewAspectRatio(AspectSurfaceView surfaceView)
    {
        // Use 4:3 — most camera sensors default to this ratio for preview.
        // The AspectSurfaceView uses this to avoid stretching on tall (20:9) screens.
        surfaceView.SetPreviewSize(4, 3);
    }

    public void DetachPreview()
    {
        _surfaceView?.Holder?.RemoveCallback(this);
        _surfaceView = null;
    }

    // ISurfaceHolderCallback — called by Android when the SurfaceView is ready/changed/destroyed
    public void SurfaceCreated(ISurfaceHolder holder)
    {
        if (_isRunning) OpenCamera();
    }

    public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height) { }

    public void SurfaceDestroyed(ISurfaceHolder holder)
    {
        CloseCamera();
    }

    private void OpenCamera()
    {
        var cameraId = _isFrontCamera ? _frontCameraId : _backCameraId;
        if (cameraId == null || _cameraManager == null || _backgroundHandler == null) return;

        // YUV_420_888 is the correct format for continuous frame analysis on Android.
        // JPEG in a repeating request causes ISP pipeline errors on Mediatek chips (Redmi Note 13).
        _analysisReader = ImageReader.NewInstance(320, 240, ImageFormatType.Yuv420888, 2);
        _analysisReader.SetOnImageAvailableListener(new AnalysisImageListener(this), _backgroundHandler);

        // Full-resolution JPEG only used for still capture (shutter button)
        _photoReader = ImageReader.NewInstance(1920, 1080, ImageFormatType.Jpeg, 1);
        _photoReader.SetOnImageAvailableListener(new PhotoImageListener(this), _backgroundHandler);

        _cameraManager.OpenCamera(cameraId, new CameraStateCallback(this), _backgroundHandler);
    }

    internal void OnCameraOpened(CameraDevice camera)
    {
        _cameraDevice = camera;
        CreateCaptureSession();
    }

    // Registers all output surfaces with the camera so it knows where to send pixels.
    private void CreateCaptureSession()
    {
        if (_cameraDevice == null || _surfaceView?.Holder?.Surface == null) return;

        var previewSurface = _surfaceView.Holder.Surface;
        var surfaces = new List<Surface>
        {
            previewSurface,
            _analysisReader!.Surface,
            _photoReader!.Surface
        };

        _cameraDevice.CreateCaptureSession(surfaces,
            new CaptureSessionCallback(this, previewSurface), _backgroundHandler);
    }

    // Called once the capture session is ready. Sets up the repeating preview request
    // that continuously feeds both the screen preview and the analysis reader.
    internal void OnSessionConfigured(CameraCaptureSession session, Surface previewSurface)
    {
        _captureSession = session;
        var builder = _cameraDevice!.CreateCaptureRequest(CameraTemplate.Preview);
        builder.AddTarget(previewSurface);
        builder.AddTarget(_analysisReader!.Surface); // analysis gets every preview frame
        builder.Set(CaptureRequest.ControlAfMode,
            Integer.ValueOf((int)ControlAFMode.ContinuousPicture)); // continuous autofocus
        builder.Set(CaptureRequest.ControlAeMode,
            Integer.ValueOf((int)ControlAEMode.On)); // auto exposure
        _captureSession.SetRepeatingRequest(builder.Build(), null, _backgroundHandler);
    }

    // Fires a single still capture — separate from the repeating preview request.
    private void TriggerPhotoCapture()
    {
        if (_cameraDevice == null || _captureSession == null || _photoReader == null) return;
        var builder = _cameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);
        builder.AddTarget(_photoReader.Surface);
        _captureSession.Capture(builder.Build(), null, _backgroundHandler);
    }

    internal void OnFrameAvailable(byte[] bgraBytes, int width, int height)
    {
        FrameReady?.Invoke(this, new CameraFrameEventArgs
        {
            PixelData = bgraBytes,
            Width = width,
            Height = height,
            BytesPerRow = width * 4
        });
    }

    internal void OnPhotoAvailable(byte[] data)
    {
        LastCapturedPhoto = data;
        _photoCaptureTcs?.TrySetResult(data); // unblocks CapturePhotoAsync() awaiter
    }

    private void CloseCamera()
    {
        _captureSession?.Close();
        _captureSession = null;
        _cameraDevice?.Close();
        _cameraDevice = null;
        _analysisReader?.Close();
        _analysisReader = null;
        _photoReader?.Close();
        _photoReader = null;
    }

    private void StartBackgroundThread()
    {
        _backgroundThread = new HandlerThread("CameraBackground");
        _backgroundThread.Start();
        _backgroundHandler = new Handler(_backgroundThread.Looper!);
    }

    private void StopBackgroundThread()
    {
        _backgroundThread?.QuitSafely();
        _backgroundThread?.Join();
        _backgroundThread = null;
        _backgroundHandler = null;
    }

    // ── Inner callbacks ─────────────────────────────

    private class CameraStateCallback : CameraDevice.StateCallback
    {
        private readonly CameraService _s;
        public CameraStateCallback(CameraService s) => _s = s;
        public override void OnOpened(CameraDevice camera) => _s.OnCameraOpened(camera);
        public override void OnDisconnected(CameraDevice camera) => camera.Close();
        public override void OnError(CameraDevice camera, CameraError error) => camera.Close();
    }

    private class CaptureSessionCallback : CameraCaptureSession.StateCallback
    {
        private readonly CameraService _s;
        private readonly Surface _preview;
        public CaptureSessionCallback(CameraService s, Surface preview) { _s = s; _preview = preview; }
        public override void OnConfigured(CameraCaptureSession session) => _s.OnSessionConfigured(session, _preview);
        public override void OnConfigureFailed(CameraCaptureSession session) { }
    }

    // Receives raw YUV frames from the camera and converts the Y (luminance) plane to BGRA.
    // Only the Y plane is needed because LuminosityAnalyzer works on luminance only.
    private class AnalysisImageListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        private readonly CameraService _s;
        private int _tick = 0;
        public AnalysisImageListener(CameraService s) => _s = s;

        public void OnImageAvailable(ImageReader? reader)
        {
            if (reader == null) return;
            var image = reader.AcquireLatestImage(); // discard older frames, only process latest
            if (image == null) return;

            try
            {
                // Throttle to ~6 fps (drop 4 out of 5 frames) — analysis doesn't need 30+ fps
                if (++_tick % 5 != 0) return;

                var planes = image.GetPlanes();
                var yPlane = planes![0];
                var yBuffer = yPlane.Buffer!;
                int rowStride = yPlane.RowStride; // may be > width due to memory alignment padding
                int w = image.Width;
                int h = image.Height;

                // Copy to local array immediately — the ByteBuffer becomes invalid after image.Close()
                // and can also become inaccessible during camera switches
                var yBytes = new byte[yBuffer.Remaining()];
                yBuffer.Get(yBytes);

                // Convert Y (grayscale) to BGRA — set R=G=B=Y so LuminosityAnalyzer sees correct luminance
                var bgra = new byte[w * h * 4];
                for (int row = 0; row < h; row++)
                {
                    for (int col = 0; col < w; col++)
                    {
                        byte y = yBytes[row * rowStride + col];
                        int i = (row * w + col) * 4;
                        bgra[i]     = y; // B
                        bgra[i + 1] = y; // G
                        bgra[i + 2] = y; // R
                        bgra[i + 3] = 255; // A
                    }
                }

                _s.OnFrameAvailable(bgra, w, h);
            }
            finally
            {
                image.Close(); // must always be closed to free the slot in the ImageReader queue
            }
        }
    }

    // Receives the full-resolution JPEG after the shutter button is pressed.
    private class PhotoImageListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        private readonly CameraService _s;
        public PhotoImageListener(CameraService s) => _s = s;

        public void OnImageAvailable(ImageReader? reader)
        {
            if (reader == null) return;
            var image = reader.AcquireNextImage();
            if (image == null) return;

            try
            {
                var planes = image.GetPlanes();
                var buffer = planes![0].Buffer!;
                var bytes = new byte[buffer.Remaining()];
                buffer.Get(bytes);
                _s.OnPhotoAvailable(bytes);
            }
            finally
            {
                image.Close();
            }
        }
    }
}
