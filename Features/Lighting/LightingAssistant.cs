using OptishotV1DOTNET.Core.Vision;
using OptishotV1DOTNET.Models;

namespace OptishotV1DOTNET.Features.Lighting;

/// <summary>
/// Enhances coaching advice with device orientation context.
/// Mirrors Swift LightingAssistant (CMMotionManager pitch tracking).
/// On MAUI we use IDeviceDisplay / Accelerometer for orientation hints.
/// </summary>
public class LightingAssistant
{
    private readonly FrameAnalyzer _frameAnalyzer;

    public string Advice { get; private set; } = string.Empty;
    public float ExposureCompensation { get; private set; } = 0f;

    public event EventHandler? AdviceChanged;

    public LightingAssistant(FrameAnalyzer frameAnalyzer)
    {
        _frameAnalyzer = frameAnalyzer;
        _frameAnalyzer.ResultReady += OnResultReady;
    }

    private void OnResultReady(object? sender, FrameAnalysisResult result)
    {
        string advice = string.Empty;
        float compensation = 0f;

        switch (result.LightingCondition.Kind)
        {
            case LightingConditionKind.TooDark:
                var dir = result.LuminanceStats.BrightestDirectionHint;
                advice = $"Tilt toward the {dir} light source or add more light.";
                compensation = 1.0f;
                break;

            case LightingConditionKind.TooBright:
                float mean = result.LuminanceStats.Mean;
                float stdDev = result.LuminanceStats.StandardDeviation;
                bool isLocalized = stdDev > 0.25f;
                advice = isLocalized
                    ? "Move the direct light source further away or use diffusion."
                    : "Reduce overall light intensity or use a lens hood.";
                compensation = -1.0f;
                break;

            case LightingConditionKind.HarshShadows:
                advice = "Use a reflector or softbox to fill harsh shadows.";
                compensation = 0.3f;
                break;

            case LightingConditionKind.Good:
            case LightingConditionKind.Excellent:
                advice = string.Empty;
                compensation = 0f;
                break;
        }

        if (Advice != advice || Math.Abs(ExposureCompensation - compensation) > 0.01f)
        {
            Advice = advice;
            ExposureCompensation = compensation;
            MainThread.BeginInvokeOnMainThread(() => AdviceChanged?.Invoke(this, EventArgs.Empty));
        }
    }
}
