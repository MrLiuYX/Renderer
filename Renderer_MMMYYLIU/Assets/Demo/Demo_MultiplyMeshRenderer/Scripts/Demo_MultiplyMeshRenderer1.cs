using Native;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// By mmmyyliu
/// 合批版
/// 特殊场合使用
/// </summary>
public class Demo_MultiplyMeshRenderer1 : MonoBehaviour
{
    [Range(1, 10000)]
    public int                                                     EntityCount;
    [SerializeField]
    private Texture2D[]                                             _diffuse;
    [SerializeField]
    private GameObject[]                                            _gos;
    private Mesh[]                                                  _meshs;
    private const int                                               OperationCountFrame = 100;
    private int                                                     _currentCount;
    private MultiplyMeshRenderer<MultiplyMeshRendererCommonData>    _renderer;
    private List<RendererHandler<MultiplyMeshRendererCommonData>>   _handler;

    private void Start()
    {
        _renderer = RendererManager.Instance.GetOrCreateMultiplyRenderer<MultiplyMeshRendererCommonData>();
        _handler = ListPool<RendererHandler<MultiplyMeshRendererCommonData>>.Get();
        EntityCount = _gos.Length;
        _meshs = new Mesh[_gos.Length];
        for (int i = 0; i < _gos.Length; i++)
        {
            _meshs[i] = _gos[i].GetComponent<MeshFilter>().sharedMesh;
        }
    }

    private void OnDestroy()
    {
        foreach (var item in _handler)
        {
            ReferencePool.Release(item);
        }
        ListPool<RendererHandler<MultiplyMeshRendererCommonData>>.Release(_handler);
    }

    private void Update()
    {
        if (_currentCount < EntityCount)
        {
            var opCount = math.min(EntityCount - _currentCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount++)
            {
                var handler = _renderer.AddData(_meshs[_currentCount % _meshs.Length], _diffuse[_currentCount % _diffuse.Length]);
                _handler.Add(handler);
                handler.Data.SetPosition(new float3(_currentCount % 100, _currentCount / 100, 0));
                _renderer.UpdateData(handler);
            }
        }
        else if (_currentCount > EntityCount)
        {
            var opCount = math.min(_currentCount - EntityCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount--)
            {
                var last = _handler.Count - 1;
                _renderer.RemoveData(_handler[last]);
                _handler.RemoveAt(last);
            }
        }
    }
}
