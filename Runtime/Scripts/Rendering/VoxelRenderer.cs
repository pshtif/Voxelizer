using System;
using System.Collections.Generic;
using BinaryEgo.Voxelizer;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BinaryEgo.Voxelizer
{
    [ExecuteInEditMode]
    public class VoxelRenderer : MonoBehaviour
    {
#if UNITY_EDITOR
        public bool renderSectionMinimized = false;
        public bool editorSectionMinimized = false;
        public bool meshesSectionMinimized = false;
#endif

        static public VoxelRenderer Instance { get; private set; }
        static public int voxelCount { get; private set; }
        static public bool isDirty = false;

        public Material voxelMaterial;
        public float cullingDistance = 12;
        public ComputeShader cullingShader;

        // Bounds
        public float minX = -10;
        public float minZ = -10;
        public float maxX = 10;
        public float maxZ = 10;

        //private Plane[] cameraFrustumPlanes = new Plane[6];
        //private int cellCountX = -1;
        //private int cellCountZ = -1;
        //private bool useCells = false;
        // public float cellSizeX = 2; 
        // public float cellSizeZ = 2;

        // private List<Matrix4x4>[] _cellMatrices;
        // private NativeList<int> _visibleCells;

        public bool enableCulling;
        public float voxelScale = 1;

        [NonSerialized]
        private bool _initialized;

        [SerializeField] 
        private VoxelGroup _defaultVoxelGroup = new VoxelGroup();
        [SerializeField]
        private List<VoxelGroup> _voxelGroups = new List<VoxelGroup>();

        public List<VoxelGroup> VoxelGroups => _voxelGroups;

        public int voxelCacheSize = 1000000;

        private uint[] _indirectArgs;

        private Bounds _renderBounds;
        private Mesh _voxelMesh;
        private bool _previousCullingEnabled;

        public VoxelMeshType voxelMeshType = VoxelMeshType.CUBE;
        private VoxelMeshType _previousVoxelMeshType = VoxelMeshType.CUBE;
        public Mesh customVoxelMesh;

        // Will be separated later
        #region EDITING

        public bool enablePainting = false;
        public Color brushColor = Color.white;
        [Range(.01f,1f)]
        public float brushSize = 1;

        #endregion

        private NativeArray<Matrix4x4> _matrixArray;
        private NativeArray<Vector4> _colorArray;
        //private NativeArray<Matrix4x4> _zeroMatrixArray;

        private ComputeBuffer _colorBuffer;
        private ComputeBuffer _matrixBuffer;
        private ComputeBuffer _visibleIdBuffer;
        private ComputeBuffer _voxelIndirectBuffer;

        void Awake()
        {
            Initialize();
        }

        public bool ToggleCulling
        {
            get { return enableCulling; }

            set { enableCulling = value; }
        }

        void Initialize()
        {
            Instance = this;

            if (_initialized)
                return;

            _renderBounds = new Bounds();

            if (voxelMaterial == null)
                return;

            _previousCullingEnabled = enableCulling;

            if (!InitializeVoxelMesh())
                return;

            //_visibleCells = new NativeList<int>(Allocator.Persistent);

            voxelMaterial.SetVector("_PivotPosWS", transform.position);
            //voxelMaterial.SetVector("_BoundSize", new Vector2(transform.localScale.x, transform.localScale.z));

            //_zeroMatrixArray = new NativeArray<Matrix4x4>(voxelCacheSize, Allocator.Persistent); 
            _matrixArray = new NativeArray<Matrix4x4>(voxelCacheSize, Allocator.Persistent);
            _colorArray = new NativeArray<Vector4>(voxelCacheSize, Allocator.Persistent);

            for (int i = 0; i < voxelCacheSize; i++)
            {
                //_zeroMatrixArray[i] = Matrix4x4.zero;
                _colorArray[i] = new Vector4(1, 0, 0, 1);
                _matrixArray[i] = Matrix4x4.TRS(new Vector3(0, 1000, 0), Quaternion.identity, Vector3.one);
            }

            _colorBuffer?.Release();
            _colorBuffer = new ComputeBuffer(voxelCacheSize, sizeof(float) * 4);
            _colorBuffer.SetData(_colorArray);

            _matrixBuffer?.Release();
            _matrixBuffer = new ComputeBuffer(voxelCacheSize, sizeof(float) * 16);
            _matrixBuffer.SetData(_matrixArray);

            _visibleIdBuffer?.Release();
            _visibleIdBuffer = new ComputeBuffer(voxelCacheSize, sizeof(uint), ComputeBufferType.Append);

            voxelMaterial.SetBuffer("_colorBuffer", _colorBuffer);
            voxelMaterial.SetBuffer("_matrixBuffer", _matrixBuffer);
            voxelMaterial.SetBuffer("_visibleIdBuffer", _visibleIdBuffer);

            _voxelIndirectBuffer?.Release();
            _indirectArgs = new uint[5] { 0, 0, 0, 0, 0 };
            _voxelIndirectBuffer =
                new ComputeBuffer(1, _indirectArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

            UpdateIndirectMeshBuffer();

            if (cullingShader != null)
            {
                cullingShader.SetBuffer(0, "_matrixBuffer", _matrixBuffer);
                cullingShader.SetBuffer(0, "_visibilityBuffer", _visibleIdBuffer);
            }

            _initialized = true;
        }

        private bool InitializeVoxelMesh()
        {
            switch (voxelMeshType)
            {
                case VoxelMeshType.CUBE:
                    _voxelMesh = MeshUtils.CreateCube();
                    break;
                case VoxelMeshType.QUAD:
                    _voxelMesh = MeshUtils.CreateQuad();
                    break;
                case VoxelMeshType.CUSTOM:
                    _voxelMesh = customVoxelMesh;
                    break;
            }

            _previousVoxelMeshType = voxelMeshType;

            return _voxelMesh != null;
        }

        private void UpdateIndirectMeshBuffer()
        {
            _indirectArgs[0] = (uint)_voxelMesh.GetIndexCount(0);
            _indirectArgs[1] = (uint)voxelCacheSize;
            _indirectArgs[2] = (uint)_voxelMesh.GetIndexStart(0);
            _indirectArgs[3] = (uint)_voxelMesh.GetBaseVertex(0);
            _indirectArgs[4] = 0;

            _voxelIndirectBuffer.SetData(_indirectArgs);
        }

        private AsyncGPUReadbackRequest asyncRequest;

        public void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SceneView.duringSceneGui += OnSceneGUI;
            }
#endif
        }

        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif
            Render();
        }

        void Render(Camera p_camera = null)
        {
            Initialize();

            if (_initialized) // && ((voxelMeshType == VoxelMeshType.CUSTOM && _voxelMesh != customVoxelMesh) || voxelMeshType != _previousVoxelMeshType))
            {
                if (InitializeVoxelMesh())
                {
                    UpdateIndirectMeshBuffer();
                }
            }

            // asyncRequest.WaitForCompletion(); 
            // if (!asyncRequest.hasError)
            // {
            //     uint[] a = asyncRequest.GetData<uint>().ToArray();
            //     Debug.Log(String.Join(",", a));
            // }

            if (_voxelGroups.Count == 0)
                return;

            int index = 0;
            _voxelGroups?.ForEach(
                vg => vg.Invalidate(_matrixBuffer, _matrixArray, _colorBuffer, _colorArray, ref index));

            if (index == 0)
                return;

            if (enableCulling != _previousCullingEnabled)
            {
                if (enableCulling != _previousCullingEnabled)
                {
                    _previousCullingEnabled = enableCulling;
                    // if (!enableCulling)
                    // {
                    //     _matrixBuffer.SetData(_matrixArray);
                    //     _colorBuffer.SetData(_colorArray);
                    // }
                }

                if (enableCulling && cullingShader != null)
                {
                    voxelMaterial.EnableKeyword("ENABLE_CULLING");
                    cullingShader.SetBuffer(0, "_matrixBuffer", _matrixBuffer);
                    cullingShader.SetBuffer(0, "_visibilityBuffer", _visibleIdBuffer);
                }
                else
                {
                    voxelMaterial.DisableKeyword("ENABLE_CULLING");
                }

                // if (enableCulling && useCells)
                // {
                //     CellInvalidation();
                // }

                isDirty = false;
            }

            if (enableCulling && cullingShader != null)
            {
                //CellCulling();
                VoxelGPUCulling();
                ComputeBuffer.CopyCount(_visibleIdBuffer, _voxelIndirectBuffer, 4);

                // Readback from GPU hack to check visible count
                //asyncRequest = AsyncGPUReadback.Request(_drawIndirectBuffer, _drawIndirectBuffer.stride, 0);
            }
            else
            {
                _indirectArgs[1] = (uint)index;
                _voxelIndirectBuffer.SetData(_indirectArgs);
            }

            voxelCount = index;
            _renderBounds.SetMinMax(new Vector3(-100, -100, -100), new Vector3(100, 100, 100));

            if (voxelMaterial.HasFloat("_VoxelScale"))
            {
                voxelMaterial.SetFloat("_VoxelScale", voxelScale);
            }

            if (voxelMaterial.HasFloat("_EnableBillboard"))
            {
                if (voxelMeshType == VoxelMeshType.QUAD)
                {
                    voxelMaterial.EnableKeyword("ENABLE_BILLBOARD");
                }
                else
                {
                    voxelMaterial.DisableKeyword("ENABLE_BILLBOARD");
                }
            }

            Graphics.DrawMeshInstancedIndirect(_voxelMesh, 0, voxelMaterial, _renderBounds, _voxelIndirectBuffer, 0,
                null,
                ShadowCastingMode.On, true, 0, p_camera);
        }

        // private void CellInvalidation()
        // {
        //     Debug.Log("CellInvalidation");
        //     
        //     cellCountX = Mathf.CeilToInt((maxX - minX) / cellSizeX); 
        //     cellCountZ = Mathf.CeilToInt((maxZ - minZ) / cellSizeZ);
        //     
        //     cellMatrices = new List<Matrix4x4>[cellCountX * cellCountZ]; 
        //     var cellColors = new List<Vector4>[cellCountX * cellCountZ];
        //     for (int i = 0; i < cellMatrices.Length; i++)
        //     {
        //         cellMatrices[i] = new List<Matrix4x4>();
        //         cellColors[i] = new List<Vector4>();
        //     }
        //
        //     for (int i = 0; i < _matrixArray.Length; i++)
        //     {
        //         Matrix4x4 matrix = _matrixArray[i];
        //         Vector3 pos = matrix.GetColumn(3);
        //
        //         int xID = Mathf.Min(cellCountX - 1, Mathf.FloorToInt(Mathf.InverseLerp(minX, maxX, pos.x) * cellCountX));
        //         int zID = Mathf.Min(cellCountZ - 1, Mathf.FloorToInt(Mathf.InverseLerp(minZ, maxZ, pos.z) * cellCountZ));
        //
        //         cellMatrices[xID + zID * cellCountX].Add(matrix);
        //         cellColors[xID + zID * cellCountX].Add(_colorArray[i]);
        //     }
        //     
        //     int offset = 0;
        //     Matrix4x4[] voxelMatrixSortedByCell = new Matrix4x4[_matrixArray.Length];
        //     Vector4[] voxelColorSortedByCell = new Vector4[_colorArray.Length];
        //     for (int i = 0; i < cellMatrices.Length; i++)
        //     {
        //         for (int j = 0; j < cellMatrices[i].Count; j++)
        //         {
        //             voxelMatrixSortedByCell[offset] = cellMatrices[i][j];
        //             voxelColorSortedByCell[offset] = cellColors[i][j];
        //             offset++;
        //         }
        //     }
        //     
        //     _matrixBuffer.SetData(voxelMatrixSortedByCell);
        //     _colorBuffer.SetData(voxelColorSortedByCell);
        // }
        //
        // private void CellCulling()
        // {
        //     if (cellMatrices == null)
        //         return;
        //     
        //     _visibleCells.Clear();
        //     Camera cam = Camera.main;
        //     
        //     float cameraOriginalFarPlane = cam.farClipPlane;
        //     cam.farClipPlane = cullingDistance;
        //     GeometryUtility.CalculateFrustumPlanes(cam, cameraFrustumPlanes);
        //     cam.farClipPlane = cameraOriginalFarPlane;
        //
        //     for (int i = 0; i < cellMatrices.Length; i++)
        //     {
        //         Vector3 centerPosWS = new Vector3 (i % cellCountX + 0.5f, 0, i / cellCountX + 0.5f);
        //         centerPosWS.x = Mathf.Lerp(minX, maxX, centerPosWS.x / cellCountX);
        //         centerPosWS.z = Mathf.Lerp(minZ, maxZ, centerPosWS.z / cellCountZ);
        //         Vector3 sizeWS = new Vector3(Mathf.Abs(maxX - minX) / cellCountX,0,Mathf.Abs(maxX - minX) / cellCountX);
        //         Bounds cellBound = new Bounds(centerPosWS, sizeWS);
        //
        //         if (GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, cellBound) && cellMatrices[i].Count > 0) 
        //         {
        //             _visibleCells.Add(i);
        //         }
        //     }
        // }

        private void VoxelGPUCulling()
        {
            Matrix4x4 v = Camera.main.worldToCameraMatrix;
            Matrix4x4 p = Camera.main.projectionMatrix;
            Matrix4x4 vp = p * v;

            _visibleIdBuffer.SetCounterValue(0);

            cullingShader.SetMatrix("_cullingMatrix", vp);
            cullingShader.SetFloat("_cullingDistance", cullingDistance);

            float threadCount = 64;
            float batchLimit = 65535 * threadCount;
            // if (useCells) 
            // {
            //     var dispatchCount = 0;
            //     for (int i = 0; i < _visibleCells.Length; i++)
            //     {
            //         int targetCellFlattenID = _visibleCells[i];
            //         int memoryOffset = 0;
            //         for (int j = 0; j < targetCellFlattenID; j++)
            //         {
            //             memoryOffset += cellMatrices[j].Count;
            //         }
            //         
            //         int batchSize = cellMatrices[targetCellFlattenID].Count;
            //         
            //         while (i < _visibleCells.Length - 1 && _visibleCells[i + 1] == _visibleCells[i] + 1)
            //         {
            //             batchSize += cellMatrices[_visibleCells[i + 1]].Count;
            //             i++;
            //         }
            //         
            //         if (batchSize < batchLimit)
            //         {
            //             cullingShader.SetInt("_batchOffset", memoryOffset);
            //             cullingShader.Dispatch(0, Mathf.CeilToInt(batchSize / threadCount), 1, 1);
            //             dispatchCount++;
            //         }
            //         else
            //         {
            //             int subBatchCount = Mathf.CeilToInt(batchSize / batchLimit);
            //             for (int k = 0; k < subBatchCount; k++)
            //             {
            //                 cullingShader.SetInt("_batchOffset", memoryOffset + k * (int)batchLimit);
            //                 float current = (batchSize < (k + 1) * (int)batchLimit)
            //                     ? batchSize - k * (int)batchLimit
            //                     : batchLimit;
            //                 cullingShader.Dispatch(0, Mathf.CeilToInt(current / threadCount), 1, 1);
            //                 dispatchCount++;
            //             }
            //         }
            //     }
            // } else
            {
                int subBatchCount = Mathf.CeilToInt(voxelCacheSize / batchLimit);
                for (int i = 0; i < subBatchCount; i++)
                {
                    cullingShader.SetInt("_batchOffset", i * (int)batchLimit);
                    float current = (voxelCacheSize < (i + 1) * (int)batchLimit)
                        ? voxelCacheSize - i * (int)batchLimit
                        : batchLimit;
                    cullingShader.Dispatch(0, Mathf.CeilToInt(current / threadCount), 1, 1);
                }
            }
        }

        public void RemoveAllGroups()
        {
            Initialize();

            foreach (var group in _voxelGroups)
            {
                group.Dispose();
            }

            _voxelGroups.Clear();
        }

        public void Add(VoxelMesh p_mesh, VoxelGroup p_group = null)
        {
            Initialize();

            if (p_group == null)
            {
                _defaultVoxelGroup.AddMesh(p_mesh);
                if (!_voxelGroups.Contains(_defaultVoxelGroup))
                {
                    _voxelGroups.Add(_defaultVoxelGroup);
                }
            }
            else
            {
                p_group.AddMesh(p_mesh);
                _voxelGroups.Add(p_group);
            }
        }

        private void Dispose()
        {
            if (!_initialized)
                return;

            _voxelGroups?.ForEach(vg => vg.Dispose());

            _matrixArray.Dispose();
            _colorArray.Dispose();
            //_zeroMatrixArray.Dispose();
            //_visibleCells.Dispose();

            _matrixBuffer?.Release();
            _matrixBuffer = null;
            _colorBuffer?.Release();
            _colorBuffer = null;
            _visibleIdBuffer?.Release();
            _visibleIdBuffer = null;
            _voxelIndirectBuffer?.Release();
            _voxelIndirectBuffer = null;

            _initialized = false;
        }

        public void ClearVoxelGroups()
        {
            _voxelGroups?.ForEach(vg => vg.Dispose());
            _defaultVoxelGroup.ClearMeshes();
            _voxelGroups.Clear();
        }
        
        private void OnDestroy()
        {
            Dispose();
        }

#if UNITY_EDITOR
        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                Dispose();
            }
        }

        void OnSceneGUI(SceneView p_sceneView)
        {
            if (Application.isPlaying || !enabled)
                return;

            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || prefabStage.IsPartOfPrefabContents(this.gameObject))
            {
                HandleEditing();

                Render(p_sceneView.camera);
            }
        }

        private int paitingType = 0;
        void HandleEditing()
        {
            if (enablePainting && !Event.current.alt)
            {
                Tools.current = Tool.None;
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    paitingType = 1;
                }

                if (Event.current.type == EventType.MouseUp)
                {
                    paitingType = 0;
                }

                if (Event.current.type == EventType.MouseDown && Event.current.control)
                {
                    paitingType = 2;
                    Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    _voxelGroups?.ForEach(vg =>
                    {
                        int index;
                        VoxelMesh voxelMesh;
                        if (vg.Hit(ray, out voxelMesh, out index))
                        {
                            brushColor = voxelMesh.GetVoxelColor(index);
                        }
                    });
                }

                if (Event.current.isMouse)
                {
                    _voxelGroups?.ForEach(vg => vg.Unhighlight());
                }
                
                if (Event.current.type == EventType.MouseMove)
                {
                    Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    _voxelGroups?.ForEach(vg =>
                    {
                        int index;
                        VoxelMesh voxelMesh;
                        if (vg.Hit(ray, out voxelMesh, out index))
                        {
                            var position = voxelMesh.GetVoxelPosition(index);
                            voxelMesh.Highlight(position, brushSize, Color.green);
                        }
                    });
                }

                if (paitingType == 1)
                {
                    Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    _voxelGroups?.ForEach(vg =>
                    {
                        int index;
                        VoxelMesh voxelMesh;
                        if (vg.Hit(ray, out voxelMesh, out index))
                        {
                            var position = voxelMesh.GetVoxelPosition(index);
                            voxelMesh.Paint(position, brushSize, brushColor);
                        }
                    });
                }
            }
        }
#endif
    }
}
