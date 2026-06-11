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
        ["ui.timeline.attach"]         = "↘ 結合",
        ["ui.timeline.attach.tip"]     = "メインウィンドウに戻す",
        ["ui.timeline.detach"]         = "↗ 別窓化",
        ["ui.timeline.detach.tip"]     = "別ウィンドウでタイムラインを開く",
        ["ui.timeline.fps"]            = "FPS:",
        ["ui.timeline.length"]         = "長さ (秒):",
        ["ui.timeline.allInterp"]      = "全キー一括補完:",
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
        ["ui.timeline.insertAll.tip"]  = "現在フレームに表示中の全シェイプのキーを追加",
        ["ui.timeline.frameDelete.tip"]= "このフレームの全キーを削除",
        ["ui.timeline.addKey.tip"]     = "キーの追加・更新",
        ["ui.timeline.deleteTrack.tip"]= "このトラックを削除",
        ["ui.timeline.menu.delete"]    = "削除",
        ["ui.timeline.menu.copyFrame"] = "フレームをコピー",
        ["ui.timeline.menu.paste"]     = "現在時刻にペースト",
        ["dlg.timeline.deleteFrame.title"] = "フレームキーの削除",
        ["dlg.timeline.deleteFrame.msg"]   = "{0}秒のすべてのキーフレームを削除しますか？",
        ["dlg.timeline.deleteTrack.title"] = "トラックの削除",
        ["dlg.timeline.deleteTrack.msg"]   = "'{0}' のすべてのキーフレームを削除しますか？",
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
        ["ui.timeline.attach"]         = "↘ Attach",
        ["ui.timeline.attach.tip"]     = "Return timeline to main window",
        ["ui.timeline.detach"]         = "↗ Detach",
        ["ui.timeline.detach.tip"]     = "Open timeline in a separate window",
        ["ui.timeline.fps"]            = "FPS:",
        ["ui.timeline.length"]         = "Len (s):",
        ["ui.timeline.allInterp"]      = "All Keys Interp:",
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
        ["ui.timeline.insertAll.tip"]  = "Insert keyframe at current time for all visible shapes",
        ["ui.timeline.frameDelete.tip"]= "Delete all keys at this frame",
        ["ui.timeline.addKey.tip"]     = "Add / Update Key",
        ["ui.timeline.deleteTrack.tip"]= "Delete this track",
        ["ui.timeline.menu.delete"]    = "Delete",
        ["ui.timeline.menu.copyFrame"] = "Copy frame",
        ["ui.timeline.menu.paste"]     = "Paste at current time",
        ["dlg.timeline.deleteFrame.title"] = "Delete Frame Keys",
        ["dlg.timeline.deleteFrame.msg"]   = "Delete all keyframes at {0}s?",
        ["dlg.timeline.deleteTrack.title"] = "Delete Track",
        ["dlg.timeline.deleteTrack.msg"]   = "Delete all keyframes for '{0}'?",
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
