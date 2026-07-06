using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    public class FxReplaceResult
    {
        public bool   Success;
        public int    ReplacedCount;
        public string NewControllerPath;  // 複製モードで生成したコントローラー
        public string BackupPath;         // 直接モードのバックアップ
        public bool   DescriptorUpdated;  // アバターの FX 参照を差し替えたか
        public string Error;
    }

    /// <summary>
    /// FX コントローラー内の Motion 参照を一括で差し替える。
    /// 複製モード: コントローラーを丸ごと複製し、複製側のみ書き換えてアバターに再セット（元は無傷）。
    /// 直接モード: バックアップを作成したうえで既存コントローラーを Undo 付きで書き換える。
    /// </summary>
    public static class FxMotionReplacer
    {
        public static FxReplaceResult ReplaceWithDuplicate(
            AnimatorController source,
            List<(FxExpressionEntry entry, FxMapping mapping)> jobs,
            Component descriptor,
            HashSet<string> targetSmrPaths)
        {
            var result = new FxReplaceResult();

            string srcPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(srcPath))
            {
                result.Error = DenEmoLoc.T("ui.fx.err.notAsset");
                return result;
            }

            string dir     = Path.GetDirectoryName(srcPath).Replace('\\', '/');
            string dstPath = AssetDatabase.GenerateUniqueAssetPath(
                dir + "/" + Path.GetFileNameWithoutExtension(srcPath) + "_DenEmo.controller");

            if (!AssetDatabase.CopyAsset(srcPath, dstPath))
            {
                result.Error = DenEmoLoc.T("ui.fx.err.copyFailed");
                return result;
            }

            var copy = AssetDatabase.LoadAssetAtPath<AnimatorController>(dstPath);
            if (copy == null)
            {
                result.Error = DenEmoLoc.T("ui.fx.err.copyFailed");
                return result;
            }

            // 複製側を再スキャンし、SlotKey で書き換え対象を再解決する
            // （State / BlendTree のオブジェクト参照はコピー間で異なるため）
            var copySlots = new Dictionary<string, FxMotionSlot>();
            foreach (var entry in FxLayerScanner.Scan(copy, targetSmrPaths))
                foreach (var slot in entry.Slots)
                    copySlots[slot.SlotKey] = slot;

            foreach (var (entry, mapping) in jobs)
            {
                foreach (var slot in entry.Slots)
                {
                    if (mapping.DisabledSlotKeys.Contains(slot.SlotKey)) continue;
                    if (!copySlots.TryGetValue(slot.SlotKey, out var copySlot)) continue;
                    WriteSlot(copySlot, mapping.NewClip, withUndo: false);
                    result.ReplacedCount++;
                }
            }

            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();

            if (descriptor != null)
                result.DescriptorUpdated = VrcAvatarReflection.SetFxController(descriptor, copy, "Set FX Controller");

            result.NewControllerPath = dstPath;
            result.Success = true;
            return result;
        }

        public static FxReplaceResult ReplaceDirect(
            AnimatorController controller,
            List<(FxExpressionEntry entry, FxMapping mapping)> jobs,
            bool backup)
        {
            var result = new FxReplaceResult();

            string ctrlPath = AssetDatabase.GetAssetPath(controller);
            if (string.IsNullOrEmpty(ctrlPath))
            {
                result.Error = DenEmoLoc.T("ui.fx.err.notAsset");
                return result;
            }

            if (backup)
            {
                string dir        = Path.GetDirectoryName(ctrlPath).Replace('\\', '/');
                string backupDir  = dir + "/_backups";
                string timestamp  = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = backupDir + "/" + Path.GetFileNameWithoutExtension(ctrlPath) + "_" + timestamp + ".controller";
                if (!Directory.Exists(backupDir))
                {
                    try { Directory.CreateDirectory(backupDir); AssetDatabase.Refresh(); } catch { }
                }
                if (AssetDatabase.CopyAsset(ctrlPath, backupPath))
                    result.BackupPath = backupPath;
            }

            foreach (var (entry, mapping) in jobs)
            {
                foreach (var slot in entry.Slots)
                {
                    if (mapping.DisabledSlotKeys.Contains(slot.SlotKey)) continue;
                    WriteSlot(slot, mapping.NewClip, withUndo: true);
                    result.ReplacedCount++;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            result.Success = true;
            return result;
        }

        private static void WriteSlot(FxMotionSlot slot, AnimationClip newClip, bool withUndo)
        {
            if (slot.Tree != null)
            {
                if (withUndo) Undo.RecordObject(slot.Tree, "Replace FX Motion");
                // ChildMotion は struct のため配列ごと再代入する必要がある
                var children = slot.Tree.children;
                if (slot.ChildIndex < 0 || slot.ChildIndex >= children.Length) return;
                children[slot.ChildIndex].motion = newClip;
                slot.Tree.children = children;
                EditorUtility.SetDirty(slot.Tree);
            }
            else if (slot.State != null)
            {
                if (withUndo) Undo.RecordObject(slot.State, "Replace FX Motion");
                slot.State.motion = newClip;
                EditorUtility.SetDirty(slot.State);
            }
        }
    }
}
