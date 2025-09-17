
using Native;

/// <summary>
/// 单个渲染控制器
/// </summary>
public class RendererHandler<T> : IReference where T : unmanaged, IGPUInstanceRendererData
{
    public int Index;
    public T Data;

    public static RendererHandler<T> Create()
    {
        var instance = ReferencePool.Acquire<RendererHandler<T>>();
        return instance;
    }

    public void Clear()
    {

    }
}