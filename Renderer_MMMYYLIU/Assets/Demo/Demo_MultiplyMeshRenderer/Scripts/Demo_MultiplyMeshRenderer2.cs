using Native;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// By mmmyyliu
/// </summary>
public class Demo_MultiplyMeshRenderer2 : MonoBehaviour
{
    public class HandlerAdapter : IReference
    {
        public RendererHandler<MultiplyMeshRendererCommonData> Handler;
        public ECSAnimationScriptObject Target;

        public void Clear()
        {
            Target = null;
        }
    }

    [Range(1, 10000)]
    public int                                                                          EntityCount;
    [SerializeField]
    private ECSAnimationScriptObject[]                                                  _objects;
    private Mesh[]                                                                      _meshs;
    private const int                                                                   OperationCountFrame = 100;
    private int                                                                         _currentCount;
    private Dictionary<ECSAnimationScriptObject, MultiplyMeshRenderer<MultiplyMeshRendererCommonData>>      _renderers;
    private List<HandlerAdapter>                                                        _handler;

    private void Start()
    {
        _renderers = DictionaryPool<ECSAnimationScriptObject, MultiplyMeshRenderer<MultiplyMeshRendererCommonData>>.Get();
        _handler = ListPool<HandlerAdapter>.Get();

        EntityCount = _objects.Length;
        for (int i = 0; i < _objects.Length; i++)
        {
            _renderers.Add(_objects[i], RendererManager.Instance.CreateMultiplyRenderer<MultiplyMeshRendererCommonData>());
        }
    }

    private void OnDestroy()
    {
        DictionaryPool<ECSAnimationScriptObject, MultiplyMeshRenderer<MultiplyMeshRendererCommonData>>.Release(_renderers);
        ListPool<HandlerAdapter>.Release(_handler);
    }

    private void Update()
    {
        if (_currentCount < EntityCount)
        {
            var opCount = math.min(EntityCount - _currentCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount++)
            {
                var @object = _objects[_currentCount % _objects.Length];
                var _renderer = _renderers[@object];
                var handler = _renderer.AddData(@object);
                var handlerAdpater = ReferencePool.Acquire<HandlerAdapter>();
                handlerAdpater.Handler = handler;
                handlerAdpater.Target = @object;                
                _handler.Add(handlerAdpater);
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
                var handlerAdpater = _handler[last];
                var _renderer = _renderers[handlerAdpater.Target];
                _renderer.RemoveData(handlerAdpater.Handler);
                _handler.RemoveAt(last);
            }
        }
    }
}
