using OptishotV1DOTNET.Core.Vision;
using OptishotV1DOTNET.Models;
using OptishotV1DOTNET.Utilities;
using OptishotV1DOTNET.ViewModels;

namespace OptishotV1DOTNET.Features.Coaching;

/// <summary>
/// Turns raw FrameAnalysisResults into actionable coaching tips shown to the user.
/// Logic: debounce incoming results → filter by cooldown → sort by severity → cap at 3 tips.
/// Mirrors Swift CoachingEngine.
/// </summary>
public class CoachingEngine
{
    private readonly FrameAnalyzer _frameAnalyzer;
    private readonly AppState _appState;

    // Tracks when each tip category was last shown, to enforce per-category cooldown
    private readonly Dictionary<TipCategory, DateTime> _lastShownByCategory = new();

    // Separate cooldown for Excellent — must not share with warning tips (different timescale)
    private DateTime _lastExcellentShown = DateTime.MinValue;

    // Debounce: we cancel and restart a delay on every new result.
    // The tip only fires after the scene is stable for CoachingDebounceSecs.
    private CancellationTokenSource? _debounceCts;

    public event EventHandler<IReadOnlyList<CoachingTip>>? TipsUpdated;
    public IReadOnlyList<CoachingTip> ActiveTips { get; private set; } = Array.Empty<CoachingTip>();

    public CoachingEngine(FrameAnalyzer frameAnalyzer, AppState appState)
    {
        _frameAnalyzer = frameAnalyzer;
        _appState = appState;
        _frameAnalyzer.ResultReady += OnResultReady;
    }

private void OnResultReady(object? sender, FrameAnalysisResult result)
    {
        // Cancel the previous pending tip — we only want to coach on stable scenes
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Delay(TimeSpan.FromSeconds(Constants.CoachingDebounceSecs), token)
            .ContinueWith(_ =>
            {
                if (token.IsCancellationRequested) return;
                ProcessResult(result);
            }, TaskScheduler.Default);
    }

    private void ProcessResult(FrameAnalysisResult result)
    {
        var candidates = new List<CoachingTip>();
        CoachingTip? excellentTip = null;

        // Lighting tip — problems get a warning; Excellent gets a celebration
        if (result.LightingCondition.NeedsCoaching)
        {
            var tip = BuildLightingTip(result);
            if (PassesCooldown(tip.Category))
                candidates.Add(tip);
        }
        else if (result.LightingCondition.Kind == LightingConditionKind.Excellent)
        {
            //AQUI neste elseif uma ideia era colocar a possibilidade de tirar a foto automaticamente
            var tip = new CoachingTip
            {
                Category = TipCategory.Lighting,
                Message = BuildExcellentTip(),
                Severity = TipSeverity.Suggestion
            };
            if ((DateTime.UtcNow - _lastExcellentShown).TotalSeconds >= Constants.ExcellentCooldownSecs)
            {
                candidates.Add(tip);
                excellentTip = tip;
            }
        }
        else if (result.LightingCondition.Kind == LightingConditionKind.Good)
        {
            var tip = new CoachingTip
            {
                Category = TipCategory.Lighting,
                Message = BuildGoodLightTip(result.LuminanceStats.Mean, result.LuminanceStats.StandardDeviation),
                Severity = TipSeverity.Suggestion
            };
            if (PassesCustomCooldown(tip.Category, Constants.GoodLightCooldownSecs))
                candidates.Add(tip);
        }
        // Shake tip — camera is moving too much (takes priority over blur)
        if (result.ShakeScore > Constants.ShakeThreshold)
        {
            var tip = new CoachingTip
            {
                Category = TipCategory.Stability,
                // Message = "Camera is moving — hold still for a sharp shot.",
                Message = "Câmara a mover — fica quieto para uma foto nítida.",
                Severity = TipSeverity.Warning
            };
            if (PassesCustomCooldown(tip.Category, Constants.StabilityCooldownSecs))
                _ = tip; // NOT MVP: candidates.Add(tip);
        }
        // Blur tip — image is soft even when not shaking (focus or lens issue)
        else if (result.LuminanceStats.SharpnessScore < Constants.BlurThreshold)
        {
            var tip = new CoachingTip
            {
                Category = TipCategory.Stability,
                // Message = "Image is blurry — hold the camera steady and wait for focus.",
                Message = "Imagem desfocada — segura a câmara firme e aguarda o foco.",
                Severity = TipSeverity.Warning
            };
            if (PassesCustomCooldown(tip.Category, Constants.StabilityCooldownSecs))
                _ = tip; // NOT MVP: candidates.Add(tip);
        }

        // Tilt tip — image is crooked (uses device accelerometer via FrameAnalyzer)
        float absTilt = MathF.Abs(result.TiltDegrees);
        if (absTilt >= Constants.TiltThresholdDegrees)
        {
            int degrees = (int)MathF.Round(absTilt);
            // string direction = result.TiltDegrees > 0 ? "left" : "right";
            string direction = result.TiltDegrees > 0 ? "esquerda" : "direita";
            var tip = new CoachingTip
            {
                Category = TipCategory.Alignment,
                // Message = $"Tilted {degrees}° — rotate {direction} to straighten.",
                Message = $"Inclinado {degrees}° — roda para a {direction} para endireitar.",
                Severity = TipSeverity.Suggestion
            };
            if (PassesCustomCooldown(tip.Category, Constants.TiltCooldownSecs))
                _ = tip; // NOT MVP: candidates.Add(tip);
        }

        // Clutter tip — requires object detection (YOLOv8s)
        var clutterObjects = result.ClutterObjects.ToList();
        if (clutterObjects.Count > 0)
        {
            var tip = BuildClutterTip(clutterObjects);
            _ = tip; // NOT MVP: candidates.Add(tip);
        }

        // Most severe tips first; cap at MaxVisibleTips (3) to avoid overwhelming the user
        var tips = candidates
            .OrderByDescending(t => (int)t.Severity)
            .Take(Constants.MaxVisibleTips)
            .ToList();

        // Update cooldown timestamps for shown categories
        foreach (var tip in tips)
        {
            _lastShownByCategory[tip.Category] = DateTime.UtcNow;
            CoachingLogger.TipShown(tip.Category.ToString(), tip.Message);
        }

        // Update Excellent cooldown independently — must not share key with warning tips
        if (excellentTip != null && tips.Contains(excellentTip))
            _lastExcellentShown = DateTime.UtcNow;

        ActiveTips = tips;

        // Log lighting condition on every result (even when no tip fires)
        CoachingLogger.LightingChanged(
            result.LightingCondition.Kind.ToString(),
            result.LuminanceStats.Mean,
            result.LuminanceStats.StandardDeviation);

        // Tips update must happen on the UI thread (touches MAUI bindings)
        MainThread.BeginInvokeOnMainThread(() =>
            TipsUpdated?.Invoke(this, tips));
    }

    // Returns true if the category hasn't been shown recently (or never shown)
    private bool PassesCooldown(TipCategory category)
    {
        if (!_lastShownByCategory.TryGetValue(category, out var lastShown))
            return true;
        return (DateTime.UtcNow - lastShown).TotalSeconds >= Constants.CoachingCooldownSecs;
    }

    private bool PassesCustomCooldown(TipCategory category, double cooldownSecs)
    {
        if (!_lastShownByCategory.TryGetValue(category, out var lastShown))
            return true;
        return (DateTime.UtcNow - lastShown).TotalSeconds >= cooldownSecs;
    }

    private ShootingMode CurrentMode => _appState.SelectedMode ?? ShootingMode.Aesthetic;

    // Tells the user what to fix to go from Good → Excellent
    private string BuildGoodLightTip(float mean, float stdDev)
    {
        var mode = CurrentMode;
        if (stdDev > Constants.ExcellentMaxStdDev)
            return mode switch
            {
                // ShootingMode.Product  => "Uneven light hiding product detail — try diffusing for flat coverage.",
                ShootingMode.Product  => "Luz irregular a esconder detalhe — tenta difundir para cobertura uniforme.",
                // ShootingMode.Portrait => "Uneven light creates harsh shadows — diffuse for softer skin tones.",
                ShootingMode.Portrait => "Luz irregular cria sombras duras — difunde para tons de pele mais suaves.",
                // _                     => "Try diffusing the light for more even coverage."
                _                     => "Tenta difundir a luz para uma cobertura mais uniforme."
            };
        if (mean < Constants.ExcellentLowMean)
            return mode switch
            {
                // ShootingMode.Product  => "A bit more light will bring out product texture and detail.",
                ShootingMode.Product  => "Um pouco mais de luz vai realçar a textura e detalhe do produto.",
                // ShootingMode.Portrait => "Add a touch more light for a clean, flattering portrait.",
                ShootingMode.Portrait => "Adiciona um pouco mais de luz para um retrato limpo e favorável.",
                // _                     => "Add a bit more light to reach perfect conditions."
                _                     => "Adiciona um pouco mais de luz para atingir condições perfeitas."
            };
        return mode switch
        {
            // ShootingMode.Product  => "Reduce light slightly for balanced product exposure.",
            ShootingMode.Product  => "Reduz um pouco a luz para uma exposição equilibrada do produto.",
            // ShootingMode.Portrait => "Dial back the light slightly for perfect portrait conditions.",
            ShootingMode.Portrait => "Reduz ligeiramente a luz para condições perfeitas de retrato.",
            // _                     => "Reduce the light slightly for perfect conditions."
            _                     => "Reduz ligeiramente a luz para condições perfeitas."
        };
    }

    private string BuildExcellentTip() => CurrentMode switch
    {
        // ShootingMode.Product  => "Perfect light for product — crisp conditions for a great shot.",
        ShootingMode.Product  => "Luz perfeita para produto — condições ideais para uma boa foto.",
        // ShootingMode.Portrait => "Perfect light for a portrait ✦ Shoot now.",
        ShootingMode.Portrait => "Luz perfeita para retrato ✦ Fotografa agora.",
        // _                     => "Perfect light! ✦ Great conditions for a shot."
        _                     => "Luz perfeita! ✦ Ótimas condições para fotografar."
    };

    // Very dark (mean < 0.10) is Critical; just dark is Warning
    private CoachingTip BuildLightingTip(FrameAnalysisResult result)
    {
        var mean = result.LuminanceStats.Mean;
        var severity = mean < 0.10f ? TipSeverity.Critical : TipSeverity.Warning;
        var dir = result.LuminanceStats.BrightestDirectionHint;
        var mode = CurrentMode;

        string message = result.LightingCondition.Kind switch
        {
            LightingConditionKind.TooDark      => BuildTooDarkMessage(mean, dir, mode),
            LightingConditionKind.TooBright    => BuildTooBrightMessage(mode),
            LightingConditionKind.HarshShadows => BuildHarshShadowsMessage(mode),
            _                                  => result.LightingCondition.CoachingMessage
        };

        return new CoachingTip
        {
            Category = TipCategory.Lighting,
            Message = message,
            Severity = severity
        };
    }

    private static string BuildTooDarkMessage(float mean, string dir, ShootingMode mode) => mode switch
    {
        ShootingMode.Product => mean < 0.10f
            // ? $"Too dark for product detail — move toward the {dir} light."
            ? $"Demasiado escuro para detalhe — move-te para a luz {dir}."
            // : $"Underexposed — add fill light from the {dir}.",
            : $"Subexposto — adiciona luz de preenchimento do lado {dir}.",
        ShootingMode.Portrait => mean < 0.10f
            // ? $"Subject is too dark — move them toward the {dir} light."
            ? $"Sujeito demasiado escuro — move-o para a luz {dir}."
            // : $"Too dark for a portrait — face the {dir} light source.",
            : $"Demasiado escuro para retrato — vira-te para a luz {dir}.",
        _ => mean < 0.10f
            // ? $"Very dark scene — move toward the {dir} light for mood and depth."
            ? $"Cena muito escura — move-te para a luz {dir} para criar ambiente."
            // : $"Too dark — add light from the {dir} direction."
            : $"Demasiado escuro — adiciona luz do lado {dir}."
    };

    private static string BuildTooBrightMessage(ShootingMode mode) => mode switch
    {
        // ShootingMode.Product  => "Overexposed — reduce direct light or add diffusion to protect highlights.",
        ShootingMode.Product  => "Sobreexposto — reduz a luz direta ou usa difusão para proteger as altas luzes.",
        // ShootingMode.Portrait => "Too bright and harsh — diffuse the light for flattering skin tones.",
        ShootingMode.Portrait => "Demasiado brilhante — difunde a luz para tons de pele mais suaves.",
        // _                     => "Too much light — reduce direct light or use diffusion."
        _                     => "Luz a mais — reduz a luz direta ou usa difusão."
    };

    private static string BuildHarshShadowsMessage(ShootingMode mode) => mode switch
    {
        // ShootingMode.Product  => "Harsh shadows hiding product detail — diffuse or bounce the light.",
        ShootingMode.Product  => "Sombras duras a esconder detalhe — difunde ou reflete a luz.",
        // ShootingMode.Portrait => "Harsh shadows are unflattering — diffuse the light or reposition your subject.",
        ShootingMode.Portrait => "Sombras duras pouco favoráveis — difunde a luz ou reposiciona o sujeito.",
        // _                     => "Harsh shadows — try diffusing the light for a softer, more balanced look."
        _                     => "Sombras duras — tenta difundir a luz para um resultado mais suave."
    };

    // Message adapts based on how many cluttered objects are detected (1, 2, or 3+)
    private static CoachingTip BuildClutterTip(List<DetectedObject> objects)
    {
        string message = objects.Count switch
        {
            // 1 => $"Remove the {objects[0].DisplayName} from the background.",
            1 => $"Remove {objects[0].DisplayName} do fundo.",
            // 2 => $"Remove the {objects[0].DisplayName} and {objects[1].DisplayName}.",
            2 => $"Remove {objects[0].DisplayName} e {objects[1].DisplayName}.",
            // _ => $"{objects[0].DisplayName}, {objects[1].DisplayName}, {objects[2].DisplayName} — clean up the background."
            _ => $"{objects[0].DisplayName}, {objects[1].DisplayName}, {objects[2].DisplayName} — limpa o fundo."
        };

        return new CoachingTip
        {
            Category = TipCategory.Composition,
            Message = message,
            Severity = TipSeverity.Suggestion
        };
    }
}
