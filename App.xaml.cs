using OptishotV1DOTNET.Views;
using OptishotV1DOTNET.ViewModels;

namespace OptishotV1DOTNET;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly AppState _appState;

    public App(IServiceProvider services, AppState appState)
    {
        InitializeComponent();
        _services = services;
        _appState = appState;

        // Start with mode selection screen
        var modeSelectionPage = services.GetRequiredService<ModeSelectionPage>();
        MainPage = new NavigationPage(modeSelectionPage)
        {
            BarBackgroundColor = Colors.Black
        };

        // Navigate to CameraPage when a mode is selected
        _appState.PropertyChanged += OnAppStateChanged;
    }

    private void OnAppStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.SelectedMode))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_appState.SelectedMode.HasValue)
                {
                    var cameraPage = _services.GetRequiredService<CameraPage>();
                    if (MainPage is NavigationPage navPage)
                        await navPage.PushAsync(cameraPage);
                }
            });
        }
    }
}
