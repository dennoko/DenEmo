using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    /// <summary>
    /// AnimatorController を走査して、blendShape カーブを持つ AnimationClip とその参照箇所を抽出する。
    /// サブステートマシン・BlendTree（ネスト含む）を再帰的に辿る。
    /// </summary>
    public static class FxLayerScanner
    {
        /// <summary>
        /// blendShape カーブを持つ全クリップを参照箇所付きで返す。
        /// 対象メッシュとのパス一致判定は MatchesTargetMesh に持たせ、表示側でフィルタする
        /// （フィルタ切替のたびに再スキャンしないため）。
        /// </summary>
        public static List<FxExpressionEntry> Scan(AnimatorController controller, HashSet<string> targetSmrPaths)
        {
            var result  = new List<FxExpressionEntry>();
            if (controller == null) return result;

            var byClip  = new Dictionary<AnimationClip, FxExpressionEntry>();
            var layers  = controller.layers;

            for (int li = 0; li < layers.Length; li++)
            {
                var sm = layers[li].stateMachine;
                if (sm == null) continue;
                WalkStateMachine(sm, li, layers[li].name, "", targetSmrPaths, byClip, result);
            }
            return result;
        }

        private static void WalkStateMachine(
            AnimatorStateMachine sm, int layerIndex, string layerName, string smPath,
            HashSet<string> targetSmrPaths,
            Dictionary<AnimationClip, FxExpressionEntry> byClip, List<FxExpressionEntry> result)
        {
            var states = sm.states;
            for (int si = 0; si < states.Length; si++)
            {
                var state = states[si].state;
                if (state == null) continue;

                string baseKey     = layerIndex + "|" + smPath + "|" + si + "|" + state.name;
                string displayPath = string.IsNullOrEmpty(smPath)
                    ? layerName + " > " + state.name
                    : layerName + " > " + smPath.TrimStart('/').Replace("/", " > ") + " > " + state.name;

                var visitedTrees = new HashSet<BlendTree>();
                CollectMotion(state.motion, state, null, -1, baseKey, "", displayPath,
                    layerIndex, layerName, smPath, targetSmrPaths, byClip, result, visitedTrees);
            }

            foreach (var child in sm.stateMachines)
            {
                if (child.stateMachine == null) continue;
                WalkStateMachine(child.stateMachine, layerIndex, layerName,
                    smPath + "/" + child.stateMachine.name, targetSmrPaths, byClip, result);
            }
        }

        private static void CollectMotion(
            Motion motion, AnimatorState state, BlendTree parentTree, int childIndex,
            string baseKey, string treePath, string displayPath,
            int layerIndex, string layerName, string smPath,
            HashSet<string> targetSmrPaths,
            Dictionary<AnimationClip, FxExpressionEntry> byClip, List<FxExpressionEntry> result,
            HashSet<BlendTree> visitedTrees)
        {
            if (motion is AnimationClip clip)
            {
                // 同一クリップは多数のステートから参照され得る。解析（GetCurveBindings のフルコピー）は
                // 未知クリップの初回だけ行い、2 回目以降はスロット追加のみで済ませる。
                if (!byClip.TryGetValue(clip, out var entry))
                {
                    if (!TryAnalyzeClip(clip, targetSmrPaths, out bool matches, out int curveCount, out string firstPath))
                        return;

                    entry = new FxExpressionEntry
                    {
                        Clip = clip,
                        MatchesTargetMesh = matches,
                        BlendShapeCurveCount = curveCount,
                        FirstBindingPath = firstPath,
                    };
                    byClip.Add(clip, entry);
                    result.Add(entry);
                }

                entry.Slots.Add(new FxMotionSlot
                {
                    LayerIndex       = layerIndex,
                    LayerName        = layerName,
                    StateMachinePath = smPath,
                    State            = state,
                    Tree             = parentTree,
                    ChildIndex       = childIndex,
                    SlotKey          = baseKey + "|" + treePath,
                    DisplayPath      = parentTree != null ? displayPath + " (BlendTree)" : displayPath,
                });
            }
            else if (motion is BlendTree tree)
            {
                if (!visitedTrees.Add(tree)) return; // 循環防止

                var children = tree.children;
                for (int ci = 0; ci < children.Length; ci++)
                {
                    CollectMotion(children[ci].motion, state, tree, ci,
                        baseKey, treePath + "/" + ci, displayPath,
                        layerIndex, layerName, smPath, targetSmrPaths, byClip, result, visitedTrees);
                }
            }
        }

        /// <summary>クリップが blendShape カーブを持つか判定し、対象メッシュとのパス一致・カーブ数も返す。</summary>
        public static bool TryAnalyzeClip(AnimationClip clip, HashSet<string> targetSmrPaths,
            out bool matchesTarget, out int blendShapeCurveCount, out string firstBindingPath)
        {
            matchesTarget        = false;
            blendShapeCurveCount = 0;
            firstBindingPath     = null;
            if (clip == null) return false;

            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.type != typeof(SkinnedMeshRenderer)) continue;
                if (!b.propertyName.StartsWith("blendShape.")) continue;

                blendShapeCurveCount++;
                if (firstBindingPath == null) firstBindingPath = b.path;
                if (targetSmrPaths != null && targetSmrPaths.Contains(b.path))
                    matchesTarget = true;
            }
            return blendShapeCurveCount > 0;
        }

        /// <summary>ピッカー用: 指定フォルダ配下の blendShape カーブを持つ AnimationClip を列挙する。</summary>
        public static List<AnimationClip> ListCandidateClips(string folder)
        {
            var result = new List<AnimationClip>();
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return result;

            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                // バックアップフォルダは候補から除外
                if (path.Contains("/_backups/")) continue;

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;
                if (TryAnalyzeClip(clip, null, out _, out _, out _))
                    result.Add(clip);
            }
            result.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            return result;
        }
    }
}
