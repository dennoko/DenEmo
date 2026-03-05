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

        public static string SaveAnimationClip(ShapeKeyModel model, string saveFolder, out string generatedPath)
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

            var clip = new AnimationClip { frameRate = 60 };
            string currentSmrPath = GetRelativePath(model.TargetSkinnedMesh.transform, model.TargetSkinnedMesh.transform.root);

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

            AssetDatabase.CreateAsset(clip, path);
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
