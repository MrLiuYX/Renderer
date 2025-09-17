using Native;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class MultiplyMeshRenderer<T> : IRenderer where T: unmanaged, IMultiplyMeshRendererData
{
    private MultiplyMeshCombiner _combiner;
    private GPUInstanceRenderer<T> _renderer;
    private Material _mat;
    private DynamicAtlas _diffuseAtlas;

    private RendererInspector _rendererInspector;
    private MeshFilter _filter;

    public static MultiplyMeshRenderer<T> Create(string matPath)
    {
        var data = ReferencePool.Acquire<MultiplyMeshRenderer<T>>();
        data.InternalCreate(matPath);
        return data;
    }

    public void Clear()
    {
        GameObject.Destroy(_mat);
        ReferencePool.Release(_diffuseAtlas);
        ReferencePool.Release(_combiner);
        ReferencePool.Release(_renderer);
    }

    private void InternalCreate(string matPath)
    {
        _mat = new Material(Shader.Find(matPath));
        _combiner = MultiplyMeshCombiner.Create();
        _diffuseAtlas = DynamicAtlas.Create();

        _diffuseAtlas.RegisterTextureChangedEvent(OnDiffuseTextureChanged);
        _renderer = GPUInstanceRenderer<T>.Create(_mat, _combiner.Mesh);

#if UNITY_EDITOR
        _rendererInspector = new GameObject($"Renderer_MultiplyMeshRenderer_{typeof(T).Name}").AddOrGetComponent<RendererInspector>();
        _rendererInspector.transform.SetParent(RendererManager.Instance.DynamicAtlasRoot.transform);
        _rendererInspector.Mesh = _combiner.Mesh;
        _rendererInspector.Material = _mat;
        _rendererInspector.DataTex = _renderer.GetDataTexure();
        _rendererInspector.DiffuseTex = _diffuseAtlas.GetTexture2DArray();
        _renderer.RegisterDataTextureChangedEvent(OnDataTextureChanged);
        _filter = _rendererInspector.gameObject.AddOrGetComponent<MeshFilter>();
#endif
    }

    public void DoRenderer()
    {
        if(_combiner.UpdateImmediately())
        {
            _rendererInspector.Mesh = _combiner.Mesh;
            _filter.mesh = _combiner.Mesh;
            _renderer.UpdateMesh(_combiner.Mesh);
        }
        _renderer.DoRenderer();
    }

    public RendererHandler<T> AddData(Mesh mesh, Texture2D diffuse)
    {
        _diffuseAtlas.AddTextureToAtlas(diffuse, out var rect, out int page);
        var handler = _renderer.AddData();
        var handler2 = _combiner.AddMesh(mesh);
        handler.Data.SetVertexStart(handler2.VertexIndexStart);
        handler.Data.SetVertexEnd(handler2.VertexIndexEnd);
        handler.Data.SetDiffuseRect(rect);
        handler.Data.SetDiffusePageIndex(page);
        _renderer.UpdateData(handler);
        return handler;
    }

    public void UpdateData(RendererHandler<T> handler)
    {
        _renderer.UpdateData(handler);
    }

    public void RemoveData(RendererHandler<T> handler)
    {
        _renderer.RemoveData(handler);
    }

    private void OnDataTextureChanged()
    {
        _rendererInspector.DataTex = _renderer.GetDataTexure();
    }

    private void OnDiffuseTextureChanged()
    {
        _rendererInspector.DiffuseTex = _diffuseAtlas.GetTexture2DArray();
        _mat.SetTexture("_DiffuseAtlas", _diffuseAtlas.GetTexture2DArray());
    }
}