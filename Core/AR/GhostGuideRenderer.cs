namespace OptishotV1DOTNET.Core.AR;

/// <summary>
/// Phase 3 — AR ghost guide overlay engine.
/// Mirrors Swift GhostGuideRenderer (ARKit ARSCNView / RealityKit).
/// On Android will use ARCore or a custom overlay approach.
/// Not yet implemented.
/// </summary>
public class GhostGuideRenderer
{
    public bool IsActive { get; private set; }

    // TODO: Phase 3 — render ghost silhouette overlay on camera preview
    public void Show(string ghostTemplateName) { }
    public void Hide() { }
}
