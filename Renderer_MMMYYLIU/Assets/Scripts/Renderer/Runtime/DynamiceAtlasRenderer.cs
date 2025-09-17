using Native;
using System;
using UnityEngine;

public unsafe class DynamiceAtlasRenderer<T> : IRenderer where T : unmanaged, IDynamicAtlasRendererData
{
    private DynamicAtlas _atlas;
    private GPUInstanceRenderer<T> _renderer;

    private Material _mat;
    private Mesh _mesh;

    private RendererInspector _rendererInspector;

    public static DynamiceAtlasRenderer<T> Create(string shaderPath)
    {
        var instance = ReferencePool.Acquire<DynamiceAtlasRenderer<T>>();
        instance.InternalOnCreate(shaderPath);
        return instance;
    }

    private void InternalOnCreate(string shaderPath)
    {
        _mesh = new Mesh()
        {
            vertices = new Vector3[]
                        {
                        new Vector3(-0.5f, -0.5f, 0),new Vector3(0.5f,  -0.5f, 0),
                        new Vector3(-0.5f,  0.5f, 0),
                        new Vector3(0.5f, 0.5f, 0),
                        },

            uv = new Vector2[]
                        {
                        new Vector2(0, 0),
                        new Vector2(1, 0),
                        new Vector2(0, 1),
                        new Vector2(1, 1),
                        },

            triangles = new int[]
                        {
                        0,2,1,1,2,3
                        },
        };

        _mat = new Material(Shader.Find(shaderPath));

        _renderer = GPUInstanceRenderer<T>.Create(_mat, _mesh);

#if UNITY_EDITOR
        _rendererInspector = new GameObject($"Renderer_MultiplyMeshRenderer_{typeof(T).Name}").AddOrGetComponent<RendererInspector>();
        _rendererInspector.transform.SetParent(RendererManager.Instance.DynamicAtlasRoot.transform);
        _rendererInspector.Mesh = _mesh;
        _rendererInspector.Material = _mat;
        _rendererInspector.DataTex = _renderer.GetDataTexure();
        _renderer.RegisterDataTextureChangedEvent(OnDataTextureChanged);
#endif
    }

    public RendererHandler<T> AddData(Texture2D texture)
    {
        if (_atlas == null) CreateAtlas();

        _atlas.AddTextureToAtlas(texture, out var rect, out var pageIndex);

        var handler = _renderer.AddData();
        handler.Data.SetRect(rect);
        handler.Data.SetPageIndex(pageIndex);
        _renderer.UpdateData(handler);
        return handler;
    }

    public void Clear()
    {
        if (_atlas != null)
        {
            _atlas.UnRegisterTextureChangedEvent(OnDynamicAtlasTextureChenged);
            ReferencePool.Release(_atlas);
            _atlas = null;
        }

        GameObject.Destroy(_mesh);
        GameObject.Destroy(_mat);
        _mesh = null;
        _mat = null;

        ReferencePool.Release(_renderer);

        Debug.Log($"DynamiceRendererDestroy : {typeof(T).Name}");
    }


    public void DoRenderer()
    {
        _renderer.DoRenderer();
    }

    public void CreateAtlas()
    {
        if (_atlas != null) return;
        _atlas = DynamicAtlas.Create();
        _atlas.RegisterTextureChangedEvent(OnDynamicAtlasTextureChenged, true);
    }

    private void OnDynamicAtlasTextureChenged()
    {
        if (_atlas.GetTexture2DArray() == null) return;
        var mat = _renderer.GetMat();
        mat.SetTexture("_dynamicAtlas", _atlas.GetTexture2DArray());
        mat.SetFloat("_dynamicSize", _atlas.GetTexture2DArray().width);
        _rendererInspector.DiffuseTex = _atlas.GetTexture2DArray();
    }

    private void OnDataTextureChanged()
    {
        _rendererInspector.DataTex = _renderer.GetDataTexure();
    }

    public void RemoveData(RendererHandler<T> handler)
    {
        _renderer.RemoveData(handler);
    }

    public void UpdateData(RendererHandler<T> handler)
    {
        _renderer.UpdateData(handler);
    }
}