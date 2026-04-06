using OptishotV1DOTNET.Models;

namespace OptishotV1DOTNET.UI.Overlays;

public partial class CoachingOverlay : ContentView
{
    public static readonly BindableProperty TipsProperty =
        BindableProperty.Create(
            nameof(Tips),
            typeof(IReadOnlyList<CoachingTip>),
            typeof(CoachingOverlay),
            Array.Empty<CoachingTip>(),
            propertyChanged: OnTipsChanged);

    public IReadOnlyList<CoachingTip> Tips
    {
        get => (IReadOnlyList<CoachingTip>)GetValue(TipsProperty);
        set => SetValue(TipsProperty, value);
    }

    public CoachingOverlay()
    {
        InitializeComponent();
    }

    private static void OnTipsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // Animate the stack in/out when tips change
        var overlay = (CoachingOverlay)bindable;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await overlay.TipsStack.FadeTo(0, 100);
            await overlay.TipsStack.FadeTo(1, 200);
        });
    }
}
