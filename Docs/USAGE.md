# DenEmo Usage Guide

DenEmo is a Unity Editor extension designed to efficiently adjust shape keys (blendshapes) and create facial expression poses or animations.
With its intuitive UI, it dramatically improves the workflow for avatar facial expression modification and animation production, especially for VRChat.

---

## 1. Basic Workflow

The tool consists of two primary modes. You can switch between them using the tabs at the top.

1.  **POSE Mode**: Create a single-frame expression (pose) and export it as an .anim file.
2.  **ANIMATION Mode**: Use the timeline to create expressions that change over time (animations).

---

## 2. Target Mesh Selection (Common)

In the **"TARGET MESH"** section at the top, specify the mesh (SkinnedMeshRenderer) you want to adjust.

-   **Mesh Field**: Drag and drop a face mesh from the hierarchy or use the circle button to select one.
-   **Refresh Button**: Reloads the list if the blendshape configuration of the mesh has changed.

---

## 3. Pose Mode

The main mode for creating static facial expression poses.

### ANIMATION SOURCE
Functions for utilizing existing animation files (.anim).
-   **Load Animation**: Applies the values at 0s from the specified clip to the current mesh.
-   **Align Animation File Keys**: Automatically sets the "Save Target (checkbox)" to only those blendshapes contained in the specified clip. This allows you to easily create variations with the same structure as existing animations.

### SEARCH & FILTER
Tools to quickly find specific shape keys among many.
-   **🔍 Search Bar**: Search by name. Supports multi-word AND searches using spaces.
-   **★ Fav**: Register frequently used shape keys as favorites and display only them.
-   **✓ Enabled**: Display only shape keys marked for saving (checked).
-   **≠0 Non-zero**: Display only shape keys with a value other than 0.
-   **↔ Symmetry**: Merges shape keys ending in `...L` and `...R` into a single row for simultaneous adjustment.

### Blendshape List
-   **Sliders**: Adjust values (0–100).
-   **Checkboxes**: Select whether to save that shape key into the animation file.
-   **Snapshot**: Temporarily saves all current values to memory.
-   **Restore Snapshot**: Instantly reverts to the saved snapshot state.

### SAVE SETTINGS
-   **Save To (default)**: Specify the default folder for new animations.
-   **Enable Overwrite Save**: When enabled, saves directly to the specified existing clip.
-   **Auto Backup on Overwrite**: Copies the original file to a `_backups/` folder before overwriting.

---

## 4. Animation Mode

A mode for creating animations with a time progression. *Note: Some features are currently under development.*

### ANIMATION CLIP
-   Select a clip to edit or click the **"New"** button to create a fresh one.

### Timeline
-   **Play/Stop**: Preview the clip's animation.
-   **Record (REC)**: When in recording mode (red), moving a slider will automatically set a keyframe at the current playback time.
-   **FPS / Length**: Set the frame rate and duration of the clip.
-   **Interp**: Choose the interpolation method between keyframes (Ease / Linear / Step).

### Operations in the List
In Animation Mode, a **◆ (Keyframe)** icon appears to the right of each shape key.
-   **◆ (Filled)**: A keyframe exists at the current time.
-   **◇ (Outline)**: No keyframe exists at the current time.
-   Clicking the icon allows you to directly add or delete a keyframe at the current playback time.

---

## FAQ

#### Q. Why should I use "Align saved keys to existing animation"?
A. Avatars often contain many shape keys that are not for facial expressions (e.g., for gimmicks). If these are included in your animation, they can cause unintended behavior or conflicts. Aligning to a known good animation ensures only the necessary keys are exported.

#### Q. Is Undo supported?
A. Yes, slider adjustments, checkbox toggles, and applying animations all support Unity's Undo/Redo system.

#### Q. How can I fine-tune only one side in Symmetry mode?
A. Temporarily disable Symmetry mode to adjust sides independently.
