# P1: Undo/Redo 後のプレビュー未更新バグ修正

## 問題

`DenEmoWindow.OnUndoRedo()` が `_model.SyncValuesFromMesh()` と `Repaint()` しか呼ばない。
Ctrl+Z でキーフレームを元に戻しても `AnimationPreviewController._curveCache` がダーティにならず、
モデルのブレンドシェイプ値が古い状態のまま表示され続ける。

## 修正箇所

**ファイル**: `DenEmoWindow.cs`  
**メソッド**: `OnUndoRedo()` (現在 255–259 行)

### 現在のコード

```csharp
private void OnUndoRedo()
{
    _model.SyncValuesFromMesh();
    Repaint();
}
```

### 修正後

```csharp
private void OnUndoRedo()
{
    _model.SyncValuesFromMesh();
    if (_currentMode == EditorMode.Animation && _animModeUI.Preview.IsActive)
    {
        _animModeUI.Preview.SetCacheDirty();
        _animModeUI.Preview.SampleAt(_animModeUI.ClipModel.CurrentTime);
    }
    Repaint();
}
```

## 理由

- `Undo.RecordObject(clip, ...)` は `AnimationClip` アセットを Undo スタックに積む
- Undo 実行後、Unity はクリップのカーブを元の状態に戻す
- しかし `AnimationPreviewController._curveCache` は古いカーブを参照したままなので再構築が必要
- `SetCacheDirty()` → `SampleAt()` で次フレームのサンプリング時にキャッシュが再構築され、SMR に正しい値が書き込まれる

## テスト手順

1. Multi Frame モードでキーフレームを 2 つ記録する
2. Ctrl+Z で 1 つ元に戻す
3. シーンビューのモデルが 1 つ目のキーの状態に戻ることを確認する（修正前は変化しない）
4. Ctrl+Y（Redo）で再適用し、モデルが 2 つ目のキーの状態に戻ることを確認する
