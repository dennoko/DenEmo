# DenEmo Usage Guide

DenEmo is a Unity Editor extension for adjusting shape keys (blendshapes) and exporting facial expression animation files (`.anim`). It has two modes: **Single Frame** for single-frame expressions, and **Multi Frame** for time-based clips.

Open DenEmo from the Unity menu bar: `dennokoworks > DenEmo`.

---

## Window Layout

From top to bottom the window shows: the header -> the mode tabs (Single Frame / Multi Frame / Apply to Avatar) -> the settings for the current mode -> the shape key list -> the save area (footer) -> the status bar. The shape key list fills the remaining height, and the settings above it scroll only when the window is short. The save button always sits at the very bottom.

## Header

- **Version and ↻**: The current version is shown next to the tool name. When a newer version exists it reads "Update available". Click ↻ to check again.
- **EN / JA**: Switches the UI language (the button shows the language you will switch to). Your choice is saved automatically.
- **?**: Opens the PDF manual (this guide) in the current language.

---

## Target Mesh (common to both modes)

The **TARGET MESH** section is always shown right below the mode tabs. Select the mesh (SkinnedMeshRenderer) you want to edit here before doing anything else. While no mesh is set, the shape key list shows a **From selection** button that sets the mesh from the object selected in the Hierarchy.

### Primary Mesh

Drag a GameObject from the Hierarchy or use the object picker to set the primary mesh. You can drop it anywhere in the DenEmo window. Click ✕ to clear it.

If the mesh is inactive (disabled or hidden), a warning appears below the field with an **Enable** button that activates it on the spot.

### Additional Meshes

Some avatars split their face across multiple meshes (e.g., separate eyebrow or teeth meshes). Drag each extra SkinnedMeshRenderer into a `+` slot below the primary field. Each additional mesh can be removed with its own ✕ button. All registered meshes share one combined shape key list.

> Example: Your VRChat avatar has `Body` and `Brow` as separate face meshes. Set `Body` as the primary mesh and drag `Brow` into the `+` slot. Both appear in the list together.

### Reload

Press the **Reload** button on the right of the section title after adding blendshapes to a mesh in another tool, or whenever the list seems out of date. It re-reads all registered meshes and rebuilds the list.

---

## Single Frame Mode

Single Frame Mode is for building a single expression frame and exporting it as an `.anim` file. This is the typical workflow for creating gesture animations in VRChat.

### ANIMATION SOURCE

This section lets you pull shape key values or checkbox settings from an existing animation clip - useful for editing or extending animations you already have.

**Clip field**
Reference any `.anim` file here to use it as a source.

**Also load non-blendshape animation** (default: on)
Also imports non-blendshape animation data - object toggles, bone transforms, material values and so on - and writes it back out when you save. If your default expression carries such data, a blendshape-only expression leaves it uncancelled and the face can break. Leaving this on avoids that.

> Imported data is NOT reflected in the DenEmo preview (the scene view). It is included in the saved `.anim`.

**Load Animation**
Reads the shape key values at 0 s from the selected clip and applies them to all sliders. The sliders update immediately so you can continue editing from that state.

> Example: You want to modify an existing "smile" expression. Load the clip and the sliders jump to the saved values. Adjust from there and save a new file.

**Align Animation Keys**
Checks or unchecks the save-target checkboxes based on which shape keys appear in the selected clip. Shape keys that the clip uses get checked; all others get unchecked. Slider values are not changed.

> Example: Your avatar has 200 shape keys but the reference "smile" clip only uses 12. After aligning, only those 12 will be exported - keeping your new animation clean and free of unintended shape keys.

---

### SEARCH & FILTER

The search field and filter chips at the top of the shape key list narrow it down. Combine multiple filters freely. Chips that are on are shown with a blue border.

**Keyword**
Type in the search field ("Search shape keys") to filter by name. Multiple words separated by spaces are treated as AND conditions. Press ✕ to clear. The right end of the search field shows "visible / total".

> Example: Type `mouth open` to show only shape keys whose names contain both words.

**★ Fav**
Shows only shape keys you have starred. Click the ☆ or ★ button on any row to toggle the favorite flag.

> Example: Star the 8-10 shape keys you use on every expression so you can isolate them instantly without searching.

**✓ Enabled**
Shows only shape keys whose save checkbox is currently checked.

> Example: Before saving, switch to this view to confirm exactly which shape keys will be written to the file.

**≠0 Non-zero**
Shows only shape keys with a slider value other than zero.

> Example: After loading a reference animation, use this filter to see which shape keys the expression actually moves.

**↔ Symmetry**
Pairs shape keys ending in `...L` and `...R` into a single combined row. The shared slider drives both sides at once. When the L and R values differ, they fall back to separate rows automatically.

> Example: `cheek_puff.L` and `cheek_puff.R` appear as one row. Turn Symmetry off if you need to set one side to a different value.

**● Vertex Filter**
Press this button, then click any vertex in the SceneView. The list narrows to show only shape keys that actually move that vertex. An active filter shows the selected vertex index. Press ✕ to clear. The ⚙ button next to it changes the color and size of the vertices drawn in the SceneView.

> Example: A vertex on the upper lip is deforming in an unexpected way. Click it in the SceneView to find which shape keys affect that specific point.

**Mesh filter (popup)**
Appears only when two or more meshes are registered. Select **All meshes** to see every shape key together, or pick one mesh name to see only that mesh's shape keys.

When the filters leave nothing to show, the list shows a **Clear filters** button.

---

### SHAPE KEYS List

The main editing area. Each row represents one shape key.

**☆ / ★**
Toggles the favorite flag for that shape key.

**Checkbox**
Determines whether this shape key is included in the exported `.anim` file. Checked keys are written; unchecked keys are ignored on save even if their value is non-zero.

Checkbox states are remembered per mesh **by shape key name**. When you update the FBX and shape keys are added or reordered, existing keys keep their checkbox state without shifting. Newly added keys start in the default state (checked). A renamed key is treated as a different key, so its checkbox state is not carried over.

**Name**
The shape key name as defined in the mesh. Checked items appear in a brighter style to distinguish them at a glance.

**[0]**
Resets the slider to zero without changing the save checkbox.

**Slider (0-100)**
Drag to adjust the blendshape weight. Supports Unity Undo/Redo (Ctrl+Z).

### Groups

Shape keys are automatically grouped by name prefix (e.g., all `mouth_*` keys form one group). The group header shows a collapse arrow, a checked/visible count, and a checkbox that checks or unchecks the entire group at once.

> Example: Your avatar has 40 mouth-related shape keys. Collapse the `mouth` group when you are only working on the eyebrows.

### Snapshot (Snap / Restore)

These buttons sit on the right of the shape key list header. **Snap** saves the current slider values for all shape keys to memory. **Restore** instantly reverts all sliders to that saved state.

> Example: You have a complex expression dialed in. Press Snap, then experiment freely. If the experiment fails, Restore brings back the working state in one click.

### Bulk Check Operations

Clicking the `⋮` button at the right end of the shape key list header opens a menu to perform bulk operations on the shape key checkboxes (inclusion toggles).

* **Check All Visible**: Checks all shape keys currently displayed in the list (matching search and filters). Shape keys bound to the VRCAvatarDescriptor eyelids/LipSync are excluded.
* **Uncheck All Visible**: Unchecks all shape keys currently displayed in the list.
* **Uncheck Unchanged (Value is 0)**: Unchecks all shape keys that are unchanged in the animation, regardless of the current search or filter visibility.
  * **Single Frame Mode (Pose)**: Unchecks shape keys whose current slider value is `0`.
  * **Multi Frame Mode (Animation)**: Unchecks shape keys that are constant at `0` throughout the entire animation clip (i.e. have no keyframes, or all of their keyframes have a value of `0`).
* **Check Favorites Only**: Checks only the starred (favorite) shape keys among those currently visible, and unchecks the rest.
* **Check Only Keys Differing from Reference Animation**: Compares the current slider values against the clip loaded in ANIMATION SOURCE, checks only the shape keys that differ, and unchecks the rest (applies to visible keys). Shape keys not contained in the clip are compared against the default value `0`, so changes outside the reference clip are also picked up as differences. This item is grayed out when no clip is set.
  > Example: Load an existing expression animation as a base, tweak it, then use this to include only your adjustments in the export.
* **Check Only Keys Differing from Snapshot**: Compares the current slider values against the Snapshot, checks only the shape keys that differ, and unchecks the rest (applies to visible keys). This item is grayed out when no Snapshot has been taken.

---

### Saving (bottom of the window)

The finished expression is exported from the save area at the very bottom of the window. Every save setting sits right above or next to the **Save** button.

**Overwrite** (checkbox at the left of the footer)
When off, the expression is saved as a new `.anim` file. When on, the row above the Save button switches to the overwrite target, and saving writes directly into that existing file.

**Save to** (when Overwrite is off)
The folder the save dialog opens in for a new file. Type a path directly or click **Browse** on its right to pick a folder.

**Target** (when Overwrite is on)
The `.anim` file to overwrite. The Save button stays disabled until a target is set.

**Auto backup** (when Overwrite is on)
Copies the current file to a `_backups/` subfolder before overwriting it. Keep this on to avoid losing previous versions.

**Keep non-shape-key data** (when Overwrite is on; default: on)
Keeps non-blendshape data already present in the overwrite target - object toggles, bone transforms, material values and so on - instead of erasing it. When off, saving produces a blendshape-only animation.

> This data is NOT reflected in the DenEmo preview (the scene view). It is included in the saved `.anim`.

**Save**
Exports all checked shape keys with their current slider values. With Overwrite on it writes into the target file; with Overwrite off it creates a new file where you choose in the save dialog. The left of the footer shows how many shape keys will be saved ("N / M to save").

> Example: You are making three variations of a "surprise" expression. Save the first normally. For the second variation, turn on Overwrite and set the first file as the target (with Auto backup on) - the original is preserved in `_backups/` and the file is updated in place.

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
3. Move the playhead -> enable **REC** -> drag sliders to record keyframes.

> Example: Start a "wink" animation by pressing New, saving the file, then recording keyframes using the timeline.

---

### SHAPE KEY VALUE CORRECTION

This section is **collapsed by default**. Click the header to expand it. While collapsed, the header still shows "N adjusted" when any shape key has a correction set.

It lets you remap the value range of individual shape keys across the entire clip. This is useful when an expression edit causes a shape key to look wrong at its full range - a common issue in VRChat when blink animations conflict with face texture modifications that partially close the eye.

The section lists every shape key that has keyframes in the current clip. For each one you can set a **Min** and **Max** bound:

| Field | Default | Effect |
|-------|---------|--------|
| **Min (0-100)** | 0 | Lower bound after correction. When above 0, the original value 0 is raised to this value, while the original 100 maps to the Max setting. Prevents the shape key from fully returning to neutral. |
| **Max (0-100)** | 100 | Upper bound after correction. When below 100, the original value 100 is lowered to this value, while the original 0 maps to the Min setting. Prevents the shape key from reaching its full intensity. |

The correction formula applied to every keyframe:

```
new_value = original_value * (Max - Min) / 100 + Min
```

Curve tangents are scaled by the same factor, preserving the shape of the animation curve.

Press **Apply Correction to Timeline** to apply the rescaled values to the animation clip in memory (the timeline). This action itself does not save the asset file. To save the changes, press **Save Animation** at the very bottom of the window.

The operation supports Undo (Ctrl+Z). Only shape keys whose Min/Max differ from the defaults (0 and 100) are affected.

> Example: A blink animation drives the `blink` shape key to 100, but your face texture modification already partially closes the eye, so reaching 100 looks broken. Set Max to 80 and press Apply Correction to Timeline. All blink keyframes are rescaled into the 0-80 range - the curve shape and timing remain unchanged. Then press **Save Animation** at the bottom of the window to save the clip.

> Example: You want `mouth_open` to never fully close in this animation. Set Min to 20. Original 0 keyframes become 20, original 100 keyframes stay at 100.

---

### TIMELINE

The timeline shows the playback position, transport controls, and a visual track area for every animated shape key. It can be detached from the main window.

**Detach / Attach**
**Detach** (on the right of the timeline header) opens the timeline in a separate floating window for more screen space. Closing the separate window (or pressing **Attach** inside it) returns the timeline to the main DenEmo window. While detached, the main window shows **Focus Timeline Window** / **Close Timeline Window** buttons.

#### Global Settings

**FPS**
Frame rate of the clip. Changing it rescales the frame numbers shown in the ruler.

**Len (s)**
Total duration of the clip in seconds.

**Interp / Apply to All**
Choose Step / Linear / Ease under **Interp** to set the interpolation for newly created keyframes. **Apply to All** changes every existing keyframe to the current choice at once.

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
| `\|◆` | Jump to the previous keyframe |
| `<` | Step one frame backward |
| `▶` / Stop | Start or stop playback (loops continuously) |
| `>` | Step one frame forward |
| `◆\|` | Jump to the next keyframe |
| `>\|` | Jump to the last frame |

#### State and Options

**Frame**
The current playhead position as a frame number. Edit directly to jump to a specific frame.

**Speed**
Playback speed multiplier (0.1x-4x). Affects preview only; it does not change the clip.

**Loop Support**
Copies the first frame's keyframe values to the end of the clip so the animation loops without a visible jump. Toggle off to remove those extra keys.

> Example: A looping idle expression needs to connect seamlessly at the end. Enable Loop Support so the last frame transitions smoothly back to the first.

**REC**
Toggles recording mode. When active, dragging any shape key slider automatically stamps a keyframe at the current playhead position. When off, sliders still move the mesh in the preview but do not create keyframes.

> Example: Move the playhead to frame 10, enable REC, drag the `mouth_smile` slider to 80. A keyframe is recorded. Move to frame 25, drag it back to 0. The animation now smoothly transitions from smile to neutral between frames 10 and 25.

#### Timeline Area

**Ruler**
Shows frame numbers above the track area. Tick density adjusts automatically based on clip length and window width. **Reset Zoom** returns the view to the whole clip (100%).

**Scrubber**
The thin bar just below the ruler. Click or drag anywhere on it to move the playhead. The mesh preview updates in real time.

**Keyframe Tracks**
One track row appears for each shape key that has at least one keyframe. Each row contains:

- **Shape key name** - the name of the animated shape key.
- **◆ button** - adds or updates a keyframe at the current playhead time, using the shape's current slider value.
- **✕ button** - deletes the entire track (all keyframes) for that shape key after confirmation.
- **◆ diamonds on the track** — each diamond is one keyframe. Drag a diamond left or right to move it to a different frame. Hover over a diamond to see a ` F:N  V:X.X ` tooltip. Right-click a diamond for a context menu:

  | Menu item | Action |
  |-----------|--------|
  | Insert Keys at Current Frame (N visible) | Inserts keys at the current playhead time for all N shape keys currently listed, using their current values. Works whether or not REC is on; when a search or filter is active only the visible shape keys are affected (N is that count) |
  | Delete All Keys at Current Frame | Deletes the keys of every track at the current playhead time after confirmation. Greyed out when there is no key at that time |
  | Delete | Removes that keyframe |
  | Copy frame | Copies the value and interpolation of all tracks at that frame to the DenEmo internal clipboard |
  | Paste at current time | Pastes clipboard contents at the current playhead position (greyed out when empty) |
  | Step / Linear / Ease | Changes the interpolation of that single keyframe |

- **Right-click on empty timeline area** - right-clicking the ruler, the scrubber, the frame move handle row, or any part of a track that is not a diamond shows a menu with "Insert Keys at Current Frame", "Delete All Keys at Current Frame" and "Paste at current time". The ruler and scrubber stay right-clickable even when there are no tracks yet, so you can always reach the bulk insert

The label column width can be resized by dragging the vertical divider between the label and the track area.

**Frame move handle row (bottom)**
Below all tracks, a `↔` drag handle appears at each frame that has keys. Drag a handle left or right to move all keyframes at that frame - across every track - to another frame. If another key blocks the way, the move stops just before it and the blocking key flashes briefly.

> Example: to record the current pose of every shape at a specific frame with REC off, move the scrubber to that frame, right-click the timeline and choose "Insert Keys at Current Frame". A single `Ctrl+Z` undoes the whole insert.

---
#### Keyboard Shortcuts

Active anywhere in the DenEmo window while the timeline is visible with a clip loaded and no text field (frame number, search box, ...) has keyboard focus. When the timeline is detached, the shortcuts work in that separate window instead.

While a shape key slider has focus, `←` / `→` are left to the slider for value adjustment; every other shortcut still applies.

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

**Copy & Paste** - there are two equivalent ways to copy and paste keyframe values:

| | Copy | Paste |
|--|------|-------|
| **Keyboard** | `Ctrl+C` - copies all keys at the current frame | `Ctrl+V` - pastes at the current frame |
| **Right-click menu** | Right-click a diamond -> **Copy frame** (copies all tracks at that frame) | Right-click a diamond or empty timeline area -> **Paste at current time** |

Both methods share the same internal clipboard. You can copy via right-click and paste with `Ctrl+V`, or vice versa.

The clipboard is DenEmo-internal and is not written to the OS clipboard. Its contents are lost on Unity restart or domain reload.

> Example: To duplicate the pose at frame 5 onto frame 30 - move to frame 5, press `Ctrl+C`, move to frame 30, press `Ctrl+V`. Press `Ctrl+Z` once to undo the paste.

> Example: Right-click the diamond at frame 10, choose **Copy frame**, scrub to frame 25, then right-click the empty track area and choose **Paste at current time**.

---


### SEARCH & FILTER (Multi Frame Mode)

All filters from Single Frame Mode are available. One additional chip appears when a clip is loaded:

**◆ Keyed Only**
Hides shape keys that have no keyframes in the current clip. Use this to focus the list on only the shapes that are actually animated.

---

### SHAPE KEYS List (Multi Frame Mode)

Identical to the Single Frame Mode list with one addition per row:

**◆ / ◇ (Keyframe button)**
Appears at the far right of each row. ◆ (filled) means a keyframe exists at the current playhead time. ◇ (outline) means no keyframe exists at this time. Clicking the icon toggles it: ◆ stamps a keyframe at the current time; ◇ removes it.

> Example: You want the mouth to be fully closed at frame 0 without using REC. Move to frame 0, set the mouth slider to 0, click ◆ to stamp the keyframe. Done.

---

### RECORDING BANNER

When **REC** is active, a banner with a red ● appears right below the mode tabs:

> ● Recording   Moving a slider records a key at the current frame   [Stop]

This banner makes the recording state unmissable so you always know whether slider movements will stamp keyframes. Press **Stop** on the banner to end recording.

---

### SAVE ANIMATION

Saving happens in the save area at the very bottom of the window.

**Overwrite** (checkbox at the left of the footer; default: on)
When on, the clip being edited is saved over its own file. When off, a save dialog opens and the clip is saved as a new clip (starting in the original clip's folder). It means the same as Overwrite in Single Frame Mode, but each mode remembers its own on/off state.

**Keyframe statistics**
The left of the footer shows how many tracks and total keyframes the current clip contains (e.g., *3 tracks / 12 keyframes total*). If no keyframes have been recorded yet, a warning is shown instead, reminding you to add keyframes before saving.

**Save Animation**
Writes the current clip's keyframes to the `.anim` file.

> Example: After recording a full wink sequence, press Save Animation to finalize the file and make it available in the Project window.

---

## Apply to Avatar (FX Layer Expression Replacement)

The **APPLY TO AVATAR** mode allows you to bulk-replace expression animations (`.anim`) used within a VRChat avatar's FX layer (Animator Controller) with custom expressions you have created.

### Avatar / FX Layer Detection

**Automatically detects the avatar and its FX layer (Animator Controller).**

- **Auto-Detection**:
  If the target mesh is part of an avatar that has a `VRC Avatar Descriptor`, the tool automatically detects the avatar root and the custom Animator Controller assigned to the FX layer.
- **Manual Assignment**:
  If no avatar is detected or the VRChat SDK is not installed, a "VRChat avatar not found" warning is displayed. In this case, drag and drop the Animator Controller asset you want to edit directly into the **"FX Layer"** field.
- **Rescan (↻)**:
  If you modify the avatar hierarchy or FX layer setup, click the **↻** button in the AVATAR / FX LAYER header to update the detected information.

---

### Expression Mapping List

Lists the animation clips within the FX controller that animate the shape keys of your target mesh.

- **Search & Filters**:
  - **Keyword**: Type in the search field ("Search animations") to filter by the original animation name (supports space-separated multi-word AND search). The right end shows "visible / total".
  - **Assigned N**: A chip in the list header. Turn it on to show only the entries that have a replacement animation assigned.
  - **Left Hand / Right Hand and gestures**: Chips below the search field narrow the list to expressions played by that hand gesture.
- **Show all**:
  If no animations matching the target mesh are found, the list shows a **Show all** button. It bypasses the path restriction and displays every blendshape animation in the controller.
- **Mesh path mismatch (Warning)**:
  If the hierarchy path of the mesh in the animation binding does not match the current path of the target mesh on the avatar, a "⚠ Mesh path mismatch" warning is displayed.

#### Preview on Hover
Hovering your mouse over any animation name in the list plays a real-time preview of that expression on the avatar in the Scene view. If a replacement is assigned, the new animation is previewed instead. The active preview animation is shown in the row right below the list, and the ✕ button there stops it.

#### Assigning Replacements
Click the **"None ▾"** button (or the assigned clip name) on the right side of a row to open a popup picker showing shape key animations in the project. Alternatively, drag and drop any `.anim` file from the Project window directly onto the slot button.
- When assigned, a green indicator bar appears on the left edge of the row.
- Click the `✕` button to clear the assignment.
- If the same animation is referenced in multiple states/places, click the `▶ / ▼` toggle on the left of the row to expand the list and enable/disable replacements for specific slots individually.

---

### Apply Settings & Execution

Applies the mapping configuration to the FX controller. Choose how under **Apply mode** below the list.

- **Duplicate & apply (safe)**:
  Creates a duplicate of the FX controller, modifies only the duplicate, and assigns it to the avatar's FX layer. This keeps your original controller asset safe and untouched.
- **Modify directly**:
  Modifies the existing FX controller asset directly. **No backup is created.** Use "Duplicate & apply" if you want to be able to go back.
- **Manual Assignment Note**:
  If no avatar is detected, you will need to manually set the duplicated controller into the avatar's FX layer.

Click **Replace** at the very bottom of the window (its left shows "Replace N (M refs)") to open a confirmation dialog showing a summary of the changes. Click "Apply" to run it. The results (new controller path, Descriptor update status) stay at the bottom of the AVATAR / FX LAYER section, and the **→** button next to the controller path locates the new controller in your Project window.

---

## FAQ

**Q. Why use "Align Animation Keys" instead of unchecking shape keys manually?**

A. Avatars often contain shape keys for gimmicks, physics toggles, or other non-expression systems. If these end up in your animation, they can interfere with other avatar features. Aligning to a reference clip excludes all of them in one step.

**Q. Does DenEmo support Undo?**

A. Yes. Slider adjustments, checkbox toggles, and loading animation values all participate in Unity's Undo/Redo system (Ctrl+Z / Ctrl+Y).

**Q. How do I adjust only one side when ↔ Symmetry is on?**

A. Turn off the ↔ Symmetry chip, adjust the individual L or R row, then turn Symmetry back on. The L and R values will remain independent until they happen to become equal again.

**Q. Can I work in Multi Frame Mode without looking at the timeline?**

A. Yes. Detach the timeline with **Detach**, minimize or move that window out of the way, and use only the ◆ / ◇ buttons in the shape key list combined with REC mode. The timeline still records keyframes correctly in the background.
