using Native;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// By mmmyyliu
/// </summary>
public class Demo_MultiplyMeshRenderer1 : MonoBehaviour
{
    [Range(1, 10000)]
    public int                                                     EntityCount;
    [SerializeField]
    private ECSAnimationScriptObject[]                                _objects;
    private const int                                               OperationCountFrame = 100;
    private int                                                     _currentCount;
    private MultiplyMeshRenderer<MultiplyMeshRendererCommonData>    _renderer;
    private List<RendererHandler<MultiplyMeshRendererCommonData>>   _handler;

    private void Start()
    {
        _renderer = RendererManager.Instance.GetOrCreateMultiplyRenderer<MultiplyMeshRendererCommonData>();
        _handler = ListPool<RendererHandler<MultiplyMeshRendererCommonData>>.Get();
        EntityCount = 1500;
    }

    private void OnDestroy()
    {
        ListPool<RendererHandler<MultiplyMeshRendererCommonData>>.Release(_handler);
    }

    private void Update()
    {
        if (_currentCount < EntityCount)
        {
            var opCount = math.min(EntityCount - _currentCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount++)
            {
                var handler = _renderer.AddData(_objects[_currentCount % _objects.Length]);
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
