using OptishotV1DOTNET.Services;
using OptishotV1DOTNET.ViewModels;

namespace OptishotV1DOTNET.Views;

public partial class CameraPage : ContentPage
{
    private readonly CameraViewModel _viewModel;
    private readonly ICameraService _cameraService;

    public CameraPage(CameraViewModel viewModel, ICameraService cameraService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _cameraService = cameraService;
        BindingContext = viewModel;

        // Attach camera service to the preview view
        CameraPreview.CameraService = cameraService;

        // Update thumbnail when a photo is captured
        _viewModel.AppState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.LastCapturedPhoto)
                && _viewModel.AppState.LastCapturedPhoto is { } photoBytes)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ThumbnailImage.Source = ImageSource.FromStream(
                        () => new MemoryStream(photoBytes));
                });
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();

        // Shutter press animation
        ShutterBtn.Pressed += async (_, _) =>
            await ShutterBtn.ScaleTo(0.85, 80, Easing.CubicOut);
        ShutterBtn.Released += async (_, _) =>
            await ShutterBtn.ScaleTo(1.0, 150, Easing.SpringOut);
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _viewModel.CleanupAsync();
    }
}
