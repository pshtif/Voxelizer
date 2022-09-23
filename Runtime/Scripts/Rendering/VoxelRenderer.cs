using System;
using System.Collections.Generic;
using BinaryEgo.Voxelizer;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class VoxelRenderer : MonoBehaviour
{
    static public VoxelRenderer Instance { get; private set; }
    static public int voxelCount { get; private set; }
    static public bool isDirty = false;

    //public MeshFilter voxelPrefab;
    public Material instanceMaterial;
    
    public float drawDistance = 12;
    public ComputeShader cullingShader;
    bool shouldBatchDispatch = true;
    
    // Bounds
    public float minX = -10;
    public float minZ = -10;
    public float maxX = 10;
    public float maxZ = 10;
    
    private Plane[] cameraFrustumPlanes = new Plane[6];
    private int cellCountX = -1;
    private int cellCountZ = -1;
    private bool useCells = false;
    
    public float cellSizeX = 2; //unity unit (m)
    public float cellSizeZ = 2; //unity unit (m)
    
    private List<Matrix4x4>[] cellMatrices;
    private NativeList<int> _visibleCells;
    
    public bool useCulling;

    [NonSerialized]
    private bool _initialized = false;

    public bool ToggleCulling
    {
        get
        {
            return useCulling;
        }

        set
        {
            useCulling = value;
        }
    }

    public int voxelCacheSize = 1000000;

    private NativeArray<Matrix4x4> _matrixArray;
    private NativeArray<Vector4> _colorArray;
    public NativeArray<Matrix4x4> MatrixArray => _matrixArray;

    private NativeArray<Matrix4x4> _zeroMatrixArray;
    private List<VoxelGroup> _voxelGroups;

    private ComputeBuffer _colorBuffer;
    private ComputeBuffer _matrixBuffer;
    private ComputeBuffer _visibleIdBuffer;
    private ComputeBuffer _voxelIndirectBuffer;

    private uint[] _indirectArgs;

    private Bounds _renderBounds;
    private Mesh _voxelMesh;
    private bool _previousCullingEnabled;

    void Awake()
    {
        Initialize();
    }

    void Initialize()
    {
        Instance = this;
        
        if (_initialized)
            return;

        if (instanceMaterial == null)
            return;

        Application.targetFrameRate = 60;
        
        _previousCullingEnabled = useCulling;
        _voxelMesh = CreateCube();
        _voxelGroups = new List<VoxelGroup>();
        _renderBounds = new Bounds();

        _visibleCells = new NativeList<int>(Allocator.Persistent);
        
        instanceMaterial.SetVector("_PivotPosWS", transform.position);
        instanceMaterial.SetVector("_BoundSize", new Vector2(transform.localScale.x, transform.localScale.z));
        
        _zeroMatrixArray = new NativeArray<Matrix4x4>(voxelCacheSize, Allocator.Persistent); 
        _matrixArray = new NativeArray<Matrix4x4>(voxelCacheSize, Allocator.Persistent);
        _colorArray = new NativeArray<Vector4>(voxelCacheSize, Allocator.Persistent);
        //_idArray = new NativeArray<uint>(voxelCacheSize, Allocator.Persistent);
        for (int i = 0; i < voxelCacheSize; i++)
        {
            _zeroMatrixArray[i] = Matrix4x4.zero;
            _colorArray[i] = new Vector4(1, 0, 0, 1);
            _matrixArray[i] = Matrix4x4.TRS(new Vector3(0,1000,0), Quaternion.identity, Vector3.one);
        }
        
        _colorBuffer?.Release();
        _colorBuffer = new ComputeBuffer(voxelCacheSize, sizeof(float) * 4);
        _colorBuffer.SetData(_colorArray);
        
        _matrixBuffer?.Release();
        _matrixBuffer = new ComputeBuffer(voxelCacheSize, sizeof(float)*16);
        _matrixBuffer.SetData(_matrixArray);
        
        _visibleIdBuffer?.Release();
        _visibleIdBuffer = new ComputeBuffer(voxelCacheSize, sizeof(uint), ComputeBufferType.Append);
        
        instanceMaterial.SetBuffer("_colorBuffer", _colorBuffer);
        instanceMaterial.SetBuffer("_matrixBuffer", _matrixBuffer);
        instanceMaterial.SetBuffer("_visibleIdBuffer", _visibleIdBuffer);
        
        _voxelIndirectBuffer?.Release();
        _indirectArgs = new uint[5] { 0, 0, 0, 0, 0 };
        _voxelIndirectBuffer = new ComputeBuffer(1, _indirectArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        
        _indirectArgs[0] = (uint)_voxelMesh.GetIndexCount(0);
        _indirectArgs[1] = (uint)voxelCacheSize;
        _indirectArgs[2] = (uint)_voxelMesh.GetIndexStart(0);
        _indirectArgs[3] = (uint)_voxelMesh.GetBaseVertex(0);
        _indirectArgs[4] = 0;

        _voxelIndirectBuffer.SetData(_indirectArgs);

        if (cullingShader != null)
        {
            cullingShader.SetBuffer(0, "_matrixBuffer", _matrixBuffer);
            cullingShader.SetBuffer(0, "_visibilityBuffer", _visibleIdBuffer);
        }
        
        _initialized = true;
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

        // asyncRequest.WaitForCompletion(); 
        // if (!asyncRequest.hasError)
        // {
        //     uint[] a = asyncRequest.GetData<uint>().ToArray();
        //     Debug.Log(String.Join(",", a));
        // }
        
        if (isDirty || useCulling != _previousCullingEnabled)
        {
            if (useCulling != _previousCullingEnabled)
            {
                _previousCullingEnabled = useCulling;
                if (!useCulling)
                {
                    _matrixBuffer.SetData(_matrixArray);
                    _colorBuffer.SetData(_colorArray);
                }
            }

            if (useCulling && useCells)
            {
                CellInvalidation();
            }

            isDirty = false;
        }

        int index = 0;
        foreach (VoxelGroup voxelGroup in _voxelGroups)
        {
            voxelGroup.Invalidate(_matrixBuffer, _matrixArray, _colorBuffer, _colorArray, ref index);
        }
        
        if (index == 0)
            return;

        if (useCulling)
        {
            instanceMaterial.EnableKeyword("CULLING");
            CellCulling();
            VoxelGPUCulling();
            ComputeBuffer.CopyCount(_visibleIdBuffer, _voxelIndirectBuffer, 4);
            
            // Readback from GPU hack to check visible count
            //asyncRequest = AsyncGPUReadback.Request(_drawIndirectBuffer, _drawIndirectBuffer.stride, 0);
        }
        else
        {
            instanceMaterial.DisableKeyword("CULLING");
            _indirectArgs[1] = (uint)index;
            _voxelIndirectBuffer.SetData(_indirectArgs);
        }

        voxelCount = index;
        _renderBounds.SetMinMax(new Vector3(minX, -5, minZ), new Vector3(maxX, 5, maxZ));

        Graphics.DrawMeshInstancedIndirect(_voxelMesh, 0, instanceMaterial, _renderBounds, _voxelIndirectBuffer, 0, null,
            ShadowCastingMode.On, true, 0, p_camera);
    }
    
    private void CellInvalidation()
    {
        Debug.Log("CellInvalidation");
        
        cellCountX = Mathf.CeilToInt((maxX - minX) / cellSizeX); 
        cellCountZ = Mathf.CeilToInt((maxZ - minZ) / cellSizeZ);
        
        cellMatrices = new List<Matrix4x4>[cellCountX * cellCountZ]; 
        var cellColors = new List<Vector4>[cellCountX * cellCountZ];
        for (int i = 0; i < cellMatrices.Length; i++)
        {
            cellMatrices[i] = new List<Matrix4x4>();
            cellColors[i] = new List<Vector4>();
        }
    
        for (int i = 0; i < _matrixArray.Length; i++)
        {
            Matrix4x4 matrix = _matrixArray[i];
            Vector3 pos = matrix.GetColumn(3);

            int xID = Mathf.Min(cellCountX - 1, Mathf.FloorToInt(Mathf.InverseLerp(minX, maxX, pos.x) * cellCountX));
            int zID = Mathf.Min(cellCountZ - 1, Mathf.FloorToInt(Mathf.InverseLerp(minZ, maxZ, pos.z) * cellCountZ));
    
            cellMatrices[xID + zID * cellCountX].Add(matrix);
            cellColors[xID + zID * cellCountX].Add(_colorArray[i]);
        }
        
        int offset = 0;
        Matrix4x4[] voxelMatrixSortedByCell = new Matrix4x4[_matrixArray.Length];
        Vector4[] voxelColorSortedByCell = new Vector4[_colorArray.Length];
        for (int i = 0; i < cellMatrices.Length; i++)
        {
            for (int j = 0; j < cellMatrices[i].Count; j++)
            {
                voxelMatrixSortedByCell[offset] = cellMatrices[i][j];
                voxelColorSortedByCell[offset] = cellColors[i][j];
                offset++;
            }
        }
        
        _matrixBuffer.SetData(voxelMatrixSortedByCell);
        _colorBuffer.SetData(voxelColorSortedByCell);
    }
    
    private void CellCulling()
    {
        if (cellMatrices == null)
            return;
        
        _visibleCells.Clear();
        Camera cam = Camera.main;
        
        float cameraOriginalFarPlane = cam.farClipPlane;
        cam.farClipPlane = drawDistance;
        GeometryUtility.CalculateFrustumPlanes(cam, cameraFrustumPlanes);
        cam.farClipPlane = cameraOriginalFarPlane;

        for (int i = 0; i < cellMatrices.Length; i++)
        {
            Vector3 centerPosWS = new Vector3 (i % cellCountX + 0.5f, 0, i / cellCountX + 0.5f);
            centerPosWS.x = Mathf.Lerp(minX, maxX, centerPosWS.x / cellCountX);
            centerPosWS.z = Mathf.Lerp(minZ, maxZ, centerPosWS.z / cellCountZ);
            Vector3 sizeWS = new Vector3(Mathf.Abs(maxX - minX) / cellCountX,0,Mathf.Abs(maxX - minX) / cellCountX);
            Bounds cellBound = new Bounds(centerPosWS, sizeWS);

            if (GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, cellBound) && cellMatrices[i].Count > 0) 
            {
                _visibleCells.Add(i);
            }
        }
    }
    
    private void VoxelGPUCulling()
    {
        Matrix4x4 v = Camera.main.worldToCameraMatrix;
        Matrix4x4 p = Camera.main.projectionMatrix;
        Matrix4x4 vp = p * v;
        
        _visibleIdBuffer.SetCounterValue(0);

        //set once only
        cullingShader.SetMatrix("_cullingMatrix", vp);
        cullingShader.SetFloat("_cullingDistance", drawDistance);
        
        float threadCount = 64;
        float batchLimit = 65535 * threadCount;
        if (useCells) 
        {
            //dispatch per visible cell
            var dispatchCount = 0;
            for (int i = 0; i < _visibleCells.Length; i++)
            {
                int targetCellFlattenID = _visibleCells[i];
                int memoryOffset = 0;
                for (int j = 0; j < targetCellFlattenID; j++)
                {
                    memoryOffset += cellMatrices[j].Count;
                }
                
                int batchSize = cellMatrices[targetCellFlattenID].Count;
                 
                if (shouldBatchDispatch)
                {
                    while (i < _visibleCells.Length - 1 && _visibleCells[i + 1] == _visibleCells[i] + 1)
                    {
                        batchSize += cellMatrices[_visibleCells[i + 1]].Count;
                        i++;
                    }
                }
                
                // We can have large batches so need to break up
                if (batchSize < batchLimit)
                {
                    cullingShader.SetInt("_batchOffset", memoryOffset);
                    cullingShader.Dispatch(0, Mathf.CeilToInt(batchSize / threadCount), 1, 1);
                    dispatchCount++;
                }
                else
                {
                    int subBatchCount = Mathf.CeilToInt(batchSize / batchLimit);
                    for (int k = 0; k < subBatchCount; k++)
                    {
                        cullingShader.SetInt("_batchOffset", memoryOffset + k * (int)batchLimit);
                        //float batchCount = (k * batchLimit > batchSize) ? batchSize - (k - 1) * batchLimit : batchLimit;
                        float current = (batchSize < (k + 1) * (int)batchLimit)
                            ? batchSize - k * (int)batchLimit
                            : batchLimit;
                        cullingShader.Dispatch(0, Mathf.CeilToInt(current / threadCount), 1, 1);
                        dispatchCount++;
                    }
                }
            }
        } else {
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
            p_group = new VoxelGroup();
        }
        
        p_group.AddMesh(p_mesh);
        _voxelGroups.Add(p_group);
    }
    
    private void Dispose()
    {
        if (!_initialized)
            return;

        if (_voxelGroups != null)
        {
            foreach (VoxelGroup group in _voxelGroups)
            {
                group.Dispose();
            }
            
            _voxelGroups = null;
        }

        _matrixArray.Dispose();
        _colorArray.Dispose();
        _zeroMatrixArray.Dispose();
        _visibleCells.Dispose();
        
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
#endif
    
    public static void Copy<T>(NativeArray<T> dest, int destIdx, NativeArray<T> src, int srcIdx, int count)
        where T : struct
    {
        dest.Slice(destIdx, count).CopyFrom(src.Slice(srcIdx, count));
    }

    public static Mesh CreateCube()
    {
        Vector3[] vertices = {
            new Vector3 (-0.5f, -0.5f, -0.5f),
            new Vector3 (0.5f, -0.5f, -0.5f),
            new Vector3 (0.5f, 0.5f, -0.5f),
            new Vector3 (-0.5f, 0.5f, -0.5f),
            new Vector3 (-0.5f, 0.5f, 0.5f),
            new Vector3 (0.5f, 0.5f, 0.5f),
            new Vector3 (0.5f, -0.5f, 0.5f),
            new Vector3 (-0.5f, -0.5f, 0.5f),
        };

        int[] triangles = {
            0, 2, 1, // front
            0, 3, 2,
            2, 3, 4, // top
            2, 4, 5,
            1, 2, 5, // right
            1, 5, 6,
            4, 0, 7, // left
            4, 3, 0,
            5, 4, 7, // back
            5, 7, 6,
            6, 7, 0, // bottom
            6, 0, 1
        };
        
        Vector3[] normals  = {
            new Vector3 (0, 0, -1),
            new Vector3 (1, 0, 0),
            new Vector3 (0, 1, 0),
            new Vector3 (0, 0, 0), 
            new Vector3 (-1, 0, 0),
            new Vector3 (0, 0, 1),
            new Vector3 (0, -1, 0),
            new Vector3 (0, 0, 0),
        };

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.Optimize();

        return mesh;
    }
    
    #if UNITY_EDITOR
    void OnSceneGUI(SceneView p_sceneView)
    {
        if (Application.isPlaying || !enabled)
            return;

        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage == null || prefabStage.IsPartOfPrefabContents(this.gameObject))
        {
            Render(p_sceneView.camera);    
        }
    }
    #endif
}
