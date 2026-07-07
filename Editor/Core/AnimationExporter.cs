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

        /// <summary>
        /// マルチフレームモードのクリップを保存する。
        /// ループ対応が有効な場合、編集中は仮想的だったループ末尾キーを保存時に実体化し、
        /// loopTime を設定する。
        /// </summary>
        public static string SaveMultiFrameClip(AnimationClipModel clipModel, string path)
        {
            if (clipModel == null || clipModel.Clip == null) return DenEmoLoc.T("dlg.apply.noClip");
            if (string.IsNullOrEmpty(path))                  return DenEmoLoc.T("dlg.apply.noClip");

            var clip = clipModel.Clip;
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (existing == clip)
            {
                // 同一アセットへの上書き：ループキーをクリップ本体へ書き込む
                if (clipModel.SmoothLoopEnabled)
                {
                    Undo.RecordObject(clip, "Save Animation Clip");
                    MaterializeLoopKeys(clip, clipModel);
                    SetLoopTime(clip);
                    clipModel.MarkDirty();
                }
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
            }
            else
            {
                AnimationClip target;
                if (existing != null)
                {
                    Undo.RecordObject(existing, "Save Animation Clip");
                    existing.ClearCurves();
                    existing.frameRate = clip.frameRate;
                    target = existing;
                }
                else
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        try { Directory.CreateDirectory(dir); } catch { }
                    }
                    target = new AnimationClip { frameRate = clip.frameRate };
                }

                CopyCurves(clip, target, clipModel);
                if (clipModel.SmoothLoopEnabled) SetLoopTime(target);

                if (existing != null) EditorUtility.SetDirty(target);
                else                  AssetDatabase.CreateAsset(target, path);
                AssetDatabase.SaveAssets();
            }

            var asset = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
            return null;
        }

        /// <summary>全カーブをコピーする。ループ対応時はブレンドシェイプカーブにのみループ末尾キーを適用する。</summary>
        private static void CopyCurves(AnimationClip source, AnimationClip target, AnimationClipModel clipModel)
        {
            foreach (var b in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, b);
                if (curve == null) continue;

                bool isBlendShape = b.type == typeof(SkinnedMeshRenderer)
                                    && b.propertyName.StartsWith("blendShape.");

                if (clipModel.SmoothLoopEnabled && isBlendShape && curve.keys.Length > 0 && clipModel.ClipLength > 0f)
                    AnimationUtility.SetEditorCurve(target, b, clipModel.BuildLoopCurve(curve));
                else
                    AnimationUtility.SetEditorCurve(target, b, curve);
            }
        }

        /// <summary>クリップ本体のブレンドシェイプカーブにループ末尾キーを書き込む（同一アセット上書き保存用）。</summary>
        private static void MaterializeLoopKeys(AnimationClip clip, AnimationClipModel clipModel)
        {
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;

                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null || curve.keys.Length == 0) continue;

                AnimationUtility.SetEditorCurve(clip, b, clipModel.BuildLoopCurve(curve));
            }
        }

        private static void SetLoopTime(AnimationClip clip)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
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
                    applied = true;
                }
            }

            if (applied) model.SyncValuesFromMesh();
            return applied ? "SUCCESS" : DenEmoLoc.T("dlg.apply.noneFound");
        }
    }
}
