using OptishotV1DOTNET.Core.AR;

namespace OptishotV1DOTNET.Features.Ghost;

/// <summary>
/// Phase 3 — Ghost outline selection and alignment manager.
/// Mirrors Swift GhostGuideManager.
/// Selects the appropriate ghost template for the shooting mode
/// and coordinates with GhostGuideRenderer to display it.
/// Not yet implemented.
/// </summary>
public class GhostGuideManager
{
    private readonly GhostGuideRenderer _renderer;

    public bool IsActive { get; private set; }

    public GhostGuideManager(GhostGuideRenderer renderer)
    {
        _renderer = renderer;
    }

    // TODO: Phase 3 — load ghost template for given shooting mode
    public void Activate(string shootingMode)
    {
        _renderer.Show(shootingMode);
        IsActive = true;
    }

    public void Deactivate()
    {
        _renderer.Hide();
        IsActive = false;
    }
}
