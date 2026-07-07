using UnityEditor;
using UnityEngine;
using DenEmo.UI;

namespace DenEmo
{
    public partial class DenEmoWindow
    {
        // ─── Scene GUI ────────────────────────────────────────────────────────

        // SceneView オーバーレイ用の本文ラベル（IMGUI のため USS を使えず、テーマの本文色を直に持つ）
        private static GUIStyle _sceneGuideStyle;
        private static GUIStyle SceneGuideStyle
        {
            get
            {
                if (_sceneGuideStyle == null)
                {
                    _sceneGuideStyle = new GUIStyle(EditorStyles.label) { fontSize = 12 };
                    _sceneGuideStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f); // --dennoko-text-secondary
                }
                return _sceneGuideStyle;
            }
        }

        // 頂点ガイドの点半径（ワールド）計算に使う定数（旧 Handles.Button のサイズ式と同じ）
        private const float VertexPickPixelRadius = 12f; // MouseDown ピックの許容ピクセル半径

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!vertexPickMode && !_vertexResultPending) return;
            if (_model.TargetSkinnedMesh == null || _model.TargetSkinnedMesh.sharedMesh == null) return;

            UpdateVertexGuideCache();
            if (vertexGuideWorldPositions == null || vertexGuideWorldPositions.Length == 0) return;

            var evt = Event.current;

            // ピックモード中はクリックでアバター等が選択されないよう既定コントロールを奪う
            if (vertexPickMode && evt.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            Vector3 camPos   = sceneView.camera.transform.position;
            bool    isOrtho  = sceneView.camera.orthographic;
            Vector3 orthoDir = -sceneView.camera.transform.forward;

            // 描画は Repaint 時のみ。全頂点ループ＋GetHandleSize＋背面カリングを Layout/MouseMove では回さない。
            if (evt.type == EventType.Repaint)
            {
                if (vertexPickMode)
                {
                    Handles.BeginGUI();
                    GUI.Label(
                        new Rect(16, 16, 520, 22),
                        DenEmoLoc.T("ui.filter.vertex.guide"),
                        SceneGuideStyle);
                    Handles.EndGUI();
                }

                var prevColor = Handles.color;
                var prevZTest = Handles.zTest;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

                for (int i = 0; i < vertexGuideWorldPositions.Length; i++)
                {
                    // 結果表示中は選択頂点のみ描画
                    if (_vertexResultPending && i != selectedVertexIndex) continue;

                    Vector3 world   = vertexGuideWorldPositions[i];
                    Vector3 viewDir = isOrtho ? orthoDir : (camPos - world).normalized;

                    if (vertexGuideWorldNormals != null && vertexGuideWorldNormals.Length > i)
                        if (Vector3.Dot(vertexGuideWorldNormals[i], viewDir) <= 0f) continue;

                    float handleSize = HandleUtility.GetHandleSize(world);
                    float size       = (0.0002f + handleSize * 0.0002f) * VertexPreviewSizeMultiplier;
                    Vector3 drawPos  = world + viewDir * (handleSize * 0.002f);

                    Handles.color = i == selectedVertexIndex ? VertexPreviewSelectedColor : VertexPreviewColor;
                    Handles.SphereHandleCap(0, drawPos, Quaternion.identity, size, EventType.Repaint);
                }
                Handles.color = prevColor;
                Handles.zTest = prevZTest;
            }

            // ピックは MouseDown（左ボタン）時に最近傍頂点を 1 パスで探す（毎イベントの全頂点ヒットテストを廃止）。
            if (vertexPickMode && evt.type == EventType.MouseDown && evt.button == 0)
            {
                int pickedIndex = FindNearestGuideVertex(evt.mousePosition, camPos, isOrtho, orthoDir);
                if (pickedIndex >= 0)
                {
                    selectedVertexIndex      = pickedIndex;
                    vertexMovedShapeIndices  = _model.CollectShapeIndicesMovingVertex(selectedVertexIndex);
                    vertexFilterActive       = true;
                    vertexPickMode           = false;
                    _vertexResultPending     = true;
                    _vertexResultClearAt     = EditorApplication.timeSinceStartup + 2.0;
                    UpdateVisibility();
                    SceneView.RepaintAll();
                    Repaint();
                    evt.Use();
                }
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                vertexPickMode       = false;
                _vertexResultPending = false;
                ClearVertexGuideCache();
                SceneView.RepaintAll();
                Repaint();
                evt.Use();
            }
        }

        /// <summary>マウス GUI 座標に最も近い（背面カリング後の）頂点インデックスを返す。許容半径外なら -1。</summary>
        private int FindNearestGuideVertex(Vector2 mouseGuiPos, Vector3 camPos, bool isOrtho, Vector3 orthoDir)
        {
            int   best    = -1;
            float bestSqr = VertexPickPixelRadius * VertexPickPixelRadius;

            for (int i = 0; i < vertexGuideWorldPositions.Length; i++)
            {
                Vector3 world   = vertexGuideWorldPositions[i];
                Vector3 viewDir = isOrtho ? orthoDir : (camPos - world).normalized;

                if (vertexGuideWorldNormals != null && vertexGuideWorldNormals.Length > i)
                    if (Vector3.Dot(vertexGuideWorldNormals[i], viewDir) <= 0f) continue;

                Vector2 gp   = HandleUtility.WorldToGUIPoint(world);
                float   dSqr = (gp - mouseGuiPos).sqrMagnitude;
                if (dSqr < bestSqr) { bestSqr = dSqr; best = i; }
            }
            return best;
        }

        // ─── Vertex guide cache ───────────────────────────────────────────────

        private const double VertexBakeThrottleSec = 0.1; // 行列微変化による再ベイクの最短間隔
        private double _lastVertexBakeTime;

        private void UpdateVertexGuideCache()
        {
            if (_model.TargetSkinnedMesh == null || _model.TargetSkinnedMesh.sharedMesh == null) return;
            var mesh             = _model.TargetSkinnedMesh.sharedMesh;
            var smrTransform     = _model.TargetSkinnedMesh.transform;
            var localToWorldMat  = smrTransform.localToWorldMatrix;
            int meshId           = mesh.GetInstanceID();

            bool structuralInvalid = vertexGuideWorldPositions == null
                || vertexGuideWorldPositions.Length != mesh.vertexCount
                || vertexGuideMeshInstanceId != meshId;

            // 行列は完全一致ではなく許容誤差付きで比較（カメラ操作は無関係だが、アニメ・物理の微小な
            // 揺れで毎フレーム BakeMesh + 全頂点 TransformPoint が走るのを防ぐ）。
            bool matrixChanged = !MatrixApproximately(vertexGuideLocalToWorld, localToWorldMat);

            if (!structuralInvalid && !matrixChanged) return;

            // 微小変化による再ベイクはスロットルする（BakeMesh はフルスキニング CPU ベイクで重い）。
            if (!structuralInvalid)
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - _lastVertexBakeTime < VertexBakeThrottleSec) return;
            }
            _lastVertexBakeTime = EditorApplication.timeSinceStartup;

            SkinnedMeshRenderer targetToBake = _model.TargetSkinnedMesh;
            Transform           bakeTransform = smrTransform;

            SkinnedMeshRenderer proxySmr = GetNDMFProxySMR(_model.TargetSkinnedMesh);
            if (proxySmr != null)
            {
                targetToBake  = proxySmr;
                bakeTransform = proxySmr.transform;
            }

            Mesh bakedMesh = new Mesh();
            targetToBake.BakeMesh(bakedMesh);
            var vertices = bakedMesh.vertices;
            var normals  = bakedMesh.normals;

            // NDMFがメッシュのトポロジーを変更して頂点数が一致しない場合は元のメッシュにフォールバック。
            // 元の頂点インデックスとの対応が保たれるため頂点フィルタの正確性は維持される。
            if (vertices == null || vertices.Length != mesh.vertexCount)
            {
                vertices      = mesh.vertices;
                normals       = mesh.normals;
                bakeTransform = smrTransform;
            }

            DestroyImmediate(bakedMesh);

            if (vertices == null || vertices.Length == 0)
            {
                vertexGuideWorldPositions = null;
                vertexGuideWorldNormals   = null;
                return;
            }

            vertexGuideWorldPositions = new Vector3[vertices.Length];
            vertexGuideWorldNormals   = new Vector3[vertices.Length];
            bool hasNormals = normals != null && normals.Length == vertices.Length;

            for (int i = 0; i < vertices.Length; i++)
            {
                vertexGuideWorldPositions[i] = bakeTransform.TransformPoint(vertices[i]);
                if (hasNormals)
                    vertexGuideWorldNormals[i] = bakeTransform.TransformDirection(normals[i]);
            }

            vertexGuideMeshInstanceId  = meshId;
            vertexGuideLocalToWorld    = localToWorldMat;
        }

        private static bool MatrixApproximately(Matrix4x4 a, Matrix4x4 b, float eps = 1e-5f)
        {
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(a[i] - b[i]) > eps) return false;
            return true;
        }

        // ─── NDMF proxy lookup ────────────────────────────────────────────────

        // NDMF のリフレクション解決はアセンブリ全走査を伴うため 1 度だけ行いキャッシュする（NDMF 不在の結果も含む）。
        private static bool _ndmfResolved;
        private static System.Type _ndmfSessionType;
        private static System.Reflection.PropertyInfo _ndmfCurrentProp;
        private static System.Reflection.PropertyInfo _ndmfMapProp;
        private static System.Reflection.MethodInfo   _ndmfTryGetValue;

        private static void ResolveNdmfReflection()
        {
            if (_ndmfResolved) return;
            _ndmfResolved = true;

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "nadena.dev.ndmf")
                {
                    _ndmfSessionType = assembly.GetType("nadena.dev.ndmf.preview.PreviewSession");
                    break;
                }
            }
            if (_ndmfSessionType == null) return;

            _ndmfCurrentProp = _ndmfSessionType.GetProperty("Current",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            _ndmfMapProp = _ndmfSessionType.GetProperty("OriginalToProxyRenderer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        private static SkinnedMeshRenderer GetNDMFProxySMR(SkinnedMeshRenderer originalSmr)
        {
            if (originalSmr == null) return null;

            ResolveNdmfReflection();
            if (_ndmfSessionType == null || _ndmfCurrentProp == null || _ndmfMapProp == null) return null;

            var session = _ndmfCurrentProp.GetValue(null);
            if (session == null) return null;

            var map = _ndmfMapProp.GetValue(session);
            if (map == null) return null;

            if (_ndmfTryGetValue == null)
                _ndmfTryGetValue = map.GetType().GetMethod("TryGetValue");
            if (_ndmfTryGetValue == null) return null;

            var args    = new object[] { originalSmr, null };
            bool found  = (bool)_ndmfTryGetValue.Invoke(map, args);
            return found ? args[1] as SkinnedMeshRenderer : null;
        }

        // ─── Cache / filter clear ─────────────────────────────────────────────

        private void ClearVertexGuideCache()
        {
            vertexGuideWorldPositions = null;
            vertexGuideWorldNormals   = null;
            vertexGuideMeshInstanceId = 0;
            vertexGuideLocalToWorld   = Matrix4x4.zero;
        }

        private void ClearVertexFilter()
        {
            vertexPickMode          = false;
            vertexFilterActive      = false;
            selectedVertexIndex     = -1;
            vertexMovedShapeIndices = null;
            _vertexResultPending    = false;
            ClearVertexGuideCache();
            UpdateVisibility();
            SceneView.RepaintAll();
            Repaint();
        }
    }
}
