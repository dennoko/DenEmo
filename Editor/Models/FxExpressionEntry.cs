using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace DenEmo.Models
{
    /// <summary>FX コントローラー内で 1 つの AnimationClip を参照している場所（ステート直下 or BlendTree 子）。</summary>
    public class FxMotionSlot
    {
        public int           LayerIndex;
        public string        LayerName;
        public string        StateMachinePath; // サブステートマシン階層（"" = ルート直下）
        public AnimatorState State;
        public BlendTree     Tree;             // null = ステート直下の Motion
        public int           ChildIndex;       // Tree != null のときの children インデックス

        /// <summary>
        /// 複製コントローラー側で同じ場所を再解決するための安定キー。
        /// レイヤー番号・ステートマシン階層・ステート順・BlendTree 内の子インデックス経路から構成される。
        /// </summary>
        public string SlotKey;

        /// <summary>"Left Hand > Fist" のような表示用パス。</summary>
        public string DisplayPath;
    }

    /// <summary>FX 内の 1 クリップと、その全参照箇所の集約。</summary>
    public class FxExpressionEntry
    {
        public AnimationClip Clip;
        public readonly List<FxMotionSlot> Slots = new List<FxMotionSlot>();

        /// <summary>blendShape カーブのバインディングパスが対象メッシュのいずれかと一致するか。</summary>
        public bool MatchesTargetMesh;

        /// <summary>クリップが動かすシェイプキーの数（表示用）。</summary>
        public int BlendShapeCurveCount;

        /// <summary>不一致時の代表バインディングパス（"⚠ メッシュパス不一致" 表示用）。</summary>
        public string FirstBindingPath;
    }

    /// <summary>1 エントリに対するユーザーの差し替え指定（セッション限定・永続化しない）。</summary>
    public class FxMapping
    {
        public AnimationClip NewClip;

        /// <summary>差し替えから除外する参照箇所の SlotKey（デフォルトは全箇所が対象）。</summary>
        public readonly HashSet<string> DisabledSlotKeys = new HashSet<string>();
    }
}
