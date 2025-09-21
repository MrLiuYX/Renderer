using Native;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

public delegate void OnDataTextureChanged();

public unsafe class GPUInstanceRenderer<T> : IRenderer, IReference where T : unmanaged, IGPUInstanceRendererData
{
    private int _pixelSize;
    private int _rendererDataSize;
    private Mesh _instanceMesh;
    private Material _instanceMaterial;
    private int _subMeshIndex = 0;

    private ComputeBuffer _argsBuffer;
    private uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };
    private Bounds _bounds = new Bounds(Vector3.zero, Vector3.one * 99999);

    private Queue<int> _freeDatas;
    private NativeList<T> _datas;
    private Texture2D _dataTexture;
    private int _textureSize;

    private bool _dirty;
    private int _dirtyDatasStart;
    private int _dirtyDatasEnd;

    private byte* _src;
    private byte* _des;
    private Matrix4x4[] _totalMatrix;
    private MaterialPropertyBlock _block;
    private const int _split = 512;
    private event OnDataTextureChanged _dataTextureEvent;

    public static GPUInstanceRenderer<T> Create(Material mat, Mesh mesh)
    {
        var instance = ReferencePool.Acquire<GPUInstanceRenderer<T>>();
        instance.InternalOnCreate(mat, mesh);
        return instance;
    }

    private void InternalOnCreate(Material mat, Mesh mesh)
    {
        //Debug.LogError($"Gfx: {SystemInfo.graphicsDeviceType}, WebGL2? {SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3}");
        //Debug.LogError($"2DArray? {SystemInfo.supports2DArrayTextures}");
        //Debug.LogError($"RGBAFloat? {SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat)}");
        //图集相关参数
        _textureSize = 0;
        _rendererDataSize = UnsafeUtility.SizeOf<T>();
        _pixelSize = _rendererDataSize / 16;
        _datas = new NativeList<T>(Allocator.Persistent);
        _src = (byte*)_datas.GetUnsafePtr();
        _freeDatas = new Queue<int>();
        _dirty = false;

        //GPUInstance相关参数
        _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _instanceMesh = mesh;
        _args[0] = (uint)_instanceMesh.GetIndexCount(_subMeshIndex);
        _args[2] = (uint)_instanceMesh.GetIndexStart(_subMeshIndex);
        _args[3] = (uint)_instanceMesh.GetBaseVertex(_subMeshIndex);
        
        _instanceMaterial = mat;
        _instanceMaterial.SetFloat("_pixelSize", _pixelSize);

        _dirtyDatasStart = int.MaxValue;
        _dirtyDatasEnd = int.MinValue;

        //兼容旧版本
        _totalMatrix = new Matrix4x4[_split];
        for (int i = 0; i < _totalMatrix.Length; i++)
        {
            _totalMatrix[i] = Matrix4x4.identity;
        }
        _block = new MaterialPropertyBlock();
    }

    public void Clear()
    {
        if (_argsBuffer != null) { _argsBuffer.Dispose(); _argsBuffer = null; }
        if (_dataTexture != null) GameObject.Destroy(_dataTexture);
        if (_datas.IsCreated) _datas.Dispose();
        _freeDatas.Clear();
        Debug.Log($"RendererDestroy : {typeof(T).Name}");
    }

    public void RegisterDataTextureChangedEvent(OnDataTextureChanged @event)
    {
        _dataTextureEvent += @event;
    }

    public void UnRegisterDataTextureChangedEvent(OnDataTextureChanged @event)
    {
        _dataTextureEvent -= @event;
    }

    public RendererHandler<T> AddData()
    {
        var data = new T();
        data.SetColor(new float3(1, 1, 1));
        data.SetPosition(float3.zero);
        data.SetRotation(float3.zero);
        data.SetScale(new float3(1, 1, 1));
        data.SetActive(true);

        var index = -1;
        if (_freeDatas.Count != 0)
        {
            index = _freeDatas.Dequeue();
        }
        else
        {
            _datas.Add(data);
            _args[1] = (uint)(_datas.Length);
            _src = (byte*)_datas.GetUnsafePtr();
            index = _datas.Length - 1;
        }

        _dirty = true;
        _dirtyDatasStart = math.min(_dirtyDatasStart, index);
        _dirtyDatasEnd = math.max(_dirtyDatasEnd, index);

        //扩容贴图
        Expansion();

        var handler = RendererHandler<T>.Create();
        handler.Index = index;
        handler.Data = data;

        return handler;

    }
    private void Expansion()
    {
        if (_textureSize * _textureSize >= _pixelSize * _datas.Length) return;

        if (_textureSize == 0) _textureSize = 1;
        while ((_textureSize * _textureSize) < _pixelSize * _datas.Length)
        {
            _textureSize = _textureSize << 1;
        }

        if (_dataTexture != null) GameObject.Destroy(_dataTexture);
        _dataTexture = new Texture2D(_textureSize, _textureSize, TextureFormat.RGBAFloat, false);
        _dataTexture.filterMode = FilterMode.Point;
        _instanceMaterial.SetTexture("_DataTex", _dataTexture);
        _dataTexture.Apply(false);
        _des = (byte*)_dataTexture.GetRawTextureData<byte>().GetUnsafePtr();

        //删除了需要全拷贝
        _dirty = true;
        _dirtyDatasStart = 0;
        _dirtyDatasEnd = _datas.Length - 1;

        _dataTextureEvent?.Invoke();
    }

    public void UpdateData(RendererHandler<T> handler)
    {
        _datas[handler.Index] = handler.Data;
        _dirty = true;
        _dirtyDatasStart = math.min(_dirtyDatasStart, handler.Index);
        _dirtyDatasEnd = math.max(_dirtyDatasEnd, handler.Index);
    }

    public void RemoveData(RendererHandler<T> handler)
    {
        var index = handler.Index;
        var data = _datas[index];
        data.SetActive(false);
        _datas[index] = data;
        _freeDatas.UniqueEnqueue(index);

        _dirty = true;
        _dirtyDatasStart = math.min(_dirtyDatasStart, handler.Index);
        _dirtyDatasEnd = math.max(_dirtyDatasEnd, handler.Index);
    }


    public void DoRenderer()
    {
        if (_dirty)
        {
            var copySize = (_dirtyDatasEnd - _dirtyDatasStart + 1) * _rendererDataSize;
            var startPos = _dirtyDatasStart * _rendererDataSize;
            UnsafeUtility.MemCpy(_des + startPos, _src + startPos, copySize);

            _dataTexture.Apply(true);
            _dirty = false;
            _dirtyDatasStart = int.MaxValue;
            _dirtyDatasEnd = int.MinValue;
        }

        _argsBuffer.SetData(_args);

        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES3)
        {
            Graphics.DrawMeshInstancedIndirect(
                _instanceMesh,
                _subMeshIndex,
                _instanceMaterial,
                _bounds,
                _argsBuffer,
                0,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false,
                0,
                null);
        }
        else
        {
            //尝试兼容WebGL
            _instanceMaterial.enableInstancing = true;
            var rendererCount = _datas.Length;
            var batches = Mathf.CeilToInt(rendererCount * 1.0f / (_split - 1));
            for (int batchIndex = 0; batchIndex < batches; batchIndex++)
            {
                _block.SetInt("_instanceIdOffset", batchIndex * (_split - 1));
                var instancesInBatch = Mathf.Min((_split - 1), rendererCount - batchIndex * (_split - 1));
                Graphics.DrawMeshInstanced(_instanceMesh, _subMeshIndex, _instanceMaterial, _totalMatrix, instancesInBatch, _block);
            }
        }
    }

    public void UpdateMesh(Mesh mesh)
    {
        _instanceMesh = mesh;
        _args[0] = (uint)_instanceMesh.GetIndexCount(_subMeshIndex);
        _args[2] = (uint)_instanceMesh.GetIndexStart(_subMeshIndex);
        _args[3] = (uint)_instanceMesh.GetBaseVertex(_subMeshIndex);
    }

    public Material GetMat()
    {
        return _instanceMaterial;
    }

    public Texture2D GetDataTexure()
    {
        return _dataTexture;
    }
}