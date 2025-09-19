
using Native;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

public class RendererManager : MonoBehaviour, IRendererManager
{
    private List<IRenderer> _renderer;
    private Dictionary<Type, IRenderer> _dynamicAtlasRendererDict;
    private Dictionary<Type, IRenderer> _multiplyMeshRendererDict;
    public GameObject DynamicAtlasRoot;

    public static RendererManager Instance;

    private void Awake()
    {
        Instance = this;
        DynamicAtlasRoot = gameObject;
        _renderer = ListPool<IRenderer>.Get();
        _dynamicAtlasRendererDict = DictionaryPool<Type, IRenderer>.Get();
        _multiplyMeshRendererDict = DictionaryPool<Type, IRenderer>.Get();
    }

    private void OnDestroy()
    {

        foreach (var item in _renderer)
        {
            ReferencePool.Release(item);
        }

        ListPool<IRenderer>.Release(_renderer);
        DictionaryPool<Type, IRenderer>.Release(_dynamicAtlasRendererDict);
        DictionaryPool<Type, IRenderer>.Release(_multiplyMeshRendererDict);
    }

    private void LateUpdate()
    {
        foreach (var item in _renderer)
        {
            item.DoRenderer();
        }
    }

    public DynamiceAtlasRenderer<T> GetOrCreateDynamicAtlasRenderer<T>(string shaderPath = "Unlit/DynamicAtlasRenderer") where T : unmanaged, IDynamicAtlasRendererData
    {
        _dynamicAtlasRendererDict.TryGetValue(typeof(T), out var renderer);

        if (renderer == null)
        {
            renderer = DynamiceAtlasRenderer<T>.Create(shaderPath);
            _dynamicAtlasRendererDict.Add(typeof(T), renderer);
            _renderer.Add(renderer);
        }

        return (DynamiceAtlasRenderer<T>)renderer;
    }

    public MultiplyMeshRenderer<T> GetOrCreateMultiplyRenderer<T>(string shaderPath = "Unlit/MultiplyMeshRenderer") where T : unmanaged, IMultiplyMeshRendererData
    {
        _multiplyMeshRendererDict.TryGetValue(typeof(T), out var renderer);

        if (renderer == null)
        {
            renderer = MultiplyMeshRenderer<T>.Create(shaderPath);
            _multiplyMeshRendererDict.Add(typeof(T), renderer);
            _renderer.Add(renderer);
        }

        return (MultiplyMeshRenderer<T>)renderer;
    }

    public MultiplyMeshRenderer<T> CreateMultiplyRenderer<T>(string shaderPath = "Unlit/MultiplyMeshRenderer") where T : unmanaged, IMultiplyMeshRendererData
    {
        var renderer = MultiplyMeshRenderer<T>.Create(shaderPath);
        _renderer.Add(renderer);
        return (MultiplyMeshRenderer<T>)renderer;
    }
}