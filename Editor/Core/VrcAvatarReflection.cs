using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DenEmo.Core
{
    /// <summary>
    /// VRChat SDK の型へ直接依存せず、リフレクションのみで VRCAvatarDescriptor を扱うヘルパ。
    /// SDK 未導入プロジェクトでもコンパイル可能に保つ（LipSyncExclusionRule と同方針）。
    /// baseAnimationLayers は struct 配列のため、要素の書き換えは box 化 → フィールドセット →
    /// Array.SetValue → 配列ごとフィールドへ書き戻し、の手順が必要。
    /// </summary>
    public static class VrcAvatarReflection
    {
        /// <summary>tr から親方向へ辿って VRCAvatarDescriptor（または旧 VRC_AvatarDescriptor）を探す。</summary>
        public static Component FindDescriptor(Transform tr)
        {
            while (tr != null)
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
                        return c;
                    }
                }
                tr = tr.parent;
            }
            return null;
        }

        /// <summary>
        /// baseAnimationLayers から FX レイヤーのコントローラーを取得する。
        /// 戻り値 false = フィールド構成が読めなかった（SDK バージョン差異など）。
        /// FX レイヤーが isDefault / コントローラー未設定の場合は true を返しつつ controller = null。
        /// </summary>
        public static bool TryGetFxController(Component descriptor, out RuntimeAnimatorController controller, out bool isDefault)
        {
            controller = null;
            isDefault  = true;
            if (descriptor == null) return false;

            var arr = GetBaseAnimationLayers(descriptor);
            if (arr == null) return false;

            for (int i = 0; i < arr.Length; i++)
            {
                object elem = arr.GetValue(i);
                if (elem == null) continue;
                if (!IsFxLayer(elem)) continue;

                var elemType = elem.GetType();
                var defField = elemType.GetField("isDefault");
                var ctrlField = elemType.GetField("animatorController");
                if (defField != null)  isDefault  = defField.GetValue(elem) is bool b && b;
                if (ctrlField != null) controller = ctrlField.GetValue(elem) as RuntimeAnimatorController;
                return true;
            }
            return false;
        }

        /// <summary>
        /// FX レイヤーのコントローラー参照を差し替える（Undo 対応）。
        /// isDefault を false にし、customizeAnimationLayers フィールドが存在すれば true にする。
        /// </summary>
        public static bool SetFxController(Component descriptor, RuntimeAnimatorController controller, string undoLabel)
        {
            if (descriptor == null) return false;

            var dtype = descriptor.GetType();
            var layersField = dtype.GetField("baseAnimationLayers");
            var arr = layersField?.GetValue(descriptor) as Array;
            if (arr == null) return false;

            for (int i = 0; i < arr.Length; i++)
            {
                object elem = arr.GetValue(i);
                if (elem == null || !IsFxLayer(elem)) continue;

                Undo.RecordObject(descriptor, undoLabel);

                var elemType = elem.GetType();
                elemType.GetField("animatorController")?.SetValue(elem, controller);
                elemType.GetField("isDefault")?.SetValue(elem, false);
                arr.SetValue(elem, i);
                layersField.SetValue(descriptor, arr);

                // SDK バージョンによっては存在しないため null なら黙ってスキップ
                var customizeField = dtype.GetField("customizeAnimationLayers");
                if (customizeField != null && customizeField.FieldType == typeof(bool))
                    customizeField.SetValue(descriptor, true);

                EditorUtility.SetDirty(descriptor);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 全プレイアブルレイヤー（baseAnimationLayers、存在すれば specialAnimationLayers も）の
        /// コントローラーを列挙する。ジェスチャー追跡でドライバーを FX 以外のレイヤーからも探すために使う。
        /// null・重複は除外。読めなければ空リストのまま返す。
        /// </summary>
        public static void GetAllLayerControllers(Component descriptor, List<RuntimeAnimatorController> result)
        {
            if (descriptor == null || result == null) return;

            var dtype = descriptor.GetType();
            string[] fieldNames = { "baseAnimationLayers", "specialAnimationLayers" };

            foreach (var fieldName in fieldNames)
            {
                var field = dtype.GetField(fieldName);
                var arr = field?.GetValue(descriptor) as Array;
                if (arr == null) continue;

                for (int i = 0; i < arr.Length; i++)
                {
                    object elem = arr.GetValue(i);
                    if (elem == null) continue;

                    var ctrlField = elem.GetType().GetField("animatorController");
                    var controller = ctrlField?.GetValue(elem) as RuntimeAnimatorController;
                    if (controller == null) continue;
                    if (result.Contains(controller)) continue;

                    result.Add(controller);
                }
            }
        }

        private static Array GetBaseAnimationLayers(Component descriptor)
        {
            var field = descriptor.GetType().GetField("baseAnimationLayers");
            return field?.GetValue(descriptor) as Array;
        }

        private static bool IsFxLayer(object layerElem)
        {
            var typeField = layerElem.GetType().GetField("type");
            var typeVal = typeField?.GetValue(layerElem);
            // enum 値のハードコードは SDK 更新耐性が低いため名前比較にする
            return typeVal != null && typeVal.ToString() == "FX";
        }

        /// <summary>Expression Menu のパラメータ 1 件（表情セット検出用の最小情報）。</summary>
        public struct ExpressionParamInfo
        {
            public string Name;
            public string ValueType;   // "Int" / "Float" / "Bool"
            public float DefaultValue;
        }

        /// <summary>
        /// descriptor の expressionParameters（VRCExpressionParameters）から全パラメータを読み取る。
        /// SDK 未導入・フィールド構成差異があっても例外を投げず、静かに result を空のままにする。
        /// </summary>
        public static void GetExpressionParameters(Component descriptor, List<ExpressionParamInfo> result)
        {
            if (descriptor == null || result == null) return;

            try
            {
                var exprParamsObj = GetMemberValue(descriptor, descriptor.GetType(), "expressionParameters");
                if (exprParamsObj == null) return;

                var paramsField = GetMemberValue(exprParamsObj, exprParamsObj.GetType(), "parameters");
                if (!(paramsField is System.Collections.IEnumerable list)) return;

                foreach (var elem in list)
                {
                    if (elem == null) continue;
                    var elemType = elem.GetType();

                    var nameObj = GetMemberValue(elem, elemType, "name");
                    var name = nameObj as string;
                    if (string.IsNullOrEmpty(name)) continue;

                    var valueTypeObj = GetMemberValue(elem, elemType, "valueType");
                    var defaultValueObj = GetMemberValue(elem, elemType, "defaultValue");

                    result.Add(new ExpressionParamInfo
                    {
                        Name = name,
                        ValueType = valueTypeObj?.ToString(),
                        DefaultValue = defaultValueObj != null ? Convert.ToSingle(defaultValueObj) : 0f,
                    });
                }
            }
            catch
            {
                // SDK 差異・未導入時は静かに諦める（result はそれまでに追加できた分のみ）
            }
        }

        /// <summary>フィールド優先・プロパティフォールバックでメンバー値を読む共通ヘルパ。</summary>
        private static object GetMemberValue(object instance, Type type, string memberName)
        {
            var field = type.GetField(memberName);
            if (field != null) return field.GetValue(instance);

            var prop = type.GetProperty(memberName);
            if (prop != null) return prop.GetValue(instance);

            return null;
        }
    }
}
