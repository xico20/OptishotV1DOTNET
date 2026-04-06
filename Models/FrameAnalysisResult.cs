namespace OptishotV1DOTNET.Models;

public class LuminanceStats
{
    public float Mean { get; init; }
    public float StandardDeviation { get; init; }

    // 0=blurry, 1=sharp — based on mean gradient magnitude across sampled pixels
    public float SharpnessScore { get; init; }

    // [TopLeft, TopRight, BottomLeft, BottomRight]
    public float[] QuadrantMeans { get; init; } = new float[4];

    // 8x8 grid of cell means — used by FrameAnalyzer to detect camera shake between frames
    public float[] GridMeans { get; init; } = new float[64];

    public int BrightestQuadrant
    {
        get
        {
            int idx = 0;
            float max = QuadrantMeans[0];
            for (int i = 1; i < QuadrantMeans.Length; i++)
            {
                if (QuadrantMeans[i] > max) { max = QuadrantMeans[i]; idx = i; }
            }
            return idx;
        }
    }

    public string BrightestDirectionHint => BrightestQuadrant switch
    {
        0 => "upper-left",
        1 => "upper-right",
        2 => "lower-left",
        3 => "lower-right",
        _ => "side"
    };
}

public class FrameAnalysisResult
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public LightingCondition LightingCondition { get; init; } = LightingCondition.Unknown;
    public LuminanceStats LuminanceStats { get; init; } = new();
    public IReadOnlyList<DetectedObject> DetectedObjects { get; init; } = Array.Empty<DetectedObject>();

    // 0=still, 1=very shaky — computed by FrameAnalyzer comparing consecutive grid means
    public float ShakeScore { get; init; }

    public IEnumerable<DetectedObject> ClutterObjects =>
        DetectedObjects.Where(o => o.IsClutter);
}
