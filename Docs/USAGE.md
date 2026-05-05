# DenEmo Usage Guide

DenEmo is a Unity Editor extension for adjusting shape keys (blendshapes) and exporting facial expression animation files (`.anim`). It has two modes: **Single Frame** for single-frame expressions, and **Multi Frame** for time-based clips.

Open DenEmo from the Unity menu bar: `dennokoworks > DenEmo`.

---

## Header

The **EN / JA** button in the top-right corner switches the UI language. Your choice is saved automatically.

---

## Target Mesh (common to both modes)

The **TARGET MESH** section is always shown at the top. Select the mesh (SkinnedMeshRenderer) you want to edit here before doing anything else.

### Primary Mesh

Drag a GameObject from the Hierarchy or use the object picker to set the primary mesh. Click ✁Eto clear it.

### Additional Meshes

Some avatars split their face across multiple meshes (e.g., separate eyebrow or teeth meshes). Drag each extra SkinnedMeshRenderer into a `+` slot below the primary field. Each additional mesh can be removed with its own ✁Ebutton. All registered meshes share one combined shape key list.

> Example: Your VRChat avatar has `Body` and `Brow` as separate face meshes. Set `Body` as the primary mesh and drag `Brow` into the `+` slot. Both appear in the list together.

### Refresh

Press **Refresh** after adding blendshapes to a mesh in another tool, or whenever the list seems out of date. It re-reads all registered meshes and rebuilds the list.

---

## Single Frame Mode

Single Frame Mode is for building a single expression frame and exporting it as an `.anim` file. This is the typical workflow for creating gesture animations in VRChat.

### ANIMATION SOURCE

This section lets you pull shape key values or checkbox settings from an existing animation clip  Euseful for editing or extending animations you already have.

**Clip field**
Reference any `.anim` file here to use it as a source.

**Load Animation**
Reads the shape key values at 0 s from the selected clip and applies them to all sliders. The sliders update immediately so you can continue editing from that state.

> Example: You want to modify an existing "smile" expression. Load the clip and the sliders jump to the saved values. Adjust from there and save a new file.

**Align Animation Keys**
Checks or unchecks the save-target checkboxes based on which shape keys appear in the selected clip. Shape keys that the clip uses get checked; all others get unchecked. Slider values are not changed.

> Example: Your avatar has 200 shape keys but the reference "smile" clip only uses 12. After aligning, only those 12 will be exported  Ekeeping your new animation clean and free of unintended shape keys.

---

### SEARCH & FILTER

These chips and the search field narrow down the shape key list. Combine multiple filters freely.

**🔍 Keyword**
Type to filter by name. Multiple words separated by spaces are treated as AND conditions. Press ✁Eto clear.

> Example: Type `mouth open` to show only shape keys whose names contain both words.

**☁EFav**
Shows only shape keys you have starred. Click the ☁Eor ☁Ebutton on any row to toggle the favorite flag.

> Example: Star the 8 E0 shape keys you use on every expression so you can isolate them instantly without searching.

**✁EEnabled**
Shows only shape keys whose save checkbox is currently checked.

> Example: Before saving, switch to this view to confirm exactly which shape keys will be written to the file.

**≠0 Non-zero**
Shows only shape keys with a slider value other than zero.

> Example: After loading a reference animation, use this filter to see which shape keys the expression actually moves.

**ↁESymmetry**
Pairs shape keys ending in `...L` and `...R` into a single combined row. The shared slider drives both sides at once. When the L and R values differ, they fall back to separate rows automatically.

> Example: `cheek_puff.L` and `cheek_puff.R` appear as one row. Turn Symmetry off if you need to set one side to a different value.

**◁EVertex Filter**
Press this button, then click any vertex in the SceneView. The list narrows to show only shape keys that actually move that vertex. An active filter shows the selected vertex index. Press ✁Eto clear.

> Example: A vertex on the upper lip is deforming in an unexpected way. Click it in the SceneView to find which shape keys affect that specific point.

**Mesh filter (popup)**
Appears only when two or more meshes are registered. Select **All** to see every shape key together, or pick one mesh name to see only that mesh's shape keys.

---

### SHAPE KEYS List

The main editing area. Each row represents one shape key.

**☁E/ ☁E*
Toggles the favorite flag for that shape key.

**Checkbox**
Determines whether this shape key is included in the exported `.anim` file. Checked keys are written; unchecked keys are ignored on save even if their value is non-zero.

**Name**
The shape key name as defined in the mesh. Checked items appear in a brighter style to distinguish them at a glance.

**[0]**
Resets the slider to zero without changing the save checkbox.

**Slider (0 E00)**
Drag to adjust the blendshape weight. Supports Unity Undo/Redo (Ctrl+Z).

### Groups

Shape keys are automatically grouped by name prefix (e.g., all `mouth_*` keys form one group). The group header shows a collapse arrow (▶ / ▼), a checked/visible count, and a checkbox that checks or unchecks the entire group at once.

> Example: Your avatar has 40 mouth-related shape keys. Collapse the `mouth` group when you are only working on the eyebrows.

### Snapshot and Restore Snapshot

**Snapshot** saves the current slider values for all shape keys to memory. **Restore Snapshot** instantly reverts all sliders to that saved state.

> Example: You have a complex expression dialed in. Take a Snapshot, then experiment freely. If the experiment fails, Restore Snapshot brings back the working state in one click.

---

### SAVE SETTINGS

Controls how the finished expression is exported.

**Save To**
The folder where new `.anim` files are created. Type a path directly or click **Browse** to pick a folder.

**Enable Overwrite Save**
When enabled, a target clip field appears below. Saving writes directly to that existing file rather than creating a new one.

**Auto Backup on Overwrite**
When overwrite is enabled, this option copies the current file to a `_backups/` subfolder before overwriting it. Keep this on to avoid losing previous versions.

**Save Animation**
Exports all checked shape keys with their current slider values. If Overwrite Save is enabled and a target is set, it overwrites that file. Otherwise it creates a new file in the Save To folder.

> Example: You are making three variations of a "surprise" expression. Save the first normally. For the second variation, enable Overwrite Save pointing at the first file (with Auto Backup on)  Ethe original is preserved in `_backups/` and the file is updated in place.

---

## Multi Frame Mode

Multi Frame Mode lets you create shape key animations that change over time. You record keyframes at different points on the timeline and the tool generates the curves automatically.

### ANIMATION CLIP

**Clip field**
Select an existing `.anim` file to edit it. The timeline and list update to reflect that clip's keyframes.

**New**
Opens a save dialog and creates a blank `.anim` file. The new clip is immediately loaded and ready to record into.

When no clip is loaded, the section shows a three-step workflow guide:

1. Drag an existing `.anim` file into the Clip field, or press **New** to create a blank clip.
2. Check / set **FPS** and **duration** in the Timeline section.
3. Move the playhead → enable **REC** → drag sliders to record keyframes.

> Example: Start a "wink" animation by pressing New, saving the file, then recording keyframes using the timeline.

---

### SHAPE KEY VALUE CORRECTION

This section is **collapsed by default**. Click the header to expand it.

It lets you remap the value range of individual shape keys across the entire clip. This is useful when an expression edit causes a shape key to look wrong at its full range  Ea common issue in VRChat when blink animations conflict with face texture modifications that partially close the eye.

The section lists every shape key that has keyframes in the current clip. For each one you can set a **Min** and **Max** bound:

| Field | Default | Effect |
|-------|---------|--------|
| **Min (0 E00)** | 0 | Lower bound after correction. When above 0, the original value 0 is raised to this value, while the original 100 maps to the Max setting. Prevents the shape key from fully returning to neutral. |
| **Max (0 E00)** | 100 | Upper bound after correction. When below 100, the original value 100 is lowered to this value, while the original 0 maps to the Min setting. Prevents the shape key from reaching its full intensity. |

The correction formula applied to every keyframe:

```
new_value = original_value ÁE(Max ∁EMin) / 100 + Min
```

Curve tangents are scaled by the same factor, preserving the shape of the animation curve.

Press **Apply Correction** to write the remapped values to the clip. The operation supports Undo (Ctrl+Z). Only shape keys whose Min/Max differ from the defaults (0 and 100) are affected.

> Example: A blink animation drives the `blink` shape key to 100, but your face texture modification already partially closes the eye, so reaching 100 looks broken. Set Max to 80 and press Apply Correction. All blink keyframes are rescaled into the 0 E0 range  Ethe curve shape and timing remain unchanged.

> Example: You want `mouth_open` to never fully close in this animation. Set Min to 20. Original 0 keyframes become 20, original 100 keyframes stay at 100.

---

### TIMELINE

The timeline shows the playback position, transport controls, and a visual track area for every animated shape key. It can be detached from the main window.

**ↁEDetach / ↁEAttach**
Detach opens the timeline in a separate floating window for more screen space. Closing the separate window (or pressing ↁEAttach inside it) returns the timeline to the main DenEmo window.

#### Global Settings

**FPS**
Frame rate of the clip. Changing it rescales the frame numbers shown in the ruler.

**Len (s)**
Total duration of the clip in seconds.

**All Keys Interp**
Changes the interpolation method for all existing keyframes at once.

| Option | Behavior |
|--------|----------|
| Step | Values snap instantly at the keyframe with no transition |
| Linear | Values change at a constant rate between keyframes |
| Ease | Values ease in and out for a natural feel |

> Example: Use Step for a blink that should happen instantly. Use Ease for a smile that flows in smoothly.

#### Transport Controls

| Button | Action |
|--------|--------|
| `\|<` | Jump to the first frame |
| `\|◁E | Jump to the previous keyframe |
| `<` | Step one frame backward |
| `▶` / `■` | Start or stop playback (loops continuously) |
| `>` | Step one frame forward |
| `◁E|` | Jump to the next keyframe |
| `>\|` | Jump to the last frame |

#### State and Options

**Frame**
The current playhead position as a frame number. Edit directly to jump to a specific frame.

**Speed**
Playback speed multiplier (0.1ÁE E4ÁE. Affects preview only; it does not change the clip.

**Loop Support**
Copies the first frame's keyframe values to the end of the clip so the animation loops without a visible jump. Toggle off to remove those extra keys.

> Example: A looping idle expression needs to connect seamlessly at the end. Enable Loop Support so the last frame transitions smoothly back to the first.

**🔴 REC**
Toggles recording mode. When active (red background), dragging any shape key slider automatically stamps a keyframe at the current playhead position. When off, sliders still move the mesh in the preview but do not create keyframes.

> Example: Move the playhead to frame 10, enable REC, drag the `mouth_smile` slider to 80. A keyframe is recorded. Move to frame 25, drag it back to 0. The animation now smoothly transitions from smile to neutral between frames 10 and 25.

#### Timeline Area

**Ruler**
Shows frame numbers above the track area. Tick density adjusts automatically based on clip length and window width.

**Scrubber**
The thin bar just below the ruler. Click or drag anywhere on it to move the playhead. The mesh preview updates in real time.

**Keyframe Tracks**
One track row appears for each shape key that has at least one keyframe. Each row contains:

- **Shape key name**  Ethe name of the animated shape key.
- **◁Ebutton**  Eadds or updates a keyframe at the current playhead time, using the shape's current slider value.
- **✁Ebutton**  Edeletes the entire track (all keyframes) for that shape key after confirmation.
- **◆ diamonds on the track** — each diamond is one keyframe. Drag a diamond left or right to move it to a different frame. Hover over a diamond to see a ` F:N  V:X.X ` tooltip. Right-click a diamond for a context menu:

  | Menu item | Action |
  |-----------|--------|
  | Delete | Removes that keyframe |
  | Copy frame | Copies the value and interpolation of **all tracks** at that frame to the DenEmo internal clipboard |
  | Paste at current time | Pastes clipboard contents at the **current playhead position** (greyed out when empty) |
  | Step / Linear / Ease | Changes the interpolation of that single keyframe |

- **Right-click on empty track area** — right-clicking anywhere on the track that is not a diamond shows a "Paste at current time" menu, so you can paste without targeting a specific keyframe handle

The label column width can be resized by dragging the vertical divider between the label and the track area.

**Frame delete row (bottom)**
Below all tracks, each frame that has any keyframe shows a ✁Ebutton. Pressing it deletes all keyframes across all tracks at that frame  Eremoving a complete pose from one point in time.

---
#### Keyboard Shortcuts

Active when a clip is loaded and no text field is focused.

| Key | Action |
|-----|--------|
| `Space` | Toggle playback |
| `←` | Step one frame backward |
| `→` | Step one frame forward |
| `,` | Jump to previous keyframe |
| `.` | Jump to next keyframe |
| `Delete` / `Backspace` | Delete all keyframes at the current frame (no confirmation, Undo supported) |
| `Ctrl+C` | Copy all keyframes at the current frame (values + interpolation) to clipboard |
| `Ctrl+V` | Paste clipboard keyframes at the current frame (overwrites existing keys, Undo supported) |

**Copy & Paste** — there are two equivalent ways to copy and paste keyframe values:

| | Copy | Paste |
|--|------|-------|
| **Keyboard** | `Ctrl+C` — copies all keys at the current frame | `Ctrl+V` — pastes at the current frame |
| **Right-click menu** | Right-click a ◆ diamond → **Copy frame** (copies all tracks at that frame) | Right-click a ◆ diamond or empty track area → **Paste at current time** |

Both methods share the same internal clipboard. You can copy via right-click and paste with `Ctrl+V`, or vice versa.

The clipboard is DenEmo-internal and is not written to the OS clipboard. Its contents are lost on Unity restart or domain reload.

> Example: To duplicate the pose at frame 5 onto frame 30 — move to frame 5, press `Ctrl+C`, move to frame 30, press `Ctrl+V`. Press `Ctrl+Z` once to undo the paste.

> Example: Right-click the ◆ diamond at frame 10, choose **Copy frame**, scrub to frame 25, then right-click the empty track area and choose **Paste at current time**.

---


### SEARCH & FILTER (Multi Frame Mode)

All filters from Single Frame Mode are available. One additional chip appears when a clip is loaded:

**◁EKeyed Only**
Hides shape keys that have no keyframes in the current clip. Use this to focus the list on only the shapes that are actually animated.

---

### SHAPE KEYS List (Multi Frame Mode)

Identical to the Single Frame Mode list with one addition per row:

**◁E/ ◁E(Keyframe button)**
Appears at the far right of each row. ◁E(filled) means a keyframe exists at the current playhead time. ◁E(outline) means no keyframe exists at this time. Clicking the icon toggles it: ◁Estamps a keyframe at the current time; ◁Eremoves it.

> Example: You want the mouth to be fully closed at frame 0 without using REC. Move to frame 0, set the mouth slider to 0, click ◁Eto stamp the keyframe. Done.

---

### RECORDING BANNER

When **🔴 REC** is active, a red banner appears below the timeline:

> ● RECORDING — Dragging a slider records a keyframe at the current time

This banner makes the recording state unmissable so you always know whether slider movements will stamp keyframes.

---

### SAVE ANIMATION

**Keyframe statistics**
The top of the Save Animation section shows how many tracks and total keyframes the current clip contains (e.g., *3 tracks / 12 keyframes total*). If no keyframes have been recorded yet, a warning message is displayed instead, reminding you to add keyframes before saving.

**Save Animation**
Writes the current clip's keyframes to the `.anim` file. If the clip was loaded from an existing file, it overwrites that file. If the clip was just created with **New** and has not been saved to disk yet, a file save dialog appears.

> Example: After recording a full wink sequence, press Save Animation to finalize the file and make it available in the Project window.

---

## FAQ

**Q. Why use "Align Animation Keys" instead of unchecking shape keys manually?**

A. Avatars often contain shape keys for gimmicks, physics toggles, or other non-expression systems. If these end up in your animation, they can interfere with other animations or avatar features. Aligning to a reference clip excludes all of them in one step, with no manual counting needed.

**Q. Does DenEmo support Undo?**

A. Yes. Slider adjustments, checkbox toggles, and loading animation values all participate in Unity's Undo/Redo system (Ctrl+Z / Ctrl+Y).

**Q. How do I adjust only one side when ↁESymmetry is on?**

A. Turn off the ↁESymmetry chip, adjust the individual L or R row, then turn Symmetry back on. The L and R values will remain independent until they happen to become equal again.

**Q. Can I work in Multi Frame Mode without looking at the timeline?**

A. Yes. Detach the timeline with ↁEDetach, minimize or move that window out of the way, and use only the ◁E◁Ebuttons in the shape key list combined with REC mode. The timeline still records keyframes correctly in the background.
