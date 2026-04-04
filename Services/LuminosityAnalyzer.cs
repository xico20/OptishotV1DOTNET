using OptishotV1DOTNET.Models;
using OptishotV1DOTNET.Utilities;

namespace OptishotV1DOTNET.Services;

/// <summary>
/// Analyzes raw BGRA pixel data for luminance statistics.
/// Mirrors the Swift LuminosityAnalyzer using ITU-R BT.709 formula.
/// </summary>
public class LuminosityAnalyzer
{
    // ITU-R BT.709 luminance weights
    private const float LumR = 0.2126f;
    private const float LumG = 0.7152f;
    private const float LumB = 0.0722f;

    /// <summary>
    /// Analyzes a BGRA frame. Samples 1 in 4 pixels for performance.
    /// Returns luminance stats and a classified LightingCondition.
    /// </summary>
    public (LuminanceStats Stats, LightingCondition Condition) Analyze(
        byte[] pixelData, int width, int height, int bytesPerRow)
    {
        if (pixelData.Length == 0)
            return (new LuminanceStats(), LightingCondition.Unknown);

        // Quadrant accumulators: [TL, TR, BL, BR]
        double[] quadSum = new double[4];
        int[] quadCount = new int[4];
        double totalSum = 0;
        double totalSumSq = 0;
        int totalCount = 0;

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

                // BGRA byte order
                float b = pixelData[idx]     / 255f;
                float g = pixelData[idx + 1] / 255f;
                float r = pixelData[idx + 2] / 255f;

                float lum = LumR * r + LumG * g + LumB * b;

                totalSum += lum;
                totalSumSq += lum * lum;
                totalCount++;

                // Quadrant
                int q = (y < halfH ? 0 : 2) + (x < halfW ? 0 : 1);
                quadSum[q] += lum;
                quadCount[q]++;
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

        var stats = new LuminanceStats
        {
            Mean = mean,
            StandardDeviation = stdDev,
            QuadrantMeans = quadMeans
        };

        var condition = Classify(mean, stdDev, stats.BrightestDirectionHint);
        return (stats, condition);
    }

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

        if (mean is >= Constants.ExcellentLowMean and <= Constants.ExcellentHighMean
            && stdDev < Constants.ExcellentMaxStdDev)
            return LightingCondition.Excellent;

        return LightingCondition.Good;
    }
}
