# P2: キーボードショートカット実装

## 対象ショートカット

| キー | 動作 | Unity 標準との対応 |
|------|------|-------------------|
| `Space` | 再生 / 停止トグル | ✅ Unity 同等 |
| `←` | 1 フレーム前に移動 | ✅ Unity 同等 |
| `→` | 1 フレーム後に移動 | ✅ Unity 同等 |
| `,` | 前のキーフレームへ移動 | ✅ Unity 同等 |
| `.` | 次のキーフレームへ移動 | ✅ Unity 同等 |
| `Delete` / `Backspace` | 現在フレームの全キーを削除 | ✅ Unity 同等（選択なしの場合は現在フレーム） |

`Ctrl+A`（全選択）は選択機能（P5 以降）の実装後に追加する。

## 実装方針

### 処理の場所

`AnimationTimelineUI.Draw()` の冒頭（`clipModel?.Clip == null` チェックの直後）でキーボードイベントを処理する。

タイムライン UI の内側で処理することで:
- クリップが未設定のときは無視される
- タイムライン別ウィンドウ (`DenEmoTimelineWindow`) でも自動的に動作する
- `DenEmoWindow.OnGUI` には変更が不要

### IMGUI のキーイベント取り扱い

IMGUI では任意のコントロール（TextField 等）がキーボードフォーカスを持っているとき、
`EventType.KeyDown` がそのコントロールに優先して渡される。
フォーカスを外してから判定するか、`GUI.GetNameOfFocusedControl()` で確認して処理を分岐する。

再生/停止・フレーム移動は操作中に TextField へ誤入力されては困るため、
**TextField にフォーカスがない場合にのみ** ショートカットを有効にする。

```csharp
bool noTextFieldFocused = string.IsNullOrEmpty(GUI.GetNameOfFocusedControl());
```

## 実装詳細

### 新規メソッド追加先

**ファイル**: `UI/AnimationTimelineUI.Controls.cs`  
**クラス**: `AnimationTimelineUI` (partial)

```csharp
// AnimationTimelineUI.Controls.cs に追加

private void HandleKeyboardInput(
    AnimationClipModel clipModel,
    AnimationPreviewController preview,
    string smrPath,
    ref bool isPlaying,
    ref double playStartRealTime,
    ref float playStartClipTime,
    EditorWindow window)
{
    Event e = Event.current;
    if (e.type != EventType.KeyDown) return;
    if (!string.IsNullOrEmpty(GUI.GetNameOfFocusedControl())) return;

    float tol = clipModel.FPS > 0f ? 0.5f / clipModel.FPS : 0.01f;

    switch (e.keyCode)
    {
        case KeyCode.Space:
            isPlaying = !isPlaying;
            if (isPlaying)
            {
                playStartRealTime = EditorApplication.timeSinceStartup;
                playStartClipTime = clipModel.CurrentTime;
            }
            e.Use();
            window.Repaint();
            break;

        case KeyCode.LeftArrow:
            isPlaying = false;
            clipModel.CurrentTime = Mathf.Max(0f, clipModel.CurrentTime - 1f / clipModel.FPS);
            preview.SampleAt(clipModel.CurrentTime);
            e.Use();
            window.Repaint();
            break;

        case KeyCode.RightArrow:
            isPlaying = false;
            clipModel.CurrentTime = Mathf.Min(clipModel.ClipLength, clipModel.CurrentTime + 1f / clipModel.FPS);
            preview.SampleAt(clipModel.CurrentTime);
            e.Use();
            window.Repaint();
            break;

        case KeyCode.Comma:
        {
            isPlaying = false;
            float[] allKeys = clipModel.GetAllKeyTimes(smrPath);
            float prev = -1f;
            foreach (float kt in allKeys)
                if (kt < clipModel.CurrentTime - tol) prev = kt;
            if (prev >= 0f) { clipModel.CurrentTime = prev; preview.SampleAt(prev); }
            e.Use();
            window.Repaint();
            break;
        }

        case KeyCode.Period:
        {
            isPlaying = false;
            float[] allKeys = clipModel.GetAllKeyTimes(smrPath);
            float next = -1f;
            foreach (float kt in allKeys)
                if (kt > clipModel.CurrentTime + tol) { next = kt; break; }
            if (next >= 0f) { clipModel.CurrentTime = next; preview.SampleAt(next); }
            e.Use();
            window.Repaint();
            break;
        }

        case KeyCode.Delete:
        case KeyCode.Backspace:
        {
            // 現在フレームに存在するキーを全トラックで削除
            float[] keysAtTime = clipModel.GetAllKeyTimes(smrPath);
            float match = -1f;
            foreach (float kt in keysAtTime)
                if (Mathf.Abs(kt - clipModel.CurrentTime) <= tol) { match = kt; break; }
            if (match >= 0f)
            {
                // 確認ダイアログは表示しない（キーボード操作は素早い操作を前提とする）
                // Undo は AnimationPreviewController.DeleteAllKeyframesAtTime 内で処理される
                preview.DeleteAllKeyframesAtTime(smrPath, match);
                preview.SampleAt(clipModel.CurrentTime);
                e.Use();
                window.Repaint();
            }
            break;
        }
    }
}
```

### Draw() への組み込み

**ファイル**: `UI/AnimationTimelineUI.cs`  
**メソッド**: `Draw()` (現在 66–130 行)

`if (clipModel?.Clip == null) return;` の直後に呼び出しを追加する。

```csharp
public void Draw(
    AnimationClipModel clipModel, ..., ref bool isPlaying,
    ref double playStartRealTime, ref float playStartClipTime, ...)
{
    if (clipModel?.Clip == null) return;
    DenEmoTheme.Initialize();

    // ── キーボード処理（クリップ有効時のみ）
    HandleKeyboardInput(
        clipModel, preview, smrPath,
        ref isPlaying, ref playStartRealTime, ref playStartClipTime, window);

    // ... 以降は既存のレイアウト描画
```

## ローカライズキーの追加は不要

ツールチップや UI ラベルを追加しないため、ローカライズキーの追加は不要。

## テスト手順

1. Multi Frame モードでクリップを開き、キーフレームをいくつか追加する
2. タイムライン上をクリックしてウィンドウにフォーカスを当てる（TextField にフォーカスがない状態を確認）
3. Space → 再生開始、もう一度 Space → 停止することを確認
4. ←/→ → 1 フレーム移動することを確認
5. `,`/`.` → 前後のキーフレームにジャンプすることを確認
6. あるキーフレームのあるフレームにスクラバーを合わせ Delete → キーが削除されることを確認
7. TextField（フレーム番号入力）にフォーカスを当てた状態で ← を押しても移動しないことを確認

## 注意事項

- `Delete` / `Backspace` は確認ダイアログを出さない（Undo で元に戻せるため）。トラック削除ボタン（✕）との一貫性を保つため将来的に合わせることを検討する。
- Unity の IMGUI では `EventType.KeyDown` が発生するのは EditorWindow が表示されていてかつウィンドウ全体にフォーカスがある場合。別ウィンドウ（`DenEmoTimelineWindow`）の場合も `Draw()` が呼ばれる構造のため同様に動作する。
