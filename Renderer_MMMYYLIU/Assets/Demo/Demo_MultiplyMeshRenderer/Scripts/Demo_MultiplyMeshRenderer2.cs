using Native;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// By mmmyyliu
/// 单独渲染版
/// 更爽更好用
/// </summary>
public class Demo_MultiplyMeshRenderer2 : MonoBehaviour
{
    public class HandlerAdapter : IReference
    {
        public RendererHandler<MultiplyMeshRendererCommonData> Handler;
        public Mesh TargetMesh;

        public void Clear()
        {
            Handler = null;
            TargetMesh = null;
        }
    }

    [Range(1, 10000)]
    public int                                                                          EntityCount;
    [SerializeField]
    private Texture2D[]                                                                 _diffuse;
    [SerializeField]
    private GameObject[]                                                                _gos;
    private Mesh[]                                                                      _meshs;
    private const int                                                                   OperationCountFrame = 100;
    private int                                                                         _currentCount;
    private Dictionary<Mesh, MultiplyMeshRenderer<MultiplyMeshRendererCommonData>>      _renderers;
    private List<HandlerAdapter>                                                        _handler;

    private void Start()
    {
        _renderers = DictionaryPool<Mesh, MultiplyMeshRenderer<MultiplyMeshRendererCommonData>>.Get();
        _handler = ListPool<HandlerAdapter>.Get();

        EntityCount = _gos.Length;
        _meshs = new Mesh[_gos.Length];
        for (int i = 0; i < _gos.Length; i++)
        {
            _meshs[i] = _gos[i].GetComponent<MeshFilter>().sharedMesh;
            _renderers.Add(_meshs[i], RendererManager.Instance.CreateMultiplyRenderer<MultiplyMeshRendererCommonData>());
        }
    }

    private void OnDestroy()
    {
        foreach (var item in _handler)
        {
            ReferencePool.Release(item.Handler);
        }
        DictionaryPool<Mesh, MultiplyMeshRenderer<MultiplyMeshRendererCommonData>>.Release(_renderers);
        ListPool<HandlerAdapter>.Release(_handler);
    }

    private void Update()
    {
        if (_currentCount < EntityCount)
        {
            var opCount = math.min(EntityCount - _currentCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount++)
            {
                var mesh = _meshs[_currentCount % _meshs.Length];
                var _renderer = _renderers[mesh];
                var handler = _renderer.AddData(mesh, _diffuse[_currentCount % _diffuse.Length]);
                var handlerAdpater = ReferencePool.Acquire<HandlerAdapter>();
                handlerAdpater.Handler = handler;
                handlerAdpater.TargetMesh = mesh;                
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
                var _renderer = _renderers[handlerAdpater.TargetMesh];
                _renderer.RemoveData(handlerAdpater.Handler);
                _handler.RemoveAt(last);
            }
        }
    }
}
