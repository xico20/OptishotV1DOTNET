namespace OptishotV1DOTNET.Models;

public enum TipCategory
{
    Lighting,
    Composition,
    Alignment,
    Stability,
    General
}

public enum TipSeverity
{
    Info     = 0,
    Suggestion = 1,
    Warning  = 2,
    Critical = 3
}

public class CoachingTip : IEquatable<CoachingTip>
{
    public Guid Id { get; } = Guid.NewGuid();
    public TipCategory Category { get; init; }
    public string Message { get; init; } = string.Empty;
    public TipSeverity Severity { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public string IconGlyph => Category switch
    {
        TipCategory.Lighting    => "☀",
        TipCategory.Composition => "⊞",
        TipCategory.Alignment   => "⊕",
        TipCategory.Stability   => "◎",
        TipCategory.General     => "💡",
        _                       => "●"
    };

    public Color AccentColor => Severity switch
    {
        TipSeverity.Critical   => Colors.Red,
        TipSeverity.Warning    => Colors.Orange,
        TipSeverity.Suggestion => Colors.Yellow,
        _                      => Colors.White
    };

    public bool Equals(CoachingTip? other) =>
        other is not null && Id == other.Id;

    public override bool Equals(object? obj) =>
        Equals(obj as CoachingTip);

    public override int GetHashCode() => Id.GetHashCode();
}
