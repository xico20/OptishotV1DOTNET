using OptishotV1DOTNET.ViewModels;

namespace OptishotV1DOTNET.Views;

public partial class ModeSelectionPage : ContentPage
{
    public ModeSelectionPage(ModeSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
