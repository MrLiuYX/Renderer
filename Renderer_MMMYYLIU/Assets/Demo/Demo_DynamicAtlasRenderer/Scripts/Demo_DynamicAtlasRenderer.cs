using Native;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// By mmmyyliu
/// 所有图片均由GPT-5生成2
/// 
/// </summary>
public class Demo_DynamicAtlasRenderer : MonoBehaviour
{
    [Range(1, 10000)]
    public int                                                      IconCount;
    [Range(0, 720)]
    private int                                                     RotSpeed;
    [SerializeField]
    private Texture2D[]                                             _allTextures;
    private const int                                               OperationCountFrame = 100;
    private int                                                     _currentCount;
    private DynamiceAtlasRenderer<DynamicAtlasRendererCommonData>   _renderer;
    private List<RendererHandler<DynamicAtlasRendererCommonData>>   _handler;

    private void Start()
    {
        _renderer = RendererManager.Instance.GetOrCreateDynamicAtlasRenderer<DynamicAtlasRendererCommonData>();
        _handler = ListPool<RendererHandler<DynamicAtlasRendererCommonData>>.Get();

        //初始化图集用 图集会有点耗 可以考虑丢入Job计算
        IconCount = _allTextures.Length;
        RotSpeed = 180;
    }

    private void OnDestroy()
    {
        foreach (var item in _handler)
        {
            ReferencePool.Release(item);
        }
        ListPool<RendererHandler<DynamicAtlasRendererCommonData>>.Release(_handler);
    }

    private void Update()
    {
        if (_currentCount < IconCount)
        {
            var opCount = math.min(IconCount - _currentCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount++)
            {
                var handler =_renderer.AddData(_allTextures[_currentCount % _allTextures.Length]);
                _handler.Add(handler);
                handler.Data.SetPosition(new float3(_currentCount % 100, _currentCount / 100, 0));
                handler.Data.SetRotation(new float3(0, UnityEngine.Random.Range(0, 360f), 0));
                _renderer.UpdateData(handler);
            }
        }
        else if(_currentCount > IconCount)
        {
            var opCount = math.min(_currentCount - IconCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount--)
            {
                var last = _handler.Count - 1;
                _renderer.RemoveData(_handler[last]);
                _handler.RemoveAt(last);
            }
        }

        for (int i = 0; i < _handler.Count; i++)
        {
            _handler[i].Data.SetRotation(_handler[i].Data.Rotation + new float3(0, RotSpeed * Time.deltaTime, 0));
            _renderer.UpdateData(_handler[i]);
        }
    }
}
