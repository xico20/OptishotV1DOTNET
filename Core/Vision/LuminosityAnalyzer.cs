using OptishotV1DOTNET.Models;
using OptishotV1DOTNET.Utilities;

namespace OptishotV1DOTNET.Core.Vision;

/// <summary>
/// Analyzes raw BGRA pixel data for luminance statistics.
/// Uses ITU-R BT.709 weights (standard for modern displays/cameras).
/// Also computes sharpness (gradient magnitude) and 8x8 grid means (for shake detection).
/// </summary>
public class LuminosityAnalyzer
{
    // ITU-R BT.709 luminance weights — green dominates because the eye is most sensitive to it
    private const float LumR = 0.2126f;
    private const float LumG = 0.7152f;
    private const float LumB = 0.0722f;

    /// <summary>
    /// Analyzes a BGRA frame. Samples 1 in 4 pixels for performance (~6ms on low-end devices).
    /// Returns luminance stats and a classified LightingCondition.
    /// </summary>
    public (LuminanceStats Stats, LightingCondition Condition) Analyze(
        byte[] pixelData, int width, int height, int bytesPerRow)
    {
        if (pixelData.Length == 0)
            return (new LuminanceStats(), LightingCondition.Unknown);

        // Accumulators for each screen quadrant (top-left, top-right, bottom-left, bottom-right)
        double[] quadSum = new double[4];
        int[] quadCount = new int[4];
        double totalSum = 0;
        double totalSumSq = 0;
        int totalCount = 0;

        // 8x8 grid means — used to detect camera shake between frames
        double[] gridSum = new double[64];
        int[] gridCount = new int[64];

        // Gradient accumulator for sharpness score
        double gradientSum = 0;
        int gradientCount = 0;

        int halfH = height / 2;
        int halfW = width / 2;

        // Sample every 4th pixel (stride of 2 in x and y = 1/4 pixels)
        for (int y = 0; y < height; y += 2)
        {
            int rowOffset = y * bytesPerRow;
            for (int x = 0; x < width; x += 2)
            {
                int idx = rowOffset + x * 4;
                if (idx + 3 >= pixelData.Length) continue;

                // BGRA byte order (Android native)
                float b = pixelData[idx]     / 255f;
                float g = pixelData[idx + 1] / 255f;
                float r = pixelData[idx + 2] / 255f;

                float lum = LumR * r + LumG * g + LumB * b; // perceptual brightness 0..1

                totalSum += lum;
                totalSumSq += lum * lum;
                totalCount++;

                // Quadrant
                int q = (y < halfH ? 0 : 2) + (x < halfW ? 0 : 1);
                quadSum[q] += lum;
                quadCount[q]++;

                // 8x8 grid cell
                int cellX = Math.Min(7, x * 8 / width);
                int cellY = Math.Min(7, y * 8 / height);
                int cell = cellY * 8 + cellX;
                gridSum[cell] += lum;
                gridCount[cell]++;

                // Gradient magnitude with right and bottom neighbors (sharpness)
                float gradH = 0, gradV = 0;
                int rightIdx = rowOffset + (x + 2) * 4;
                if (rightIdx + 2 < pixelData.Length)
                {
                    float rLum = LumR * (pixelData[rightIdx + 2] / 255f)
                               + LumG * (pixelData[rightIdx + 1] / 255f)
                               + LumB * (pixelData[rightIdx]     / 255f);
                    gradH = MathF.Abs(rLum - lum);
                }
                int bottomIdx = (y + 2) * bytesPerRow + x * 4;
                if (bottomIdx + 2 < pixelData.Length)
                {
                    float bLum = LumR * (pixelData[bottomIdx + 2] / 255f)
                               + LumG * (pixelData[bottomIdx + 1] / 255f)
                               + LumB * (pixelData[bottomIdx]     / 255f);
                    gradV = MathF.Abs(bLum - lum);
                }
                gradientSum += gradH + gradV;
                gradientCount++;
            }
        }

        if (totalCount == 0)
            return (new LuminanceStats(), LightingCondition.Unknown);

        float mean = (float)(totalSum / totalCount);
        float variance = (float)(totalSumSq / totalCount - mean * mean);
        float stdDev = MathF.Sqrt(MathF.Max(0, variance));

        var quadMeans = new float[4];
        for (int i = 0; i < 4; i++)
            quadMeans[i] = quadCount[i] > 0 ? (float)(quadSum[i] / quadCount[i]) : 0f;

        var gridMeans = new float[64];
        for (int i = 0; i < 64; i++)
            gridMeans[i] = gridCount[i] > 0 ? (float)(gridSum[i] / gridCount[i]) : 0f;

        // Sharpness: normalize mean gradient to 0..1 (typical sharp scene ~0.05–0.15)
        float rawSharpness = gradientCount > 0 ? (float)(gradientSum / gradientCount) : 0f;
        float sharpnessScore = Math.Min(1f, rawSharpness / 0.15f);

        var stats = new LuminanceStats
        {
            Mean = mean,
            StandardDeviation = stdDev,
            SharpnessScore = sharpnessScore,
            QuadrantMeans = quadMeans,
            GridMeans = gridMeans
        };

        var condition = Classify(mean, stdDev, stats.BrightestDirectionHint);
        return (stats, condition);
    }

    // Maps mean brightness and contrast (stdDev) to a coaching label.
    private static LightingCondition Classify(float mean, float stdDev, string brightestDir)
    {
        if (mean < Constants.DarkThreshold)
        {
            string suggestion = mean < 0.10f
                ? $"Very dark scene. Move toward the {brightestDir} light source."
                : $"Too dark. Add light from the {brightestDir} direction.";
            return LightingCondition.TooDark(suggestion);
        }

        if (mean > Constants.BrightThreshold)
            return LightingCondition.TooBright("Too much light. Reduce direct light or use diffusion.");

        if (stdDev > Constants.HarshContrastThreshold)
            return LightingCondition.HarshShadows("Harsh shadows detected. Try diffusing the light source.");

        // Sweet spot: mean in [0.35, 0.65], low contrast, but not a uniformly flat/obscured scene
        if (mean is >= Constants.ExcellentLowMean and <= Constants.ExcellentHighMean
            && stdDev >= Constants.ExcellentMinStdDev
            && stdDev < Constants.ExcellentMaxStdDev)
            return LightingCondition.Excellent;

        return LightingCondition.Good;
    }
}
