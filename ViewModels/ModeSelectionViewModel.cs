using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OptishotV1DOTNET.Models;

namespace OptishotV1DOTNET.ViewModels;

public partial class ModeSelectionViewModel : ObservableObject
{
    private readonly AppState _appState;

    public ModeSelectionViewModel(AppState appState)
    {
        _appState = appState;
    }

    public IReadOnlyList<ModeItem> Modes { get; } = new List<ModeItem>
    {
        new(ShootingMode.Aesthetic, "Aesthetic", "✦"),
        new(ShootingMode.Product,   "Product",   "■"),
        new(ShootingMode.Portrait,  "Portrait",  "◉") 
    };

    [RelayCommand]
    private void SelectMode(ShootingMode mode)
    {
        _appState.SelectedMode = mode;
    }
}

public record ModeItem(ShootingMode Mode, string Name, string Icon);
