# OptiShot iOS MVP — Architecture

## Philosophy
**"We don't just fix the photo. We fix the photographer."**

OptiShot is an Outcome-First camera app. The AI Instructor provides real-time
AR coaching *before* the user presses the shutter button.

---

## Xcode Project Structure

```
OptiShot/
├── App/
│   ├── OptiShotApp.swift              # @main entry point
│   └── AppState.swift                 # Global app-level state
│
├── Core/
│   ├── Camera/
│   │   ├── CameraManager.swift        # AVCaptureSession orchestrator
│   │   ├── CameraPreviewView.swift    # UIViewRepresentable for live preview
│   │   └── PhotoCaptureDelegate.swift # AVCapturePhotoCaptureDelegate
│   ├── Vision/
│   │   ├── FrameAnalyzer.swift        # Central pipeline dispatcher
│   │   ├── LuminosityAnalyzer.swift   # Lighting / exposure analysis
│   │   └── ObjectDetectionPipeline.swift  # YOLO-based object detection
│   ├── AR/
│   │   └── GhostGuideRenderer.swift   # ARKit ghost overlay engine
│   └── ML/
│       └── ModelManager.swift         # CoreML model lifecycle
│
├── Features/
│   ├── Coaching/
│   │   └── CoachingEngine.swift       # Aggregates all analysis → coaching tips
│   ├── Lighting/
│   │   └── LightingAssistant.swift    # Directional light advisor
│   ├── Ghost/
│   │   └── GhostGuideManager.swift    # Ghost outline selection & alignment
│   └── Composition/
│       └── CompositionCleanup.swift   # Background clutter detection
│
├── UI/
│   ├── Screens/
│   │   └── CameraScreen.swift         # Main camera SwiftUI view
│   ├── Overlays/
│   │   ├── CoachingOverlay.swift      # Floating coaching tip cards
│   │   ├── LightingOverlay.swift      # Lighting direction arrows
│   │   └── ClutterWarningOverlay.swift# "Remove the water bottle" banners
│   └── Components/
│       ├── ShutterButton.swift        # Animated capture button
│       └── ModeSelector.swift         # Feature toggle strip
│
├── Models/
│   ├── CoachingTip.swift              # Data model for coaching messages
│   ├── DetectedObject.swift           # Bounding box + label + confidence
│   ├── LightingCondition.swift        # Enum for lighting states
│   └── FrameAnalysisResult.swift      # Aggregated per-frame analysis
│
├── Utilities/
│   ├── FrameThrottler.swift           # Adaptive frame-skip logic
│   ├── PixelBufferExtensions.swift    # CVPixelBuffer helpers
│   └── Constants.swift                # App-wide constants
│
└── Resources/
    ├── Assets.xcassets/               # App icons, ghost templates
    └── MLModels/                      # .mlmodelc compiled CoreML bundles
        └── (YOLOv8s.mlmodelc)         # Object detection model
```

---

## Data Flow

```
AVCaptureSession (60 fps)
       │
       ▼
  CameraManager  ──► Live preview (full-res, no lag)
       │
       │  every Nth frame (adaptive throttle)
       ▼
  FrameAnalyzer (background DispatchQueue)
       ├── LuminosityAnalyzer   → LightingCondition
       ├── ObjectDetectionPipeline → [DetectedObject]
       └── (future: PoseEstimation, DepthMap)
       │
       ▼
  CoachingEngine (aggregates results)
       │
       ▼
  @Published [CoachingTip]  ──► SwiftUI Overlays (main thread)
```

---

## Performance Strategy

| Concern            | Strategy                                          |
|--------------------|---------------------------------------------------|
| Frame rate         | ML runs on background queue; preview untouched     |
| Battery            | Adaptive throttle: analyze 4-10 fps, not 60       |
| Thermal            | Downsample frames to 640×480 for ML inference      |
| Memory             | Reuse CVPixelBuffer pool; no frame copies          |
| UI responsiveness  | Combine debounce on coaching tips (200ms)          |

---

## Key Frameworks

- **AVFoundation** — Camera session, video output, photo capture
- **Vision** — VNImageRequestHandler for luminosity + object detection
- **CoreML** — YOLOv8s (or MobileNetSSD) for real-time detection
- **ARKit** — Ghost guide overlays (ARSCNView or RealityKit)
- **SwiftUI** — All UI chrome; wraps UIKit camera via UIViewRepresentable
- **Combine** — Reactive state propagation from analyzers → UI
