using AVFoundation;
using CoreFoundation;
using CoreMedia;
using CoreVideo;
using Foundation;
using Photos;
using UIKit;
using OptishotV1DOTNET.Services;

namespace OptishotV1DOTNET.Platforms.iOS;

/// <summary>
/// iOS implementation of ICameraService using AVFoundation.
/// Mirrors the Swift CameraManager.
/// </summary>
public class CameraService : NSObject, ICameraService, IAVCaptureVideoDataOutputSampleBufferDelegate
{
    private AVCaptureSession? _session;
    private AVCaptureDeviceInput? _currentInput;
    private AVCaptureVideoDataOutput? _videoOutput;
    private AVCapturePhotoOutput? _photoOutput;
    private AVCaptureVideoPreviewLayer? _previewLayer;

    private readonly DispatchQueue _sessionQueue = new("com.optishot.sessionQueue", false);
    private readonly DispatchQueue _videoQueue = new("com.optishot.videoQueue", false);

    private bool _isFrontCamera = false;
    private TaskCompletionSource<byte[]?>? _photoCaptureTcs;

    public bool IsRunning => _session?.Running ?? false;
    public bool IsFrontCamera => _isFrontCamera;
    public byte[]? LastCapturedPhoto { get; private set; }

    public event EventHandler<CameraFrameEventArgs>? FrameReady;

    // ──────────────────────────────────────────────
    // Permissions
    // ──────────────────────────────────────────────

    public async Task<bool> RequestPermissionsAsync()
    {
        var cameraStatus = AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Video);
        if (cameraStatus == AVAuthorizationStatus.NotDetermined)
        {
            cameraStatus = await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Video)
                ? AVAuthorizationStatus.Authorized
                : AVAuthorizationStatus.Denied;
        }
        return cameraStatus == AVAuthorizationStatus.Authorized;
    }

    // ──────────────────────────────────────────────
    // Session lifecycle
    // ──────────────────────────────────────────────

    public Task StartAsync()
    {
        var tcs = new TaskCompletionSource();
        _sessionQueue.DispatchAsync(() =>
        {
            ConfigureSession();
            _session?.StartRunning();
            tcs.SetResult();
        });
        return tcs.Task;
    }

    public Task StopAsync()
    {
        var tcs = new TaskCompletionSource();
        _sessionQueue.DispatchAsync(() =>
        {
            _session?.StopRunning();
            tcs.SetResult();
        });
        return tcs.Task;
    }

    // ──────────────────────────────────────────────
    // Session configuration
    // ──────────────────────────────────────────────

    private void ConfigureSession()
    {
        _session = new AVCaptureSession();
        _session.BeginConfiguration();
        _session.SessionPreset = AVCaptureSession.PresetPhoto;

        // Best available back camera
        var device = BestBackCamera();
        if (device is null)
        {
            _session.CommitConfiguration();
            return;
        }

        NSError? error;
        var input = new AVCaptureDeviceInput(device, out error);
        if (error is not null || !_session.CanAddInput(input))
        {
            _session.CommitConfiguration();
            return;
        }
        _session.AddInput(input);
        _currentInput = input;

        // Configure device: 60 fps, continuous AF/AE
        ConfigureDevice(device);

        // Video output for frame analysis
        _videoOutput = new AVCaptureVideoDataOutput();
        _videoOutput.WeakVideoSettings = new NSMutableDictionary
        {
            { CVPixelBuffer.PixelFormatTypeKey,
              NSNumber.FromInt32((int)CVPixelFormatType.CV32BGRA) }
        };
        _videoOutput.AlwaysDiscardsLateVideoFrames = true;
        _videoOutput.SetSampleBufferDelegateQueue(this, _videoQueue);

        if (_session.CanAddOutput(_videoOutput))
            _session.AddOutput(_videoOutput);

        // Photo output
        _photoOutput = new AVCapturePhotoOutput();
        if (_session.CanAddOutput(_photoOutput))
            _session.AddOutput(_photoOutput);

        // Preview layer
        _previewLayer = new AVCaptureVideoPreviewLayer(_session)
        {
            VideoGravity = AVLayerVideoGravity.ResizeAspectFill
        };

        _session.CommitConfiguration();
    }

    private static void ConfigureDevice(AVCaptureDevice device)
    {
        NSError? err;
        device.LockForConfiguration(out err);
        if (err is not null) return;

        // 60 fps
        var targetRate = new CMTime(1, 60);
        foreach (var range in device.ActiveFormat.VideoSupportedFrameRateRanges)
        {
            if (range.MaxFrameRate >= 60)
            {
                device.ActiveVideoMinFrameDuration = targetRate;
                device.ActiveVideoMaxFrameDuration = targetRate;
                break;
            }
        }

        if (device.IsFocusModeSupported(AVCaptureFocusMode.ContinuousAutoFocus))
            device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;

        if (device.IsExposureModeSupported(AVCaptureExposureMode.ContinuousAutoExposure))
            device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;

        device.UnlockForConfiguration();
    }

    private static AVCaptureDevice? BestBackCamera()
    {
        // Priority: triple > dual-wide > dual > wide-angle
        var positions = new[]
        {
            AVCaptureDeviceType.BuiltInTripleCamera,
            AVCaptureDeviceType.BuiltInDualWideCamera,
            AVCaptureDeviceType.BuiltInDualCamera,
            AVCaptureDeviceType.BuiltInWideAngleCamera
        };
        foreach (var type in positions)
        {
            var dev = AVCaptureDevice.GetDefaultDevice(type, AVMediaTypes.Video, AVCaptureDevicePosition.Back);
            if (dev is not null) return dev;
        }
        return AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
    }

    // ──────────────────────────────────────────────
    // Switch camera
    // ──────────────────────────────────────────────

    public Task SwitchCameraAsync()
    {
        var tcs = new TaskCompletionSource();
        _sessionQueue.DispatchAsync(() =>
        {
            _isFrontCamera = !_isFrontCamera;
            var position = _isFrontCamera
                ? AVCaptureDevicePosition.Front
                : AVCaptureDevicePosition.Back;

            var newDevice = _isFrontCamera
                ? AVCaptureDevice.GetDefaultDevice(AVCaptureDeviceType.BuiltInWideAngleCamera,
                    AVMediaTypes.Video, AVCaptureDevicePosition.Front)
                : BestBackCamera();

            if (newDevice is null || _session is null || _currentInput is null)
            {
                tcs.SetResult();
                return;
            }

            NSError? error;
            var newInput = new AVCaptureDeviceInput(newDevice, out error);
            if (error is not null) { tcs.SetResult(); return; }

            _session.BeginConfiguration();
            _session.RemoveInput(_currentInput);
            if (_session.CanAddInput(newInput))
            {
                _session.AddInput(newInput);
                _currentInput = newInput;
                ConfigureDevice(newDevice);
            }
            _session.CommitConfiguration();
            tcs.SetResult();
        });
        return tcs.Task;
    }

    // ──────────────────────────────────────────────
    // Photo capture
    // ──────────────────────────────────────────────

    public async Task<byte[]?> CapturePhotoAsync()
    {
        if (_photoOutput is null) return null;

        _photoCaptureTcs = new TaskCompletionSource<byte[]?>();

        var settings = AVCapturePhotoSettings.Create();
        if (_photoOutput.AvailablePhotoCodecTypes.Contains(AVVideoCodecType.Hevc))
            settings = AVCapturePhotoSettings.FromFormat(
                new NSDictionary<NSString, NSObject>(
                    AVVideo.CodecKey, new NSString(AVVideoCodecType.Hevc)));

        settings.MaxPhotoDimensions = _photoOutput.MaxPhotoDimensions;

        _photoOutput.CapturePhoto(settings, new PhotoCaptureDelegate(bytes =>
        {
            LastCapturedPhoto = bytes;
            _photoCaptureTcs?.TrySetResult(bytes);

            // Haptic feedback
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var generator = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Medium);
                generator.Prepare();
                generator.ImpactOccurred();
            });

            // Save to Photos library
            if (bytes is not null)
                SaveToPhotoLibrary(bytes);
        }));

        return await _photoCaptureTcs.Task;
    }

    private static void SaveToPhotoLibrary(byte[] bytes)
    {
        PHPhotoLibrary.RequestAuthorization(status =>
        {
            if (status != PHAuthorizationStatus.Authorized) return;
            PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
            {
                var image = UIImage.LoadFromData(NSData.FromArray(bytes));
                if (image is not null)
                    PHAssetChangeRequest.FromImage(image);
            }, null);
        });
    }

    // ──────────────────────────────────────────────
    // Preview attachment
    // ──────────────────────────────────────────────

    public void AttachPreview(object nativeView)
    {
        if (nativeView is UIView uiView && _previewLayer is not null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _previewLayer.Frame = uiView.Bounds;
                uiView.Layer.InsertSublayer(_previewLayer, 0);
            });
        }
    }

    public void DetachPreview()
    {
        MainThread.BeginInvokeOnMainThread(() => _previewLayer?.RemoveFromSuperLayer());
    }

    // ──────────────────────────────────────────────
    // Frame delegate (IAVCaptureVideoDataOutputSampleBufferDelegate)
    // ──────────────────────────────────────────────

    [Export("captureOutput:didOutputSampleBuffer:fromConnection:")]
    public void DidOutputSampleBuffer(
        AVCaptureOutput captureOutput,
        CMSampleBuffer sampleBuffer,
        AVCaptureConnection connection)
    {
        using var pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer;
        if (pixelBuffer is null) return;

        pixelBuffer.Lock(CVPixelBufferLock.ReadOnly);

        int width = (int)pixelBuffer.Width;
        int height = (int)pixelBuffer.Height;
        int bytesPerRow = (int)pixelBuffer.BytesPerRow;
        nint dataPtr = pixelBuffer.BaseAddress;
        int dataLength = bytesPerRow * height;

        var pixelData = new byte[dataLength];
        System.Runtime.InteropServices.Marshal.Copy(dataPtr, pixelData, 0, dataLength);

        pixelBuffer.Unlock(CVPixelBufferLock.ReadOnly);

        FrameReady?.Invoke(this, new CameraFrameEventArgs
        {
            PixelData = pixelData,
            Width = width,
            Height = height,
            BytesPerRow = bytesPerRow
        });
    }

    // ──────────────────────────────────────────────
    // Dispose
    // ──────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _session?.StopRunning();
            _session?.Dispose();
            _videoOutput?.Dispose();
            _photoOutput?.Dispose();
            _previewLayer?.Dispose();
        }
        base.Dispose(disposing);
    }

    // ──────────────────────────────────────────────
    // Inner delegate class for photo capture
    // ──────────────────────────────────────────────

    private class PhotoCaptureDelegate : AVCapturePhotoCaptureDelegate
    {
        private readonly Action<byte[]?> _completion;

        public PhotoCaptureDelegate(Action<byte[]?> completion)
        {
            _completion = completion;
        }

        public override void DidFinishProcessingPhoto(
            AVCapturePhotoOutput output,
            AVCapturePhoto photo,
            NSError? error)
        {
            if (error is not null)
            {
                _completion(null);
                return;
            }
            var data = photo.FileDataRepresentation;
            _completion(data?.ToArray());
        }
    }
}
