using Native;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class MultiplyMeshRenderer<T> : IRenderer where T: unmanaged, IMultiplyMeshRendererData
{
    private MultiplyMeshCombiner _combiner;
    private GPUInstanceRenderer<T> _renderer;
    private Material _mat;
    private DynamicAtlas _martixAtlas;

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
        ReferencePool.Release(_martixAtlas);
        ReferencePool.Release(_combiner);
        ReferencePool.Release(_renderer);
    }

    private void InternalCreate(string matPath)
    {
        _mat = new Material(Shader.Find(matPath));
        _combiner = MultiplyMeshCombiner.Create();
        _martixAtlas = DynamicAtlas.Create(256, TextureFormat.RGBAHalf);
        _martixAtlas.RegisterTextureChangedEvent(OnDiffuseTextureChanged);
        _renderer = GPUInstanceRenderer<T>.Create(_mat, _combiner.Mesh);

//#if UNITY_EDITOR
        _rendererInspector = new GameObject($"Renderer_MultiplyMeshRenderer_{typeof(T).Name}").AddOrGetComponent<RendererInspector>();
        _rendererInspector.transform.SetParent(RendererManager.Instance.DynamicAtlasRoot.transform);
        _rendererInspector.Mesh = _combiner.Mesh;
        _rendererInspector.Material = _mat;
        _rendererInspector.DataTex = _renderer.GetDataTexure();
        _renderer.RegisterDataTextureChangedEvent(OnDataTextureChanged);
        _filter = _rendererInspector.gameObject.AddOrGetComponent<MeshFilter>();
        //_rendererInspector.gameObject.AddOrGetComponent<MeshRenderer>();
//#endif
    }

    public void DoRenderer()
    {
        if(_combiner.UpdateImmediately())
        {
            _rendererInspector.Mesh = _combiner.Mesh;
            _filter.mesh = _combiner.Mesh;
            _renderer.UpdateMesh(_combiner.Mesh);
            _mat.SetTexture("_DiffuseAtlas", _combiner.DiffuseAtlas);
        }
        _renderer.DoRenderer();
    }

    public RendererHandler<T> AddData(ECSAnimationScriptObject @object)
    {
        var handler = _renderer.AddData();

        var start = int.MaxValue;
        var end = int.MinValue;
        for (int i = 0; i < @object.MeshPath.Length; i++)
        {
            var mesh = @object.MeshPath[i];
            var diffuse = @object.Diffuse[i];
            var handler2 = _combiner.AddMesh(mesh, diffuse);
            start = math.min(handler2.VertexIndexStart, start);
            end = math.max(handler2.VertexIndexEnd, end);   
        }

        handler.Data.SetVertexStart(start);
        handler.Data.SetVertexEnd(end);

        _martixAtlas.AddTextureToAtlas(@object.Matrix, out var rect, out var pageIndex);
        handler.Data.SetMartixRect(rect);
        handler.Data.SetMatrixPage(pageIndex);

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
        _mat.SetTexture("_MatrixAtlas", _martixAtlas.GetTexture2DArray());
    }
}