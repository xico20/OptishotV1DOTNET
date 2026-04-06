using System.Windows.Input;

namespace OptishotV1DOTNET.UI.Components;

public partial class ModeSelector : ContentView
{
    public static readonly BindableProperty LightingEnabledProperty =
        BindableProperty.Create(nameof(LightingEnabled), typeof(bool), typeof(ModeSelector), true,
            propertyChanged: (b, _, _) => ((ModeSelector)b).UpdateLightingAppearance());

    public static readonly BindableProperty CompositionEnabledProperty =
        BindableProperty.Create(nameof(CompositionEnabled), typeof(bool), typeof(ModeSelector), true,
            propertyChanged: (b, _, _) => ((ModeSelector)b).UpdateCompositionAppearance());

    public static readonly BindableProperty ToggleLightingCommandProperty =
        BindableProperty.Create(nameof(ToggleLightingCommand), typeof(ICommand), typeof(ModeSelector));

    public static readonly BindableProperty ToggleCompositionCommandProperty =
        BindableProperty.Create(nameof(ToggleCompositionCommand), typeof(ICommand), typeof(ModeSelector));

    public bool LightingEnabled
    {
        get => (bool)GetValue(LightingEnabledProperty);
        set => SetValue(LightingEnabledProperty, value);
    }

    public bool CompositionEnabled
    {
        get => (bool)GetValue(CompositionEnabledProperty);
        set => SetValue(CompositionEnabledProperty, value);
    }

    public ICommand? ToggleLightingCommand
    {
        get => (ICommand?)GetValue(ToggleLightingCommandProperty);
        set => SetValue(ToggleLightingCommandProperty, value);
    }

    public ICommand? ToggleCompositionCommand
    {
        get => (ICommand?)GetValue(ToggleCompositionCommandProperty);
        set => SetValue(ToggleCompositionCommandProperty, value);
    }

    public ModeSelector()
    {
        InitializeComponent();
        UpdateLightingAppearance();
        UpdateCompositionAppearance();
    }

    private void UpdateLightingAppearance()
    {
        var active = LightingEnabled;
        LightingFrame.Background = active ? new SolidColorBrush(Color.FromArgb("#26FFFFFF")) : new SolidColorBrush(Colors.Transparent);
        LightingIcon.TextColor = active ? Colors.White : Colors.Gray;
        LightingLabel.TextColor = active ? Colors.White : Colors.Gray;
    }

    private void UpdateCompositionAppearance()
    {
        var active = CompositionEnabled;
        CompositionFrame.Background = active ? new SolidColorBrush(Color.FromArgb("#26FFFFFF")) : new SolidColorBrush(Colors.Transparent);
        CompositionIcon.TextColor = active ? Colors.White : Colors.Gray;
        CompositionLabel.TextColor = active ? Colors.White : Colors.Gray;
    }
}
