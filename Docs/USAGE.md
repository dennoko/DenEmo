# DenEmo Usage Guide

DenEmo is a Unity Editor extension for adjusting shape keys (blendshapes) and exporting facial expression animation files (`.anim`). Use **SINGLE FRAME** for single-frame expressions and **MULTI FRAME** for time-based clips. The **APPLY TO AVATAR** tab assigns expressions to the avatar's FX layer.

Open DenEmo from the Unity menu bar: `dennokoworks > DenEmo`.

---

## Header

The **EN / JA** button in the top-right corner switches the UI language. Your choice is saved automatically.

A guide below the tabs shows the next steps for your current state, including mesh selection, clip creation and recording.

---

## Target Mesh (common to both modes)

The **TARGET MESH** section is always shown at the top. Select the mesh (SkinnedMeshRenderer) you want to edit here before doing anything else.

### Primary Mesh

Drag a GameObject from the Hierarchy or use the object picker to set the primary mesh. Click ✕ to clear it.

### Additional Meshes

Some avatars split their face across multiple meshes (e.g., separate eyebrow or teeth meshes). Drag each extra SkinnedMeshRenderer into a `+` slot below the primary field. Each additional mesh can be removed with its own ✕ button. All registered meshes share one combined shape key list.

> Example: Your VRChat avatar has `Body` and `Brow` as separate face meshes. Set `Body` as the primary mesh and drag `Brow` into the `+` slot. Both appear in the list together.

### Refresh

Press **Refresh** after adding blendshapes to a mesh in another tool, or whenever the list seems out of date. It re-reads all registered meshes and rebuilds the list.

---

## Single Frame Mode

Single Frame Mode is for building a single expression frame and exporting it as an `.anim` file. This is the typical workflow for creating gesture animations in VRChat.

### Start from an existing clip (optional)

This section starts collapsed. Expand it when you want to reuse an expression. It opens automatically when the UI is rebuilt with a reference clip already selected.

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

**Clear filters** clears the keyword, favorites-only, included-only, nonzero, vertex and mesh filters. In Multi Frame Mode it also clears the keyed-only filter. Shape values, inclusion checkboxes, symmetry and favorite assignments stay unchanged.

These chips and the search field narrow down the shape key list. Combine multiple filters freely.

**🔍 Keyword**
Type to filter by name. Multiple words separated by spaces are treated as AND conditions. Press ✕ to clear.

> Example: Type `mouth open` to show only shape keys whose names contain both words.

**☆ Fav**
Shows only shape keys you have starred. Click the ☆ or ★ button on any row to toggle the favorite flag.

> Example: Star the 8-10 shape keys you use on every expression so you can isolate them instantly without searching.

**✓ Enabled**
Shows only shape keys whose save checkbox is currently checked.

> Example: Before saving, switch to this view to confirm exactly which shape keys will be written to the file.

**Non-zero**
Shows only shape keys with a slider value other than zero.

> Example: After loading a reference animation, use this filter to see which shape keys the expression actually moves.

**↔ Symmetry**
Pairs shape keys ending in `...L` and `...R` into a single combined row. The shared slider drives both sides at once. When the L and R values differ, they fall back to separate rows automatically.

> Example: `cheek_puff.L` and `cheek_puff.R` appear as one row. Turn Symmetry off if you need to set one side to a different value.

**● Vertex Filter**
Press this button, then click any vertex in the SceneView. The list narrows to show only shape keys that actually move that vertex. An active filter shows the selected vertex index. Press ✕ to clear.

> Example: A vertex on the upper lip is deforming in an unexpected way. Click it in the SceneView to find which shape keys affect that specific point.

**Mesh filter (popup)**
Appears only when two or more meshes are registered. Select **All meshes** to see every shape key together, or pick one mesh name to see only that mesh's shape keys.

---

### SHAPE KEYS List

The main editing area. Each row represents one shape key.

**☆ / ★**
Toggles the favorite flag for that shape key.

**Checkbox**
Determines whether this shape key is included in the exported `.anim` file. Checked keys are written; unchecked keys are ignored on save even if their value is non-zero.

**Name**
The shape key name as defined in the mesh. Checked items appear in a brighter style to distinguish them at a glance.

**[0]**
Resets the slider to zero without changing the save checkbox.

**Slider (0-100)**
Drag to adjust the blendshape weight. Supports Unity Undo/Redo (Ctrl+Z).

### Groups

Shape keys are automatically grouped by name prefix (e.g., all `mouth_*` keys form one group). The group header shows a collapse arrow, a checked/visible count, and a checkbox that checks or unchecks the entire group at once.

> Example: Your avatar has 40 mouth-related shape keys. Collapse the `mouth` group when you are only working on the eyebrows.

### Snapshot and Restore Snapshot

**Snapshot** saves the current slider values for all shape keys to memory. **Restore Snapshot** instantly reverts all sliders to that saved state.

> Example: You have a complex expression dialed in. Take a Snapshot, then experiment freely. If the experiment fails, Restore Snapshot brings back the working state in one click.

### Bulk Check Operations

Clicking the `✓▼` button in the toolbar opens a menu to perform bulk operations on the shape key checkboxes (inclusion toggles).

* **Check All Visible**: Checks all shape keys currently displayed in the list (matching search and filters). Shape keys bound to the VRCAvatarDescriptor eyelids/LipSync are excluded.
* **Uncheck All Visible**: Unchecks all shape keys currently displayed in the list.
* **Uncheck Unchanged (Value is 0)**: Unchecks all shape keys that are unchanged in the animation, regardless of the current search or filter visibility.
  * **Single Frame Mode (Pose)**: Unchecks shape keys whose current slider value is `0`.
  * **Multi Frame Mode (Animation)**: Unchecks shape keys that are constant at `0` throughout the entire animation clip (i.e. have no keyframes, or all of their keyframes have a value of `0`).
* **Check Favorites Only**: Checks only the starred (favorite) shape keys among those currently visible, and unchecks the rest.
* **Check Only Keys Differing from Reference Animation**: Compares the current slider values against the clip loaded in the optional source section, checks only the shape keys that differ, and unchecks the rest (applies to visible keys). Shape keys not contained in the clip are compared against the default value `0`, so changes outside the reference clip are also picked up as differences. This item is grayed out when no clip is set.
  > Example: Load an existing expression animation as a base, tweak it, then use this to include only your adjustments in the export.
* **Check Only Keys Differing from Snapshot**: Compares the current slider values against the Snapshot, checks only the shape keys that differ, and unchecks the rest (applies to visible keys). This item is grayed out when no Snapshot has been taken.

---

### SAVE SETTINGS

Controls how the finished expression is exported.

**Save To**
The folder where new `.anim` files are created. Type a path directly or click **Browse** to pick a folder.

**Enable Overwrite Save**
When enabled, a target clip field appears below. Saving writes directly to that existing file rather than creating a new one.

**Auto Backup on Overwrite**
When overwrite is enabled, this option copies the current file to a `_backups/` subfolder before overwriting it. Keep this on to avoid losing previous versions.

**Keep non-blendshape animation** (default: on)
Keeps non-blendshape data already present in the overwrite target - object toggles, bone transforms, material values and so on - instead of erasing it. When off, saving produces a blendshape-only animation.

> This data is NOT reflected in the DenEmo preview (the scene view). It is included in the saved `.anim`.

**Save expression as... / Overwrite [clip name]**
The included shape count appears above the button. Checked shapes are exported with their current values, including checked shapes hidden by keyword filters.

With overwrite off, choose a name in the save dialog. With overwrite on, the selected clip is updated and folder controls are disabled. The target must be a standalone `.anim` file inside `Assets`; embedded model clips cannot be overwritten directly.

If no shapes are included, or the overwrite target is missing or invalid, inline text explains the problem and saving is disabled. A missing overwrite target no longer falls back to creating a new file. Cancelling is reported as cancellation, not success. Overwriting an existing pose clip records the change in Undo.

> Example: You are making three variations of a "surprise" expression. Save the first normally. For the second variation, enable Overwrite Save pointing at the first file (with Auto Backup on) - the original is preserved in `_backups/` and the file is updated in place.

---

## Multi Frame Mode

Multi Frame Mode lets you create shape key animations that change over time. You record keyframes at different points on the timeline and the tool generates the curves automatically.

### ANIMATION CLIP

**Clip field**
Select an existing `.anim` file to edit it. The timeline and list update to reflect that clip's keyframes.

**New**
Opens a save dialog and creates a blank `.anim` file. The new clip is immediately loaded and ready to record into.

Sliders, search and the timeline remain hidden until a clip is selected; saving is disabled without a clip.

When no clip is loaded, the section shows a three-step workflow guide:

1. Drag an existing `.anim` file into the Clip field, or press **New** to create a blank clip.
2. Check / set **FPS** and **duration** in the Timeline section.
3. Move the playhead -> click **Start recording** -> drag sliders to record keyframes.

> Example: Start a "wink" animation by pressing New, saving the file, then recording keyframes using the timeline.

---

### SHAPE KEY VALUE CORRECTION

This section is **collapsed by default**. Click the header to expand it.

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

Press **Apply Correction to Timeline** to apply the rescaled values to the animation clip in memory (the timeline). This action itself does not save the asset file. To save the changes, use the Save section at the very bottom of the window.

The operation supports Undo (Ctrl+Z). Only shape keys whose Min/Max differ from the defaults (0 and 100) are affected.

> Example: A blink animation drives the `blink` shape key to 100, but your face texture modification already partially closes the eye, so reaching 100 looks broken. Set Max to 80 and press Apply Correction to Timeline. All blink keyframes are rescaled into the 0-80 range - the curve shape and timing remain unchanged. Then, use the Save section at the bottom of the window to save the clip.

> Example: You want `mouth_open` to never fully close in this animation. Set Min to 20. Original 0 keyframes become 20, original 100 keyframes stay at 100.

---

### TIMELINE

The timeline shows the playback position, transport controls, and a visual track area for every animated shape key. It can be detached from the main window.

**↗ Detach / ↘ Attach**
Detach opens the timeline in a separate floating window for more screen space. Closing the separate window (or pressing ↘ Attach inside it) returns the timeline to the main DenEmo window.

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
| `\|◆` | Jump to the previous keyframe |
| `<` | Step one frame backward |
| Play / Stop | Start or stop playback (loops continuously) |
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

**Start recording / Recording (REC)**
Click to toggle recording. When on, sliders record keys at the current time. With recording off, moving a shape that already has a key at this time updates that key. Moving a shape without a key creates an unrecorded preview change.

**Unrecorded changes**
A persistent notice shows the change count and two actions:

- **Record at current time** writes the changes as keys in one Undo operation.
- **Revert changes** restores existing tracks to their current curve values and untracked shapes to their preview-start values.

Saving is disabled until these changes are resolved. Record them before seeking, playing or switching clips; otherwise they are discarded with a warning. Bulk insertion for visible shapes only clears the pending changes actually inserted.

> Example: Move the playhead to frame 10, enable REC, drag the `mouth_smile` slider to 80. A keyframe is recorded. Move to frame 25, drag it back to 0. The animation now smoothly transitions from smile to neutral between frames 10 and 25.

#### Timeline Area

**Ruler**
Shows frame numbers above the track area. Tick density adjusts automatically based on clip length and window width.

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
Appears at the far right of each row. ◆ (filled) means a keyframe exists at the current playhead time. ◇ (outline) means no keyframe exists at this time. Clicking ◇ adds a keyframe; clicking ◆ removes the existing keyframe.

> Example: You want the mouth to be fully closed at frame 0 without using REC. Move to frame 0, set the mouth slider to 0, click ◇ to stamp the keyframe. Done.

---

### RECORDING BANNER

When **REC** is active, a red banner appears below the timeline:

> RECORDING - Dragging a slider records a keyframe at the current time

This banner makes the recording state unmissable so you always know whether slider movements will stamp keyframes.

---

### SAVE ANIMATION

**Keyframe statistics**
The top of the Save Animation section shows how many tracks and total keyframes the current clip contains (e.g., *3 tracks / 12 keyframes total*). If no keyframes have been recorded yet, a warning message is displayed instead, reminding you to add keyframes before saving.

**Save Animation**
Resolve unrecorded changes with Record at current time or Revert changes first.

Writes the current clip's keyframes to the `.anim` file. If the clip was loaded from an existing file, it overwrites that file. If the clip was just created with **New** and has not been saved to disk yet, a file save dialog appears.

> Example: After recording a full wink sequence, press Save Animation to finalize the file and make it available in the Project window.

---

## Apply to Avatar (FX Layer Expression Replacement)

The **APPLY TO AVATAR** mode allows you to bulk-replace expression animations (`.anim`) used within a VRChat avatar's FX layer (Animator Controller) with custom expressions you have created.

### Avatar / FX Layer Detection

**Automatically detects the avatar and its FX layer (Animator Controller).**

- **Auto-Detection**:
  If the target mesh is part of an avatar that has a `VRC Avatar Descriptor`, the tool automatically detects the avatar root and the custom Animator Controller assigned to the FX layer.
- **Manual Assignment**:
  If no avatar is detected or the VRChat SDK is not installed, a "VRChat avatar not found" warning is displayed. In this case, drag and drop the Animator Controller asset you want to edit directly into the **"Manual Controller"** field.
- **Rescan**:
  If you modify the avatar hierarchy or FX layer setup, click the **"Rescan"** button to update the detected information.

---

### Expression Mapping List

**Clear filters** clears keyword, assigned-only, hand and gesture filters without clearing clip assignments.

Lists the animation clips within the FX controller that animate the shape keys of your target mesh.

- **Search & Filters**:
  - **🔍 Keyword**: Filter by the original animation name (supports space-separated multi-word AND search).
  - **Assigned Only**: Shows only the entries that have a replacement animation assigned.
- **Show all blendshape animations**:
  If no animations matching the target mesh are found, click this to bypass the path restriction and display all animation clips in the controller.
- **Mesh path mismatch (Warning)**:
  If the hierarchy path of the mesh in the animation binding does not match the current path of the target mesh on the avatar, a "⚠ Mesh path mismatch" warning is displayed.

#### Preview on Hover
Hovering your mouse over any animation name in the list plays a real-time preview of that expression on the avatar in the Scene view. If a replacement is assigned, the new animation is previewed instead. The active preview animation is displayed in the info band at the bottom, and you can stop it at any time.

#### Assigning Replacements
Click the **"None ▾"** button (or the assigned clip name) on the right side of a row to open a popup picker showing shape key animations in the project. Alternatively, drag and drop any `.anim` file from the Project window directly onto the slot button.
- When assigned, a green indicator bar appears on the left edge of the row.
- Click the `✕` button to clear the assignment.
- If the same animation is referenced in multiple states/places, click the `▶ / ▼` toggle on the left of the row to expand the list and enable/disable replacements for specific slots individually.

---

### Apply Settings & Execution

The summary shows the target FX controller, number of expressions and affected locations. Assignments hidden by filters are included. When no replacements are assigned, inline text explains how to start. Review the replacements in the confirmation dialog before applying.

Applies the mapping configuration to the FX controller.

- **Duplicate and Apply (Recommended)**:
  Creates a duplicate of the FX controller, modifies only the duplicate, and assigns it to the avatar's FX layer. This keeps your original controller asset safe and untouched.
- **Direct Modify**:
  Modifies the existing FX controller asset directly and records the changes in Undo. This operation does not create an automatic backup. Use Duplicate and Apply to preserve the original controller.
- **Manual Assignment Note**:
  If no avatar is detected, you will need to manually set the duplicated controller into the avatar's FX layer.

Click the **"Replace [N] Expressions"** button to open a confirmation dialog showing a summary of the changes. Click "OK" to apply. The results (new controller path, backup location, Descriptor update status) will be shown in a card, and you can click the **"Show" (Ping)** button to locate the new controller in your Project view.

---

## FAQ

**Q. Why use "Align Animation Keys" instead of unchecking shape keys manually?**

A. Avatars often contain shape keys for gimmicks, physics toggles, or other non-expression systems. If these end up in your animation, they can interfere with other avatar features. Aligning to a reference clip excludes all of them in one step.

**Q. Does DenEmo support Undo?**

A. Yes. Slider adjustments, checkbox toggles, and loading animation values all participate in Unity's Undo/Redo system (Ctrl+Z / Ctrl+Y).

**Q. How do I adjust only one side when ↔ Symmetry is on?**

A. Turn off the ↔ Symmetry chip, adjust the individual L or R row, then turn Symmetry back on. The L and R values will remain independent until they happen to become equal again.

**Q. Can I work in Multi Frame Mode without looking at the timeline?**

A. Yes. Detach the timeline with ↗ Detach, minimize or move that window out of the way, and use only the ◆ / ◇ buttons in the shape key list combined with REC mode. The timeline still records keyframes correctly in the background.
