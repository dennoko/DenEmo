using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using DenEmo.Models;

namespace DenEmo.Core
{
    /// <summary>
    /// AnimatorController 群を解析し、各 FxMotionSlot が「どのハンドジェスチャー文脈で再生され得るか」を推定する。
    /// GestureLeft / GestureRight の直接条件だけでなく、VRCAvatarParameterDriver（Set）を介した
    /// 間接制御（中間パラメータ経由）も固定点反復で解決する。VRChat SDK 型へは直接依存せず、
    /// ドライバーの読み出しはリフレクションで行う（VrcAvatarReflection と同方針）。
    /// </summary>
    public static class GestureTraceUtil
    {
        /// <summary>GestureLeft / GestureRight の値（0〜7）に対応するジェスチャー名。VRChat 慣例に合わせ英語固定。</summary>
        public static readonly string[] GestureNames =
            { "Neutral", "Fist", "HandOpen", "FingerPoint", "Victory", "RockNRoll", "HandGun", "ThumbsUp" };

        // 文脈爆発を防ぐ上限（1 トランジション結果・1 ステートあたり）。
        private const int MaxCombos = 16;
        private const int MaxPasses = 8;

        /// <summary>ドライバー Set の事実（このステートがアクティブなら name = value に設定される）。</summary>
        private struct Setter
        {
            public AnimatorState State;
            public float Value;
        }

        /// <summary>収集した 1 本のトランジション（条件 → 遷移先ステート）。</summary>
        private class TransitionRecord
        {
            public AnimatorCondition[] Conditions;
            public AnimatorState Destination;
        }

        /// <summary>controllers 全体を解析し、entries の各スロットに GestureHints を充填する。</summary>
        public static void PopulateGestureHints(List<AnimatorController> controllers, List<FxExpressionEntry> entries)
        {
            if (entries == null) return;

            // ── 1. トランジション収集 & ステート列挙（全コントローラー横断・サブステートマシン再帰） ──
            var transitions = new List<TransitionRecord>();
            var allStates   = new HashSet<AnimatorState>();

            if (controllers != null)
            {
                foreach (var ctrl in controllers)
                {
                    if (ctrl == null) continue;
                    var layers = ctrl.layers;
                    if (layers == null) continue;
                    foreach (var layer in layers)
                    {
                        var sm = layer != null ? layer.stateMachine : null;
                        if (sm == null) continue;
                        CollectStateMachine(sm, transitions, allStates, new HashSet<AnimatorStateMachine>());
                    }
                }
            }

            // ── 2. ドライバー事実表（VRCAvatarParameterDriver の Set のみ） ──
            var setters = BuildSetterTable(allStates);

            // ── 3. 固定点反復（各ステートがアクティブになり得るジェスチャー文脈を収束） ──
            var hints = new Dictionary<AnimatorState, HashSet<FxGestureHint>>();
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                bool changed = false;
                foreach (var tr in transitions)
                {
                    var results = EvalTransition(tr.Conditions, setters, hints);
                    if (results == null || results.Count == 0) continue;

                    if (!hints.TryGetValue(tr.Destination, out var set))
                    {
                        set = new HashSet<FxGestureHint>();
                        hints[tr.Destination] = set;
                    }
                    foreach (var r in results)
                    {
                        if (set.Count >= MaxCombos) break;
                        if (r.IsEmpty) continue;
                        if (set.Add(r)) changed = true;
                    }
                }
                if (!changed) break;
            }

            // ── 4. スロットへ反映（1D BlendTree 補正込み） ──
            FillSlots(entries, hints, setters);
        }

        // ── 収集 ──────────────────────────────────────────────────────────────

        private static void CollectStateMachine(
            AnimatorStateMachine sm,
            List<TransitionRecord> transitions,
            HashSet<AnimatorState> allStates,
            HashSet<AnimatorStateMachine> visited)
        {
            if (sm == null || !visited.Add(sm)) return; // 循環・重複防止

            AddTransitions(sm.anyStateTransitions, transitions);
            AddTransitions(sm.entryTransitions,    transitions);

            var states = sm.states;
            if (states != null)
            {
                foreach (var cs in states)
                {
                    var state = cs.state;
                    if (state == null) continue;
                    allStates.Add(state);
                    AddTransitions(state.transitions, transitions);
                }
            }

            var children = sm.stateMachines;
            if (children != null)
                foreach (var child in children)
                    CollectStateMachine(child.stateMachine, transitions, allStates, visited);
        }

        // AnimatorStateTransition / AnimatorTransition はいずれも AnimatorTransitionBase 派生（配列共変で受ける）。
        private static void AddTransitions(AnimatorTransitionBase[] arr, List<TransitionRecord> transitions)
        {
            if (arr == null) return;
            foreach (var t in arr)
            {
                if (t == null) continue;
                var dest = ResolveDestination(t);
                if (dest == null) continue; // 遷移先ステートが特定できないものは対象外
                transitions.Add(new TransitionRecord { Conditions = t.conditions, Destination = dest });
            }
        }

        private static AnimatorState ResolveDestination(AnimatorTransitionBase t)
        {
            if (t.destinationState != null) return t.destinationState;
            // サブステートマシンへの遷移は defaultState に帰属（1 段のみ）
            if (t.destinationStateMachine != null) return t.destinationStateMachine.defaultState;
            return null;
        }

        // ── ドライバー事実表 ──────────────────────────────────────────────────

        private static Dictionary<string, List<Setter>> BuildSetterTable(HashSet<AnimatorState> states)
        {
            var setters = new Dictionary<string, List<Setter>>();
            foreach (var state in states)
            {
                if (state == null) continue;
                var behaviours = state.behaviours;
                if (behaviours == null) continue;

                foreach (var b in behaviours)
                {
                    if (b == null) continue;
                    if (b.GetType().Name != "VRCAvatarParameterDriver") continue;

                    // SDK バージョン差異に耐えるため、1 ビヘイビアぶんの読み出しを try/catch で保護
                    try
                    {
                        var list = GetMemberValue(b, "parameters") as System.Collections.IList;
                        if (list == null) continue;

                        foreach (var p in list)
                        {
                            if (p == null) continue;

                            var typeVal = GetMemberValue(p, "type");
                            // 静的解決可能なのは Set のみ（Add / Random / Copy は対象外）
                            if (typeVal == null || typeVal.ToString() != "Set") continue;

                            var name = GetMemberValue(p, "name") as string;
                            if (string.IsNullOrEmpty(name)) continue;

                            var valObj = GetMemberValue(p, "value");
                            if (valObj == null) continue;
                            float value = Convert.ToSingle(valObj);

                            if (!setters.TryGetValue(name, out var l))
                            {
                                l = new List<Setter>();
                                setters[name] = l;
                            }
                            l.Add(new Setter { State = state, Value = value });
                        }
                    }
                    catch { /* SDK 差異は静かに無視 */ }
                }
            }
            return setters;
        }

        /// <summary>フィールド優先・プロパティフォールバックでメンバー値を読む。</summary>
        private static object GetMemberValue(object obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var f = t.GetField(name);
            if (f != null) return f.GetValue(obj);
            var pr = t.GetProperty(name);
            if (pr != null) return pr.GetValue(obj);
            return null;
        }

        // ── 反復本体 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 1 トランジションの条件からジェスチャー文脈集合を導出する。
        /// null = このトランジションは今回対象外（未解決・ジェスチャー非依存・矛盾）。
        /// </summary>
        private static List<FxGestureHint> EvalTransition(
            AnimatorCondition[] conditions,
            Dictionary<string, List<Setter>> setters,
            Dictionary<AnimatorState, HashSet<FxGestureHint>> hints)
        {
            var direct = new FxGestureHint(-1, -1);
            bool hasDirect = false;
            var derivedSets = new List<HashSet<FxGestureHint>>();

            if (conditions != null)
            {
                foreach (var c in conditions)
                {
                    string param = c.parameter;

                    if (param == "GestureLeft" || param == "GestureRight")
                    {
                        // 直接条件は Equals・範囲 0〜7 のみ制約とみなす
                        if (c.mode != AnimatorConditionMode.Equals) continue;
                        int g = Mathf.RoundToInt(c.threshold);
                        if (g < 0 || g > 7) continue;

                        if (param == "GestureLeft")
                        {
                            if (direct.Left >= 0 && direct.Left != g) return null; // 同ハンドに矛盾する値 → 棄却
                            direct.Left = g;
                        }
                        else
                        {
                            if (direct.Right >= 0 && direct.Right != g) return null;
                            direct.Right = g;
                        }
                        hasDirect = true;
                    }
                    else if (setters.TryGetValue(param, out var setterList))
                    {
                        // ドライバーで Set されるパラメータへの条件（間接制御）
                        // Equals / If / IfNot 以外（Greater / Less / NotEqual）はこの条件ごと無視
                        if (c.mode != AnimatorConditionMode.Equals &&
                            c.mode != AnimatorConditionMode.If &&
                            c.mode != AnimatorConditionMode.IfNot)
                            continue;

                        var contexts = new HashSet<FxGestureHint>();
                        foreach (var s in setterList)
                        {
                            bool match;
                            switch (c.mode)
                            {
                                case AnimatorConditionMode.Equals: match = Mathf.Approximately(s.Value, c.threshold); break;
                                case AnimatorConditionMode.If:     match = s.Value != 0f; break; // bool パラメータ
                                default:                           match = s.Value == 0f; break; // IfNot
                            }
                            if (!match) continue;
                            if (hints.TryGetValue(s.State, out var sctx))
                                foreach (var h in sctx) contexts.Add(h);
                        }

                        // 文脈が空 = このトランジションは今回未解決（次反復で再評価）
                        if (contexts.Count == 0) return null;
                        derivedSets.Add(contexts);
                    }
                    // 未知パラメータ・その他 → 無視
                }
            }

            // 直接条件も間接文脈も無い → ジェスチャー非依存
            if (!hasDirect && derivedSets.Count == 0) return null;

            // direct を起点に derivedSets を直積マージ
            var results = new List<FxGestureHint> { direct };
            foreach (var set in derivedSets)
            {
                var merged = new List<FxGestureHint>();
                var seen = new HashSet<FxGestureHint>();
                foreach (var r in results)
                {
                    foreach (var d in set)
                    {
                        if (TryMerge(r, d, out var m) && seen.Add(m))
                        {
                            merged.Add(m);
                            if (merged.Count >= MaxCombos) break;
                        }
                    }
                    if (merged.Count >= MaxCombos) break;
                }
                results = merged;
                if (results.Count == 0) break; // すべて矛盾で消えた
            }
            return results;
        }

        /// <summary>2 つの hint をマージ（同ハンドに異なる値があれば矛盾 → false）。</summary>
        private static bool TryMerge(FxGestureHint a, FxGestureHint b, out FxGestureHint m)
        {
            m = new FxGestureHint(-1, -1);
            if (a.Left  >= 0 && b.Left  >= 0 && a.Left  != b.Left)  return false;
            if (a.Right >= 0 && b.Right >= 0 && a.Right != b.Right) return false;
            m.Left  = a.Left  >= 0 ? a.Left  : b.Left;
            m.Right = a.Right >= 0 ? a.Right : b.Right;
            return true;
        }

        // ── スロット反映 ──────────────────────────────────────────────────────

        private static void FillSlots(
            List<FxExpressionEntry> entries,
            Dictionary<AnimatorState, HashSet<FxGestureHint>> hints,
            Dictionary<string, List<Setter>> setters)
        {
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                foreach (var slot in entry.Slots)
                {
                    if (slot == null) continue;

                    var working = new HashSet<FxGestureHint>();
                    if (slot.State != null && hints.TryGetValue(slot.State, out var stateHints))
                        foreach (var h in stateHints) working.Add(h);

                    RefineByBlendTree(slot, hints, setters, ref working);

                    slot.GestureHints.Clear();
                    var seen = new HashSet<FxGestureHint>();
                    foreach (var h in working)
                        if (!h.IsEmpty && seen.Add(h))
                            slot.GestureHints.Add(h);
                }
            }
        }

        private static void RefineByBlendTree(
            FxMotionSlot slot,
            Dictionary<AnimatorState, HashSet<FxGestureHint>> hints,
            Dictionary<string, List<Setter>> setters,
            ref HashSet<FxGestureHint> working)
        {
            var tree = slot.Tree;
            if (tree == null || tree.blendType != BlendTreeType.Simple1D) return;

            var children = tree.children;
            if (children == null || slot.ChildIndex < 0 || slot.ChildIndex >= children.Length) return;

            string p = tree.blendParameter;
            float threshold = children[slot.ChildIndex].threshold;
            int g = Mathf.RoundToInt(threshold);

            if (p == "GestureLeft" && g >= 0 && g <= 7)
            {
                if (working.Count == 0) working.Add(new FxGestureHint(g, -1));
                else
                {
                    var updated = new HashSet<FxGestureHint>();
                    foreach (var h in working) updated.Add(new FxGestureHint(g, h.Right)); // Left を上書き
                    working = updated;
                }
            }
            else if (p == "GestureRight" && g >= 0 && g <= 7)
            {
                if (working.Count == 0) working.Add(new FxGestureHint(-1, g));
                else
                {
                    var updated = new HashSet<FxGestureHint>();
                    foreach (var h in working) updated.Add(new FxGestureHint(h.Left, g)); // Right を上書き
                    working = updated;
                }
            }
            else if (setters.TryGetValue(p, out var setterList))
            {
                // BlendTree の子が「ドライバーで駆動される値」でゲートされている場合はそちらを採用（より具体的）
                var contexts = new HashSet<FxGestureHint>();
                foreach (var s in setterList)
                    if (Mathf.Approximately(s.Value, threshold) &&
                        hints.TryGetValue(s.State, out var sctx))
                        foreach (var h in sctx) contexts.Add(h);
                if (contexts.Count > 0) working = contexts; // ステート文脈を置き換え
            }
            // GestureLeftWeight / GestureRightWeight はアナログ → 上記いずれにも一致せず何もしない
        }
    }
}
