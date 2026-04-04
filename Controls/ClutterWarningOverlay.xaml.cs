using OptishotV1DOTNET.Models;

namespace OptishotV1DOTNET.Controls;

public partial class ClutterWarningOverlay : ContentView
{
    public static readonly BindableProperty ClutterObjectsProperty =
        BindableProperty.Create(
            nameof(ClutterObjects),
            typeof(IReadOnlyList<DetectedObject>),
            typeof(ClutterWarningOverlay),
            Array.Empty<DetectedObject>(),
            propertyChanged: OnClutterObjectsChanged);

    public IReadOnlyList<DetectedObject> ClutterObjects
    {
        get => (IReadOnlyList<DetectedObject>)GetValue(ClutterObjectsProperty);
        set => SetValue(ClutterObjectsProperty, value);
    }

    public string Message => BuildMessage(ClutterObjects);

    public ClutterWarningOverlay()
    {
        InitializeComponent();
    }

    private static string BuildMessage(IReadOnlyList<DetectedObject> objects)
    {
        if (objects.Count == 0) return string.Empty;
        if (objects.Count == 1) return $"Remove the {objects[0].DisplayName} from the background.";
        if (objects.Count == 2) return $"Remove the {objects[0].DisplayName} and {objects[1].DisplayName}.";
        return $"{objects[0].DisplayName}, {objects[1].DisplayName}, {objects[2].DisplayName} — clean up the background.";
    }

    private static void OnClutterObjectsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var overlay = (ClutterWarningOverlay)bindable;
        overlay.OnPropertyChanged(nameof(Message));
    }
}
