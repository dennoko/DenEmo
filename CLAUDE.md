# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DenEmo is a Unity Editor extension (IMGUI) for editing and animating blendshapes on `SkinnedMeshRenderer` components, primarily for VRChat avatar facial animations. It supports two modes: **Single Frame (Pose)** for static expressions, and **Multi Frame (Animation)** for recording keyframe-based animation clips.

## Development Environment

No build or test commands exist — this is a Unity Editor extension loaded automatically from `Assets/dennokoworks/DenEmo/Editor/`. Changes to `.cs` files are hot-reloaded by Unity on save. Open the DenEmo window from the Unity menu `dennokoworks/DenEmo`.

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

**UI Toolkit only** — All UI is UI Toolkit (UXML/USS); there is no IMGUI drawing left except the SceneView vertex-picking overlay in `DenEmoWindow.VertexFilter.cs` (Handles/GUI, which USS can't reach — uses a local `GUIStyle` with the theme's body color hardcoded). `DenEmoTheme` has been deleted.

**Localization** — All user-visible strings go through `DenEmoLoc.T(key)` / `DenEmoLoc.Tf(key, args)`. Add keys to both `JA` and `EN` dictionaries in `Utils/DenEmoLocalization.cs`.

**Undo** — Keyframe mutations call `Undo.RecordObject(clip, label)` before modifying the `AnimationCurve`. After any Undo/Redo event, `Preview.SetCacheDirty()` and `Preview.SampleAt()` must be called to keep the viewport consistent.

## Design system

Colors and styles live in USS, loaded in this order by `DenEmoUiAssets.SetupRoot()`: `UI/DennokoTheme.uss` (skill theme, parts L0/L1) → `UI/DennokoBlocks.uss` (skill blocks/patterns, L2/L3) → `UI/DenEmoStyles.uss` (DenEmo-only classes). The theme and blocks files are verbatim copies of the skill's `assets/` — don't edit them; override in `DenEmoStyles.uss`. Shared C# helpers copied from the skill live in `Utils/` (`DennokoAppBar`, `DennokoStatus`, `DennokoIcons`, `DennokoBlockUI`, `DennokoUIFont`). Full spec: `.claude/skills/dennokoworks_color_schema/` (start at `blocks/README.md`). Key conventions:

- Window layout = skill pattern P3 (一覧編集): standard appbar → mode tabbar → per-mode `.dennoko-page` (`.dennoko-upper-scroll` that scrolls only when short + `.dennoko-list-slot` holding the growing B4 list card with its searchbar) → output dock → footer (one `.dennoko-button-primary`, switched per mode; FX uses `DenEmoWindow.FxFooter`) → `.dennoko-status` via `DennokoStatus`. Don't wrap the whole page in a ScrollView.
- Every root gets `dennoko-root` (theme entry point); C# fallback bg is `new Color32(0x12,0x12,0x12,0xFF)`.
- Colors go through USS variables (`var(--dennoko-*)`) only — never hardcode except in the theme's variable definitions.
- `--dennoko-surface-0/1/2/3` = background elevation layers (darkest → lightest); `--dennoko-outline-subtle` = static structure lines, `--dennoko-outline` = interactive element borders. `.dennoko-card` (+ block marker such as `dennoko-target-card` / `dennoko-list-card`) = bordered containers; `.dennoko-icon-button` (icon via `DennokoIcons.Apply`) = small inline action; `.dennoko-chip` (+ `dennoko-button-active`) = toggle chip; `.dennoko-segmented.dennoko-choice-group` = enum choice of ≤4 items.
- Semantic: `--dennoko-semantic-info/warning/success/error` (and `.dennoko-text-*` helpers).
- Custom canvas drawing (`TimelineUITKView` Painter2D) reads theme colors via `CustomStyleProperty<Color>` on a `CustomStyleResolvedEvent`, not hardcoded values.
- UXML/USS are loaded by GUID via `UI/DenEmoUiAssets.cs`; `.meta` files carry those GUIDs.
- **Never write `!important` in USS** — Unity does not support it and discards the whole declaration. Win with specificity: every custom `.dennoko-*` selector must be prefixed with `.dennoko-root`, otherwise the generic `.dennoko-root .unity-text-element` / `.unity-button` resets (0,2,0) beat it.
- Selected/active state in a mutually exclusive button group (mode tabs, filter chips, timeline toggles) = `EnableInClassList("dennoko-button-active", …)` only. The theme paints a `--dennoko-semantic-info` blue border; don't override the border color per-component.
- `Utils/DennokoUIFont.cs` is the shared font manager (OS Meiryo → SDF FontAsset, self-healing against atlas eviction). `DenEmoUiAssets.SetupRoot()` calls `DennokoUIFont.Apply(root)` — never build a `FontAsset` anywhere else. Add newly introduced Japanese UI characters to its `WarmupJapanese` constant.
- Layout goes in USS, not `element.style.*` in C#. Only per-instance dynamic values (`display`, computed widths/heights) belong inline.
