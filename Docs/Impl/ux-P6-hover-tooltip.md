# P6: キーフレームホバー時の値・時刻ツールチップ

## 問題

タイムラインのキーフレーム（◆）にホバーしても何も表示されない。
Unity の Animation ウィンドウでは "Frame: N / Value: X.X" が表示される。

加えて、キーをドラッグ移動中にどのフレームに移動しているかが視覚的に分からない。

## 仕様

### ホバー時

- ◆ にマウスが重なっているとき、ラベルを隣に描画する
- 表示形式: `F:N  V:X.X`（コンパクトに）または `Frame:N / Val:X.X`
- ラベルはキーの右側に出す。右端に近い場合は左側にフォールバック

### ドラッグ中

- キーをドラッグ移動しているとき、移動先のフレーム番号をキーの真上（またはスクラバーのフレーム番号フィールド）で強調表示する

## 実装: ホバーラベル

**ファイル**: `UI/AnimationTimelineUI.Tracks.cs`  
`DrawTrackRow()` 内のキー描画ループを拡張する。

### ホバー判定と描画

`EventType.Repaint` のブロックをマウス位置に基づいて分岐させる。
IMGUI では `Repaint` イベント内でしか描画できないため、前フレームのマウス位置（`Event.current.mousePosition`）を使う。

```csharp
// DrawTrackRow の既存キー描画ループ内（◆ ラベル描画の直後）に追加

foreach (float kTime in keyTimes)
{
    float kx = TimeToPixel(kTime, clipModel.ClipLength, trackX, trackW);  // P4 実装後
    float ky = rowRect.y + rowRect.height * 0.5f;

    Rect hitR = new Rect(kx - DIAMOND_SIZE - 2, ky - DIAMOND_SIZE - 2,
                         (DIAMOND_SIZE + 2) * 2, (DIAMOND_SIZE + 2) * 2);

    bool isCurrent = Mathf.Abs(kTime - clipModel.CurrentTime)
                     <= (clipModel.FPS > 0f ? 0.5f / clipModel.FPS : 0.01f);

    if (Event.current.type == EventType.Repaint)
    {
        // ◆ 描画（既存）
        var style = new GUIStyle(_kfLabelStyle) { ... };
        GUI.Label(new Rect(kx - 8, ky - 8, 16, 16), "◆", style);

        // ─── ホバーラベル ──────────────────────────────────────────────────
        bool isHovered = hitR.Contains(Event.current.mousePosition);
        if (isHovered)
        {
            int   frameNum = Mathf.RoundToInt(kTime * clipModel.FPS);
            float val      = clipModel.GetShapeKeyValue(shapeName, kTime);
            string tipText = $"F:{frameNum}  V:{val:F1}";

            EnsureHoverLabelStyle();
            Vector2 size = _hoverLabelStyle.CalcSize(new GUIContent(tipText));

            // 右側に余裕がある場合は右に、なければ左に表示
            float labelX = (kx + 10 + size.x < trackX + trackW)
                ? kx + 10
                : kx - 10 - size.x;
            float labelY = ky - size.y * 0.5f;

            Rect bgRect = new Rect(labelX - 3, labelY - 2, size.x + 6, size.y + 4);
            EditorGUI.DrawRect(bgRect, new Color(0.1f, 0.1f, 0.1f, 0.85f));
            GUI.Label(new Rect(labelX, labelY, size.x, size.y), tipText, _hoverLabelStyle);
        }
    }

    // ... 既存: カーソル変更、ドラッグ処理、右クリックメニュー
}
```

### スタイル

`AnimationTimelineUI.cs` のキャッシュフィールドに追加:

```csharp
private GUIStyle _hoverLabelStyle;

private void EnsureHoverLabelStyle()
{
    if (_hoverLabelStyle == null || _hoverLabelStyle.normal.background == null)
    {
        _hoverLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            normal = { textColor = Color.white },
            padding = new RectOffset(2, 2, 1, 1),
        };
    }
}
```

## 実装: ドラッグ中フレーム番号表示

`HandleKeyframeDrag()` でドラッグ中（`_isDraggingKeyframe == true`）のとき、
移動先フレームをスクラバー直下に表示する。

**ファイル**: `UI/AnimationTimelineUI.Scrubber.cs`  
`DrawScrubberLine()` に追加:

```csharp
private void DrawScrubberLine(Rect rect, AnimationClipModel clipModel)
{
    float trackW = rect.width - _trackLabelWidth - RIGHT_PADDING;
    float trackX = rect.x + _trackLabelWidth;
    float norm   = clipModel.ClipLength > 0f ? clipModel.CurrentTime / clipModel.ClipLength : 0f;
    norm = Mathf.Clamp01(norm);
    float sx = trackX + ((norm - _viewStart) / ViewRange) * trackW;  // P4 対応

    // 白ライン（既存）
    EditorGUI.DrawRect(new Rect(sx - 1, rect.y, 2, rect.height), new Color(1f, 1f, 1f, 0.8f));
    EditorGUI.DrawRect(new Rect(sx - 5, rect.y, 10, rect.height), DenEmoTheme.TextPrimary);

    // ─── ドラッグ中フレーム番号ラベル ─────────────────────────────────────
    if (_isDraggingKeyframe)
    {
        EnsureHoverLabelStyle();
        string frameText = clipModel.CurrentFrame.ToString();
        Vector2 fSize = _hoverLabelStyle.CalcSize(new GUIContent(frameText));
        float labelX = sx - fSize.x * 0.5f;
        float labelY = rect.y - fSize.y - 2;

        Rect bgRect = new Rect(labelX - 2, labelY - 1, fSize.x + 4, fSize.y + 2);
        EditorGUI.DrawRect(bgRect, new Color(0.2f, 0.5f, 1f, 0.9f));  // 青背景
        GUI.Label(new Rect(labelX, labelY, fSize.x, fSize.y), frameText, _hoverLabelStyle);
    }
}
```

`DrawScrubberLine()` は `DrawRulerAndScrubber()` から呼ばれており、
`EnsureHoverLabelStyle()` の呼び出し順に注意（`EnsureTimelineStyles()` の後に呼ぶ）。

## P4 との依存関係

ホバーラベルの `kx` 計算は P4 の `TimeToPixel()` を使うと実装がシンプルになる。
P4 未実装の場合は一時的に直接計算 (`trackX + (kTime / clipLen) * trackW`) を使用してもよい。

## テスト手順

1. クリップにキーを追加し、タイムライントラックの ◆ にマウスを乗せる
2. ラベル `F:N  V:X.X` が ◆ の隣に表示されることを確認
3. トラック右端の ◆ では左側に表示が出ることを確認（フォールバック）
4. ◆ をドラッグ中にスクラバー位置のフレーム番号が青背景で表示されることを確認
5. マウスを外すとラベルが消えることを確認
