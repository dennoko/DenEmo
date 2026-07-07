# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

DenEmo is a Unity Editor extension (IMGUI) for editing and animating blendshapes on `SkinnedMeshRenderer` components, primarily for VRChat avatar facial animations. It supports two modes: **Single Frame (Pose)** for static expressions, and **Multi Frame (Animation)** for recording keyframe-based animation clips.

## Development Environment

No build or test commands exist — this is a Unity Editor extension loaded automatically from `Assets/Editor/`. Changes to `.cs` files are hot-reloaded by Unity on save. Open the DenEmo window from the Unity menu `dennokoworks/DenEmo`.

## Architecture

### Mode split

`DenEmoWindow` hosts two top-level modes (`EditorMode.Pose` / `EditorMode.Animation`) switched by `SwitchMode()`. Mode state is persisted per-project via `DenEmoProjectPrefs`.

### Object ownership

```
DenEmoWindow                  ← EditorWindow; lifecycle, settings persistence, UI Toolkit chrome routing
  AnimationModeUI             ← Animation mode orchestrator; owns ClipModel + Preview + Playback
    AnimationClipModel        ← Clip state (FPS, length, CurrentTime, keyframe queries)
    AnimationPreviewController← Evaluates blendShape curves → SMR weights; records/deletes keyframes
  TimelineUITKView            ← Timeline (UI Toolkit; Painter2D canvas). Used both inline (Animation mode) and in DenEmoTimelineWindow
  ShapeKeyListUI              ← Blendshape list with sliders (partial class: .cs / .Rows.cs / .Segments.cs)
ShapeKeyModel                 ← All blendshapes on active SMR(s); search/filter/group logic
```

### Key patterns

**AnimationDrawContext** — Created by `AnimationModeUI.BuildDrawContext()` and passed into `ShapeKeyListUI.DrawList()`. Injects animation-mode callbacks (keyframe toggle, value recording while sliding) without a direct dependency from `ShapeKeyListUI` to animation types.

**Partial classes** — `DenEmoWindow` and `ShapeKeyListUI` are each split across multiple `.cs` files by functional area. When adding a new method, choose the file whose concern it matches (`Sections`, `Preferences`, `VertexFilter`, `Rows`, `Segments`).

**Preview isolation** — `AnimationPreviewController` does NOT use Unity's `AnimationMode.StartAnimationMode()` (which resets bone transforms). It evaluates only `blendShape.*` curves manually via `AnimationCurve.Evaluate()` and writes weights directly to the SMR. The curve cache (`_curveCache`) must be invalidated by calling `SetCacheDirty()` after any clip mutation, followed by `SampleAt()` to refresh the viewport.

**UI Toolkit only** — All UI is UI Toolkit (UXML/USS); no IMGUI drawing remains except the SceneView vertex-picking overlay in `DenEmoWindow.VertexFilter.cs` (Handles/GUI, which USS can't reach — uses a local `GUIStyle` with the theme's body color hardcoded). `DenEmoTheme` has been deleted.

**Localization** — All user-visible strings go through `DenEmoLoc.T(key)` / `DenEmoLoc.Tf(key, args)`. Add keys to both `JA` and `EN` dictionaries in `Utils/DenEmoLocalization.cs`.

**Undo** — Keyframe mutations call `Undo.RecordObject(clip, label)` before modifying the `AnimationCurve`. After any Undo/Redo event, `Preview.SetCacheDirty()` and `Preview.SampleAt()` must be called to keep the viewport consistent.

## Design system

Colors and styles live in USS: theme tokens in `UI/DennokoTheme.uss`, project-specific classes/variables in `UI/DenEmoStyles.uss`. Full spec: `dennokoworks_color_schema/forUnity/`. Key conventions:

- Every root gets `dennoko-root` (theme entry point); C# fallback bg is `new Color32(0x12,0x12,0x12,0xFF)`.
- Colors go through USS variables (`var(--dennoko-*)`) only — never hardcode except in the theme's variable definitions.
- `--dennoko-surface-0/1/2` = elevation layers; `.dennoko-card` / `.dennoko-card-outer` = bordered containers; `.dennoko-mini-button` = small inline button; `.dennoko-chip` (+ `dennoko-button-active`) = toggle chip.
- Semantic: `--dennoko-semantic-info/warning/success/error` (and `.dennoko-text-*` helpers).
- Custom canvas drawing (`TimelineUITKView` Painter2D) reads theme colors via `CustomStyleProperty<Color>` on `CustomStyleResolvedEvent`.
- UXML/USS loaded by GUID via `UI/DenEmoUiAssets.cs`.
