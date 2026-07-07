# DenEmo

## 概要

DenEmoは、Unity上でSkinnedMeshRendererのシェイプキー（ブレンドシェイプ）を効率的に調整・アニメーション化するためのエディタ拡張ツールです。VRChatアバターの表情・リップシンクアニメーション作成をサポートします。

## 主な機能

- シングルフレームモード（ポーズ）: シェイプキー値を調整して静止表情を作成し、.animファイルに書き出し
- マルチフレームモード（アニメーション）: タイムラインでキーフレームを編集してアニメーションクリップを作成
- 複数のSkinnedMeshRendererを同時管理（メッシュ切り替えフィルタ付き）
- 左右同期編集（Symmetryモード）
- 頂点フィルタ：SceneViewで頂点を選択し、その頂点を動かすシェイプキーのみ表示
- お気に入り・グループ折りたたみ
- スナップショット保存/復元
- Undo/Redo対応
- VRChat VRCAvatarDescriptorのリップシンクシェイプを自動除外
- NDMFプレビューメッシュに対応した頂点ガイド表示
- 日本語/英語UI切り替え

## 使い方

詳細なユーザーガイドは以下のドキュメントを参照してください:
- [日本語ユーザーガイド](Docs/USAGE_ja.md)
- [English User Guide](Docs/USAGE.md)

### 基本的な流れ

1. Unityメニュー `dennokoworks/DenEmo` からウィンドウを開く
2. 対象のSkinnedMeshRendererをドラッグ&ドロップ、またはフィールドにアサイン
3. **シングルフレームモード（Single Frame）**: スライダーでシェイプキー値を調整 → 保存設定セクションで.animファイルを出力
4. **マルチフレームモード（Multi Frame）**: クリップを読み込みまたは新規作成 → タイムラインでキーフレームを打ち → アニメーションを保存

### シングルフレームモード

- **アニメーション参照**: 既存クリップの値をメッシュに適用、またはクリップに含まれるシェイプキーのみ有効化（キー揃え）
- **保存設定**: 出力フォルダ指定、上書き保存（自動バックアップ付き）、新規ファイル保存に対応

### マルチフレームモード

- **クリップ操作**: クリップのロード・新規作成、FPS・長さ設定
- **タイムライン**:
  - 再生/停止、フレームステップ、前後のキーフレームへジャンプ
  - 再生速度調整（0.1×〜4×）
  - スムーズループ対応（先頭/末尾キーを自動同期）
  - RECモード: スライダー操作が自動でキーフレームとして記録される
  - 別ウィンドウにタイムラインを切り離し可能
- **補完タイプ**: Step / Linear / Ease を全キー一括変更またはキーごとに設定
- **シェイプキー値補正**: クリップ全体の各シェイプキーのキーフレーム値を最小値/最大値でリスケール（VRChatまばたき競合対策に有効）
- **キー操作**:
  - ◆/◇ ボタンでキーフレームのトグル
  - Ctrl+C/V で現在時刻のキーフレームをコピー＆ペースト
  - Delete/Backspace で現在時刻の全キーフレームを削除

### 検索・フィルタ

| チップ | 動作 |
|---|---|
| ★ お気に入り | お気に入り登録済みのみ表示 |
| ✓ 有効のみ | 有効化（保存対象）のみ表示 |
| ≠0 非ゼロ | 値が0でないもののみ表示 |
| ↔ 左右同期 | 末尾がL/Rのシェイプキーを1行に統合して同時編集 |
| 頂点フィルタ | SceneViewで頂点をクリックし、その頂点を動かすシェイプキーのみ表示 |
| ◆ キー有りのみ | マルチフレームモードのみ: 現在クリップでキーフレームがあるシェイプキーのみ表示 |

### キーボードショートカット（タイムライン）

| キー | 動作 |
|---|---|
| Space | 再生/停止 |
| ← / → | 1フレーム戻る/進む |
| , / . | 前/次のキーフレームへ移動 |
| Delete / Backspace | 現在時刻の全キーフレームを削除 |
| Ctrl+C | 現在時刻のキーフレームをコピー |
| Ctrl+V | 現在時刻にキーフレームをペースト |

### 左右同期編集（Symmetry）

- Symmetryチップをオンにすると、末尾がL/RのシェイプキーがI行に統合表示されます
- 対応サフィックス: `_L`/`_R`, `.L`/`.R`, `-L`/`-R`, ` (L)`/` (R)`, ` L`/` R`（小文字の`l`/`r`も可）、`_左`/`_右`, `.左`/`.右`, `-左`/`-右`, ` (左)`/` (右)`, ` 左`/` 右`
- スライダー・ゼロボタン・チェックボックスの操作が左右に同じ値で適用されます

## ファイル構成

```
DenEmoWindow.cs               メインウィンドウ（エントリーポイント）
DenEmoWindow.Sections.cs      UIセクション（対象メッシュ・検索・フィルタ・保存）
DenEmoWindow.VertexFilter.cs  頂点フィルタ・SceneGUI
DenEmoWindow.Preferences.cs   設定の永続化
Core/
  AnimationExporter.cs        .animファイルのエクスポート
  AnimationPreviewController.cs ブレンドシェイププレビュー・キーフレーム記録
  SymmetryParser.cs           L/R対称シェイプキーの解析
  LipSyncExclusionRule.cs     VRChatリップシンクシェイプ自動除外
UI/
  AnimationModeUI.cs          マルチフレームモードのオーケストレーター
  TimelineUITKView.cs         タイムラインUI（UI Toolkit / Painter2D。埋め込み・別窓兼用）
  AnimationClipCorrectionUI.cs シェイプキー値補正UI
  ShapeKeyListUI.cs           シェイプキーリスト（.Rows.cs / .Segments.cs）
  DenEmoTimelineWindow.cs     タイムライン別ウィンドウ
  DennokoTheme.uss            デザイントークン（USS変数）
  DenEmoStyles.uss            プロジェクト固有のUSSクラス
  DenEmoUiAssets.cs           UXML/USSのGUIDロード
  VertexPreviewOptionsPopup.cs 頂点プレビュー表示設定ポップアップ
Models/
  AnimationClipModel.cs       アニメーションクリップの状態管理
  ShapeKeyModel.cs            ブレンドシェイプデータ・検索/フィルタ/グループ
  ShapeKeyItem.cs             シェイプキー1件のデータ
Utils/
  DenEmoLocalization.cs       日本語/英語ローカライズ
  DenEmoProjectPrefs.cs       プロジェクト単位の設定永続化
```

---

# DenEmo

## Overview

DenEmo is a Unity Editor extension for efficiently editing and animating blendshapes (shape keys) on SkinnedMeshRenderer components. It is primarily designed for VRChat avatar facial expression and lipsync animation workflows.

## Features

- **Single Frame mode (Pose)**: Adjust shape key values for a static expression and export as a .anim file
- **Multi Frame mode (Animation)**: Record keyframe-based animations on a timeline and export as an AnimationClip
- Multiple SkinnedMeshRenderer targets with per-mesh filter
- Symmetry edit mode (merge and edit L/R shape keys simultaneously)
- Vertex filter: pick a vertex in the SceneView and show only shape keys that move it
- Favorites, group collapse/expand
- Snapshot save/restore
- Undo/Redo support
- Auto-exclusion of VRChat VRCAvatarDescriptor lipsync shapes
- NDMF preview mesh support for vertex guide rendering
- Switchable Japanese/English UI

## Usage

For detailed instructions, see:
- [Japanese User Guide](Docs/USAGE_ja.md)
- [English User Guide](Docs/USAGE.md)

### Quick Start

1. Open the window from `dennokoworks/DenEmo` in the Unity menu bar
2. Drag and drop a SkinnedMeshRenderer (or GameObject) onto the window, or assign it in the target field
3. **Single Frame mode**: Adjust sliders → export .anim from the Save Settings section
4. **Multi Frame mode**: Load or create a clip → set keyframes on the timeline → Save Animation

### Single Frame Mode

- **Animation Source**: Apply an existing clip's values to the mesh, or align include flags to match shapes present in the clip
- **Save Settings**: Output folder, overwrite mode (with optional auto-backup), or new file

### Multi Frame Mode

- **Clip**: Load or create a new AnimationClip, configure FPS and duration
- **Timeline**:
  - Transport: play/stop, frame step, jump to previous/next keyframe
  - Playback speed (0.1×–4×)
  - Smooth loop: auto-synchronizes the first and last keyframe values for seamless looping
  - REC mode: slider moves automatically write keyframes at the current playhead
  - Detachable into a separate Timeline window
- **Interpolation**: Step / Linear / Ease — set per keyframe or bulk-apply to all
- **Shape Key Value Correction**: Rescale min/max of each shape key's keyframe values across the entire clip (useful to fix blink conflicts in VRChat)
- **Keyframe operations**:
  - ◆/◇ button to toggle a keyframe on the current shape
  - Ctrl+C/V to copy and paste keyframes at the current time
  - Delete/Backspace to delete all keyframes at the current time

### Search & Filter

| Chip | Behavior |
|---|---|
| ★ Fav | Show favorites only |
| ✓ Enabled | Show included shapes only |
| ≠0 Non-zero | Show non-zero values only |
| ↔ Symmetry | Merge L/R pairs into one row for simultaneous editing |
| Vertex filter | Show only shape keys that affect the picked vertex |
| ◆ Keyed Only | (Animation mode) Show only shapes with tracks in the current clip |

### Keyboard Shortcuts (Timeline)

| Key | Action |
|---|---|
| Space | Play / Stop |
| ← / → | Step one frame backward / forward |
| , / . | Jump to previous / next keyframe |
| Delete / Backspace | Delete all keyframes at current time |
| Ctrl+C | Copy keyframes at current time |
| Ctrl+V | Paste copied keyframes at current time |

### Symmetry Edit

- Turn on the **↔ Symmetry** chip to merge shape key pairs whose names end in L/R into a single row
- Supported suffixes: `_L`/`_R`, `.L`/`.R`, `-L`/`-R`, ` (L)`/` (R)`, ` L`/` R` (lowercase also supported), `_左`/`_右`, `.左`/`.右`, `-左`/`-右`, ` (左)`/` (右)`, ` 左`/` 右`
- Slider, zero button, and include checkbox apply the same value to both sides simultaneously

## License

MIT License
