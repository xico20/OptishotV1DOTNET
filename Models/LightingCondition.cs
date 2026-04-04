namespace OptishotV1DOTNET.Models;

public enum LightingConditionKind
{
    Unknown,
    TooDark,
    TooBright,
    HarshShadows,
    Good,
    Excellent
}

public class LightingCondition : IEquatable<LightingCondition>
{
    public LightingConditionKind Kind { get; init; }
    public string? Suggestion { get; init; }

    public static LightingCondition Unknown      => new() { Kind = LightingConditionKind.Unknown };
    public static LightingCondition Good         => new() { Kind = LightingConditionKind.Good };
    public static LightingCondition Excellent    => new() { Kind = LightingConditionKind.Excellent };

    public static LightingCondition TooDark(string suggestion) =>
        new() { Kind = LightingConditionKind.TooDark, Suggestion = suggestion };

    public static LightingCondition TooBright(string suggestion) =>
        new() { Kind = LightingConditionKind.TooBright, Suggestion = suggestion };

    public static LightingCondition HarshShadows(string suggestion) =>
        new() { Kind = LightingConditionKind.HarshShadows, Suggestion = suggestion };

    public bool NeedsCoaching => Kind is LightingConditionKind.TooDark
        or LightingConditionKind.TooBright
        or LightingConditionKind.HarshShadows;

    public string CoachingMessage => Suggestion ?? string.Empty;

    public string DisplayLabel => Kind switch
    {
        LightingConditionKind.Unknown      => "Analyzing…",
        LightingConditionKind.TooDark      => "Too Dark",
        LightingConditionKind.TooBright    => "Too Bright",
        LightingConditionKind.HarshShadows => "Harsh Light",
        LightingConditionKind.Good         => "Good Light",
        LightingConditionKind.Excellent    => "Perfect Light ✦",
        _                                  => "Unknown"
    };

    public Color StatusColor => Kind switch
    {
        LightingConditionKind.Unknown      => Colors.Gray,
        LightingConditionKind.TooDark      => Colors.Orange,
        LightingConditionKind.TooBright    => Colors.Orange,
        LightingConditionKind.HarshShadows => Colors.Yellow,
        LightingConditionKind.Good         => Colors.LightGreen,
        LightingConditionKind.Excellent    => Color.FromArgb("#3EB489"), // mint green
        _                                  => Colors.Gray
    };

    public bool Equals(LightingCondition? other) =>
        other is not null && Kind == other.Kind && Suggestion == other.Suggestion;

    public override bool Equals(object? obj) => Equals(obj as LightingCondition);

    public override int GetHashCode() => HashCode.Combine(Kind, Suggestion);
}
