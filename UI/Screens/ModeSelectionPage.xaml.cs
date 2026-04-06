using OptishotV1DOTNET.ViewModels;

namespace OptishotV1DOTNET.UI.Screens;

public partial class ModeSelectionPage : ContentPage
{
    public ModeSelectionPage(ModeSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
