# マルチフレームモード UX 改善 — マスタープラン

## 背景

前回の UX 評価と Unity 標準 Animation ウィンドウとの機能比較により、以下の問題を特定した。

- **バグ**: Ctrl+Z 後にプレビューが更新されない（キャッシュ未クリア）
- **基本機能の欠如**: キーボードショートカット、一括キー追加、タイムラインズーム、コピペ
- **UX の問題**: フロー順序、フィードバック不足など（別計画で扱う）

## 実施計画一覧

| ファイル | 内容 | 優先度 | 依存 |
|----------|------|--------|------|
| [ux-P1-undo-bugfix.md](ux-P1-undo-bugfix.md) | Undo/Redo 後のプレビュー未更新バグ修正 | P1 🔴 | なし |
| [ux-P2-keyboard-shortcuts.md](ux-P2-keyboard-shortcuts.md) | キーボードショートカット実装 | P1 🔴 | なし |
| [ux-P3-insert-key-all.md](ux-P3-insert-key-all.md) | 現在フレームへの全シェイプ一括キー追加 | P2 🟡 | なし |
| [ux-P4-timeline-zoom.md](ux-P4-timeline-zoom.md) | タイムラインのズーム＆スクロール | P2 🟡 | なし |
| [ux-P5-copy-paste.md](ux-P5-copy-paste.md) | コピー＆ペースト（キーフレーム） | P2 🟡 | P2-キーボード |
| [ux-P6-hover-tooltip.md](ux-P6-hover-tooltip.md) | キーホバー時の値・時刻ツールチップ | P3 🟢 | なし |

## 実施順序

```
P1-undo-bugfix   ←── 即修正（1 箇所 3 行）
P2-keyboard      ←── 他機能の基盤になる（Delete / Space）
P3-insert-key    ←── 独立して実装可能
P4-zoom          ←── 独立して実装可能
P5-copy-paste    ←── P2 の keyboard 実装後（Ctrl+C/V ショートカットを使用）
P6-tooltip       ←── 独立して実装可能
```

## 対象外（スコープ外）

- カーブ編集（意図的に除外）
- キーフレームの矩形選択 UI（実装コストが高く、他機能より優先度低）
- WrapMode 設定（VRChat ではアニメーターで制御するため優先度低）
- 作成準備段階の UX 改善（別の計画 `animation-mode-refactoring-plan.md` で扱う）

## 共通の注意事項

- 新規ローカライズキーはすべて `DenEmoLocalization.cs` の `JA` と `EN` 両方に追加する
- 新規 `GUIStyle` は `Ensure*()` パターンで遅延生成し、ドメインリロードに対応させる
- キーフレーム書き換えは必ず `Undo.RecordObject` → 書き換え → `SetCacheDirty()` → `SampleAt()` の順で行う
- `EventType.KeyDown` を処理したイベントは必ず `Event.current.Use()` を呼ぶ
