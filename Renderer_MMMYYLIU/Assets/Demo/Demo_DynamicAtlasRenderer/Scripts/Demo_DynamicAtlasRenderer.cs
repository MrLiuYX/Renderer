using System;
using Native;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// By mmmyyliu
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
    private NativeList<RendererHandler<DynamicAtlasRendererCommonData>>   _handler;
    private JobHandle _job;

    private void Start()
    {
        _renderer = RendererManager.Instance.GetOrCreateDynamicAtlasRenderer<DynamicAtlasRendererCommonData>();
        _handler = new NativeList<RendererHandler<DynamicAtlasRendererCommonData>>(Allocator.Persistent);
        
        IconCount = _allTextures.Length;
        RotSpeed = 180;
    }

    private void OnDestroy()
    {
        _handler.Dispose();
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
                _handler[_currentCount] = handler;
                _renderer.UpdateData(handler);
            }
        }
        else if(_currentCount > IconCount)
        {
            var opCount = math.min(_currentCount - IconCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount--)
            {
                var last = _handler.Length - 1;
                _renderer.RemoveData(_handler[last]);
                _handler.RemoveAt(last);
            }
        }
        
        _job = new AnimationJob
        {
            RotSpeed = RotSpeed,
            DeltaTime = Time.deltaTime,
            Write = _handler,
        }.Schedule(_handler.Length, 1 << 6);
    }

    private void LateUpdate()
    {
        _job.Complete();
        for (int i = 0; i < _handler.Length; i++)
        {
            _renderer.UpdateData(_handler[i]);
        }
    }

    [BurstCompile]
    private struct AnimationJob : IJobParallelFor
    {
        [ReadOnly] public float RotSpeed;
        [ReadOnly] public float DeltaTime;
        [NativeDisableParallelForRestriction] public NativeList<RendererHandler<DynamicAtlasRendererCommonData>> Write;

        [BurstCompile]
        public void Execute(int index)
        {
            var handler = Write[index];
            handler.Data.SetRotation(Write[index].Data.Rotation + new float3(0, RotSpeed * DeltaTime, 0));
            Write[index] = handler;
        }
    }
}
