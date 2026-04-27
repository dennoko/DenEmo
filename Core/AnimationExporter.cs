using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    public static class AnimationExporter
    {
        private static string GetRelativePath(Transform target, Transform root)
        {
            if (target == root) return "";
            var parts = new List<string>();
            var t = target;
            while (t != null && t != root)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

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

            string currentSmrPath = GetRelativePath(model.TargetSkinnedMesh.transform, model.TargetSkinnedMesh.transform.root);

            var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            AnimationClip clip;
            bool isOverwrite;

            if (existingClip != null)
            {
                // 上書き前に自動バックアップ
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

            foreach (var item in model.Items)
            {
                if (!item.IsIncluded || item.IsVrcShape || item.IsLipSyncShape)
                    continue;

                float current = model.TargetSkinnedMesh.GetBlendShapeWeight(item.Index);
                string prop = "blendShape." + item.Name;
                
                var binding = new EditorCurveBinding
                {
                    type = typeof(SkinnedMeshRenderer),
                    path = currentSmrPath,
                    propertyName = prop
                };

                var key = new Keyframe[2];
                key[0] = new Keyframe(0f, current);
                key[1] = new Keyframe(0.0001f, current);
                var curve = new AnimationCurve(key);
                
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            if (isOverwrite)
            {
                EditorUtility.SetDirty(clip);
            }
            else
            {
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
            return null; // indicates success
        }

        public static string SaveAnimationClipToPath(ShapeKeyModel model, string path, out string generatedPath, bool autoBackup = false)
        {
            generatedPath = null;
            if (model.TargetSkinnedMesh == null) return DenEmoLoc.T("dlg.apply.noTarget");
            if (string.IsNullOrEmpty(path)) return DenEmoLoc.T("dlg.apply.noClip");

            string currentSmrPath = GetRelativePath(model.TargetSkinnedMesh.transform, model.TargetSkinnedMesh.transform.root);

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

            foreach (var item in model.Items)
            {
                if (!item.IsIncluded || item.IsVrcShape || item.IsLipSyncShape)
                    continue;

                float current = model.TargetSkinnedMesh.GetBlendShapeWeight(item.Index);
                string prop = "blendShape." + item.Name;

                var binding = new EditorCurveBinding
                {
                    type = typeof(SkinnedMeshRenderer),
                    path = currentSmrPath,
                    propertyName = prop
                };

                var key = new Keyframe[2];
                key[0] = new Keyframe(0f, current);
                key[1] = new Keyframe(0.0001f, current);
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(key));
            }

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

        /// <summary>
        /// Saves a multi-frame AnimationClip that has already been built in memory to the given asset path.
        /// If the clip is already the asset at that path it is simply marked dirty.
        /// If a different clip already exists at that path its curves are replaced.
        /// If no asset exists yet one is created.
        /// Returns null on success, an error message string on failure.
        /// </summary>
        public static string SaveMultiFrameClip(AnimationClipModel clipModel, string path)
        {
            if (clipModel == null || clipModel.Clip == null) return DenEmoLoc.T("dlg.apply.noClip");
            if (string.IsNullOrEmpty(path))                  return DenEmoLoc.T("dlg.apply.noClip");

            var clip = clipModel.Clip;
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (existing == clip)
            {
                // The clip is already the asset — just persist it.
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
            }
            else if (existing != null)
            {
                // A different asset lives at that path — overwrite its curves.
                Undo.RecordObject(existing, "Save Animation Clip");
                existing.ClearCurves();
                existing.frameRate = clip.frameRate;
                ApplyCurvesWithOptionalLoop(clip, existing, clipModel);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
            }
            else
            {
                // No asset yet — create one.
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); } catch { }
                }
                
                var newClip = new AnimationClip { frameRate = clip.frameRate };
                ApplyCurvesWithOptionalLoop(clip, newClip, clipModel);
                
                AssetDatabase.CreateAsset(newClip, path);
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

        private static void ApplyCurvesWithOptionalLoop(AnimationClip source, AnimationClip target, AnimationClipModel clipModel)
        {
            foreach (var b in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, b);
                if (curve != null)
                {
                    if (clipModel.SmoothLoopEnabled && curve.keys.Length > 0 && clipModel.ClipLength > 0f)
                    {
                        var loopCurve = new AnimationCurve(curve.keys);
                        float valZero = loopCurve.Evaluate(0f);
                        float tol = clipModel.FPS > 0f ? 0.5f / clipModel.FPS : 0.01f;
                        
                        int endIdx = -1;
                        for (int i = 0; i < loopCurve.keys.Length; i++)
                            if (Mathf.Abs(loopCurve.keys[i].time - clipModel.ClipLength) <= tol) { endIdx = i; break; }
                            
                        if (endIdx >= 0)
                        {
                            loopCurve.RemoveKey(endIdx);
                        }
                        
                        int newIdx = loopCurve.AddKey(new Keyframe(clipModel.ClipLength, valZero));
                        
                        int startIdx = -1;
                        for (int i = 0; i < loopCurve.keys.Length; i++)
                            if (Mathf.Abs(loopCurve.keys[i].time) <= tol) { startIdx = i; break; }
                            
                        if (startIdx >= 0)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(loopCurve, newIdx, AnimationUtility.GetKeyLeftTangentMode(loopCurve, startIdx));
                            AnimationUtility.SetKeyRightTangentMode(loopCurve, newIdx, AnimationUtility.GetKeyRightTangentMode(loopCurve, startIdx));
                        }
                        
                        AnimationUtility.SetEditorCurve(target, b, loopCurve);
                    }
                    else
                    {
                        AnimationUtility.SetEditorCurve(target, b, curve);
                    }
                }
            }
        }

        public static string ApplyAnimationToMesh(AnimationClip clip, ShapeKeyModel model)
        {
            if (clip == null) return DenEmoLoc.T("dlg.apply.noClip");
            if (model.TargetSkinnedMesh == null) return DenEmoLoc.T("dlg.apply.noTarget");

            var bindings = AnimationUtility.GetCurveBindings(clip);
            bool applied = false;
            
            foreach (var b in bindings)
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;
                
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) continue;
                
                float value = curve.Evaluate(0f);
                string shapeName = b.propertyName.Substring("blendShape.".Length);
                int idx = model.TargetSkinnedMesh.sharedMesh.GetBlendShapeIndex(shapeName);
                
                if (idx >= 0)
                {
                    model.TargetSkinnedMesh.SetBlendShapeWeight(idx, value);
                    if (idx < model.Items.Count)
                    {
                        model.Items[idx].Value = value;
                    }
                    applied = true;
                }
            }
            
            if (applied)
            {
                return "SUCCESS";
            }
            else
            {
                return DenEmoLoc.T("dlg.apply.noneFound");
            }
        }
    }
}
