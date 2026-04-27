# DenEmo

## 概要

DenEmoは、Unity上でSkinnedMeshRendererのシェイプキー（ブレンドシェイプ）を効率的に調整・アニメーション化するためのエディタ拡張ツールです。VRChat向けモデルや表情アニメーションの作成をサポートします。

- シェイプキー値の調整
- アニメーションファイル（.anim）の生成
- 既存アニメーションとのキー揃え
- スナップショット保存・復元
- Undo/Redo対応
- 日本語・英語UI切り替え
- 左右同期編集（Symmetry）モード（L/R統合表示・同時編集）

## 使い方

詳細な使い方は以下のドキュメントを参照してください。
- [日本語ユーザーガイド](Docs/USAGE_ja.md)
- [English User Guide](Docs/USAGE.md)

1. Unityのメニュー「Tools > DenEmo」からウィンドウを開きます。
2. 対象の顔のメッシュ（SkinnedMeshRendererを持つオブジェクト）を指定します。
3. ポーズモードまたはアニメーションモードを選択して編集します。
4. 設定が完了したら保存ボタンで.animファイルを出力できます。

### 左右同期編集（Symmetry）
- フィルタ行の「Symmetry」チェックをONにすると、末尾が L/R のシェイプキーを1行に統合して表示します。
- 対応サフィックス: `_L`/`_R`, `.L`/`.R`, `-L`/`-R`, ` (L)`/` (R)`, ` L`/` R`（小文字の`l`/`r`も可）; `_左`/`_右`, `.左`/`.右`, `-左`/`-右`, ` (左)`/` (右)`, ` 左`/` 右`
- 統合行でのスライダー操作・0ボタン・チェックは左右に同じ値で適用されます。

## ファイル構成
- DenEmoWindow.cs: メインウィンドウ（エントリーポイント）
- Core/: ロジック・エクスポート・左右同期処理など
- UI/: UIコンポーネント・タイムライン・リスト表示
- Models/: データモデル
- Utils/: 共通ユーティリティ・多言語対応

---

# DenEmo

## Overview

DenEmo is a Unity Editor extension for efficiently editing and animating blendshapes (shape keys) on SkinnedMeshRenderer components. Ideal for VRChat avatars and facial animation workflows.

- Batch and individual blendshape adjustment
- Animation file (.anim) export
- Key alignment with existing animations
- Snapshot save/restore
- Undo/Redo support
- Switchable Japanese/English UI
- Symmetry edit mode (merge and edit L/R together)

## Usage

For detailed instructions, please refer to the following documents:
- [Japanese User Guide](Docs/USAGE_ja.md)
- [English User Guide](Docs/USAGE.md)

1. Open the window from Unity menu: "Tools > DenEmo"
2. Assign your target SkinnedMeshRenderer
3. Choose either Pose Mode or Animation Mode to edit.
4. Click "Save Animation" to export a .anim file.

### Symmetry Edit
- Turn on the "Symmetry" toggle in the filter row to merge shape keys that end with L/R into a single row.
- Supported suffixes: `_L`/`_R`, `.L`/`.R`, `-L`/`-R`, ` (L)`/` (R)`, ` L`/` R` (lowercase `l`/`r` also supported)
- Slider, zero button, and include checkbox apply to both sides simultaneously.

## File Structure
- DenEmoWindow.cs: Main window (Entry point)
- Core/: Logic, Export, Symmetry processing, etc.
- UI/: UI Components, Timeline, List view
- Models/: Data models
- Utils/: Common utilities, Localization support

## License
MIT License
