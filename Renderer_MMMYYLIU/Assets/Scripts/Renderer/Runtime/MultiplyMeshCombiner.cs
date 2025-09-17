using Native;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

/// <summary>
/// ʵ�ֶ�Mesh�ϲ�
/// </summary>
public unsafe class MultiplyMeshCombiner : IReference
{
    public class MultiplyMeshRendererHandler : IReference
    {
        /// <summary>
        /// ������ʼλ��
        /// ŵҪʹ������ "���ڵ���"��λ��Ϊ��Meshλ��
        /// </summary>
        public int VertexIndexStart;

        /// <summary>
        /// ����ĩʼλ��
        /// ŵҪʹ������ "С��"��λ��Ϊ��Meshλ��
        /// </summary>
        public int VertexIndexEnd;

        public void Clear()
        {

        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ShaderVertexData
    {
        //����λ��
        public float4 Pos;
        //����
        public float3 Normal;
        //UV
        public float3 UV;
        ////������ͼUV
        //public float4 DiffuseUV;
        ////������ͼ
        //public float DiffusePageIndex;
    }

    private static VertexAttributeDescriptor[] _layout = new VertexAttributeDescriptor[3]
    {
        new VertexAttributeDescriptor( VertexAttribute.Position, VertexAttributeFormat.Float32, 4, 0),
        new VertexAttributeDescriptor( VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
        new VertexAttributeDescriptor( VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3, 0),
        //new VertexAttributeDescriptor( VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4, 0),
        //new VertexAttributeDescriptor( VertexAttribute.TexCoord5, VertexAttributeFormat.Float32, 1, 0),
    };

    /// <summary>
    /// һ��Meshֻ����һ��MeshData
    /// </summary>
    private struct InternalMeshData
    {
        public Mesh.MeshDataArray MeshDataArray;
        /// <summary>
        /// Diffuse��UV
        /// </summary>
        public Rect DiffuseRect;
        /// <summary>
        /// Diffuseҳ��
        /// </summary>
        public int PageIndex;
        /// <summary>
        /// �ڼ���ʵ��
        /// </summary>
        public int EntityPosition;
        /// <summary>
        /// Shader���ݴ����еĶ�����ʼ����
        /// </summary>
        public int VertexDataPosition;
        /// <summary>
        /// Shader���ݴ����е���������ʼ����
        /// </summary>
        public int TriangleDataPosition;
    }

    private GameObject                          _multiplyMeshCombineObj;
    private MeshFilter                          _filter;
    private Mesh                                _mesh;
    private SubMeshDescriptor                   _descriptor;
    private NativeArray<ShaderVertexData>       _vertexDatas;
    private NativeArray<uint>                   _triangle;
    private int                                 _vertexCount;
    private int                                 _triangleCount;
    //private DynamicAtlas                        _diffuseAtlas;
    private int                                 _entityCount;
    private int                                 _vertexDatasize;
    private int                                 _triangleSize;
    private int                                 _needAddVertexCount;
    private int                                 _needAddTriangleCount;
    private Dictionary<Mesh, InternalMeshData>  _meshDataDict;
    private List<InternalMeshData>              _needWriteInternalMeshData;


    public Mesh Mesh => _mesh;
    //public Texture2DArray DiffuseAtlas => _diffuseAtlas.GetTexture2DArray();

    public static MultiplyMeshCombiner Create()
    {
        var data = ReferencePool.Acquire<MultiplyMeshCombiner>();
        data.InternalCreate();
        return data;
    }

    private void InternalCreate()
    {
        _entityCount = 0;
        _multiplyMeshCombineObj = new GameObject("MultiplyMeshCombine");
        _filter = _multiplyMeshCombineObj.AddComponent<MeshFilter>();
        //_diffuseAtlas = DynamicAtlas.Create();
        _vertexDatasize = 1024;
        _triangleSize = 1024;

        _mesh = new Mesh();
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 999999);
        _mesh.MarkDynamic();
        _mesh.subMeshCount = 1;
        _mesh.indexFormat = IndexFormat.UInt32;

        _descriptor = new SubMeshDescriptor();
        _descriptor.baseVertex = 0;
        _descriptor.bounds = default;
        _descriptor.firstVertex = 0;
        _descriptor.indexStart = 0;
        _descriptor.topology = MeshTopology.Triangles;

        _filter.mesh = _mesh;

        _vertexDatas = new NativeArray<ShaderVertexData>(_vertexDatasize, Allocator.Persistent);
        _triangle = new NativeArray<uint>(_triangleSize, Allocator.Persistent);

        _vertexCount = 0;
        _triangleCount = 0;

        _meshDataDict = DictionaryPool<Mesh, InternalMeshData>.Get();
        _needWriteInternalMeshData = ListPool<InternalMeshData>.Get();
    }

    public void Clear()
    {
        _vertexDatas.Dispose();
        _triangle.Dispose();
        _filter = null;
        _mesh = null;
        _vertexCount = 0;
        _triangleCount = 0;
        _needAddVertexCount = 0;
        _needAddTriangleCount = 0;
        GameObject.Destroy(_multiplyMeshCombineObj);
        GameObject.Destroy(_mesh);
        //ReferencePool.Release(_diffuseAtlas);
        DictionaryPool<Mesh, InternalMeshData>.Release(_meshDataDict);
        ListPool<InternalMeshData>.Release(_needWriteInternalMeshData);
    }

    public MultiplyMeshRendererHandler AddMesh(Mesh mesh)
    {
        //_diffuseAtlas.AddTextureToAtlas(diffuseTex, out var rect, out var pageIndex);

        MultiplyMeshRendererHandler handler = ReferencePool.Acquire<MultiplyMeshRendererHandler>();

        if (!_meshDataDict.TryGetValue(mesh, out var internalMeshData))
        {
            internalMeshData.MeshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            //internalMeshData.DiffuseRect = rect;
            //internalMeshData.PageIndex = pageIndex;
            internalMeshData.EntityPosition = _entityCount;
            internalMeshData.VertexDataPosition = _vertexCount + _needAddVertexCount;
            internalMeshData.TriangleDataPosition = _triangleCount + _needAddTriangleCount;

            _needAddVertexCount += _vertexCount + mesh.vertexCount;
            _needAddTriangleCount += mesh.triangles.Length;
            _needWriteInternalMeshData.Add(internalMeshData);
            _meshDataDict.Add(mesh, internalMeshData);
        }

        handler.VertexIndexStart = internalMeshData.VertexDataPosition;
        handler.VertexIndexEnd = internalMeshData.VertexDataPosition + mesh.vertexCount;

        _entityCount++;

        return handler;
    }

    public bool UpdateImmediately()
    {
        if (_needWriteInternalMeshData.Count == 0) return false;

        UpdateMeshVertexData();
        UpdateMeshTriangleData();
        Upload();
        _needAddTriangleCount = 0;
        _needAddVertexCount = 0;
        _needWriteInternalMeshData.Clear();
        return true;
    }

    private void UpdateMeshVertexData()
    {
        var endVertexCount = _vertexCount + _needAddVertexCount;
        var needCreateNewContainer = _vertexDatasize < endVertexCount;
        while(_vertexDatasize < endVertexCount)
        {
            _vertexDatasize = _vertexDatasize << 1;
        }

        if(needCreateNewContainer)
        {
            var src = _vertexDatas.GetUnsafePtr();
            var newContainer = new NativeArray<ShaderVertexData>(_vertexDatasize, Allocator.Persistent);
            var dst = newContainer.GetUnsafePtr();
            UnsafeUtility.MemCpy(dst, src, _vertexCount * UnsafeUtility.SizeOf<ShaderVertexData>());
            _vertexDatas.Dispose();
            _vertexDatas = newContainer;    
        }

        for (int j = 0; j < _needWriteInternalMeshData.Count; j++)
        {
            var internalMeshData = _needWriteInternalMeshData[j];
            var meshDataArray = internalMeshData.MeshDataArray[0];

            var vertexs = new NativeArray<Vector3>(meshDataArray.vertexCount, Allocator.TempJob);
            meshDataArray.GetVertices(vertexs);

            var normals = new NativeArray<Vector3>(meshDataArray.vertexCount, Allocator.TempJob);
            meshDataArray.GetNormals(normals);

            var uvs = new NativeArray<Vector3>(meshDataArray.vertexCount, Allocator.TempJob);
            meshDataArray.GetUVs(0, uvs);

            new VertexDataInjectJob()
            {
                _vertexDatas = _vertexDatas,
                _indexOffset = internalMeshData.VertexDataPosition,

                _vertexs = vertexs,
                _normals = normals,
                _uvs = uvs,
                _diffuseUV = internalMeshData.DiffuseRect.ToFloat4(),
                _pageIndex = internalMeshData.PageIndex,
            }.Schedule(meshDataArray.vertexCount, 64).Complete();            
        }

        _vertexCount = endVertexCount;
        _descriptor.vertexCount = _vertexCount;
    }

    private void UpdateMeshTriangleData()
    {
        var endTriangleCount = _triangleCount + _needAddTriangleCount;
        var needCreateNewContainer = _triangleSize < endTriangleCount;
        while (_triangleSize < endTriangleCount)
        {
            _triangleSize = _triangleSize << 1;
        }

        if (needCreateNewContainer)
        {
            var src = _triangle.GetUnsafePtr();
            var newContainer = new NativeArray<uint>(_triangleSize, Allocator.Persistent);
            var dst = newContainer.GetUnsafePtr();
            UnsafeUtility.MemCpy(dst, src, _triangleCount * UnsafeUtility.SizeOf<uint>());
            _triangle.Dispose();
            _triangle = newContainer;
        }

        for (int j = 0; j < _needWriteInternalMeshData.Count; j++)
        {
            var internalMeshData = _needWriteInternalMeshData[j];
            var meshDataArray = internalMeshData.MeshDataArray[0];
            var triangles = ReadTriangles(meshDataArray, 0,  Allocator.TempJob);
            new IndexDataInjectJob()
            {
                _triangle = _triangle,
                _indexOffset = internalMeshData.TriangleDataPosition,
                _vertexDataPosition = internalMeshData.VertexDataPosition,
                _triangles = triangles,
            }.Schedule(triangles.Length, 64).Complete();
        }

        _triangleCount = endTriangleCount;
        _descriptor.indexCount = _triangleCount;
    }

    private void Upload()
    {
        _mesh.SetVertexBufferParams(_vertexCount, _layout);
        _mesh.SetVertexBufferData(_vertexDatas
            , 0
            , 0
            , _vertexCount
            , 0
            , MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices);

        _mesh.SetIndexBufferParams(_triangleCount, IndexFormat.UInt32);
        _mesh.SetIndexBufferData(_triangle,
            0,
            0,
            _triangleCount,
            MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices);

        _mesh.SetSubMesh(0, _descriptor, MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
        _mesh.MarkModified();
    }

    [BurstCompile]
    private struct VertexDataInjectJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<ShaderVertexData> _vertexDatas;
        public int _indexOffset;
        internal NativeArray<Vector3> _vertexs;
        internal NativeArray<Vector3> _normals;
        internal NativeArray<Vector3> _uvs;
        internal float4 _diffuseUV;
        internal int _pageIndex;

        [BurstCompile]
        public void Execute(int index)
        {
            _vertexDatas[index + _indexOffset] = new ShaderVertexData()
            {
                Pos = new float4(_vertexs[index], 1),
                Normal = _normals[index],
                UV = _uvs[index],
                //DiffuseUV = _diffuseUV,
                //DiffusePageIndex = _pageIndex,
            };
        }
    }

    [BurstCompile]
    private struct IndexDataInjectJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        internal NativeArray<uint> _triangle;
        internal NativeArray<uint> _triangles;
        internal int _indexOffset;
        internal int _vertexDataPosition;

        [BurstCompile]
        public void Execute(int index)
        {
            _triangle[index + _indexOffset] = (uint)(_triangles[index] + _vertexDataPosition);
        }
    }

    public NativeArray<uint> ReadTriangles(Mesh.MeshData md, int s, Allocator alloc = Allocator.Temp)
    {
        var sub = md.GetSubMesh(s);
        if (sub.topology != MeshTopology.Triangles)
            return new NativeArray<uint>(0, alloc);

        NativeArray<uint> outIdx = new NativeArray<uint>(sub.indexCount, alloc, NativeArrayOptions.UninitializedMemory);

        if (md.indexFormat == IndexFormat.UInt16)
        {
            var all = md.GetIndexData<ushort>();                        
            var slice = all.GetSubArray(sub.indexStart, sub.indexCount); 
            for (int i = 0; i < slice.Length; i++)
                outIdx[i] = (uint)(slice[i] + sub.baseVertex);
        }
        else
        {
            var all = md.GetIndexData<int>();                            
            var slice = all.GetSubArray(sub.indexStart, sub.indexCount);
            for (int i = 0; i < slice.Length; i++)
                outIdx[i] = (uint)(slice[i] + sub.baseVertex);
        }

        return outIdx;
    }

    //public void RegisterDiffuseAtlasChanged(OnDynamicAtlasTextureChanged @event)
    //{
    //    _diffuseAtlas.RegisterTextureChangedEvent(@event);
    //}
}
