# 内部ロジック パフォーマンスボトルネック調査レポート

作成日: 2026-07-07
対象: DenEmo 内部ロジック（Core / Models / ウィンドウ駆動部）
前提: UI は USS/UI Toolkit 移行で大きく変わるため、**描画コードそのものは対象外**とし、UI から呼ばれる側のロジック・毎フレーム/ポーリングで駆動されるロジック・重い一括処理を重点調査した。
関連: `multiframe-feature-ux-performance-report.md`（2026-06、A-1/A-4 対策は実装済み。本レポートはその後のコードベースに対する再調査）

想定ワークロード: VRChat アバター（頂点 3〜7 万、ブレンドシェイプ 100〜400、複数メッシュでアイテム総数 500+、クリップはトラック数十×キー数十）。

---

## 0. 現状すでに良好な設計（維持すべき）

改善提案の前に、既に効いている軽量化を確認した。USS 移行時にこれらを壊さないこと。

| 仕組み | 場所 |
|---|---|
| Revision ベースのトラックキャッシュ（`MarkDirty` → 次アクセス時に一度だけ再構築） | `AnimationClipModel.EnsureCache()` |
| 単一トラック変更の高速パス（全再構築を回避） | `AnimationClipModel.UpdateTrackCurve()` |
| プレビューのサンプル対象マップを Revision + Mesh でキャッシュ | `AnimationPreviewController.EnsureSampleTargets()` |
| スライダードラッグ中のキー記録スロットル（50ms）＋ Undo スナップショット 1 回化 | `AnimationModeUI.FlushPendingRecords()` |
| 再生サンプリングのクリップ FPS 上限スロットル | `AnimationPlayback.Tick()` |
| `SetEditorCurves` 複数形 API による一括コミット（2022+） | `AnimationClipEditor.SetEditorCurvesBatch()` |
| 行プランのシグネチャ比較による差分再構築 | `ShapeKeyListUI.TickStructureAndSync()` |
| FX ホバープレビューのクリップ単位ターゲットキャッシュ | `FxClipSampler.EnsureTargets()` |

---

## 1. 高優先度（体感フリーズ・秒単位の停止が起こり得る）

### H-1. 頂点フィルタ: 全ブレンドシェイプのデルタ配列コピー

**場所**: `ShapeKeyModel.CollectShapeIndicesMovingVertex()`（`Models/ShapeKeyModel.cs:151`）

頂点を 1 つピックするたびに、**全ブレンドシェイプ × 全フレーム**について
`mesh.GetBlendShapeFrameVertices()` を呼び、頂点数分の `Vector3[]` を **3 本（頂点・法線・接線）** ネイティブ→マネージドへフルコピーしている。

- 7 万頂点 × 200 シェイプの場合: 200 回 × 3 配列 × 70,000 × 12 byte ≈ **約 500 MB のメモリコピー**が同期実行され、UI が数秒止まる。
- 使うのは `deltaVertices[vertexIndex]` の **1 要素だけ**なのに全配列を取得している。
- さらに `DenEmoWindow.RefreshListAndCache()`（`DenEmoWindow.cs:719`）はフィルタ有効中の再読込のたびにこれを再実行する。同じメッシュ・同じ頂点でも毎回フルスキャンになる。

**改善案**（効果順）:
1. **メッシュ単位の「シェイプ→移動頂点マスク」キャッシュ**を作る。初回に全シェイプのデルタを 1 回だけ走査し、`shapeIndex → BitArray(vertexCount)`（しきい値超過フラグ）を `Mesh.GetInstanceID()` をキーに保持する。2 回目以降のピックは O(shapeCount) のビット参照だけになり瞬時。メモリは 200 シェイプ × 70k 頂点で約 1.7 MB と軽い。
2. 法線・接線バッファの取得をやめる（判定は頂点デルタのみ）。`GetBlendShapeFrameVertices` は `deltaNormals` / `deltaTangents` に null を渡せる。これだけでコピー量が 1/3 になる（Unity バージョンによる null 許容は要動作確認。不可なら共有バッファ維持）。
3. 初回スキャンに `EditorUtility.DisplayProgressBar` を出す（軽量化ではないが体感改善）。

### H-2. 頂点ピックモード: SceneGUI での全頂点ループと再ベイク

**場所**: `DenEmoWindow.VertexFilter.cs:39-70`（描画/ピック）, `UpdateVertexGuideCache()`（同 `:101`）

これは Scene ビュー側のロジックであり USS 移行の影響を受けないため、内部ロジックとして扱う。

1. **全頂点ループが SceneGUI の全イベントで走る**: `OnSceneGUI` は Layout / MouseMove / Repaint 等のイベントごとに呼ばれ、そのたびに全頂点（数万）について `HandleUtility.GetHandleSize()`（カメラ行列演算）＋ `Handles.Button()`（個別のコントロール ID 登録＋ヒットテスト）を実行している。7 万頂点では 1 イベントあたり数十 ms 級で、ピックモード中の Scene ビューが著しく重くなる。
2. **キャッシュ無効化条件が厳しすぎる**: `vertexGuideLocalToWorld != localToWorldMat` は行列の完全一致比較のため、カメラ操作は無関係だが、アバターがアニメーション再生・物理などでわずかでも動くと毎フレーム `BakeMesh()`（フルスキニング CPU ベイク）＋全頂点の `TransformPoint` マネージドループが再実行される。

**改善案**:
1. 描画は `Handles.Button` の全頂点ループをやめ、Repaint 時のみ `GL.Begin(GL.POINTS)`（または点群用の一時 Mesh 1 個）で一括描画する。ピックは MouseDown 時のみ「マウス位置とスクリーン座標の最近傍頂点」を 1 パスで探す（`HandleUtility.WorldToGUIPoint` を全頂点に回すのは同じだが、**MouseDown の 1 イベントだけ**になる）。さらに絞るならカメラレイと BakeMesh 結果への `Physics.Raycast` 相当（MeshCollider 一時生成 or 自前三角形交差）でヒット三角形→最寄り頂点にする。
2. 行列比較を `Matrix4x4` の成分差の許容誤差付き比較にし、かつ再ベイクは「最後のベイクから一定時間（例 100ms）」のスロットルを入れる。
3. 背面カリング（法線内積）は Repaint 時のみ行う（現在は全イベントで実行）。

### H-3. キードラッグ移動: マウスイベントごとの全トラック走査＋逐次コミット

**場所**: `AnimationClipEditor.MoveKeys()`（`Core/AnimationClipEditor.cs:188`）

タイムライン上のキーをドラッグすると、マウスドラッグイベントごとに:

1. **全トラックについて `track.Curve.keys` を取得** — `AnimationCurve.keys` は呼ぶたびにキー配列全体をネイティブからコピーする（後述 §4）。ブロッキング限界の計算だけならキャッシュ済みの `track.KeyTimes`（`float[]`）で足りる。
2. `var k = track.Curve.keys[keyIndex]`（`:231`）— **1 要素を読むために配列全体を再コピー**。
3. 移動対象トラックごとに `AnimationUtility.SetEditorCurve()` を**個別に**呼ぶ（`:234`）。「全トラック同時移動」では T 回のクリップ内部再構築が 1 マウスイベント内で発生する。`SetEditorCurvesBatch` が使われていない唯一の複数トラック変更パス。
4. `UpdateTrackCurve()` が移動トラックごとに `RebuildAllKeyTimes()`（SortedSet 全構築）を呼ぶ — ループ内で T 回。

**改善案**:
- 限界計算を `track.KeyTimes` ベースに変更（`curve.keys` 取得を全廃）。
- 移動対象のカーブ変更を集めて `SetEditorCurvesBatch` で 1 回コミット。
- `RebuildAllKeyTimes` はループ後に 1 回だけ呼ぶ（`UpdateTrackCurve` に「AllKeyTimes 再構築を遅延する」オーバーロードを追加するか、MoveKeys 専用のバッチ更新メソッドを `AnimationClipModel` に追加）。

---

## 2. 中優先度（操作中のカクつき・定常負荷）

### M-1. 再生中の `SyncValuesFromMesh` が全アイテム走査

**場所**: `ShapeKeyModel.SyncValuesFromMesh(HashSet<string>)`（`Models/ShapeKeyModel.cs:321`）、呼び出し元 `AnimationPreviewController.SampleAt()`（`Core/AnimationPreviewController.cs:74`）

再生中はクリップ FPS（最大 60 回/秒）で `SampleAt` が呼ばれ、そのたびに **Items 全件**（複数メッシュで 500+）をループして名前の `HashSet.Contains`（文字列ハッシュ計算）で絞り込んでいる。トラックが 10 本でも 500 件走査 × 60 Hz。

**改善案**: `EnsureSampleTargets()` でマップを再構築するタイミングで、`_sampleNames` に一致する `ShapeKeyItem` の参照リストも一緒に作り、`SampleAt` では「対象アイテムだけ」を直接ループして `item.Value = smr.GetBlendShapeWeight(...)` する（`ShapeKeyModel` に `SyncValues(IReadOnlyList<ShapeKeyItem>)` を追加）。アイテムリスト再構築（`RefreshList`）で参照が無効になるため、`ShapeKeyModel` 側にリスト世代カウンタを 1 つ持たせて突き合わせる。

### M-2. `RebuildAllKeyTimes` の SortedSet 全構築がドラッグ記録のたびに走る

**場所**: `AnimationClipModel.RebuildAllKeyTimes()`（`Models/AnimationClipModel.cs:133`）

`UpdateTrackCurve()`（＝REC ドラッグの 50ms フラッシュごと、キー移動ごと）に毎回 `SortedSet<float>` を new して**全トラックの全キー**を挿入し直す。50 トラック × 30 キーなら 1 フラッシュあたり 1,500 挿入＋アロケーション。

**改善案**: 変更されたのは 1 トラックだけなので、「旧 KeyTimes を除いた残り」と「新 KeyTimes」の**ソート済み配列マージ**にする。もしくは `AllKeyTimes` にも dirty フラグを持たせ、実際に読まれる時（スクラバー描画・Prev/Next キー移動）まで再構築を遅延する。後者が実装コスト最小。

### M-3. `FlushPendingRecords` が シェイプごとに個別コミット

**場所**: `AnimationModeUI.FlushPendingRecords()`（`UI/AnimationModeUI.cs:174`）→ `AnimationClipEditor.RecordKey()`

フラッシュ 1 回で `_pendingRecords` の各シェイプについて `RecordKey` → `SetEditorCurve`（クリップ内部再構築）が**個別に**走る。左右同期（Merged 行）のドラッグでは常に 2 シェイプ、REC 中に複数スライダーを続けて触ると数シェイプ分が 50ms ごとに逐次コミットされる。

**改善案**: `AnimationClipEditor` に `RecordKeysThrottled(IEnumerable<(name, value)>, time, interp, recordUndo)` のようなバッチ版を追加し（既存 `RecordKeys` とほぼ同一、Undo 制御引数付き）、フラッシュを 1 回の `SetEditorCurvesBatch` にまとめる。

### M-4. `SymmetryParser.TryParseLRSuffix` の繰り返し呼び出し

**場所**: `Core/SymmetryParser.cs:15`、呼び出し元 `ShapeKeyModel.UpdateVisibility()`（アイテムごとに 2 回）、`ShapeKeyListUI.BuildRowPlan()`（**150ms ポーリングごと**にアイテムごと 1 回）

1 回のパースで最大 30 通り超の `EndsWith` を試す。シェイプ名は不変なのに、左右同期モードでは 150ms ごとに全可視アイテム分を再パースし続ける。500 アイテムなら毎秒 10 万回超の文字列比較が**アイドル中も**走る。

**改善案**: パース結果（baseName, side）を `static Dictionary<string, (string, LRSide)>` でメモ化する。シェイプ名の総数は高々数百なのでメモリは無視できる。`GetGroupKey`（`ShapeKeyModel.cs:346`）も同様にメモ化可能だが、こちらは `BuildGroups` 時のみなので優先度低。

### M-5. リスト構造ポーリングが非表示中も全量実行される

**場所**: `ShapeKeyListUI.Bind()`（`UI/ShapeKeyListUI.cs:85-86`）の `schedule.Execute(...).Every(50/150)`

`TickStructureAndSync`（150ms）は、リストが FxSetup モード等で**非表示でも** `BuildRowPlan`（全アイテム走査＋左右ペアリング）＋`ComputePlanSignature` を回し続ける。`ApplyPending`（50ms）も空チェックのみとはいえ常駐する。ウィンドウを開いたまま放置しているだけで CPU を消費する構造。

**改善案**: ティック冒頭で `Root.resolvedStyle.display == DisplayStyle.None`（または `panel == null` / ホストの表示状態）なら即 return する。加えて「`MarkStructureDirty` されておらず、かつ Items の世代が変わっていなければ `BuildRowPlan` 自体をスキップ」できるよう、`ShapeKeyModel` に変更カウンタを持たせると、シグネチャ計算も不要になる（現在はプラン構築→ハッシュ→比較という「毎回作って捨てる」方式）。

### M-6. `FxLayerScanner` が同一クリップを参照回数分だけ解析

**場所**: `FxLayerScanner.CollectMotion()`（`Core/FxLayerScanner.cs:76`）

`TryAnalyzeClip()`（`GetCurveBindings` = バインディング配列のフルコピー）を `byClip` の重複チェック**より先に**呼んでいるため、N 個のステートから参照されるクリップは N 回解析される。VRChat の FX は同一表情クリップを多数のステートで使い回すのが普通なので、スキャン時間が参照数倍になる。

**改善案**: `byClip.TryGetValue` を先に行い、ヒットしたら Slot 追加のみで return する（3 行の入れ替え）。

---

## 3. 低優先度（マイクロ最適化・定常だが軽微）

| # | 内容 | 場所 | 改善案 |
|---|---|---|---|
| L-1 | `DenEmoProjectPrefs.ProjectKey` がアクセスごとに `Path.GetFullPath` ＋文字列連結。EditorPrefs は Windows ではレジストリアクセス | `Utils/DenEmoProjectPrefs.cs:8` | `ProjectKey` を `static readonly` 化（Application.dataPath はセッション中不変） |
| L-2 | `GetNDMFProxySMR` がキャッシュ再構築のたびにアセンブリ全走査＋リフレクション解決 | `DenEmoWindow.VertexFilter.cs:166` | `PropertyInfo`/`MethodInfo` を static Lazy でキャッシュ（NDMF 不在の結果も含めて） |
| L-3 | `HasKeyframeAt` / `FindTimeIndex` が線形探索（KeyTimes は昇順ソート済み） | `Models/AnimationClipModel.cs:313` | `Array.BinarySearch` ＋許容誤差チェックに置換。行数十×150ms ポーリングで呼ばれるが 1 回あたりは軽微 |
| L-4 | `MatchesAllTokens` がアイテムごとに `ToLowerInvariant()` を 1＋トークン数回アロケート | `Models/ShapeKeyModel.cs:391` | フィルタ変更時のみ実行なので実害は小さいが、`string.IndexOf(t, StringComparison.OrdinalIgnoreCase)` に置換すればアロケーションゼロ |
| L-5 | `BuildGroups` のソート比較子内で `ActiveMeshes.IndexOf` | `Models/ShapeKeyModel.cs:235` | 事前に `Dictionary<SMR,int>` を作る。リフレッシュ時のみなので軽微 |
| L-6 | `GetAllTargetMeshes` が呼び出しごとに List＋HashSet を new。250ms ポーリング（`UpdateAnimSectionsVisibility` 等）×2 系統から呼ばれる | `DenEmoWindow.cs:690` | ターゲット変更時のみ再構築するキャッシュに（`HasUsableTarget` は先頭要素チェックで足りる） |
| L-7 | `AnimationTimelineUI.cs:259` の `CopyFrame` が トラックごとに `GetKeyInterpolation` → `curve.keys` フルコピー | `UI/AnimationTimelineUI.cs:252` | 単発操作なので実害小。§4 の方針で `curve.length` / KeyTimes を使う |

---

## 4. 横断的な注意: `AnimationCurve.keys` はアクセスごとにフルコピー

`AnimationCurve.keys` ゲッターは呼ぶたびに**キー配列全体をネイティブ側からコピー**する。コードベースには次のパターンが散在している:

```csharp
if (curve == null || curve.keys.Length == 0) ...   // コピー1回目（長さを見るだけ）
var keys = curve.keys;                              // コピー2回目
```

該当箇所: `AnimationClipModel.UpdateTrackCurve()` / `EnsureCache()` / `FindKeyIndex()`、`AnimationClipEditor.DeleteKey()` / `DeleteKeysAfter()` / `SetAllKeysInterpolation()` / `MoveKeys()` など。

**方針**:
- 長さチェックは `curve.length`（コピーなし）を使う。
- 1 メソッド内では `var keys = curve.keys` を**一度だけ**取得してローカルで使い回す。
- 時刻だけが必要な箇所（`FindKeyIndex` の大半の呼び出し、MoveKeys の限界計算）は、キャッシュ済み `AnimationTrack.KeyTimes` を受け取るオーバーロードを用意して `curve.keys` を触らない。

これは個々には小さいが、ドラッグ中に毎イベント実行されるパス（H-3、M-3）では支配的なコストになる。

---

## 5. 推奨実施順序

| 順 | 項目 | 期待効果 | 実装規模 |
|---|---|---|---|
| 1 | H-1 頂点フィルタのマスクキャッシュ＋法線/接線取得廃止 | 秒単位フリーズ → 初回のみ・以降瞬時 | 中（新キャッシュクラス 1 つ） |
| 2 | H-3 MoveKeys の KeyTimes 化＋バッチコミット | キードラッグのカクつき解消 | 小〜中 |
| 3 | M-3 FlushPendingRecords のバッチ化 | REC ドラッグ（特に LR 同期）の滑らかさ | 小 |
| 4 | M-2 AllKeyTimes の遅延再構築 | 2・3 と相乗 | 小 |
| 5 | H-2 SceneGUI の点描画一括化＋ピック 1 パス化 | ピックモード中の Scene ビュー応答性 | 中 |
| 6 | M-4 SymmetryParser メモ化 ＋ M-5 非表示時ポーリング停止 | アイドル時 CPU 削減 | 小 |
| 7 | M-1 SyncValuesFromMesh の対象限定 | 再生中の定常負荷削減 | 小 |
| 8 | M-6 FxLayerScanner 重複解析排除 | FX タブ初期化の短縮 | 極小（3 行） |
| 9 | §4 curve.keys パターン一掃 ＋ L 群 | 全体の底上げ | 小の積み重ね |

USS 移行との関係: 上記はすべて `Core/`・`Models/`・SceneGUI・スケジューラ駆動ロジックに閉じており、IMGUI → USS の置き換えと競合しない。むしろ M-5（ポーリングの表示状態ガード）と M-1/M-4 は、USS 移行後もリスト同期の仕組み（`schedule.Execute` ポーリング）が残る前提なら先に入れておく価値が高い。
