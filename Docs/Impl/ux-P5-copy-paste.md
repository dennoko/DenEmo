# P5: キーフレームのコピー＆ペースト

## 前提

本機能は P2 キーボードショートカット（`Ctrl+C` / `Ctrl+V` のキー処理）が実装済みであることを前提とする。

## 仕様

### コピー（Ctrl+C）

- **対象**: 現在スクラバーがある時刻と同フレームに存在する **全トラックのキーフレーム**
- **コピーされる情報**: シェイプ名・値・補間タイプ（時刻は相対的に扱う）
- クリップボードは DenEmo 独自の内部クリップボード（OS クリップボードへの保存は行わない）

### ペースト（Ctrl+V）

- **対象**: 現在スクラバーがある時刻に、コピーしたキーを書き込む
- コピー元と同じ相対位置で貼り付ける（単一フレームコピーなので相対は 0）
- 既存キーがある場合は上書き（`WriteSingleKey` の既存ロジックを流用）
- 貼り付け後、現在フレームのプレビューを更新する

## データ構造

`AnimationTimelineUI` にコピーバッファを追加する。

**ファイル**: `UI/AnimationTimelineUI.cs`

```csharp
// ─── Copy/paste clipboard ─────────────────────────────────────────────────
private struct KeyClipboardEntry
{
    public string             ShapeName;
    public float              Value;
    public InterpolationType  Interp;
}

private List<KeyClipboardEntry> _keyClipboard = new List<KeyClipboardEntry>();
private bool _hasClipboardData => _keyClipboard != null && _keyClipboard.Count > 0;
```

コピー元フレームの絶対時刻は保持しない。「現在時刻からの相対時刻」で持つ設計にすることで、
将来的に複数フレームにわたるコピーに拡張しやすくする。

```csharp
private struct KeyClipboardEntry
{
    public string             ShapeName;
    public float              RelativeTime;  // コピー時の先頭フレームからのオフセット (秒)
    public float              Value;
    public InterpolationType  Interp;
}
```

単一フレームのコピーでは `RelativeTime = 0` となる。

## コピー処理

**ファイル**: `UI/AnimationTimelineUI.Controls.cs`  
`HandleKeyboardInput()` 内に `Ctrl+C` の処理を追加する（P2 で追加したメソッドを拡張）。

```csharp
case KeyCode.C when e.control:
{
    _keyClipboard.Clear();
    float tol = clipModel.FPS > 0f ? 0.5f / clipModel.FPS : 0.01f;
    var shapes = clipModel.GetShapeNamesWithKeys(smrPath);

    foreach (string shapeName in shapes)
    {
        float[] times = clipModel.GetKeyTimesForShape(shapeName, smrPath);
        foreach (float kt in times)
        {
            if (Mathf.Abs(kt - clipModel.CurrentTime) > tol) continue;

            // 補間タイプを取得（クリップのカーブから読み取る）
            var interp = GetKeyInterpolationType(shapeName, smrPath, kt, clipModel);
            float value = clipModel.GetShapeKeyValue(shapeName, kt);

            _keyClipboard.Add(new KeyClipboardEntry
            {
                ShapeName    = shapeName,
                RelativeTime = 0f,
                Value        = value,
                Interp       = interp,
            });
        }
    }

    e.Use();
    // ステータス表示（省略可、あると親切）
    break;
}
```

### 補間タイプ取得ヘルパー

`AnimationClipModel` には現在補間タイプを取得するメソッドがないため追加する。

**ファイル**: `Models/AnimationClipModel.cs`

```csharp
/// <summary>指定シェイプ・時刻のキーの補間タイプを返す。見つからない場合は Ease。</summary>
public InterpolationType GetKeyInterpolationType(string shapeName, float time, string smrPath = null)
{
    if (Clip == null) return InterpolationType.Ease;
    float tol = FPS > 0f ? 0.5f / FPS : 0.01f;
    string propName = "blendShape." + shapeName;
    foreach (var b in AnimationUtility.GetCurveBindings(Clip))
    {
        if (b.type != typeof(SkinnedMeshRenderer) || b.propertyName != propName) continue;
        if (smrPath != null && b.path != smrPath) continue;
        var curve = AnimationUtility.GetEditorCurve(Clip, b);
        if (curve == null) continue;
        for (int i = 0; i < curve.keys.Length; i++)
        {
            if (Mathf.Abs(curve.keys[i].time - time) > tol) continue;
            var lm = AnimationUtility.GetKeyLeftTangentMode(curve, i);
            if (lm == AnimationUtility.TangentMode.Constant) return InterpolationType.Step;
            if (lm == AnimationUtility.TangentMode.Linear)   return InterpolationType.Linear;
            return InterpolationType.Ease;
        }
    }
    return InterpolationType.Ease;
}
```

`HandleKeyboardInput` の `smrPath` 引数を通じてアクセスできるよう `clipModel` の `GetKeyInterpolationType()` を呼ぶ。

## ペースト処理

**ファイル**: `UI/AnimationTimelineUI.Controls.cs`  
`HandleKeyboardInput()` に `Ctrl+V` の処理を追加。

```csharp
case KeyCode.V when e.control:
{
    if (!_hasClipboardData) break;

    foreach (var entry in _keyClipboard)
    {
        float pasteTime = clipModel.CurrentTime + entry.RelativeTime;
        pasteTime = Mathf.Clamp(pasteTime, 0f, clipModel.ClipLength);
        pasteTime = Mathf.Round(pasteTime * clipModel.FPS) / clipModel.FPS;

        preview.RecordKeyframe(entry.ShapeName, smrPath, pasteTime, entry.Value, entry.Interp);
    }

    preview.SampleAt(clipModel.CurrentTime);
    e.Use();
    window.Repaint();
    break;
}
```

## P2 の `HandleKeyboardInput` シグネチャ拡張

P2 で追加した `HandleKeyboardInput` の引数に以下を追加する:

```csharp
private void HandleKeyboardInput(
    AnimationClipModel clipModel,
    AnimationPreviewController preview,
    string smrPath,
    InterpolationType currentInterp,   // ← Ctrl+V で補間タイプが必要な場合に使用（今回は entry.Interp を使うため不要だが将来のため）
    ref bool isPlaying,
    ref double playStartRealTime,
    ref float playStartClipTime,
    EditorWindow window)
```

`Draw()` からの呼び出しも更新する。

## ビジュアルフィードバック（任意）

コピー後にステータスバーに `"{N} キーをコピーしました"` を表示すると分かりやすい。
ただし `AnimationTimelineUI` は `DenEmoWindow.SetStatus()` に直接アクセスできないため、
`Draw()` に `Action<string, int> setStatus = null` 引数を追加するか、
コピー後は無音で処理する（Ctrl+Z で確認できるため支障なし）。

## 注意事項

- `_keyClipboard` はウィンドウのドメインリロード後に消える（`[SerializeField]` などで永続化しない）。シンプルに揮発性クリップボードとして扱う。
- 別ウィンドウ（`DenEmoTimelineWindow`）でコピーして `DenEmoWindow` でペーストする操作は、両方が同じ `AnimationTimelineUI` インスタンスを参照しているため自動的に動作する。
- `GetShapeKeyValue(shapeName, kt)` は `AnimationClipModel` に既存のメソッド。

## テスト手順

1. フレーム 5 にいくつかのシェイプのキーを記録する
2. スクラバーをフレーム 5 に合わせ、Ctrl+C
3. スクラバーをフレーム 20 に移動し、Ctrl+V
4. フレーム 20 にフレーム 5 と同じシェイプ・値のキーが追加されることを確認
5. タイムライントラックに ◆ が表示されることを確認
6. Ctrl+Z → ペーストが取り消されることを確認
7. フレームにキーが存在しない場所で Ctrl+C → 何もコピーされない（ペースト後に変化なし）ことを確認
