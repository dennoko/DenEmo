using System.Collections.Generic;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    public static class LipSyncExclusionRule
    {
        public static void ApplyExclusion(SkinnedMeshRenderer targetSkinnedMesh, List<ShapeKeyItem> items)
        {
            if (targetSkinnedMesh == null) return;
            
            Component descriptor = null;
            var tr = targetSkinnedMesh.transform;
            while (tr != null && descriptor == null)
            {
                var comps = tr.GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var t = c.GetType();
                    var name = t.Name;
                    var full = t.FullName;
                    
                    if (name == "VRC_AvatarDescriptor" || name == "VRCAvatarDescriptor" || 
                        (full != null && (full.Contains("VRC_AvatarDescriptor") || full.Contains("VRCAvatarDescriptor"))))
                    {
                        descriptor = c; 
                        break;
                    }
                }
                tr = tr.parent;
            }

            if (descriptor == null) return;

            var dtype = descriptor.GetType();
            var lipSyncStyleProp = dtype.GetProperty("lipSync");
            var lipSyncStyleField = dtype.GetField("lipSync");
            object lipSyncVal = null;
            
            if (lipSyncStyleProp != null) lipSyncVal = lipSyncStyleProp.GetValue(descriptor, null);
            else if (lipSyncStyleField != null) lipSyncVal = lipSyncStyleField.GetValue(descriptor);
            
            string lipSyncStyleName = lipSyncVal != null ? lipSyncVal.ToString() : null;
            
            if (string.IsNullOrEmpty(lipSyncStyleName)) return;
            if (!lipSyncStyleName.Contains("VisemeBlendShape")) return;

            var smrProp = dtype.GetProperty("VisemeSkinnedMesh");
            var smrField = dtype.GetField("VisemeSkinnedMesh");
            var namesProp = dtype.GetProperty("VisemeBlendShapes");
            var namesField = dtype.GetField("VisemeBlendShapes");
            
            if (smrProp == null && smrField == null) return;
            if (namesProp == null && namesField == null) return;

            var namesObj = namesProp != null ? namesProp.GetValue(descriptor, null) : namesField.GetValue(descriptor);
            var names = namesObj as string[];
            
            if (names == null || names.Length == 0) return;

            var set = new HashSet<string>(names);
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.Name) && set.Contains(item.Name))
                {
                    item.IsLipSyncShape = true;
                }
            }
        }
    }
}
