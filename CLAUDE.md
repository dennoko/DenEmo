# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DenEmo is a Unity Editor extension (IMGUI) for editing and animating blendshapes on `SkinnedMeshRenderer` components, primarily for VRChat avatar facial animations. It supports two modes: **Single Frame (Pose)** for static expressions, and **Multi Frame (Animation)** for recording keyframe-based animation clips.

## Development Environment

No build or test commands exist — this is a Unity Editor extension loaded automatically from `Assets/Editor/`. Changes to `.cs` files are hot-reloaded by Unity on save. Open the DenEmo window from the Unity menu `dennokoworks/DenEmo`.

## Architecture

### Mode split

`DenEmoWindow` hosts two top-level modes (`EditorMode.Pose` / `EditorMode.Animation`) switched by `SwitchMode()`. Mode state is persisted per-project via `DenEmoProjectPrefs`.

### Object ownership

```
DenEmoWindow                  ← EditorWindow; lifecycle, settings persistence, OnGUI routing
  AnimationModeUI             ← Animation mode orchestrator; owns ClipModel + Preview + TimelineUI
    AnimationClipModel        ← Clip state (FPS, length, CurrentTime, keyframe queries)
    AnimationPreviewController← Evaluates blendShape curves → SMR weights; records/deletes keyframes
    AnimationTimelineUI       ← Draws timeline strip (partial class: .cs / .Controls.cs / .Scrubber.cs / .Tracks.cs)
  ShapeKeyListUI              ← Blendshape list with sliders (partial class: .cs / .Rows.cs / .Segments.cs)
ShapeKeyModel                 ← All blendshapes on active SMR(s); search/filter/group logic
```

### Key patterns

**AnimationDrawContext** — Created by `AnimationModeUI.BuildDrawContext()` and passed into `ShapeKeyListUI.DrawList()`. Injects animation-mode callbacks (keyframe toggle, value recording while sliding) without a direct dependency from `ShapeKeyListUI` to animation types.

**Partial classes** — `AnimationTimelineUI`, `DenEmoWindow`, and `ShapeKeyListUI` are each split across multiple `.cs` files by functional area. When adding a new method, choose the file whose concern it matches (`Controls`, `Scrubber`, `Tracks`, `Sections`, `Preferences`, `Rows`, `Segments`).

**Preview isolation** — `AnimationPreviewController` does NOT use Unity's `AnimationMode.StartAnimationMode()` (which resets bone transforms). It evaluates only `blendShape.*` curves manually via `AnimationCurve.Evaluate()` and writes weights directly to the SMR. The curve cache (`_curveCache`) must be invalidated by calling `SetCacheDirty()` after any clip mutation, followed by `SampleAt()` to refresh the viewport.

**GUIStyle lifecycle** — All custom `GUIStyle` objects are created lazily inside `Ensure*()` methods (null-check after domain reload). `DenEmoTheme.Initialize()` must be called at the top of every `OnGUI`.

**Localization** — All user-visible strings go through `DenEmoLoc.T(key)` / `DenEmoLoc.Tf(key, args)`. Add keys to both `JA` and `EN` dictionaries in `Utils/DenEmoLocalization.cs`.

**Undo** — Keyframe mutations call `Undo.RecordObject(clip, label)` before modifying the `AnimationCurve`. After any Undo/Redo event, `Preview.SetCacheDirty()` and `Preview.SampleAt()` must be called to keep the viewport consistent.

## Design system

Color tokens and styles live in `UI/DenEmoTheme.cs`. Full spec: `dennokoworks_color_schema/forUnity/`. Key conventions:

- `Surface0/1/2` = background elevation layers (darkest → lightest)
- `DenEmoTheme.CardStyle` / `CardOuterStyle` = 1px-bordered card container
- `DenEmoTheme.MiniButtonStyle` = small inline button (◆, ✕, ★, etc.)
- Bordered textures: `DenEmoTheme.MakeBorderedTex(fillColor, borderColor)` (3×3, 9-slice)
- Semantic colors: `SemanticInfo` (blue), `SemanticWarning` (yellow), `SemanticSuccess` (green), `SemanticError` (red)
