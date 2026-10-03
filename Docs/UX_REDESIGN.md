# UX redesign experiment

Branch: `experiment/ux-guided-workflow`

## Objective

Rebuild the editing experience so users can create a pose, animate it, and apply
expressions to an avatar without reading a manual. Preserve the dennokoworks
dark-gray theme, UI Toolkit, Japanese/English localization, preview isolation,
and Undo behavior. The work is experimental and is not complete yet.

## Design references

- Apple onboarding: https://developer.apple.com/design/human-interface-guidelines/onboarding
  Keep instructions close to the task and avoid requiring a separate tutorial.
- Material states: https://m3.material.io/foundations/interaction/states/overview
  Make current interaction state visible.
- Local design source: `.claude/skills/dennokoworks_color_schema/SKILL.md`
  Use the existing dark surfaces and semantic tokens; keep layout in USS.

## Implemented foundation

- Task-based pose/animation tab names, larger flexible tabs.
- Contextual guidance for target selection, clip creation, pose editing,
  animation editing, recording, and avatar application.
- A single action clears search and visibility filters, including mesh and
  animation-key filtering. It preserves symmetry, shape values and saved-target
  selections. Search receives keyboard focus afterward.
- Localized clear-button tooltips and the all-meshes option.
- Optional pose import is collapsed initially (expanded when a reference clip
  is already selected), bringing editing controls closer to the target.
- Pose save shows the included shape count and explicit new/overwrite action.
  Missing included shapes or overwrite destinations show inline recovery text
  and disable saving. Embedded/model clips and paths outside Assets cannot be
  selected for direct pose overwrite. Cancelling no longer reports success.
- Pose clip overwrites register Undo before clearing/writing curves.
- Animation sliders/search wait for a clip; saving is disabled without a clip.
- Animation changes made without recording or an existing key have a persistent
  warning with Record at current time / Revert changes actions. Saving waits for
  resolution. Values are retained per shape for recording; reverting restores
  untracked shapes as well as resampling existing curves. Playback flushes pending
  keys before time advances. Batch insert clears only changes actually inserted.
- Recording has explicit start/active labels instead of a constant REC caption.
- FX has labeled search, clear search/gesture filters, and a pre-apply summary of
  the target controller, expression count and affected locations. Inline text
  explains that hidden assignments are included and a review dialog follows.

## Remaining implementation and audit

- Review the full pose workflow: optional clip import, editing, saved-shape
  selection, export destination, overwrite prevention and completion feedback.
- Review animation creation, recording discoverability, unrecorded edits,
  timeline navigation, detached timeline, key deletion and Undo/Redo preview.
- Review FX assignment, preview, duplicate/direct application and recovery.
- Reduce advanced-option clutter without hiding essential state; review empty
  search results, target acquisition and invalid input recovery.
- Inspect actual layouts at 480 x 400 and wider sizes in Unity Light and Dark,
  including Japanese/English, keyboard focus and font reload behavior.

## Validation so far

UXML parses as XML, names are unique, new localization keys exist in JA and EN,
no duplicate localization keys were found, and `git diff --check` passes.
All DenEmo Editor C# sources also compiled successfully using Unity 2022.3.22f1's
Mono/Roslyn compiler, C# 9, and the generated project's reference DLLs/defines.
Output: `%TEMP%/DenEmoUxCompile/DenEmo.Ux.dll`; response file beside it.
Four pre-existing CS0649 warnings concern version-check JSON fields; no errors.
This standalone compilation does not establish USS rendering or interactive
correctness. Unity runtime verification is outstanding.

Pose save acceptance checks: no included shapes, overwrite with no destination,
embedded FBX clip, valid Assets .anim, new save cancellation, new save success,
overwrite with/without extra curves, backup, and Undo/Redo after overwrite.
Confirm the included count agrees with the exported curves for multiple meshes.

Animation acceptance checks: edit an unkeyed shape with recording off, verify the
warning and save gate, record it and Undo/Redo; revert an untracked shape with a
nonzero starting weight; insert only filtered shapes and confirm other unrecorded
changes remain identified; start playback with pending throttled keys; switch
clips/modes and confirm no pending values are transferred to the next clip.
FX acceptance checks: clear keyword/assigned/hand/gesture filters without losing
assignments; verify the summary against confirmation and actual replaced slots;
test duplicate and direct application and Undo/recovery.

Filter acceptance checks: combine keyword/favorites/included/nonzero/vertex/
mesh/key filters, clear them, verify all eligible shapes return, values and
inclusion flags remain unchanged, symmetry stays enabled, and preferences do
not restore the cleared filters after reopening. Verify this in both pose and
animation modes, with multiple meshes and after Undo/Redo.
