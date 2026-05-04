# P3: 現在フレームへの全シェイプ一括キー追加

## 問題

Unity の Animation ウィンドウにはツールバーに「Add Keyframe (◆)」ボタンがあり、
表示中の全プロパティの現在値を現在フレームに一括記録できる。

現在の実装には:
- 全フレームの **削除** はある（`DrawKeyframeDeleteButtons` の ✕ 行）
- 全シェイプへの **追加** がない

RECモードをオフにしたまま特定フレームに手動で全シェイプのキーを打ちたい場合に方法がない。

## 仕様

ボタン1つで「現在スクラバーが指しているフレームに、現在シェイプキーリストに表示されている全シェイプの現在値をキーとして追加する」。

- REC モードの ON/OFF に関わらず使用できる
- フィルタ（「キー有りのみ」など）が有効なら表示シェイプのみ対象
- すでにそのフレームにキーが存在する場合は現在値で上書き
- 追加したキーの補間タイプは `currentInterp` に従う

## 実装箇所

### 1. `AnimationPreviewController` にメソッド追加

**ファイル**: `Core/AnimationPreviewController.cs`

```csharp
/// <summary>
/// 現在時刻に、指定シェイプリスト全てのキーを一括記録する。
/// </summary>
public void RecordAllKeyframes(
    IEnumerable<ShapeKeyItem> items,
    string smrPath,
    float time,
    InterpolationType interp)
{
    if (_clipModel?.Clip == null) return;

    var smr = _shapeModel?.TargetSkinnedMesh;
    if (smr == null || smr.sharedMesh == null) return;

    Undo.RecordObject(_clipModel.Clip, "Insert Keyframe (All)");

    foreach (var item in items)
    {
        int index = smr.sharedMesh.GetBlendShapeIndex(item.Name);
        if (index < 0) continue;
        float value = smr.GetBlendShapeWeight(index);

        var binding = MakeBinding(item.Name, smrPath);
        var curve = AnimationUtility.GetEditorCurve(_clipModel.Clip, binding) ?? new AnimationCurve();
        float tol = _clipModel.FPS > 0f ? 0.5f / _clipModel.FPS : 0.01f;
        WriteSingleKey(curve, time, value, interp, tol);
        AnimationUtility.SetEditorCurve(_clipModel.Clip, binding, curve);
    }

    EditorUtility.SetDirty(_clipModel.Clip);
    _cacheDirty = true;
}
```

`WriteSingleKey` は既存の private メソッドをそのまま再利用する（アクセス修飾子の変更不要）。

### 2. ボタンの追加場所

`DrawKeyframeDeleteButtons()` の左端（ラベル列）に「全追加」ボタンを配置する。

**ファイル**: `UI/AnimationTimelineUI.Scrubber.cs`  
**メソッド**: `DrawKeyframeDeleteButtons()` (現在 106–159 行)

現在この行はキーのある時刻ごとに ✕ ボタンと ＝ ドラッグハンドルを描画する「削除行」。
この行の左端ラベル部分にボタンを追加する。

```csharp
private void DrawKeyframeDeleteButtons(
    AnimationClipModel clipModel,
    AnimationPreviewController preview,
    string smrPath,
    ShapeKeyModel shapeModel,          // ← 引数を追加
    InterpolationType currentInterp,   // ← 引数を追加
    EditorWindow window)
{
    float[] allKeys = clipModel.GetAllKeyTimes(smrPath);

    Rect rowRect = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
    if (Event.current.type == EventType.Repaint)
    {
        EditorGUI.DrawRect(rowRect, DenEmoTheme.Surface0);
        float cy = rowRect.y;
        EditorGUI.DrawRect(
            new Rect(rowRect.x + _trackLabelWidth, cy, rowRect.width - _trackLabelWidth, 1),
            DenEmoTheme.Outline);
    }

    // ── 左端: 「現在フレームに全シェイプのキーを追加」ボタン ──────────────
    var insertTip = DenEmoLoc.EnglishMode
        ? "Insert keyframe at current time for all visible shapes"
        : "現在フレームに表示中の全シェイプのキーを追加";
    var insertLabel = new GUIContent("◆+", insertTip);
    Rect insertRect = new Rect(rowRect.x + 4, rowRect.y + 8, _trackLabelWidth - 12, 20);
    if (GUI.Button(insertRect, insertLabel, DenEmoTheme.MiniButtonStyle))
    {
        // ShapeKeyModel から現在表示中のアイテムを収集
        var visibleItems = shapeModel.Items
            .Where(i => i.IsVisible && !i.IsVrcShape && !i.IsLipSyncShape)
            .ToList();
        preview.RecordAllKeyframes(visibleItems, smrPath, clipModel.CurrentTime, currentInterp);
        preview.SampleAt(clipModel.CurrentTime);
        window.Repaint();
    }

    // ── 以降は既存の allKeys == null チェックと各フレームの ✕ / ＝ ──────────
    if (allKeys == null || allKeys.Length == 0) return;
    // ... 既存コード続く
```

### 3. `Draw()` からの呼び出し更新

**ファイル**: `UI/AnimationTimelineUI.cs`  
**メソッド**: `Draw()` 内の `DrawKeyframeDeleteButtons` 呼び出し

```csharp
// 変更前
DrawKeyframeDeleteButtons(clipModel, preview, smrPath, window);

// 変更後
DrawKeyframeDeleteButtons(clipModel, preview, smrPath, shapeModel, currentInterp, window);
```

## ローカライズキー追加

`DenEmoLocalization.cs` の `JA` / `EN` 両方に追加:

```csharp
// JA
["ui.timeline.insertAll"] = "現在フレームに全シェイプのキーを追加",
// EN
["ui.timeline.insertAll"] = "Insert keyframe at current time for all visible shapes",
```

ただし上記は `GUIContent` のツールチップに直接ハードコードしてもよい（短い文字列のため）。

## 注意事項

- `ShapeKeyItem.IsVisible` は検索・フィルタ適用後のフラグ。フィルタが有効なら絞り込み後のシェイプのみ対象となる。これは意図した動作（表示中のものだけ追加する）。
- `using System.Linq;` が `AnimationTimelineUI.Scrubber.cs` に未追加の場合は追加する。
- ボタンラベルは `◆+` を想定しているが、幅が足りない場合は `+◆` や `All◆` に変えること。

## テスト手順

1. クリップを開いてスクラバーをフレーム 0 に置く
2. スライダーでいくつかのシェイプを動かす
3. `◆+` ボタンを押す → タイムラインにトラックが追加されることを確認
4. 別のフレームに移動して `◆+` を押す → 既存トラックに 2 つ目のキーが追加されることを確認
5. フレーム 0 に戻って同じシェイプ値で `◆+` → 上書きされる（重複キーが追加されない）ことを確認
6. Ctrl+Z で戻る → まとめて 1 回の Undo で全部のキーが消えることを確認
