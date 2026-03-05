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

        // Apply animation to mesh
        ["ui.applyAnim.label"] = "アニメーションを適用",
        ["ui.applyAnim.tip"] = "選択したアニメーションクリップのブレンドシェイプ値（時刻0秒）を現在のメッシュに反映します。",
        ["ui.applyAnim.button"] = "適用",
        ["ui.applyAnim.button.tip"] = "アニメーションの値をメッシュへ反映します（一致するシェイプのみ）。Undo対応。",

        // Filter
        ["ui.filter.showIncluded"] = "有効なシェイプのみ表示",
        ["ui.filter.showIncluded.tip"] = "チェックが入っている（保存対象の）シェイプだけを一覧に表示します。",

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

        // Footer
        ["ui.footer.saveAnim"] = "アニメーションを保存",
        ["ui.footer.refresh"] = "更新",
        ["ui.footer.saveTo"] = "保存先 (既定):",
        ["ui.footer.browse"] = "参照",

        // Dialogs & messages
        ["dlg.error"] = "エラー",
        ["dlg.info"] = "情報",
        ["dlg.ok"] = "OK",
        ["dlg.save.done.title"] = "保存完了",
        ["dlg.save.done.msg"] = "アニメーションを保存しました: {0}",
        ["dlg.apply.noTarget"] = "対象の SkinnedMeshRenderer が選択されていません。",
        ["dlg.apply.noClip"] = "アニメーションが選択されていません。",
        ["dlg.apply.done.title"] = "適用完了",
        ["dlg.apply.done.msg"] = "アニメーションのシェイプキー値をメッシュに適用しました。",
        ["dlg.apply.noneFound"] = "アニメーションに適用できるブレンドシェイプが見つかりませんでした。",

        // Save Panel
        ["save.panel.title"] = "アニメーションを保存",
        ["save.panel.defaultName"] = "blendshape_anim",
        ["save.panel.hint"] = "生成されたアニメーションを保存",
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

        // Apply animation to mesh
        ["ui.applyAnim.label"] = "Apply Animation",
        ["ui.applyAnim.tip"] = "Applies blendshape values at time 0s from the selected clip to the current mesh.",
        ["ui.applyAnim.button"] = "Apply",
        ["ui.applyAnim.button.tip"] = "Apply animation values to the mesh (matching shapes only). Supports Undo.",

        // Filter
        ["ui.filter.showIncluded"] = "Show only enabled shapes",
        ["ui.filter.showIncluded.tip"] = "List only shapes that are checked (will be saved).",

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

        // Footer
        ["ui.footer.saveAnim"] = "Save Animation",
        ["ui.footer.refresh"] = "Refresh",
        ["ui.footer.saveTo"] = "Save To (default):",
        ["ui.footer.browse"] = "Browse",

        // Dialogs & messages
        ["dlg.error"] = "Error",
        ["dlg.info"] = "Info",
        ["dlg.ok"] = "OK",
        ["dlg.save.done.title"] = "Saved",
        ["dlg.save.done.msg"] = "Animation saved: {0}",
        ["dlg.apply.noTarget"] = "No target SkinnedMeshRenderer selected.",
        ["dlg.apply.noClip"] = "No animation selected.",
        ["dlg.apply.done.title"] = "Applied",
        ["dlg.apply.done.msg"] = "Applied animation blendshape values to the mesh.",
        ["dlg.apply.noneFound"] = "No applicable blendshapes were found in the animation.",

        // Save Panel
        ["save.panel.title"] = "Save Animation",
        ["save.panel.defaultName"] = "blendshape_anim",
        ["save.panel.hint"] = "Save generated animation",
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
