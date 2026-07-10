using System.Collections.Generic;
using UnityEditor;

public static class DenEmoLoc
{
    const string PREF_LANG_EN = "DenEmo_Lang_EnglishMode"; // Keep language global intentionally

    static bool _englishMode = false;
    public static bool EnglishMode
    {
        get => _englishMode;
        set
        {
            if (_englishMode == value) return;
            _englishMode = value;
            EditorPrefs.SetBool(PREF_LANG_EN, _englishMode);
        }
    }

    public static void LoadPrefs()
    {
    _englishMode = EditorPrefs.GetBool(PREF_LANG_EN, false);
    }

    static readonly Dictionary<string, string> JA = new Dictionary<string, string>
    {
        // Status
        ["status.ready"] = "準備完了",
        ["status.saving"] = "保存中...",
        ["status.applying"] = "適用中...",
        ["status.alignedSavedTargets"] = "保存対象をベースアニメーションに揃えました",

        // Top bar
        ["ui.lang.englishMode"] = "Enable English mode",
        ["ui.version.checking"] = "確認中...",
        ["ui.version.update"] = "更新あり {0}",
        ["ui.version.error"] = "最新版を取得できません",
        ["ui.version.reload.tooltip"] = "アップデートを再確認",

        // Sections
        ["ui.section.basic"] = "基本設定",
        ["ui.section.search"] = "シェイプキー検索",

        // Mesh field
        ["ui.mesh.label"] = "メッシュ",
        ["ui.mesh.tooltip"] = "SkinnedMeshRenderer コンポーネントを指定します。",
        ["ui.mesh.missing"] = "対象のメッシュが選択されていません。SkinnedMeshRenderer を指定してください。",
        ["ui.mesh.noShapes"] = "このメッシュにはシェイプキーがありません。",
    ["ui.mesh.inactive.warn"] = "選択中のメッシュは非アクティブ（無効または非表示）です。意図したメッシュか確認してください。",

        // Align to existing clip
        ["ui.align.toggle"] = "保存するキーを既存のアニメーションに揃える",
        ["ui.align.toggle.tip"] = "保存時、ここで指定したベースアニメーションに含まれるブレンドシェイプのキーだけを書き出します。未選択時は有効な全シェイプを保存します。",
        ["ui.align.base.label"] = "ベースアニメーション",
        ["ui.align.base.tip"] = "保存対象のシェイプを選別するために参照するAnimationClipです。『適用』を押すと、このクリップに含まれるブレンドシェイプのみを保存対象に切り替えます。",
        ["ui.align.apply.button"] = "適用",
        ["ui.align.apply.tip"] = "ベースアニメーションに含まれるブレンドシェイプのみ保存対象（チェック）にします。vrc.* 系は除外されます。",

        // Animation source (unified clip field + actions)
        ["ui.animSource.clip.label"] = "アニメーションクリップ",
        ["ui.animSource.clip.tip"] = "操作対象の AnimationClip を指定します。",
        ["ui.animSource.loadAnim.button"] = "アニメーションを読み込む",
        ["ui.animSource.alignKeys.button"] = "アニメーションファイルのキーを揃える",

        // Apply animation to mesh (tooltip kept for reuse)
        ["ui.applyAnim.label"] = "アニメーションを適用",
        ["ui.applyAnim.tip"] = "選択したアニメーションクリップのブレンドシェイプ値（時刻0秒）を現在のメッシュに反映します。",
        ["ui.applyAnim.button"] = "適用",
        ["ui.applyAnim.button.tip"] = "アニメーションの値をメッシュへ反映します（一致するシェイプのみ）。Undo対応。",

        // Filter
        ["ui.filter.showIncluded"] = "有効なシェイプのみ表示",
        ["ui.filter.showIncluded.tip"] = "チェックが入っている（保存対象の）シェイプだけを一覧に表示します。",
        ["ui.filter.vertex"] = "● 頂点で絞り込み",
        ["ui.filter.vertex.active"] = "● 頂点:{0}",
        ["ui.filter.vertex.cancel"] = "× キャンセル",
        ["ui.filter.vertex.guide"] = "頂点を1つクリックして絞り込み（Esc/キャンセルで中止）",

    // Symmetry
    ["ui.symmetry.label"] = "左右同期編集",
    ["ui.symmetry.tip"] = "末尾がL/Rのシェイプを1行にまとめ、左右を同時に編集します。",

        // Snapshot
        ["ui.snapshot.create"] = "一時保存（スナップショット）",
        ["ui.snapshot.restore"] = "スナップショットにリセット",

        // Bulk operations
        ["ui.list.bulkMenu.tip"] = "一括操作",
        ["ui.bulk.checkAll"] = "表示中をすべてチェック",
        ["ui.bulk.uncheckAll"] = "表示中のチェックをすべて外す",
        ["ui.bulk.uncheckUnchanged"] = "変更のないチェックを外す (値が0)",
        ["ui.bulk.checkFavorites"] = "表示中のお気に入りのみチェック",

        // Search
        ["ui.search.title"] = "シェイプキー検索",
        ["ui.search.clear"] = "クリア",

        // Group suffix
        ["ui.group.all"] = "(全選択)",
        ["ui.group.none"] = "(全解除)",
        ["ui.group.some"] = "(一部)",

        // Footer / Save Settings
        ["ui.footer.saveAnim"] = "アニメーションを保存",
        ["ui.footer.refresh"] = "更新",
        ["ui.footer.saveTo"] = "保存先 (既定):",
        ["ui.footer.browse"] = "参照",
        ["ui.footer.overwriteEnable"] = "上書き保存を有効にする",
        ["ui.footer.overwriteEnable.tip"] = "指定したアニメーションファイルに直接上書き保存します。無効時はダイアログで保存先を選択します。",
        ["ui.footer.overwriteTarget"] = "上書き先",

        // UI Toolkit sections (titles / filter chips / save options)
        ["ui.section.targetMesh"]    = "対象メッシュ",
        ["ui.section.animSource"]    = "アニメーション参照",
        ["ui.section.searchFilter"]  = "検索・絞り込み",
        ["ui.section.saveSettings"]  = "保存設定",
        ["ui.section.saveAnim"]      = "アニメーション保存",
        ["ui.filter.keyword"]        = "🔍 キーワード",
        ["ui.filter.fav"]            = "★ お気に入り",
        ["ui.filter.enabled"]        = "✓ 有効のみ",
        ["ui.filter.nonzero"]        = "≠0 非ゼロ",
        ["ui.filter.symmetry"]       = "↔ 左右同期",
        ["ui.filter.keyedOnly"]      = "◆ キー有りのみ",
        ["ui.filter.keyedOnly.tip"]  = "現在のクリップでトラック（キーフレーム）があるシェイプキーのみ表示",
        ["ui.filter.previewOptions"] = "⚙ 表示設定",
        ["ui.footer.browse.title"]   = "フォルダを選択",
        ["ui.footer.autoBackup"]     = "上書き時に自動バックアップ",
        ["ui.footer.autoBackup.tip"] = "上書き保存前に既存ファイルを _backups/ フォルダに複製します。",
        ["ui.animMode.saveAsNew"]     = "新規クリップとして保存",
        ["ui.animMode.saveAsNew.tip"] = "元クリップのフォルダをデフォルトパスとしてファイルダイアログを開き、新規クリップとして保存します。",
        ["ui.animMode.save.button"]   = "アニメーションを保存",

        // Dialogs & messages
        ["dlg.error"] = "エラー",
        ["dlg.info"] = "情報",
        ["dlg.ok"] = "OK",
        ["dlg.save.done.title"] = "保存完了",
        ["dlg.save.done.msg"] = "アニメーションを保存しました: {0}",
        ["dlg.save.noIncluded.title"] = "保存できません",
        ["dlg.save.noIncluded.msg"] = "保存対象のシェイプキーが一つもありません。\n\nアニメーションに含めるシェイプキーにチェックを入れてから保存してください。\n\nヒント：アバターのデフォルト表情アニメーション（FX レイヤーのデフォルト状態に設定されているアニメーション）と保存するシェイプキーを揃えておくと、表情のトラブルが起きにくくなります。「保存するキーを既存のアニメーションに揃える」機能もご活用ください。",
        ["dlg.apply.noTarget"] = "対象の SkinnedMeshRenderer が選択されていません。",
        ["dlg.apply.noClip"] = "アニメーションが選択されていません。",
        ["dlg.apply.done.title"] = "適用完了",
        ["dlg.apply.done.msg"] = "アニメーションのシェイプキー値をメッシュに適用しました。",
        ["dlg.apply.noneFound"] = "アニメーションに適用できるブレンドシェイプが見つかりませんでした。",

        // Save Panel
        ["save.panel.title"] = "アニメーションを保存",
        ["save.panel.defaultName"] = "blendshape_anim",
        ["save.panel.hint"] = "生成されたアニメーションを保存",

        // Animation mode
        ["ui.animMode.clip.label"] = "クリップ",
        ["ui.animMode.clip.new"]   = "新規",
        ["ui.animMode.clip.hint"]  = "クリップを選択するか、「新規」で作成してください。",
        ["ui.animMode.length.label"] = "長さ(s):",
        ["ui.animMode.interp.label"] = "補間:",
        ["ui.animMode.tab.pose"]     = "シングルフレーム",
        ["ui.animMode.tab.anim"]     = "マルチフレーム",

        // Animation mode – workflow guide (shown when no clip is loaded)
        ["ui.animMode.guide.step1"]  = "① クリップフィールドに既存の .anim ファイルをセット、または「新規」で新しいクリップを作成",
        ["ui.animMode.guide.step2"]  = "② タイムラインで FPS・長さ（秒）を確認・設定",
        ["ui.animMode.guide.step3"]  = "③ 再生ヘッドを移動 → REC をオン → スライダーを動かしてキーフレームを記録",

        // Animation mode – recording banner
        ["ui.animMode.rec.banner"]   = "● 録画中 — スライダーを動かすと現在のフレームにキーフレームが自動記録されます",

        // Animation mode – save section info
        ["ui.animMode.noKeys.warn"]  = "⚠ キーフレームがまだありません。REC モードでスライダーを動かしてキーフレームを追加してから保存してください。",
        ["ui.animMode.keyStats"]     = "{0} トラック / 合計 {1} キーフレーム",

        // Common dialogs
        ["dlg.yes"] = "はい",
        ["dlg.no"]  = "いいえ",

        // Timeline
        ["ui.timeline.title"]          = "タイムライン",
        ["ui.timeline.noClip"]         = "アニメーションクリップが読み込まれていません。",
        ["ui.timeline.attach"]         = "↘ 結合",
        ["ui.timeline.attach.tip"]     = "メインウィンドウに戻す",
        ["ui.timeline.detach"]         = "↗ 別窓化",
        ["ui.timeline.detach.tip"]     = "別ウィンドウでタイムラインを開く",
        ["ui.timeline.fps"]            = "FPS:",
        ["ui.timeline.length"]         = "長さ (秒):",
        ["ui.timeline.interp"]         = "補間(新規キー):",
        ["ui.timeline.applyInterpAll"]     = "全キーに適用",
        ["ui.timeline.applyInterpAll.tip"] = "すべての既存キーフレームの補間タイプを現在の選択に一括変更します",
        ["ui.timeline.frame"]          = "フレーム:",
        ["ui.timeline.speed"]          = "速度:",
        ["ui.timeline.loop"]           = " ループ対応",
        ["ui.timeline.loop.tip"]       = "なめらかに繋げるために最後の値を最初に揃える機能（プレビューと保存時に反映されます）",
        ["ui.timeline.rec.tip"]        = "録画モード切替",
        ["ui.timeline.goStart.tip"]    = "先頭フレームへ移動",
        ["ui.timeline.prevKey.tip"]    = "前のキーフレームへ移動",
        ["ui.timeline.prevFrame.tip"]  = "1フレーム戻る",
        ["ui.timeline.play.tip"]       = "再生開始",
        ["ui.timeline.stop.tip"]       = "再生停止",
        ["ui.timeline.nextFrame.tip"]  = "1フレーム進む",
        ["ui.timeline.nextKey.tip"]    = "次のキーフレームへ移動",
        ["ui.timeline.goEnd.tip"]      = "末尾フレームへ移動",
        ["ui.timeline.zoomReset"]      = "ズームリセット",
        ["ui.timeline.zoomReset.tip"]  = "ズームを100%にリセット",
        ["ui.timeline.noKeys"]         = "キーフレームがまだありません。🔴 REC または ◆ ボタンでキーを追加してください。",
        ["ui.timeline.insertAll.tipN"] = "現在フレームに表示中の {0} 個のシェイプのキーを追加（検索・フィルターで対象が変わります）",
        ["ui.timeline.frameDelete.tip"]= "このフレームの全キーを削除",
        ["ui.timeline.moveFrame.tip"]  = "ドラッグでこのフレームの全キーを移動",
        ["ui.timeline.addKey.tip"]     = "キーの追加・更新",
        ["ui.timeline.deleteTrack.tip"]= "このトラックを削除",
        ["ui.timeline.menu.delete"]    = "削除",
        ["ui.timeline.menu.copyFrame"] = "フレームをコピー",
        ["ui.timeline.menu.paste"]     = "現在時刻にペースト",
        ["ui.timeline.help.tip"]       = "タイムライン操作\n・ズーム：Ctrl+マウスホイール（別窓化時はホイール単独でも可）\n・Space：再生 / 停止\n・← / →：1フレーム移動\n・, / .：前後のキーフレームへ移動\n・Delete：現在フレームの全キー削除\n・Ctrl+C / Ctrl+V：フレームのコピー / ペースト\n・キー右クリック：削除・コピー・補間の変更",
        // Timeline separate window (UI Toolkit)
        ["ui.timeline.separate.notice"] = "タイムラインは別ウィンドウで開かれています。",
        ["ui.timeline.separate.focus"]  = "ウィンドウをフォーカス",
        ["ui.timeline.separate.close"]  = "ウィンドウを閉じて結合",
        ["ui.timeline.window.notOpen"]  = "DenEmo ウィンドウが開かれていません。",
        ["ui.timeline.window.open"]     = "DenEmo を開く",
        ["dlg.timeline.deleteFrame.title"] = "フレームキーの削除",
        ["dlg.timeline.deleteFrame.msg"]   = "{0}秒のすべてのキーフレームを削除しますか？",
        ["dlg.timeline.deleteTrack.title"] = "トラックの削除",
        ["dlg.timeline.deleteTrack.msg"]   = "'{0}' のすべてのキーフレームを削除しますか？",
        ["dlg.timeline.applyInterpAll.title"] = "補間の一括変更",
        ["dlg.timeline.applyInterpAll.msg"]   = "すべてのキーフレームの補間タイプを {0} に変更しますか？\nこの操作は Undo できます。",
        ["dlg.timeline.shorten.title"]  = "クリップ長の短縮",
        ["dlg.timeline.shorten.msg"]    = "新しい長さの範囲外に {0} 個のキーフレームがあります。\nキーを残したまま短縮するか、範囲外のキーを削除するか選択してください。",
        ["dlg.timeline.shorten.keep"]   = "短縮（キーは残す）",
        ["dlg.timeline.shorten.delete"] = "短縮してキーを削除",
        ["dlg.cancel"] = "キャンセル",

        // Animation mode – conflict warning / status
        ["ui.animMode.animWindowConflict"] = "⚠ Unity の Animation ウィンドウのプレビューが有効です。同じクリップを同時に編集すると、値の競合や動作が重くなる原因になります。Animation ウィンドウのプレビューを終了することをおすすめします。",
        ["status.anim.tweaksDiscarded"]    = "未記録のスライダー変更 {0} 件を破棄しました（◆ ボタンか REC で記録できます）",

        // Animation mode – clip section / value correction (UI Toolkit)
        ["ui.animMode.clip.section"]  = "アニメーションクリップ",
        ["ui.correction.title"]       = "シェイプキー値補正",
        ["ui.correction.desc"]        = "クリップ全体の各シェイプキーのキーフレーム値を再スケールします。\n表情改変によりシェイプキーの最大値が破綻する場合（VRChat のまばたき競合など）に使用してください。",
        ["ui.correction.noTracks"]    = "このクリップにキーフレームのあるシェイプキーがありません。",
        ["ui.correction.col.shape"]   = "シェイプキー",
        ["ui.correction.col.min"]     = "最小値 (0–100)",
        ["ui.correction.col.max"]     = "最大値 (0–100)",
        ["ui.correction.col.min.tip"] = "補正後の下限値（デフォルト 0）。\n0 より大きい値にすると、元の値 0 がこの値になるようリスケールされます（元の値 100 は最大値の設定値に変わります）。\n例：Min=20 にすると、シェイプキーが完全にニュートラルに戻ることを防げます。",
        ["ui.correction.col.max.tip"] = "補正後の上限値（デフォルト 100）。\n100 未満の値にすると、元の値 100 がこの値になるようリスケールされます（元の値 0 は最小値の設定値に変わります）。\n例：まばたきシェイプキーの Max=80 にすると、このアニメーション内で目が完全に閉じないように制限できます。",
        ["ui.correction.apply"]       = "補正を反映",
        ["status.correction.applied"] = "補正を反映しました。",
        ["status.correction.none"]    = "補正対象がありません（全て既定値です）。",

        // Shape key list (UI Toolkit)
        ["ui.list.title"]     = "シェイプキー",
        ["ui.list.noMatch"]   = "フィルター条件に一致するシェイプキーがありません。",
        ["ui.fav.add"]        = "お気に入り追加",
        ["ui.fav.remove"]     = "お気に入り解除",
        ["ui.key.add"]        = "現在時刻にキーフレームを追加",
        ["ui.key.remove"]     = "現在時刻のキーフレームを削除",
        ["ui.row.zero.tip"]   = "値を0にリセット",
        ["ui.row.zeroLR.tip"] = "左右の値を0にリセット",

        // FX setup mode (アバターへ適用)
        ["ui.fx.tab"]                    = "アバターへ適用",
        ["ui.fx.section.avatar"]         = "アバター / FXレイヤー",
        ["ui.fx.avatar.label"]           = "アバター: {0}",
        ["ui.fx.avatar.fieldLabel"]      = "対象アバター",
        ["ui.fx.fx.fieldLabel"]          = "FXレイヤー",
        ["ui.fx.avatar.notFound"]        = "VRChat アバターが見つかりません。VRC Avatar Descriptor の付いたアバター配下のメッシュを指定してください。（VRChat SDK が未導入の可能性もあります）",
        ["ui.fx.avatar.manualController"] = "コントローラーを直接指定",
        ["ui.fx.fx.label"]               = "FX: {0}",
        ["ui.fx.fx.stats"]               = "表情アニメーション {0} 件",
        ["ui.fx.fx.missing"]             = "FX レイヤーにカスタムコントローラーが設定されていません。",
        ["ui.fx.fx.readFailed"]          = "FX レイヤー情報を読み取れませんでした（SDK バージョン差異の可能性）。コントローラーを直接指定してください。",
        ["ui.fx.fx.overrideUnsupported"] = "Animator Override Controller には対応していません。",
        ["ui.fx.rescan"]                 = "再スキャン",
        ["ui.fx.section.list"]           = "表情の差し替え",
        ["ui.fx.filter.assignedOnly"]    = "割当て済みのみ",
        ["ui.fx.filter.showAll"]         = "すべてのシェイプキーアニメーションを表示",
        ["ui.fx.list.empty"]             = "対象メッシュのシェイプキーを動かすアニメーションが見つかりませんでした。",
        ["ui.fx.list.emptyFiltered"]     = "条件に一致するアニメーションがありません。",
        ["ui.fx.row.usage"]              = "({0}箇所)",
        ["ui.fx.row.pathMismatch"]       = "⚠ メッシュパス不一致: {0}",
        ["ui.fx.gesture.left"]           = "L:{0}",
        ["ui.fx.gesture.right"]          = "R:{0}",
        ["ui.fx.gesture.both"]           = "L:{0}+R:{1}",
        ["ui.fx.gesture.more"]           = "+{0}",
        ["ui.fx.gesture.tag.tip"]        = "このクリップを再生するハンドジェスチャー: {0}",
        ["ui.fx.gesture.filter.left"]    = "左手",
        ["ui.fx.gesture.filter.right"]   = "右手",
        ["ui.fx.slot.none"]              = "未指定 ▾",
        ["ui.fx.slot.clear.tip"]         = "割当てを解除",
        ["ui.fx.hover.hint"]             = "行にマウスを乗せると、シーンでそのアニメーションがプレビュー再生されます",
        ["ui.fx.hover.playing"]          = "● プレビュー中: {0}",
        ["ui.fx.hover.stop"]             = "停止",
        ["ui.fx.err.selfAssign"]         = "差し替え元と同じアニメーションは指定できません。",
        ["ui.fx.err.sceneClip"]          = "プロジェクトアセットではないアニメーションは指定できません。",
        ["ui.fx.err.notAsset"]           = "コントローラーがプロジェクトアセットではないため処理できません。",
        ["ui.fx.err.copyFailed"]         = "コントローラーの複製に失敗しました。",
        ["ui.fx.section.apply"]          = "適用",
        ["ui.fx.apply.count"]            = "割当て済み: {0} 件",
        ["ui.fx.mode.duplicate"]         = "複製して適用（推奨）",
        ["ui.fx.mode.direct"]            = "直接変更",
        ["ui.fx.mode.duplicate.desc"]    = "FX コントローラーを複製し、複製側だけを書き換えてアバターにセットします。元のコントローラーは変更されません。",
        ["ui.fx.mode.direct.desc"]       = "⚠ 既存のコントローラーを直接書き換えます（バックアップは作成されません）。",
        ["ui.fx.mode.manualNote"]        = "アバター未検出のため、複製したコントローラーは手動で FX レイヤーにセットしてください。",
        ["ui.fx.apply.button"]           = "{0} 件の表情を差し替える",
        ["ui.fx.confirm.title"]          = "表情の差し替え",
        ["ui.fx.confirm.duplicate.head"] = "FX コントローラーを複製して差し替えます。元のコントローラーは変更されません。",
        ["ui.fx.confirm.direct.head"]    = "既存の FX コントローラーを直接変更します（バックアップなし）。",
        ["ui.fx.confirm.slotCount"]      = "差し替える参照箇所: {0} 箇所",
        ["ui.fx.confirm.more"]           = "…ほか {0} 件",
        ["ui.fx.confirm.ok"]             = "実行",
        ["ui.fx.result.success"]         = "✓ {0} 箇所の表情参照を差し替えました",
        ["ui.fx.result.newController"]   = "新しいコントローラー: {0}",
        ["ui.fx.result.descriptorSet"]   = "アバターの FX レイヤーに新しいコントローラーをセットしました",
        ["ui.fx.result.manualSet"]       = "⚠ FX レイヤーへのセットは手動で行ってください",
        ["ui.fx.result.backup"]          = "バックアップ: {0}",
        ["ui.fx.result.ping"]            = "表示",
        ["ui.fx.picker.title"]           = "差し替え先アニメーション",
        ["ui.fx.picker.none"]            = "なし（割当て解除）",
        ["ui.fx.picker.empty"]           = "フォルダにシェイプキーアニメーションがありません",
        ["ui.fx.picker.folder.tip"]      = "参照フォルダを変更",
        ["status.fx.applied"]            = "表情の差し替えが完了しました",

        // Vertex preview options popup
        ["ui.vertexPreview.title"]         = "頂点プレビュー設定",
        ["ui.vertexPreview.normalColor"]   = "通常の色",
        ["ui.vertexPreview.selectedColor"] = "選択中の色",
        ["ui.vertexPreview.size"]          = "サイズ",
    };

    static readonly Dictionary<string, string> EN = new Dictionary<string, string>
    {
        // Status
        ["status.ready"] = "Ready",
        ["status.saving"] = "Saving...",
        ["status.applying"] = "Applying...",
        ["status.alignedSavedTargets"] = "Save targets aligned to base animation",

        // Top bar
        ["ui.lang.englishMode"] = "日本語モードを有効化",
        ["ui.version.checking"] = "Checking...",
        ["ui.version.update"] = "Update available {0}",
        ["ui.version.error"] = "Update check failed",
        ["ui.version.reload.tooltip"] = "Re-check for updates",

        // Sections
        ["ui.section.basic"] = "Basic Settings",
        ["ui.section.search"] = "Blendshape Search",

        // Mesh field
        ["ui.mesh.label"] = "Mesh",
        ["ui.mesh.tooltip"] = "Assign a SkinnedMeshRenderer component.",
        ["ui.mesh.missing"] = "No target mesh selected. Please assign a SkinnedMeshRenderer.",
        ["ui.mesh.noShapes"] = "This mesh has no blendshapes.",
    ["ui.mesh.inactive.warn"] = "The selected mesh is inactive (disabled or not active in hierarchy). Please verify it's the intended target.",

        // Align to existing clip
        ["ui.align.toggle"] = "Align saved keys to existing animation",
        ["ui.align.toggle.tip"] = "When saving, only write keys for blendshapes that exist in the specified base animation. When disabled, all enabled shapes are saved.",
        ["ui.align.base.label"] = "Base Animation",
        ["ui.align.base.tip"] = "AnimationClip used to select which shapes will be saved. Clicking 'Apply' toggles save targets to only those contained in this clip.",
        ["ui.align.apply.button"] = "Apply",
        ["ui.align.apply.tip"] = "Set save targets (checks) to shapes contained in the base animation. vrc.* shapes are excluded.",

        // Animation source (unified clip field + actions)
        ["ui.animSource.clip.label"] = "Animation Clip",
        ["ui.animSource.clip.tip"] = "Specify the AnimationClip to work with.",
        ["ui.animSource.loadAnim.button"] = "Load Animation",
        ["ui.animSource.alignKeys.button"] = "Align Animation File Keys",

        // Apply animation to mesh (tooltip kept for reuse)
        ["ui.applyAnim.label"] = "Apply Animation",
        ["ui.applyAnim.tip"] = "Applies blendshape values at time 0s from the selected clip to the current mesh.",
        ["ui.applyAnim.button"] = "Apply",
        ["ui.applyAnim.button.tip"] = "Apply animation values to the mesh (matching shapes only). Supports Undo.",

        // Filter
        ["ui.filter.showIncluded"] = "Show only enabled shapes",
        ["ui.filter.showIncluded.tip"] = "List only shapes that are checked (will be saved).",
        ["ui.filter.vertex"] = "● Filter by Vertex",
        ["ui.filter.vertex.active"] = "● Vertex:{0}",
        ["ui.filter.vertex.cancel"] = "× Cancel",
        ["ui.filter.vertex.guide"] = "Click one vertex to filter (Esc/Cancel to stop)",

    // Symmetry
    ["ui.symmetry.label"] = "Symmetry edit",
    ["ui.symmetry.tip"] = "Merge L/R-suffixed shapes into one row and edit both sides together.",

        // Snapshot
        ["ui.snapshot.create"] = "Snapshot",
        ["ui.snapshot.restore"] = "Restore Snapshot",

        // Bulk operations
        ["ui.list.bulkMenu.tip"] = "Bulk operations",
        ["ui.bulk.checkAll"] = "Check All Visible",
        ["ui.bulk.uncheckAll"] = "Uncheck All Visible",
        ["ui.bulk.uncheckUnchanged"] = "Uncheck Unchanged (Value is 0)",
        ["ui.bulk.checkFavorites"] = "Check Favorites Only",

        // Search
        ["ui.search.title"] = "Blendshape Search",
        ["ui.search.clear"] = "Clear",

        // Group suffix
        ["ui.group.all"] = "(All)",
        ["ui.group.none"] = "(None)",
        ["ui.group.some"] = "(Partial)",

        // Footer / Save Settings
        ["ui.footer.saveAnim"] = "Save Animation",
        ["ui.footer.refresh"] = "Refresh",
        ["ui.footer.saveTo"] = "Save To (default):",
        ["ui.footer.browse"] = "Browse",
        ["ui.footer.overwriteEnable"] = "Enable overwrite save",
        ["ui.footer.overwriteEnable.tip"] = "Saves directly to the specified animation file. When disabled, a dialog will open to choose the save path.",
        ["ui.footer.overwriteTarget"] = "Overwrite target",

        // UI Toolkit sections (titles / filter chips / save options)
        ["ui.section.targetMesh"]    = "TARGET MESH",
        ["ui.section.animSource"]    = "ANIMATION SOURCE",
        ["ui.section.searchFilter"]  = "SEARCH & FILTER",
        ["ui.section.saveSettings"]  = "SAVE SETTINGS",
        ["ui.section.saveAnim"]      = "SAVE ANIMATION",
        ["ui.filter.keyword"]        = "🔍 Keyword",
        ["ui.filter.fav"]            = "★ Fav",
        ["ui.filter.enabled"]        = "✓ Enabled",
        ["ui.filter.nonzero"]        = "≠0 Non-zero",
        ["ui.filter.symmetry"]       = "↔ Symmetry",
        ["ui.filter.keyedOnly"]      = "◆ Keyed Only",
        ["ui.filter.keyedOnly.tip"]  = "Show only shape keys that have tracks/keyframes in the current clip",
        ["ui.filter.previewOptions"] = "⚙ Preview",
        ["ui.footer.browse.title"]   = "Select Folder",
        ["ui.footer.autoBackup"]     = "Auto backup on overwrite",
        ["ui.footer.autoBackup.tip"] = "Copies the existing .anim file to _backups/ before overwriting.",
        ["ui.animMode.saveAsNew"]     = "Save as new clip",
        ["ui.animMode.saveAsNew.tip"] = "Opens a file dialog to save as a new animation clip. The original clip's folder is used as the default path.",
        ["ui.animMode.save.button"]   = "Save Animation",

        // Dialogs & messages
        ["dlg.error"] = "Error",
        ["dlg.info"] = "Info",
        ["dlg.ok"] = "OK",
        ["dlg.save.done.title"] = "Saved",
        ["dlg.save.done.msg"] = "Animation saved: {0}",
        ["dlg.save.noIncluded.title"] = "Cannot Save",
        ["dlg.save.noIncluded.msg"] = "No shape keys are checked for saving.\n\nPlease check the shape keys you want to include in the animation before saving.\n\nTip: Aligning the saved keys with your avatar's default expression animation (the animation set on the default state of the FX layer) helps prevent expression issues. Try using the 'Align saved keys to existing animation' feature.",
        ["dlg.apply.noTarget"] = "No target SkinnedMeshRenderer selected.",
        ["dlg.apply.noClip"] = "No animation selected.",
        ["dlg.apply.done.title"] = "Applied",
        ["dlg.apply.done.msg"] = "Applied animation blendshape values to the mesh.",
        ["dlg.apply.noneFound"] = "No applicable blendshapes were found in the animation.",

        // Save Panel
        ["save.panel.title"] = "Save Animation",
        ["save.panel.defaultName"] = "blendshape_anim",
        ["save.panel.hint"] = "Save generated animation",

        // Animation mode
        ["ui.animMode.clip.label"] = "Clip",
        ["ui.animMode.clip.new"]   = "New",
        ["ui.animMode.clip.hint"]  = "Select a clip or create a new one.",
        ["ui.animMode.length.label"] = "Length(s):",
        ["ui.animMode.interp.label"] = "Interp:",
        ["ui.animMode.tab.pose"]     = "SINGLE FRAME",
        ["ui.animMode.tab.anim"]     = "MULTI FRAME",

        // Animation mode – workflow guide (shown when no clip is loaded)
        ["ui.animMode.guide.step1"]  = "① Drag an existing .anim file into the Clip field, or press \"New\" to create a blank clip",
        ["ui.animMode.guide.step2"]  = "② Check / set FPS and duration in the Timeline section",
        ["ui.animMode.guide.step3"]  = "③ Move the playhead → enable REC → drag sliders to record keyframes",

        // Animation mode – recording banner
        ["ui.animMode.rec.banner"]   = "● RECORDING — Dragging a slider records a keyframe at the current time",

        // Animation mode – save section info
        ["ui.animMode.noKeys.warn"]  = "⚠ No keyframes yet. Enable REC mode and drag sliders to add keyframes before saving.",
        ["ui.animMode.keyStats"]     = "{0} tracks / {1} keyframes total",

        // Common dialogs
        ["dlg.yes"] = "Yes",
        ["dlg.no"]  = "No",

        // Timeline
        ["ui.timeline.title"]          = "TIMELINE",
        ["ui.timeline.noClip"]         = "No animation clip loaded.",
        ["ui.timeline.attach"]         = "↘ Attach",
        ["ui.timeline.attach.tip"]     = "Return timeline to main window",
        ["ui.timeline.detach"]         = "↗ Detach",
        ["ui.timeline.detach.tip"]     = "Open timeline in a separate window",
        ["ui.timeline.fps"]            = "FPS:",
        ["ui.timeline.length"]         = "Len (s):",
        ["ui.timeline.interp"]         = "Interp (new keys):",
        ["ui.timeline.applyInterpAll"]     = "Apply to All",
        ["ui.timeline.applyInterpAll.tip"] = "Change the interpolation type of ALL existing keyframes to the current selection",
        ["ui.timeline.frame"]          = "Frame:",
        ["ui.timeline.speed"]          = "Speed:",
        ["ui.timeline.loop"]           = " Loop Support",
        ["ui.timeline.loop.tip"]       = "Aligns the last value to the first to connect smoothly (applied in preview and on save)",
        ["ui.timeline.rec.tip"]        = "Toggle recording mode",
        ["ui.timeline.goStart.tip"]    = "Go to start frame",
        ["ui.timeline.prevKey.tip"]    = "Go to previous keyframe",
        ["ui.timeline.prevFrame.tip"]  = "Step one frame backward",
        ["ui.timeline.play.tip"]       = "Start playback",
        ["ui.timeline.stop.tip"]       = "Stop playback",
        ["ui.timeline.nextFrame.tip"]  = "Step one frame forward",
        ["ui.timeline.nextKey.tip"]    = "Go to next keyframe",
        ["ui.timeline.goEnd.tip"]      = "Go to end frame",
        ["ui.timeline.zoomReset"]      = "Reset Zoom",
        ["ui.timeline.zoomReset.tip"]  = "Reset zoom to 100%",
        ["ui.timeline.noKeys"]         = "No keyframes yet. Use 🔴 REC or the ◆ button to add keys.",
        ["ui.timeline.insertAll.tipN"] = "Insert keys for the {0} visible shape(s) at the current frame (affected by search/filters)",
        ["ui.timeline.frameDelete.tip"]= "Delete all keys at this frame",
        ["ui.timeline.moveFrame.tip"]  = "Drag to move all keys at this frame",
        ["ui.timeline.addKey.tip"]     = "Add / Update Key",
        ["ui.timeline.deleteTrack.tip"]= "Delete this track",
        ["ui.timeline.menu.delete"]    = "Delete",
        ["ui.timeline.menu.copyFrame"] = "Copy frame",
        ["ui.timeline.menu.paste"]     = "Paste at current time",
        ["ui.timeline.help.tip"]       = "Timeline controls\n- Zoom: Ctrl + mouse wheel (wheel alone works in the detached window)\n- Space: Play / Stop\n- Left / Right: step one frame\n- , / . : go to previous / next keyframe\n- Delete: delete all keys at the current frame\n- Ctrl+C / Ctrl+V: copy / paste frame\n- Right-click a key: delete, copy, change interpolation",
        // Timeline separate window (UI Toolkit)
        ["ui.timeline.separate.notice"] = "Timeline is open in a separate window.",
        ["ui.timeline.separate.focus"]  = "Focus Timeline Window",
        ["ui.timeline.separate.close"]  = "Close Timeline Window",
        ["ui.timeline.window.notOpen"]  = "DenEmo Window is not open.",
        ["ui.timeline.window.open"]     = "Open DenEmo",
        ["dlg.timeline.deleteFrame.title"] = "Delete Frame Keys",
        ["dlg.timeline.deleteFrame.msg"]   = "Delete all keyframes at {0}s?",
        ["dlg.timeline.deleteTrack.title"] = "Delete Track",
        ["dlg.timeline.deleteTrack.msg"]   = "Delete all keyframes for '{0}'?",
        ["dlg.timeline.applyInterpAll.title"] = "Apply Interpolation to All Keys",
        ["dlg.timeline.applyInterpAll.msg"]   = "Change the interpolation type of all keyframes to {0}?\nThis operation can be undone.",
        ["dlg.timeline.shorten.title"]  = "Shorten Clip Length",
        ["dlg.timeline.shorten.msg"]    = "{0} keyframe(s) are beyond the new length.\nChoose whether to keep them or delete them.",
        ["dlg.timeline.shorten.keep"]   = "Shorten (keep keys)",
        ["dlg.timeline.shorten.delete"] = "Shorten & delete keys",
        ["dlg.cancel"] = "Cancel",

        // Animation mode – conflict warning / status
        ["ui.animMode.animWindowConflict"] = "⚠ Unity's Animation window preview is active. Editing the same clip in both tools can cause value conflicts and slowdowns. Consider exiting the Animation window preview.",
        ["status.anim.tweaksDiscarded"]    = "Discarded {0} unrecorded slider change(s) (use the ◆ button or REC to record them)",

        // Animation mode – clip section / value correction (UI Toolkit)
        ["ui.animMode.clip.section"]  = "ANIMATION CLIP",
        ["ui.correction.title"]       = "SHAPE KEY VALUE CORRECTION",
        ["ui.correction.desc"]        = "Rescale keyframe values of individual shape keys across the entire clip.\nUseful when an expression edit makes a shape key's full range look broken (e.g. blink conflicts in VRChat).",
        ["ui.correction.noTracks"]    = "No shape keys with keyframes found in this clip.",
        ["ui.correction.col.shape"]   = "Shape Key",
        ["ui.correction.col.min"]     = "Min (0–100)",
        ["ui.correction.col.max"]     = "Max (0–100)",
        ["ui.correction.col.min.tip"] = "Lower bound after correction (default 0).\nWhen above 0, the original value 0 is raised to this value while the original 100 maps to the Max setting.\nExample: Min=20 prevents the shape key from fully returning to neutral.",
        ["ui.correction.col.max.tip"] = "Upper bound after correction (default 100).\nWhen below 100, the original value 100 is lowered to this value while the original 0 maps to the Min setting.\nExample: Max=80 on a blink shape prevents the eye from fully closing in this animation.",
        ["ui.correction.apply"]       = "Apply Correction",
        ["status.correction.applied"] = "Correction applied.",
        ["status.correction.none"]    = "No corrections to apply (all values are at their defaults).",

        // Shape key list (UI Toolkit)
        ["ui.list.title"]     = "SHAPE KEYS",
        ["ui.list.noMatch"]   = "No results match the current filter.",
        ["ui.fav.add"]        = "Add to favorites",
        ["ui.fav.remove"]     = "Remove from favorites",
        ["ui.key.add"]        = "Add keyframe at current time",
        ["ui.key.remove"]     = "Remove keyframe at current time",
        ["ui.row.zero.tip"]   = "Reset value to zero",
        ["ui.row.zeroLR.tip"] = "Reset both L/R values to zero",

        // FX setup mode (Apply to Avatar)
        ["ui.fx.tab"]                    = "APPLY TO AVATAR",
        ["ui.fx.section.avatar"]         = "AVATAR / FX LAYER",
        ["ui.fx.avatar.label"]           = "Avatar: {0}",
        ["ui.fx.avatar.fieldLabel"]      = "Target Avatar",
        ["ui.fx.fx.fieldLabel"]          = "FX Layer",
        ["ui.fx.avatar.notFound"]        = "No VRChat avatar found. Assign a mesh under an avatar with a VRC Avatar Descriptor. (The VRChat SDK may not be installed.)",
        ["ui.fx.avatar.manualController"] = "Assign controller manually",
        ["ui.fx.fx.label"]               = "FX: {0}",
        ["ui.fx.fx.stats"]               = "{0} expression animation(s)",
        ["ui.fx.fx.missing"]             = "No custom controller is set on the FX layer.",
        ["ui.fx.fx.readFailed"]          = "Could not read the FX layer (possibly an SDK version difference). Assign the controller manually.",
        ["ui.fx.fx.overrideUnsupported"] = "Animator Override Controllers are not supported.",
        ["ui.fx.rescan"]                 = "Rescan",
        ["ui.fx.section.list"]           = "REPLACE EXPRESSIONS",
        ["ui.fx.filter.assignedOnly"]    = "Assigned only",
        ["ui.fx.filter.showAll"]         = "Show all blendshape animations",
        ["ui.fx.list.empty"]             = "No animations that move the target mesh's blendshapes were found.",
        ["ui.fx.list.emptyFiltered"]     = "No animations match the current filter.",
        ["ui.fx.row.usage"]              = "({0} uses)",
        ["ui.fx.row.pathMismatch"]       = "⚠ Mesh path mismatch: {0}",
        ["ui.fx.gesture.left"]           = "L:{0}",
        ["ui.fx.gesture.right"]          = "R:{0}",
        ["ui.fx.gesture.both"]           = "L:{0}+R:{1}",
        ["ui.fx.gesture.more"]           = "+{0}",
        ["ui.fx.gesture.tag.tip"]        = "Hand gesture(s) that play this clip: {0}",
        ["ui.fx.gesture.filter.left"]    = "Left Hand",
        ["ui.fx.gesture.filter.right"]   = "Right Hand",
        ["ui.fx.slot.none"]              = "(none) ▾",
        ["ui.fx.slot.clear.tip"]         = "Clear assignment",
        ["ui.fx.hover.hint"]             = "Hover a row to preview that animation in the Scene view",
        ["ui.fx.hover.playing"]          = "● Previewing: {0}",
        ["ui.fx.hover.stop"]             = "Stop",
        ["ui.fx.err.selfAssign"]         = "Cannot assign the same animation as the one being replaced.",
        ["ui.fx.err.sceneClip"]          = "Only project asset animations can be assigned.",
        ["ui.fx.err.notAsset"]           = "The controller is not a project asset.",
        ["ui.fx.err.copyFailed"]         = "Failed to duplicate the controller.",
        ["ui.fx.section.apply"]          = "APPLY",
        ["ui.fx.apply.count"]            = "Assigned: {0}",
        ["ui.fx.mode.duplicate"]         = "Duplicate & apply (safe)",
        ["ui.fx.mode.direct"]            = "Modify directly",
        ["ui.fx.mode.duplicate.desc"]    = "Duplicates the FX controller, modifies only the copy, and assigns it to the avatar. The original controller is untouched.",
        ["ui.fx.mode.direct.desc"]       = "⚠ Modifies the existing controller directly (no backup is created).",
        ["ui.fx.mode.manualNote"]        = "No avatar detected — you will need to assign the duplicated controller to the FX layer manually.",
        ["ui.fx.apply.button"]           = "Replace {0} expression(s)",
        ["ui.fx.confirm.title"]          = "Replace Expressions",
        ["ui.fx.confirm.duplicate.head"] = "The FX controller will be duplicated and modified. The original controller is untouched.",
        ["ui.fx.confirm.direct.head"]    = "The existing FX controller will be modified directly (without backup).",
        ["ui.fx.confirm.slotCount"]      = "Motion references to replace: {0}",
        ["ui.fx.confirm.more"]           = "…and {0} more",
        ["ui.fx.confirm.ok"]             = "Apply",
        ["ui.fx.result.success"]         = "✓ Replaced {0} motion reference(s)",
        ["ui.fx.result.newController"]   = "New controller: {0}",
        ["ui.fx.result.descriptorSet"]   = "The new controller has been set on the avatar's FX layer",
        ["ui.fx.result.manualSet"]       = "⚠ Please assign it to the FX layer manually",
        ["ui.fx.result.backup"]          = "Backup: {0}",
        ["ui.fx.result.ping"]            = "Ping",
        ["ui.fx.picker.title"]           = "Replacement animation",
        ["ui.fx.picker.none"]            = "None (clear assignment)",
        ["ui.fx.picker.empty"]           = "No blendshape animations in this folder",
        ["ui.fx.picker.folder.tip"]      = "Change folder",
        ["status.fx.applied"]            = "Expressions replaced successfully",

        // Vertex preview options popup
        ["ui.vertexPreview.title"]         = "Vertex Preview Settings",
        ["ui.vertexPreview.normalColor"]   = "Normal Color",
        ["ui.vertexPreview.selectedColor"] = "Selected Color",
        ["ui.vertexPreview.size"]          = "Size",
    };

    public static string T(string key)
    {
        var dict = _englishMode ? EN : JA;
        if (dict.TryGetValue(key, out var value)) return value;
        // Fallback
        if (JA.TryGetValue(key, out var ja)) return ja;
        if (EN.TryGetValue(key, out var en)) return en;
        return key;
    }

    public static string Tf(string key, params object[] args)
    {
        var format = T(key);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
    }
}
