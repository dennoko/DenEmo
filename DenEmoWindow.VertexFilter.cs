using UnityEditor;
using UnityEngine;
using DenEmo.UI;

namespace DenEmo
{
    public partial class DenEmoWindow
    {
        // ─── Scene GUI ────────────────────────────────────────────────────────

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!vertexPickMode && !_vertexResultPending) return;
            if (_model.TargetSkinnedMesh == null || _model.TargetSkinnedMesh.sharedMesh == null) return;
            DenEmoTheme.Initialize();

            UpdateVertexGuideCache();
            if (vertexGuideWorldPositions == null || vertexGuideWorldPositions.Length == 0) return;

            if (vertexPickMode)
            {
                Handles.BeginGUI();
                GUI.Label(
                    new Rect(16, 16, 520, 22),
                    DenEmoLoc.T("ui.filter.vertex.guide"),
                    DenEmoTheme.SecondaryTextStyle);
                Handles.EndGUI();
            }

            var prevColor = Handles.color;
            var prevZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            int     pickedIndex = -1;
            Vector3 camPos      = sceneView.camera.transform.position;
            bool    isOrtho     = sceneView.camera.orthographic;
            Vector3 orthoDir    = -sceneView.camera.transform.forward;

            for (int i = 0; i < vertexGuideWorldPositions.Length; i++)
            {
                // 結果表示中は選択頂点のみ描画
                if (_vertexResultPending && i != selectedVertexIndex) continue;

                Vector3 world   = vertexGuideWorldPositions[i];
                Vector3 viewDir = isOrtho ? orthoDir : (camPos - world).normalized;

                if (vertexGuideWorldNormals != null && vertexGuideWorldNormals.Length > i)
                {
                    if (Vector3.Dot(vertexGuideWorldNormals[i], viewDir) <= 0f) continue;
                }

                float handleSize = HandleUtility.GetHandleSize(world);
                float size       = (0.0002f + handleSize * 0.0002f) * VertexPreviewSizeMultiplier;
                Vector3 drawPos  = world + viewDir * (handleSize * 0.002f);

                Handles.color = i == selectedVertexIndex ? VertexPreviewSelectedColor : VertexPreviewColor;

                if (vertexPickMode)
                {
                    if (Handles.Button(drawPos, Quaternion.identity, size, size, Handles.SphereHandleCap))
                    {
                        pickedIndex = i;
                        break;
                    }
                }
                else
                {
                    Handles.SphereHandleCap(0, drawPos, Quaternion.identity, size, EventType.Repaint);
                }
            }
            Handles.color = prevColor;
            Handles.zTest = prevZTest;

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
                Event.current.Use();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                vertexPickMode       = false;
                _vertexResultPending = false;
                ClearVertexGuideCache();
                SceneView.RepaintAll();
                Repaint();
                Event.current.Use();
            }
        }

        // ─── Vertex guide cache ───────────────────────────────────────────────

        private void UpdateVertexGuideCache()
        {
            if (_model.TargetSkinnedMesh == null || _model.TargetSkinnedMesh.sharedMesh == null) return;
            var mesh             = _model.TargetSkinnedMesh.sharedMesh;
            var smrTransform     = _model.TargetSkinnedMesh.transform;
            var localToWorldMat  = smrTransform.localToWorldMatrix;
            int meshId           = mesh.GetInstanceID();

            bool cacheInvalid = vertexGuideWorldPositions == null
                || vertexGuideWorldPositions.Length != mesh.vertexCount
                || vertexGuideMeshInstanceId != meshId
                || vertexGuideLocalToWorld != localToWorldMat;

            if (!cacheInvalid) return;

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

        // ─── NDMF proxy lookup ────────────────────────────────────────────────

        private static SkinnedMeshRenderer GetNDMFProxySMR(SkinnedMeshRenderer originalSmr)
        {
            if (originalSmr == null) return null;

            System.Type sessionType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "nadena.dev.ndmf")
                {
                    sessionType = assembly.GetType("nadena.dev.ndmf.preview.PreviewSession");
                    break;
                }
            }
            if (sessionType == null) return null;

            var currentProp = sessionType.GetProperty("Current",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (currentProp == null) return null;

            var session = currentProp.GetValue(null);
            if (session == null) return null;

            var mapProp = sessionType.GetProperty("OriginalToProxyRenderer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mapProp == null) return null;

            var map = mapProp.GetValue(session);
            if (map == null) return null;

            var tryGetValue = map.GetType().GetMethod("TryGetValue");
            if (tryGetValue == null) return null;

            var args    = new object[] { originalSmr, null };
            bool found  = (bool)tryGetValue.Invoke(map, args);
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
