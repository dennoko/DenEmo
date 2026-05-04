# P4: タイムラインのズーム＆スクロール

## 問題

現在のタイムラインは常に「クリップ全体を全幅に収める」固定スケール。
FPS=60・長さ3秒のクリップでは 180 フレームが狭い幅に並び、隣接するキーを個別に操作できない。

Unity の Animation ウィンドウではスクロールホイールでX軸をズームイン/アウトし、
ズーム後はスクロールバーで表示範囲を移動できる。

## 仕様

- スクロールホイール（タイムライン上で）: ズームイン/アウト
- ズーム倍率: 1.0（全体表示）〜 50.0
- ズーム中心: マウスカーソルの位置（カーソル下の時刻が固定される）
- スクロールバー: タイムライン下部に水平スクロールバーを追加
- タイムラインには常にスクラバー（現在フレーム）が表示される範囲が保持される
- ズーム 1.0 のときはスクロールバーを非表示にする

## 状態変数

**ファイル**: `UI/AnimationTimelineUI.cs`  
`AnimationTimelineUI` クラスの内部状態として追加。

```csharp
// ─── View (zoom & scroll) state ──────────────────────────────────────────
private float _viewStart = 0f;   // 表示開始位置 (0.0 – 1.0 正規化)
private float _viewEnd   = 1f;   // 表示終了位置 (0.0 – 1.0 正規化)

private float ViewRange => Mathf.Max(0.01f, _viewEnd - _viewStart);
```

## 座標変換ヘルパー

タイムライン全体で共通して使う変換式を内部ヘルパーにまとめる。
（現在は各メソッドに `float norm = kTime / clipLength` がバラバラに書かれている）

**ファイル**: `UI/AnimationTimelineUI.cs`

```csharp
/// <summary>クリップ上の時刻 → ピクセルX座標に変換（ズーム考慮）。</summary>
private float TimeToPixel(float time, float clipLen, float trackX, float trackW)
{
    if (clipLen <= 0f) return trackX;
    float norm = time / clipLen;
    float visible = (norm - _viewStart) / ViewRange;
    return trackX + visible * trackW;
}

/// <summary>ピクセルX座標 → クリップ上の時刻に変換（ズーム考慮）。</summary>
private float PixelToTime(float pixelX, float clipLen, float trackX, float trackW)
{
    if (trackW <= 0f || clipLen <= 0f) return 0f;
    float visible = (pixelX - trackX) / trackW;
    float norm = _viewStart + visible * ViewRange;
    return Mathf.Clamp(norm, 0f, 1f) * clipLen;
}
```

## ホイールズーム処理

**ファイル**: `UI/AnimationTimelineUI.Scrubber.cs`  
新規メソッド `HandleTimelineZoom()` を追加し、`DrawRulerAndScrubber()` から呼ぶ。

```csharp
private void HandleTimelineZoom(Rect trackRect, float clipLen, float trackX, float trackW)
{
    Event e = Event.current;
    if (e.type != EventType.ScrollWheel) return;
    if (!trackRect.Contains(e.mousePosition)) return;

    // カーソル位置の正規化時刻を固定点にしてズーム
    float mouseNorm = _viewStart + ((e.mousePosition.x - trackX) / trackW) * ViewRange;
    mouseNorm = Mathf.Clamp01(mouseNorm);

    float zoomFactor = e.delta.y > 0f ? 1.15f : (1f / 1.15f);
    float newRange = Mathf.Clamp(ViewRange * zoomFactor, 1f / 50f, 1f);

    _viewStart = mouseNorm - (mouseNorm - _viewStart) * (newRange / ViewRange);
    _viewEnd   = _viewStart + newRange;

    // 範囲を [0, 1] に収める
    if (_viewStart < 0f) { _viewEnd -= _viewStart; _viewStart = 0f; }
    if (_viewEnd   > 1f) { _viewStart -= (_viewEnd - 1f); _viewEnd = 1f; }
    _viewStart = Mathf.Clamp01(_viewStart);
    _viewEnd   = Mathf.Clamp01(_viewEnd);

    e.Use();
}
```

### `DrawRulerAndScrubber()` への追加

```csharp
private void DrawRulerAndScrubber(AnimationClipModel clipModel, AnimationPreviewController preview, EditorWindow window)
{
    Rect rulerRect = GUILayoutUtility.GetRect(0, RULER_HEIGHT, GUILayout.ExpandWidth(true));
    Rect scrubRect = GUILayoutUtility.GetRect(0, SCRUBBER_HEIGHT, GUILayout.ExpandWidth(true));

    float trackW = rulerRect.width - _trackLabelWidth - RIGHT_PADDING;
    float trackX = rulerRect.x + _trackLabelWidth;
    Rect trackArea = new Rect(trackX, rulerRect.y, trackW, rulerRect.height + scrubRect.height);

    // ズーム処理
    HandleTimelineZoom(trackArea, clipModel.ClipLength, trackX, trackW);

    // スクロールバー（ズーム < 全体表示の場合のみ）
    if (ViewRange < 1f - 0.001f)
        DrawTimelineScrollbar(clipModel.ClipLength, trackX, trackW, window);

    // ... 既存の Repaint 描画、スクラバー入力処理
```

## スクロールバー

**ファイル**: `UI/AnimationTimelineUI.Scrubber.cs`  
新規メソッド `DrawTimelineScrollbar()`

```csharp
private const float SCROLLBAR_HEIGHT = 10f;

private void DrawTimelineScrollbar(float clipLen, float trackX, float trackW, EditorWindow window)
{
    Rect sbRect = GUILayoutUtility.GetRect(0, SCROLLBAR_HEIGHT, GUILayout.ExpandWidth(true));
    Rect trackSbRect = new Rect(trackX, sbRect.y, trackW, SCROLLBAR_HEIGHT);

    float newStart = GUI.HorizontalScrollbar(
        trackSbRect,
        _viewStart,
        ViewRange,   // サムのサイズ
        0f,
        1f);

    if (!Mathf.Approximately(newStart, _viewStart))
    {
        float range = ViewRange;
        _viewStart = Mathf.Clamp01(newStart);
        _viewEnd   = Mathf.Clamp01(_viewStart + range);
        window.Repaint();
    }
}
```

## 既存コードの座標計算の置き換え

以下 3 ファイルにある `norm = kTime / clipModel.ClipLength` + ピクセル変換を、
`TimeToPixel()` / `PixelToTime()` に置き換える。

### `AnimationTimelineUI.Scrubber.cs`

**`SeekFromMouseX()`**:
```csharp
// 変更前
private static void SeekFromMouseX(...)
{
    float t = Mathf.Clamp01((mouseX - trackX) / trackW) * clipModel.ClipLength;
    t = Mathf.Round(t * clipModel.FPS) / clipModel.FPS;
    ...
}

// 変更後（static → インスタンスメソッドに変更が必要）
private void SeekFromMouseX(...)
{
    float t = PixelToTime(mouseX, clipModel.ClipLength, trackX, trackW);
    t = Mathf.Round(t * clipModel.FPS) / clipModel.FPS;
    ...
}
```

**`DrawRulerTicks()`**:
ルーラー目盛りの描画も `_viewStart` / `_viewEnd` に基づいて表示範囲のフレームだけ描画する。

```csharp
// 変更後
int frameStart = Mathf.FloorToInt(_viewStart * total);
int frameEnd   = Mathf.CeilToInt(_viewEnd   * total);
for (int f = frameStart; f <= frameEnd; f += step)
{
    float x = TimeToPixel((float)f / clipModel.FPS, clipModel.ClipLength, trackX, trackW);
    if (x < trackX || x > trackX + trackW) continue;
    // ... ティック描画
}
```

### `AnimationTimelineUI.Tracks.cs`

**`DrawTrackRow()` 内のキー描画**:
```csharp
// 変更前
float norm = clipModel.ClipLength > 0f ? kTime / clipModel.ClipLength : 0f;
float kx = trackX + norm * trackW;

// 変更後
float kx = TimeToPixel(kTime, clipModel.ClipLength, trackX, trackW);
if (kx < trackX - DIAMOND_SIZE || kx > trackX + trackW + DIAMOND_SIZE) continue; // 範囲外スキップ
```

スクラバーライン（現在フレームの白線）も同様に `TimeToPixel()` で計算する。

**`HandleKeyframeDrag()` のドラッグ先計算**:
```csharp
// 変更前
float norm = Mathf.Clamp01((mouseX - trackX) / trackW);
int targetFrame = Mathf.RoundToInt(norm * clipModel.ClipLength * clipModel.FPS);

// 変更後
float t = PixelToTime(mouseX, clipModel.ClipLength, trackX, trackW);
int targetFrame = Mathf.RoundToInt(Mathf.Clamp(t * clipModel.FPS, 0f, clipModel.TotalFrames));
```

## `SeekFromMouseX` の static → instance 変更

`SeekFromMouseX` は現在 `static` だが、`PixelToTime()` がインスタンスフィールド (`_viewStart` など) を使うため **インスタンスメソッドに変更**が必要。

`HandleScrubberInput()` の呼び出し元が `SeekFromMouseX(e.mousePosition.x, ...)` なので引数シグネチャは変えない。

## スクラバー追従（自動スクロール）

再生中またはキーボードで現在フレームを移動したとき、スクラバーが見えている範囲外に出た場合は表示範囲をシフトする。

**ファイル**: `UI/AnimationTimelineUI.cs`  
`Draw()` の playback 進行後（`OnUpdate` 内ではなく `Draw` 内で行う）:

```csharp
private void EnsureScrubberVisible(AnimationClipModel clipModel, EditorWindow window)
{
    if (clipModel.ClipLength <= 0f) return;
    float norm = clipModel.CurrentTime / clipModel.ClipLength;
    if (norm < _viewStart || norm > _viewEnd)
    {
        float halfRange = ViewRange * 0.5f;
        _viewStart = Mathf.Clamp01(norm - halfRange);
        _viewEnd   = Mathf.Clamp01(_viewStart + ViewRange);
        window.Repaint();
    }
}
```

## RULER_HEIGHT の調整

スクロールバーが追加されるため、タイムライン全体の高さが `SCROLLBAR_HEIGHT` 分増える。
`Draw()` 内の `EditorGUILayout.BeginVertical("box")` 内に収まるため、外側のレイアウトへの影響は最小限。

## テスト手順

1. 長いクリップ（例: 60fps × 3s）を作成してキーを複数追加する
2. タイムライン上でスクロールホイールを回す → ズームイン/アウトすることを確認
3. ズームイン後、スクロールバーで左右移動できることを確認
4. ズーム 1.0（全体表示）時にスクロールバーが非表示になることを確認
5. キーボード ←/→ でフレームを移動したとき、スクラバーが見えない範囲にいったらビューが追従することを確認
6. ドラッグでキーを移動できることを確認（ズーム後も正しいフレームに移動する）
7. スクラバークリックで任意のフレームに移動できることを確認（ズーム後も正しい時刻になる）
