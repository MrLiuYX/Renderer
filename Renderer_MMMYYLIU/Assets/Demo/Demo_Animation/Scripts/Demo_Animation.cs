using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Demo_Animation : MonoBehaviour
{
    [Range(1, 10000)]
    public int EntityCount;
    [SerializeField]
    private ECSAnimationScriptObject[] _objects;
    private const int OperationCountFrame = 100;
    private int _currentCount;
    private MultiplyMeshRenderer<MultiplyMeshRendererCommonData> _renderer;
    private NativeList<RendererHandler<MultiplyMeshRendererCommonData>> _handler;
    private JobHandle _job;

    private void Start()
    {
        _renderer = RendererManager.Instance.GetOrCreateMultiplyRenderer<MultiplyMeshRendererCommonData>();
        _handler = new NativeList<RendererHandler<MultiplyMeshRendererCommonData>>(Allocator.Persistent);
        EntityCount = 100;
    }

    private void OnDestroy()
    {
        _handler.Dispose();
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
                var matrix = _objects[_currentCount % _objects.Length].Matrix;
                handler.Data.SetAnimationRow(UnityEngine.Random.Range(0, matrix.height));
                _handler[_currentCount] = handler;
                _renderer.UpdateData(handler);
            }
        }
        else if (_currentCount > EntityCount)
        {
            var opCount = math.min(_currentCount - EntityCount, OperationCountFrame);
            for (int i = 0; i < opCount; i++, _currentCount--)
            {
                var last = _handler.Length - 1;
                _renderer.RemoveData(_handler[last]);
                _handler.RemoveAt(last);
            }
        }

        _job = new AnimationJob
        {
            Write = _handler,
        }
        .Schedule(_handler.Length, 1 << 6);
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
        [NativeDisableParallelForRestriction] public NativeList<RendererHandler<MultiplyMeshRendererCommonData>> Write;

        [BurstCompile]
        public void Execute(int index)
        {
            var handler = Write[index];
            handler.Data.SetAnimationRow(1 * 0.25f + handler.Data.AnimationRow);
            Write[index] = handler;
        }
    }
}
