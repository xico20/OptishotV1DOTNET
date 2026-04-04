using OptishotV1DOTNET.Utilities;

namespace OptishotV1DOTNET.Models;

public class DetectedObject : IEquatable<DetectedObject>
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Label { get; init; } = string.Empty;
    public float Confidence { get; init; }

    // Normalized 0-1 coordinates (x, y, width, height)
    public Rect BoundingBox { get; init; }

    public bool IsClutter =>
        Constants.ClutterLabels.Contains(Label.ToLowerInvariant());

    public string DisplayName =>
        char.ToUpper(Label[0]) + Label[1..];

    public bool Equals(DetectedObject? other) =>
        other is not null && Id == other.Id;

    public override bool Equals(object? obj) =>
        Equals(obj as DetectedObject);

    public override int GetHashCode() => Id.GetHashCode();
}
