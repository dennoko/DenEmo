using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    public static class AnimationExporter
    {
        public static string SaveAnimationClip(ShapeKeyModel model, string saveFolder, out string generatedPath, bool autoBackup = false)
        {
            generatedPath = null;
            if (model.TargetSkinnedMesh == null) return DenEmoLoc.T("dlg.apply.noTarget");

            if (!Directory.Exists(saveFolder))
            {
                try { Directory.CreateDirectory(saveFolder); } catch { }
            }

            string defaultName = model.TargetObject ? model.TargetObject.name + "_blendshape" : DenEmoLoc.T("save.panel.defaultName");
            string path = EditorUtility.SaveFilePanelInProject(DenEmoLoc.T("save.panel.title"), defaultName + ".anim", "anim", DenEmoLoc.T("save.panel.hint"), saveFolder);

            if (string.IsNullOrEmpty(path)) return null;

            var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            AnimationClip clip;
            bool isOverwrite;

            if (existingClip != null)
            {
                if (autoBackup)
                {
                    string dir        = Path.GetDirectoryName(path).Replace('\\', '/');
                    string backupDir  = dir + "/_backups";
                    string timestamp  = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string backupPath = backupDir + "/" + Path.GetFileNameWithoutExtension(path) + "_" + timestamp + ".anim";
                    if (!Directory.Exists(backupDir))
                    {
                        try { Directory.CreateDirectory(backupDir); AssetDatabase.Refresh(); } catch { }
                    }
                    AssetDatabase.CopyAsset(path, backupPath);
                }
                clip = existingClip;
                clip.ClearCurves();
                clip.frameRate = 60;
                isOverwrite = true;
            }
            else
            {
                clip = new AnimationClip { frameRate = 60 };
                isOverwrite = false;
            }

            WriteItemsToClip(model, clip);

            if (isOverwrite)
                EditorUtility.SetDirty(clip);
            else
                AssetDatabase.CreateAsset(clip, path);

            AssetDatabase.SaveAssets();

            var asset = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }

            generatedPath = path;
            return null;
        }

        public static string SaveAnimationClipToPath(ShapeKeyModel model, string path, out string generatedPath, bool autoBackup = false)
        {
            generatedPath = null;
            if (model.TargetSkinnedMesh == null) return DenEmoLoc.T("dlg.apply.noTarget");
            if (string.IsNullOrEmpty(path))      return DenEmoLoc.T("dlg.apply.noClip");

            var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            AnimationClip clip;

            if (existingClip != null)
            {
                if (autoBackup)
                {
                    string dir       = Path.GetDirectoryName(path).Replace('\\', '/');
                    string backupDir = dir + "/_backups";
                    string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string backupPath = backupDir + "/" + Path.GetFileNameWithoutExtension(path) + "_" + timestamp + ".anim";
                    if (!Directory.Exists(backupDir))
                    {
                        try { Directory.CreateDirectory(backupDir); AssetDatabase.Refresh(); } catch { }
                    }
                    AssetDatabase.CopyAsset(path, backupPath);
                }
                clip = existingClip;
                clip.ClearCurves();
                clip.frameRate = 60;
            }
            else
            {
                clip = new AnimationClip { frameRate = 60 };
            }

            WriteItemsToClip(model, clip);

            if (existingClip != null)
            {
                EditorUtility.SetDirty(clip);
            }
            else
            {
                string dir2 = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir2))
                {
                    try { Directory.CreateDirectory(dir2); } catch { }
                }
                AssetDatabase.CreateAsset(clip, path);
            }
            AssetDatabase.SaveAssets();

            var asset = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }

            generatedPath = path;
            return null;
        }

        // 各アイテムの OwnerSmr / SmrPath を使って複数メッシュ分をまとめてクリップに書き込む
        private static void WriteItemsToClip(ShapeKeyModel model, AnimationClip clip)
        {
            foreach (var item in model.Items)
            {
                if (!item.IsIncluded || item.IsVrcShape || item.IsLipSyncShape) continue;

                var smr = item.OwnerSmr ?? model.TargetSkinnedMesh;
                if (smr == null) continue;

                string smrPath = !string.IsNullOrEmpty(item.SmrPath)
                    ? item.SmrPath
                    : ShapeKeyModel.ComputeSmrPath(smr);

                float current = smr.GetBlendShapeWeight(item.Index);
                string prop   = "blendShape." + item.Name;

                var binding = new EditorCurveBinding
                {
                    type         = typeof(SkinnedMeshRenderer),
                    path         = smrPath,
                    propertyName = prop,
                };

                var key = new Keyframe[2];
                key[0] = new Keyframe(0f,      current);
                key[1] = new Keyframe(0.0001f, current);
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(key));
            }
        }

        public static string ApplyAnimationToMesh(AnimationClip clip, ShapeKeyModel model)
        {
            if (clip == null)                        return DenEmoLoc.T("dlg.apply.noClip");
            if (model.TargetSkinnedMesh == null)     return DenEmoLoc.T("dlg.apply.noTarget");

            var bindings = AnimationUtility.GetCurveBindings(clip);
            bool applied = false;

            foreach (var b in bindings)
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;

                var   curve     = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) continue;

                float value     = curve.Evaluate(0f);
                string shapeName = b.propertyName.Substring("blendShape.".Length);
                int idx = model.TargetSkinnedMesh.sharedMesh.GetBlendShapeIndex(shapeName);

                if (idx >= 0)
                {
                    model.TargetSkinnedMesh.SetBlendShapeWeight(idx, value);
                    if (idx < model.Items.Count)
                        model.Items[idx].Value = value;
                    applied = true;
                }
            }

            return applied ? "SUCCESS" : DenEmoLoc.T("dlg.apply.noneFound");
        }
    }
}
