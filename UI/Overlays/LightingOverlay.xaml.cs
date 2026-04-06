using OptishotV1DOTNET.Models;

namespace OptishotV1DOTNET.UI.Overlays;

public partial class LightingOverlay : ContentView
{
    public static readonly BindableProperty ConditionProperty =
        BindableProperty.Create(
            nameof(Condition),
            typeof(LightingCondition),
            typeof(LightingOverlay),
            LightingCondition.Unknown,
            propertyChanged: OnConditionChanged);

    public LightingCondition Condition
    {
        get => (LightingCondition)GetValue(ConditionProperty);
        set => SetValue(ConditionProperty, value);
    }

    public string LabelText => Condition?.DisplayLabel ?? "Analyzing…";
    public Color StatusColor => Condition?.StatusColor ?? Colors.Gray;

    public LightingOverlay()
    {
        InitializeComponent();
    }

    private static void OnConditionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var overlay = (LightingOverlay)bindable;
        overlay.OnPropertyChanged(nameof(LabelText));
        overlay.OnPropertyChanged(nameof(StatusColor));

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await overlay.BadgeFrame.ScaleTo(0.95, 150, Easing.CubicOut);
            await overlay.BadgeFrame.ScaleTo(1.0, 150, Easing.CubicIn);
        });
    }
}
