using UnityEngine;

public interface IRendererManager
{
    public DynamiceAtlasRenderer<T> GetOrCreateDynamicAtlasRenderer<T>(string shaderPath = "Unlit/DynamicAtlasRenderer") where T : unmanaged, IDynamicAtlasRendererData;

    public MultiplyMeshRenderer<T> GetOrCreateMultiplyRenderer<T>(string shaderPath = "Unlit/MultiplyMeshRenderer") where T : unmanaged, IMultiplyMeshRendererData;

    public MultiplyMeshRenderer<T> CreateMultiplyRenderer<T>(string shaderPath = "Unlit/MultiplyMeshRenderer") where T : unmanaged, IMultiplyMeshRendererData;
}
